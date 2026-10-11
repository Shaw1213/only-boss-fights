using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamLayer.UnityTools
{
    /// <summary>
    /// Import defaults for DreamLayer images, by subfolder of <see cref="Root"/>. They're applied on an image's FIRST
    /// import only, so manual Inspector changes stick.
    ///
    /// The built-in rules below can be overridden per project with <see cref="SettingsPath"/> (a JSON file; Tools →
    /// DreamLayer → Create Import Rules File writes the defaults there to start from). Installed as a package, these
    /// scripts are read-only, so that file is the place to change the root folder, the subfolders, sizes and pivots.
    /// </summary>
    public static class DreamLayerImportRules
    {
        /// <summary>Optional per-project override of the root folder and the rules.</summary>
        public const string SettingsPath = "ProjectSettings/DreamLayerImportRules.json";

        [Serializable]
        public struct Rule
        {
            public string folder;          // subfolder under Root ("" = the fallback rule)
            public int maxSize;            // Unity max texture size
            public float pixelsPerUnit;    // world scale
            public Vector2 pivot;          // normalized; (0.5, 0) = bottom-center (feet)
            public string note;
        }

        [Serializable]
        class Settings
        {
            public string root;
            public Rule[] rules;
            public Rule fallback;
        }

        const string DefaultRoot = "Assets/Art/DreamLayer/";

        // Characters stand on the ground (bottom-center pivot); backgrounds and UI are centered.
        // Max sizes follow a WebGL budget: characters 1024, backgrounds 2048, UI 1024.
        static readonly Rule[] DefaultRules =
        {
            new Rule { folder = "Characters",  maxSize = 1024, pixelsPerUnit = 256, pivot = new Vector2(0.5f, 0f),   note = "feet pivot" },
            new Rule { folder = "Props",       maxSize = 1024, pixelsPerUnit = 256, pivot = new Vector2(0.5f, 0f),   note = "feet pivot" },
            new Rule { folder = "Backgrounds", maxSize = 2048, pixelsPerUnit = 128, pivot = new Vector2(0.5f, 0.5f), note = "centered" },
            new Rule { folder = "UI",          maxSize = 1024, pixelsPerUnit = 100, pivot = new Vector2(0.5f, 0.5f), note = "centered" },
        };

        static readonly Rule DefaultFallback = new Rule { folder = "", maxSize = 1024, pixelsPerUnit = 256, pivot = new Vector2(0.5f, 0.5f), note = "default" };

        static Settings current;
        static DateTime currentStamp;

        /// <summary>DreamLayer images and sprite-sheet ZIPs dropped under this folder are handled automatically.</summary>
        public static string Root => Current.root;

        public static Rule[] Rules => Current.rules;

        /// <summary>The rule for images directly under <see cref="Root"/> or in a subfolder no rule names.</summary>
        public static Rule Default => Current.fallback;

        /// <summary>True when <see cref="SettingsPath"/> exists and is in use.</summary>
        public static bool UsingProjectFile => File.Exists(SettingsPath);

        public static bool IsManaged(string assetPath) => assetPath.Replace('\\', '/').StartsWith(Root);

        public static Rule For(string assetPath)
        {
            string rel = assetPath.Replace('\\', '/').Substring(Root.Length);
            foreach (var rule in Rules)
                if (rel.StartsWith(rule.folder + "/")) return rule;
            return Default;
        }

        // Reloaded whenever the file changes, so an edit applies to the next import without a recompile.
        static Settings Current
        {
            get
            {
                var stamp = File.Exists(SettingsPath) ? File.GetLastWriteTimeUtc(SettingsPath) : DateTime.MinValue;
                if (current == null || stamp != currentStamp)
                {
                    currentStamp = stamp;
                    current = Load();
                }
                return current;
            }
        }

        static Settings Defaults() => new Settings { root = DefaultRoot, rules = (Rule[])DefaultRules.Clone(), fallback = DefaultFallback };

        static Settings Load()
        {
            var settings = Defaults();
            if (!File.Exists(SettingsPath)) return settings;
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(SettingsPath), settings); // keys left out keep their defaults
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DreamLayer] Couldn't read {SettingsPath} ({e.Message}); using the built-in import rules.");
                return Defaults();
            }
            settings.root = string.IsNullOrWhiteSpace(settings.root) ? DefaultRoot : settings.root.Replace('\\', '/').TrimEnd('/') + "/";
            if (settings.rules == null) settings.rules = (Rule[])DefaultRules.Clone();
            return settings;
        }

        [MenuItem("Tools/DreamLayer/Create Import Rules File")]
        static void CreateSettingsFile()
        {
            if (File.Exists(SettingsPath))
            {
                EditorUtility.DisplayDialog("DreamLayer import rules", $"{SettingsPath} already exists. Edit it in a text editor; changes apply to the next import.", "OK");
                return;
            }
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(Defaults(), true));
            EditorUtility.DisplayDialog("DreamLayer import rules", $"Wrote the built-in rules to {SettingsPath}. Edit the root folder, subfolders, " +
                                        "max sizes, pixels per unit and pivots there; changes apply to the next import.", "OK");
        }
    }
}
