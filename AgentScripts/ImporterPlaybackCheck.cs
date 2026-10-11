using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Checks the DreamLayer ZIP importer's output for walk_sample: import settings (WebGL format,
/// slicing, pivots) in edit mode, and real playback timing through an Animator in Play mode.
///   unity command run_script -- --file AgentScripts/ImporterPlaybackCheck.cs --entry ImporterPlaybackCheck.Setup
///   unity command run_script -- --file AgentScripts/ImporterPlaybackCheck.cs --entry ImporterPlaybackCheck.Measure --timeout_ms 60000
/// </summary>
public static class ImporterPlaybackCheck
{
    const string Dir = "Assets/Art/DreamLayer/Characters/walk_sample";

    public static string Setup()
    {
        if (Application.isPlaying) return "exit play mode first";
        var sb = new StringBuilder();
        string sheet = $"{Dir}/walk_sample_sheet.png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(sheet);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        imp.GetSourceTextureWidthAndHeight(out int w, out int h);
        sb.AppendLine($"sheet {w}x{h}: mode={imp.spriteImportMode} ppu={imp.spritePixelsPerUnit} maxSize={imp.maxTextureSize} compression={imp.textureCompression} WebGL format={imp.GetAutomaticFormat("WebGL")} mips={imp.mipmapEnabled}");
        sb.AppendLine($"sprites: {sprites.Length} ({string.Join(", ", sprites.Select(s => s.name))})");
        if (sprites.Length > 0)
        {
            var s0 = sprites[0];
            sb.AppendLine($"first sprite rect {s0.rect}, pivot px {s0.pivot}, normalized {(s0.pivot / s0.rect.size).ToString("0.000")}, world size {s0.bounds.size.x:0.00}x{s0.bounds.size.y:0.00} u, bottom at y={s0.bounds.min.y:0.000}");
        }
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Dir}/walk_sample.anim");
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).FirstOrDefault();
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        sb.AppendLine($"clip length {clip.length:0.000}s loop={AnimationUtility.GetAnimationClipSettings(clip).loopTime} keys: {string.Join(" ", keys.Select(k => $"{k.time:0.00}:{k.value.name}"))}");

        // Test scene: one SpriteRenderer + Animator using the generated controller.
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isDirty) EditorSceneManager.SaveScene(scene);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var go = new GameObject("walk_sample");
        go.AddComponent<SpriteRenderer>().sprite = sprites.FirstOrDefault();
        go.AddComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{Dir}/walk_sample.controller");
        sb.AppendLine("test scene ready");
        return sb.ToString();
    }

    public static async Task<string> Measure()
    {
        if (!Application.isPlaying) return "enter play mode first";
        var sr = GameObject.Find("walk_sample")?.GetComponent<SpriteRenderer>();
        if (sr == null) return "test object not found";
        var changes = new List<(float t, string name)>();
        string last = null;
        float end = Time.time + 6.5f;
        while (Time.time < end)
        {
            string name = sr.sprite != null ? sr.sprite.name : "null";
            if (name != last) { changes.Add((Time.time, name)); last = name; }
            await Awaitable.NextFrameAsync();
        }
        var sb = new StringBuilder($"sprite changes: {changes.Count}\n");
        for (int i = 1; i < changes.Count; i++)
            sb.AppendLine($"  {changes[i - 1].name} held {Mathf.RoundToInt((changes[i].t - changes[i - 1].t) * 1000f)} ms -> {changes[i].name}");
        bool looped = false;
        for (int i = 1; i < changes.Count; i++) if (changes[i - 1].name == "walk_sample_06" && changes[i].name == "walk_sample_00") looped = true;
        sb.AppendLine($"looped last -> first: {looped}; feet (sprite bottom) at y = {sr.bounds.min.y:0.000} with the object at y = {sr.transform.position.y:0.000}");
        return sb.ToString();
    }
}
