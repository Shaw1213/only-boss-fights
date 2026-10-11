using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Play-mode checks for solid bodies (BodyBlock), in Boss01:
///   A  walking into him stops at his body (never closer than contact)
///   B  the dash goes through him and comes out the other side
///   C  his lunge stops at the hero
///   D  overlapping at a wall: nobody is pushed out of the arena, they end apart
/// Movement goes through a virtual keyboard (same path as a player).
///   unity command run_script --timeout 90 -- --file AgentScripts/BotBodyChecks.cs --entry BotBodyChecks.Run --timeout_ms 80000
/// </summary>
public static class BotBodyChecks
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
            float contact = (hero.config.hurtboxSize.x + boss.config.hurtboxSize.x) * 0.5f;
            log.AppendLine($"contact distance {contact:0.00} (hero {hero.config.hurtboxSize.x} + boss {boss.config.hurtboxSize.x}, halved)");

            // A) Walk into him for 1.5 s.
            Place(-4f, 0f);
            float closest = await HoldTracking(Key.D, 1.5f);
            log.AppendLine($"A walk into him:  closest {closest:0.00}, hero stopped at {hero.transform.position.x:0.00} (expect >= {contact - 0.01f:0.00}, at about {-contact:0.00})");

            // B) Dash through: from contact, dash right.
            await Seconds(0.3f);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.D, Key.Space));
            await Seconds(0.05f);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            await Seconds(0.6f);
            float after = hero.transform.position.x - boss.transform.position.x;
            log.AppendLine($"B dash through:   hero {after:+0.00;-0.00} from him after the dash (expect > {contact:0.00}: the far side)");
            await Seconds(0.5f);

            // C) His lunge stops at the hero (hero in front, parked; the hit lands, the body doesn't pass).
            var lunge = FindMove("B1_Move_LungeThrust");
            if (lunge == null) log.AppendLine("C lunge:          B1_Move_LungeThrust not found");
            else
            {
                Place(-3.5f, 2f);
                hero.Health.Heal(99);
                ForceMove(lunge);
                float minGap = float.PositiveInfinity;
                float end = Time.time + 3f;
                while (Time.time < end && boss.CurrentState == BossController.State.Attacking)
                {
                    await Awaitable.EndOfFrameAsync();
                    minGap = Mathf.Min(minGap, Mathf.Abs(hero.transform.position.x - boss.transform.position.x));
                }
                log.AppendLine($"C lunge:          closest {minGap:0.00} (expect >= {contact - 0.01f:0.00}), hero hp {hero.Health.Current}/{hero.Health.Max}, " +
                               $"hero still left of him = {hero.transform.position.x < boss.transform.position.x}");
                await Park();
            }

            // D) Overlapping at the left wall (as if he landed on a cornered hero).
            var arena = boss.arena;
            float wallX = arena.minX + hero.config.hurtboxSize.x * 0.5f;
            Place(wallX, wallX + 0.4f);
            await Seconds(0.4f);
            float gap = boss.transform.position.x - hero.transform.position.x;
            log.AppendLine($"D at the wall:    hero {hero.transform.position.x:0.00} (wall {wallX:0.00}), gap {gap:0.00} (expect >= {contact - 0.01f:0.00}, hero inside the arena)");
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

    static async Task<float> HoldTracking(Key key, float seconds)
    {
        float closest = float.PositiveInfinity;
        InputSystem.QueueStateEvent(kb, new KeyboardState(key));
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            await Awaitable.EndOfFrameAsync(); // what was drawn: after LateUpdate, where bodies are resolved
            closest = Mathf.Min(closest, Mathf.Abs(hero.transform.position.x - boss.transform.position.x));
        }
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        await Awaitable.NextFrameAsync();
        return closest;
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
        float end = Time.time + 4f;
        while (Time.time < end && boss.CurrentState == BossController.State.Attacking) await Awaitable.NextFrameAsync();
        typeof(BossController).GetMethod("EnterIdle", Private).Invoke(boss, new object[] { 999f });
        await Seconds(1.1f);
    }

    static void ForceMove(BossMove move)
    {
        typeof(BossController).GetField("move", Private).SetValue(boss, move);
        typeof(BossController).GetMethod("StartMove", Private).Invoke(boss, null);
    }

    static BossMove FindMove(string name)
    {
        foreach (var moves in new[] { boss.config.phase1Moves, boss.config.phase2Moves })
            foreach (var m in moves) if (m != null && m.name == name) return m;
        return null;
    }

    static async Task Seconds(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) await Awaitable.NextFrameAsync();
    }
}
