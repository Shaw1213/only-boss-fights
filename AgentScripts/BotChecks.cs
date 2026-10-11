using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Targeted mechanic checks in Play mode through a virtual keyboard: backdash, charged heavy,
/// 3-hit light combo, Shockwave Slam (forced via reflection) hitting a standing hero and being
/// dashed through. All waits are in game seconds: an unfocused, uncapped Editor can run at
/// hundreds of fps, so frame counts are meaningless here. Run:
///   unity command run_script --timeout 120 -- --file AgentScripts/BotChecks.cs --entry BotChecks.Run --timeout_ms 110000
/// </summary>
public static class BotChecks
{
    static Keyboard kb;
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
        var log = new StringBuilder();
        var seen = new List<string>();

        try
        {
            await Reload();

            // 1) Backdash: no direction held, boss to the right -> dash left by dashDistance (wall permitting).
            float x0 = hero.transform.position.x;
            float room = x0 - (hero.arena.minX + hero.config.hurtboxSize.x * 0.5f);
            await Hold(0.02f, Key.LeftShift);
            await Watch(0.5f, seen);
            log.AppendLine($"Backdash: {string.Join(" > ", seen)}; moved {hero.transform.position.x - x0:0.00} (expected -{Mathf.Min(hero.config.dashDistance, room):0.00})");

            // 2) Charged heavy: hold J 0.55 s, release.
            seen.Clear();
            await Hold(0.55f, Key.J, seen);
            await Watch(0.5f, seen);
            log.AppendLine($"Charge: {string.Join(" > ", seen)}");

            // 3) Light combo next to the boss: three taps 0.12 s apart.
            await WalkTo(boss.transform.position.x - 1.8f);
            int bossHp = boss.Health.Current;
            seen.Clear();
            for (int tap = 0; tap < 3; tap++)
            {
                await Hold(0.02f, Key.J, seen);
                await Watch(0.12f, seen);
            }
            await Watch(0.5f, seen);
            log.AppendLine($"Combo: {string.Join(" > ", seen)}; boss hp {bossHp} -> {boss.Health.Current}");

            // 4) Shockwave vs a standing hero (should hit), then dashed through (should not).
            await Reload();
            var slam = FindMove("B1_Move_ShockwaveSlam");
            if (slam == null) { log.AppendLine("Shockwave: move asset not found"); return log.ToString(); }

            int hp = hero.Health.Current;
            ForceMove(slam);
            bool spawned = await WaitUntil(() => Object.FindObjectsByType<Projectile>().Length > 0, 2f);
            await WaitUntil(() => Object.FindObjectsByType<Projectile>().Length == 0, 3f);
            log.AppendLine($"Shockwave (standing): spawned={spawned}; hero hp {hp} -> {hero.Health.Current}");

            await Watch(1.5f, null); // hurt invulnerability runs out
            hp = hero.Health.Current;
            ForceMove(slam);
            bool dashed = false;
            float end = Time.time + 4f;
            while (Time.time < end)
            {
                var ps = Object.FindObjectsByType<Projectile>();
                if (!dashed && ps.Length > 0 && Mathf.Abs(ps[0].transform.position.x - hero.transform.position.x) < 1.6f)
                {
                    int dir = ps[0].transform.position.x > hero.transform.position.x ? 1 : -1;
                    await Hold(0.02f, Key.LeftShift, null, dir > 0 ? Key.D : Key.A);
                    dashed = true;
                }
                if (dashed && Object.FindObjectsByType<Projectile>().Length == 0) break;
                await Awaitable.NextFrameAsync();
            }
            log.AppendLine($"Shockwave (dash through): dashed={dashed}; hero hp {hp} -> {hero.Health.Current}");
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
        await Watch(0.05f, null);
        // Park the boss in a long idle; the checks drive it explicitly.
        typeof(BossController).GetMethod("EnterIdle", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, new object[] { 999f });
    }

    static void ForceMove(BossMove move)
    {
        var t = typeof(BossController);
        t.GetField("move", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boss, move);
        t.GetMethod("StartMove", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, null);
    }

    static BossMove FindMove(string name)
    {
        foreach (var m in boss.config.phase2Moves) if (m != null && m.name == name) return m;
        return null;
    }

    /// <summary>Holds keys for a duration of game time, then releases them.</summary>
    static async Task Hold(float seconds, Key key, List<string> seen = null, Key extra = Key.None)
    {
        var keys = extra == Key.None ? new[] { key } : new[] { key, extra };
        float end = Time.time + seconds;
        do
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
            await Awaitable.NextFrameAsync();
            if (seen != null) Record(seen);
        } while (Time.time < end);
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        await Awaitable.NextFrameAsync();
        if (seen != null) Record(seen);
    }

    static async Task Watch(float seconds, List<string> seen)
    {
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            await Awaitable.NextFrameAsync();
            if (seen != null) Record(seen);
        }
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
        await Watch(0.3f, null);
    }

    static void Record(List<string> seen)
    {
        string s = hero.CurrentState == HeroController.State.Attack && hero.CurrentAttack != null
            ? hero.CurrentAttack.name
            : hero.CurrentState.ToString();
        if (seen.Count == 0 || seen[seen.Count - 1] != s) seen.Add(s);
    }
}
