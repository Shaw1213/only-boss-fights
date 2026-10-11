using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Play-mode checks for Harvest, driven through a virtual mouse + keyboard (waits in game seconds):
/// parry the Flame Wave → hold Fire; a charged heavy fires the fire wave (bonus damage + burn);
/// ice freezes the boss's clock; wood roots it and heals; a riposte carries the element; light attacks
/// don't spend it; the red Shockwave Slam can't be harvested.
///   unity command run_script --timeout 160 -- --file AgentScripts/BotHarvestChecks.cs --entry BotHarvestChecks.Run --timeout_ms 150000
/// </summary>
public static class BotHarvestChecks
{
    const BindingFlags NonPublic = BindingFlags.NonPublic | BindingFlags.Instance;
    static Keyboard kb;
    static Mouse mouse;
    static HeroController hero;
    static BossController boss;

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
        mouse = InputSystem.AddDevice<Mouse>("BotMouse");
        var log = new StringBuilder();

        try
        {
            await Reload();
            typeof(BossController).GetProperty("Phase").SetValue(boss, 2); // the Flame Wave is a phase-2 move
            var flame = FindMove("B1_Move_FlameWave", boss.config.phase2Moves);
            var cleave = FindMove("B1_Move_OverheadCleave", boss.config.phase2Moves);
            var thrust = FindMove("B1_Move_LungeThrust", boss.config.phase2Moves);
            var slam = FindMove("B1_Move_ShockwaveSlam", boss.config.phase2Moves);
            var cfg = hero.config;
            log.AppendLine($"setup: flame wave in phase 2 = {flame != null}, elementWave = {cfg.elementWave != null}, boss hp {boss.Health.Current}");

            // A) Parry the Flame Wave's fireball as it arrives -> harvest Fire.
            await WalkTo(boss.transform.position.x - 5.5f);
            int hp = hero.Health.Current;
            ForceMove(flame);
            bool pressed = await ParryProjectile(0.45f);
            await Watch(0.3f);
            log.AppendLine($"A parry fireball:     pressed={pressed}, held={hero.HeldElement}, callout '{hero.Callout}', hero hp {hp}->{hero.Health.Current}, harvests {hero.HarvestCount}");
            await Park();

            // B) Charged heavy with Fire: a crescent wave, +50% damage (rounded up), then burn ticks.
            boss.Health.Init(boss.config.maxHealth);
            int before = boss.Health.Current;
            await HoldAttack(0.55f);
            bool landed = await WaitUntil(() => boss.Health.Current < before, 1.5f);
            int afterHit = boss.Health.Current;
            log.AppendLine($"B fire wave:          landed={landed}, damage {before - afterHit} (expect {cfg.elementWave.damage + Mathf.CeilToInt(cfg.elementWave.damage * (cfg.fireDamageMultiplier - 1f))}), burning={boss.IsBurning}, held={hero.HeldElement}, callout '{hero.Callout}'");
            await Watch(cfg.fireBurnIntervalSeconds * cfg.fireBurnTicks + 0.3f);
            log.AppendLine($"  burn:               {afterHit}->{boss.Health.Current} (expect -{cfg.fireBurnTicks * cfg.fireBurnDamage}), still burning={boss.IsBurning}");

            // C) Ice: the boss's clock stops. Start a Cleave while frozen: its windup must hold.
            await WalkTo(boss.transform.position.x - 6.5f);
            SetElement(Element.Ice);
            await HoldAttack(0.55f);
            bool frozen = await WaitUntil(() => boss.IsFrozen, 1.5f);
            float bossX = boss.transform.position.x;
            ForceMove(cleave);
            await Watch(0.8f);
            log.AppendLine($"C ice wave:           frozen={frozen}, windup held: phase {boss.CurrentPhase} at {boss.StateTime:0.000}s, boss moved {Mathf.Abs(boss.transform.position.x - bossX):0.00}, callout '{hero.Callout}'");
            bool thawed = await WaitUntil(() => !boss.IsFrozen, cfg.iceFreezeSeconds);
            await WaitUntil(() => boss.CurrentPhase != AttackPhase.Startup, 1.5f);
            log.AppendLine($"  thaw:               thawed={thawed}, then the cleave continued: {boss.CurrentPhase}");
            await Park();

            // D) Wood: root + heal. A Lunge Thrust while rooted must stay in place.
            hero.Health.Init(cfg.maxHealth);
            hero.Health.TakeDamage(2);
            hp = hero.Health.Current;
            SetElement(Element.Wood);
            await HoldAttack(0.55f);
            bool rooted = await WaitUntil(() => boss.IsRooted, 1.5f);
            log.AppendLine($"D wood wave:          rooted={rooted}, hero hp {hp}->{hero.Health.Current}, callout '{hero.Callout}'");
            bossX = boss.transform.position.x;
            ForceMove(thrust);
            await WaitUntil(() => boss.CurrentPhase == AttackPhase.Recovery, 2f);
            log.AppendLine($"  lunge while rooted: boss moved {Mathf.Abs(boss.transform.position.x - bossX):0.00} (a normal lunge moves {thrust.steps[0].lungeDistance:0.0})");
            await Park();

            // E) Light attacks don't spend the element.
            await WalkTo(boss.transform.position.x - 2.2f);
            SetElement(Element.Fire);
            await Click(MouseButton.Left);
            await Watch(0.5f);
            log.AppendLine($"E light attack:       held after a light hit = {hero.HeldElement}");
            await Watch(0.4f);

            // F) Riposte carries the element: parry a Cleave, riposte, expect riposte damage + fire bonus.
            boss.Health.Init(boss.config.maxHealth);
            before = boss.Health.Current;
            ForceMove(cleave);
            await PressBefore(8f, MouseButton.Right);
            await WaitUntil(() => boss.CurrentPhase == AttackPhase.Recovery, 1f);
            await Click(MouseButton.Left);
            await Watch(0.4f);
            log.AppendLine($"F fire riposte:       damage {before - boss.Health.Current} (expect {cfg.riposte.damage + Mathf.CeilToInt(cfg.riposte.damage * (cfg.fireDamageMultiplier - 1f))}), burning={boss.IsBurning}, held={hero.HeldElement}, callout '{hero.Callout}'");
            await Park();

            // G) The red Shockwave Slam can't be parried, so it can't be harvested.
            SetElement(Element.None);
            hero.Health.Init(cfg.maxHealth);
            await WalkTo(boss.transform.position.x - 2.0f);
            ForceMove(slam);
            await PressBefore(4f, MouseButton.Right);
            await Watch(0.3f);
            log.AppendLine($"G parry the red slam: held={hero.HeldElement}, callout '{hero.Callout}'");
            return log.ToString();
        }
        finally
        {
            if (kb != null) { InputSystem.QueueStateEvent(kb, new KeyboardState()); InputSystem.RemoveDevice(kb); }
            if (mouse != null) { InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.RemoveDevice(mouse); }
            settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            settings.backgroundBehavior = oldBackground;
        }
    }

    // ------------------------------------------------------------------ helpers

    static void SetButton(Mouse m, MouseButton button, bool down) =>
        InputSystem.QueueStateEvent(m, down ? new MouseState().WithButton(button) : new MouseState());

    static void SetElement(Element e)
    {
        for (int i = 0; i < 4 && hero.HeldElement != e; i++) hero.DebugCycleElement();
    }

    /// <summary>Right-clicks when a boss projectile is within <paramref name="gap"/> units of the hero's hurtbox.</summary>
    static async Task<bool> ParryProjectile(float gap)
    {
        float end = Time.time + 4f;
        while (Time.time < end)
        {
            var hurt = hero.Hurtbox;
            foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                float half = p.transform.localScale.x * 0.5f;
                float distance = Mathf.Max(p.transform.position.x - half - hurt.xMax, hurt.xMin - (p.transform.position.x + half));
                if (distance <= gap)
                {
                    await Click(MouseButton.Right);
                    return true;
                }
            }
            await Awaitable.NextFrameAsync();
        }
        return false;
    }

    static async Task HoldAttack(float seconds)
    {
        SetButton(mouse, MouseButton.Left, true);
        await Watch(seconds);
        SetButton(mouse, MouseButton.Left, false);
        await Awaitable.NextFrameAsync();
    }

    /// <summary>Waits until the current boss step is this many frames from going active, then clicks.</summary>
    static async Task PressBefore(float frames, MouseButton button)
    {
        float end = Time.time + 3f;
        while (Time.time < end)
        {
            var step = boss.CurrentStep;
            if (step != null && boss.CurrentPhase == AttackPhase.Startup && (step.ActiveStart - boss.StateTime) * Frames.PerSecond <= frames)
                break;
            await Awaitable.NextFrameAsync();
        }
        await Click(button);
    }

    static async Task Click(MouseButton button)
    {
        SetButton(mouse, button, true);
        await Watch(0.02f);
        SetButton(mouse, button, false);
        await Awaitable.NextFrameAsync();
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
        await Watch(0.05f);
        SetBossIdle(999f);
    }

    /// <summary>Waits for the boss's move, the hero's hurt invulnerability and any projectiles to finish, then parks the boss.</summary>
    static async Task Park()
    {
        await WaitUntil(() => boss.CurrentState != BossController.State.Attacking && boss.CurrentState != BossController.State.Staggered && !boss.IsFrozen, 5f);
        SetBossIdle(999f);
        await WaitUntil(() => Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0, 3f);
        await Watch(1.1f);
    }

    static void SetBossIdle(float seconds) =>
        typeof(BossController).GetMethod("EnterIdle", NonPublic).Invoke(boss, new object[] { seconds });

    static void ForceMove(BossMove move)
    {
        var t = typeof(BossController);
        t.GetField("move", NonPublic).SetValue(boss, move);
        t.GetMethod("StartMove", NonPublic).Invoke(boss, null);
    }

    static BossMove FindMove(string name, BossMove[] moves) => moves.FirstOrDefault(m => m != null && m.name == name);

    static async Task Watch(float seconds)
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

    static async Task WalkTo(float x)
    {
        float end = Time.time + 4f;
        while (Time.time < end && Mathf.Abs(hero.transform.position.x - x) > 0.15f)
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState(hero.transform.position.x < x ? Key.D : Key.A));
            await Awaitable.NextFrameAsync();
        }
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        await Watch(0.2f);
    }
}
