using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Play-mode checks for the final form's leap and for boss spells landing on the hero (open BossFinal.unity first):
///   A-C  Leap Smash: out of reach in the air, the mark follows the hero and then locks, a hero who stays takes 3,
///        a hero who leaves after the lock is safe, and he lands on the locked spot.
///   D    Heaven Fall: after the landing, the red slam sends a shockwave both ways.
///   E-G  Spells that hit the hero: fire burns (ticks; a dash puts it out), ice chills (half walk speed), wood roots
///        (no walking, no dashing).
/// Hits are delivered directly; movement goes through a virtual keyboard (same path as a player).
///   unity command run_script --timeout 90 -- --file AgentScripts/BotSpellChecks.cs --entry BotSpellChecks.Run --timeout_ms 80000
/// </summary>
public static class BotSpellChecks
{
    static Keyboard kb;
    static HeroController hero;
    static BossController boss;
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public static async Task<string> Run()
    {
        if (!Application.isPlaying) return "enter play mode first";
        FirstTimeCoach.Suppressed = true; // bots play the fight, the first-time coach would pause it
        var settings = InputSystem.settings;
        var oldEditorBehavior = settings.editorInputBehaviorInPlayMode;
        var oldBackground = settings.backgroundBehavior;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        kb = InputSystem.AddDevice<Keyboard>("BotKeyboard");
        var log = new StringBuilder();
        try
        {
            await Reload();
            var leap = FindMove("U_Move_LeapSmash", boss.config.phase1Moves);
            var fall = FindMove("U_Move_HeavenFall", boss.config.phase2Moves);
            if (leap == null || fall == null) return "leap moves missing: run BuildBossFinal";

            // A) Leap, hero stands still: out of reach in the air, the mark tracks, then he lands on the hero for 3.
            Place(-3f, 3f);
            int hp = hero.Health.Current;
            ForceMove(leap);
            await Seconds(0.45f);
            float air = (float)typeof(BossController).GetField("airHeight", Private).GetValue(boss);
            bool outOfReach = boss.Hurtbox.yMin > hero.Hurtbox.yMax;
            SetX(hero, -1f);
            await Seconds(0.15f);
            float tracked = boss.CurrentHitbox.center.x;
            await WaitUntil(() => boss.CurrentPhase == AttackPhase.Recovery, 3f);
            log.AppendLine($"A leap, stay:     air {air:0.0} (out of reach={outOfReach}), mark followed to {tracked:0.0} (hero -1.0), " +
                           $"landed at {boss.transform.position.x:0.0}, hero hp {hp}->{hero.Health.Current} (expect -3)");
            await Park();

            // B) Leap, hero leaves on the lock: the mark stays put, he lands on the empty spot.
            Place(-2f, 3f);
            hero.Health.Heal(99);
            hp = hero.Health.Current;
            ForceMove(leap);
            var step = boss.CurrentStep;
            float lockAt = step.ActiveStart - Frames.ToSeconds(step.leapLockFrames);
            await WaitUntil(() => boss.StateTime >= lockAt, 3f);
            float lockedX = boss.CurrentHitbox.center.x;
            await Hold(Key.A, 0.3f); // walk out after the lock (the "move now" moment), as a player would
            float markAfter = boss.CurrentHitbox.center.x;
            await WaitUntil(() => boss.CurrentPhase == AttackPhase.Recovery, 3f);
            log.AppendLine($"B leap, leave:    locked at {lockedX:0.0}, mark after the hero left {markAfter:0.0}, hero at {hero.transform.position.x:0.0}, " +
                           $"hero hp {hp}->{hero.Health.Current} (expect no change)");
            await Park();

            // C) Hitting him in the air does nothing (the hurtbox is up there with him).
            Place(-0.5f, 0.5f);
            int bossHp = boss.Health.Current;
            ForceMove(leap);
            await Seconds(0.5f);
            bool hit = boss.Hurtbox.Overlaps(hero.config.lightCombo[0].HitboxAt(hero.transform.position, hero.Facing));
            log.AppendLine($"C swing at him in the air: hitbox reaches him={hit} (expect False), boss hp {bossHp}->{boss.Health.Current}");
            SetX(hero, -6f);
            await WaitUntil(() => boss.CurrentState != BossController.State.Attacking, 4f);
            await Park();

            // D) Heaven Fall: the landing, then a red slam with a shockwave each way.
            typeof(BossController).GetProperty("Phase").SetValue(boss, 2);
            Place(-6f, 1f);
            hero.Health.Heal(99);
            ForceMove(fall);
            int maxWaves = 0, left = 0, right = 0;
            float end = Time.time + 3.5f;
            while (Time.time < end && boss.CurrentState == BossController.State.Attacking)
            {
                int n = 0, l = 0, r = 0;
                foreach (var p in Object.FindObjectsByType<Projectile>())
                    if (p.Owner == Team.Boss) { n++; if (p.Direction < 0) l++; else r++; }
                if (n > maxWaves) { maxWaves = n; left = l; right = r; }
                await Awaitable.NextFrameAsync();
            }
            log.AppendLine($"D heaven fall:    shockwaves {maxWaves} (left {left}, right {right}; expect 1 + 1)");
            await Park();

            // E) Fire lands: burning, a tick of damage, and a dash puts the rest out.
            Place(-6f, 6f);
            hero.Health.Heal(99);
            hp = hero.Health.Current;
            Hit(Element.Fire);
            bool burning = hero.IsBurning;
            await Seconds(1.1f);
            int afterTick = hero.Health.Current;
            await Tap(Key.Space);
            await Seconds(1.2f);
            log.AppendLine($"E fire:           burning={burning}, hp {hp} -> {afterTick} after the hit + 1 tick (expect -2), " +
                           $"dash -> burning={hero.IsBurning}, hp {hero.Health.Current} (no more ticks), callout '{hero.Callout}'");

            // F) Ice lands: chilled, walking at half speed.
            await Seconds(0.8f);
            hero.Health.Heal(99);
            SetX(hero, 3f);
            float normal = await WalkSpeed();
            Hit(Element.Ice);
            await Seconds(0.4f);
            SetX(hero, 3f);
            float chilled = await WalkSpeed();
            log.AppendLine($"F ice:            chilled={hero.IsChilled}, walk {normal:0.0} -> {chilled:0.0} u/s (expect half)");

            // G) Wood lands: rooted, no walking, no dashing; free again after.
            await Seconds(2.6f);
            hero.Health.Heal(99);
            Hit(Element.Wood);
            await Seconds(0.3f);
            bool rooted = hero.IsRooted;
            float x0 = hero.transform.position.x;
            await Hold(Key.D, 0.3f);
            float walked = hero.transform.position.x - x0;
            await Tap(Key.Space);
            string stateAfterDash = hero.CurrentState.ToString();
            await Seconds(1.0f);
            log.AppendLine($"G wood:           rooted={rooted}, walked {walked:0.00} (expect 0), dash -> {stateAfterDash} (expect no Dash), " +
                           $"rooted after 1.3 s={hero.IsRooted}");
            return log.ToString();
        }
        finally
        {
            if (kb != null) { InputSystem.QueueStateEvent(kb, new KeyboardState()); InputSystem.RemoveDevice(kb); }
            settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            settings.backgroundBehavior = oldBackground;
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A boss spell of this element lands on the hero (as if not parried).</summary>
    static void Hit(Element element)
    {
        typeof(HeroController).GetField("hurtInvulnTimer", Private).SetValue(hero, 0f);
        var spell = ScriptableObject.CreateInstance<AttackData>();
        spell.damage = 1;
        spell.element = element;
        spell.hitStopFrames = 0;
        hero.ReceiveHit(new HitInfo { attackerTeam = Team.Boss, attack = spell, sourcePosition = boss.transform.position });
    }

    static async Task<float> WalkSpeed()
    {
        float x0 = hero.transform.position.x;
        await Hold(Key.A, 0.15f); // reach full speed
        x0 = hero.transform.position.x;
        float t0 = Time.time;
        await Hold(Key.A, 0.3f);
        return Mathf.Abs(hero.transform.position.x - x0) / Mathf.Max(0.01f, Time.time - t0);
    }

    static void Place(float heroX, float bossX)
    {
        SetX(hero, heroX);
        SetX(boss, bossX);
        typeof(BossController).GetMethod("EnterIdle", Private).Invoke(boss, new object[] { 999f });
    }

    static void SetX(Component c, float x)
    {
        var p = c.transform.position;
        p.x = x;
        c.transform.position = p;
    }

    static async Task Reload()
    {
        var oldHero = Object.FindAnyObjectByType<HeroController>();
        var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
        while (!op.isDone) await Awaitable.NextFrameAsync();
        for (int i = 0; i < 1000; i++)
        {
            hero = Object.FindAnyObjectByType<HeroController>();
            if (hero != null && !ReferenceEquals(hero, oldHero)) break;
            await Awaitable.NextFrameAsync();
        }
        boss = Object.FindAnyObjectByType<BossController>();
        await Seconds(0.05f);
        typeof(BossController).GetMethod("EnterIdle", Private).Invoke(boss, new object[] { 999f });
    }

    static async Task Park()
    {
        await WaitUntil(() => boss.CurrentState != BossController.State.Attacking, 4f);
        typeof(BossController).GetMethod("EnterIdle", Private).Invoke(boss, new object[] { 999f });
        await Seconds(1.1f);
    }

    static void ForceMove(BossMove move)
    {
        typeof(BossController).GetField("move", Private).SetValue(boss, move);
        typeof(BossController).GetMethod("StartMove", Private).Invoke(boss, null);
    }

    static BossMove FindMove(string name, BossMove[] moves)
    {
        foreach (var m in moves) if (m != null && m.name == name) return m;
        return null;
    }

    static async Task Tap(Key key)
    {
        InputSystem.QueueStateEvent(kb, new KeyboardState(key));
        await Seconds(0.03f);
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        await Awaitable.NextFrameAsync();
    }

    static async Task Hold(Key key, float seconds)
    {
        InputSystem.QueueStateEvent(kb, new KeyboardState(key));
        await Seconds(seconds);
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        await Awaitable.NextFrameAsync();
    }

    static async Task Seconds(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) await Awaitable.NextFrameAsync();
    }

    static async Task<bool> WaitUntil(System.Func<bool> condition, float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            if (condition()) return true;
            await Awaitable.NextFrameAsync();
        }
        return condition();
    }
}
