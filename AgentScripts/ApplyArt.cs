using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DreamLayerJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires DreamLayer pose sprites into the game: creates the PoseSets, assigns attack poses to the
/// AttackData assets, and sets the hero's and boss's PoseSet in the Boss 1 scene. Re-runnable: poses
/// that aren't imported yet are skipped, so run it again whenever new poses land.
///   unity command run_script --file AgentScripts/ApplyArt.cs --entry ApplyArt.All
/// </summary>
public static class ApplyArt
{
    const string Art = "Assets/Art/DreamLayer/Characters";
    const string ScenePath = "Assets/Scenes/Boss01.unity";

    // pose sprite -> PoseSet field, and pose sprite -> (attack asset, phase). A phase without a pose falls back
    // to the character's idle, so combo follow-ups name a startup pose to avoid flashing idle between hits.
    static readonly (string pose, string field)[] HeroStates =
    {
        ("hero_idle", "idle"), ("hero_block", "parry"), ("hero_charge", "charge"), ("hero_dash", "dash"), ("hero_hurt", "hurt"), ("hero_dead", "dead"),
    };
    static readonly (string pose, string attack, string phase)[] HeroAttacks =
    {
        // Light combo: windup -> slash, (slash held) -> rising slash, windup -> slash as the finisher. (Light 1 plays
        // its sprite sheet instead; its recovery returns to the ready stance, see ApplyAttackAnimation below.)
        ("hero_windup", "Hero/Attacks/Hero_Light1", "startup"), ("hero_slash", "Hero/Attacks/Hero_Light1", "active"),
        ("hero_idle", "Hero/Attacks/Hero_Light1", "recovery"),
        ("hero_slash", "Hero/Attacks/Hero_Light2", "startup"), ("hero_slash2", "Hero/Attacks/Hero_Light2", "active"),
        ("hero_windup", "Hero/Attacks/Hero_Light3", "startup"), ("hero_slash", "Hero/Attacks/Hero_Light3", "active"),
        ("hero_charge", "Hero/Attacks/Hero_ChargedHeavy", "startup"), ("hero_slash", "Hero/Attacks/Hero_ChargedHeavy", "active"),
        // Riposte: the deflect flows into the rising counter.
        ("hero_block", "Hero/Attacks/Hero_Riposte", "startup"), ("hero_slash2", "Hero/Attacks/Hero_Riposte", "active"),
        ("hero_dash", "Hero/Attacks/Hero_DashAttack", "startup"), ("hero_slash", "Hero/Attacks/Hero_DashAttack", "active"),
        // Flash counter: the dash pose through the flash step, then the rising slash from behind.
        ("hero_dash", "Hero/Attacks/Hero_FlashCounter", "startup"), ("hero_slash2", "Hero/Attacks/Hero_FlashCounter", "active"),
    };
    static readonly (string pose, string field)[] KnightStates =
    {
        ("knight_idle", "idle"), ("knight_staggered", "staggered"), ("knight_hurt", "hurt"), ("knight_defeated", "dead"),
        ("knight_phase2", "phaseChange"), // the cracked, scorched armor while he transforms (world art batch)
    };
    static readonly (string pose, string attack, string phase)[] KnightAttacks =
    {
        ("knight_cleave_windup", "Boss01/Attacks/B1_OverheadCleave", "startup"), ("knight_cleave_strike", "Boss01/Attacks/B1_OverheadCleave", "active"),
        // Phase 2's own poses: the slam drives the sword into the floor (and leaves it there through recovery); the
        // Flame Wave sets the blade on fire in the windup, the telegraph, and drags it along the floor to release the wave.
        ("knight_slam_windup", "Boss01/Attacks/B1_ShockwaveSlam", "startup"), ("knight_slam", "Boss01/Attacks/B1_ShockwaveSlam", "active"),
        ("knight_slam", "Boss01/Attacks/B1_ShockwaveSlam", "recovery"),
        ("knight_flame_windup", "Boss01/Attacks/B1_FlameWave", "startup"), ("knight_flame_swing", "Boss01/Attacks/B1_FlameWave", "active"),
        ("knight_cleave_windup", "Boss01/Attacks/B1_Combo1", "startup"), ("knight_cleave_strike", "Boss01/Attacks/B1_Combo1", "active"),
        ("knight_cleave_windup", "Boss01/Attacks/B1_Combo3_Heavy", "startup"), ("knight_cleave_strike", "Boss01/Attacks/B1_Combo3_Heavy", "active"),
        ("knight_cleave_windup", "Boss01/Attacks/B1_Combo3_Chain", "startup"), ("knight_cleave_strike", "Boss01/Attacks/B1_Combo3_Chain", "active"),
        ("knight_cleave_windup", "Boss01/Attacks/B1_Combo4_Delayed", "startup"), ("knight_cleave_strike", "Boss01/Attacks/B1_Combo4_Delayed", "active"),
        ("knight_lunge_windup", "Boss01/Attacks/B1_LungeThrust", "startup"), ("knight_lunge", "Boss01/Attacks/B1_LungeThrust", "active"),
        ("knight_cleave_strike", "Boss01/Attacks/B1_Combo2", "startup"), ("knight_lunge", "Boss01/Attacks/B1_Combo2", "active"),
        // The punish window: every big swing ends leaning on the sword stuck in the ground ("hit him now").
        ("knight_recovery", "Boss01/Attacks/B1_OverheadCleave", "recovery"),
        ("knight_recovery", "Boss01/Attacks/B1_Combo3_Heavy", "recovery"), ("knight_recovery", "Boss01/Attacks/B1_Combo4_Delayed", "recovery"),
    };

