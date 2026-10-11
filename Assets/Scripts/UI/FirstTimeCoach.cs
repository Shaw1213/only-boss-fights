using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// The first-time experience: each mechanic is taught the first time a fight puts it in front of the player, and then
    /// the coach gets out of the way. No tutorial level; the Knight is the tutorial.
    ///  - The first white glints slow time, then stop it a few frames before the blow lands: "PARRY NOW". Time moves again
    ///    on the press, so the first parries land and show the timing. After two guided parries the prompt stays (no pause)
    ///    until a parry lands unaided; three misses in a row bring the pause back.
    ///  - The same for red (dash through it), a spell (parry it to take it), a clash (flash counter) and the riposte after
    ///    a parry. Floor marks get slow motion and a prompt; a harvested spell gets a prompt until it's spent.
    /// What the player has shown they know is saved (GameProgress), so nothing is taught twice. Owned and drawn by the
    /// fight HUD (DebugHUD).
    /// </summary>
    public class FirstTimeCoach
    {
        /// <summary>Off for bots and captures (this Play session only).</summary>
        public static bool Suppressed;

        // Lessons (saved by these ids).
        const string Parry = "parry", Riposte = "riposte", Dash = "dash", Harvest = "harvest", Spend = "spend",
                     Counter = "counter", Floor = "floor";
        static readonly string[] All = { Parry, Riposte, Dash, Harvest, Spend, Counter, Floor };

        enum Wait { None, Parry, Dash, Attack }

        const float LeadInScale = 0.3f;    // slow motion from the cue to the pause
        const float FloorScale = 0.45f;    // floor marks: slow motion, no pause
        const int ParryPauseFrames = 3;    // the pause comes this many frames before a parryable blow (inside the parry window)
        const int DashPauseFrames = 4;     // ... and before a red one (past the dash's startup, inside its invulnerability)
        const float ParryPauseGap = 0.45f; // spells: the pause comes when the projectile is this close
        const float DashPauseGap = 0.6f;
        const float Reach = 0f;            // a blow "will reach" if its hitbox (plus the rest of its lunge and this) touches the hero

        // This session (survives retries): how often each lesson was guided, and the misses since.
        static int guidedParries, parryMisses, guidedDashes, dashMisses, guidedHarvests, guidedRipostes, guidedCounters;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            guidedParries = parryMisses = guidedDashes = dashMisses = guidedHarvests = guidedRipostes = guidedCounters = 0;
            Suppressed = false;
        }

        /// <summary>A boss step that is teaching something, from its cue until it's resolved.</summary>
        class Window
        {
            public string lesson;
            public AttackData step;
            public int serial;
            public int heroHp;
            public bool guide;     // slows time (and pauses, for parry / dash / spell)
            public bool pausePast; // the pause point has passed (paused or not)
            public bool guided;    // the player answered the pause
            public bool done;      // answered: the prompt goes
            public bool dashed;
            public Projectile projectile;
            public bool sawProjectile;
        }

        readonly HeroController hero;
        readonly BossController boss;
        readonly FightFlow flow;
        readonly HeroInput input;

        Window window;
        int lastSerial = -1;
        int lastParries, lastHarvests;
        Element lastHeld;
        Wait wait;
        string pausedLesson;

        // The prompt on screen.
        string title, note;
        (string, string)[] keys;
        bool big;
        float shownAt;

        GUIStyle titleBig, titleSmall, capBig, hintBig, capSmall, hintSmall, noteStyle;
        float stylesFor = -1f;

        public FirstTimeCoach(HeroController hero, BossController boss, FightFlow flow, float nameCardSeconds)
        {
            this.hero = hero;
            this.boss = boss;
            this.flow = flow;
            input = hero.InputSource;
            lastParries = hero.ParryCount - hero.ClashCount;
            lastHarvests = hero.HarvestCount;
            // A first-timer's first swing waits for the name card to clear (a retry skips the card: no wait).
            if (!Suppressed && !FightFlow.IsRetry && !AllLearned()) boss.HoldFirstMove(nameCardSeconds + 0.3f);
        }

        static bool Learned(string lesson) => GameProgress.IsLearned(lesson);

        static bool AllLearned()
        {
            foreach (var l in All) if (!Learned(l)) return false;
            return true;
        }

        // ------------------------------------------------------------------ every frame (from the HUD's Update)

        public void Tick()
        {
            if (Suppressed || hero == null || boss == null || flow == null || input == null ||
                flow.Current != FightFlow.Result.Fighting || hero.CurrentState == HeroController.State.Dead)
            {
                if (wait != Wait.None) Resume();
                window = null;
                Show(null, null, null, false);
                return;
            }

            if (wait != Wait.None)
            {
                // Paused: time moves again on the key it asked for (the press stays buffered for the hero).
                float buffer = hero.config.inputBufferSeconds;
                bool pressed = wait == Wait.Parry ? input.HasParry(buffer) : wait == Wait.Dash ? input.HasDash(buffer) : input.HasAttack(buffer);
                if (pressed) Resume();
                return;
            }

            NoteLearned();
            TrackWindow();
            if (wait != Wait.None) return; // the window just paused

            if (window != null && !window.done)
            {
                ShowWindowPrompt(window, false);
                return;
            }
            if (!Learned(Counter) && hero.FlashCounterReady && hero.config.flashCounter != null &&
                Mathf.Abs(boss.transform.position.x - hero.transform.position.x) <= hero.config.flashCounterRange)
            {
                if (guidedCounters < 1 && hero.CurrentState == HeroController.State.Idle && hero.CanDash)
                    Pause(Wait.Dash, Counter, "CLASH!", new[] { ("Space", "flash counter now") }, "Dash right after a clash to slip behind him and strike.");
                else Show("CLASH: DASH", new[] { ("Space", "flash counter") }, null, false);
                return;
            }
            if (!Learned(Riposte) && hero.RiposteReady && boss.IsOpenForRiposte && hero.config.riposte != null && RiposteReaches())
            {
                if (guidedRipostes < 1 && hero.CurrentState == HeroController.State.Idle)
                    Pause(Wait.Attack, Riposte, "PARRIED! HE'S OPEN", new[] { ("LMB J", "riposte now") }, "Strike right after a parry: a riposte hits hard.");
                else Show("RIPOSTE", new[] { ("LMB J", "strike now") }, null, false);
                return;
            }
            if (!Learned(Spend) && hero.HeldElement != Element.None && hero.config.harvestEnabled)
            {
                Show($"YOUR BLADE HOLDS {Name(hero.HeldElement)}", new[] { ("LMB J", "hold, then release: send it at him") },
                     "A riposte carries it too.", false);
                return;
            }
            Show(null, null, null, false);
        }

        /// <summary>What the player just did by themselves (or with help) this frame.</summary>
        void NoteLearned()
        {
            var attack = hero.CurrentAttack;
            if (attack != null && attack == hero.config.riposte) Learn(Riposte);
            if (attack != null && attack == hero.config.flashCounter) Learn(Counter);

            int parries = hero.ParryCount - hero.ClashCount; // the parry button's, not clashes
            if (parries > lastParries)
            {
                lastParries = parries;
                bool helped = window != null && window.guided;
                if (!helped) Learn(Parry);
                else if (window.lesson == Parry) guidedParries++;
                parryMisses = 0;
                if (window != null && (window.lesson == Parry || window.lesson == Harvest)) window.done = true;
            }
            if (hero.HarvestCount > lastHarvests)
            {
                lastHarvests = hero.HarvestCount;
                Learn(Harvest);
            }
            if (lastHeld != Element.None && hero.HeldElement == Element.None) Learn(Spend);
            lastHeld = hero.HeldElement;
        }

        void Learn(string lesson) => GameProgress.MarkLearned(lesson);

        /// <summary>A riposte from here would land (a spell parried from across the arena opens nothing).</summary>
        bool RiposteReaches()
        {
            var r = hero.config.riposte;
            var hit = r.HitboxAt(hero.transform.position, hero.Facing);
            if (hero.Facing > 0) hit.xMax += r.lungeDistance + Reach;
            else hit.xMin -= r.lungeDistance + Reach;
            return hit.Overlaps(boss.Hurtbox);
        }

        // ------------------------------------------------------------------ boss steps that teach

        void TrackWindow()
        {
            if (window != null && Over(window))
            {
                End(window);
                window = null;
            }

            var step = boss.CurrentStep;
            if (window == null && step != null && boss.StepSerial != lastSerial && boss.StateTime >= step.CueStart)
            {
                lastSerial = boss.StepSerial;
                string lesson = LessonFor(step);
                if (lesson != null)
                    window = new Window
                    {
                        lesson = lesson, step = step, serial = boss.StepSerial, heroHp = hero.Health.Current, guide = Guide(lesson),
                    };
            }
            if (window != null) Update(window);
        }

        string LessonFor(AttackData step)
        {
            if (step.cue == TelegraphCue.FloorMarker) return Learned(Floor) ? null : Floor;
            if (!step.parryable) return Learned(Dash) ? null : Dash;
            if (step.element != Element.None && hero.config.harvestEnabled && !Learned(Harvest)) return Harvest;
            if (step.cue == TelegraphCue.WhiteGlint) return Learned(Parry) ? null : Parry; // a spell, once harvesting is known, still teaches the parry
            return null;
        }

        static bool Guide(string lesson) => lesson switch
        {
            Parry => guidedParries < 2 || parryMisses >= 3,
            Dash => guidedDashes < 1 || dashMisses >= 2,
            Harvest => guidedHarvests < 1,
            Floor => true, // slow motion until it's been dodged once
            _ => false,
        };

        void Update(Window w)
        {
            if (hero.CurrentState == HeroController.State.Dash)
            {
                w.dashed = true;
                if (w.lesson == Dash) w.done = true;
            }
            if (w.step.spawnsProjectile && !w.sawProjectile) FindProjectile(w);
            if (!w.guide || w.pausePast || w.done) return;

            if (w.lesson == Floor)
            {
                if (boss.StepSerial == w.serial && boss.StateTime < w.step.ActiveStart) HitStop.SlowMotion(FloorScale, 0.06f);
                else w.pausePast = true;
                return;
            }

            HitStop.SlowMotion(LeadInScale, 0.06f); // the lead-in: the cue, then the blow coming
            if (!AtPausePoint(w)) return;
            w.pausePast = true;
            if (!CanAnswer(w)) return; // mid-swing, too far, rooted: no pause this time, the prompt stays
            Wait key = w.lesson == Dash ? Wait.Dash : Wait.Parry;
            ShowWindowPrompt(w, true);
            Pause(key, w.lesson, title, keys, note);
        }

        bool AtPausePoint(Window w)
        {
            if (w.step.spawnsProjectile)
                return w.projectile != null && Gap(w) <= (w.lesson == Dash ? DashPauseGap : ParryPauseGap);
            if (boss.StepSerial != w.serial || boss.CurrentState != BossController.State.Attacking) return true; // gone: stop slowing
            int lead = w.lesson == Dash ? DashPauseFrames : ParryPauseFrames;
            return boss.StateTime >= w.step.ActiveStart - Frames.ToSeconds(lead);
        }

        bool CanAnswer(Window w)
        {
            var st = hero.CurrentState;
            if (st != HeroController.State.Idle && st != HeroController.State.Charging) return false; // busy, or already answering
            if (w.lesson == Dash && !hero.CanDash) return false;
            if (w.step.spawnsProjectile) return w.projectile != null && Gap(w) > -0.05f;
            if (boss.StepSerial != w.serial || boss.CurrentState != BossController.State.Attacking) return false;
            // Only for a blow that will land: its hitbox, carried forward by the rest of its lunge, must reach the hero.
            var hit = boss.CurrentHitbox;
            var step = w.step;
            float lungeLeft = 0f;
            if (step.lungeDistance > 0f)
            {
                float from = step.lungeDuringActiveOnly ? step.ActiveStart : 0f, span = step.ActiveEnd - from;
                if (span > 0f) lungeLeft = step.lungeDistance * Mathf.Clamp01((step.ActiveEnd - Mathf.Max(boss.StateTime, from)) / span);
            }
            bool heroRight = hero.transform.position.x > boss.transform.position.x;
            if (heroRight) hit.xMax += lungeLeft + Reach;
            else hit.xMin -= lungeLeft + Reach;
            return hit.Overlaps(hero.Hurtbox);
        }

        void FindProjectile(Window w)
        {
            foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (p.Owner != Team.Boss || p.Attack != w.step) continue;
                if (p.Direction != (hero.transform.position.x > p.transform.position.x ? 1 : -1)) continue; // the one coming at the hero
                w.projectile = p;
                w.sawProjectile = true;
                return;
            }
        }

        /// <summary>Distance from the projectile's leading edge to the hero's hurtbox (negative: overlapping or past).</summary>
        float Gap(Window w)
        {
            var p = w.projectile;
            float half = w.step.projectileSize.x * 0.5f, x = p.transform.position.x;
            var body = hero.Hurtbox;
            return p.Direction > 0 ? body.xMin - (x + half) : (x - half) - body.xMax;
        }

        bool Over(Window w)
        {
            if (w.step.spawnsProjectile && w.sawProjectile)
                return w.projectile == null || Gap(w) < -(hero.Hurtbox.width + w.step.projectileSize.x + 0.2f);
            return boss.StepSerial != w.serial || boss.CurrentState != BossController.State.Attacking ||
                   (!w.step.spawnsProjectile && boss.StateTime > w.step.ActiveEnd + 0.05f);
        }

        void End(Window w)
        {
            bool hurt = hero.Health.Current < w.heroHp;
            switch (w.lesson)
            {
                case Parry:
                    if (hurt && !w.guided) parryMisses++;
                    break;
                case Harvest:
                    if (w.guided && w.done) guidedHarvests++;
                    break;
                case Dash:
                    if (!hurt && w.dashed)
                    {
                        if (w.guided) guidedDashes++;
                        else Learn(Dash);
                        dashMisses = 0;
                    }
                    else if (hurt && !w.guided) dashMisses++;
                    break;
                case Floor:
                    if (!hurt) Learn(Floor);
                    break;
            }
        }

        void ShowWindowPrompt(Window w, bool paused)
        {
            string e = Name(w.step.element);
            switch (w.lesson)
            {
                case Parry:
                    if (paused) Show("WHITE GLINT: PARRY IT", new[] { ("RMB K", "parry now") }, "A white glint means you can parry. Press as the blow lands.", true);
                    else Show("WHITE GLINT", new[] { ("RMB K", "parry as the blow lands") }, null, false);
                    break;
                case Harvest:
                    if (paused) Show($"{e} SPELL: PARRY IT", new[] { ("RMB K", "parry now") }, "Parry a spell and your blade keeps it.", true);
                    else Show($"{e} SPELL", new[] { ("RMB K", "parry it to take it") }, null, false);
                    break;
                case Dash:
                {
                    // A wave on the ground: dash through it, toward where it comes from. A blow: any dash.
                    var p = w.projectile;
                    var dashKeys = p != null ? new[] { (p.Direction > 0 ? "A Space" : "D Space", "together: dash through it") } : new[] { ("Space", "dash now") };
                    if (paused) Show("RED: YOU CAN'T PARRY THIS", dashKeys, "Red means dash. Nothing hits you mid-dash.", true);
                    else Show("RED: DASH", w.step.spawnsProjectile ? new[] { ("A D Space", "dash through it") } : dashKeys, null, false);
                    break;
                }
                case Floor:
                    Show("MARKED GROUND", new[] { ("A D", "get out of it"), ("Space", "or dash") }, null, false);
                    break;
            }
        }

        static string Name(Element e) => e == Element.None ? "" : e.ToString().ToUpperInvariant();

        // ------------------------------------------------------------------ pause

        void Pause(Wait key, string lesson, string t, (string, string)[] k, string n)
        {
            wait = key;
            pausedLesson = lesson;
            HitStop.Held = true;
            Show(t, k, n, true);
        }

        void Resume()
        {
            HitStop.Held = false;
            if (pausedLesson == Riposte) guidedRipostes++;
            else if (pausedLesson == Counter) guidedCounters++;
            else if (window != null) window.guided = true;
            wait = Wait.None;
            pausedLesson = null;
            Show(null, null, null, false);
        }

        // ------------------------------------------------------------------ drawing (from the HUD's OnGUI)

        void Show(string t, (string, string)[] k, string n, bool isBig)
        {
            if (t != title || isBig != big) shownAt = Time.unscaledTime;
            title = t;
            keys = k;
            note = n;
            big = isBig;
        }

        /// <summary>The prompt, a third of the way down the screen: a dark band, a gold title, the keys, a line of why.</summary>
        public void Draw(float s)
        {
            if (string.IsNullOrEmpty(title)) return;
            EnsureStyles(s);
            float a = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.15f);
            float cy = Screen.height * 0.3f;
            if (big)
            {
                // Paused: the keys pulse until they're pressed.
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 7f);
                UiKit.Band(cy, (note != null ? 124f : 100f) * s, a, s);
                UiKit.Text(new Rect(0f, cy - (note != null ? 46f : 36f) * s, Screen.width, 36f * s), title, titleBig, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, a), s, 2f);
                float ky = cy - (note != null ? 6f : 0f) * s;
                if (keys != null) UiKit.KeyHints(Screen.width * 0.5f, ky, keys, capBig, hintBig, a * pulse, s * 1.4f);
                if (note != null) UiKit.Text(new Rect(0f, cy + 26f * s, Screen.width, 22f * s), note, noteStyle, new Color(1f, 1f, 1f, a * 0.85f), s);
            }
            else
            {
                UiKit.Band(cy, 70f * s, a * 0.75f, s, false);
                UiKit.Text(new Rect(0f, cy - 27f * s, Screen.width, 26f * s), title, titleSmall, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, a), s, 2f);
                if (keys != null) UiKit.KeyHints(Screen.width * 0.5f, cy + 2f * s, keys, capSmall, hintSmall, a * 0.95f, s * 1.15f);
                if (note != null) UiKit.Text(new Rect(0f, cy + 26f * s, Screen.width, 20f * s), note, noteStyle, new Color(1f, 1f, 1f, a * 0.7f), s);
            }
        }

        void EnsureStyles(float s)
        {
            if (titleBig != null && Mathf.Approximately(stylesFor, s)) return;
            stylesFor = s;
            var baseStyle = new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0), wordWrap = false };
            baseStyle.normal.textColor = Color.white;
            titleBig = new GUIStyle(baseStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(26f * s), fontStyle = FontStyle.Bold };
            titleSmall = new GUIStyle(baseStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(19f * s), fontStyle = FontStyle.Bold };
            if (UiKit.DisplayFont != null) titleBig.font = titleSmall.font = UiKit.DisplayFont;
            // Key caps and their words, sized for KeyHints at 1.4x (paused) and 1.15x the HUD's scale.
            capBig = new GUIStyle(baseStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(11f * 1.4f * s), fontStyle = FontStyle.Bold };
            hintBig = new GUIStyle(baseStyle) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(12f * 1.4f * s) };
            capSmall = new GUIStyle(baseStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(11f * 1.15f * s), fontStyle = FontStyle.Bold };
            hintSmall = new GUIStyle(baseStyle) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(12f * 1.15f * s) };
            noteStyle = new GUIStyle(baseStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(14f * s) };
        }
    }
}
