using System.Threading.Tasks;
using DreamLayerJam;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the lobby in a state for a screenshot and pauses the Editor. Play mode only.
///   Title:   unity command run_script -- --file AgentScripts/LobbyShots.cs --entry LobbyShots.Title
///   Explore: unity command run_script -- --file AgentScripts/LobbyShots.cs --entry LobbyShots.Explore
/// Unpause with EditorApplication.isPaused = false.
/// </summary>
public static class LobbyShots
{
    public static async Task<string> Title()
    {
        EditorApplication.isPaused = false;
        GameProgress.ResetAll();
        GameProgress.TitleShown = false;
        var op = SceneManager.LoadSceneAsync("Lobby");
        while (!op.isDone) await Awaitable.NextFrameAsync();
        await Seconds(0.5f);
        EditorApplication.isPaused = true;
        return "paused on the title";
    }

    public static async Task<string> Explore()
    {
        EditorApplication.isPaused = false;
        GameProgress.TitleShown = true;
        GameProgress.LastEntranceId = "boss02"; // stand by the crown, sword + eye in view
        var op = SceneManager.LoadSceneAsync("Lobby");
        while (!op.isDone) await Awaitable.NextFrameAsync();
        await Seconds(1.0f); // fade-in finishes
        EditorApplication.isPaused = true;
        return "paused in the lobby";
    }

    static async Task Seconds(float s)
    {
        float end = Time.realtimeSinceStartup + s;
        while (Time.realtimeSinceStartup < end) await Awaitable.NextFrameAsync();
    }
}