    public static string All()
    {
        var log = new StringBuilder();
        AssetDatabase.Refresh();

        var heroPoses = EnsurePoseSet("Assets/Data/Hero/HeroPoses.asset", 2.2f, log);
        var knightPoses = EnsurePoseSet("Assets/Data/Boss01/KnightPoses.asset", 4.0f, log);
        ApplyStates(heroPoses, $"{Art}/Hero", HeroStates, log);
        // Keep one pixel scale for the hero whatever the idle drawing is: the standing master was 804 px for
        // 2.2 units (the ready-stance idle is shorter, so idleWorldHeight follows its height).
        if (heroPoses.idle != null)
        {
            heroPoses.idleWorldHeight = heroPoses.idle.rect.height * (2.2f / 804f);
            log.AppendLine($"HeroPoses.idleWorldHeight = {heroPoses.idleWorldHeight:0.###} ({heroPoses.idle.rect.height} px)");
        }
        ApplyStates(knightPoses, $"{Art}/Knight", KnightStates, log);
        ApplyAttacks($"{Art}/Hero", HeroAttacks, log);
        ApplyAttacks($"{Art}/Knight", KnightAttacks, log);
        // Light 1 as a real sprite sequence (DreamLayer sheet, 512 px): frames 1-3 retimed onto startup + active,
        // the swing (frame 3, a smear) on the 3 active frames. Frame 4 turns to face the camera (and 5-7 repeat it), so
        // the sheet stops there and recovery returns to the ready stance (hero_idle = the sheet's own frame 1), which
        // also hands off to idle / walk without a jump. (hero_slash as recovery threw the head back 445 px.)
        // Size: frame 1 is the idle's stance, 1.82x by height and width (633 / 348 px).
        ApplyAttackAnimation("Hero/Attacks/Hero_Light1", $"{Art}/Hero/hero_slash_anim", "hero_slash_anim", 1.82f, new[] { 0, 1, 2 }, 2, false, log);
        // Movement: a combat-step cycle (custom animation prompt, sword held still, feet on the ground) from a
        // DreamLayer sprite sheet, imported by the DreamLayer importer. The 512 px regeneration (same prompt as the
        // first 256 px sheet, which was soft) walked forward out of its cell, so it is cleaned with --remove-drift and
        // its three cut-off blade tips are rebuilt (Tools/patch_blade_tip.py). Its cells are 1.82x smaller than the
        // pose art: frame 1 is the idle's own stance, exactly 2x the 256 px sheet (3.64x). The authored cycle is 5 s;
        // it plays ~5.6x faster at full speed (moveCycleSpeed 1.1), and backwards when the hero backs away, which a
        // step cycle survives. (hero_run, the earlier sprint sheet, stays in the project for the lobby.)
        ApplyFlipbook(heroPoses, $"{Art}/Hero/hero_step", "hero_step", 1.82f, 1.1f, log);
        // The knight's approach: a heavy walk (custom prompt, 512 px, drift removed). Frame 1 is the master stance,
        // 3.04x by height and width (959 / 315 px). Cycle speed 0.55: ~1.2 units per step at his 2.5 units/s walk.
        ApplyFlipbook(knightPoses, $"{Art}/Knight/knight_walk", "knight_walk", 3.04f, 0.55f, log);

        // Breathing: the knight's slow, heavy breath (the fight hero keeps the PoseSet default, 1.2% every 2.6 s).
        knightPoses.idleBreathAmount = 0.01f;
        knightPoses.idleBreathRate = 1.8f;
        EditorUtility.SetDirty(knightPoses);

        // The lobby's own set: relaxed, sword sheathed on his back. Same pixel scale as the fight (2.2 units per 804 px);
        // the walk sheet was generated from this idle, so its frame 1 is the idle (2.43x by height and width, 754 / 310 px).
        // The loop is frames 8,2,3,4,5,6,7: frame 1 is the idle itself, and with frame 8 the gait passed through two
        // feet-together stances in a row (a stutter every cycle). Starting on 8 (feet together, arms down) also makes
        // idle -> walk seamless. Foot-locked (Tools/foot_travel.py --order 8,2,3,4,5,6,7): frame times follow the planted
        // foot's measured travel, and the cycle speed comes from the total travel (4 footfalls per loop), so the feet
        // don't skate at any walk speed.
        var lobbyPoses = EnsurePoseSet("Assets/Data/Hero/HeroLobbyPoses.asset", 2.0f, log);
        var lobbyIdle = Load($"{Art}/Hero", "hero_lobby_idle");
        if (lobbyIdle != null)
        {
            lobbyPoses.idle = lobbyIdle;
            lobbyPoses.idleWorldHeight = lobbyIdle.rect.height * (2.2f / 804f);
            // At ease, he breathes slower and a little deeper than in his combat stance: 1.5%, one breath every 3.7 s.
            lobbyPoses.idleBreathAmount = 0.015f;
            lobbyPoses.idleBreathRate = 1.7f;
            EditorUtility.SetDirty(lobbyPoses);
            log.AppendLine($"HeroLobbyPoses.idle = hero_lobby_idle ({lobbyPoses.idleWorldHeight:0.###} units)");
        }
        ApplyFlipbook(lobbyPoses, $"{Art}/Hero/hero_lobby_walk", "hero_lobby_walk", 2.43f, 0.35f, log, new[] { 7, 1, 2, 3, 4, 5, 6 }, footLocked: true);
        // 4 footfalls per loop (frames 8, 2, 4, 6 land), for the footstep sounds.
        if (lobbyPoses.move != null) lobbyPoses.move.footfallsPerLoop = 4;
        // 2026-10-11: two generated run sheets were tried in its place (hero_lobby_run: the front arm never came down;
        // hero_lobby_run2: the arms swing but the legs jog on the spot). Shawn: "just use walk ... it looks the most
        // natural. those all look off the running". Both runs stay in the Animation Gym for comparison (BuildAnimGym).

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hero = Object.FindAnyObjectByType<HeroController>();
        var boss = Object.FindAnyObjectByType<BossController>();
        hero.poses = heroPoses;
        boss.poses = knightPoses;
        EditorUtility.SetDirty(hero);
        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        log.AppendLine($"wired poses into {scene.path}");
        return log.ToString();
    }

