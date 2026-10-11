using System.IO;
using System.Text;
using DreamLayerJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds (or refreshes) the greybox Lobby scene: a chamber with three relic entrances — the
/// Knight's sword, the King's crown, the Unknown One's eye (placeholder) — the lobby hero, the
/// director (title → zoom → explore → enter) and the camera. Puts Lobby first in Build Settings.
/// Safe to re-run: objects are found by name and re-wired.
///   unity command run_script --file AgentScripts/BuildLobby.cs --entry BuildLobby.All
/// </summary>
public static class BuildLobby
{
    const string ScenePath = "Assets/Scenes/Lobby.unity";
    const string SquarePath = "Assets/Art/Source/Placeholder/Square.png";
    const string CirclePath = "Assets/Art/Source/Placeholder/Circle.png";
    const float GroundY = -3f;
    // The game's name (Shawn, 2026-10-10). PlayerSettings.productName is "Only Boss Fights - The Fallen Knight":
    // no colon there, because the product name becomes a folder name on Windows.
    public const string GameTitle = "Only Boss Fights";
    public const string GameSubtitle = "The Fallen Knight";

    public static string All()
    {
        var log = new StringBuilder();
        var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        var circle = EnsureCircleSprite(log);
        var heroConfig = AssetDatabase.LoadAssetAtPath<HeroConfig>("Assets/Data/Hero/HeroConfig.asset");
        if (square == null || heroConfig == null) return "Run BuildBoss01 first (needs Square.png and HeroConfig).";

        // Open or create the scene (saving whatever is open first).
        var current = EditorSceneManager.GetActiveScene();
        if (current.isDirty) EditorSceneManager.SaveScene(current);
        var scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera + 2D light
        var camGo = FindOrCreateRoot("Main Camera");
        camGo.tag = "MainCamera";
        var cam = GetOrAdd<Camera>(camGo);
        cam.orthographic = true;
        cam.orthographicSize = 6.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.07f, 0.09f);
        camGo.transform.SetPositionAndRotation(new Vector3(0f, 0.6f, -10f), Quaternion.identity);
        GetOrAdd<UniversalAdditionalCameraData>(camGo);
        var lightGo = FindOrCreateRoot("Global Light 2D");
        var light = GetOrAdd<Light2D>(lightGo);
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;

        // Chamber
        var chamber = FindOrCreateRoot("Chamber");
        Rect(chamber.transform, "BackWall", square, new Vector2(0f, 2f), new Vector2(36f, 10f), new Color(0.15f, 0.14f, 0.17f), -10);
        Rect(chamber.transform, "Floor", square, new Vector2(0f, GroundY - 1.5f), new Vector2(36f, 3f), new Color(0.10f, 0.10f, 0.12f), -5);
        Rect(chamber.transform, "FloorEdge", square, new Vector2(0f, GroundY - 0.03f), new Vector2(36f, 0.06f), new Color(0.25f, 0.23f, 0.27f), -4);

        // Relics
        var relics = FindOrCreateRoot("Relics");
        var sword = BuildRelic(relics.transform, "Sword", -8f, "boss01", "The Knight's Sword", "Boss01", null, false,
            "Sealed", new Color(0.82f, 0.85f, 0.92f), square, circle, RelicShape.Sword);
        var crown = BuildRelic(relics.transform, "Crown", 0f, "boss02", "The King's Crown", "Boss02", new[] { "boss01" }, false,
            "Sealed - defeat the Knight first", new Color(0.95f, 0.78f, 0.30f), square, circle, RelicShape.Crown);
        var eye = BuildRelic(relics.transform, "Eye", 8f, "final", "The Eye Beneath the Throne", "BossFinal", new[] { "boss01", "boss02" }, false,
            "Sealed - defeat the Knight and the King first", new Color(0.40f, 0.78f, 0.52f), square, circle, RelicShape.Eye);

        // Hero
        var heroGo = FindOrCreateRoot("Hero");
        heroGo.transform.position = new Vector3(-12f, GroundY, 0f);
        GetOrAdd<HeroInput>(heroGo);
        var hero = GetOrAdd<LobbyHero>(heroGo);
        hero.config = heroConfig;
        hero.groundY = GroundY;
        hero.minX = -15.5f;
        hero.maxX = 15.5f;
        var body = Rect(heroGo.transform, "Body", square, Vector2.zero, heroConfig.hurtboxSize, new Color(0.30f, 0.85f, 0.95f), 10);
        body.transform.localPosition = new Vector3(0f, heroConfig.hurtboxSize.y * 0.5f, 0f);
        hero.body = body.transform;

