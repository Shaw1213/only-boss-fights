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
/// Play-mode checks for the parry rework, driven through a virtual mouse + keyboard:
/// clash (attack into a hit), riposte-only-when-open, right-click parry, clashing a full combo,
/// mashing, the EARLY and RED = DASH callouts, and the flash counter (H-L, incl. cancelling a swing into it and a dash
/// pressed inside the clash freeze). Waits in game seconds unless noted.
///   unity command run_script --timeout 150 -- --file AgentScripts/BotParryChecks.cs --entry BotParryChecks.Run --timeout_ms 140000
/// </summary>
public static class BotParryChecks
{
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
            var cleave = FindMove("B1_Move_OverheadCleave", boss.config.phase1Moves);
            var combo = FindMove("B1_Move_ThreeStrike", boss.config.phase1Moves);
            var slam = FindMove("B1_Move_ShockwaveSlam", boss.config.phase2Moves);

            // A) Clash: left click into the Cleave, 6 frames before it goes active.
            await WalkTo(boss.transform.position.x - 2.4f);
            int hp = hero.Health.Current, parries = hero.ParryCount, bossHp = boss.Health.Current;
            ForceMove(cleave);
            await PressBefore(6f, MouseButton.Left);
            await WaitUntil(() => boss.CurrentPhase == AttackPhase.Recovery, 1f);
            log.AppendLine($"A clash (LMB):        parried={hero.ParryCount - parries == 1}, hero hp {hp}->{hero.Health.Current}, callout '{hero.Callout}'");

            // B) Riposte: boss is now in its recovery (open) -> left click is a riposte.
            var seen = new List<string>();
            await Click(MouseButton.Left, seen);
            await Watch(0.4f, seen);
            log.AppendLine($"B riposte when open:  {string.Join(" > ", seen)}; boss hp {bossHp}->{boss.Health.Current}");
            await Park();

            // C) Right-click parry on the Cleave, 8 frames early.
            hp = hero.Health.Current; parries = hero.ParryCount;
            ForceMove(cleave);
            await PressBefore(8f, MouseButton.Right);
            await WaitUntil(() => boss.CurrentPhase == AttackPhase.Recovery, 1f);
            log.AppendLine($"C parry (RMB):        parried={hero.ParryCount - parries == 1}, hero hp {hp}->{hero.Health.Current}, callout '{hero.Callout}'");
            await Park();

            // D) Clash every hit of the Three-Strike combo; riposte must NOT fire mid-combo.
            await WalkTo(boss.transform.position.x - 2.4f);
            hp = hero.Health.Current; parries = hero.ParryCount;
            seen.Clear();
            ForceMove(combo);
            float reacted = -1f;
            float end = Time.time + 3f;
            while (Time.time < end && boss.CurrentState == BossController.State.Attacking)
            {
                var step = boss.CurrentStep;
                float stepStart = Time.time - boss.StateTime;
                if (step != null && boss.CurrentPhase == AttackPhase.Startup &&
                    (step.ActiveStart - boss.StateTime) * Frames.PerSecond <= 5f && Mathf.Abs(stepStart - reacted) > 0.001f)
                {
                    reacted = stepStart;
                    await Click(MouseButton.Left, seen);
                    continue;
                }
                await Awaitable.NextFrameAsync();
                Record(seen);
            }
            log.AppendLine($"D clash full combo:   parries {hero.ParryCount - parries}/3, boss {boss.CurrentState}, hero hp {hp}->{hero.Health.Current}; hero: {string.Join(" > ", seen)}");
            await Park();

            // E) Mash left click through a Cleave windup (does mashing auto-parry?).
            hp = hero.Health.Current; parries = hero.ParryCount;
            ForceMove(cleave);
            end = Time.time + 1.2f;
            while (Time.time < end && boss.CurrentPhase != AttackPhase.Recovery)
            {
                mouse.SetButton(MouseButton.Left, true); await Watch(0.02f, null);
                mouse.SetButton(MouseButton.Left, false); await Watch(0.03f, null);
            }
            await WaitUntil(() => boss.CurrentPhase == AttackPhase.Recovery, 1f);
            log.AppendLine($"E mash (LMB 20/s):    parried={hero.ParryCount - parries > 0}, hero hp {hp}->{hero.Health.Current}");
            await Park();