    /// <param name="footLocked">Use <c>&lt;folder&gt;.foot_travel.json</c> (Tools/foot_travel.py, next to the sheet's ZIP): its
    /// foot-locked frame times, and a cycle speed derived from the measured travel instead of <paramref name="cycleSpeed"/>.</param>
    /// <param name="uniformLoopSeconds">If above 0, every frame lasts the same, this loop length in total (instead of the
    /// sheet's durations).</param>
    static void ApplyFlipbook(PoseSet set, string folder, string name, float scale, float cycleSpeed, StringBuilder log, int[] order = null, bool footLocked = false,
                              float uniformLoopSeconds = 0f)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath($"{folder}/{name}_sheet.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
        string report = $"{folder}/{name}.import.json";
        if (sprites.Length == 0 || !File.Exists(report)) return;
        var seconds = ReadMs(File.ReadAllText(report), "durations_ms");
        if (order != null)
        {
            sprites = order.Select(i => sprites[i]).ToArray();
            if (seconds != null) seconds = order.Select(i => seconds[i]).ToArray();
        }
        string travelReport = $"{folder}.foot_travel.json";
        if (footLocked && File.Exists(travelReport))
        {
            // The cycle covers total_travel_px of floor (sheet pixels) in its authored time: that ground speed, in units
            // at this PoseSet's scale (size 1), is the speed at which it plays at its authored rate. The report must
            // have been measured for the same frame order (foot_travel.py --order, 1-based).
            string json = File.ReadAllText(travelReport);
            var locked = ReadMs(json, "durations_ms");
            var total = Regex.Match(json, @"""total_travel_px""\s*:\s*(\d+)");
            var measured = Regex.Match(json, @"""order""\s*:\s*\[([^\]]*)\]");
            string expected = string.Join(",", (order ?? Enumerable.Range(0, sprites.Length).ToArray()).Select(i => i + 1));
            string got = measured.Success ? Regex.Replace(measured.Groups[1].Value, @"\s", "") : expected;
            if (got != expected)
                log.AppendLine($"WARNING {name}: foot_travel.json was measured for frames [{got}], not [{expected}]; foot lock skipped");
            else if (locked != null && locked.Length == sprites.Length && total.Success)
            {
                seconds = locked;
                float unitsPerCycle = int.Parse(total.Groups[1].Value) / sprites[0].pixelsPerUnit * set.Scale * scale;
                cycleSpeed = unitsPerCycle / seconds.Sum();
                log.AppendLine($"{name}: foot-locked, {unitsPerCycle:0.##} units of floor per cycle -> moveCycleSpeed {cycleSpeed:0.###}");
            }
        }
        if (uniformLoopSeconds > 0f) seconds = Enumerable.Repeat(uniformLoopSeconds / sprites.Length, sprites.Length).ToArray();
        set.move = new Flipbook { frames = sprites, frameSeconds = seconds, loop = true, scale = scale };
        set.moveCycleSpeed = cycleSpeed;
        EditorUtility.SetDirty(set);
        log.AppendLine($"{set.name}.move = {name} ({sprites.Length} frames{(order != null ? " [" + string.Join(",", order.Select(i => i + 1)) + "]" : "")}, {string.Join("/", seconds ?? new float[0])} s, x{scale})");
    }

