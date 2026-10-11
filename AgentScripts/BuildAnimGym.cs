using System.Linq;
using System.Text;
using DreamLayerJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds (or refreshes) the Animation Gym scene: a copy of the Boss 1 scene's camera, 2D light and
/// volume (so sprites are lit exactly like in the fight), with the gameplay removed and the gym added.
///   unity command run_script --file AgentScripts/BuildAnimGym.cs --entry BuildAnimGym.All
/// </summary>
public static class BuildAnimGym
{
    const string Source = "Assets/Scenes/Boss01.unity";
    const string ScenePath = "Assets/Scenes/AnimGym.unity";
    const string LobbyScene = "Assets/Scenes/Lobby.unity";
    const string SquarePath = "Assets/Art/Source/Placeholder/Square.png";
    const float GroundY = -2f;
    const string RunSheet = "Assets/Art/DreamLayer/Characters/Hero/hero_lobby_run/hero_lobby_run_sheet.png";
    // The second run sheet (2026-10-11, Shawn: the arms never came down): 9 frames, arms swinging, same reference and scale.
    const string RunSheet2 = "Assets/Art/DreamLayer/Characters/Hero/hero_lobby_run2/hero_lobby_run2_sheet.png";
    const float RunScale = 2.44f;         // as in ApplyArt: frame 1 is the lobby idle at 2.44x
    const float StepSeconds = 1f / 2f;    // 2 steps a second, as in ApplyArt

