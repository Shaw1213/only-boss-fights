using System.Threading.Tasks;
using DreamLayerJam;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Restarts the fight and pauses the Editor late in the boss's first windup (telegraph visible), so a
/// screenshot can be taken. Unpause with EditorApplication.isPaused = false.
///   unity command run_script --timeout 60 -- --file AgentScripts/PauseOnWindup.cs --entry PauseOnWindup.Run --timeout_ms 50000
/// </summary>
public static class PauseOnWindup
{
    public static async Task<string> Run()
    {
        if (!Application.isPlaying) return "enter play mode first";
        FirstTimeCoach.Suppressed = true; // bots play the fight, the first-time coach would pause it
        var oldHero = Object.FindAnyObjectByType<HeroController>();
        var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
        while (!op.isDone) await Awaitable.NextFrameAsync();

        HeroController hero = null;
        for (int i = 0; i < 1000 && (hero == null || ReferenceEquals(hero, oldHero)); i++)
        {
            hero = Object.FindAnyObjectByType<HeroController>();
            await Awaitable.NextFrameAsync();
        }
        var boss = Object.FindAnyObjectByType<BossController>();

        float end = Time.time + 10f;
        while (Time.time < end)
        {
            var step = boss.CurrentStep;
            if (step != null && boss.CurrentPhase == AttackPhase.Startup && boss.StateTime >= step.StartupTime * 0.7f)
            {
                EditorApplication.isPaused = true;
                return $"paused in {boss.CurrentMove.name} windup (cue {step.cue}) at t={Time.time:0.00}s";
            }
            await Awaitable.NextFrameAsync();
        }
        return "no windup seen within 10 s";
    }
}
