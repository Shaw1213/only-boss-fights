using System.Text;
using DreamLayerJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires the Unknown One's DreamLayer art into the final fight: his PoseSet (idle, hurt, defeated, the god waking for
/// phase 2), the poses of each attack, a hurtbox fitted to the art, and the throne room he took (calm, and torn open in
/// phase 2). Poses are imported first by Tools/import_poses.py (final_*), arenas by Tools/import_world_art.py.
/// Re-runnable.
///   unity command run_script --file AgentScripts/ApplyFinalArt.cs --entry ApplyFinalArt.All
/// </summary>
public static class ApplyFinalArt
{
    const string Art = "Assets/Art/DreamLayer/Characters/Final";
    const string Backgrounds = "Assets/Art/DreamLayer/Backgrounds";
    const string ScenePath = "Assets/Scenes/BossFinal.unity";
    const string PosesPath = "Assets/Data/BossFinal/FinalPoses.asset";
    const string ConfigPath = "Assets/Data/BossFinal/BossFinalConfig.asset";
    const string Attacks = "Assets/Data/BossFinal/Attacks";

    // The last form is the biggest: 4.8 units with crown and plume (the King 4.6, the Knight 4.0, the hero 2.2). His
    // hurtbox is the armored body, as wide as the King's so body contact and spacing stay the same (1.4); the claw arm
    // hangs outside it, like a sword.
    const float IdleWorldHeight = 4.8f;
    static readonly Vector2 HurtboxSize = new Vector2(2.0f, 3.5f);

    static readonly (string pose, string field)[] States =
    {
        ("final_idle", "idle"), ("final_hurt", "hurt"), ("final_hurt", "staggered"), ("final_defeated", "dead"),
        ("final_phase2", "phaseChange"), // the god wakes: the chest eye opens, tentacles burst out
    };

    // The danger language in silhouettes: the sword raised = the knight's cleave (it glints: parry); the claw thrust
    // forward = the god's lash and lances (parry them, harvest); the claw sunk into the floor = the floor opens (move);
    // airborne then crouched = the leap (move, then punish). Recovery falls back to idle unless named.
    static readonly (string pose, string attack, string phase)[] AttackPoses =
    {
        ("final_cleave_windup", "U_HollowCleave", "startup"), ("final_cleave_strike", "U_HollowCleave", "active"),
        ("final_cleave_strike", "U_HollowCleave", "recovery"), // the punish window: his blade is down
        ("final_lash", "U_TendrilLash", "startup"), ("final_lash", "U_TendrilLash", "active"),
        ("final_floor_cast", "U_GraspingGround", "startup"), ("final_floor_cast", "U_GraspingGround", "active"), ("final_floor_cast", "U_GraspingGround", "recovery"),
        ("final_floor_cast", "U_Bind", "startup"), ("final_floor_cast", "U_Bind", "active"),
        ("final_cleave_windup", "U_BindStrike", "startup"), ("final_cleave_strike", "U_BindStrike", "active"), ("final_cleave_strike", "U_BindStrike", "recovery"),
        // The leap: airborne while his mark hunts the hero, crouched where he landed (it's also his take-off crouch).
        ("final_leap", "U_LeapSmash", "startup"), ("final_smash", "U_LeapSmash", "active"), ("final_smash", "U_LeapSmash", "recovery"),
        ("final_leap", "U_HeavenFall", "startup"), ("final_smash", "U_HeavenFall", "active"),
        // Heaven Fall's follow-up and the Knight's ground wave: the sword raised again (red glow), then slammed down.
        ("final_cleave_windup", "U_HeavenFallQuake", "startup"), ("final_smash", "U_HeavenFallQuake", "active"), ("final_smash", "U_HeavenFallQuake", "recovery"),
        ("final_cleave_windup", "U_ShockwaveSlam", "startup"), ("final_smash", "U_ShockwaveSlam", "active"), ("final_smash", "U_ShockwaveSlam", "recovery"),
        ("final_lash", "U_VolleyFire", "startup"), ("final_lash", "U_VolleyFire", "active"),
        ("final_lash", "U_VolleyIce", "startup"), ("final_lash", "U_VolleyIce", "active"),
        ("final_lash", "U_VolleyWood", "startup"), ("final_lash", "U_VolleyWood", "active"),
        ("final_floor_cast", "U_Procession1", "startup"), ("final_floor_cast", "U_Procession1", "active"),
        ("final_floor_cast", "U_Procession2", "startup"), ("final_floor_cast", "U_Procession2", "active"),
        ("final_floor_cast", "U_Procession3", "startup"), ("final_floor_cast", "U_Procession3", "active"),
        ("final_floor_cast", "U_Procession4", "startup"), ("final_floor_cast", "U_Procession4", "active"), ("final_floor_cast", "U_Procession4", "recovery"),
        ("final_floor_cast", "U_Maw", "startup"), ("final_floor_cast", "U_Maw", "active"), ("final_floor_cast", "U_Maw", "recovery"),
    };