    static float[] ReadMs(string json, string key)
    {
        var m = Regex.Match(json, $@"""{key}""\s*:\s*\[([^\]]*)\]");
        return m.Success ? m.Groups[1].Value.Split(',').Select(v => int.Parse(v.Trim()) / 1000f).ToArray() : null;
    }

    static void ApplyAttackAnimation(string attackPath, string folder, string name, float scale, int[] order, int hitFrame, bool throughRecovery, StringBuilder log)
    {
        var attack = AssetDatabase.LoadAssetAtPath<AttackData>($"Assets/Data/{attackPath}.asset");
        var sprites = AssetDatabase.LoadAllAssetsAtPath($"{folder}/{name}_sheet.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
        if (attack == null || sprites.Length == 0) return;
        if (order != null) sprites = order.Select(i => sprites[i]).ToArray();
        attack.animation = new Flipbook { frames = sprites, loop = false, scale = scale };
        attack.animationHitFrame = hitFrame;
        attack.animationThroughRecovery = throughRecovery;
        EditorUtility.SetDirty(attack);
        log.AppendLine($"{attack.name}.animation = {name} ({sprites.Length} frames, hit frame {hitFrame + 1}, x{scale}{(throughRecovery ? "" : ", recovery on the poses")})");
    }

    static PoseSet EnsurePoseSet(string path, float height, StringBuilder log)
    {
        var set = AssetDatabase.LoadAssetAtPath<PoseSet>(path);
        if (set != null) return set;
        set = ScriptableObject.CreateInstance<PoseSet>();
        set.idleWorldHeight = height;
        AssetDatabase.CreateAsset(set, path);
        log.AppendLine($"created {path}");
        return set;
    }

    static Sprite Load(string folder, string pose) => AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{pose}.png");

    static void ApplyStates(PoseSet set, string folder, (string pose, string field)[] table, StringBuilder log)
    {
        foreach (var (pose, field) in table)
        {
            var sprite = Load(folder, pose);
            if (sprite == null) continue;
            typeof(PoseSet).GetField(field).SetValue(set, sprite);
            log.AppendLine($"{set.name}.{field} = {pose}");
        }
        EditorUtility.SetDirty(set);
    }

    static void ApplyAttacks(string folder, (string pose, string attack, string phase)[] table, StringBuilder log)
    {
        foreach (var (pose, attackPath, phase) in table)
        {
            var sprite = Load(folder, pose);
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>($"Assets/Data/{attackPath}.asset");
            if (sprite == null || attack == null) continue;
            if (phase == "startup") attack.startupPose = sprite;
            else if (phase == "active") attack.activePose = sprite;
            else attack.recoveryPose = sprite;
            EditorUtility.SetDirty(attack);
            log.AppendLine($"{Path.GetFileName(attackPath)}.{phase} = {pose}");
        }
    }
}
