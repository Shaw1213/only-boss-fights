using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DreamLayerJam
{
    /// <summary>
    /// Animation Gym: reviews every pose, movement cycle and attack of each character on its real frame
    /// data (60 fps), with the same squash/lean the fight applies (<see cref="PoseMotion"/>).
    ///
    /// Shows the phase timeline (startup / active / recovery, telegraph cue, pose changes), hitboxes and
    /// hurtboxes, the feet pivot and floor, an onion skin of the previous pose, and the other character
    /// for scale. Keyboard:
    ///   Tab character · Up/Down clip · Space play/pause · Left/Right step one frame · Home restart
    ///   1-4 speed (0.1x / 0.25x / 0.5x / 1x), [ ] nudge it 5% (tune a cycle's pace by eye) · F flip · H boxes · O onion skin · S squash/lean
    ///   M movement (attacks: apply the lunge; cycles: treadmill, the floor ruler scrolls at the in-game ground speed,
    ///   so a planted foot that doesn't ride the ruler is sliding) · C scale reference · L loop.
    ///   Click or drag the timeline to scrub.
    /// Characters: the hero, the boss, and the hero's relaxed lobby set (at its lobby size and run speed), with other
    /// loops of the lobby run to compare against the one in the game.
    /// </summary>
    public class AnimationGym : MonoBehaviour
    {
        [Header("Data")]
        public HeroConfig heroConfig;
        public BossConfig bossConfig;
        public PoseSet heroPoses;
        public PoseSet bossPoses;
        [Tooltip("The lobby's relaxed set (sword on his back). Size and walk speed copied from the Lobby scene's LobbyHero.")]
        public PoseSet lobbyPoses;
        public float lobbyArtScale = 1f;
        public float lobbyWalkSpeedFactor = 1f;
        [Tooltip("Other loops of the lobby run (same sheet), shown after the one in the game. Built by BuildAnimGym.")]
        public CycleVariant[] lobbyCycleVariants;

        [System.Serializable]
        public class CycleVariant
        {
            public string name;
            public Flipbook cycle;
            [Tooltip("Ground speed (units/s, at size 1) at which this loop plays at its authored rate; 0 = the PoseSet's.")]
            public float cycleSpeed;
        }

        [Header("Scene")]
        public SpriteRenderer subject;
        [Tooltip("Onion skin: the previous pose, faint.")]
        public SpriteRenderer ghost;
        [Tooltip("The other character's idle, faint, for scale.")]
        public SpriteRenderer reference;
        public SpriteRenderer hitbox;
        public SpriteRenderer hurtbox;
        public SpriteRenderer pivotMarker;
        public SpriteRenderer cueMarker;
        public float groundY = -2f;
        public float subjectX = -1.5f;
        public float referenceX = 3.2f;

        enum Kind { Pose, Cycle, Attack }

        class Clip
        {
            public string name;
            public Kind kind;
            public Sprite pose;
            public Flipbook cycle;
            public float cycleSpeed; // a cycle variant's own ground speed at size 1 (0 = the PoseSet's moveCycleSpeed)
            public AttackData[] steps;

            public float Duration
            {
                get
                {
                    switch (kind)
                    {
                        case Kind.Pose: return 1f;
                        case Kind.Cycle: return cycle.Duration;
                        default:
                            float t = 0f;
                            foreach (var s in steps) t += s.TotalTime;
                            return t;
                    }
                }
            }
        }

        class Character
        {
            public string name;
            public bool isBoss;
            public PoseSet poses;
            public Vector2 hurtboxOffset, hurtboxSize; // zero = none (the lobby)
            public float artScale = 1f;   // size in its scene relative to the PoseSet (the lobby draws the hero bigger)
            public float groundSpeed;     // full walking speed in its scene, units/s
            public int referenceIndex;    // the character drawn beside it for scale
            public readonly List<Clip> clips = new List<Clip>();
        }

        /// <summary>Everything the view needs at one moment of a clip.</summary>
        struct Sample
        {
            public Sprite sprite;
            public float scaleMul;
            public PoseMotion.Motion motion;
            public float lunge;
            public AttackData step;
            public int stepIndex;
            public float local;
            public AttackPhase phase;
            public int cycleFrame;
        }

        readonly List<Character> characters = new List<Character>();
        int charIndex;
        int clipIndex;
        float time;
        bool playing = true;
        bool loop = true;
        float speed = 1f;
        int facing = 1;
        bool showBoxes = true;
        bool showOnion;
        bool squash = true;
        bool applyLunge;
        bool showReference = true;
        Rect timelineRect;
        float breathTime;          // breathing runs on its own clock (a pose clip loops every second)
        int loops;                 // completed loops of the current clip, so the treadmill keeps scrolling
        readonly List<Transform> rulerTicks = new List<Transform>();
        readonly List<float> rulerHomeX = new List<float>();
        const float RulerMin = -6f, RulerSpan = 15f; // a multiple of 5, so the long ticks stay every 5 units
        GUIStyle small, bold, right;
        readonly StringBuilder sb = new StringBuilder();

        // Same polish as the fight: eased squash/lean while playing (snapped when paused or scrubbing),
        // idle breathing, and the slash arc over active frames.
        const float MotionEase = 30f;
        static readonly Color HeroSlash = new Color(0.7f, 1f, 1f, 0.85f);
        SpriteRenderer arc;
        Vector3 easedScale = Vector3.one;
        float easedLean;

        Character Current => characters[charIndex];
        Clip CurrentClip => Current.clips[clipIndex];

        void Awake()
        {
            characters.Add(BuildHero());
            characters.Add(BuildBoss());
            if (lobbyPoses != null && lobbyPoses.IsUsable) characters.Add(BuildLobbyHero());
            foreach (Transform child in transform)
                if (child.name.StartsWith("Tick "))
                {
                    rulerTicks.Add(child);
                    rulerHomeX.Add(child.position.x);
                }
            arc = new GameObject("SlashArc").AddComponent<SpriteRenderer>();
            arc.transform.SetParent(transform, false);
            arc.sortingOrder = 7;
        }

        Character BuildHero()
        {
            var c = new Character { name = "Hero", poses = heroPoses, hurtboxOffset = heroConfig.hurtboxOffset, hurtboxSize = heroConfig.hurtboxSize,
                                    groundSpeed = heroConfig.moveSpeed, referenceIndex = 1 };
            AddStatePoses(c);
            if (heroPoses.move != null && heroPoses.move.IsValid) c.clips.Add(new Clip { name = "Move cycle", kind = Kind.Cycle, cycle = heroPoses.move });
            for (int i = 0; i < heroConfig.lightCombo.Length; i++) AddAttack(c, $"Light {i + 1}", heroConfig.lightCombo[i]);
            if (heroConfig.lightCombo.Length > 0) c.clips.Add(new Clip { name = "Light combo (1-2-3)", kind = Kind.Attack, steps = heroConfig.lightCombo });
            AddAttack(c, "Charged heavy", heroConfig.chargedHeavy);
            AddAttack(c, "Riposte", heroConfig.riposte);
            AddAttack(c, "Flash counter", heroConfig.flashCounter);
            AddAttack(c, "Dash attack", heroConfig.dashAttack);
            return c;
        }

        Character BuildBoss()
        {
            var c = new Character { name = bossConfig.displayName, isBoss = true, poses = bossPoses, hurtboxOffset = bossConfig.hurtboxOffset, hurtboxSize = bossConfig.hurtboxSize,
                                    groundSpeed = bossConfig.walkSpeed, referenceIndex = 0 };
            AddStatePoses(c);
            if (bossPoses.move != null && bossPoses.move.IsValid) c.clips.Add(new Clip { name = "Move cycle", kind = Kind.Cycle, cycle = bossPoses.move });
            var seen = new HashSet<BossMove>();
            foreach (var moves in new[] { bossConfig.phase1Moves, bossConfig.phase2Moves })
                if (moves != null)
                    foreach (var m in moves)
                        if (m != null && m.steps != null && m.steps.Length > 0 && seen.Add(m))
                            c.clips.Add(new Clip { name = m.name.Replace("B1_Move_", ""), kind = Kind.Attack, steps = m.steps });
            return c;
        }

        /// <summary>The lobby set at its lobby size; the fight hero beside it at the same size, to compare the two drawings.</summary>
        Character BuildLobbyHero()
        {
            var c = new Character { name = "Hero (lobby)", poses = lobbyPoses, artScale = lobbyArtScale,
                                    groundSpeed = heroConfig.moveSpeed * lobbyWalkSpeedFactor, referenceIndex = 0 };
            c.clips.Add(new Clip { name = "Idle (relaxed)", kind = Kind.Pose, pose = lobbyPoses.idle });
            if (lobbyPoses.move != null && lobbyPoses.move.IsValid) c.clips.Add(new Clip { name = "Walk (in the game)", kind = Kind.Cycle, cycle = lobbyPoses.move });
            if (lobbyCycleVariants != null)
                foreach (var v in lobbyCycleVariants)
                    if (v != null && v.cycle != null && v.cycle.IsValid)
                        c.clips.Add(new Clip { name = v.name, kind = Kind.Cycle, cycle = v.cycle, cycleSpeed = v.cycleSpeed });
            return c;
        }

        static void AddStatePoses(Character c)
        {
            var p = c.poses;
            void Add(string label, Sprite s) { if (s != null) c.clips.Add(new Clip { name = label, kind = Kind.Pose, pose = s }); }
            Add("Idle", p.idle);
            Add("Dash", p.dash);
            Add("Charge", p.charge);
            Add("Parry", p.parry);
            Add("Hurt", p.hurt);
            Add("Staggered", p.staggered);
            Add("Dead", p.dead);
            Add("Phase change", p.phaseChange);
        }

        static void AddAttack(Character c, string label, AttackData a)
        {
            if (a != null) c.clips.Add(new Clip { name = label, kind = Kind.Attack, steps = new[] { a } });
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            HandleKeys();
            float duration = CurrentClip.Duration;
            if (playing)
            {
                time += Time.unscaledDeltaTime * speed;
                breathTime += Time.unscaledDeltaTime * speed;
                if (time >= duration)
                {
                    if (loop) loops += Mathf.FloorToInt(time / duration);
                    time = loop ? Mathf.Repeat(time, duration) : duration;
                }
            }
            time = Mathf.Clamp(time, 0f, duration);
            Render();
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame) SelectCharacter((charIndex + 1) % characters.Count);
            if (kb.downArrowKey.wasPressedThisFrame) SelectClip((clipIndex + 1) % Current.clips.Count);
            if (kb.upArrowKey.wasPressedThisFrame) SelectClip((clipIndex - 1 + Current.clips.Count) % Current.clips.Count);
            if (kb.spaceKey.wasPressedThisFrame) playing = !playing;
            if (kb.rightArrowKey.wasPressedThisFrame) Step(+1);
            if (kb.leftArrowKey.wasPressedThisFrame) Step(-1);
            if (kb.homeKey.wasPressedThisFrame) time = 0f;
            if (kb.digit1Key.wasPressedThisFrame) speed = 0.1f;
            if (kb.digit2Key.wasPressedThisFrame) speed = 0.25f;
            if (kb.digit3Key.wasPressedThisFrame) speed = 0.5f;
            if (kb.digit4Key.wasPressedThisFrame) speed = 1f;
            if (kb.leftBracketKey.wasPressedThisFrame) speed = Mathf.Max(0.05f, Mathf.Round((speed - 0.05f) * 20f) / 20f);
            if (kb.rightBracketKey.wasPressedThisFrame) speed = Mathf.Min(2f, Mathf.Round((speed + 0.05f) * 20f) / 20f);
            if (kb.fKey.wasPressedThisFrame) facing = -facing;
            if (kb.hKey.wasPressedThisFrame) showBoxes = !showBoxes;
            if (kb.oKey.wasPressedThisFrame) showOnion = !showOnion;
            if (kb.sKey.wasPressedThisFrame) squash = !squash;
            if (kb.mKey.wasPressedThisFrame) applyLunge = !applyLunge;
            if (kb.cKey.wasPressedThisFrame) showReference = !showReference;
            if (kb.lKey.wasPressedThisFrame) loop = !loop;
        }

        void Step(int frames)
        {
            playing = false;
            time = Mathf.Clamp(Mathf.Round(time * Frames.PerSecond + frames) / Frames.PerSecond, 0f, CurrentClip.Duration);
        }

        public void SelectCharacter(int index)
        {
            charIndex = index;
            clipIndex = 0;
            time = 0f;
            loops = 0;
        }

        public void SelectClip(int index)
        {
            clipIndex = index;
            time = 0f;
            loops = 0;
        }

        /// <summary>Ground covered per second of cycle time (the cycle plays at its authored rate at moveCycleSpeed).</summary>
        static float CycleGroundSpeed(Character c, Clip clip) => (clip.cycleSpeed > 0f ? clip.cycleSpeed : c.poses.moveCycleSpeed) * c.artScale;

        /// <summary>How many times its authored rate the cycle plays at the character's full walking speed.</summary>
        static float CycleRate(Character c, Clip clip) => CycleGroundSpeed(c, clip) > 0f ? c.groundSpeed / CycleGroundSpeed(c, clip) : 1f;

        // ------------------------------------------------------------------ sampling

        Sample Evaluate(Clip clip, float t)
        {
            var s = new Sample { scaleMul = 1f, motion = PoseMotion.Motion.None, stepIndex = -1, phase = AttackPhase.None, cycleFrame = -1 };
            var poses = Current.poses;
            switch (clip.kind)
            {
                case Kind.Pose:
                    s.sprite = clip.pose;
                    break;
                case Kind.Cycle:
                    s.sprite = clip.cycle.Sample(t);
                    s.scaleMul = clip.cycle.scale;
                    s.cycleFrame = System.Array.IndexOf(clip.cycle.frames, s.sprite);
                    break;
                case Kind.Attack:
                {
                    float local = t;
                    float lunge = 0f;
                    int i = 0;
                    while (i < clip.steps.Length - 1 && local >= clip.steps[i].TotalTime)
                    {
                        lunge += clip.steps[i].lungeDistance;
                        local -= clip.steps[i].TotalTime;
                        i++;
                    }
                    var step = clip.steps[i];
                    s.step = step;
                    s.stepIndex = i;
                    s.local = local;
                    s.phase = step.PhaseAt(local);
                    if (s.phase == AttackPhase.None) s.phase = AttackPhase.Recovery; // the very end of the clip
                    s.sprite = PoseSet.First(step.PoseAt(local), poses.idle);
                    s.scaleMul = step.PoseScaleAt(local);
                    s.lunge = lunge + LungeTraveled(step, local);
                    s.motion = Current.isBoss
                        ? PoseMotion.BossAttack(s.phase, step.StartupTime > 0f ? Mathf.Clamp01(local / step.StartupTime) : 1f, i == clip.steps.Length - 1)
                        : PoseMotion.HeroAttack(s.phase);
                    break;
                }
            }
            if (s.sprite == null) s.sprite = poses.idle;
            return s;
        }

        static float LungeTraveled(AttackData a, float t)
        {
            if (a.lungeDistance == 0f) return 0f;
            float start = a.lungeDuringActiveOnly ? a.ActiveStart : 0f;
            float end = a.ActiveEnd;
            return end > start ? a.lungeDistance * Mathf.Clamp01((t - start) / (end - start)) : 0f;
        }

        // ------------------------------------------------------------------ view

        void Render()
        {
            var c = Current;
            var clip = CurrentClip;
            var s = Evaluate(clip, time);
            float baseScale = BaseScale(c);
            float x = subjectX + (applyLunge ? s.lunge * facing : 0f) + (squash ? s.motion.lean * facing : 0f);

            subject.sprite = s.sprite;
            subject.flipX = facing < 0;
            var deform = squash ? PoseMotion.Deform(Vector3.one, s.motion.squash.x, s.motion.squash.y, true) : Vector3.one;
            if (squash && clip.kind == Kind.Pose && clip.pose == c.poses.idle)
                deform.y *= 1f + c.poses.idleBreathAmount * Mathf.Sin(breathTime * c.poses.idleBreathRate); // breathing, as in the game
            float lean = squash ? s.motion.lean : 0f;
            float ease = playing ? 1f - Mathf.Exp(-Time.unscaledDeltaTime * speed * MotionEase) : 1f;
            easedScale = Vector3.Lerp(easedScale, deform, ease);
            easedLean = Mathf.Lerp(easedLean, lean, ease);
            x += (easedLean - lean) * facing;
            subject.transform.localScale = Vector3.Scale(Vector3.one * baseScale * s.scaleMul, easedScale);
            subject.transform.position = new Vector3(x, groundY, 0f);

            // Onion skin: the pose shown before the current one.
            ghost.enabled = showOnion && clip.kind != Kind.Pose;
            if (ghost.enabled)
            {
                Sample prev = s;
                for (float t = time; t >= 0f; t -= 1f / Frames.PerSecond)
                {
                    prev = Evaluate(clip, t);
                    if (prev.sprite != s.sprite) break;
                }
                ghost.enabled = prev.sprite != s.sprite;
                ghost.sprite = prev.sprite;
                ghost.flipX = facing < 0;
                ghost.color = new Color(0.55f, 0.85f, 1f, 0.35f);
                ghost.transform.localScale = Vector3.one * baseScale * prev.scaleMul;
                ghost.transform.position = new Vector3(subjectX + (applyLunge ? prev.lunge * facing : 0f), groundY, 0f);
            }

            // The other character, for scale (drawn at the subject's scene size, so the lobby set sits beside the fight
            // hero at the same pixel scale).
            var other = characters[c.referenceIndex];
            reference.enabled = showReference && other.poses != null && other.poses.idle != null;
            if (reference.enabled)
            {
                reference.sprite = other.poses.idle;
                reference.flipX = facing > 0; // faces the subject
                reference.color = new Color(1f, 1f, 1f, 0.35f);
                reference.transform.localScale = Vector3.one * other.poses.Scale * c.artScale;
                reference.transform.position = new Vector3(subjectX + referenceX * c.artScale * facing, groundY, 0f);
            }

            // Treadmill: the floor ruler scrolls back at the ground speed the cycle is played for.
            float scroll = applyLunge && clip.kind == Kind.Cycle ? (loops * clip.Duration + time) * CycleGroundSpeed(c, clip) * facing : 0f;
            for (int i = 0; i < rulerTicks.Count; i++)
            {
                var p = rulerTicks[i].position;
                p.x = scroll == 0f ? rulerHomeX[i] : RulerMin + Mathf.Repeat(rulerHomeX[i] - RulerMin - scroll, RulerSpan);
                rulerTicks[i].position = p;
            }

            var feet = new Vector2(x, groundY);
            pivotMarker.transform.position = new Vector3(x, groundY, 0f);

            hurtbox.enabled = showBoxes && c.hurtboxSize != Vector2.zero;
            if (hurtbox.enabled) Place(hurtbox, CombatBox.FromLocal(feet, c.hurtboxOffset, c.hurtboxSize, facing));

            // Slash arc: from the first active frame, fading over its duration (as in the fight).
            float sinceActive = s.step != null ? s.local - s.step.ActiveStart : -1f;
            arc.enabled = s.step != null && sinceActive >= 0f && sinceActive < 0.16f;
            if (arc.enabled)
            {
                var trail = !c.isBoss ? HeroSlash : s.step.element != Element.None ? ElementColors.Of(s.step.element) : s.step.parryable ? Color.white : new Color(1f, 0.15f, 0.1f);
                if (c.isBoss) trail.a = 0.8f;
                SlashArc.Place(arc, s.step.HitboxAt(feet, facing), facing, trail, sinceActive / 0.16f);
            }

            bool active = s.step != null && s.phase == AttackPhase.Active;
            hitbox.enabled = showBoxes && active;
            if (hitbox.enabled)
            {
                hitbox.color = s.step.parryable ? new Color(1f, 1f, 1f, 0.35f) : new Color(1f, 0.2f, 0.1f, 0.4f);
                Place(hitbox, s.step.HitboxAt(feet, facing));
            }

            // Telegraph cue: the white glint (parryable) or red glow (dash) from the cue frame to the hit.
            bool cue = s.step != null && s.phase == AttackPhase.Startup && s.step.cue != TelegraphCue.None && s.local >= s.step.CueStart;
            cueMarker.enabled = cue && s.step.cue != TelegraphCue.FloorMarker;
            if (cueMarker.enabled)
            {
                cueMarker.color = s.step.cue == TelegraphCue.RedGlow ? new Color(1f, 0.2f, 0.1f) : Color.white;
                cueMarker.transform.position = new Vector3(x + (c.hurtboxSize.x * 0.5f + 0.2f) * facing, groundY + c.hurtboxSize.y + 0.3f, 0f);
            }
            subject.color = cue && s.step.cue == TelegraphCue.RedGlow ? Color.Lerp(Color.white, new Color(1f, 0.15f, 0.1f), 0.45f) : Color.white;
        }

        static void Place(SpriteRenderer r, Rect rect)
        {
            r.transform.position = new Vector3(rect.center.x, rect.center.y, 0f);
            r.transform.localScale = new Vector3(rect.width, rect.height, 1f);
        }

        // ------------------------------------------------------------------ UI

        void OnGUI()
        {
            if (characters.Count == 0) return;
            float u = Mathf.Max(0.5f, Mathf.Min(Screen.height / 540f, Screen.width / 960f));
            EnsureStyles(u);
            var c = Current;
            var clip = CurrentClip;
            var s = Evaluate(clip, time);

            // Character + clip list (left)
            float x = 12f * u, y = 10f * u, w = 230f * u, line = 19f * u;
            GUI.Label(new Rect(x, y, 520f * u, line), $"ANIMATION GYM  ·  {c.name}  (Tab)", bold);
            y += line + 4f * u;
            for (int i = 0; i < c.clips.Count; i++)
            {
                var r = new Rect(x, y, w, line);
                if (i == clipIndex) Fill(r, new Color(0.3f, 0.85f, 0.95f, 0.25f));
                if (GUI.Button(r, $"  {c.clips[i].name}", small)) SelectClip(i);
                y += line;
            }

            // Readout (top right)
            sb.Clear();
            int frame = Mathf.FloorToInt(time * Frames.PerSecond);
            int total = Mathf.Max(1, Mathf.RoundToInt(clip.Duration * Frames.PerSecond));
            sb.Append($"{clip.name}   f {frame} / {total}   ({time * 1000f:0} ms)   x{speed:0.##} {(playing ? "playing" : "paused")}\n");
            sb.Append($"pose: {(s.sprite != null ? s.sprite.name : "-")}\n");
            if (clip.kind == Kind.Cycle)
            {
                float ms = clip.cycle.frameSeconds != null && s.cycleFrame >= 0 && s.cycleFrame < clip.cycle.frameSeconds.Length ? clip.cycle.frameSeconds[s.cycleFrame] * 1000f : 100f;
                float rate = CycleRate(c, clip);
                sb.Append($"frame {s.cycleFrame + 1}/{clip.cycle.frames.Length} ({s.sprite.name})   {ms:0} ms = {ms * 0.06f:0.#} f   size x{clip.cycle.scale:0.##}{(c.artScale != 1f ? $" · scene size x{c.artScale:0.##}" : "")}\n");
                sb.Append($"in game: x{rate:0.#} at full speed ({c.groundSpeed:0.#} u/s) = {clip.Duration / rate:0.00} s per cycle, this frame {ms / rate:0} ms · {CycleGroundSpeed(c, clip) * clip.Duration:0.00} units per cycle\n");
                float stepsPerSecond = clip.cycle.footfallsPerLoop * rate / Mathf.Max(0.01f, clip.Duration);
                sb.Append($"cadence {stepsPerSecond:0.0} steps/s in game · playing now at {stepsPerSecond * speed / rate:0.0} steps/s ([ ] to tune)\n");
            }
            else if (clip.kind == Kind.Pose && clip.pose == c.poses.idle)
                sb.Append($"height {s.sprite.bounds.size.y * BaseScale(c):0.00} units · breathing {c.poses.idleBreathAmount * 100f:0.#}% every {2f * Mathf.PI / Mathf.Max(0.01f, c.poses.idleBreathRate):0.0} s\n");
            else if (s.step != null)
            {
                var st = s.step;
                sb.Append($"step {s.stepIndex + 1}/{clip.steps.Length}: {st.name}   {s.phase}\n");
                sb.Append($"startup {st.startupFrames}f   active {st.activeFrames}f   recovery {st.recoveryFrames}f   dmg {st.damage}" +
                          $"{(st.parryable ? "" : "   UNPARRYABLE")}{(st.element != Element.None ? "   " + st.element : "")}{(st.lungeDistance != 0f ? $"   lunge {st.lungeDistance:0.#}" : "")}\n");
                if (st.HasAnimation)
                    sb.Append($"animation: {st.animation.frames.Length} frames retimed, hit frame {st.animationHitFrame + 1} on the first active frame, size x{st.animation.scale:0.##}" +
                              $"{(st.animationThroughRecovery ? "" : $" · recovery {Name(PoseSet.First(st.recoveryPose, st.activePose, st.startupPose))}")}\n");
                else
                    sb.Append($"poses: startup {Name(st.startupPose)} · active {Name(PoseSet.First(st.activePose, st.startupPose))} · recovery {Name(PoseSet.First(st.recoveryPose, st.activePose, st.startupPose))}\n");
            }
            GUI.Label(new Rect(Screen.width - 620f * u, 10f * u, 608f * u, 110f * u), sb.ToString(), right);

            DrawTimeline(clip, u);

            string toggles = $"F facing {(facing > 0 ? "→" : "←")} · H boxes {(showBoxes ? "on" : "off")} · O onion {(showOnion ? "on" : "off")} · S squash {(squash ? "on" : "off")} · " +
                             $"M {(CurrentClip.kind == Kind.Cycle ? "treadmill" : "lunge")} {(applyLunge ? "on" : "off")} · C scale ref {(showReference ? "on" : "off")} · L loop {(loop ? "on" : "off")}";
            GUI.Label(new Rect(12f * u, Screen.height - 24f * u, Screen.width - 24f * u, 20f * u),
                "Space play · ←/→ frame · Home restart · 1-4 speed, [ ] ±5% · ↑/↓ clip   |   " + toggles, small);
        }

        static string Name(Sprite s) => s != null ? s.name : "(idle)";
        static float BaseScale(Character c) => c.poses.Scale * c.artScale;

        void DrawTimeline(Clip clip, float u)
        {
            float duration = clip.Duration;
            timelineRect = new Rect(12f * u, Screen.height - 74f * u, Screen.width - 24f * u, 22f * u);
            var r = timelineRect;
            Fill(r, new Color(0f, 0f, 0f, 0.55f));
            float Px(float t) => r.x + r.width * (duration > 0f ? t / duration : 0f);

            if (clip.kind == Kind.Attack)
            {
                float t0 = 0f;
                foreach (var st in clip.steps)
                {
                    Segment(r, Px(t0), Px(t0 + st.ActiveStart), new Color(0.95f, 0.7f, 0.2f, 0.85f));
                    Segment(r, Px(t0 + st.ActiveStart), Px(t0 + st.ActiveEnd), new Color(0.95f, 0.2f, 0.15f, 0.95f));
                    Segment(r, Px(t0 + st.ActiveEnd), Px(t0 + st.TotalTime), new Color(0.35f, 0.5f, 0.8f, 0.85f));
                    if (st.cue != TelegraphCue.None)
                        Fill(new Rect(Px(t0 + st.CueStart) - 1.5f * u, r.y - 6f * u, 3f * u, 6f * u), st.cue == TelegraphCue.RedGlow ? Color.red : Color.white);
                    t0 += st.TotalTime;
                }
            }
            else if (clip.kind == Kind.Cycle)
            {
                float t0 = 0f;
                for (int i = 0; i < clip.cycle.frames.Length; i++)
                {
                    float d = clip.cycle.frameSeconds != null && i < clip.cycle.frameSeconds.Length && clip.cycle.frameSeconds[i] > 0f ? clip.cycle.frameSeconds[i] : 0.1f;
                    Segment(r, Px(t0) + 1f, Px(t0 + d), i % 2 == 0 ? new Color(0.3f, 0.75f, 0.85f, 0.8f) : new Color(0.2f, 0.55f, 0.65f, 0.8f));
                    GUI.Label(new Rect(Px(t0) + 3f * u, r.y + 2f * u, 40f * u, r.height), (i + 1).ToString(), small);
                    t0 += d;
                }
            }
            else Segment(r, r.x, r.xMax, new Color(0.5f, 0.5f, 0.55f, 0.8f));

            // Pose changes (white ticks under the bar) and 60 fps frame ticks every 10 frames.
            Sprite last = null;
            for (float t = 0f; t <= duration; t += 1f / Frames.PerSecond)
            {
                var sp = Evaluate(clip, t).sprite;
                if (sp != last && t > 0f) Fill(new Rect(Px(t) - 1f, r.yMax, 2f, 6f * u), Color.white);
                last = sp;
            }
            for (int f = 0; f <= duration * Frames.PerSecond; f += 10) Fill(new Rect(Px(f / Frames.PerSecond), r.y, 1f, 4f * u), new Color(1f, 1f, 1f, 0.5f));

            Fill(new Rect(Px(time) - 1.5f * u, r.y - 3f * u, 3f * u, r.height + 6f * u), Color.yellow);
            GUI.Label(new Rect(r.x, r.y - 20f * u, 600f * u, 18f * u), "timeline: startup (amber) · active (red) · recovery (blue) · cue (tick above) · pose change (tick below)", small);

            // Scrub with the mouse.
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && r.Contains(e.mousePosition))
            {
                playing = false;
                time = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width) * duration;
                e.Use();
            }
        }

        static void Segment(Rect r, float x0, float x1, Color c) => Fill(new Rect(x0, r.y, Mathf.Max(1f, x1 - x0), r.height), c);

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void EnsureStyles(float u)
        {
            int size = Mathf.RoundToInt(13f * u);
            if (small != null && small.fontSize == size) return;
            small = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleLeft };
            small.normal.textColor = Color.white;
            small.hover.textColor = new Color(0.6f, 0.95f, 1f);
            bold = new GUIStyle(small) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(15f * u) };
            right = new GUIStyle(small) { alignment = TextAnchor.UpperRight, wordWrap = false };
        }
    }
}
