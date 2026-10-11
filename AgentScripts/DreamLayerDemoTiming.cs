using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Validation helpers for DreamLayer's unity-sprite-import demo (docs/unity-editor-test-plan.md in
/// their repo). Setup (edit mode): new empty scene + their menu item. Measure (Play mode): records
/// each sprite change on the demo's SpriteRenderer with game time, to check per-frame durations and looping.
///   unity command run_script -- --file AgentScripts/DreamLayerDemoTiming.cs --entry DreamLayerDemoTiming.Setup
///   unity command run_script -- --file AgentScripts/DreamLayerDemoTiming.cs --entry DreamLayerDemoTiming.Measure --timeout_ms 60000
/// </summary>
public static class DreamLayerDemoTiming
{
    public static string Setup()
    {
        if (Application.isPlaying) return "exit play mode first";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isDirty) EditorSceneManager.SaveScene(scene);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        bool ran = EditorApplication.ExecuteMenuItem("Tools/DreamLayer/Create sprite demo");
        var demo = GameObject.Find("DreamLayer sprite demo");
        var cam = Camera.main;
        return $"menu item ran: {ran}; demo object: {(demo != null)}; components: " +
               (demo != null ? string.Join(", ", System.Array.ConvertAll(demo.GetComponents<Component>(), c => c.GetType().Name)) : "-") +
               $"; camera: {(cam != null ? cam.name + " ortho " + cam.orthographicSize : "none")}";
    }

    public static async Task<string> Measure()
    {
        if (!Application.isPlaying) return "enter play mode first";
        var demo = GameObject.Find("DreamLayer sprite demo");
        if (demo == null) return "demo object not found";
        var sr = demo.GetComponent<SpriteRenderer>();
        var changes = new List<(float t, string name)>();
        string last = null;
        float end = Time.time + 6.5f; // a little over two 2.83 s cycles
        while (Time.time < end)
        {
            string name = sr.sprite != null ? sr.sprite.name : "null";
            if (name != last) { changes.Add((Time.time, name)); last = name; }
            await Awaitable.NextFrameAsync();
        }

        var sb = new StringBuilder();
        sb.AppendLine($"sprite changes observed: {changes.Count}");
        for (int i = 1; i < changes.Count; i++)
            sb.AppendLine($"  {changes[i - 1].name} held {Mathf.RoundToInt((changes[i].t - changes[i - 1].t) * 1000f)} ms -> {changes[i].name}");
        bool looped = false;
        for (int i = 1; i < changes.Count; i++)
            if (changes[i - 1].name == "walk_07" && changes[i].name == "walk_01") looped = true;
        sb.AppendLine($"looped walk_07 -> walk_01: {looped}");
        var b = sr.sprite != null ? sr.sprite.bounds : default;
        sb.AppendLine($"sprite world bounds: center {b.center}, size {b.size} (pivot center => feet at y = {b.min.y:0.00}, not at the GameObject's origin)");
        return sb.ToString();
    }
}
