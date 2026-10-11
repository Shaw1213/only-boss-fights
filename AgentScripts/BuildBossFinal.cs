using System.IO;
using System.Text;
using DreamLayerJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds (or refreshes) the final boss greybox, the Unknown One: data assets with the numbers from
/// docs/design/boss-final.md, a BossFinal scene copied from the Boss02 greybox, and the lobby's eye relic opened
/// (still sealed until the Knight and the King are cleared).
///
/// The final form is the exam: the Knight's parry tests and the King's floor tests, remixed, plus wood (the only
/// heal) and a finale where the knight inside holds the god for the final blow (BossConfig.finaleAtHealth).
///
/// Safe to re-run: existing data assets are never overwritten (tuned values survive); the scene is copied once and
/// then only re-wired.
///
///   unity command run_script --file AgentScripts/BuildBossFinal.cs --entry BuildBossFinal.All
/// </summary>
public static class BuildBossFinal
{
    const string SourceScene = "Assets/Scenes/Boss02.unity";
    const string ScenePath = "Assets/Scenes/BossFinal.unity";
    const string LobbyScene = "Assets/Scenes/Lobby.unity";
    const string SpritePath = "Assets/Art/Source/Placeholder/Square.png";
    const string Data = "Assets/Data/BossFinal";