            // F) Parry far too early -> EARLY.
            ForceMove(cleave);
            await PressBefore(30f, MouseButton.Right);
            await WaitUntil(() => boss.CurrentPhase == AttackPhase.Recovery, 1f);
            log.AppendLine($"F parry 30f early:    callout '{hero.Callout}', hero hp {hero.Health.Current}");
            await Park();

            // G) Parry the red Shockwave Slam -> RED = DASH.
            await WalkTo(boss.transform.position.x - 2.0f);
            ForceMove(slam);
            await PressBefore(4f, MouseButton.Right);
            await Watch(0.3f, null);
            log.AppendLine($"G parry a red attack: callout '{hero.Callout}', hero hp {hero.Health.Current}");
            await Park();

            // H) Flash counter: clash the Cleave, then dash -> step through him and slash from behind.
            await WalkTo(boss.transform.position.x - 2.4f);
            hp = hero.Health.Current; bossHp = boss.Health.Current;
            float side0 = Mathf.Sign(hero.transform.position.x - boss.transform.position.x);
            seen.Clear();
            ForceMove(cleave);
            int clashes = hero.ParryCount;
            await PressBefore(6f, MouseButton.Left);
            await WaitUntil(() => hero.ParryCount > clashes, 0.5f);
            await Watch(0.03f, seen);
            bool ready = hero.FlashCounterReady;
            await Tap(new KeyboardState(Key.Space), seen);
            await Watch(0.35f, seen);
            float side1 = Mathf.Sign(hero.transform.position.x - boss.transform.position.x);
            log.AppendLine($"H flash counter:      ready={ready}, {string.Join(" > ", seen)}; crossed={side0 != side1}, boss hp {bossHp}->{boss.Health.Current}, hero hp {hp}->{hero.Health.Current}, callout '{hero.Callout}'");
            await Park();

            // I) Mid-combo: clash the Three-Strike's first hit, flash through; the rest of the combo must whiff.
            await WalkTo(boss.transform.position.x - 2.4f);
            hp = hero.Health.Current; bossHp = boss.Health.Current;
            seen.Clear();
            ForceMove(combo);
            int clashesI = hero.ParryCount;
            await PressBefore(5f, MouseButton.Left);
            await WaitUntil(() => hero.ParryCount > clashesI, 0.5f);
            await Watch(0.03f, seen);
            await Tap(new KeyboardState(Key.Space), seen);
            await Watch(0.3f, seen);
            string bossDuring = boss.CurrentState.ToString();
            await WaitUntil(() => boss.CurrentState != BossController.State.Attacking, 3f);
            log.AppendLine($"I mid-combo counter:  {string.Join(" > ", seen)}; boss {bossDuring} during, boss hp {bossHp}->{boss.Health.Current}, hero hp {hp}->{hero.Health.Current} (rest of the combo)");
            await Park();

