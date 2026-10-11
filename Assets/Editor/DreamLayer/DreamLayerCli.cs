using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DreamLayer.UnityTools
{
    /// <summary>
    /// Runs the official DreamLayer CLI from the editor: <c>node &lt;cli.js&gt; &lt;args&gt; --json</c>.
    ///
    /// - No shell: arguments go straight to node, so prompts can contain any characters.
    /// - The API key comes from the environment (Unity's process, or on Windows the user-level
    ///   variable when Unity was started before the key was set). It is passed only to the child
    ///   process and is never logged, displayed or stored in assets or EditorPrefs.
    /// - Asynchronous: output is collected off the main thread and the callback runs on the main thread.
    /// </summary>
    public static class DreamLayerCli
    {
        public const string Package = "dreamlayer@0.4.0-beta.4"; // the beta includes sprite sheets
        public const string KeyVariable = "DREAMLAYER_API_KEY";
        const string PrefsCliPath = "DreamLayer.CliPath";        // optional override (a path, not a secret)

        public class Result
        {
            public int exitCode;
            public string stdout = "", stderr = "";
            public JsonObject json;
            public bool Ok => exitCode == 0;

            /// <summary>The CLI's documented exit codes, in words.</summary>
            public string Meaning => exitCode switch
            {
                0 => "success",
                1 => "usage or local error",
                2 => "authentication / access (check DREAMLAYER_API_KEY)",
                3 => "out of credits or quota",
                4 => "permanent API failure",
                5 => "temporary failure: recover with the execution id, don't resubmit",
                6 => "DreamLayer asked a question (name the operation explicitly)",
                -1 => "could not start node",
                _ => $"exit code {exitCode}",
            };
        }

        // ------------------------------------------------------------------ key + CLI discovery

        static string ApiKey()
        {
            string key = Environment.GetEnvironmentVariable(KeyVariable);
            if (string.IsNullOrEmpty(key) && Application.platform == RuntimePlatform.WindowsEditor)
                key = Environment.GetEnvironmentVariable(KeyVariable, EnvironmentVariableTarget.User);
            return key;
        }

        public static bool HasApiKey => !string.IsNullOrEmpty(ApiKey());

        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        static string LocalInstallDir => Path.Combine(ProjectRoot, "Library", "DreamLayerCLI");

        /// <summary>Path to the CLI's entry script, or null if it isn't installed anywhere we know.</summary>
        public static string FindCli(out string version)
        {
            version = null;
            string overridePath = EditorPrefs.GetString(PrefsCliPath, "");
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath)) { version = "override"; return overridePath; }

            string best = null, bestVersion = null;
            foreach (var dir in CandidatePackageDirs())
            {
                string pkg = Path.Combine(dir, "package.json");
                if (!File.Exists(pkg)) continue;
                try
                {
                    var json = (JsonObject)DreamLayerJson.Parse(File.ReadAllText(pkg));
                    string v = json.GetString("version", "0");
                    string bin = json.GetString("bin") ?? json.GetObject("bin")?.GetString("dreamlayer");
                    if (bin == null) continue;
                    string entry = Path.GetFullPath(Path.Combine(dir, bin));
                    if (!File.Exists(entry)) continue;
                    if (v == Package.Split('@')[1]) { version = v; return entry; } // the pinned version wins
                    if (bestVersion == null || string.CompareOrdinal(v, bestVersion) > 0) { best = entry; bestVersion = v; }
                }
                catch (Exception) { /* unreadable package.json: skip */ }
            }
            version = bestVersion;
            return best;
        }

        static IEnumerable<string> CandidatePackageDirs()
        {
            yield return Path.Combine(LocalInstallDir, "node_modules", "dreamlayer");
            string npxCache = Application.platform == RuntimePlatform.WindowsEditor
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache", "_npx")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".npm", "_npx");
            if (Directory.Exists(npxCache))
                foreach (var d in Directory.GetDirectories(npxCache))
                    yield return Path.Combine(d, "node_modules", "dreamlayer");
        }

        // ------------------------------------------------------------------ running

        /// <summary>Runs the CLI with the given arguments plus --json; the callback runs on the main thread.</summary>
        public static void Execute(IList<string> args, Action<Result> done, int timeoutSeconds = 1000)
        {
            string cli = FindCli(out _);
            if (cli == null) { done(new Result { exitCode = 1, stderr = "DreamLayer CLI not found. Use Install CLI in the DreamLayer window." }); return; }

            var psi = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add(cli);
            foreach (var a in args) psi.ArgumentList.Add(a);
            if (!args.Contains("--json")) psi.ArgumentList.Add("--json");
            string key = ApiKey();
            if (!string.IsNullOrEmpty(key)) psi.Environment[KeyVariable] = key;
            Start(psi, done, timeoutSeconds);
        }

        /// <summary>Installs the pinned CLI into Library/DreamLayerCLI (per project, not committed).</summary>
        public static void InstallLocal(Action<Result> done)
        {
            Directory.CreateDirectory(LocalInstallDir);
            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            var psi = new ProcessStartInfo(windows ? "cmd.exe" : "npm") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            if (windows) { psi.ArgumentList.Add("/c"); psi.ArgumentList.Add("npm"); }
            foreach (var a in new[] { "install", "--no-fund", "--no-audit", "--prefix", LocalInstallDir, Package }) psi.ArgumentList.Add(a);
            Start(psi, done, 600);
        }

        static void Start(ProcessStartInfo psi, Action<Result> done, int timeoutSeconds)
        {
            var output = new StringBuilder();
            var errors = new StringBuilder();
            Process process;
            try
            {
                process = new Process { StartInfo = psi };
                process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (errors) errors.AppendLine(e.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                done(new Result { exitCode = -1, stderr = $"Couldn't start {psi.FileName}: {ex.Message} (is Node.js 22.12+ installed and on PATH?)" });
                return;
            }

            var started = DateTime.UtcNow;
            void Poll()
            {
                bool timedOut = (DateTime.UtcNow - started).TotalSeconds > timeoutSeconds;
                if (!process.HasExited && !timedOut) return;
                EditorApplication.update -= Poll;
                if (timedOut && !process.HasExited) { try { process.Kill(); } catch (Exception) { } }
                process.WaitForExit(); // flushes the async readers
                var result = new Result
                {
                    exitCode = timedOut ? 5 : process.ExitCode,
                    stdout = output.ToString().Trim(),
                    stderr = errors.ToString().Trim(),
                };
                result.json = TryParseObject(result.stdout) ?? TryParseObject(result.stderr);
                process.Dispose();
                done(result);
            }
            EditorApplication.update += Poll;
        }

        static JsonObject TryParseObject(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            try { return DreamLayerJson.Parse(text.Substring(start, end - start + 1)) as JsonObject; }
            catch (Exception) { return null; }
        }
    }
}
