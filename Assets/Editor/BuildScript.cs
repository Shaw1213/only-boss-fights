using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// WebGL build for itch.io (CLAUDE.md §4, §8). Editor-only.
///
/// Batch mode (Unity must not have the project open):
///   "&lt;Unity.exe&gt;" -batchmode -quit -projectPath "&lt;project&gt;" -executeMethod BuildScript.WebGL -logFile -
/// From the open Editor: Tools → Build → WebGL for itch.io (or the Pipeline `build` command, then ZipBuild).
///
/// Output: Builds/WebGL/ and Builds/itch-webgl.zip (index.html at the zip root, as itch.io expects).
/// </summary>
public static class BuildScript
{
    public const string OutputDir = "Builds/WebGL";
    public const string ZipPath = "Builds/itch-webgl.zip";

    [MenuItem("Tools/Build/WebGL for itch.io")]
    public static void WebGL()
    {
        ApplyWebGLSettings();
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        });

        bool ok = report.summary.result == BuildResult.Succeeded;
        Debug.Log($"[BuildScript] WebGL build {report.summary.result}: {report.summary.totalSize / (1024f * 1024f):0.0} MB, {report.summary.totalTime.TotalSeconds:0} s");
        if (ok) ZipBuild();
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    /// <summary>
    /// itch.io-safe WebGL settings. Gzip + decompression fallback: itch.io doesn't send the headers
    /// Brotli needs, and a Brotli build without fallback fails to load there.
    /// </summary>
    public static void ApplyWebGLSettings()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.defaultWebScreenWidth = 1280; // 16:9, matches the itch.io viewport setting
        PlayerSettings.defaultWebScreenHeight = 720;
        // Our page template (Assets/WebGLTemplates/OnlyBossFights): the game fills the itch.io frame (and the screen in
        // fullscreen), letterboxed to 16:9, with the game's own loading screen and icon.
        PlayerSettings.WebGL.template = "PROJECT:OnlyBossFights";
        AssetDatabase.SaveAssets();
    }

    /// <summary>Zips the contents of Builds/WebGL (not the folder itself) so index.html is at the zip root.</summary>
    [MenuItem("Tools/Build/Zip WebGL build for itch.io")]
    public static string ZipBuild()
    {
        if (!File.Exists(Path.Combine(OutputDir, "index.html")))
            throw new FileNotFoundException($"No WebGL build found in {OutputDir}. Build first.");
        if (File.Exists(ZipPath)) File.Delete(ZipPath);
        ZipFile.CreateFromDirectory(OutputDir, ZipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);
        string result = $"{ZipPath} ({new FileInfo(ZipPath).Length / (1024f * 1024f):0.0} MB)";
        Debug.Log($"[BuildScript] zipped {result}");
        return result;
    }
}
