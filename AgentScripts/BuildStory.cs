using System.Text;
using DreamLayerJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The story, in one place (Shawn's arc, 2026-10-10): beaten for the first time, the knight goes looking for power;
/// power makes him king; the crown isn't enough, so he takes the Unknown One's offer and becomes its body. At the very
/// end, the knight he was holds the god still for the last strike.
/// Told in two places: a card before each fight (the relic) and the lines after each win (the first win only for the
/// Knight and the King, every time for the ending). Edit the text here and re-run:
///   unity command run_script --file AgentScripts/BuildStory.cs --entry BuildStory.All
/// </summary>
public static class BuildStory
{
    const string LobbyScene = "Assets/Scenes/Lobby.unity";
    const string FinalScene = "Assets/Scenes/BossFinal.unity";
    const string KnightScene = "Assets/Scenes/Boss01.unity";
    const string KingScene = "Assets/Scenes/Boss02.unity";

    // Shown over black as the hero enters each relic (LevelEntrance.story).
    static readonly (string fightId, string title, string story)[] Relics =
    {
        ("boss01", "The Knight's Sword",
            "The realm's finest knight. He had never lost a duel."),
        ("boss02", "The King's Crown",
            "Power made him king.\nHe froze his court so no one could take it from him."),
        ("final", "The Eye Beneath the Throne",
            "He said yes. The Unknown One wears his body now,\nbut somewhere inside, the knight remembers."),
    };

    const string EndingArt = "Assets/Art/DreamLayer/Backgrounds/ending.png";

    // Shown line by line after a win (FightFlow.epilogue), then the gold closing line.
    static readonly (string scene, string[] lines, string closing, bool firstWinOnly, MusicTrack music)[] AfterWins =
    {
        (KnightScene, new[]
        {
            "The knight fell to one knee. For the first time in his life, he had lost.",
            "He swore he would never be weak again.",
            "He went looking for power.",
        }, "A new relic wakes: the King's Crown", true, MusicTrack.Lobby),
        (KingScene, new[]
        {
            "The frozen throne cracked beneath him.",
            "A crown was not enough. He wanted more.",
            "Beneath the throne, something answered.",
        }, "A new relic wakes: the Eye Beneath the Throne", true, MusicTrack.Lobby),
        (FinalScene, new[]
        {
            "The knight held the god still, long enough for one last strike.",
            "He had chased power since the day he lost.",
            "At the very end, he chose to lose.",
        }, "Thank you for playing", false, MusicTrack.Ending),
    };

    public static string All()
    {
        var log = new StringBuilder();
        var current = EditorSceneManager.GetActiveScene();
        if (current.isDirty) EditorSceneManager.SaveScene(current);

        // Lobby relics. Single mode: two open scenes would mean two global 2D lights.
        var lobby = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);
        foreach (var root in lobby.GetRootGameObjects())
            foreach (var e in root.GetComponentsInChildren<LevelEntrance>(true))
                foreach (var r in Relics)
                {
                    if (e.fightId != r.fightId) continue;
                    e.title = r.title;
                    e.story = r.story;
                    EditorUtility.SetDirty(e);
                    log.AppendLine($"relic {e.name}: '{r.title}' + story ({r.story.Split(' ').Length} words)");
                }
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);

        // After each win.
        foreach (var w in AfterWins)
        {
            var scene = EditorSceneManager.OpenScene(w.scene, OpenSceneMode.Single);
            var flow = Object.FindAnyObjectByType<FightFlow>();
            if (flow == null) { log.AppendLine($"no FightFlow in {w.scene}"); continue; }
            flow.epilogue = (string[])w.lines.Clone();
            flow.epilogueClosing = w.closing;
            flow.epilogueFirstClearOnly = w.firstWinOnly;
            flow.epilogueMusic = w.music;
            // The ending's illustration (DreamLayer: the knight at rest in the throne room at dawn); the others play over black.
            flow.epilogueArt = w.scene == FinalScene ? AssetDatabase.LoadAssetAtPath<Sprite>(EndingArt) : null;
            EditorUtility.SetDirty(flow);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"{scene.name}: {w.lines.Length} lines after the win, closing '{w.closing}', " +
                           $"{(w.firstWinOnly ? "first win only" : "every win")}, music {w.music}");
        }
        return log.ToString();
    }
}