        // Director
        var directorGo = FindOrCreateRoot("Lobby");
        var director = GetOrAdd<LobbyDirector>(directorGo);
        director.hero = hero;
        director.cam = cam;
        director.entrances = new[] { sword, crown, eye };
        director.minX = -16f;
        director.maxX = 16f;
        director.gameTitle = GameTitle;
        director.gameSubtitle = GameSubtitle;

        foreach (var o in new Object[] { hero, director, sword, crown, eye, cam, light }) EditorUtility.SetDirty(o);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureFirstInBuildSettings(ScenePath, log);
        log.AppendLine($"lobby built and saved: {ScenePath}");
        return log.ToString();
    }

    enum RelicShape { Sword, Crown, Eye }

    static LevelEntrance BuildRelic(Transform parent, string name, float x, string fightId, string title, string sceneName,
        string[] requires, bool notBuilt, string sealedMessage, Color color, Sprite square, Sprite circle, RelicShape shape)
    {
        var root = FindOrCreateChild(parent, name);
        root.transform.position = new Vector3(x, GroundY, 0f);

        // Pedestal (background, the hero walks in front of it)
        Rect(root.transform, "Pedestal", square, new Vector2(0f, 0.45f), new Vector2(1.3f, 0.9f), new Color(0.22f, 0.21f, 0.24f), -2);

        // Hovering relic at the hero's chest height, so walking into it reads as touching it.
        var visual = FindOrCreateChild(root.transform, "Visual");
        visual.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var tinted = new System.Collections.Generic.List<SpriteRenderer>();
        switch (shape)
        {
            case RelicShape.Sword: // point down, as if planted
                tinted.Add(Rect(visual.transform, "Blade", square, new Vector2(0f, -0.25f), new Vector2(0.16f, 1.3f), color, 5));
                tinted.Add(Rect(visual.transform, "Guard", square, new Vector2(0f, 0.45f), new Vector2(0.75f, 0.12f), color, 6));
                tinted.Add(Rect(visual.transform, "Grip", square, new Vector2(0f, 0.65f), new Vector2(0.12f, 0.3f), color, 5));
                tinted.Add(Rect(visual.transform, "Pommel", square, new Vector2(0f, 0.85f), new Vector2(0.22f, 0.22f), color, 6));
                break;
            case RelicShape.Crown:
                tinted.Add(Rect(visual.transform, "Band", square, new Vector2(0f, 0f), new Vector2(1.0f, 0.32f), color, 5));
                tinted.Add(Rect(visual.transform, "SpikeL", square, new Vector2(-0.38f, 0.3f), new Vector2(0.2f, 0.32f), color, 5));
                tinted.Add(Rect(visual.transform, "SpikeM", square, new Vector2(0f, 0.36f), new Vector2(0.2f, 0.42f), color, 5));
                tinted.Add(Rect(visual.transform, "SpikeR", square, new Vector2(0.38f, 0.3f), new Vector2(0.2f, 0.32f), color, 5));
                break;
            case RelicShape.Eye:
                tinted.Add(Rect(visual.transform, "Iris", circle, Vector2.zero, new Vector2(0.95f, 0.95f), color, 5));
                Rect(visual.transform, "Pupil", circle, Vector2.zero, new Vector2(0.22f, 0.62f), new Color(0.05f, 0.05f, 0.06f), 6);
                break;
        }

        var entrance = GetOrAdd<LevelEntrance>(root);
        entrance.fightId = fightId;
        entrance.title = title;
        entrance.sceneName = sceneName;
        entrance.requires = requires;
        entrance.notBuiltYet = notBuilt;
        entrance.sealedMessage = sealedMessage;
        entrance.color = color;
        entrance.visual = visual.transform;
        entrance.tintedParts = tinted.ToArray();
        entrance.touchRadius = 0.9f;
        return entrance;
    }

    // ------------------------------------------------------------------ helpers

    static Sprite EnsureCircleSprite(StringBuilder log)
    {
        if (!File.Exists(CirclePath))
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    byte a = (byte)(255f * Mathf.Clamp01(r - d + 0.5f)); // 1-pixel anti-aliased edge
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(CirclePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CirclePath);
            log.AppendLine($"created {CirclePath}");
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(CirclePath);
        if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, 64f) || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
    }

    static void EnsureFirstInBuildSettings(string path, StringBuilder log)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        log.AppendLine("build settings: " + string.Join(", ", scenes.ConvertAll(s => Path.GetFileNameWithoutExtension(s.path))));
    }

    static SpriteRenderer Rect(Transform parent, string name, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order)
    {
        var go = FindOrCreateChild(parent, name);
        go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = GetOrAdd<SpriteRenderer>(go);
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    static GameObject FindOrCreateRoot(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name) return root;
        return new GameObject(name);
    }

    static GameObject FindOrCreateChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
}
