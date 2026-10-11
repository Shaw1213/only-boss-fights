using System.Reflection;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode helpers for checking the lobby (open Lobby.unity and enter Play mode first).
///   Explore: plays the title zoom (first call), puts the hero at x facing right, waits for the camera, and pauses for
///   capture_game_view. Args: [x, seconds walking right before pausing].
/// </summary>
public static class LobbyCheck
{
    public static async Task<string> Explore(float x, float walkSeconds)
    {
        EditorApplication.isPaused = false;
        var d = Object.FindAnyObjectByType<LobbyDirector>();
        d.hero.PlaceAt(x, 1);
        if (d.CurrentPhase == LobbyDirector.Phase.Title)
        {
            // The real flow: the click-to-play zoom from the title shot onto the hero.
            typeof(LobbyDirector).GetMethod("SetPhase", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(d, new object[] { LobbyDirector.Phase.ZoomIn });
            float zoomEnd = Time.time + d.zoomInSeconds + 0.3f;
            while (Time.time < zoomEnd) await Awaitable.NextFrameAsync();
        }
        d.hero.PlaceAt(x, 1);
        float until = Time.time + 1.2f;
        while (Time.time < until) await Awaitable.NextFrameAsync();
        if (walkSeconds > 0f)
        {
            // Walk by moving the hero directly at the lobby walk speed (no keyboard in a pipeline call).
            var velocity = typeof(LobbyHero).GetField("velocityX", BindingFlags.NonPublic | BindingFlags.Instance);
            float end = Time.time + walkSeconds;
            while (Time.time < end)
            {
                velocity.SetValue(d.hero, d.hero.config.moveSpeed * 0.8f);
                await Awaitable.NextFrameAsync();
            }
        }
        EditorApplication.isPaused = true;
        return $"phase {d.CurrentPhase}, hero x {d.hero.transform.position.x:0.##}, camera {d.cam.transform.position} size {d.cam.orthographicSize:0.##}";
    }
}
