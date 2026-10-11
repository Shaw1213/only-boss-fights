using System.Reflection;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Poses the Boss 1 fight for art screenshots, then pauses the Editor (capture with capture_game_view).
/// Play mode only. Entries: Idle, Windup (boss cleave windup, hero idle), Slash (hero slash, boss idle),
/// Greybox (same as Windup with the greybox boxes).
///   unity command run_script -- --file AgentScripts/ArtShots.cs --entry ArtShots.Windup
/// </summary>
public static class ArtShots
{
    const BindingFlags NonPublic = BindingFlags.NonPublic | BindingFlags.Instance;
    static HeroController hero;
    static BossController boss;

    public static async Task<string> Idle()
    {
        await Setup(true);
        EditorApplication.isPaused = true;
        return "paused: idle";
    }

    public static async Task<string> Windup() => await WindupShot(true);
    public static async Task<string> Greybox() => await WindupShot(false);

    static async Task<string> WindupShot(bool art)
    {
        await Setup(art);
        ForceMove(Find(boss.config.phase1Moves, "B1_Move_OverheadCleave"));
        float end = Time.time + 3f;
        while (Time.time < end && !(boss.CurrentPhase == AttackPhase.Startup && boss.StateTime >= boss.CurrentStep.StartupTime * 0.8f))
            await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        return $"paused: cleave windup (art={art})";
    }

    /// <summary>Boss cleave at impact, hero charging a heavy.</summary>
    public static async Task<string> Strike()
    {
        await Setup(true);
        ForceMove(Find(boss.config.phase1Moves, "B1_Move_OverheadCleave"));
        typeof(HeroController).GetMethod("EnterCharging", NonPublic).Invoke(hero, null);
        float end = Time.time + 3f;
        while (Time.time < end && boss.CurrentPhase != AttackPhase.Active) await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        return "paused: cleave impact + hero charging";
    }

    /// <summary>Boss lunge thrust, mid-lunge.</summary>
    public static async Task<string> Lunge()
    {
        await Setup(true);
        hero.transform.position = new Vector3(-6f, hero.transform.position.y, 0f);
        ForceMove(Find(boss.config.phase1Moves, "B1_Move_LungeThrust"));
        float end = Time.time + 3f;
        while (Time.time < end && boss.CurrentPhase != AttackPhase.Active) await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        return "paused: lunge thrust";
    }

    /// <summary>Hero running toward the boss (virtual keyboard), paused mid-cycle after <paramref name="seconds"/>.</summary>
    public static async Task<string> Run(float seconds, float dir)
    {
        await Setup(true);
        hero.transform.position = new Vector3(dir > 0f ? -7f : -1f, hero.transform.position.y, 0f);
        var settings = InputSystem.settings;
        var oldEditor = settings.editorInputBehaviorInPlayMode;
        var oldBackground = settings.backgroundBehavior;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var kb = InputSystem.AddDevice<Keyboard>("ShotKeyboard");
        try
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                InputSystem.QueueStateEvent(kb, new KeyboardState(dir > 0f ? Key.D : Key.A));
                await Awaitable.NextFrameAsync();
            }
            EditorApplication.isPaused = true;
            return $"paused: running at x={hero.transform.position.x:0.0}";
        }
        finally
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            InputSystem.RemoveDevice(kb);
            settings.editorInputBehaviorInPlayMode = oldEditor;
            settings.backgroundBehavior = oldBackground;
        }
    }

    public static async Task<string> Slash()
    {
        await Setup(true);
        hero.transform.position = new Vector3(boss.transform.position.x - 2.6f, hero.transform.position.y, 0f);
        typeof(HeroController).GetMethod("StartAttackFromPress", NonPublic).Invoke(hero, null);
        float end = Time.time + 1f;
        while (Time.time < end && hero.CurrentPhase != AttackPhase.Active) await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        return "paused: hero slash";
    }

    static async Task Setup(bool art)
    {
        EditorApplication.isPaused = false;
        var old = Object.FindAnyObjectByType<HeroController>();
        var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
        while (!op.isDone) await Awaitable.NextFrameAsync();
        for (int i = 0; i < 1000; i++)
        {
            hero = Object.FindAnyObjectByType<HeroController>();
            if (hero != null && !ReferenceEquals(hero, old)) break;
            await Awaitable.NextFrameAsync();
        }
        boss = Object.FindAnyObjectByType<BossController>();
        typeof(BossController).GetMethod("EnterIdle", NonPublic).Invoke(boss, new object[] { 999f });
        hero.SetArtMode(art);
        boss.SetArtMode(art);
        hero.transform.position = new Vector3(-1.5f, hero.transform.position.y, 0f);
        boss.transform.position = new Vector3(2.5f, boss.transform.position.y, 0f);
        float until = Time.time + 0.3f;
        while (Time.time < until) await Awaitable.NextFrameAsync();
    }

    static void ForceMove(BossMove move)
    {
        var t = typeof(BossController);
        t.GetField("move", NonPublic).SetValue(boss, move);
        t.GetMethod("StartMove", NonPublic).Invoke(boss, null);
    }

    static BossMove Find(BossMove[] moves, string name)
    {
        foreach (var m in moves) if (m != null && m.name == name) return m;
        return null;
    }
}
