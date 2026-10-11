using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Scripted playtest bot: drives the hero through a virtual keyboard (real Input System events,
/// same code path as a player) to exercise parry, riposte, light combo, charged heavy, dash,
/// stagger and phase 2. It reads the boss's frame data, so it parries "perfectly" — it tests that
/// the systems work, not whether the fight is fun. Run in Play mode:
///   unity command run_script --timeout 120 -- --file AgentScripts/BotFight.cs --entry BotFight.Run --args "[45]" --timeout_ms 110000
/// </summary>
public static class BotFight
{
    public static async Task<string> Run(double seconds)
    {
        if (!Application.isPlaying) return "enter play mode first";
        FirstTimeCoach.Suppressed = true; // bots play the fight, the first-time coach would pause it

        var settings = InputSystem.settings;
        var oldEditorBehavior = settings.editorInputBehaviorInPlayMode;
        var oldBackground = settings.backgroundBehavior;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var kb = InputSystem.AddDevice<Keyboard>("BotKeyboard");
        var log = new StringBuilder();

        try
        {
            // Fresh fight.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            for (int i = 0; i < 5; i++) await Awaitable.NextFrameAsync();
            var hero = Object.FindAnyObjectByType<HeroController>();
            var boss = Object.FindAnyObjectByType<BossController>();
            var flow = Object.FindAnyObjectByType<FightFlow>();

            float end = Time.realtimeSinceStartup + (float)seconds;
            float reactedStepStart = -1f;
            int parryPresses = 0, dashes = 0, ripostes = 0, charges = 0, staggers = 0;
            int heroHp = hero.Health.Current, bossHp = boss.Health.Current;
            int chargeFrames = 0;
            bool jDown = false;
            var lastBossState = boss.CurrentState;
            int lastPhase = boss.Phase;
            var keys = new List<Key>();
            var parriedProjectiles = new HashSet<Projectile>();
            bool heldLogged = false;

            while (Time.realtimeSinceStartup < end && flow.Current == FightFlow.Result.Fighting)
            {
                keys.Clear();
                float dx = boss.transform.position.x - hero.transform.position.x;
                int toward = dx > 0f ? 1 : -1;
                float dist = Mathf.Abs(dx);
                bool busy = false;

                // 1) Defense: parry white-glint hits just before they go active; dash through red ones.
                var step = boss.CurrentStep;
                if (step != null && boss.CurrentPhase == AttackPhase.Startup)
                {
                    float stepStart = Time.time - boss.StateTime;
                    float framesToActive = (step.ActiveStart - boss.StateTime) * Frames.PerSecond;
                    if (framesToActive <= 4f && Mathf.Abs(stepStart - reactedStepStart) > 0.001f)
                    {
                        reactedStepStart = stepStart;
                        if (step.parryable && step.hitboxSize.x > 0f) { keys.Add(Key.K); parryPresses++; busy = true; }
                    }
                }
                // Area attacks (floor marker): walk out of the marked spot, or dash out if it's about to erupt.
                if (!busy && step != null && step.cue == TelegraphCue.FloorMarker && boss.CurrentPhase == AttackPhase.Startup)
                {
                    var zone = boss.CurrentHitbox;
                    var hurt = hero.Hurtbox;
                    if (zone.xMin - 0.3f < hurt.xMax && hurt.xMin < zone.xMax + 0.3f)
                    {
                        int away = hero.transform.position.x < zone.center.x ? -1 : 1;
                        if (hero.transform.position.x + away * 3f < -7f || hero.transform.position.x + away * 3f > 7f) away = -away; // not into a wall
                        float framesLeft = (step.ActiveStart - boss.StateTime) * Frames.PerSecond;
                        if (framesLeft < 10f && hero.CurrentState == HeroController.State.Idle) { keys.Add(Key.LeftShift); dashes++; }
                        keys.Add(away > 0 ? Key.D : Key.A);
                        busy = true;
                    }
                }
                foreach (var p in Object.FindObjectsByType<Projectile>())
                {
                    if (p.Owner != Team.Boss || p.Attack == null) continue;
                    float pd = p.transform.position.x - hero.transform.position.x;
                    if (p.Direction * pd > 0f) continue; // moving away
                    float gap = Mathf.Abs(pd) - p.Attack.projectileSize.x * 0.5f - hero.Hurtbox.width * 0.5f;
                    float framesToContact = gap / p.Attack.projectileSpeed * Frames.PerSecond;
                    if (p.Attack.parryable)
                    {
                        if (framesToContact <= 4f && parriedProjectiles.Add(p)) { keys.Add(Key.K); parryPresses++; busy = true; break; }
                    }
                    else if (Mathf.Abs(pd) < 1.6f && hero.CurrentState == HeroController.State.Idle)
                    {
                        keys.Add(Key.LeftShift);
                        keys.Add(pd > 0f ? Key.D : Key.A); // dash through it
                        dashes++;
                        busy = true;
                        break;
                    }
                }

                // 2) Offense.
                if (!busy)
                {
                    bool punish = boss.CurrentState == BossController.State.Staggered ||
                                  (boss.CurrentMove != null && boss.StepIndex == boss.CurrentMove.steps.Length - 1 && boss.CurrentPhase == AttackPhase.Recovery);
                    if (chargeFrames > 0)
                    {
                        chargeFrames--;
                        if (chargeFrames > 0) keys.Add(Key.J); // release on the last frame
                    }
                    else if (hero.RiposteReady)
                    {
                        if (!jDown) { keys.Add(Key.J); ripostes++; }
                    }
                    else if (boss.CurrentState == BossController.State.Staggered && dist < 2.6f && hero.CurrentState == HeroController.State.Idle && charges == staggers - 1)
                    {
                        chargeFrames = 30; // hold ~0.5 s, then release: charged heavy
                        charges++;
                        keys.Add(Key.J);
                    }
                    else if (punish && dist < 2.4f)
                    {
                        if (!jDown) keys.Add(Key.J); // tap every other frame: light combo
                    }
                    else if (dist > 2.2f && (step == null || boss.CurrentPhase != AttackPhase.Startup))
                    {
                        keys.Add(toward > 0 ? Key.D : Key.A);
                    }
                }

                jDown = keys.Contains(Key.J);
                InputSystem.QueueStateEvent(kb, new KeyboardState(keys.ToArray()));
                await Awaitable.NextFrameAsync();

                // Event log.
                if (boss.CurrentState != lastBossState)
                {
                    if (boss.CurrentState == BossController.State.Staggered) { staggers++; log.AppendLine($"{flow.FightTime,6:0.0}s  boss {(boss.IsHeld ? "HELD (finale)" : "STAGGERED")} at {boss.Health.Current} hp"); }
                    if (boss.CurrentState == BossController.State.PhaseTransition) log.AppendLine($"{flow.FightTime,6:0.0}s  PHASE TRANSITION");
                    if (boss.CurrentState == BossController.State.Dead) log.AppendLine($"{flow.FightTime,6:0.0}s  boss DEAD");
                    lastBossState = boss.CurrentState;
                }
                if (boss.IsHeld && !heldLogged) { heldLogged = true; log.AppendLine($"{flow.FightTime,6:0.0}s  FINALE: the knight holds him (at {boss.Health.Current} hp)"); }
                if (boss.Phase != lastPhase) { log.AppendLine($"{flow.FightTime,6:0.0}s  now phase {boss.Phase}"); lastPhase = boss.Phase; }
                if (hero.Health.Current != heroHp) { log.AppendLine($"{flow.FightTime,6:0.0}s  hero hit -> {hero.Health.Current}/{hero.Health.Max} (boss was {boss.CurrentState} {(boss.CurrentMove != null ? boss.CurrentMove.name : "")})"); heroHp = hero.Health.Current; }
                if (boss.Health.Current != bossHp) bossHp = boss.Health.Current;
            }

            InputSystem.QueueStateEvent(kb, new KeyboardState());
            await Awaitable.NextFrameAsync();

            var summary = new StringBuilder();
            summary.AppendLine($"Result: {flow.Current} after {flow.FightTime:0.0}s");
            summary.AppendLine($"Hero hp {hero.Health.Current}/{hero.Health.Max} · Boss hp {boss.Health.Current}/{boss.Health.Max} · phase {boss.Phase}");
            summary.AppendLine($"Parry presses {parryPresses} · successful parries {hero.ParryCount} · riposte presses {ripostes} · charged heavies {charges} · dashes {dashes} · staggers {staggers}");
            summary.Append(log);
            return summary.ToString();
        }
        finally
        {
            InputSystem.RemoveDevice(kb);
            settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            settings.backgroundBehavior = oldBackground;
        }
    }
}
