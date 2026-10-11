using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// Boss state machine: Idle → Approach → Attacking (each step: windup / active / recovery) → Idle,
    /// interrupted by Staggered, PhaseTransition and Dead. Bosses have super armor: hits deal damage and
    /// stagger but don't interrupt a move. All numbers come from BossConfig / BossMove / AttackData.
    /// Harvested elements land on the boss as statuses: burn (damage ticks), freeze (its clock stops),
    /// root (it can't move). Their numbers live in the hero's HeroConfig (Harvest).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class BossController : MonoBehaviour, IHittable
    {
        public enum State { Idle, Approach, Attacking, Staggered, PhaseTransition, Dead }

        [Header("Data")]
        public BossConfig config;
        public FeedbackConfig feedback;

        [Header("Scene")]
        public HeroController hero;
        public Arena arena;

        [Header("Greybox visuals")]
        public Transform body;
        public SpriteFlash flash;
        public SpriteRenderer glint;
        public SpriteRenderer floorMarker;
        public SpriteRenderer hitboxDebug;
        public Sprite projectileSprite;
        public Color phase1Color = new Color(0.62f, 0.42f, 0.85f);
        public Color phase2Color = new Color(0.85f, 0.30f, 0.30f);
        public Color redGlowColor = new Color(1f, 0.15f, 0.1f);
        [Tooltip("Shown during the last step's recovery: the punish window.")]
        public Color openColor = new Color(0.45f, 0.45f, 0.55f);
        public Color staggerColor = new Color(1f, 0.85f, 0.3f);
        public Color deadColor = new Color(0.3f, 0.3f, 0.32f);

        [Header("Art (optional)")]
        [Tooltip("When set, the body shows pose sprites (feet pivot, facing right) instead of the greybox box. Attack poses come from each AttackData.")]
        public PoseSet poses;

        Health health;
        State state;
        float stateTime;
        float prevStateTime;
        float stateDuration;
        int facing = -1;
        float flinchTimer; // after a combo break: the hurt pose while idle
        float stepDistance; // ground walked since the last footstep sound

        BossMove move;
        BossMove lastMove;
        int stepIndex;
        bool stepResolved;
        bool stepSpawned;
        int parriedThisMove;
        float stepTargetX; // atHero steps: where the hero stood when the windup began
        bool finaleUsed;
        bool finaleHolding;
        float airHeight; // leaps: height above the ground (the hurtbox goes up with him, out of reach)
        Color markerBaseColor;
        SpriteRenderer frostPatch; // the IceSpikes floor style's telegraph (created on first use)
        float lastStaggerDamageTime = float.NegativeInfinity;

        // Statuses from harvested elements
        float frozenTimer;
        float rootTimer;
        int burnTicksLeft;
        float burnTimer;

        Vector3 bodyBaseScale;
        Vector3 bodyBasePos;

        // Art mode: pose sprites replace the greybox box. Squash/stretch and tints are toned down for art (PoseMotion).
        bool artMode;
        SpriteRenderer bodyRenderer;
        Sprite greyboxSprite;
        Vector3 greyboxScale;
        Vector3 greyboxPos;
        float moveCycleTime;

        // Polish: squash/lean eased over ~2 frames (pose changes don't snap), idle breathing, slash arcs.
        const float MotionEase = 30f;
        Vector3 easedScale;
        Vector3 easedPos;
        SlashArc slashArc;
        bool stepArcPlayed;

        public State CurrentState => state;
        public int Phase { get; private set; } = 1;
        public float Stagger { get; private set; }
        public float StaggerNormalized => config.staggerMax > 0f ? Stagger / config.staggerMax : 0f;
        public BossMove CurrentMove => state == State.Attacking ? move : null;
        public int StepIndex => stepIndex;
        /// <summary>Counts every step begun this fight: a new value means a new swing, even the same one again.</summary>
        public int StepSerial { get; private set; }
        public AttackData CurrentStep => state == State.Attacking && move != null ? move.steps[stepIndex] : null;
        public AttackPhase CurrentPhase => CurrentStep != null ? CurrentStep.PhaseAt(stateTime) : AttackPhase.None;
        /// <summary>Where the current step's hit lands (also during its windup), in world space.</summary>
        public Rect CurrentHitbox => CurrentStep != null ? StepHitbox(CurrentStep) : default;
        public float StateTime => stateTime;
        public Health Health => health;
        public bool IsFrozen => frozenTimer > 0f;
        public bool IsRooted => rootTimer > 0f;
        public bool IsBurning => burnTicksLeft > 0;
        /// <summary>The finale's last stand is holding him still (BossConfig.finaleAtHealth).</summary>
        public bool IsHeld => state == State.Staggered && finaleHolding;
        public bool FinaleUsed => finaleUsed;

        /// <summary>
        /// True when a riposte makes sense: the boss isn't about to swing again. Mid-combo it's false,
        /// so the hero's press stays a normal attack that can clash with the next hit.
        /// </summary>
        public bool IsOpenForRiposte =>
            state == State.Idle || state == State.Approach || state == State.Staggered ||
            (state == State.Attacking && move != null && stepIndex == move.steps.Length - 1 && CurrentPhase != AttackPhase.Startup);

        public Team Team => Team.Boss;
        public Rect Hurtbox => CombatBox.FromLocal((Vector2)transform.position + new Vector2(0f, airHeight), config.hurtboxOffset, config.hurtboxSize, facing);

        float DistanceToHero => hero != null ? Mathf.Abs(hero.transform.position.x - transform.position.x) : 0f;

        readonly BodyBlock bodies = new BodyBlock();

        void Awake()
        {
            health = GetComponent<Health>();
            health.Init(config.maxHealth);
            if (config.floorStyle == FloorStyle.Tentacles) Tentacles.Warm(); // drawn at load, not at the first eruption
            if (body != null)
            {
                bodyBaseScale = body.localScale;
                bodyBasePos = body.localPosition;
                bodyRenderer = body.GetComponent<SpriteRenderer>();
                greyboxSprite = bodyRenderer != null ? bodyRenderer.sprite : null;
                greyboxScale = bodyBaseScale;
                greyboxPos = bodyBasePos;
            }
            SetArtMode(poses != null && poses.IsUsable);
            if (floorMarker != null) markerBaseColor = floorMarker.color;
            slashArc = SlashArc.Create(transform, 7);
            EnterIdle(config.firstAttackDelaySeconds);
        }

        public bool ArtMode => artMode;

        /// <summary>Waits at least this long (from the fight's start) before the first move: the first-time coach keeps
        /// the first swing out from under the boss's name card.</summary>
        public void HoldFirstMove(float seconds)
        {
            if (state == State.Idle && lastMove == null) stateDuration = Mathf.Max(stateDuration, seconds);
        }

        /// <summary>Switches between pose sprites and the greybox box (F4 in the greybox HUD).</summary>
        public void SetArtMode(bool on)
        {
            artMode = on && poses != null && poses.IsUsable && bodyRenderer != null;
            if (body == null || bodyRenderer == null) return;
            bodyBaseScale = artMode ? Vector3.one * poses.Scale : greyboxScale;
            bodyBasePos = artMode ? Vector3.zero : greyboxPos;
            bodyRenderer.sprite = artMode ? poses.idle : greyboxSprite;
            bodyRenderer.flipX = false;
            easedScale = bodyBaseScale;
            easedPos = bodyBasePos;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f) // dt is 0 during hit-stop
            {
                TickStatuses(dt);
                if (frozenTimer > 0f)
                {
                    // Frozen: the boss's clock stops. Windups hold, live hitboxes stop checking, nothing moves.
                    frozenTimer -= dt;
                    UpdateVisuals();
                    return;
                }
                prevStateTime = stateTime;
                stateTime += dt;
                DecayStagger(dt);
                if (flinchTimer > 0f) flinchTimer -= dt;

                switch (state)
                {
                    case State.Idle:
                        FaceHero();
                        if (stateTime >= stateDuration) ChooseMove();
                        break;
                    case State.Approach:
                        UpdateApproach(dt);
                        break;
                    case State.Attacking:
                        UpdateAttacking(dt);
                        break;
                    case State.Staggered:
                        if (stateTime >= stateDuration)
                        {
                            Stagger = 0f;
                            finaleHolding = false;
                            EnterIdle(IdleTime());
                        }
                        break;
                    case State.PhaseTransition:
                        if (stateTime >= stateDuration)
                        {
                            Phase = 2;
                            EnterIdle(0.4f);
                        }
                        break;
                }
                ClampToArena();
            }
            UpdateVisuals();
        }

        /// <summary>After everyone has moved: the hero and the boss can't stand inside each other (BodyBlock).</summary>
        void LateUpdate() => bodies.Resolve(hero, this, arena, Time.deltaTime);

        // ---------------------------------------------------------------- move selection & spacing

        void ChooseMove()
        {
            if (hero != null && hero.Health != null && hero.Health.IsDead)
            {
                EnterIdle(1f);
                return;
            }

            var moves = Phase == 1 ? config.phase1Moves : config.phase2Moves;
            if (moves == null || moves.Length == 0)
            {
                EnterIdle(1f);
                return;
            }

            float total = 0f;
            foreach (var m in moves)
                if (IsCandidate(m, moves)) total += m.weight;

            BossMove chosen = null;
            float pick = Random.value * total;
            foreach (var m in moves)
            {
                if (!IsCandidate(m, moves)) continue;
                chosen = m;
                pick -= m.weight;
                if (pick <= 0f) break;
            }

            move = chosen != null ? chosen : moves[0];
            SetState(State.Approach);
            stateDuration = config.approachMaxSeconds;
        }

        bool IsCandidate(BossMove m, BossMove[] moves) =>
            m != null && m.steps != null && m.steps.Length > 0 && !(config.avoidRepeats && moves.Length > 1 && m == lastMove);

        void UpdateApproach(float dt)
        {
            FaceHero();
            float dist = DistanceToHero;
            int dir = 0;
            if (dist > move.preferredRange + move.rangeTolerance) dir = facing;
            else if (dist < move.preferredRange - move.rangeTolerance) dir = -facing;

            if (dir == 0 || stateTime >= stateDuration || AtWall(dir))
            {
                StartMove();
                return;
            }
            Translate(dir * config.walkSpeed * dt);
            if (poses != null && poses.moveCycleSpeed > 0f && rootTimer <= 0f) moveCycleTime += dir * facing * config.walkSpeed * dt / poses.moveCycleSpeed;
            // Armored footsteps, one per step of ground covered: the approach is heard as well as seen.
            if (rootTimer <= 0f && (stepDistance += config.walkSpeed * dt) >= (poses != null ? poses.StepLength : 1.2f))
            {
                stepDistance = 0f;
                GameAudio.Play(Sfx.BossStep);
            }
        }

        // ---------------------------------------------------------------- attacking

        void StartMove()
        {
            lastMove = move;
            parriedThisMove = 0;
            SetState(State.Attacking);
            BeginStep(0);
        }

        void BeginStep(int index)
        {
            StepSerial++;
            stepIndex = index;
            stateTime = 0f;
            prevStateTime = 0f;
            stepResolved = false;
            stepSpawned = false;
            stepArcPlayed = false;
            if (index == 0 || !move.lockFacing) FaceHero();
            if (hero != null) stepTargetX = hero.transform.position.x;
        }

        /// <summary>A step's hitbox: in front of the boss, or (atHero) on the spot the hero stood when it began.</summary>
        Rect StepHitbox(AttackData step) => step.atHero
            ? CombatBox.FromLocal(new Vector2(stepTargetX, transform.position.y), step.hitboxOffset, step.hitboxSize, facing)
            : step.HitboxAt(transform.position, facing);

        void UpdateAttacking(float dt)
        {
            var step = move.steps[stepIndex];
            Translate(step.LungeSpeedAt(stateTime) * facing * dt);
            if (step.leapHeight > 0f) UpdateLeap(step, dt);

            // The telegraph's sound, with its glint: a clean ring = parry it, a snarl = dash.
            if (step.cue != TelegraphCue.None && prevStateTime <= step.CueStart && stateTime > step.CueStart && stateTime < step.ActiveStart)
                GameAudio.Play(step.cue == TelegraphCue.WhiteGlint ? Sfx.CueGlint : Sfx.CueDanger);

            if (step.ActiveBetween(prevStateTime, stateTime))
            {
                if (!stepArcPlayed)
                {
                    GameAudio.Play(Sfx.BossSwing);
                    if (step.leapHeight > 0f)
                    {
                        airHeight = 0f;
                        GameAudio.Play(Sfx.BossSlam); // the landing shakes the arena whether it hits or not
                        CameraShake.Add(step.shakeTrauma);
                        GroundImpact.Play(transform.position.x, arena != null ? arena.groundY : transform.position.y,
                                          step.hitboxSize.x * 2.2f, new Color(0.72f, 0.66f, 0.8f, 0.85f));
                    }
                    stepArcPlayed = true;
                    if (config.floorStyle != FloorStyle.Marker && step.cue == TelegraphCue.FloorMarker && step.leapHeight <= 0f)
                    {
                        // The King's floor attacks: ice spikes burst up across the hitbox, stay while it's live, shatter.
                        // The Unknown One's: his tentacles burst up, writhe while it's live, and pull back.
                        float ground = arena != null ? arena.groundY : transform.position.y;
                        if (config.floorStyle == FloorStyle.IceSpikes) IceSpikes.Erupt(StepHitbox(step), ground, step.ActiveEnd - step.ActiveStart + 0.25f);
                        else Tentacles.Erupt(StepHitbox(step), ground, step.ActiveEnd - step.ActiveStart + 0.2f);
                        GameAudio.Play(Sfx.BossSlam, 0.6f);
                        CameraShake.Add(step.shakeTrauma * 0.5f);
                    }
                    else
                    {
                        // Sword trail in the danger language: element color, red (dash) or white (parry).
                        var trail = step.element != Element.None ? ElementColors.Of(step.element) : step.parryable ? Color.white : redGlowColor;
                        trail.a = 0.8f;
                        slashArc.Play(StepHitbox(step), facing, trail);
                    }
                }
                if (step.spawnsProjectile && !stepSpawned)
                {
                    stepSpawned = true;
                    SpawnProjectile(step, facing);
                    if (step.projectileBothWays) SpawnProjectile(step, -facing);
                    GameAudio.Play(step.element == Element.Fire ? Sfx.FlameWave : !step.parryable ? Sfx.BossSlam : Sfx.WaveRelease);
                }
                if (!stepResolved) TryHitHero(step);
            }
            if (state != State.Attacking) return; // a parry can stagger the boss mid-move

            if (stateTime >= step.TotalTime)
            {
                if (stepIndex + 1 < move.steps.Length) BeginStep(stepIndex + 1);
                else EnterIdle(IdleTime());
            }
        }

        void TryHitHero(AttackData step)
        {
            if (hero == null) return;
            if (!StepHitbox(step).Overlaps(hero.Hurtbox)) return;

            var result = hero.ReceiveHit(new HitInfo { attackerTeam = Team.Boss, attack = step, sourcePosition = transform.position });
            switch (result)
            {
                case HitResult.Hit:
                case HitResult.Ignored:
                    stepResolved = true;
                    break;
                case HitResult.Parried:
                    stepResolved = true;
                    OnParried();
                    break;
                case HitResult.Dodged:
                    break; // i-frames: the hitbox stays live until the active frames end
            }
        }

        void OnParried()
        {
            parriedThisMove++;
            if (flash != null) flash.Flash(feedback.parry.flashColor, feedback.parry.flashFrames);
            Translate(-facing * config.parryPushback);
            AddStagger(config.staggerPerParry);
            if (state == State.Attacking && move.fullParryStaggers && parriedThisMove >= move.steps.Length) EnterStagger();
        }

        /// <summary>
        /// A parried spell feeds the stagger meter like a parried swing, and counts toward a full-parry stagger while
        /// its move is still playing (a volley). No pushback: he's across the arena.
        /// </summary>
        void OnProjectileParried(BossMove from)
        {
            if (this == null || state == State.Dead || state == State.PhaseTransition) return;
            if (flash != null) flash.Flash(feedback.parry.flashColor, feedback.parry.flashFrames);
            AddStagger(config.staggerPerParry);
            if (state == State.Attacking && move == from && ++parriedThisMove >= move.steps.Length && move.fullParryStaggers) EnterStagger();
        }

        /// <summary>
        /// A leap: a short crouch, then up out of reach during the windup, hanging over the landing spot (which follows
        /// the hero until the lock), then a fast drop that lands on the first active frame (beats in PoseMotion).
        /// </summary>
        void UpdateLeap(AttackData step, float dt)
        {
            float t = stateTime, land = step.ActiveStart;
            if (t >= land) { airHeight = 0f; return; }
            if (t < land - Frames.ToSeconds(step.leapLockFrames) && hero != null) stepTargetX = hero.transform.position.x;
            float up = Mathf.Clamp01((t - PoseMotion.LeapCrouch) / PoseMotion.LeapRise);
            float down = Mathf.Clamp01((land - t) / PoseMotion.LeapFall);
            airHeight = step.leapHeight * Mathf.Min(1f - (1f - up) * (1f - up), down);
            if (prevStateTime < PoseMotion.LeapCrouch && t >= PoseMotion.LeapCrouch) GameAudio.Play(Sfx.Dash); // the jump
            if (t > PoseMotion.LeapCrouch + PoseMotion.LeapRise * 0.5f)
                Translate(Mathf.MoveTowards(transform.position.x, stepTargetX, 16f * dt) - transform.position.x);
        }

        void SpawnProjectile(AttackData step, int dir)
        {
            var go = new GameObject($"{step.name} Projectile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = projectileSprite;
            sr.color = step.element != Element.None ? ElementColors.Of(step.element) : step.parryable ? Color.white : redGlowColor;
            sr.sortingOrder = 4;

            float frontX = transform.position.x + dir * (config.hurtboxSize.x * 0.5f + step.projectileSize.x * 0.5f);
            float groundY = arena != null ? arena.groundY : transform.position.y;
            var from = move;
            go.AddComponent<Projectile>().Launch(step, Team.Boss, hero, new Vector2(frontX, groundY), dir, arena,
                onHit: r => { if (r == HitResult.Parried) OnProjectileParried(from); });
            // Art mode: fire for an elemental wave, a red crest for an unparryable one (the greybox keeps the hitbox square).
            if (artMode)
            {
                var style = step.element == Element.Fire ? ProjectileVisual.Style.Flame
                          : !step.parryable ? ProjectileVisual.Style.Shockwave : ProjectileVisual.Style.Crescent;
                ProjectileVisual.Attach(go, style, sr.color, dir, step.projectileSize, groundY);
                // The Unknown One's ground wave: his tentacles burst up along its path, glowing red (dash).
                if (config.floorStyle == FloorStyle.Tentacles && style == ProjectileVisual.Style.Shockwave)
                    TentacleWave.Attach(go, groundY, dir, 1.5f, redGlowColor);
            }
        }

        // ---------------------------------------------------------------- taking hits

        public HitResult ReceiveHit(in HitInfo hit)
        {
            if (state == State.Dead || state == State.PhaseTransition) return HitResult.Ignored;

            if (flash != null) flash.Flash(feedback.hitFlashColor, feedback.hitFlashFrames);
            if (TakeDamage(hit.Damage, hit.StaggerDamage))
            {
                ApplyElement(hit.element);
                // A combo breaker cancels the steps still to come (a single attack just plays out).
                if (hit.attack != null && hit.attack.breaksCombo && state == State.Attacking && move != null && stepIndex < move.steps.Length - 1)
                    BreakCombo();
            }
            return HitResult.Hit;
        }

        void BreakCombo()
        {
            flinchTimer = config.comboBreakFlinchSeconds;
            EnterIdle(config.comboBreakFlinchSeconds + config.comboBreakIdleSeconds);
        }

        /// <summary>Applies damage and stagger. False if the boss died or entered its phase transition.</summary>
        bool TakeDamage(int amount, float stagger)
        {
            health.TakeDamage(amount);
            if (health.IsDead)
            {
                Die();
                return false;
            }
            if (Phase == 1 && health.Normalized <= config.phase2AtHealth && config.phase2Moves != null && config.phase2Moves.Length > 0)
            {
                EnterPhaseTransition();
                return false;
            }
            if (!finaleUsed && config.finaleAtHealth > 0f && Phase == 2 && health.Normalized <= config.finaleAtHealth)
            {
                EnterFinaleHold();
                return false;
            }
            AddStagger(stagger);
            return true;
        }

        // ---------------------------------------------------------------- harvested elements

        void ApplyElement(Element element)
        {
            var h = hero != null ? hero.config : null;
            if (h == null || element == Element.None) return;
            switch (element)
            {
                case Element.Fire:
                    burnTicksLeft = h.fireBurnTicks;
                    burnTimer = h.fireBurnIntervalSeconds;
                    break;
                case Element.Ice:
                    frozenTimer = h.iceFreezeSeconds;
                    break;
                case Element.Wood:
                    rootTimer = h.woodRootSeconds;
                    break;
            }
        }

        void TickStatuses(float dt)
        {
            if (rootTimer > 0f) rootTimer -= dt;
            if (burnTicksLeft <= 0) return;
            burnTimer -= dt;
            if (burnTimer > 0f) return;
            var h = hero != null ? hero.config : null;
            burnTimer += h != null ? h.fireBurnIntervalSeconds : 0.8f;
            burnTicksLeft--;
            if (flash != null) flash.Flash(ElementColors.Fire, 3);
            GameAudio.Play(Sfx.BurnTick);
            TakeDamage(h != null ? h.fireBurnDamage : 1, 0f);
        }

        void ClearStatuses()
        {
            frozenTimer = 0f;
            rootTimer = 0f;
            burnTicksLeft = 0;
        }

        void AddStagger(float amount)
        {
            if (amount <= 0f || state == State.Staggered || state == State.Dead || state == State.PhaseTransition) return;
            Stagger = Mathf.Min(config.staggerMax, Stagger + amount);
            lastStaggerDamageTime = Time.time;
            if (Stagger >= config.staggerMax) EnterStagger();
        }

        void DecayStagger(float dt)
        {
            if (state == State.Staggered || Stagger <= 0f) return;
            if (Time.time - lastStaggerDamageTime >= config.staggerDecayDelaySeconds)
                Stagger = Mathf.Max(0f, Stagger - config.staggerDecayPerSecond * dt);
        }

        void EnterStagger()
        {
            Stagger = config.staggerMax;
            SetState(State.Staggered);
            stateDuration = config.staggerDurationSeconds;
            finaleHolding = false;
            Play(feedback.bossStaggered);
            GameAudio.Play(Sfx.BossStagger);
        }

        /// <summary>
        /// The final form's last stand (once per fight): the knight inside holds the god still, his helm glinting one
        /// last time, and the hero lands the final blow. A long stagger: hits land, ripostes work.
        /// </summary>
        void EnterFinaleHold()
        {
            finaleUsed = true;
            ClearStatuses();
            Stagger = config.staggerMax;
            SetState(State.Staggered);
            stateDuration = config.finaleHoldSeconds;
            finaleHolding = true;
            Play(feedback.phaseChange);
            GameAudio.Play(Sfx.BossStagger);
        }

        void EnterPhaseTransition()
        {
            Stagger = 0f;
            ClearStatuses();
            SetState(State.PhaseTransition);
            stateDuration = config.phaseTransitionSeconds;
            Play(feedback.phaseChange);
            GameAudio.Play(Sfx.PhaseChange);
            GameAudio.PlayMusic(MusicTrack.FightPhase2);
        }

        void Die()
        {
            ClearStatuses();
            SetState(State.Dead);
            GameAudio.Play(Sfx.BossDeath);
            HitStop.SlowMotion(feedback.bossKillSlowScale, feedback.bossKillSlowSeconds);
            CameraShake.Add(feedback.bossKillTrauma);
        }

        void Play(FeedbackConfig.Bundle bundle)
        {
            HitStop.Freeze(bundle.hitStopFrames);
            CameraShake.Add(bundle.trauma);
            if (flash != null && bundle.flashFrames > 0) flash.Flash(bundle.flashColor, bundle.flashFrames);
        }

        // ---------------------------------------------------------------- helpers

        float IdleTime()
        {
            var range = Phase == 1 ? config.phase1IdleSeconds : config.phase2IdleSeconds;
            return Random.Range(range.x, range.y);
        }

        void EnterIdle(float seconds)
        {
            SetState(State.Idle);
            stateDuration = seconds;
        }

        void SetState(State next)
        {
            airHeight = 0f;
            state = next;
            stateTime = 0f;
            prevStateTime = 0f;
        }

        void FaceHero()
        {
            if (hero == null) return;
            float dx = hero.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) facing = dx > 0f ? 1 : -1;
        }

        void Translate(float dx)
        {
            if (rootTimer > 0f) return; // rooted: no approach, lunges stay in place, no pushback
            if (dx != 0f) transform.position += new Vector3(dx, 0f, 0f);
        }

        void ClampToArena()
        {
            if (arena == null) return;
            var p = transform.position;
            p.x = arena.Clamp(p.x, config.hurtboxSize.x * 0.5f);
            p.y = arena.groundY;
            transform.position = p;
        }

        bool AtWall(int dir)
        {
            if (arena == null) return false;
            float half = config.hurtboxSize.x * 0.5f;
            float x = transform.position.x;
            return dir < 0 ? x <= arena.minX + half + 0.01f : x >= arena.maxX - half - 0.01f;
        }

        // ---------------------------------------------------------------- greybox visuals (telegraphs)

        void UpdateVisuals()
        {
            if (body == null) return;
            Vector3 scale = bodyBaseScale;
            Vector3 pos = bodyBasePos;
            Color color = artMode ? (Phase == 1 ? Color.white : Color.Lerp(Color.white, phase2Color, 0.12f)) : Phase == 1 ? phase1Color : phase2Color;
            bool showGlint = false;
            float glintScale = 0.35f;
            bool showMarker = false;
            bool showHitbox = false;
            Rect hitRect = default;
            Color hitColor = default;

            switch (state)
            {
                case State.Approach:
                    pos.y = bodyBasePos.y + Mathf.Abs(Mathf.Sin(stateTime * 10f)) * (artMode && poses.move != null && poses.move.IsValid ? 0f : 0.08f); // walk bob (greybox only)
                    break;
                case State.Attacking:
                {
                    var step = move.steps[stepIndex];
                    var phase = step.PhaseAt(stateTime);
                    if (phase == AttackPhase.Startup)
                    {
                        // The windup pose says WHAT is coming; the cue, a fixed time before the hit, says WHEN.
                        float k = step.StartupTime > 0f ? Mathf.Clamp01(stateTime / step.StartupTime) : 1f;
                        var motion = step.leapHeight > 0f ? PoseMotion.BossLeap(stateTime, step.ActiveStart)
                                                          : PoseMotion.BossAttack(phase, k, false); // shared with the Animation Gym
                        scale = Deform(motion.squash.x, motion.squash.y);
                        pos.x += motion.lean * facing;
                        bool cueOn = stateTime >= step.CueStart;
                        float sinceCue = stateTime - step.CueStart;
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
                        switch (step.cue)
                        {
                            case TelegraphCue.WhiteGlint:
                                showGlint = cueOn;
                                glintScale = Mathf.Lerp(0.75f, 0.35f, Mathf.Clamp01(sinceCue / 0.1f)); // pops in, then settles
                                color = Tint(color, Color.white, cueOn ? 0.35f : 0.1f * k);
                                break;
                            case TelegraphCue.RedGlow:
                                // Gameplay-critical: art keeps most of the red.
                                float red = cueOn ? 0.6f + 0.4f * pulse : 0.3f * k;
                                color = Color.Lerp(color, redGlowColor, artMode ? red * 0.7f : red);
                                break;
                            case TelegraphCue.FloorMarker:
                                showMarker = true;
                                break;
                        }
                    }
                    else if (phase == AttackPhase.Active)
                    {
                        var motion = step.leapHeight > 0f ? PoseMotion.BossLeap(stateTime, step.ActiveStart) : PoseMotion.BossAttack(phase, 1f, false);
                        scale = Deform(motion.squash.x, motion.squash.y);
                        pos.x += motion.lean * facing;
                        showHitbox = true;
                        hitRect = StepHitbox(step);
                        hitColor = step.parryable ? new Color(1f, 1f, 1f, 0.35f) : new Color(1f, 0.2f, 0.1f, 0.35f);
                    }
                    else if (phase == AttackPhase.Recovery && stepIndex == move.steps.Length - 1)
                    {
                        color = Tint(color, openColor, 1f); // punish window
                        var motion = PoseMotion.BossAttack(phase, 1f, true);
                        scale = Deform(motion.squash.x, motion.squash.y);
                    }
                    break;
                }
                case State.Staggered:
                    color = Tint(color, Color.Lerp(staggerColor, openColor, 0.3f + 0.3f * Mathf.Sin(Time.time * 8f)), 1f);
                    if (finaleHolding)
                    {
                        showGlint = true; // the knight's glint, one last time
                        glintScale = 0.45f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
                    }
                    if (!(artMode && poses.staggered != null))
                    {
                        scale = Deform(1.1f, 0.75f);
                        pos.y = bodyBasePos.y * 0.75f;
                    }
                    break;
                case State.PhaseTransition:
                    color = Tint(color, Mathf.Repeat(Time.unscaledTime * 8f, 1f) < 0.5f ? phase1Color : phase2Color, 1f);
                    break;
                case State.Dead:
                    color = Tint(color, deadColor, 1f);
                    if (!(artMode && poses.dead != null))
                    {
                        scale = Deform(1.2f, 0.3f);
                        pos.y = bodyBasePos.y * 0.3f;
                    }
                    break;
            }

            // Status tints on top of everything else.
            if (IsFrozen) color = Color.Lerp(color, ElementColors.Ice, 0.75f);
            else if (IsBurning) color = Color.Lerp(color, ElementColors.Fire, 0.25f + 0.25f * Mathf.Sin(Time.time * 20f));
            if (IsRooted && state != State.Dead) color = Color.Lerp(color, ElementColors.Wood, 0.35f);

            if (state == State.Idle) scale.y *= 1f + (artMode ? poses.idleBreathAmount : 0.015f) * Mathf.Sin(Time.time * (artMode ? poses.idleBreathRate : 1.8f)); // breathing

            // Ease toward the target so pose changes don't snap (dt = 0 in hit-stop freezes it).
            float ease = 1f - Mathf.Exp(-Time.deltaTime * MotionEase);
            easedScale = Vector3.Lerp(easedScale, scale, ease);
            easedPos = Vector3.Lerp(easedPos, pos, ease);
            body.localScale = easedScale;
            body.localPosition = easedPos + new Vector3(0f, airHeight, 0f); // leaps aren't eased: the drop must be sharp
            if (flash != null) flash.BaseColor = color;
            if (artMode)
            {
                var sprite = PoseSprite();
                bodyRenderer.sprite = sprite;
                bodyRenderer.flipX = facing < 0; // art faces right
                if (poses.move != null && poses.move.IsValid && System.Array.IndexOf(poses.move.frames, sprite) >= 0) body.localScale = easedScale * poses.move.scale;
                else if (state == State.Attacking && move.steps[stepIndex].HasAnimation) body.localScale = easedScale * move.steps[stepIndex].PoseScaleAt(stateTime);
            }

            if (glint != null)
            {
                glint.enabled = showGlint;
                if (showGlint)
                {
                    float s = glintScale;
                    float gx = transform.position.x + (config.hurtboxSize.x * 0.5f + 0.2f) * facing;
                    float gy = transform.position.y + airHeight + config.hurtboxSize.y + 0.3f;
                    glint.transform.position = new Vector3(gx, gy, 0f);
                    glint.transform.localScale = new Vector3(s, s, 1f);
                }
            }

            // The IceSpikes style replaces the marker bar with a frost patch that spreads and brightens through the windup,
            // the Tentacles style with a dark pool. A leap keeps the marker: its mark follows the hero, then locks.
            bool frost = showMarker && config.floorStyle != FloorStyle.Marker && move.steps[stepIndex].leapHeight <= 0f;
            if (frost)
            {
                if (frostPatch == null)
                {
                    frostPatch = new GameObject("FrostPatch").AddComponent<SpriteRenderer>();
                    frostPatch.transform.SetParent(transform, true);
                    frostPatch.sortingOrder = 0;
                }
                var markStep = move.steps[stepIndex];
                float k = markStep.StartupTime > 0f ? Mathf.Clamp01(stateTime / markStep.StartupTime) : 1f;
                float patchGround = arena != null ? arena.groundY : transform.position.y;
                if (config.floorStyle == FloorStyle.IceSpikes) IceSpikes.ShowPatch(frostPatch, StepHitbox(markStep), patchGround, k, stateTime >= markStep.CueStart);
                else Tentacles.ShowPatch(frostPatch, StepHitbox(markStep), patchGround, k, stateTime >= markStep.CueStart);
            }
            else if (frostPatch != null) frostPatch.enabled = false;

            if (floorMarker != null)
            {
                floorMarker.enabled = showMarker && !frost;
                if (showMarker && !frost)
                {
                    var markStep = move.steps[stepIndex];
                    var r = StepHitbox(markStep);
                    // A leap's mark follows the hero faintly, then locks bright and thick: move now.
                    bool leap = markStep.leapHeight > 0f;
                    bool locked = !leap || stateTime >= markStep.ActiveStart - Frames.ToSeconds(markStep.leapLockFrames);
                    var mc = markerBaseColor;
                    if (leap) mc.a = locked ? Mathf.Min(1f, mc.a * 1.6f) : mc.a * 0.45f;
                    floorMarker.color = mc;
                    floorMarker.transform.position = new Vector3(r.center.x, transform.position.y + 0.05f, 0f);
                    floorMarker.transform.localScale = new Vector3(r.width, leap && locked ? 0.18f : 0.1f, 1f);
                }
            }

            if (hitboxDebug != null)
            {
                hitboxDebug.enabled = showHitbox && CombatDebug.ShowHitboxes;
                if (hitboxDebug.enabled)
                {
                    hitboxDebug.color = hitColor;
                    hitboxDebug.transform.position = hitRect.center;
                    hitboxDebug.transform.localScale = new Vector3(hitRect.width, hitRect.height, 1f);
                }
            }
        }

        /// <summary>Greybox: full squash/stretch. Art: toned down so the drawing doesn't smear.</summary>
        Vector3 Deform(float x, float y) => PoseMotion.Deform(bodyBaseScale, x, y, artMode);

        /// <summary>Greybox: the box takes the state color. Art: a light wash over the drawing.</summary>
        Color Tint(Color current, Color target, float amount) => Color.Lerp(current, target, artMode ? amount * PoseMotion.ArtTint : amount);

        Sprite PoseSprite()
        {
            switch (state)
            {
                case State.Approach:
                    return poses.move != null && poses.move.IsValid && rootTimer <= 0f ? poses.move.Sample(moveCycleTime) : poses.idle;
                case State.Attacking:
                {
                    var step = move.steps[stepIndex];
                    // A leap gathers in its landing pose (crouched low) before it jumps.
                    if (step.leapHeight > 0f && stateTime < PoseMotion.LeapCrouch && step.activePose != null) return step.activePose;
                    return PoseSet.First(step.PoseAt(stateTime), poses.idle);
                }
                case State.Staggered: return PoseSet.First(poses.staggered, poses.hurt, poses.idle);
                case State.PhaseTransition: return PoseSet.First(poses.phaseChange, poses.hurt, poses.staggered, poses.idle);
                case State.Dead: return PoseSet.First(poses.dead, poses.staggered, poses.hurt, poses.idle);
                default: return flinchTimer > 0f ? PoseSet.First(poses.hurt, poses.idle) : poses.idle;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (config == null) return;
            var r = CombatBox.FromLocal(transform.position, config.hurtboxOffset, config.hurtboxSize, facing);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(r.center, r.size);
        }
    }
}
