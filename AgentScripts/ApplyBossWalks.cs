using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DreamLayerJam;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The King's and the Unknown One's approach walks (they used to slide in their idle poses, as the Knight once did).
/// Each is a DreamLayer 8-frame walk sheet (custom prompt, 512 px, background kept), cleaned locally:
///   clean_sprite_zip.py --snap-feet --remove-drift, then normalize_sheet_scale.py --skip-red --match-idle (the sheets
///   zoomed in ~20% across the cycle; every frame is scaled back to frame 1's height, and the pivot is set so the walk
///   starts where the idle stands), then foot_travel.py (frame times follow the planted foot, so it doesn't skate).
/// The importer slices the ZIP; this sets PoseSet.move. Re-runnable, and independent of ApplyKingArt / ApplyFinalArt
/// (they don't touch the walk).
///   unity command run_script --file AgentScripts/ApplyBossWalks.cs --entry ApplyBossWalks.All
/// </summary>
public static class ApplyBossWalks
{
    const string Art = "Assets/Art/DreamLayer/Characters";

    public static string All()
    {
        var log = new StringBuilder();
        AssetDatabase.Refresh();
        // Flipbook scales: the idle's height over frame 1's (normalize_sheet_scale.py --match-idle).
        Walk("Assets/Data/Boss02/KingPoses.asset", $"{Art}/King/king_walk", "king_walk", 2.954f, log);
        Walk("Assets/Data/BossFinal/FinalPoses.asset", $"{Art}/Final/final_walk", "final_walk", 3.522f, log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static void Walk(string posesPath, string folder, string name, float scale, StringBuilder log)
    {
        var set = AssetDatabase.LoadAssetAtPath<PoseSet>(posesPath);
        var sprites = AssetDatabase.LoadAllAssetsAtPath($"{folder}/{name}_sheet.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
        string travelPath = $"{folder}.foot_travel.json";
        if (set == null || sprites.Length == 0 || !File.Exists(travelPath))
        {
            log.AppendLine($"skipped {name}: poses {set != null}, {sprites.Length} sprites, foot travel {File.Exists(travelPath)}");
            return;
        }
        // Foot-locked: the cycle covers total_travel_px of floor (sheet pixels) in its authored time; that ground speed,
        // in units at this PoseSet's scale, is the speed at which it plays at its authored rate.
        string json = File.ReadAllText(travelPath);
        var seconds = Regex.Match(json, @"""durations_ms""\s*:\s*\[([^\]]*)\]").Groups[1].Value
                           .Split(',').Select(v => int.Parse(v.Trim()) / 1000f).ToArray();
        int travelPx = int.Parse(Regex.Match(json, @"""total_travel_px""\s*:\s*(\d+)").Groups[1].Value);
        float unitsPerCycle = travelPx / sprites[0].pixelsPerUnit * set.Scale * scale;
        set.move = new Flipbook { frames = sprites, frameSeconds = seconds, loop = true, scale = scale, footfallsPerLoop = 2 };
        set.moveCycleSpeed = unitsPerCycle / seconds.Sum();
        EditorUtility.SetDirty(set);
        log.AppendLine($"{set.name}.move = {name} ({sprites.Length} frames, x{scale}, foot-locked: {unitsPerCycle:0.##} units per cycle, " +
                       $"moveCycleSpeed {set.moveCycleSpeed:0.###}, step {set.StepLength:0.##} units)");
    }
}
