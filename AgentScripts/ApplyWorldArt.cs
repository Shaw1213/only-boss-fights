using System.Text;
using DreamLayerJam;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts the DreamLayer world art into the scenes: the Boss 1 arena backdrop (calm + burning phase 2), and the
/// lobby (moonlit sanctuary, the three relics on its pedestals, the hero's real art).
/// Re-runnable; art that isn't imported yet is skipped.
///   python -I Tools/import_world_art.py
///   unity command run_script --file AgentScripts/ApplyWorldArt.cs --entry ApplyWorldArt.All     (Boss 1)
///   unity command run_script --file AgentScripts/ApplyWorldArt.cs --entry ApplyWorldArt.Lobby
/// </summary>
public static class ApplyWorldArt
{
    const string Backgrounds = "Assets/Art/DreamLayer/Backgrounds";
    const string BossScene = "Assets/Scenes/Boss01.unity";
    const string LobbyScene = "Assets/Scenes/Lobby.unity";
    const string Props = "Assets/Art/DreamLayer/Props";

    // The lobby image, fitted (Tools/import_world_art.py): the floor strip in front of the pedestals stretched 2x and
    // the top trimmed 138 rows. 24 units wide (just wider than the 23.1-unit title shot); the hero stands on that floor
    // 25% of the way in (row 1233 of 1440), in front of the pedestals (base line row 1158, tops row 874); side
    // pedestals centered at 23.5% / 76.5% of the width. The title camera sits just inside the painting's bottom and
    // the explore camera keeps the ground 18% up the screen, so neither ever sees past the painting's floor.
    const float LobbyWidthUnits = 24f;
    const float LobbyHeroScale = 1.55f;
    const float LobbyGroundFraction = 1233f / 1440f;
    const float LobbyPedestalTopFraction = 874f / 1440f;
    const float LobbyExploreGroundFraction = 0.18f;
    const float LobbyPedestalX = 0.265f * LobbyWidthUnits; // distance of the side pedestals from the center
    static readonly (string relic, string sprite, float x, float size, bool byHeight, Color light)[] Relics =
    {
        ("Sword", "relic_sword", -LobbyPedestalX, 2.0f, true, new Color(1f, 0.85f, 0.6f)),
        ("Crown", "relic_crown", 0f, 1.5f, false, new Color(1f, 0.8f, 0.35f)),
        ("Eye", "relic_eye", LobbyPedestalX, 1.45f, false, new Color(0.7f, 0.45f, 1f)),
    };

    // The fitted arena (Tools/import_world_art.py): feet stand on row 1219.5 of 1440 (a fraction, because the
    // texture imports at the 2048 cap, not at its source size), and the image is drawn 4% wider than the 16:9
    // view so camera shake never shows an edge. The camera moves up 0.6 to keep the floor band on screen.
    const float ArenaGroundFraction = 1219.5f / 1440f;
    const float ArenaWidthUnits = 17.78f * 1.04f;
    const float CameraY = 0.6f;
    // Backgrounds sit a little darker than the characters so the fighters pop (the "grayscale check");
    // the burning hall is busier, so it gets more.
    static readonly Color CalmTint = new Color(0.88f, 0.88f, 0.88f, 1f);
    static readonly Color BurningTint = new Color(0.8f, 0.8f, 0.8f, 1f);

    public static string All()
    {
        var log = new StringBuilder();
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != BossScene) scene = EditorSceneManager.OpenScene(BossScene, OpenSceneMode.Single);

        var calm = AssetDatabase.LoadAssetAtPath<Sprite>($"{Backgrounds}/arena1.png");
        var burning = AssetDatabase.LoadAssetAtPath<Sprite>($"{Backgrounds}/arena1_burning.png");
        if (calm == null) return "arena1 not imported yet";

        var arena = Object.FindAnyObjectByType<Arena>();
        var boss = Object.FindAnyObjectByType<BossController>();
        var root = GameObject.Find("Backdrop") ?? new GameObject("Backdrop");
        if (!root.TryGetComponent<ArenaBackdrop>(out var backdrop)) backdrop = root.AddComponent<ArenaBackdrop>();
        backdrop.boss = boss;
        backdrop.calm = Layer(root.transform, "Calm", calm, -100, CalmTint, arena.groundY, log);
        backdrop.phase2 = burning != null ? Layer(root.transform, "Phase2", burning, -99, BurningTint, arena.groundY, log) : null;
        EditorUtility.SetDirty(backdrop);

