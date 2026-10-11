using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DreamLayer.UnityTools
{
    /// <summary>
    /// Tools → DreamLayer → Generate Window. Generate / edit / cut out an image with the DreamLayer CLI
    /// and import it straight into &lt;import root&gt;/&lt;folder&gt;/ with the project's import rules
    /// (<see cref="DreamLayerImportRules"/>; the folder list comes from the rules).
    ///
    /// Every paid request asks for confirmation first, gets a unique idempotency key, and writes a
    /// provenance sidecar (&lt;name&gt;.dreamlayer.json) BEFORE submitting, so an uncertain result can be
    /// recovered with the execution id (free) instead of paying again.
    /// </summary>
    public class DreamLayerWindow : EditorWindow
    {
        enum Operation { Generate, Edit, Cutout }

        static readonly string[] Aspects = { "1:1", "16:9", "9:16", "4:3", "3:4" };
        // The rules' subfolders (Characters, Props, Backgrounds, UI by default).
        static string[] Folders
        {
            get
            {
                var names = DreamLayerImportRules.Rules.Select(r => r.folder).Where(f => !string.IsNullOrEmpty(f)).ToArray();
                return names.Length > 0 ? names : new[] { "Images" };
            }
        }

        Operation operation;
        string prompt = "";
        UnityEngine.Object reference;
        int aspect;
        int folder;
        string fileName = "new_asset";
        string recoverId = "";

        string cliPath, cliVersion, balance = "–", status = "";
        MessageType statusType = MessageType.None;
        bool busy;
        Vector2 scroll;

        [MenuItem("Tools/DreamLayer/Generate Window")]
        static void Open() => GetWindow<DreamLayerWindow>("DreamLayer");

        void OnEnable() => cliPath = DreamLayerCli.FindCli(out cliVersion);
        void OnSelectionChange() => Repaint();

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            // ---------------------------------------------------------------- status
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("API key", DreamLayerCli.HasApiKey ? "found in the environment" : $"missing: set {DreamLayerCli.KeyVariable}");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("CLI", cliPath != null ? $"{cliVersion} ({Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(cliPath)))})" : "not found");
                using (new EditorGUI.DisabledScope(busy))
                    if (GUILayout.Button(cliPath == null ? "Install CLI" : "Reinstall", GUILayout.Width(90))) InstallCli();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Balance", balance);
                using (new EditorGUI.DisabledScope(busy || cliPath == null || !DreamLayerCli.HasApiKey))
                    if (GUILayout.Button("Refresh", GUILayout.Width(90))) RefreshBalance();
            }

            // ---------------------------------------------------------------- request
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Request", EditorStyles.boldLabel);
            operation = (Operation)EditorGUILayout.EnumPopup("Operation", operation);
            if (operation != Operation.Generate)
                reference = EditorGUILayout.ObjectField("Reference image", reference, typeof(UnityEngine.Object), false);
            if (operation != Operation.Cutout)
            {
                EditorGUILayout.LabelField("Prompt");
                prompt = EditorGUILayout.TextArea(prompt, GUILayout.MinHeight(70));
            }
            if (operation == Operation.Generate)
                aspect = EditorGUILayout.Popup(new GUIContent("Aspect", "DreamLayer may still return a square image."), aspect, Aspects);
            folder = EditorGUILayout.Popup("Folder", Mathf.Clamp(folder, 0, Folders.Length - 1), Folders);
            fileName = EditorGUILayout.TextField("Name", fileName);
            EditorGUILayout.LabelField("Output", OutputAssetPath(), EditorStyles.miniLabel);

            string missing = MissingInput();
            using (new EditorGUI.DisabledScope(busy || missing != null))
                if (GUILayout.Button(busy ? "Working…" : $"{operation} (1 credit)", GUILayout.Height(28))) Submit();
            if (missing != null && !busy) EditorGUILayout.HelpBox(missing, MessageType.Info);

            // ---------------------------------------------------------------- recover
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Recover (free)", EditorStyles.boldLabel);
            recoverId = EditorGUILayout.TextField(new GUIContent("Execution id", "Downloads a finished result without paying again."), recoverId);
            using (new EditorGUI.DisabledScope(busy || string.IsNullOrWhiteSpace(recoverId) || cliPath == null))
                if (GUILayout.Button("Download result into the output path")) Recover();

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);

            // ---------------------------------------------------------------- provenance of selection
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected asset provenance", EditorStyles.boldLabel);
            string sidecar = SidecarFor(AssetDatabase.GetAssetPath(Selection.activeObject));
            if (sidecar != null && File.Exists(sidecar))
                EditorGUILayout.TextArea(File.ReadAllText(sidecar), EditorStyles.wordWrappedMiniLabel);
            else
                EditorGUILayout.LabelField("Select a DreamLayer image to see how it was made.", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ actions

        void InstallCli()
        {
            busy = true;
            SetStatus($"Installing {DreamLayerCli.Package} into Library/DreamLayerCLI…", MessageType.Info);
            DreamLayerCli.InstallLocal(r =>
            {
                busy = false;
                cliPath = DreamLayerCli.FindCli(out cliVersion);
                SetStatus(cliPath != null ? $"CLI ready: {cliVersion}" : $"Install failed: {Trim(r.stderr)}", cliPath != null ? MessageType.Info : MessageType.Error);
            });
        }

        void RefreshBalance()
        {
            busy = true;
            DreamLayerCli.Execute(new[] { "balance" }, r =>
            {
                busy = false;
                balance = r.Ok && r.json != null ? $"{r.json.GetNumber("available"):0.#} credits available" : $"error: {r.Meaning}";
                Repaint();
            });
        }

        void Submit()
        {
            string assetPath = OutputAssetPath();
            string fullPath = Path.GetFullPath(assetPath);
            string referencePath = reference != null ? Path.GetFullPath(AssetDatabase.GetAssetPath(reference)) : null;
            string op = operation.ToString().ToLowerInvariant();
            string key = $"dl-{op}-{Path.GetFileNameWithoutExtension(assetPath)}-{DateTime.UtcNow:yyyyMMddHHmmss}";

            if (!EditorUtility.DisplayDialog("Spend a DreamLayer credit?",
                    $"{operation} → {assetPath}\n\nThis spends 1 credit (charged only if a result is delivered).", "Spend 1 credit", "Cancel"))
                return;

            var args = new List<string>();
            switch (operation)
            {
                case Operation.Generate: args.AddRange(new[] { "generate", prompt, "--aspect", Aspects[aspect] }); break;
                case Operation.Edit: args.AddRange(new[] { "edit", referencePath, prompt }); break;
                case Operation.Cutout: args.AddRange(new[] { "cutout", referencePath }); break;
            }
            args.AddRange(new[] { "--idempotency-key", key, "--out", fullPath, "--quiet" });

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var record = new Dictionary<string, object>
            {
                ["status"] = "pending",
                ["operation"] = operation == Operation.Generate ? "text_to_image" : operation == Operation.Edit ? "image_to_image" : "background_remove",
                ["prompt"] = operation == Operation.Cutout ? null : prompt,
                ["reference_image"] = reference != null ? AssetDatabase.GetAssetPath(reference) : null,
                ["requested_aspect_ratio"] = operation == Operation.Generate ? Aspects[aspect] : null,
                ["idempotency_key"] = key,
                ["submitted_utc"] = DateTime.UtcNow.ToString("o"),
                ["credits"] = 1,
                ["client"] = DreamLayerCli.Package,
            };
            WriteSidecar(assetPath, record); // written first: an interrupted run can still be recovered

            busy = true;
            SetStatus($"Working… (idempotency key {key})", MessageType.Info);
            var started = DateTime.UtcNow;
            DreamLayerCli.Execute(args, r => Finish(r, assetPath, record, started));
        }

        void Recover()
        {
            string assetPath = OutputAssetPath();
            var record = new Dictionary<string, object> { ["status"] = "recovering", ["execution_id"] = recoverId.Trim(), ["recovered_utc"] = DateTime.UtcNow.ToString("o"), ["credits"] = 0 };
            busy = true;
            SetStatus("Downloading the finished result (free)…", MessageType.Info);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(assetPath)));
            DreamLayerCli.Execute(new[] { "download", recoverId.Trim(), "--out", Path.GetFullPath(assetPath) }, r => Finish(r, assetPath, record, DateTime.UtcNow));
        }

        void Finish(DreamLayerCli.Result r, string assetPath, Dictionary<string, object> record, DateTime started)
        {
            busy = false;
            if (r.json != null)
            {
                foreach (var field in new[] { "execution_id", "conversation_id", "status" })
                    if (r.json.GetString(field) is string v) record[field == "status" ? "dreamlayer_status" : field] = v;
                if (r.json.GetString("execution_id") is string id) recoverId = id;
            }
            record["exit_code"] = r.exitCode;
            record["seconds_to_result"] = Math.Round((DateTime.UtcNow - started).TotalSeconds, 1);

            if (r.Ok && File.Exists(Path.GetFullPath(assetPath)))
            {
                record["status"] = "completed";
                AssetDatabase.ImportAsset(assetPath); // DreamLayerTexturePostprocessor applies the folder's rules
                if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
                {
                    importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                    record["output_size"] = new[] { w, h };
                }
                WriteSidecar(assetPath, record);
                AssetDatabase.ImportAsset(SidecarFor(assetPath));
                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                SetStatus($"Done: {assetPath}", MessageType.Info);
            }
            else
            {
                record["status"] = "failed_or_uncertain";
                record["error"] = Trim(r.stderr.Length > 0 ? r.stderr : r.stdout);
                WriteSidecar(assetPath, record);
                SetStatus($"{r.Meaning}. {Trim(r.stderr)}" + (string.IsNullOrEmpty(recoverId) ? "" : $"\nIf a result exists, use Recover with {recoverId}."), MessageType.Warning);
            }
            Repaint();
        }

        // ------------------------------------------------------------------ helpers

        string MissingInput()
        {
            if (!DreamLayerCli.HasApiKey) return $"Set the {DreamLayerCli.KeyVariable} environment variable.";
            if (cliPath == null) return "Install the DreamLayer CLI first.";
            if (operation != Operation.Generate && reference == null) return "Pick a reference image from the project.";
            if (operation != Operation.Cutout && string.IsNullOrWhiteSpace(prompt)) return "Write a prompt.";
            if (string.IsNullOrWhiteSpace(fileName)) return "Name the output.";
            return null;
        }

        string OutputAssetPath()
        {
            string safe = new string((fileName ?? "").Trim().Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
            if (safe.Length == 0) safe = "new_asset";
            var folders = Folders;
            return AssetDatabase.GenerateUniqueAssetPath($"{DreamLayerImportRules.Root}{folders[Mathf.Clamp(folder, 0, folders.Length - 1)]}/{safe}.png");
        }

        static string SidecarFor(string assetPath) =>
            string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? null : assetPath.Substring(0, assetPath.Length - 4) + ".dreamlayer.json";

        static void WriteSidecar(string assetPath, Dictionary<string, object> record) =>
            File.WriteAllText(Path.GetFullPath(SidecarFor(assetPath)), ToJson(record));

        static string ToJson(Dictionary<string, object> record)
        {
            string Value(object v) => v switch
            {
                null => "null",
                string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"",
                bool b => b ? "true" : "false",
                int[] arr => "[" + string.Join(", ", arr) + "]",
                IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => "\"" + v + "\"",
            };
            var sb = new StringBuilder("{\n");
            int i = 0;
            foreach (var kv in record)
                sb.Append($"  \"{kv.Key}\": {Value(kv.Value)}{(++i < record.Count ? "," : "")}\n");
            return sb.Append("}\n").ToString();
        }

        static string Trim(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s.Substring(0, 300) + "…" : s);

        void SetStatus(string text, MessageType type)
        {
            status = text;
            statusType = type;
            Repaint();
        }
    }
}
