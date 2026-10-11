using System.IO;
using System.Text;
using DreamLayerJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds (or refreshes) the Boss 1 greybox: placeholder sprite, data assets with the numbers from
/// docs/design/boss-01.md, and the scene objects, all wired together.
///
/// Safe to re-run: existing data assets are never overwritten (tuned values survive); scene objects
/// are found by name and re-wired rather than duplicated.
///
///   unity command run_script --file AgentScripts/BuildBoss01.cs --entry BuildBoss01.All
/// </summary>
public static class BuildBoss01
{
    const string ScenePath = "Assets/Scenes/Boss01.unity";
    const string TemplateScenePath = "Assets/Scenes/SampleScene.unity";
    const string SpritePath = "Assets/Art/Source/Placeholder/Square.png";
    const string Data = "Assets/Data";

    public static string All()
    {
        var log = new StringBuilder();
        EnsureFolder("Assets/Art/Source/Placeholder");
        EnsureFolder($"{Data}/Feel");
        EnsureFolder($"{Data}/Hero/Attacks");
        EnsureFolder($"{Data}/Boss01/Attacks");
        EnsureFolder($"{Data}/Boss01/Moves");

        var square = EnsureSquareSprite(log);
        var feedback = LoadOrCreate<FeedbackConfig>($"{Data}/Feel/FeedbackConfig.asset", _ => { }, log);
        var heroConfig = BuildHeroData(log);
        var bossConfig = BuildBossData(log);
        BuildScene(square, feedback, heroConfig, bossConfig, log);

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>Only the hero's data assets (new attacks get created and wired; existing ones are kept). Leaves scenes alone.</summary>
    public static string HeroData()
    {
        var log = new StringBuilder();
        BuildHeroData(log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ------------------------------------------------------------------ data

    static HeroConfig BuildHeroData(StringBuilder log)
    {
        string d = $"{Data}/Hero/Attacks";
        //                                              start act rec dmg stagger  hitbox offset   hitbox size     lunge stop shake
        var light1  = Attack($"{d}/Hero_Light1.asset",       5, 3, 12, 1,  2f, V(0.9f, 1.0f), V(1.4f, 0.9f), 0f,   3, 0.08f, log);
        var light2  = Attack($"{d}/Hero_Light2.asset",       5, 3, 12, 1,  2f, V(1.0f, 1.1f), V(1.5f, 0.9f), 0.15f, 3, 0.08f, log);
        var light3  = Attack($"{d}/Hero_Light3.asset",       6, 3, 18, 2,  4f, V(1.1f, 1.0f), V(1.7f, 1.1f), 0.3f, 4, 0.12f, log);
        var heavy   = Attack($"{d}/Hero_ChargedHeavy.asset", 4, 4, 20, 5, 15f, V(1.2f, 1.0f), V(2.2f, 1.4f), 0.4f, 7, 0.30f, log);
        var riposte = Attack($"{d}/Hero_Riposte.asset",      3, 4, 14, 6, 20f, V(1.1f, 1.1f), V(2.0f, 1.4f), 0.6f, 8, 0.30f, log);
        var dashAtk = Attack($"{d}/Hero_DashAttack.asset",   4, 6, 14, 1,  2f, V(0.9f, 1.0f), V(1.4f, 0.8f), 1.5f, 3, 0.10f, log);
        // The clash's reward: startup = the flash step through the boss (no lunge; HeroController moves the hero).
        var flash   = Attack($"{d}/Hero_FlashCounter.asset", 4, 4, 14, 5, 30f, V(1.0f, 1.1f), V(1.9f, 1.4f), 0f,   9, 0.35f, log, a => a.breaksCombo = true);
        // Harvest: the infused charged heavy fires this crescent instead of its melee hit.
        var wave    = Attack($"{d}/Hero_ElementWave.asset",    0, 1,  0, 5, 15f, V(0f, 0f),     V(0f, 0f),     0f,   7, 0.30f, log, a =>
        {
            a.projectileSpeed = 14f;
            a.projectileSize = V(0.8f, 1.6f);
            a.projectileLifetime = 1.2f;
        });
        foreach (var a in new[] { light1, light2, light3, heavy, riposte, dashAtk, wave, flash }) { a.cue = TelegraphCue.None; EditorUtility.SetDirty(a); }

        var config = LoadOrCreate<HeroConfig>($"{Data}/Hero/HeroConfig.asset", h =>
        {
            h.lightCombo = new[] { light1, light2, light3 };
            h.chargedHeavy = heavy;
            h.riposte = riposte;
            h.flashCounter = flash;
            h.dashAttack = dashAtk;
            h.elementWave = wave;
        }, log);
        if (config.elementWave == null)
        {
            config.elementWave = wave;
            EditorUtility.SetDirty(config);
            log.AppendLine("wired   HeroConfig.elementWave");
        }
        if (config.flashCounter == null)
        {
            config.flashCounter = flash;
            EditorUtility.SetDirty(config);
            log.AppendLine("wired   HeroConfig.flashCounter");
        }
        return config;
    }

    static BossConfig BuildBossData(StringBuilder log)
    {
        string d = $"{Data}/Boss01/Attacks";
        string m = $"{Data}/Boss01/Moves";

        // Boss windups from the design doc: 700 ms = 42 f, 450 ms = 27 f, 400 ms = 24 f.
        // Combo rhythm: hit-to-hit = active + recovery + next startup (3 + 3 + 9 = 15 f = 250 ms).
        //                                                    start act rec dmg     hitbox offset   hitbox size    lunge stop shake
        var cleave  = Attack($"{d}/B1_OverheadCleave.asset",    42, 4, 54, 1, 0f, V(1.5f, 1.6f), V(2.6f, 3.0f), 0f,   6, 0.35f, log);
        var thrust  = Attack($"{d}/B1_LungeThrust.asset",       27, 8, 30, 1, 0f, V(1.6f, 1.6f), V(2.2f, 0.8f), 5f,   5, 0.30f, log);
        var combo1  = Attack($"{d}/B1_Combo1.asset",            24, 3,  3, 1, 0f, V(1.5f, 1.6f), V(2.2f, 1.6f), 0.4f, 4, 0.25f, log);
        var combo2  = Attack($"{d}/B1_Combo2.asset",             9, 3,  3, 1, 0f, V(1.5f, 1.6f), V(2.2f, 1.6f), 0.4f, 4, 0.25f, log);
        var combo3  = Attack($"{d}/B1_Combo3_Heavy.asset",      15, 4, 42, 1, 0f, V(1.6f, 1.6f), V(2.4f, 1.8f), 0.5f, 6, 0.35f, log);
        // Phase 2: hit 3 chains into a delayed 4th (the first feint; only after the rhythm is learned).
        var combo3c = Attack($"{d}/B1_Combo3_Chain.asset",      15, 4,  3, 1, 0f, V(1.6f, 1.6f), V(2.4f, 1.8f), 0.5f, 6, 0.35f, log);
        var combo4  = Attack($"{d}/B1_Combo4_Delayed.asset",    33, 4, 42, 1, 0f, V(1.6f, 1.6f), V(2.4f, 1.8f), 0.5f, 6, 0.35f, log);
        var slam    = Attack($"{d}/B1_ShockwaveSlam.asset",     36, 6, 40, 1, 0f, V(1.3f, 0.5f), V(1.6f, 1.0f), 0f,   6, 0.40f, log, a =>
        {
            a.parryable = false;
            a.cue = TelegraphCue.RedGlow;
            a.spawnsProjectile = true;
            a.projectileSpeed = 9f;
            a.projectileSize = V(0.9f, 0.7f);
            a.projectileLifetime = 2.5f;
        });
        // Phase 2: his blade ignites. A parryable fire wave at chest height (the glint + orange says "parry and harvest";
        // the red ground wave above says "dash"). The swing itself also burns up close.
        var flame   = Attack($"{d}/B1_FlameWave.asset",         30, 4, 36, 1, 0f, V(1.4f, 1.6f), V(2.0f, 2.0f), 0f,   5, 0.30f, log, a =>
        {
            a.element = Element.Fire;
            a.spawnsProjectile = true;
            a.projectileSpeed = 7.5f;
            a.projectileSize = V(0.9f, 0.9f);
            a.projectileHeight = 0.6f;
            a.projectileLifetime = 3f;
        });

        var mCleave = Move($"{m}/B1_Move_OverheadCleave.asset",  new[] { cleave },                         2.6f, 0.9f, 1.0f, false, log);
        var mThrust = Move($"{m}/B1_Move_LungeThrust.asset",     new[] { thrust },                         6.5f, 2.0f, 0.8f, false, log);
        var mCombo3 = Move($"{m}/B1_Move_ThreeStrike.asset",     new[] { combo1, combo2, combo3 },         2.4f, 0.8f, 1.0f, true,  log);
        var mCombo4 = Move($"{m}/B1_Move_FourStrikeFeint.asset", new[] { combo1, combo2, combo3c, combo4 }, 2.4f, 0.8f, 1.0f, true,  log);
        var mSlam   = Move($"{m}/B1_Move_ShockwaveSlam.asset",   new[] { slam },                           6.0f, 4.0f, 0.9f, false, log);
        var mFlame  = Move($"{m}/B1_Move_FlameWave.asset",       new[] { flame },                          6.0f, 2.5f, 1.2f, false, log);

        var config = LoadOrCreate<BossConfig>($"{Data}/Boss01/Boss01Config.asset", b =>
        {
            b.displayName = "The Fallen Knight";
            b.maxHealth = 100;
            b.phase1Moves = new[] { mCleave, mThrust, mCombo3 };
            b.phase2Moves = new[] { mCleave, mThrust, mCombo4, mSlam, mFlame };
        }, log);
        if (System.Array.IndexOf(config.phase2Moves, mFlame) < 0)
        {
            var moves = new System.Collections.Generic.List<BossMove>(config.phase2Moves) { mFlame };
            config.phase2Moves = moves.ToArray();
            EditorUtility.SetDirty(config);
            log.AppendLine("added   B1_Move_FlameWave to phase 2");
        }
        return config;
    }

    static AttackData Attack(string path, int startup, int active, int recovery, int damage, float stagger,
        Vector2 offset, Vector2 size, float lunge, int hitStop, float trauma, StringBuilder log, System.Action<AttackData> extra = null)
    {
        return LoadOrCreate<AttackData>(path, a =>
        {
            a.startupFrames = startup;
            a.activeFrames = active;
            a.recoveryFrames = recovery;
            a.damage = damage;
            a.staggerDamage = stagger;
            a.hitboxOffset = offset;
            a.hitboxSize = size;
            a.lungeDistance = lunge;
            a.hitStopFrames = hitStop;
            a.shakeTrauma = trauma;
            extra?.Invoke(a);
        }, log);
    }

    static BossMove Move(string path, AttackData[] steps, float range, float tolerance, float weight, bool fullParryStaggers, StringBuilder log)
    {
        return LoadOrCreate<BossMove>(path, mv =>
        {
            mv.steps = steps;
            mv.preferredRange = range;
            mv.rangeTolerance = tolerance;
            mv.weight = weight;
            mv.fullParryStaggers = fullParryStaggers;
        }, log);
    }

    // ------------------------------------------------------------------ scene

    static void BuildScene(Sprite square, FeedbackConfig feedback, HeroConfig heroConfig, BossConfig bossConfig, StringBuilder log)
    {
        // Rename the template scene once (keeps its GUID, camera, 2D light and volume).
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateScenePath) != null)
        {
            var current = EditorSceneManager.GetActiveScene();
            if (current.isDirty) EditorSceneManager.SaveScene(current);
            string error = AssetDatabase.MoveAsset(TemplateScenePath, ScenePath);
            log.AppendLine(string.IsNullOrEmpty(error) ? $"renamed {TemplateScenePath} -> {ScenePath}" : $"rename failed: {error}");
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureInBuildSettings(ScenePath, log);

        // Arena
        var arenaGo = FindOrCreateRoot("Arena");
        var arena = GetOrAdd<Arena>(arenaGo);
        arena.minX = -8f;
        arena.maxX = 8f;
        arena.groundY = -3f;

        // Camera
        var cam = Camera.main;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        GetOrAdd<CameraShake>(cam.gameObject);
        var fit = GetOrAdd<CameraFit>(cam.gameObject);
        fit.arena = arena;
        EditorUtility.SetDirty(fit);
        var ground = FindOrCreateChild(arenaGo.transform, "Ground");
        ground.transform.position = new Vector3(0f, -4.5f, 0f);
        ground.transform.localScale = new Vector3(24f, 3f, 1f);
        SetSprite(ground, square, new Color(0.17f, 0.17f, 0.2f), 0);

        // Hero
        var heroGo = FindOrCreateRoot("Hero");
        heroGo.transform.position = new Vector3(-5f, arena.groundY, 0f);
        GetOrAdd<Health>(heroGo);
        GetOrAdd<HeroInput>(heroGo);
        var hero = GetOrAdd<HeroController>(heroGo);
        var heroBody = FindOrCreateChild(heroGo.transform, "Body");
        heroBody.transform.localPosition = new Vector3(0f, heroConfig.hurtboxSize.y * 0.5f, 0f);
        heroBody.transform.localScale = new Vector3(heroConfig.hurtboxSize.x, heroConfig.hurtboxSize.y, 1f);
        var heroSprite = SetSprite(heroBody, square, hero.baseColor, 3);
        var heroFlash = GetOrAdd<SpriteFlash>(heroBody);
        heroFlash.target = heroSprite;
        var heroHitbox = SetSprite(FindOrCreateChild(heroGo.transform, "HitboxDebug"), square, new Color(0.4f, 1f, 1f, 0.35f), 6);
        heroHitbox.enabled = false;
        var elementOrb = SetSprite(FindOrCreateChild(heroGo.transform, "ElementOrb"), square, Color.white, 5);
        elementOrb.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        elementOrb.enabled = false;

        // Boss
        var bossGo = FindOrCreateRoot("Boss");
        bossGo.transform.position = new Vector3(4f, arena.groundY, 0f);
        GetOrAdd<Health>(bossGo);
        var boss = GetOrAdd<BossController>(bossGo);
        var bossBody = FindOrCreateChild(bossGo.transform, "Body");
        bossBody.transform.localPosition = new Vector3(0f, bossConfig.hurtboxSize.y * 0.5f, 0f);
        bossBody.transform.localScale = new Vector3(bossConfig.hurtboxSize.x, bossConfig.hurtboxSize.y, 1f);
        var bossSprite = SetSprite(bossBody, square, boss.phase1Color, 2);
        var bossFlash = GetOrAdd<SpriteFlash>(bossBody);
        bossFlash.target = bossSprite;
        var glint = SetSprite(FindOrCreateChild(bossGo.transform, "Glint"), square, Color.white, 5);
        glint.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        glint.enabled = false;
        var marker = SetSprite(FindOrCreateChild(bossGo.transform, "FloorMarker"), square, new Color(1f, 0.3f, 0.2f, 0.6f), 1);
        marker.enabled = false;
        var bossHitbox = SetSprite(FindOrCreateChild(bossGo.transform, "HitboxDebug"), square, new Color(1f, 1f, 1f, 0.35f), 6);
        bossHitbox.enabled = false;

        // Wiring
        hero.config = heroConfig;
        hero.feedback = feedback;
        hero.boss = boss;
        hero.arena = arena;
        hero.body = heroBody.transform;
        hero.flash = heroFlash;
        hero.hitboxDebug = heroHitbox;
        hero.elementOrb = elementOrb;
        hero.waveSprite = square;

        boss.config = bossConfig;
        boss.feedback = feedback;
        boss.hero = hero;
        boss.arena = arena;
        boss.body = bossBody.transform;
        boss.flash = bossFlash;
        boss.glint = glint;
        boss.floorMarker = marker;
        boss.hitboxDebug = bossHitbox;
        boss.projectileSprite = square;

        // Systems
        var systems = FindOrCreateRoot("Systems");
        GetOrAdd<HitStop>(systems);
        var flow = GetOrAdd<FightFlow>(systems);
        flow.hero = hero;
        flow.boss = boss;
        var hud = GetOrAdd<DebugHUD>(systems);
        hud.hero = hero;
        hud.boss = boss;
        hud.flow = flow;

        foreach (var o in new Object[] { hero, boss, flow, hud, arena, heroFlash, bossFlash }) EditorUtility.SetDirty(o);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine($"scene built and saved: {scene.path}");
    }

    static void EnsureInBuildSettings(string path, StringBuilder log)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == TemplateScenePath);
        if (!scenes.Exists(s => s.path == path))
        {
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            log.AppendLine($"added {path} to build settings");
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ------------------------------------------------------------------ helpers

    static Sprite EnsureSquareSprite(StringBuilder log)
    {
        if (!File.Exists(SpritePath))
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color32[32 * 32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpritePath);
            log.AppendLine($"created {SpritePath}");
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 32f)) { importer.spritePixelsPerUnit = 32f; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (changed) importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    static T LoadOrCreate<T>(string path, System.Action<T> init, StringBuilder log) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            log.AppendLine($"kept    {path}");
            return existing;
        }
        var asset = ScriptableObject.CreateInstance<T>();
        init(asset);
        AssetDatabase.CreateAsset(asset, path);
        log.AppendLine($"created {path}");
        return asset;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
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

    static SpriteRenderer SetSprite(GameObject go, Sprite sprite, Color color, int sortingOrder)
    {
        var sr = GetOrAdd<SpriteRenderer>(go);
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);
}
