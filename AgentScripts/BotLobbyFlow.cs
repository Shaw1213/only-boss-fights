using System.Text;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// End-to-end lobby flow through a virtual keyboard + mouse: title → click → zoom; walking past the
/// sword must not enter; stopping at it enters Boss01; winning returns to the lobby next to the sword
/// with it cleared; the crown is sealed until then; the eye says it's sealed; stopping at the crown enters the King
/// (Boss02). Entering shows the relic's story card; a key press skips it. Run in Play mode:
///   unity command run_script --timeout 150 -- --file AgentScripts/BotLobbyFlow.cs --entry BotLobbyFlow.Run --timeout_ms 140000
/// </summary>
public static class BotLobbyFlow
{
    static Keyboard kb;
    static Mouse mouse;

    public static async Task<string> Run()
    {
        if (!Application.isPlaying) return "enter play mode first";
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
            GameProgress.ResetAll();
            GameProgress.TitleShown = false;
            var lobby = await LoadAndFind<LobbyDirector>("Lobby");
            log.AppendLine($"1 start:        phase {lobby.CurrentPhase}, camera size {lobby.cam.orthographicSize:0.0}");

            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            await Seconds(0.05f);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            await Seconds(2f);
            log.AppendLine($"2 after click:  phase {lobby.CurrentPhase}, camera size {lobby.cam.orthographicSize:0.0}, hero x {lobby.hero.transform.position.x:0.0}");

            // Relic positions come from the scene (they sit on the painted pedestals).
            float swordX = lobby.entrances[0].transform.position.x;
            float crownX = lobby.entrances[1].transform.position.x;
            float eyeX = lobby.entrances[2].transform.position.x;

            // Walk straight past the sword without stopping.
            await WalkTo(lobby.hero, swordX + 4f);
            log.AppendLine($"3 walked past:  phase {lobby.CurrentPhase} (must stay Explore), hero x {lobby.hero.transform.position.x:0.0}");

            // The crown stays sealed until the Knight is beaten.
            await WalkTo(lobby.hero, crownX);
            await Seconds(1.0f);
            log.AppendLine($"3b crown first: {lobby.entrances[1].State}, phase {lobby.CurrentPhase} (must stay Explore), message '{lobby.Message}'");

            // Stop at the sword -> charge -> enter.
            await WalkTo(lobby.hero, swordX);
            await Seconds(1.0f);
            log.AppendLine($"4 stood at it:  phase {lobby.CurrentPhase}");
            log.AppendLine($"4b story card:  {await SkipStory(lobby)}");
            var boss = await WaitFind<BossController>(5f);
            log.AppendLine($"5 in fight:     {(boss != null ? SceneManager.GetActiveScene().name : "fight did not load")}");
            if (boss == null) return log.ToString();

            // Win instantly. The first win plays the story beat: a press during the first second doesn't skip it,
            // a press once it's running shows every line, the next returns to the lobby.
            var kill = ScriptableObject.CreateInstance<AttackData>();
            kill.damage = 999;
            boss.ReceiveHit(new HitInfo { attackerTeam = Team.Hero, attack = kill, sourcePosition = boss.transform.position });
            var flow = Object.FindAnyObjectByType<FightFlow>();
            await Seconds(1.0f);
            await PressJ();
            log.AppendLine($"6 result:       {flow.Current}, cleared boss01 = {GameProgress.IsCleared("boss01")}, story after the win = {flow.ShowsEpilogue}, " +
                           $"a press at 1 s skipped it = {flow.EpilogueDone} (expect False)");
            await Seconds(2.5f);
            await PressJ();
            log.AppendLine($"6b story:       every line shown after a press at 3.5 s = {flow.EpilogueDone}, closing '{flow.epilogueClosing}'");
            await Seconds(0.3f);
            await PressJ();

            lobby = await WaitFind<LobbyDirector>(5f);
            if (lobby == null) { log.AppendLine("7 back:         lobby did not load"); return log.ToString(); }
            await Seconds(0.3f);
            log.AppendLine($"7 back:         phase {lobby.CurrentPhase} (title skipped), hero x {lobby.hero.transform.position.x:0.0}; " +
                           $"sword {lobby.entrances[0].State}, crown {lobby.entrances[1].State}, eye {lobby.entrances[2].State}");

            // Eye: sealed (walking past the crown on the way must not enter it). Crown: enters the King's fight.
            crownX = lobby.entrances[1].transform.position.x;
            eyeX = lobby.entrances[2].transform.position.x;
            await WalkTo(lobby.hero, eyeX);
            await Seconds(1.0f);
            log.AppendLine($"8 eye:          phase {lobby.CurrentPhase}, message '{lobby.Message}'");
            await WalkTo(lobby.hero, crownX);
            await Seconds(1.0f);
            log.AppendLine($"9 crown:        phase {lobby.CurrentPhase}");
            log.AppendLine($"9b story card:  {await SkipStory(lobby)}");
            var king = await WaitFind<BossController>(8f);
            log.AppendLine($"10 in fight:    {SceneManager.GetActiveScene().name}, boss '{(king != null ? king.config.displayName : "none")}'");
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

    static async Task PressJ()
    {
        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.J));
        await Seconds(0.05f);
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        await Awaitable.NextFrameAsync();
    }

    /// <summary>Waits for the story card, lets it show for a moment, then skips it with a key press.</summary>
    static async Task<string> SkipStory(LobbyDirector lobby)
    {
        float end = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < end && lobby != null && lobby.CurrentPhase != LobbyDirector.Phase.Story)
            await Awaitable.NextFrameAsync();
        if (lobby == null || lobby.CurrentPhase != LobbyDirector.Phase.Story) return "no story card";
        await Seconds(0.8f);
        float shown = Time.realtimeSinceStartup;
        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.J));
        await Seconds(0.05f);
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        while (lobby != null && Time.realtimeSinceStartup - shown < 3f) await Awaitable.NextFrameAsync();
        return lobby == null ? "shown, skipped (fight loading)" : $"still up {Time.realtimeSinceStartup - shown:0.0}s after the skip";
    }

    static async Task<T> LoadAndFind<T>(string scene) where T : Object
    {
        var op = SceneManager.LoadSceneAsync(scene);
        while (!op.isDone) await Awaitable.NextFrameAsync();
        await Seconds(0.1f);
        return Object.FindAnyObjectByType<T>();
    }

    static async Task<T> WaitFind<T>(float seconds) where T : Object
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            var found = Object.FindAnyObjectByType<T>();
            if (found != null) return found;
            await Awaitable.NextFrameAsync();
        }
        return null;
    }

    static async Task WalkTo(LobbyHero hero, float x)
    {
        float end = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < end && hero != null && Mathf.Abs(hero.transform.position.x - x) > 0.15f)
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState(hero.transform.position.x < x ? Key.D : Key.A));
            await Awaitable.NextFrameAsync();
        }
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        await Awaitable.NextFrameAsync();
    }

    static async Task Seconds(float s)
    {
        float end = Time.realtimeSinceStartup + s;
        while (Time.realtimeSinceStartup < end) await Awaitable.NextFrameAsync();
    }
}
