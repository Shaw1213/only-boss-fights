using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// Side-view hero: move, dash, parry, light combo, charged heavy, riposte, flash counter, dash attack.
    /// A small enum state machine; every number comes from <see cref="HeroConfig"/> and
    /// <see cref="AttackData"/>, so the feel is tuned in the Inspector, not in code.
    /// The hero always faces the boss (duel stance), so dashing away is a backdash.
    /// Harvest: parrying or clashing an elemental spell loads its element into the blade; the next
    /// charged heavy fires it as a crescent wave, or the next riposte that lands carries it.
    /// Flash Counter: a clash (attack-parry) opens a short window in which a dash flash-steps through the boss
    /// and slashes from his far side, even mid-combo (a parry's riposte needs the boss open).
    /// </summary>
    [RequireComponent(typeof(Health), typeof(HeroInput))]
    public class HeroController : MonoBehaviour, IHittable
    {
        public enum State { Idle, Dash, Attack, Charging, Parry, Hurt, Dead }

        [Header("Data")]
        public HeroConfig config;
        public FeedbackConfig feedback;

        [Header("Scene")]
        public BossController boss;
        public Arena arena;

        [Header("Greybox visuals")]
        public Transform body;
        public SpriteFlash flash;
        public SpriteRenderer hitboxDebug;
        public Color baseColor = new Color(0.30f, 0.85f, 0.95f);
        public Color chargeColor = new Color(1.00f, 0.80f, 0.25f);
        public Color parryColor = new Color(0.85f, 1.00f, 1.00f);
        [Tooltip("Shows the held element at the sword hand (stands in for the blade's rune line).")]
        public SpriteRenderer elementOrb;
        public Sprite waveSprite;
        [Tooltip("Sword trail over the active frames (a harvested element tints the riposte's trail).")]
        public Color slashColor = new Color(0.7f, 1f, 1f, 0.85f);

        [Header("Art (optional)")]
        [Tooltip("When set, the body shows pose sprites (feet pivot, facing right) instead of the greybox box. F4 toggles at runtime.")]
        public PoseSet poses;

        Health health;
        HeroInput input;

        State state;
        float stateTime;
        float prevStateTime;
        int facing = 1;
        float velocityX;

        AttackData attack;
        float attackStartInput; // on the input clock (combo chaining)
        bool attackHasHit;
        bool attackInvulnerable;
        bool attackIsLight;
        bool attackFromNeutral;
        bool chargeEligible;
        int comboIndex;
        float comboIdleTime;

        int dashDir;
        float dashCooldown;
        float lastDashEndTime = float.NegativeInfinity;

        float riposteTimer;
        float flashTimer;           // open after a clash: the next dash is a flash counter
        float flashFromX, flashToX; // the flash step's path (startup frames)
        float flashTrailTimer;
        SpriteRenderer counterGlint; // gold diamond over the hero while the counter is ready: "dash now"
        // Statuses from the boss's spells (the mirror of what harvest does to him): burn, chill, root.
        int burnTicksLeft;
        float burnTimer;
        float chillTimer;
        float rootTimer;
        SpriteRenderer statusMark; // a colored band at the feet while a status is on (reads on dark art)
        SpriteRenderer counterGlintOutline; // dark rim so it reads on light walls too
        static readonly Color CounterGlintGold = new Color(1f, 0.8f, 0.2f);
        float hurtInvulnTimer;

        Element heldElement;
        Element attackWaveElement; // set when an infused charged heavy starts: it fires a wave instead of a melee hit
        bool attackFiredWave;

        // For the EARLY / LATE callouts: a parry attempt this long before getting hit reads as "EARLY".
        const float EarlyCalloutSeconds = 0.65f;
        float lastDefensePressTime = float.NegativeInfinity;
        float lastHurtTime = float.NegativeInfinity;

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
        AfterimageTrail dashTrail;
        float afterimageTimer;
        bool attackArcPlayed;
        PoseCrossfade crossfade;
        bool wasMoving;

        public State CurrentState => state;
        public AttackData CurrentAttack => state == State.Attack ? attack : null;
        public AttackPhase CurrentPhase => state == State.Attack && attack != null ? attack.PhaseAt(stateTime) : AttackPhase.None;
        public float StateTime => stateTime;
        public int Facing => facing;
        public bool RiposteReady => riposteTimer > 0f;
        public bool FlashCounterReady => flashTimer > 0f;
        public bool IsBurning => burnTicksLeft > 0;
        public bool IsChilled => chillTimer > 0f;
        public bool IsRooted => rootTimer > 0f;
        /// <summary>A dash pressed now would start (not rooted, off cooldown).</summary>
        public bool CanDash => !IsRooted && dashCooldown <= 0f;
        public int ParryCount { get; private set; }
        /// <summary>How many of those parries were clashes (an attack meeting the hit), not the parry button.</summary>
        public int ClashCount { get; private set; }
        public Health Health => health;
        public HeroInput InputSource => input;
        /// <summary>The harvested element loaded into the blade (None when empty).</summary>
        public Element HeldElement => heldElement;
        public int HarvestCount { get; private set; }
        /// <summary>Short feedback text ("PARRY!", "CLASH!", "EARLY", "LATE", "RED = DASH") and when it was set (unscaled time).</summary>
        public string Callout { get; private set; }
        public float CalloutTime { get; private set; } = float.NegativeInfinity;

        public Team Team => Team.Hero;
        public Rect Hurtbox => CombatBox.FromLocal(transform.position, config.hurtboxOffset, config.hurtboxSize, facing);

        bool ParryWindowOpen => state == State.Parry && stateTime < Frames.ToSeconds(config.parryWindowFrames);

        /// <summary>Attack-parry: the first frames of an attack started from neutral deflect parryable hits.</summary>
        bool ClashWindowOpen =>
            config.attackParryEnabled && state == State.Attack && attackFromNeutral &&
            stateTime < Frames.ToSeconds(config.attackParryWindowFrames);

        /// <summary>Moves that go past the boss's body (BodyBlock): the dash (HeroConfig.dashPassesThroughBoss) and the flash counter's step.</summary>
        public bool PassesThroughBoss =>
            (state == State.Dash && config.dashPassesThroughBoss) ||
            (state == State.Attack && attack == config.flashCounter && stateTime < attack.ActiveStart);

        public bool IsInvulnerable
        {
            get
            {
                if (hurtInvulnTimer > 0f) return true;
                if (state == State.Dash)
                {
                    float start = Frames.ToSeconds(config.dashStartupFrames);
                    return stateTime >= start && stateTime < start + Frames.ToSeconds(config.dashInvulnerableFrames);
                }
                // The riposte can't be interrupted through its active frames.
                if (state == State.Attack && attackInvulnerable) return stateTime < attack.ActiveEnd;
                return false;
            }
        }

        void Awake()
        {
            health = GetComponent<Health>();
            input = GetComponent<HeroInput>();
            health.Init(config.maxHealth);
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
            slashArc = SlashArc.Create(transform, 7);
            dashTrail = AfterimageTrail.Create("Hero Afterimages", 2);
            if (waveSprite != null)
            {
                // The danger language's glint means "now"; the hero's gold one means "counter now".
                var glintGo = new GameObject("Counter Glint");
                glintGo.transform.SetParent(transform, false);
                glintGo.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                counterGlint = glintGo.AddComponent<SpriteRenderer>();
                counterGlint.sprite = waveSprite;
                counterGlint.sortingOrder = 9;
                counterGlint.enabled = false;
                var rimGo = new GameObject("Rim");
                rimGo.transform.SetParent(glintGo.transform, false);
                rimGo.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
                counterGlintOutline = rimGo.AddComponent<SpriteRenderer>();
                counterGlintOutline.sprite = waveSprite;
                counterGlintOutline.sortingOrder = 8;
                counterGlintOutline.enabled = false;

                var markGo = new GameObject("Status Mark");
                markGo.transform.SetParent(transform, false);
                statusMark = markGo.AddComponent<SpriteRenderer>();
                statusMark.sprite = waveSprite;
                statusMark.sortingOrder = 4;
                statusMark.enabled = false;
            }
            if (body != null) crossfade = PoseCrossfade.Create(body.parent, (bodyRenderer != null ? bodyRenderer.sortingOrder : 3) + 1);
        }

        public bool ArtMode => artMode;

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
            if (dt > 0f) // dt is 0 during hit-stop: everything holds, input keeps buffering
            {
                prevStateTime = stateTime;
                stateTime += dt;
                if (dashCooldown > 0f) dashCooldown -= dt;
                if (riposteTimer > 0f) riposteTimer -= dt;
                if (flashTimer > 0f) flashTimer -= dt;
                if (hurtInvulnTimer > 0f) hurtInvulnTimer -= dt;
                TickStatuses(dt);

                switch (state)
                {
                    case State.Idle: UpdateIdle(dt); break;
                    case State.Dash: UpdateDash(); break;
                    case State.Attack: UpdateAttack(dt); break;
                    case State.Charging: UpdateCharging(); break;
                    case State.Parry: UpdateParry(); break;
                    case State.Hurt: UpdateHurt(dt); break;
                    case State.Dead: Decelerate(dt); break;
                }
                ClampToArena();
            }
            UpdateVisuals();
        }

        // ---------------------------------------------------------------- states

        void UpdateIdle(float dt)
        {
            FaceBoss();
            float target = input.Move * config.moveSpeed * (IsRooted ? 0f : IsChilled ? config.chillMoveMultiplier : 1f);
            float rate = Mathf.Abs(target) > 0.01f ? config.acceleration : config.deceleration;
            velocityX = Mathf.MoveTowards(velocityX, target, rate * dt);
            Translate(velocityX * dt);
            // Move cycle: starts from frame 1 (the idle's own stance) and plays backwards when backing away
            // (the hero always faces the boss).
            bool moving = Mathf.Abs(velocityX) > 0.5f;
            if (moving && !wasMoving) moveCycleTime = 0f;
            wasMoving = moving;
            if (poses != null && poses.moveCycleSpeed > 0f) moveCycleTime += velocityX * facing * dt / poses.moveCycleSpeed;

            comboIdleTime += dt;
            if (comboIdleTime > config.comboResetSeconds) comboIndex = 0;

            if (CanStartCharge()) { EnterCharging(); return; }
            TryStartAction();
        }

        bool TryStartAction()
        {
            float buffer = config.inputBufferSeconds;
            if (config.dedicatedParryEnabled && input.ConsumeParry(buffer)) { EnterParry(); return true; }
            if (!IsRooted && dashCooldown <= 0f && input.ConsumeDash(buffer))
            {
                if (CanFlashCounter()) BeginFlashCounter();
                else EnterDash();
                return true;
            }
            if (input.ConsumeAttack(buffer)) { StartAttackFromPress(); return true; }
            return false;
        }

        /// <summary>After a clash, a dash (neutral or toward the boss) becomes the flash counter; dashing away stays a dash.</summary>
        bool CanFlashCounter()
        {
            if (flashTimer <= 0f || config.flashCounter == null || boss == null || IsRooted) return false;
            float dx = boss.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > config.flashCounterRange) return false;
            float m = input.Move;
            return !(Mathf.Abs(m) > 0.2f && Mathf.Sign(m) != Mathf.Sign(dx));
        }

        /// <summary>
        /// While the counter is ready, a dash cancels whatever the hero is doing into it: a swing or parry pressed right
        /// after the clash shouldn't eat the reward. Only consumes the press when it becomes a counter.
        /// </summary>
        bool TryFlashCounterCancel()
        {
            if (flashTimer <= 0f || dashCooldown > 0f || !CanFlashCounter()) return false;
            if (!input.ConsumeDash(config.inputBufferSeconds)) return false;
            BeginFlashCounter();
            return true;
        }

        /// <summary>An attack press becomes a riposte, a dash attack, or the next light hit (in that priority).</summary>
        void StartAttackFromPress()
        {
            bool fromNeutral = state != State.Attack; // combo follow-ups can't clash

            // Riposte only when the boss is open; mid-combo the press stays a normal (clash-capable) attack.
            if (riposteTimer > 0f && config.riposte != null && (boss == null || boss.IsOpenForRiposte))
            {
                riposteTimer = 0f;
                flashTimer = 0f; // one reward per clash: the riposte or the counter
                EnterAttack(config.riposte, isLight: false, invulnerable: true, fromNeutral: false);
                return;
            }
            if (config.dashAttackEnabled && config.dashAttack != null && Time.time - lastDashEndTime <= config.dashAttackWindowSeconds)
            {
                EnterAttack(config.dashAttack, isLight: false, invulnerable: false, fromNeutral: true);
                return;
            }
            if (config.lightCombo == null || config.lightCombo.Length == 0) return;
            if (comboIndex >= config.lightCombo.Length) comboIndex = 0;
            EnterAttack(config.lightCombo[comboIndex++], isLight: true, invulnerable: false, fromNeutral: fromNeutral);
            chargeEligible = input.AttackHeld; // only a press that is still held can turn into a charge
        }

        /// <summary>Plans the step to the boss's far side (or stays put if a wall leaves no room there), then attacks.</summary>
        void BeginFlashCounter()
        {
            flashTimer = 0f;
            riposteTimer = 0f;
            dashCooldown = config.dashCooldownSeconds; // it spends the dash
            float bx = boss.transform.position.x;
            int side = transform.position.x < bx ? 1 : -1;
            float gap = boss.config.hurtboxSize.x * 0.5f + config.hurtboxSize.x * 0.5f + config.flashCounterGap;
            float to = bx + side * gap;
            if (arena != null && arena.Clamp(to, config.hurtboxSize.x * 0.5f) != to) to = transform.position.x;
            flashFromX = transform.position.x;
            flashToX = to;
            GameAudio.Play(Sfx.FlashCounter);
            flashTrailTimer = 0f;
            comboIndex = 0;
            EnterAttack(config.flashCounter, isLight: false, invulnerable: true, fromNeutral: false);
        }

        /// <summary>The flash step: crosses during startup leaving afterimages, then turns to face the boss.</summary>
        void UpdateFlashStep()
        {
            float startup = attack.ActiveStart;
            float t = startup > 0f ? Mathf.Clamp01(stateTime / startup) : 1f;
            var p = transform.position;
            p.x = Mathf.Lerp(flashFromX, flashToX, Mathf.SmoothStep(0f, 1f, t));
            transform.position = p;
            if (t < 1f && bodyRenderer != null && (flashTrailTimer -= Time.deltaTime) <= 0f)
            {
                flashTrailTimer = 0.012f;
                dashTrail.Spawn(bodyRenderer);
            }
            if (t >= 1f) FaceBoss(); // land facing him: the slash comes from behind
        }

        void EnterAttack(AttackData data, bool isLight, bool invulnerable, bool fromNeutral)
        {
            SetState(State.Attack);
            attack = data;
            attackIsLight = isLight;
            attackInvulnerable = invulnerable;
            attackFromNeutral = fromNeutral;
            attackHasHit = false;
            attackStartInput = input.Clock;
            chargeEligible = false;
            velocityX = 0f;
            attackWaveElement = data == config.chargedHeavy && config.harvestEnabled && config.elementWave != null ? heldElement : Element.None;
            attackFiredWave = false;
            attackArcPlayed = false;
            if (fromNeutral && config.attackParryEnabled) NoteDefensePress();
        }

        void UpdateAttack(float dt)
        {
            if (TryFlashCounterCancel()) return;
            if (attack == config.flashCounter) UpdateFlashStep();
            Translate(attack.LungeSpeedAt(stateTime) * facing * dt);

            // Sword trail on the first active frame (the infused heavy fires a wave instead).
            if (!attackArcPlayed && attackWaveElement == Element.None && attack.ActiveBetween(prevStateTime, stateTime))
            {
                attackArcPlayed = true;
                GameAudio.Play(attack == config.chargedHeavy || attack == config.riposte || attack == config.flashCounter ? Sfx.HeroSwingHeavy : Sfx.HeroSwingLight);
                bool infusable = attack == config.riposte || attack == config.flashCounter;
                var trail = infusable && heldElement != Element.None && config.harvestEnabled
                    ? new Color(ElementColors.Of(heldElement).r, ElementColors.Of(heldElement).g, ElementColors.Of(heldElement).b, slashColor.a)
                    : attack == config.flashCounter ? new Color(FlashGold.r, FlashGold.g, FlashGold.b, slashColor.a) // a gold flash for the counter
                    : slashColor;
                slashArc.Play(attack.HitboxAt(transform.position, facing), facing, trail);
            }

            if (!attackHasHit && attack.ActiveBetween(prevStateTime, stateTime))
            {
                if (attackWaveElement != Element.None) LaunchWave();
                else TryHitBoss();
            }

            // Holding the button through a light attack turns it into a charge.
            if (attackIsLight && CanStartCharge()) { EnterCharging(); return; }

            // Light combo: a press during this hit cancels its recovery into the next hit.
            // The last hit has no follow-up, so its full recovery is the commitment.
            bool inRecovery = stateTime >= attack.ActiveEnd;
            if (inRecovery && attackIsLight && comboIndex < config.lightCombo.Length && input.ConsumeAttackSince(attackStartInput))
            {
                StartAttackFromPress();
                return;
            }

            if (stateTime >= attack.TotalTime)
            {
                if (comboIndex >= config.lightCombo.Length) comboIndex = 0;
                EnterIdle();
            }
        }

        bool CanStartCharge() =>
            chargeEligible && config.chargedHeavy != null && input.AttackHeld && input.AttackHoldDuration >= config.chargeHoldSeconds;

        void EnterCharging()
        {
            SetState(State.Charging);
            GameAudio.Play(Sfx.ChargeStart);
            chargeEligible = false;
            velocityX = 0f;
        }

        void UpdateCharging()
        {
            // Vulnerable while charging; dash or parry cancel the charge.
            if (TryFlashCounterCancel()) return;
            float buffer = config.inputBufferSeconds;
            if (config.dedicatedParryEnabled && input.ConsumeParry(buffer)) { EnterParry(); return; }
            if (!IsRooted && dashCooldown <= 0f && input.ConsumeDash(buffer)) { EnterDash(); return; }
            if (!input.AttackHeld)
            {
                comboIndex = 0;
                EnterAttack(config.chargedHeavy, isLight: false, invulnerable: false, fromNeutral: true); // a released heavy can clash
            }
        }

        void EnterDash()
        {
            SetState(State.Dash);
            GameAudio.Play(Sfx.Dash);
            float m = input.Move;
            dashDir = Mathf.Abs(m) > 0.2f ? (m > 0f ? 1 : -1) : -facing; // no direction held = backdash
            dashCooldown = config.dashCooldownSeconds * (IsChilled ? config.chillDashCooldownMultiplier : 1f);
            chargeEligible = false;
            velocityX = 0f;
            if (IsBurning)
            {
                burnTicksLeft = 0; // a dash puts the fire out
                SetCallout("PUT OUT!");
            }
        }

        void UpdateDash()
        {
            float startup = Frames.ToSeconds(config.dashStartupFrames);
            float moveTime = Frames.ToSeconds(config.dashInvulnerableFrames);
            float moveEnd = startup + moveTime;
            float total = moveEnd + Frames.ToSeconds(config.dashRecoveryFrames);

            // Move only for the part of this frame inside the dash window (exact at any frame rate).
            float from = Mathf.Max(prevStateTime, startup);
            float to = Mathf.Min(stateTime, moveEnd);
            if (to > from)
            {
                Translate(dashDir * config.dashDistance * (to - from) / moveTime);
                afterimageTimer -= Time.deltaTime;
                if (afterimageTimer <= 0f && bodyRenderer != null)
                {
                    afterimageTimer = 0.03f;
                    dashTrail.Spawn(bodyRenderer);
                }
            }

            // A dash attack can cancel the dash recovery.
            if (stateTime >= moveEnd && config.dashAttackEnabled && input.ConsumeAttack(config.inputBufferSeconds))
            {
                lastDashEndTime = Time.time;
                StartAttackFromPress();
                return;
            }

            if (stateTime >= total)
            {
                lastDashEndTime = Time.time;
                EnterIdle();
            }
        }

        void EnterParry()
        {
            SetState(State.Parry);
            chargeEligible = false;
            velocityX = 0f;
            NoteDefensePress();
        }

        /// <summary>Remembers a parry attempt; pressed just after getting hit → "LATE".</summary>
        void NoteDefensePress()
        {
            if (Time.time - lastHurtTime < 0.25f) SetCallout("LATE");
            lastDefensePressTime = Time.time;
        }

        void SetCallout(string text)
        {
            Callout = text;
            CalloutTime = Time.unscaledTime;
        }

        void UpdateParry()
        {
            if (TryFlashCounterCancel()) return;
            if (stateTime >= Frames.ToSeconds(config.parryWindowFrames + config.parryWhiffRecoveryFrames)) EnterIdle();
        }

        void UpdateHurt(float dt)
        {
            Decelerate(dt);
            if (stateTime >= Frames.ToSeconds(config.hitstunFrames)) EnterIdle();
        }

        void Decelerate(float dt)
        {
            velocityX = Mathf.MoveTowards(velocityX, 0f, config.deceleration * dt);
            Translate(velocityX * dt);
        }

        // ---------------------------------------------------------------- hits

        void TryHitBoss()
        {
            if (boss == null) return;
            if (!attack.HitboxAt(transform.position, facing).Overlaps(boss.Hurtbox)) return;

            attackHasHit = true;
            // A riposte or flash counter that lands carries the held element (spent only on a hit, so a whiff keeps it).
            bool infusable = attack == config.riposte || attack == config.flashCounter;
            var infusion = infusable && config.harvestEnabled ? heldElement : Element.None;
            var result = boss.ReceiveHit(new HitInfo
            {
                attackerTeam = Team.Hero, attack = attack, sourcePosition = transform.position,
                element = infusion, extraDamage = ExtraDamage(attack, infusion),
            });
            if (result == HitResult.Hit)
            {
                GameAudio.Play(attack.damage >= 3 ? Sfx.HitHeavy : Sfx.HitLight);
                HitStop.Freeze(attack.hitStopFrames);
                CameraShake.Add(attack.shakeTrauma);
                if (infusion != Element.None) OnElementSpent(infusion);
                else if (attack == config.flashCounter) SetCallout("FLASH COUNTER!");
            }
        }

        /// <summary>The infused charged heavy: the blade releases the held element as a crescent wave.</summary>
        void LaunchWave()
        {
            attackHasHit = true;
            attackFiredWave = true;
            var element = attackWaveElement;
            var wave = config.elementWave;
            heldElement = Element.None; // spent on release; the crescent carries it
            attackWaveElement = Element.None;

            var go = new GameObject($"Hero Wave ({element})");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = waveSprite;
            sr.color = ElementColors.Of(element);
            sr.sortingOrder = 4;

            float frontX = transform.position.x + facing * (config.hurtboxSize.x * 0.5f + wave.projectileSize.x * 0.5f);
            float groundY = arena != null ? arena.groundY : transform.position.y;
            go.AddComponent<Projectile>().Launch(wave, Team.Hero, boss, new Vector2(frontX, groundY), facing, arena,
                element, ExtraDamage(wave, element), result =>
                {
                    if (result != HitResult.Hit) return;
                    GameAudio.Play(Sfx.HitHeavy);
                    HitStop.Freeze(wave.hitStopFrames);
                    CameraShake.Add(wave.shakeTrauma);
                    OnElementLanded(element);
                });
            if (artMode) ProjectileVisual.Attach(go, ProjectileVisual.Style.Crescent, ElementColors.Of(element), facing, wave.projectileSize, groundY);
            GameAudio.Play(Sfx.WaveRelease);
            CameraShake.Add(wave.shakeTrauma * 0.5f);
        }

        int ExtraDamage(AttackData data, Element element) =>
            element == Element.Fire ? Mathf.CeilToInt(data.damage * (config.fireDamageMultiplier - 1f)) : 0;

        void OnElementSpent(Element element)
        {
            heldElement = Element.None;
            OnElementLanded(element);
        }

        /// <summary>Hero-side effects of a landed element (the boss applies burn / freeze / root itself).</summary>
        void OnElementLanded(Element element)
        {
            if (element == Element.Wood) health.Heal(config.woodHeal);
            SetCallout(element switch
            {
                Element.Fire => "BURN!",
                Element.Ice => "FREEZE!",
                Element.Wood => "ROOT! +1",
                _ => Callout,
            });
        }

        /// <summary>Greybox testing (F3): cycles the held element None → Fire → Ice → Wood.</summary>
        public void DebugCycleElement() => heldElement = (Element)(((int)heldElement + 1) % 4);

        public HitResult ReceiveHit(in HitInfo hit)
        {
            if (state == State.Dead) return HitResult.Ignored;

            bool parryWindow = ParryWindowOpen;
            bool clashWindow = ClashWindowOpen;
            if ((parryWindow || clashWindow) && hit.Parryable)
            {
                OnParrySuccess(clash: !parryWindow, hit.attack != null ? hit.attack.element : Element.None);
                return HitResult.Parried;
            }
            if (IsInvulnerable) return HitResult.Dodged;

            if (parryWindow || clashWindow) SetCallout("RED = DASH");                          // tried to parry an unparryable hit
            else if (hit.Parryable && Time.time - lastDefensePressTime < EarlyCalloutSeconds) SetCallout("EARLY"); // the window closed before the hit
            OnHurt(hit);
            return HitResult.Hit;
        }

        void OnParrySuccess(bool clash, Element spell)
        {
            ParryCount++;
            if (clash) ClashCount++;
            riposteTimer = config.riposteWindowSeconds;
            if (clash && config.flashCounter != null) flashTimer = config.flashCounterWindowSeconds;
            comboIndex = 0;
            Play(feedback.parry);
            GameAudio.Play(clash ? Sfx.Clash : Sfx.Parry);
            string text = clash ? "CLASH!" : "PARRY!";
            if (spell != Element.None && config.harvestEnabled)
            {
                GameAudio.Play(Sfx.Harvest);
                heldElement = spell; // harvest: a new element replaces the old one
                HarvestCount++;
                text += $" +{spell.ToString().ToUpperInvariant()}";
            }
            SetCallout(text);
            EnterIdle(); // ends the parry / attack early: free to parry the next hit or riposte
        }

        void OnHurt(in HitInfo hit)
        {
            lastHurtTime = Time.time;
            flashTimer = 0f; // a hit closes the counter window
            health.TakeDamage(hit.Damage);
            hurtInvulnTimer = config.hurtInvulnerableSeconds;
            int away = transform.position.x >= hit.sourcePosition.x ? 1 : -1;
            velocityX = away * config.hurtKnockbackSpeed;
            chargeEligible = false;
            input.ClearBuffer();

            int attackStop = hit.attack != null ? hit.attack.hitStopFrames : 0;
            float attackTrauma = hit.attack != null ? hit.attack.shakeTrauma : 0f;
            HitStop.Freeze(Mathf.Max(feedback.heroHurt.hitStopFrames, attackStop));
            CameraShake.Add(Mathf.Max(feedback.heroHurt.trauma, attackTrauma));
            if (flash != null) flash.Flash(feedback.heroHurt.flashColor, feedback.heroHurt.flashFrames);
            GameAudio.Play(health.IsDead ? Sfx.HeroDeath : Sfx.HeroHurt);

            SetState(health.IsDead ? State.Dead : State.Hurt);
            if (health.IsDead) ClearStatuses();
            else if (hit.attackerTeam == Team.Boss && hit.attack != null) ApplyStatus(hit.attack.element);
        }

        /// <summary>A boss spell that lands (not parried) leaves its element on the hero.</summary>
        void ApplyStatus(Element element)
        {
            switch (element)
            {
                case Element.Fire:
                    burnTicksLeft = config.heroBurnTicks;
                    burnTimer = config.heroBurnIntervalSeconds;
                    SetCallout("BURNING! DASH");
                    break;
                case Element.Ice:
                    chillTimer = config.chillSeconds;
                    SetCallout("CHILLED");
                    break;
                case Element.Wood:
                    // Only the Unknown One roots the hero: his tentacles burst up and hold the hero's legs.
                    if (!IsRooted)
                        Tentacles.Bind(transform, arena != null ? arena.groundY : transform.position.y, 1.2f,
                                       () => this != null && IsRooted && state != State.Dead);
                    rootTimer = config.rootSeconds;
                    SetCallout("ROOTED");
                    break;
            }
        }

        void TickStatuses(float dt)
        {
            if (chillTimer > 0f) chillTimer -= dt;
            if (rootTimer > 0f) rootTimer -= dt;
            if (burnTicksLeft <= 0 || state == State.Dead) return;
            burnTimer -= dt;
            if (burnTimer > 0f) return;
            burnTimer += config.heroBurnIntervalSeconds;
            burnTicksLeft--;
            health.TakeDamage(config.heroBurnDamage);
            if (flash != null) flash.Flash(ElementColors.Fire, 4);
            GameAudio.Play(Sfx.BurnTick);
            if (health.IsDead)
            {
                GameAudio.Play(Sfx.HeroDeath);
                SetState(State.Dead);
                ClearStatuses();
            }
        }

        void ClearStatuses()
        {
            burnTicksLeft = 0;
            chillTimer = 0f;
            rootTimer = 0f;
        }

        void Play(FeedbackConfig.Bundle bundle)
        {
            HitStop.Freeze(bundle.hitStopFrames);
            CameraShake.Add(bundle.trauma);
            if (flash != null && bundle.flashFrames > 0) flash.Flash(bundle.flashColor, bundle.flashFrames);
        }

        // ---------------------------------------------------------------- helpers

        void SetState(State next)
        {
            state = next;
            stateTime = 0f;
            prevStateTime = 0f;
        }

        void EnterIdle()
        {
            SetState(State.Idle);
            comboIdleTime = 0f;
        }

        void FaceBoss()
        {
            if (boss == null) return;
            float dx = boss.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) facing = dx > 0f ? 1 : -1;
        }

        void Translate(float dx)
        {
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

        // ---------------------------------------------------------------- greybox visuals

        void UpdateVisuals()
        {
            if (body == null) return;
            Vector3 scale = bodyBaseScale;
            Vector3 pos = bodyBasePos;
            Color color = artMode ? Color.white : baseColor;

            switch (state)
            {
                case State.Dash:
                    scale = Deform(1.3f, 0.8f);
                    pos.y = bodyBasePos.y * 0.8f;
                    if (IsInvulnerable) color.a = artMode ? 0.6f : 0.45f;
                    break;
                case State.Attack:
                {
                    var phase = attack.PhaseAt(stateTime);
                    var motion = PoseMotion.HeroAttack(phase); // shared with the Animation Gym
                    scale = Deform(motion.squash.x, motion.squash.y);
                    pos.x += motion.lean * facing;
                    if (attackInvulnerable && phase != AttackPhase.Recovery) color = Tint(color, parryColor, 1f);
                    else if (ClashWindowOpen) color = Tint(color, parryColor, 0.35f); // clash window: slight cyan
                    break;
                }
                case State.Charging:
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
                    var charge = heldElement != Element.None && config.harvestEnabled ? ElementColors.Of(heldElement) : chargeColor;
                    color = Tint(color, charge, 0.6f + 0.4f * pulse);
                    scale = Deform(1.08f, 0.9f);
                    pos.x -= 0.15f * facing;
                    break;
                }
                case State.Parry:
                    color = ParryWindowOpen ? Tint(color, parryColor, 1f) : Tint(color, Color.black, 0.45f); // whiff = darker, vulnerable
                    break;
                case State.Hurt:
                    color = Tint(color, Color.red, 0.5f);
                    break;
                case State.Dead:
                    color = Tint(color, new Color(0.35f, 0.35f, 0.4f), 1f);
                    if (!(artMode && poses.dead != null))
                    {
                        scale = Deform(1.3f, 0.35f);
                        pos.y = bodyBasePos.y * 0.35f;
                    }
                    break;
            }

            if (riposteTimer > 0f && state == State.Idle) color = Tint(color, parryColor, 0.6f);
            // Statuses from boss spells.
            if (IsBurning) color = Tint(color, ElementColors.Fire, 0.4f + 0.3f * Mathf.Sin(Time.time * 20f));
            else if (IsChilled) color = Tint(color, ElementColors.Ice, 0.6f);
            if (IsRooted) color = Tint(color, ElementColors.Grip, 0.3f); // the tentacles holding him say the rest
            // Counter ready: a gold pulse (the counter's slash color) says "dash now".
            if (flashTimer > 0f && state != State.Dead) color = Tint(color, FlashGold, 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 24f));
            if (hurtInvulnTimer > 0f && state != State.Dead && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f) color.a *= 0.35f;
            if (state == State.Idle && Mathf.Abs(velocityX) < 0.5f) scale.y *= 1f + (artMode ? poses.idleBreathAmount : 0.02f) * Mathf.Sin(Time.time * (artMode ? poses.idleBreathRate : 2.4f)); // breathing

            // Ease toward the target so pose changes don't snap (dt = 0 in hit-stop freezes it).
            float ease = 1f - Mathf.Exp(-Time.deltaTime * MotionEase);
            easedScale = Vector3.Lerp(easedScale, scale, ease);
            easedPos = Vector3.Lerp(easedPos, pos, ease);
            body.localScale = easedScale;
            body.localPosition = easedPos;
            if (flash != null) flash.BaseColor = color;
            if (artMode)
            {
                var sprite = PoseSprite();
                if (crossfade != null && sprite != bodyRenderer.sprite && IsIdleMoveSwap(bodyRenderer.sprite, sprite)) crossfade.Begin(bodyRenderer);
                bodyRenderer.sprite = sprite;
                bodyRenderer.flipX = facing < 0; // art faces right
                if (poses.move != null && poses.move.IsValid && System.Array.IndexOf(poses.move.frames, sprite) >= 0) body.localScale = easedScale * poses.move.scale;
                else if (state == State.Attack && attack != null && attack.HasAnimation) body.localScale = easedScale * attack.PoseScaleAt(stateTime);
            }

            if (statusMark != null)
            {
                var status = state == State.Dead ? Element.None : IsRooted ? Element.Wood : IsBurning ? Element.Fire : IsChilled ? Element.Ice : Element.None;
                statusMark.enabled = status != Element.None && status != Element.Wood; // rooted: the tentacles on his legs are the mark
                if (statusMark.enabled)
                {
                    var c = status == Element.Wood ? ElementColors.Grip : ElementColors.Of(status); // rooted: the god's grip
                    c.a = status == Element.Fire ? 0.6f + 0.4f * Mathf.Sin(Time.time * 25f) : 0.85f;
                    statusMark.color = c;
                    statusMark.transform.position = transform.position + new Vector3(0f, 0.06f, 0f);
                    statusMark.transform.localScale = new Vector3(config.hurtboxSize.x * 1.6f, status == Element.Wood ? 0.3f : 0.14f, 1f);
                }
            }

            if (counterGlint != null)
            {
                counterGlint.enabled = flashTimer > 0f && state != State.Dead;
                counterGlintOutline.enabled = counterGlint.enabled;
                if (counterGlint.enabled)
                {
                    // Pops in with the clash, pulses, and fades over the window's last 0.2 s (it reads as a timer too).
                    float since = config.flashCounterWindowSeconds - flashTimer;
                    float s = Mathf.Lerp(0.7f, 0.34f, Mathf.Clamp01(since / 0.1f)) * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 24f));
                    counterGlint.transform.position = transform.position + new Vector3(0f, config.hurtboxSize.y + 0.5f, 0f);
                    counterGlint.transform.localScale = new Vector3(s, s, 1f);
                    float alpha = Mathf.Clamp01(flashTimer / 0.2f);
                    counterGlint.color = new Color(CounterGlintGold.r, CounterGlintGold.g, CounterGlintGold.b, alpha);
                    counterGlintOutline.color = new Color(0.08f, 0.07f, 0.1f, 0.85f * alpha);
                }
            }

            if (elementOrb != null)
            {
                elementOrb.enabled = heldElement != Element.None && state != State.Dead;
                if (elementOrb.enabled)
                {
                    float s = 0.32f + 0.05f * Mathf.Sin(Time.time * 9f);
                    elementOrb.color = ElementColors.Of(heldElement);
                    elementOrb.transform.position = transform.position + new Vector3((config.hurtboxSize.x * 0.5f + 0.25f) * facing, config.hurtboxSize.y * 0.55f, 0f);
                    elementOrb.transform.localScale = new Vector3(s, s, 1f);
                }
            }

            if (hitboxDebug != null)
            {
                bool show = CombatDebug.ShowHitboxes && state == State.Attack && !attackFiredWave && attack.PhaseAt(stateTime) == AttackPhase.Active;
                hitboxDebug.enabled = show;
                if (show)
                {
                    var r = attack.HitboxAt(transform.position, facing);
                    hitboxDebug.transform.position = r.center;
                    hitboxDebug.transform.localScale = new Vector3(r.width, r.height, 1f);
                }
            }
        }

        /// <summary>Greybox: full squash/stretch. Art: toned down so the drawing doesn't smear.</summary>
        Vector3 Deform(float x, float y) => PoseMotion.Deform(bodyBaseScale, x, y, artMode);

        /// <summary>Greybox: the box takes the state color. Art: a light wash over the drawing.</summary>
        static readonly Color FlashGold = new Color(1f, 0.88f, 0.55f);

        Color Tint(Color current, Color target, float amount) => Color.Lerp(current, target, artMode ? amount * PoseMotion.ArtTint : amount);

        bool IsMoveFrame(Sprite s) => poses.move != null && poses.move.IsValid && System.Array.IndexOf(poses.move.frames, s) >= 0;

        /// <summary>Idle → walk or walk → idle: the swaps that get a cross-fade (attack poses keep their snap).</summary>
        bool IsIdleMoveSwap(Sprite from, Sprite to) => (from == poses.idle && IsMoveFrame(to)) || (IsMoveFrame(from) && to == poses.idle);

        Sprite PoseSprite()
        {
            switch (state)
            {
                case State.Dash: return PoseSet.First(poses.dash, poses.idle);
                case State.Attack: return PoseSet.First(attack.PoseAt(stateTime), poses.idle);
                case State.Charging: return PoseSet.First(poses.charge, config.chargedHeavy != null ? config.chargedHeavy.startupPose : null, poses.idle);
                case State.Parry: return PoseSet.First(poses.parry, poses.idle);
                case State.Hurt: return PoseSet.First(poses.hurt, poses.idle);
                case State.Dead: return PoseSet.First(poses.dead, poses.hurt, poses.idle);
                default:
                    return Mathf.Abs(velocityX) > 0.5f && poses.move != null && poses.move.IsValid ? poses.move.Sample(moveCycleTime) : poses.idle;
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
