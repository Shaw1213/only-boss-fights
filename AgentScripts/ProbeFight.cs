using DreamLayerJam;
using UnityEngine;

/// <summary>
/// Read-only snapshot of the running fight, for checking the greybox from the CLI in Play mode:
///   unity command run_script --file AgentScripts/ProbeFight.cs --entry ProbeFight.Snapshot
/// </summary>
public static class ProbeFight
{
    public static string Snapshot()
    {
        if (!Application.isPlaying) return "not in play mode";
        FirstTimeCoach.Suppressed = true; // bots play the fight, the first-time coach would pause it
        var hero = Object.FindAnyObjectByType<HeroController>();
        var boss = Object.FindAnyObjectByType<BossController>();
        var flow = Object.FindAnyObjectByType<FightFlow>();
        if (hero == null || boss == null) return "hero or boss not found";

        string bossMove = boss.CurrentMove != null ? $"{boss.CurrentMove.name} step {boss.StepIndex + 1}/{boss.CurrentMove.steps.Length} {boss.CurrentPhase}" : "-";
        return $"t={Time.time:0.00}s timeScale={Time.timeScale:0.00} | " +
               $"Hero {hero.CurrentState} hp {hero.Health.Current}/{hero.Health.Max} x={hero.transform.position.x:0.00} | " +
               $"Boss {boss.CurrentState} phase {boss.Phase} hp {boss.Health.Current}/{boss.Health.Max} stagger {boss.Stagger:0} x={boss.transform.position.x:0.00} move {bossMove} | " +
               $"Result {(flow != null ? flow.Current.ToString() : "?")}";
    }
}
