using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DreamLayer.UnityTools;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exercises the DreamLayer window's FREE paths through its real code (reflection, no credits):
/// CLI discovery, key detection, balance refresh, and Recover (re-download a finished execution).
///   unity command run_script --timeout 120 -- --file AgentScripts/DreamLayerWindowCheck.cs --entry DreamLayerWindowCheck.Run --timeout_ms 110000 --args "[\"<execution-id>\"]"
/// </summary>
public static class DreamLayerWindowCheck
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;

    public static async Task<string> Run(string executionId)
    {
        var sb = new StringBuilder();
        var window = EditorWindow.GetWindow<DreamLayerWindow>("DreamLayer");
        var t = typeof(DreamLayerWindow);
        object Get(string f) => t.GetField(f, F).GetValue(window);
        void Set(string f, object v) => t.GetField(f, F).SetValue(window, v);

        sb.AppendLine($"key found: {DreamLayerCli.HasApiKey}; cli: {Get("cliVersion")} at {Get("cliPath")}");

        t.GetMethod("RefreshBalance", F).Invoke(window, null);
        await WaitIdle(window, t);
        sb.AppendLine($"balance: {Get("balance")}");

        Set("folder", 0); // Characters
        Set("fileName", "knight_test_recovered");
        Set("recoverId", executionId);
        string expected = (string)t.GetMethod("OutputAssetPath", F).Invoke(window, null);
        t.GetMethod("Recover", F).Invoke(window, null);
        await WaitIdle(window, t);
        sb.AppendLine($"status: {Get("status")}");
        sb.AppendLine($"file exists: {File.Exists(expected)} ({expected})");

        string sidecar = expected.Substring(0, expected.Length - 4) + ".dreamlayer.json";
        sb.AppendLine($"sidecar: {(File.Exists(sidecar) ? File.ReadAllText(sidecar).Replace("\n", " ") : "missing")}");
        if (AssetImporter.GetAtPath(expected) is TextureImporter imp)
        {
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            sb.AppendLine($"import: type={imp.textureType} mode={imp.spriteImportMode} ppu={imp.spritePixelsPerUnit} pivot={s.spritePivot} maxSize={imp.maxTextureSize} compression={imp.textureCompression} WebGL={imp.GetAutomaticFormat("WebGL")}");
        }
        return sb.ToString();
    }

    static async Task WaitIdle(EditorWindow window, System.Type t)
    {
        var busy = t.GetField("busy", F);
        float end = (float)EditorApplication.timeSinceStartup + 90f;
        await Awaitable.NextFrameAsync();
        while ((bool)busy.GetValue(window) && EditorApplication.timeSinceStartup < end) await Awaitable.NextFrameAsync();
    }
}