    public static string All()
    {
        var log = new StringBuilder();
        EnsureFolder($"{Data}/Attacks");
        EnsureFolder($"{Data}/Moves");
        var config = BuildData(log);
        BuildScene(config, log);
        OpenEye(log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ------------------------------------------------------------------ data

    static BossConfig BuildData(StringBuilder log)
    {
        string d = $"{Data}/Attacks";
        string m = $"{Data}/Moves";

        // The glint is the knight's honor: his sword strikes glint (parry). The god's own attacks never glint:
        // tendrils mark the floor (move) or glow red (dash). Frames at 60 fps.
        //                                                    start act rec dmg     hitbox offset   hitbox size    lunge stop shake
        // Phase 1: the vessel. The knight's moves, the god's reach.
        var cleave = Attack($"{d}/U_HollowCleave.asset",        42, 4, 54, 2, 0f, V(1.6f, 1.6f), V(2.8f, 3.0f), 0f,   6, 0.35f, log);
        // Tendril Lash: a long wood whip (white glint). Parry it to harvest wood: root him and heal 1, the only heal.
        var lash   = Attack($"{d}/U_TendrilLash.asset",         33, 6, 36, 1, 0f, V(2.6f, 1.0f), V(4.2f, 0.9f), 0f,   5, 0.3f,  log, a => a.element = Element.Wood);
        var grasp  = Attack($"{d}/U_GraspingGround.asset",      45, 8, 36, 1, 0f, V(0f, 1.1f),   V(1.8f, 2.2f), 0f,   6, 0.35f, log, Area);
        // Bind and Strike: tendrils mark the hero's spot, then he lunges at wherever the hero stepped to. Move, then parry.
        var bind   = Attack($"{d}/U_Bind.asset",                40, 8,  0, 1, 0f, V(0f, 1.1f),   V(1.8f, 2.2f), 0f,   5, 0.3f,  log, Area);
        var strike = Attack($"{d}/U_BindStrike.asset",          24, 8, 36, 2, 0f, V(1.6f, 1.6f), V(2.2f, 0.8f), 4.5f, 5, 0.3f,  log);

        // Leap Smash: he jumps out of reach; his mark follows the hero, locks 20 frames before he lands (the snarl),
        // and he lands for 3. The long recovery is the reward for getting out.
        var leap   = Attack($"{d}/U_LeapSmash.asset",           72, 6, 54, 3, 0f, V(0f, 1.2f),   V(2.6f, 2.4f), 0f,  10, 0.6f,  log, Leap);

        // Phase 2: the god wakes. Three elements in the Knight's combo rhythm: the last one parried is the one kept.
        var vFire  = Attack($"{d}/U_VolleyFire.asset",          24, 3,  3, 1, 0f, V(0f, 0f),     V(0f, 0f),     0f,   4, 0.2f,  log, a => Lance(a, Element.Fire));
        var vIce   = Attack($"{d}/U_VolleyIce.asset",            9, 3,  3, 1, 0f, V(0f, 0f),     V(0f, 0f),     0f,   4, 0.2f,  log, a => Lance(a, Element.Ice));
        var vWood  = Attack($"{d}/U_VolleyWood.asset",          15, 4, 60, 1, 0f, V(0f, 0f),     V(0f, 0f),     0f,   5, 0.25f, log, a => Lance(a, Element.Wood));
        var march1 = Attack($"{d}/U_Procession1.asset",         36, 6,  0, 1, 0f, V(1.8f, 1.0f), V(1.6f, 2.0f), 0f,   5, 0.3f,  log, Line);
        var march2 = Attack($"{d}/U_Procession2.asset",         10, 6,  0, 1, 0f, V(3.4f, 1.0f), V(1.6f, 2.0f), 0f,   5, 0.3f,  log, Line);
        var march3 = Attack($"{d}/U_Procession3.asset",         10, 6,  0, 1, 0f, V(5.0f, 1.0f), V(1.6f, 2.0f), 0f,   5, 0.3f,  log, Line);
        var march4 = Attack($"{d}/U_Procession4.asset",         10, 6, 40, 1, 0f, V(6.6f, 1.0f), V(1.6f, 2.0f), 0f,   5, 0.3f,  log, Line);
        // The Knight's red ground wave: dash over it.
        var slam   = Attack($"{d}/U_ShockwaveSlam.asset",       36, 6, 40, 1, 0f, V(1.3f, 0.5f), V(1.6f, 1.0f), 0f,   6, 0.40f, log, a =>
        {
            a.parryable = false;
            a.cue = TelegraphCue.RedGlow;
            a.spawnsProjectile = true;
            a.projectileSpeed = 9f;
            a.projectileSize = V(0.9f, 0.7f);
            a.projectileLifetime = 2.5f;
        });
        // Heaven Fall: the Leap Smash, then a red-glow slam where he landed sends a shockwave both ways. Move, then dash.
        var fall   = Attack($"{d}/U_HeavenFall.asset",          66, 6,  0, 3, 0f, V(0f, 1.2f),   V(2.6f, 2.4f), 0f,  10, 0.6f,  log, Leap);
        var quake  = Attack($"{d}/U_HeavenFallQuake.asset",     20, 6, 48, 2, 0f, V(0f, 0.5f),   V(2.4f, 1.0f), 0f,   6, 0.45f, log, a =>
        {
            a.parryable = false;
            a.cue = TelegraphCue.RedGlow;
            a.spawnsProjectile = true;
            a.projectileBothWays = true;
            a.projectileSpeed = 9f;
            a.projectileSize = V(0.9f, 0.7f);
            a.projectileLifetime = 2.5f;
        });
        // Maw: the floor opens wide around the hero (900 ms, 2 damage: a bigger hit gets a longer warning). Run, or dash through it.
        var maw    = Attack($"{d}/U_Maw.asset",                 54, 8, 48, 2, 0f, V(0f, 1.1f),   V(6.0f, 2.2f), 0f,   8, 0.5f,  log, Area);

        //                                                                                            range  tol  weight fullParry
        var mCleave = Move($"{m}/U_Move_HollowCleave.asset",   new[] { cleave },                         2.6f, 0.9f, 1.0f, false, log);
        var mLash   = Move($"{m}/U_Move_TendrilLash.asset",    new[] { lash },                           4.0f, 1.2f, 1.1f, false, log);
        var mGrasp  = Move($"{m}/U_Move_GraspingGround.asset", new[] { grasp },                          5.0f, 4.0f, 1.0f, false, log);
        var mBind   = Move($"{m}/U_Move_BindAndStrike.asset",  new[] { bind, strike },                   4.0f, 1.5f, 0.9f, false, log);
        var mVolley = Move($"{m}/U_Move_ElementVolley.asset",  new[] { vFire, vIce, vWood },             6.5f, 2.5f, 1.0f, true,  log);
        var mMarch  = Move($"{m}/U_Move_Procession.asset",     new[] { march1, march2, march3, march4 }, 4.5f, 2.0f, 0.8f, false, log, mv => mv.lockFacing = true);
        var mSlam   = Move($"{m}/U_Move_ShockwaveSlam.asset",  new[] { slam },                           6.0f, 4.0f, 0.8f, false, log);
        var mMaw    = Move($"{m}/U_Move_Maw.asset",            new[] { maw },                            5.0f, 4.0f, 0.8f, false, log);
        var mLeap   = Move($"{m}/U_Move_LeapSmash.asset",      new[] { leap },                           5.0f, 5.0f, 1.0f, false, log);
        var mFall   = Move($"{m}/U_Move_HeavenFall.asset",     new[] { fall, quake },                    5.0f, 5.0f, 1.2f, false, log);

        var config = LoadOrCreate<BossConfig>($"{Data}/BossFinalConfig.asset", b =>
        {
            b.displayName = "The Unknown One";
            b.maxHealth = 120;
            b.walkSpeed = 2.2f;
            b.approachMaxSeconds = 1.8f;
            b.hurtboxSize = new Vector2(2.0f, 4.0f);
            b.hurtboxOffset = new Vector2(0f, 2.0f);
            b.phase1Moves = new[] { mCleave, mLash, mGrasp, mBind, mLeap };
            b.phase1IdleSeconds = new Vector2(0.45f, 0.8f);
            b.phase2Moves = new[] { mCleave, mLash, mVolley, mMarch, mSlam, mMaw, mBind, mFall };
            b.phase2IdleSeconds = new Vector2(0.2f, 0.4f);
            b.finaleAtHealth = 0.15f;
            b.finaleHoldSeconds = 4f;
            b.finaleText = "The knight holds him. Strike!";
        }, log);
        // Moves added after the first build (v2) join an existing config.
        config.phase1Moves = WithMove(config.phase1Moves, mLeap, log);
        config.phase2Moves = WithMove(config.phase2Moves, mFall, log);
        EditorUtility.SetDirty(config);
        return config;
    }

    static BossMove[] WithMove(BossMove[] moves, BossMove add, StringBuilder log)
    {
        if (System.Array.IndexOf(moves, add) >= 0) return moves;
        log.AppendLine($"added   {add.name}");
        var list = new System.Collections.Generic.List<BossMove>(moves) { add };
        return list.ToArray();
    }

    static void Leap(AttackData a)
    {
        Area(a);
        a.leapHeight = 6f;
        a.leapLockFrames = 20;
        a.cueLeadFrames = 20; // the danger snarl sounds when the mark locks
    }

    static void Lance(AttackData a, Element element)
    {
        a.element = element;
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

        // Greybox: a box the size of his hurtbox until his art exists (art mode needs a PoseSet). Once his art is wired
        // (AgentScripts/ApplyFinalArt.cs), re-running this keeps it: poses and the taken throne room stay.
        bool hasArt = AssetDatabase.LoadAssetAtPath<PoseSet>("Assets/Data/BossFinal/FinalPoses.asset") != null;
        boss.config = config;
        if (!hasArt) boss.poses = null;
        boss.phase1Color = new Color(0.44f, 0.36f, 0.58f); // the vessel: bruised violet
        boss.phase2Color = new Color(0.30f, 0.62f, 0.44f); // the god awake: sickly green
        var square = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (boss.body != null)
        {
            boss.body.localPosition = new Vector3(0f, config.hurtboxSize.y * 0.5f, 0f);
            boss.body.localScale = new Vector3(config.hurtboxSize.x, config.hurtboxSize.y, 1f);
            var sr = boss.body.GetComponent<SpriteRenderer>();
            if (sr != null) { sr.sprite = square; sr.color = boss.phase1Color; EditorUtility.SetDirty(sr); }
            EditorUtility.SetDirty(boss.body);
        }
        flow.fightId = "final";
        var hud = Object.FindAnyObjectByType<DebugHUD>();
        if (hud != null) { hud.nameCardSubtitle = "Final form"; EditorUtility.SetDirty(hud); }

        foreach (var root in scene.GetRootGameObjects())
            if (!hasArt && root.name == "Backdrop" && root.activeSelf) { root.SetActive(false); log.AppendLine("backdrop off (greybox)"); }

        EditorUtility.SetDirty(boss);
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine($"scene wired and saved: {scene.path} (boss {config.displayName}, fightId {flow.fightId})");
    }

    /// <summary>The lobby's eye relic was built as "not built yet": open it (it stays sealed until both bosses are cleared).</summary>
    static void OpenEye(StringBuilder log)
    {
        // Single, not additive: two open scenes means two global 2D lights (an error). The boss scene is saved already.
        var scene = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var e in root.GetComponentsInChildren<LevelEntrance>(true))
                {
                    if (e.fightId != "final") continue;
                    e.sceneName = "BossFinal";
                    if (e.notBuiltYet)
                    {
                        e.notBuiltYet = false;
                        log.AppendLine($"opened lobby relic {e.name} -> BossFinal (sealed until boss01 + boss02)");
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