    // The taken throne room (Tools/import_world_art.py, unfitted, like arena2): same canvas and feet row. Unlike the
    // King's, it stays fairly light behind the fighters (he is dark), so it's only dimmed a little, with a cold violet
    // cast; the torn version's void is brighter, so it's dimmed a bit more.
    const float ArenaGroundFraction = 1219.5f / 1440f;
    const float ArenaWidthUnits = 17.78f * 1.04f;
    static readonly Color CalmTint = new Color(0.80f, 0.78f, 0.86f, 1f);
    static readonly Color TornTint = new Color(0.74f, 0.72f, 0.80f, 1f);

    public static string All()
    {
        var log = new StringBuilder();
        AssetDatabase.Refresh();

        var poses = AssetDatabase.LoadAssetAtPath<PoseSet>(PosesPath);
        if (poses == null)
        {
            poses = ScriptableObject.CreateInstance<PoseSet>();
            AssetDatabase.CreateAsset(poses, PosesPath);
            log.AppendLine($"created {PosesPath}");
        }
        foreach (var (pose, field) in States)
        {
            var sprite = Load(pose);
            if (sprite == null) { log.AppendLine($"missing {pose}"); continue; }
            typeof(PoseSet).GetField(field).SetValue(poses, sprite);
        }
        poses.idleWorldHeight = IdleWorldHeight;
        poses.idleBreathAmount = 0.012f; // heavy, like the King, with a little more heave: something else breathes in there
        poses.idleBreathRate = 1.4f;
        EditorUtility.SetDirty(poses);
        log.AppendLine($"FinalPoses: idle {poses.idle?.name}, {IdleWorldHeight} units tall (scale {poses.Scale:0.####})");

        int wired = 0;
        foreach (var (pose, attackName, phase) in AttackPoses)
        {
            var sprite = Load(pose);
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>($"{Attacks}/{attackName}.asset");
            if (sprite == null || attack == null) { log.AppendLine($"skipped {attackName}.{phase} ({pose})"); continue; }
            if (phase == "startup") attack.startupPose = sprite;
            else if (phase == "active") attack.activePose = sprite;
            else attack.recoveryPose = sprite;
            EditorUtility.SetDirty(attack);
            wired++;
        }
        log.AppendLine($"attack poses wired: {wired}/{AttackPoses.Length}");

        var config = AssetDatabase.LoadAssetAtPath<BossConfig>(ConfigPath);
        config.hurtboxSize = HurtboxSize;
        config.hurtboxOffset = new Vector2(0f, HurtboxSize.y * 0.5f);
        config.floorStyle = FloorStyle.Tentacles; // Shawn: his floor attacks, his wave and his root are tentacles
        EditorUtility.SetDirty(config);
        log.AppendLine($"hurtbox {HurtboxSize.x} x {HurtboxSize.y} (was the greybox's 2 x 4); floor attacks: tentacles");

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isDirty && scene.path != ScenePath) EditorSceneManager.SaveScene(scene);
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boss = Object.FindAnyObjectByType<BossController>();
        var arena = Object.FindAnyObjectByType<Arena>();
        boss.poses = poses;
        EditorUtility.SetDirty(boss);

        // The taken throne room replaces the greybox floor.
        var calm = AssetDatabase.LoadAssetAtPath<Sprite>($"{Backgrounds}/arena3.png");
        var torn = AssetDatabase.LoadAssetAtPath<Sprite>($"{Backgrounds}/arena3_torn.png");
        GameObject root = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "Backdrop") root = r;
        if (calm != null && root != null)
        {
            root.SetActive(true);
            if (!root.TryGetComponent<ArenaBackdrop>(out var backdrop)) backdrop = root.AddComponent<ArenaBackdrop>();
            backdrop.boss = boss;
            backdrop.calm = Layer(root.transform, "Calm", calm, -100, CalmTint, arena.groundY, log);
            backdrop.phase2 = torn != null ? Layer(root.transform, "Phase2", torn, -99, TornTint, arena.groundY, log) : null;
            EditorUtility.SetDirty(backdrop);
            EditorUtility.SetDirty(root);
            var ground = arena.transform.Find("Ground");
            if (ground != null && ground.TryGetComponent<SpriteRenderer>(out var groundRenderer))
            {
                groundRenderer.enabled = false;
                EditorUtility.SetDirty(groundRenderer);
            }
            log.AppendLine("backdrop on: the taken throne room (calm / torn), greybox floor hidden");
        }
        else log.AppendLine(calm == null ? "arena3 not imported yet" : "no Backdrop object in the scene");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        log.AppendLine($"saved {scene.path}");
        return log.ToString();
    }

    static Sprite Load(string pose) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/{pose}.png");

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
        float centerY = groundY + (ArenaGroundFraction - 0.5f) * height; // centered pivot
        t.localScale = new Vector3(scale, scale, 1f);
        t.position = new Vector3(0f, centerY, 5f);
        EditorUtility.SetDirty(sr);
        EditorUtility.SetDirty(t);
        log.AppendLine($"{name} = {sprite.name}: {ArenaWidthUnits:0.##} x {height:0.##} units at y {centerY:0.###}");
        return sr;
    }
}
