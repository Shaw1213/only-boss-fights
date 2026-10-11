using System.Reflection;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode helpers for checking Boss 1 visuals (open Boss01.unity and enter Play mode first).
///   PhaseShot: drops the boss to its phase-2 threshold, waits the given seconds into the transformation,
///   and pauses for capture_game_view. Args: [seconds].
///   MoveShot / Wait / EndShot: force a move, let the fight run, or end it, then pause (see each method).
/// </summary>
public static class FightCheck
{
    public static async Task<string> PhaseShot(float seconds)
    {
        EditorApplication.isPaused = false;
        var boss = Object.FindAnyObjectByType<BossController>();
        var health = boss.GetComponent<Health>();
        int target = (int)(health.Max * boss.config.phase2AtHealth);
        typeof(BossController).GetMethod("TakeDamage", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(boss, new object[] { health.Current - target, 0f });
        float until = Time.time + seconds;
        while (Time.time < until) await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        return $"boss {health.Current}/{health.Max}, phase {boss.Phase}, state {boss.CurrentState}";
    }

    /// <summary>Forces one boss move (by asset name) and pauses the given seconds into it. Args: [name, seconds].</summary>
    public static async Task<string> MoveShot(string moveName, float seconds)
    {
        EditorApplication.isPaused = false;
        var boss = Object.FindAnyObjectByType<BossController>();
        BossMove move = null;
        foreach (var m in boss.config.phase1Moves) if (m != null && m.name == moveName) move = m;
        foreach (var m in boss.config.phase2Moves) if (m != null && m.name == moveName) move = m;
        if (move == null) return $"no move {moveName}";
        var t = typeof(BossController);
        t.GetField("move", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boss, move);
        t.GetMethod("StartMove", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, null);
        float until = Time.time + seconds;
        while (Time.time < until) await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        return $"{moveName} @ {seconds:0.00}s: state {boss.CurrentState}, step {boss.CurrentStep?.name} {boss.CurrentPhase}";
    }

    /// <summary>Flash counter from 2.4 units in front of the boss (as if just clashed); pauses this many seconds in. Args: [seconds].</summary>
    public static async Task<string> FlashShot(float seconds)
    {
        EditorApplication.isPaused = false;
        var hero = Object.FindAnyObjectByType<HeroController>();
        var boss = Object.FindAnyObjectByType<BossController>();
        typeof(BossController).GetMethod("EnterIdle", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, new object[] { 999f });
        var p = hero.transform.position;
        hero.transform.position = new Vector3(boss.transform.position.x - 2.4f, p.y, p.z);
        float settle = Time.time + 0.3f;
        while (Time.time < settle) await Awaitable.NextFrameAsync();
        var t = typeof(HeroController);
        t.GetField("flashTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hero, 0.4f);
        t.GetMethod("BeginFlashCounter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hero, null);
        float until = Time.time + seconds;
        while (Time.time < until) await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        return $"hero x {hero.transform.position.x:0.##} ({hero.CurrentState} {hero.CurrentAttack?.name} {hero.CurrentPhase}), boss x {boss.transform.position.x:0.##}";
    }

    /// <summary>Waits the given seconds (the boss plays on its own) and pauses. Args: [seconds].</summary>
    public static async Task<string> Wait(float seconds)
    {
        EditorApplication.isPaused = false;
        float until = Time.time + seconds;
        while (Time.time < until) await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        var boss = Object.FindAnyObjectByType<BossController>();
        return $"state {boss.CurrentState}, x {boss.transform.position.x:0.##}";
    }

    /// <summary>Ends the fight (win = kill the boss, else kill the hero) and pauses after the given seconds. Args: [win 1/0, seconds].</summary>
    public static async Task<string> EndShot(float win, float seconds)
    {
        EditorApplication.isPaused = false;
        var kill = ScriptableObject.CreateInstance<AttackData>();
        kill.damage = 999;
        if (win > 0f)
        {
            var boss = Object.FindAnyObjectByType<BossController>();
            boss.ReceiveHit(new HitInfo { attackerTeam = Team.Hero, attack = kill, sourcePosition = boss.transform.position });
        }
        else
        {
            var hero = Object.FindAnyObjectByType<HeroController>();
            hero.Health.TakeDamage(999);
        }
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        return $"result {Object.FindAnyObjectByType<FightFlow>().Current}";
    }
}