        // The painted floor replaces the greybox one.
        var ground = arena.transform.Find("Ground");
        if (ground != null && ground.TryGetComponent<SpriteRenderer>(out var groundRenderer))
        {
            groundRenderer.enabled = false;
            EditorUtility.SetDirty(groundRenderer);
            log.AppendLine("greybox Ground hidden");
        }

        var cam = Camera.main;
        var p = cam.transform.position;
        cam.transform.position = new Vector3(p.x, CameraY, p.z);
        EditorUtility.SetDirty(cam.transform);
        log.AppendLine($"camera y = {CameraY}");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log.ToString();
    }

    public static string Lobby()
    {
        var log = new StringBuilder();
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != LobbyScene) scene = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Backgrounds}/lobby.png");
        if (sprite == null) return "lobby not imported yet";

        var director = Object.FindAnyObjectByType<LobbyDirector>();
        var hero = Object.FindAnyObjectByType<LobbyHero>();
        float groundY = hero.groundY;

        // Backdrop: feet on the floor in front of the pedestals.
        var go = GameObject.Find("LobbyBackdrop") ?? new GameObject("LobbyBackdrop");
        if (!go.TryGetComponent<SpriteRenderer>(out var sr)) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -100;
        float scale = LobbyWidthUnits / sprite.bounds.size.x;
        float height = sprite.bounds.size.y * scale;
        float bottom = groundY - (1f - LobbyGroundFraction) * height;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.transform.position = new Vector3(0f, bottom + height * 0.5f, 5f);
        EditorUtility.SetDirty(sr);
        EditorUtility.SetDirty(go.transform);
        log.AppendLine($"LobbyBackdrop: {LobbyWidthUnits} x {height:0.##} units, bottom at y {bottom:0.##}");

        // The painting replaces the greybox room.
        foreach (var name in new[] { "Floor", "BackWall", "FloorEdge" })
        {
            var g = GameObject.Find(name);
            if (g != null && g.TryGetComponent<SpriteRenderer>(out var r)) { r.enabled = false; EditorUtility.SetDirty(r); log.AppendLine($"greybox {name} hidden"); }
        }

        // Cameras: the title shot sits just inside the painting's floor; exploring keeps the ground 18% up.
        float half = LobbyWidthUnits * 0.5f;
        director.minX = -half;
        director.maxX = half;
        director.titleCenter = new Vector2(0f, bottom + director.titleSize + 0.05f);
        director.playGroundFraction = LobbyExploreGroundFraction;
        EditorUtility.SetDirty(director);
        log.AppendLine($"title camera y {director.titleCenter.y:0.##}, explore ground at {LobbyExploreGroundFraction:P0}");

        // The hero: the fight's art, inside the painting.
        // The lobby's relaxed set (sword on his back), else the fight's art.
        hero.poses = AssetDatabase.LoadAssetAtPath<PoseSet>("Assets/Data/Hero/HeroLobbyPoses.asset");
        if (hero.poses == null || !hero.poses.IsUsable) hero.poses = AssetDatabase.LoadAssetAtPath<PoseSet>("Assets/Data/Hero/HeroPoses.asset");
        // The painting's pedestals are 2.6 units tall; at fight size the hero stood shorter than an altar. The
        // upright lobby idle at 1.55x is 3.2 units: pedestal tops at his shoulders, relics just above his head.
        hero.artScale = LobbyHeroScale;
        // A calm walk, not the fight's footwork: 3.0 units/s is ~196 steps a minute with the sheet's ~0.9-unit steps
        // (3.6 was ~235 and the arms flailed; 2.4, ~157, felt too slow to Shawn). A run at 4.8-5.4 was tried on
        // 2026-10-11 and dropped: the walk looks the most natural.
        hero.walkSpeedFactor = 0.5f;
        // He stands on the painted floor, in front of the pedestals: a soft contact shadow grounds him there.
        hero.contactShadow = true;
        hero.shadowWidth = 0.9f;
        hero.shadowHeight = 0.2f;
        hero.shadowOpacity = 0.75f;
        hero.minX = -half + 0.7f;
        hero.maxX = half - 0.7f;
        hero.transform.position = new Vector3(-half + 2f, groundY, 0f);
        EditorUtility.SetDirty(hero);
        EditorUtility.SetDirty(hero.transform);
        log.AppendLine($"LobbyHero: art from HeroPoses, bounds +-{hero.maxX:0.#}");

        // Relics on the painted pedestals, each with its own light.
        float pedestalTop = bottom + (1f - LobbyPedestalTopFraction) * height;
        foreach (var e in Object.FindObjectsByType<LevelEntrance>(FindObjectsSortMode.None))
        {
            int i = System.Array.FindIndex(Relics, r => r.relic == e.name);
            if (i < 0) continue;
            var (relic, spriteName, x, size, byHeight, lightColor) = Relics[i];
            var art = AssetDatabase.LoadAssetAtPath<Sprite>($"{Props}/{spriteName}.png");
            if (art == null) continue;
            e.transform.position = new Vector3(x, groundY, 0f);
            var pedestal = e.transform.Find("Pedestal");
            if (pedestal != null) foreach (var r in pedestal.GetComponentsInChildren<SpriteRenderer>(true)) { r.enabled = false; EditorUtility.SetDirty(r); }
            foreach (var r in e.visual.GetComponentsInChildren<SpriteRenderer>(true)) { r.enabled = false; EditorUtility.SetDirty(r); }
            e.visual.localPosition = new Vector3(0f, pedestalTop - groundY + 0.15f, 0f);
            e.visual.localScale = Vector3.one;

            var artT = e.visual.Find("Art") ?? new GameObject("Art").transform;
            artT.SetParent(e.visual, false);
            if (!artT.TryGetComponent<SpriteRenderer>(out var artR)) artR = artT.gameObject.AddComponent<SpriteRenderer>();
            artR.enabled = true;
            artR.sprite = art;
            artR.sortingOrder = 5;
            float s = size / (byHeight ? art.bounds.size.y : art.bounds.size.x);
            artT.localPosition = Vector3.zero;
            artT.localScale = new Vector3(s, s, 1f);
            e.tintedParts = new[] { artR };
            e.color = Color.white;

            var glowT = e.visual.Find("Glow") ?? new GameObject("Glow").transform;
            glowT.SetParent(e.visual, false);
            glowT.localPosition = new Vector3(0f, art.bounds.size.y * s * 0.5f, 0f);
            if (!glowT.TryGetComponent<Light2D>(out var light)) light = glowT.gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = lightColor;
            light.pointLightInnerRadius = 0.3f;
            light.pointLightOuterRadius = 3.2f;
            e.glow = light;
            e.glowIntensity = 1.4f;
            EditorUtility.SetDirty(artR);
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(e);
            EditorUtility.SetDirty(e.transform);
            log.AppendLine($"{relic}: {spriteName} at x {x:0.##}, on the pedestal top (y {pedestalTop:0.##})");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log.ToString();
    }

    /// <summary>A full-width background layer whose ground row sits on the arena's ground line.</summary>
    static SpriteRenderer Layer(Transform parent, string name, Sprite sprite, int order, Color tint, float groundY, StringBuilder log)
    {
        var t = parent.Find(name) ?? new GameObject(name).transform;
        t.SetParent(parent, false);
        if (!t.TryGetComponent<SpriteRenderer>(out var sr)) sr = t.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.color = tint;
        float scale = ArenaWidthUnits / sprite.bounds.size.x;
        float height = sprite.bounds.size.y * scale;
        // Centered pivot: the ground row is (fraction - 0.5) of the height below the center.
        float centerY = groundY + (ArenaGroundFraction - 0.5f) * height;
        t.localScale = new Vector3(scale, scale, 1f);
        t.position = new Vector3(0f, centerY, 5f);
        EditorUtility.SetDirty(sr);
        EditorUtility.SetDirty(t);
        log.AppendLine($"{name} = {sprite.name}: {ArenaWidthUnits:0.##} x {height:0.##} units at y {centerY:0.###}, order {order}");
        return sr;
    }
}
