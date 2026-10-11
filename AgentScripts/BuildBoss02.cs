using System.IO;
using System.Text;
using DreamLayerJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds (or refreshes) the Boss 2 greybox, the King: data assets with the numbers from docs/design/boss-02.md, a
/// Boss02 scene copied from Boss01 (same hero, camera, arena and systems) with the King's data and a placeholder box,
/// and the lobby's crown relic opened.
///
/// Safe to re-run: existing data assets are never overwritten (tuned values survive); the scene is copied once and
/// then only re-wired.
///
///   unity command run_script --file AgentScripts/BuildBoss02.cs --entry BuildBoss02.All
/// </summary>
public static class BuildBoss02
{
    const string SourceScene = "Assets/Scenes/Boss01.unity";
    const string ScenePath = "Assets/Scenes/Boss02.unity";
    const string LobbyScene = "Assets/Scenes/Lobby.unity";
    const string SpritePath = "Assets/Art/Source/Placeholder/Square.png";
    const string Data = "Assets/Data/Boss02";

    public static string All()
    {
        var log = new StringBuilder();
        EnsureFolder($"{Data}/Attacks");
        EnsureFolder($"{Data}/Moves");
        var config = BuildData(log);
        BuildScene(config, log);
        OpenCrown(log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ------------------------------------------------------------------ data

    static BossConfig BuildData(StringBuilder log)
    {
        string d = $"{Data}/Attacks";
        string m = $"{Data}/Moves";

        // The Knight's timing signatures carry over: the cleave keeps 700 ms / 900 ms, the volley keeps the
        // three-strike rhythm (400 ms, then a hit every 250 ms). Frames at 60 fps.
        //                                                   start act rec dmg     hitbox offset   hitbox size    lunge stop shake
        var cleave = Attack($"{d}/K_RoyalCleave.asset",        42, 4, 54, 1, 0f, V(1.6f, 1.6f), V(2.8f, 3.0f), 0f,   6, 0.35f, log);

        // Frost Lance: an ice shard at chest height. Parryable (white glint), so it can be harvested: ice freezes him.
        // No melee hitbox: only the shard hits.
        var lance = Attack($"{d}/K_FrostLance.asset",          33, 4, 30, 1, 0f, V(0f, 0f),     V(0f, 0f),     0f,   5, 0.25f, log, Lance);

        // Glacier Rise: ice erupts where the hero stood when he raised his hand (floor marker: move out).
        var rise = Attack($"{d}/K_GlacierRise.asset",          45, 8, 36, 1, 0f, V(0f, 1.1f),   V(1.8f, 2.2f), 0f,   6, 0.35f, log, Area);

        // Phase 2. Frost Volley: three lances in the Knight's combo rhythm; parry all three for a stagger.
        var volley1 = Attack($"{d}/K_Volley1.asset",           24, 3,  3, 1, 0f, V(0f, 0f),     V(0f, 0f),     0f,   4, 0.2f,  log, Lance);
        var volley2 = Attack($"{d}/K_Volley2.asset",            9, 3,  3, 1, 0f, V(0f, 0f),     V(0f, 0f),     0f,   4, 0.2f,  log, Lance);
        var volley3 = Attack($"{d}/K_Volley3.asset",           15, 4, 60, 1, 0f, V(0f, 0f),     V(0f, 0f),     0f,   5, 0.25f, log, Lance);

        // Twin Rise: the second eruption marks wherever the hero moved to.
        var twin1 = Attack($"{d}/K_TwinRise1.asset",           45, 8,  6, 1, 0f, V(0f, 1.1f),   V(1.8f, 2.2f), 0f,   6, 0.35f, log, Area);
        var twin2 = Attack($"{d}/K_TwinRise2.asset",           30, 8, 36, 1, 0f, V(0f, 1.1f),   V(1.8f, 2.2f), 0f,   6, 0.35f, log, Area);

        // Procession: spikes erupt in a line from him toward the hero, ~270 ms apart. Dash through, or stay out of reach.
        var march1 = Attack($"{d}/K_Procession1.asset",        36, 6,  0, 1, 0f, V(1.8f, 1.0f), V(1.6f, 2.0f), 0f,   5, 0.3f,  log, Line);
        var march2 = Attack($"{d}/K_Procession2.asset",        10, 6,  0, 1, 0f, V(3.4f, 1.0f), V(1.6f, 2.0f), 0f,   5, 0.3f,  log, Line);
        var march3 = Attack($"{d}/K_Procession3.asset",        10, 6,  0, 1, 0f, V(5.0f, 1.0f), V(1.6f, 2.0f), 0f,   5, 0.3f,  log, Line);
        var march4 = Attack($"{d}/K_Procession4.asset",        10, 6, 40, 1, 0f, V(6.6f, 1.0f), V(1.6f, 2.0f), 0f,   5, 0.3f,  log, Line);

        //                                                                                 range  tol  weight fullParry
        var mCleave = Move($"{m}/K_Move_RoyalCleave.asset", new[] { cleave },                   2.6f, 0.9f, 1.0f, false, log);
        // He backs off to cast: the hero has to close the distance through his lances.
        var mLance  = Move($"{m}/K_Move_FrostLance.asset",  new[] { lance },                    7.0f, 2.5f, 1.2f, false, log);
        var mRise   = Move($"{m}/K_Move_GlacierRise.asset", new[] { rise },                     5.0f, 4.0f, 1.0f, false, log);
        var mVolley = Move($"{m}/K_Move_FrostVolley.asset", new[] { volley1, volley2, volley3 }, 6.5f, 2.5f, 1.0f, true,  log);
        var mTwin   = Move($"{m}/K_Move_TwinRise.asset",    new[] { twin1, twin2 },             5.0f, 4.0f, 1.0f, false, log);
        var mMarch  = Move($"{m}/K_Move_Procession.asset",  new[] { march1, march2, march3, march4 }, 4.5f, 2.0f, 0.9f, false, log, mv => mv.lockFacing = true);

        return LoadOrCreate<BossConfig>($"{Data}/Boss02Config.asset", b =>
        {
            b.displayName = "The Frozen King";
            b.maxHealth = 100;
            b.walkSpeed = 1.8f; // a king doesn't chase
            b.approachMaxSeconds = 2.0f;
            b.hurtboxSize = new Vector2(2.0f, 4.0f);
            b.hurtboxOffset = new Vector2(0f, 2.0f);
            b.phase1Moves = new[] { mCleave, mLance, mRise };
            b.phase1IdleSeconds = new Vector2(0.7f, 1.1f);
            b.phase2Moves = new[] { mCleave, mVolley, mTwin, mMarch, mRise };
            b.phase2IdleSeconds = new Vector2(0.35f, 0.6f);
        }, log);
    }

    static void Lance(AttackData a)
    {
        a.element = Element.Ice;
        a.spawnsProjectile = true;
        a.projectileSpeed = 9f;
        a.projectileSize = V(0.9f, 0.6f);
        a.projectileHeight = 0.8f;
        a.projectileLifetime = 3f;
    }

    static void Area(AttackData a)
    {
        a.atHero = true;
        a.parryable = false;
        a.cue = TelegraphCue.FloorMarker;
    }

    static void Line(AttackData a)
    {
        a.parryable = false;
        a.cue = TelegraphCue.FloorMarker;
    }

    // ------------------------------------------------------------------ scene

    static void BuildScene(BossConfig config, StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            var current = EditorSceneManager.GetActiveScene();
            if (current.isDirty) EditorSceneManager.SaveScene(current);
            log.AppendLine(AssetDatabase.CopyAsset(SourceScene, ScenePath) ? $"copied {SourceScene} -> {ScenePath}" : "copy failed");
        }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureInBuildSettings(ScenePath, log);

        var boss = Object.FindAnyObjectByType<BossController>();
        var flow = Object.FindAnyObjectByType<FightFlow>();
        if (boss == null || flow == null) { log.AppendLine("no BossController / FightFlow in the scene"); return; }

        // Greybox King: a box the size of his hurtbox until his art exists (art mode needs a PoseSet). Once his art is
        // wired (AgentScripts/ApplyKingArt.cs), re-running this keeps it: poses and the throne room stay.
        bool hasArt = AssetDatabase.LoadAssetAtPath<PoseSet>("Assets/Data/Boss02/KingPoses.asset") != null;
        boss.config = config;
        if (!hasArt) boss.poses = null;
        boss.phase1Color = new Color(0.36f, 0.52f, 0.86f); // royal blue
        boss.phase2Color = new Color(0.66f, 0.86f, 0.98f); // frozen
        var square = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (boss.body != null)
        {
            boss.body.localPosition = new Vector3(0f, config.hurtboxSize.y * 0.5f, 0f);
            boss.body.localScale = new Vector3(config.hurtboxSize.x, config.hurtboxSize.y, 1f);
            var sr = boss.body.GetComponent<SpriteRenderer>();
            if (sr != null) { sr.sprite = square; sr.color = boss.phase1Color; EditorUtility.SetDirty(sr); }
            EditorUtility.SetDirty(boss.body);
        }
        flow.fightId = "boss02";
        var hud = Object.FindAnyObjectByType<DebugHUD>();
        if (hud != null) { hud.nameCardSubtitle = "Second form"; EditorUtility.SetDirty(hud); }

        // The Knight's painting doesn't belong here; the greybox floor shows until the throne room is painted.
        foreach (var root in scene.GetRootGameObjects())
        {
            if (hasArt) break;
            if (root.name == "Backdrop" && root.activeSelf) { root.SetActive(false); log.AppendLine("backdrop off (greybox)"); }
            if (root.name == "Arena")
            {
                var ground = root.transform.Find("Ground");
                var sr = ground != null ? ground.GetComponent<SpriteRenderer>() : null;
                if (sr != null && !sr.enabled) { sr.enabled = true; EditorUtility.SetDirty(sr); }
            }
        }

        EditorUtility.SetDirty(boss);
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine($"scene wired and saved: {scene.path} (boss {config.displayName}, fightId {flow.fightId})");
    }

    /// <summary>The lobby's crown relic was built as "not built yet": open it now that the fight exists.</summary>
    static void OpenCrown(StringBuilder log)
    {
        // Single, not additive: two open scenes means two global 2D lights (an error). The boss scene is saved already.
        var scene = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var e in root.GetComponentsInChildren<LevelEntrance>(true))
                {
                    if (e.fightId != "boss02") continue;
                    e.sceneName = "Boss02";
                    if (e.notBuiltYet)
                    {
                        e.notBuiltYet = false;
                        log.AppendLine($"opened lobby relic {e.name} -> Boss02");
                    }
                    EditorUtility.SetDirty(e);
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static void EnsureInBuildSettings(string path, StringBuilder log)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        log.AppendLine($"added {path} to build settings");
    }

    // ------------------------------------------------------------------ helpers (as in BuildBoss01)

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

    static BossMove Move(string path, AttackData[] steps, float range, float tolerance, float weight, bool fullParryStaggers, StringBuilder log,
        System.Action<BossMove> extra = null)
    {
        return LoadOrCreate<BossMove>(path, mv =>
        {
            mv.steps = steps;
            mv.preferredRange = range;
            mv.rangeTolerance = tolerance;
            mv.weight = weight;
            mv.fullParryStaggers = fullParryStaggers;
            extra?.Invoke(mv);
        }, log);
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

    static Vector2 V(float x, float y) => new Vector2(x, y);
}
