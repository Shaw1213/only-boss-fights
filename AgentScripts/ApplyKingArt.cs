using System.IO;
using System.Text;
using DreamLayerJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires the King's DreamLayer art into the Boss 2 scene: his PoseSet (idle, hurt, defeated, the frozen phase change),
/// the poses of each attack, a hurtbox fitted to the art, and the throne room behind him (calm, and frozen over in
/// phase 2). Poses are imported first by Tools/import_poses.py (king_*), arenas by Tools/import_world_art.py.
/// Re-runnable.
///   unity command run_script --file AgentScripts/ApplyKingArt.cs --entry ApplyKingArt.All
/// </summary>
public static class ApplyKingArt
{
    const string Art = "Assets/Art/DreamLayer/Characters/King";
    const string Backgrounds = "Assets/Art/DreamLayer/Backgrounds";
    const string ScenePath = "Assets/Scenes/Boss02.unity";
    const string PosesPath = "Assets/Data/Boss02/KingPoses.asset";
    const string ConfigPath = "Assets/Data/Boss02/Boss02Config.asset";

    // He stands 4.6 units tall with crown and plume: 15% over the Knight (4.0), twice the hero (2.2). His hurtbox is the
    // armored body without crown, plume or sword, in the same proportion as the Knight's (2.9 of 4.0), and wider: the
    // pauldrons.
    const float IdleWorldHeight = 4.6f;
    static readonly Vector2 HurtboxSize = new Vector2(2.0f, 3.35f);

    static readonly (string pose, string field)[] States =
    {
        ("king_idle", "idle"), ("king_hurt", "hurt"), ("king_hurt", "staggered"), ("king_defeated", "dead"),
        ("king_phase2", "phaseChange"), // frost climbing his armor while he transforms
    };

    // Each move family has its own silhouette, so the pose says which answer is coming before the cue does:
    // the sword raised = the cleave (parry), the palm thrust = a lance (parry or dash through), the sword planted in the
    // floor = the floor erupts (move). Recovery falls back to idle unless named.
    static readonly (string pose, string attack, string phase)[] Attacks =
    {
        ("king_cleave_windup", "K_RoyalCleave", "startup"), ("king_cleave_strike", "K_RoyalCleave", "active"),
        ("king_cleave_strike", "K_RoyalCleave", "recovery"), // the punish window: his blade is down
        ("king_cast", "K_FrostLance", "startup"), ("king_cast", "K_FrostLance", "active"),
        ("king_cast", "K_Volley1", "startup"), ("king_cast", "K_Volley1", "active"),
        ("king_cast", "K_Volley2", "startup"), ("king_cast", "K_Volley2", "active"),
        ("king_cast", "K_Volley3", "startup"), ("king_cast", "K_Volley3", "active"),
        ("king_floor_cast", "K_GlacierRise", "startup"), ("king_floor_cast", "K_GlacierRise", "active"), ("king_floor_cast", "K_GlacierRise", "recovery"),
        ("king_floor_cast", "K_TwinRise1", "startup"), ("king_floor_cast", "K_TwinRise1", "active"), ("king_floor_cast", "K_TwinRise1", "recovery"),
        ("king_floor_cast", "K_TwinRise2", "startup"), ("king_floor_cast", "K_TwinRise2", "active"), ("king_floor_cast", "K_TwinRise2", "recovery"),
        ("king_floor_cast", "K_Procession1", "startup"), ("king_floor_cast", "K_Procession1", "active"), ("king_floor_cast", "K_Procession1", "recovery"),
        ("king_floor_cast", "K_Procession2", "startup"), ("king_floor_cast", "K_Procession2", "active"), ("king_floor_cast", "K_Procession2", "recovery"),
        ("king_floor_cast", "K_Procession3", "startup"), ("king_floor_cast", "K_Procession3", "active"), ("king_floor_cast", "K_Procession3", "recovery"),
        ("king_floor_cast", "K_Procession4", "startup"), ("king_floor_cast", "K_Procession4", "active"), ("king_floor_cast", "K_Procession4", "recovery"),
    };