    public static string All()
    {
        var log = new StringBuilder();
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) EditorSceneManager.SaveScene(active);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            AssetDatabase.CopyAsset(Source, ScenePath);
            log.AppendLine($"copied {Source} -> {ScenePath}");
        }
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Hero" || root.name == "Boss" || root.name == "Systems" || root.name == "Arena")
            {
                log.AppendLine($"removed {root.name}");
                Object.DestroyImmediate(root);
            }

        var cam = Camera.main;
        foreach (var c in new System.Type[] { typeof(CameraFit), typeof(CameraShake) })
            if (cam.TryGetComponent(c, out var comp)) Object.DestroyImmediate(comp);
        cam.orthographic = true;
        cam.orthographicSize = 2.8f;
        cam.transform.position = new Vector3(1f, GroundY + 1.3f, -10f);

        var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        var gymGo = FindOrCreateRoot("Gym");
        var gym = gymGo.TryGetComponent<AnimationGym>(out var g) ? g : gymGo.AddComponent<AnimationGym>();
        gym.groundY = GroundY;
        gym.subjectX = -0.6f;
        gym.referenceX = 3.0f;

        var ground = Child(gymGo, "Ground", square, new Color(0.45f, 0.45f, 0.5f), 0);
        ground.transform.position = new Vector3(0f, GroundY - 0.015f, 0f);
        ground.transform.localScale = new Vector3(40f, 0.03f, 1f);
        for (int i = -6; i <= 8; i++)
        {
            // 1-unit ruler ticks along the floor
            var tick = Child(gymGo, $"Tick {i}", square, new Color(0.45f, 0.45f, 0.5f), 0);
            tick.transform.position = new Vector3(i, GroundY - 0.08f, 0f);
            tick.transform.localScale = new Vector3(0.02f, i % 5 == 0 ? 0.16f : 0.1f, 1f);
        }
        gym.reference = Child(gymGo, "Reference", null, Color.white, 1);
        gym.ghost = Child(gymGo, "Ghost", null, Color.white, 2);
        gym.subject = Child(gymGo, "Subject", null, Color.white, 3);
        gym.hurtbox = Child(gymGo, "Hurtbox", square, new Color(0.2f, 1f, 0.4f, 0.16f), 0); // behind the character
        gym.hitbox = Child(gymGo, "Hitbox", square, new Color(1f, 1f, 1f, 0.35f), 6);
        gym.pivotMarker = Child(gymGo, "Pivot", square, new Color(1f, 0.2f, 1f, 0.9f), 7);
        gym.pivotMarker.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
        gym.pivotMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        gym.cueMarker = Child(gymGo, "Cue", square, Color.white, 8);
        gym.cueMarker.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        gym.cueMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        gym.heroConfig = AssetDatabase.LoadAssetAtPath<HeroConfig>("Assets/Data/Hero/HeroConfig.asset");
        gym.bossConfig = AssetDatabase.LoadAssetAtPath<BossConfig>("Assets/Data/Boss01/Boss01Config.asset");
        gym.heroPoses = AssetDatabase.LoadAssetAtPath<PoseSet>("Assets/Data/Hero/HeroPoses.asset");
        gym.bossPoses = AssetDatabase.LoadAssetAtPath<PoseSet>("Assets/Data/Boss01/KnightPoses.asset");

        // The lobby set, at the size and walk speed the Lobby scene gives it (read from its LobbyHero, so the gym
        // can't drift from the game).
        gym.lobbyPoses = AssetDatabase.LoadAssetAtPath<PoseSet>("Assets/Data/Hero/HeroLobbyPoses.asset");
        var lobby = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Additive);
        foreach (var root in lobby.GetRootGameObjects())
        {
            var lobbyHero = root.GetComponentInChildren<LobbyHero>(true);
            if (lobbyHero == null) continue;
            gym.lobbyArtScale = lobbyHero.artScale;
            gym.lobbyWalkSpeedFactor = lobbyHero.walkSpeedFactor;
            log.AppendLine($"lobby set: size x{gym.lobbyArtScale}, walk x{gym.lobbyWalkSpeedFactor} of the fight speed");
            break;
        }
        EditorSceneManager.CloseScene(lobby, true);

        // The two generated run sheets tried for the lobby on 2026-10-11 (the walk stayed: ApplyArt), kept to compare
        // against it: same size, played at 2 steps a second at the speed a run would need (4.8 units/s).
        var run = AssetDatabase.LoadAllAssetsAtPath(RunSheet).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        var run2 = AssetDatabase.LoadAllAssetsAtPath(RunSheet2).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        float cycleSpeed = gym.heroConfig.moveSpeed * 0.8f / gym.lobbyArtScale; // a run's 4.8 units/s (the walk is 3.0)
        AnimationGym.CycleVariant Variant(Sprite[] sheet, string name, int[] frames, int footfalls) => new AnimationGym.CycleVariant
        {
            name = name, cycleSpeed = cycleSpeed,
            cycle = new Flipbook
            {
                frames = frames.Select(f => sheet[f - 1]).ToArray(), loop = true, scale = RunScale, footfallsPerLoop = footfalls,
                frameSeconds = Enumerable.Repeat(StepSeconds * footfalls / frames.Length, frames.Length).ToArray(),
            },
        };
        var variants = new System.Collections.Generic.List<AnimationGym.CycleVariant>();
        if (run2.Length == 9)
            variants.Add(Variant(run2, "Run v2, not used (arms swing): 2-9", new[] { 2, 3, 4, 5, 6, 7, 8, 9 }, 2));
        if (run.Length == 8)
        {
            variants.Add(Variant(run, "Run v1, not used (arm up): 2-3-4-5", new[] { 2, 3, 4, 5 }, 1));
            variants.Add(Variant(run, "Run v1, not used: 2-3-4-5-8-3-4-5", new[] { 2, 3, 4, 5, 8, 3, 4, 5 }, 2));
            variants.Add(Variant(run, "Run v1, not used: 2-8 as delivered", new[] { 2, 3, 4, 5, 6, 7, 8 }, 2));
        }
        gym.lobbyCycleVariants = variants.ToArray();
        log.AppendLine($"lobby run variants: {gym.lobbyCycleVariants.Length}");

        EditorUtility.SetDirty(gym);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine($"gym built: {scene.path}");
        return log.ToString();
    }

    static SpriteRenderer Child(GameObject parent, string name, Sprite sprite, Color color, int order)
    {
        var t = parent.transform.Find(name);
        var go = t != null ? t.gameObject : new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var sr = go.TryGetComponent<SpriteRenderer>(out var r) ? r : go.AddComponent<SpriteRenderer>();
        if (sprite != null) sr.sprite = sprite;
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
}