            // K) Attack again right after a mid-combo clash, then dash: the dash cancels that swing into the counter.
            // (Stagger reset first: a clash that staggers him makes the press a riposte, which spends the clash's reward.)
            hero.Health.Heal(hero.Health.Max);
            await WalkTo(boss.transform.position.x - 2.4f);
            typeof(BossController).GetField("<Stagger>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boss, 0f);
            bossHp = boss.Health.Current;
            seen.Clear();
            ForceMove(combo);
            int clashesK = hero.ParryCount;
            await PressBefore(5f, MouseButton.Left);
            await WaitUntil(() => hero.ParryCount > clashesK, 0.5f);
            await Watch(0.03f, seen);
            await Click(MouseButton.Left, seen);
            await Watch(0.05f, seen);
            string stateK = hero.CurrentAttack != null ? hero.CurrentAttack.name : hero.CurrentState.ToString();
            await Tap(new KeyboardState(Key.Space), seen);
            await Watch(0.3f, seen);
            await WaitUntil(() => boss.CurrentState != BossController.State.Attacking, 3f);
            log.AppendLine($"K attack, then dash:  dashed during {stateK}: {string.Join(" > ", seen)}; boss hp {bossHp}->{boss.Health.Current}");
            await Park();

            // L) Dash the instant the clash lands, inside its freeze (real time, not game time): the press must survive the freeze.
            hero.Health.Heal(hero.Health.Max);
            await WalkTo(boss.transform.position.x - 2.4f);
            bossHp = boss.Health.Current;
            seen.Clear();
            ForceMove(cleave);
            int clashesL = hero.ParryCount;
            await PressBefore(6f, MouseButton.Left);
            await WaitUntil(() => hero.ParryCount > clashesL, 0.5f);
            await Awaitable.NextFrameAsync();
            bool frozen = Time.timeScale == 0f;
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space));
            await RealSeconds(0.03f);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            await Watch(0.35f, seen);
            log.AppendLine($"L dash in the freeze: frozen at press={frozen}, {string.Join(" > ", seen)}; boss hp {bossHp}->{boss.Health.Current}");
            await Park();

            // J) Dash AWAY after a clash stays a normal backdash (an escape, not a counter).
            await WalkTo(boss.transform.position.x - 2.4f);
            seen.Clear();
            ForceMove(cleave);
            int clashesJ = hero.ParryCount;
            await PressBefore(6f, MouseButton.Left);
            await WaitUntil(() => hero.ParryCount > clashesJ, 0.5f);
            await Watch(0.03f, seen);
            await Tap(new KeyboardState(Key.A, Key.Space), seen);
            await Watch(0.3f, seen);
            log.AppendLine($"J dash away:          {string.Join(" > ", seen)}");
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

    static void SetButton(this Mouse m, MouseButton button, bool down) =>
        InputSystem.QueueStateEvent(m, down ? new MouseState().WithButton(button) : new MouseState());

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
        await Click(button, null);
    }

    static async Task Click(MouseButton button, List<string> seen)
    {
        mouse.SetButton(button, true);
        await Watch(0.02f, seen);
        mouse.SetButton(button, false);
        await Awaitable.NextFrameAsync();
        if (seen != null) Record(seen);
    }

    static async Task Tap(KeyboardState state, List<string> seen)
    {
        InputSystem.QueueStateEvent(kb, state);
        await Watch(0.03f, seen);
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        await Awaitable.NextFrameAsync();
        if (seen != null) Record(seen);
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
        await Watch(0.05f, null);
        SetBossIdle(999f);
    }

    /// <summary>Waits for the boss's move and the hero's hurt invulnerability to finish, then parks the boss.</summary>
    static async Task Park()
    {
        await WaitUntil(() => boss.CurrentState != BossController.State.Attacking && boss.CurrentState != BossController.State.Staggered, 4f);
        SetBossIdle(999f);
        await Watch(1.1f, null);
    }

    static void SetBossIdle(float seconds) =>
        typeof(BossController).GetMethod("EnterIdle", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, new object[] { seconds });

    static void ForceMove(BossMove move)
    {
        var t = typeof(BossController);
        t.GetField("move", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boss, move);
        t.GetMethod("StartMove", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, null);
    }

    static BossMove FindMove(string name, BossMove[] moves)
    {
        foreach (var m in moves) if (m != null && m.name == name) return m;
        return null;
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

    static async Task RealSeconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) await Awaitable.NextFrameAsync();
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
        await Watch(0.2f, null);
    }

    static void Record(List<string> seen)
    {
        string s = hero.CurrentState == HeroController.State.Attack && hero.CurrentAttack != null
            ? hero.CurrentAttack.name
            : hero.CurrentState.ToString();
        if (seen.Count == 0 || seen[seen.Count - 1] != s) seen.Add(s);
    }
}