    // The throne room (Tools/import_world_art.py, unfitted): feet on row 1219.5 of 1440, like arena1, drawn 4% wider
    // than the view. It's painted very pale, and so is the King, so it sits much darker than the Knight's hall to keep
    // him readable (the "grayscale check"), with a cold blue cast; the frozen version is busier at the top but darker on
    // the floor, so it needs a little less.
    const float ArenaGroundFraction = 1219.5f / 1440f;
    const float ArenaWidthUnits = 17.78f * 1.04f;
    static readonly Color CalmTint = new Color(0.56f, 0.60f, 0.70f, 1f);
    static readonly Color FrozenTint = new Color(0.62f, 0.66f, 0.76f, 1f);

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
        poses.idleBreathAmount = 0.01f; // slow and heavy, like the Knight's
        poses.idleBreathRate = 1.6f;
        EditorUtility.SetDirty(poses);
        log.AppendLine($"KingPoses: idle {poses.idle?.name}, {IdleWorldHeight} units tall (scale {poses.Scale:0.####})");

        int wired = 0;
        foreach (var (pose, attackName, phase) in Attacks)
        {
            var sprite = Load(pose);
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>($"Assets/Data/Boss02/Attacks/{attackName}.asset");
            if (sprite == null || attack == null) { log.AppendLine($"skipped {attackName}.{phase} ({pose})"); continue; }
            if (phase == "startup") attack.startupPose = sprite;
            else if (phase == "active") attack.activePose = sprite;
            else attack.recoveryPose = sprite;
            EditorUtility.SetDirty(attack);
            wired++;
        }
        log.AppendLine($"attack poses wired: {wired}/{Attacks.Length}");

        var config = AssetDatabase.LoadAssetAtPath<BossConfig>(ConfigPath);
        config.hurtboxSize = HurtboxSize;
        config.hurtboxOffset = new Vector2(0f, HurtboxSize.y * 0.5f);
        config.floorStyle = FloorStyle.IceSpikes; // Shawn: his floor attacks are ice spikes, not the red marker
        EditorUtility.SetDirty(config);
        log.AppendLine($"hurtbox {HurtboxSize.x} x {HurtboxSize.y} (was the greybox's 2 x 4); floor attacks: ice spikes");

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isDirty && scene.path != ScenePath) EditorSceneManager.SaveScene(scene);
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boss = Object.FindAnyObjectByType<BossController>();
        var arena = Object.FindAnyObjectByType<Arena>();
        boss.poses = poses;
        EditorUtility.SetDirty(boss);

        // The throne room replaces the greybox floor.
        var calm = AssetDatabase.LoadAssetAtPath<Sprite>($"{Backgrounds}/arena2.png");
        var frozen = AssetDatabase.LoadAssetAtPath<Sprite>($"{Backgrounds}/arena2_frozen.png");
        GameObject root = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "Backdrop") root = r;
        if (calm != null && root != null)
        {
            root.SetActive(true);
            if (!root.TryGetComponent<ArenaBackdrop>(out var backdrop)) backdrop = root.AddComponent<ArenaBackdrop>();
            backdrop.boss = boss;
            backdrop.calm = Layer(root.transform, "Calm", calm, -100, CalmTint, arena.groundY, log);
            backdrop.phase2 = frozen != null ? Layer(root.transform, "Phase2", frozen, -99, FrozenTint, arena.groundY, log) : null;
            EditorUtility.SetDirty(backdrop);
            EditorUtility.SetDirty(root);
            var ground = arena.transform.Find("Ground");
            if (ground != null && ground.TryGetComponent<SpriteRenderer>(out var groundRenderer))
            {
                groundRenderer.enabled = false;
                EditorUtility.SetDirty(groundRenderer);
            }
            log.AppendLine("backdrop on: throne room (calm / frozen), greybox floor hidden");
        }
        else log.AppendLine(calm == null ? "arena2 not imported yet" : "no Backdrop object in the scene");

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
