using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace DreamLayer.UnityTools
{
    /// <summary>
    /// Imports a DreamLayer sprite-sheet ZIP natively in the Unity editor (no Python, no Resources).
    ///
    /// ZIP (sheet.png + atlas.json) →
    ///   &lt;name&gt;/&lt;name&gt;_sheet.png, sliced from the atlas rects (top-left origin, flipped for Unity)
    ///   one shared pivot at the feet: from atlas "alignment" + the sheet's own alpha baseline; horizontally on the
    ///   first frame's hip (the centroid of the 50-65% height band, the same anchor Tools/normalize_pose.py gives
    ///   single poses), because "source_canvas" sheets keep the reference's framing and the body sits off-center;
    ///   an atlas "pivot_x" overrides it (when a limb at hip height pulls the measurement off the body)
    ///   &lt;name&gt;/&lt;name&gt;.anim, keyed on each frame's own frame_durations_ms (not one fixed fps)
    ///   &lt;name&gt;/&lt;name&gt;.controller (created once, never overwritten)
    ///   &lt;name&gt;/&lt;name&gt;.import.json, a report incl. frame data (60 fps frames) and anchor jitter
    /// Warns on kept_background_frames (frames where DreamLayer's cutout failed).
    ///
    /// Use: drop the ZIP anywhere under the import root, Assets/Art/DreamLayer/ by default (auto-imports), or
    /// Tools → DreamLayer → Import Sprite Sheet ZIP…, or right-click a ZIP → DreamLayer → Import.
    /// Re-importing updates the sheet and clip in place (references keep working).
    /// </summary>
    public static class DreamLayerSpriteSheetImporter
    {
        const long MaxZipBytes = 200L * 1024 * 1024;
        const int MaxEntries = 400;
        const int MaxSheetSize = 2048; // WebGL budget; DreamLayer's frame size is free, so export smaller instead

        public class Result
        {
            public string name, sheetPath, clipPath, controllerPath;
            public int frames;
            public float totalSeconds;
            public bool loop;
            public Vector2 pivot;
            public float pixelsPerUnit;
            public int[] durationsMs;
            public int baselineSpreadPx, centerSpreadPx;
            public List<string> warnings = new List<string>();
        }

        // ------------------------------------------------------------------ menus

        [MenuItem("Tools/DreamLayer/Import Sprite Sheet ZIP...")]
        static void ImportFromDisk()
        {
            string zip = EditorUtility.OpenFilePanel("DreamLayer sprite-sheet ZIP", "", "zip");
            if (string.IsNullOrEmpty(zip)) return;
            string destDir = DreamLayerImportRules.Root + "Characters";
            Directory.CreateDirectory(destDir);
            string dest = AssetDatabase.GenerateUniqueAssetPath($"{destDir}/{Path.GetFileName(zip)}");
            File.Copy(zip, dest);
            AssetDatabase.ImportAsset(dest); // DreamLayerZipWatcher imports it
        }

        [MenuItem("Assets/DreamLayer/Import Sprite Sheet ZIP", true)]
        static bool ValidateImportSelected() =>
            Selection.activeObject != null && AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        [MenuItem("Assets/DreamLayer/Import Sprite Sheet ZIP")]
        static void ImportSelected() => Import(AssetDatabase.GetAssetPath(Selection.activeObject));

        // ------------------------------------------------------------------ import

        public static Result Import(string zipAssetPath)
        {
            zipAssetPath = zipAssetPath.Replace('\\', '/');
            var result = new Result { name = SafeName(Path.GetFileNameWithoutExtension(zipAssetPath)) };
            string zipFull = Path.GetFullPath(zipAssetPath);
            if (new FileInfo(zipFull).Length > MaxZipBytes) throw new InvalidDataException("ZIP is larger than 200 MB.");

            // 1. Read atlas.json + the sheet image from the ZIP (with basic safety checks).
            string atlasText;
            byte[] sheetBytes;
            using (var archive = ZipFile.OpenRead(zipFull))
            {
                if (archive.Entries.Count > MaxEntries) throw new InvalidDataException($"ZIP has more than {MaxEntries} entries.");
                foreach (var e in archive.Entries)
                    if (e.FullName.Contains("..") || Path.IsPathRooted(e.FullName))
                        throw new InvalidDataException($"Unsafe ZIP entry: {e.FullName}");
                atlasText = ReadText(archive, "atlas.json") ?? throw new InvalidDataException("atlas.json not found in the ZIP.");
                var atlasForImage = (JsonObject)DreamLayerJson.Parse(atlasText);
                string imageName = atlasForImage.GetString("image", "sheet.png");
                sheetBytes = ReadBytes(archive, imageName) ?? throw new InvalidDataException($"{imageName} not found in the ZIP.");
            }
            var atlas = (JsonObject)DreamLayerJson.Parse(atlasText);

            // 2. Frames: TexturePacker-style hash (DreamLayer today), array, or a plain grid as fallback.
            var frames = ReadFrames(atlas, result.name);
            if (frames.Count == 0) throw new InvalidDataException("atlas.json lists no frames.");
            int declared = atlas.GetInt("frame_count", frames.Count);
            if (declared != frames.Count) result.warnings.Add($"atlas frame_count is {declared} but {frames.Count} frame rects were found.");
            result.frames = frames.Count;

            // 3. Timing: per-frame durations (held poses last longer), never one fixed fps.
            result.durationsMs = ReadDurations(atlas, frames.Count, result.warnings);
            result.totalSeconds = result.durationsMs.Sum() / 1000f;
            result.loop = DecideLoop(atlas);

            var kept = atlas.GetArray("kept_background_frames");
            if (kept != null && kept.Count > 0)
                result.warnings.Add($"DreamLayer kept the background on frame(s) {string.Join(", ", kept)} (cutout failed). Check or clean them before use.");
            if (atlas.GetBool("enlarged"))
                result.warnings.Add("Frames were enlarged from a smaller source; a larger export doesn't add detail.");

            // 4. Write the sheet into a folder named after the ZIP.
            string dir = $"{Path.GetDirectoryName(zipAssetPath).Replace('\\', '/')}/{result.name}";
            Directory.CreateDirectory(dir);
            result.sheetPath = $"{dir}/{result.name}{DreamLayerTexturePostprocessor.SheetSuffix}";
            File.WriteAllBytes(result.sheetPath, sheetBytes);
            File.WriteAllText($"{dir}/{result.name}_atlas.json", atlasText);

            // 5. Pivot at the feet: measure each frame's lowest opaque row and use the median baseline.
            var sheetTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheetTex.LoadImage(sheetBytes);
            int sheetW = sheetTex.width, sheetH = sheetTex.height;
            string alignment = atlas.GetString("alignment", "ground_anchor");
            var folderRule = DreamLayerImportRules.For(result.sheetPath);
            // Custom animations come back as "source_canvas"; in a feet-pivot folder (Characters/, Props/) the
            // character still stands on the ground, so measure the feet anyway.
            bool feet = alignment == "ground_anchor" || folderRule.pivot.y == 0f;
            if (feet && alignment != "ground_anchor")
                result.warnings.Add($"alignment is \"{alignment}\"; measured a feet pivot anyway because {folderRule.folder}/ uses feet pivots.");
            result.pivot = MeasurePivot(sheetTex, frames, feet, out result.baselineSpreadPx, out result.centerSpreadPx);
            UnityEngine.Object.DestroyImmediate(sheetTex);
            // A big limb at hip height (a clawed arm hanging in front) pulls the hip measurement off the body. An atlas
            // "pivot_x" (0-1 across the cell, written by a cleanup step that knows where the body is) wins over it.
            if (atlas.Has("pivot_x"))
            {
                float measured = result.pivot.x;
                result.pivot.x = Mathf.Clamp01((float)atlas.GetNumber("pivot_x", measured));
                result.warnings.Add($"pivot x {result.pivot.x:0.###} from atlas \"pivot_x\" (measured {measured:0.###}).");
            }
            if (Mathf.Max(sheetW, sheetH) > MaxSheetSize)
                result.warnings.Add($"Sheet is {sheetW}x{sheetH}, above {MaxSheetSize}. DreamLayer's frame size costs nothing, so a smaller frame_size keeps WebGL builds lighter.");

            // 6. Import + slice through the Sprite Editor data provider (spritesheet API is removed in Unity 6).
            AssetDatabase.ImportAsset(result.sheetPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(result.sheetPath);
            var rule = DreamLayerImportRules.For(result.sheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = Mathf.Min(MaxSheetSize, Mathf.NextPowerOfTwo(Mathf.Max(sheetW, sheetH)));
            result.pixelsPerUnit = importer.spritePixelsPerUnit > 0 ? importer.spritePixelsPerUnit : rule.pixelsPerUnit;
            Slice(importer, frames, sheetH, result.pivot, result.name);
            importer.SaveAndReimport();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(result.sheetPath).OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);
            var ordered = new Sprite[frames.Count];
            for (int i = 0; i < frames.Count; i++)
                if (!sprites.TryGetValue(FrameName(result.name, i), out ordered[i]))
                    throw new InvalidDataException($"Sliced sprite {FrameName(result.name, i)} is missing after import.");

            // 7. AnimationClip keyed on the real durations; Animator controller created once.
            result.clipPath = $"{dir}/{result.name}.anim";
            WriteClip(result.clipPath, ordered, result.durationsMs, result.loop);
            result.controllerPath = $"{dir}/{result.name}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(result.controllerPath) == null)
                AnimatorController.CreateAnimatorControllerAtPathWithClip(result.controllerPath, AssetDatabase.LoadAssetAtPath<AnimationClip>(result.clipPath));

            // 8. Report. Import just the files we wrote (no full Refresh: this can run inside an import callback).
            string reportPath = $"{dir}/{result.name}.import.json";
            File.WriteAllText(reportPath, ReportJson(result, zipAssetPath, atlas));
            AssetDatabase.ImportAsset(reportPath);
            AssetDatabase.ImportAsset($"{dir}/{result.name}_atlas.json");
            Debug.Log(Summary(result));
            foreach (var w in result.warnings) Debug.LogWarning($"[DreamLayer] {result.name}: {w}");
            return result;
        }

        // ------------------------------------------------------------------ atlas reading

        static List<RectInt> ReadFrames(JsonObject atlas, string name)
        {
            var rects = new List<RectInt>();
            void AddRect(JsonObject fr)
            {
                var r = fr.GetObject("frame") ?? fr; // TexturePacker nests {frame:{x,y,w,h}}; plain rects also accepted
                rects.Add(new RectInt(r.GetInt("x"), r.GetInt("y"), r.GetInt("w"), r.GetInt("h")));
            }

            if (atlas.GetObject("frames") is JsonObject hash)
                foreach (var kv in hash) { if (kv.Value is JsonObject fr) AddRect(fr); }
            else if (atlas.GetArray("frames") is List<object> list)
                foreach (var o in list) { if (o is JsonObject fr) AddRect(fr); }
            else
            {
                int cell = atlas.GetInt("cell"), cols = Mathf.Max(1, atlas.GetInt("columns", 1)), count = atlas.GetInt("frame_count");
                for (int i = 0; i < count; i++) rects.Add(new RectInt((i % cols) * cell, (i / cols) * cell, cell, cell));
            }
            return rects;
        }

        static int[] ReadDurations(JsonObject atlas, int count, List<string> warnings)
        {
            var list = atlas.GetArray("frame_durations_ms");
            var d = new int[count];
            if (list != null && list.Count == count)
            {
                for (int i = 0; i < count; i++) d[i] = Mathf.Max(1, (int)Math.Round((double)list[i]));
                double declared = atlas.GetNumber("duration_ms", d.Sum());
                if (Math.Abs(declared - d.Sum()) > 1)
                    warnings.Add($"duration_ms says {declared} ms but frame_durations_ms sum to {d.Sum()} ms; using the per-frame values.");
                return d;
            }
            if (list != null) warnings.Add($"frame_durations_ms has {list.Count} entries for {count} frames; spreading duration_ms evenly instead.");
            int each = Mathf.Max(1, (int)Math.Round(atlas.GetNumber("duration_ms", 100.0 * count) / count));
            for (int i = 0; i < count; i++) d[i] = each;
            return d;
        }

        /// <summary>animation_mode if present; else sampling ("cycle" loops); else the walk/run/idle presets loop.</summary>
        static bool DecideLoop(JsonObject atlas)
        {
            string mode = atlas.GetString("animation_mode");
            if (mode != null) return mode == "loop";
            string sampling = atlas.GetString("sampling");
            if (sampling != null) return sampling == "cycle";
            string action = atlas.GetString("action");
            return action == "walk" || action == "run" || action == "idle";
        }

        // ------------------------------------------------------------------ pivot + slicing

        static Vector2 MeasurePivot(Texture2D sheet, List<RectInt> frames, bool feet, out int baselineSpread, out int centerSpread)
        {
            baselineSpread = centerSpread = 0;
            if (!feet) return new Vector2(0.5f, 0.5f);

            var px = sheet.GetPixels32();
            int w = sheet.width, h = sheet.height;
            var bottoms = new List<int>();
            var centers = new List<int>();
            float hipX = -1f;
            foreach (var f in frames)
            {
                int highest = -1, lowest = -1;
                long sumX = 0, n = 0;
                for (int y = 0; y < f.height; y++)          // y from the frame's top (atlas coordinates)
                {
                    int texY = h - 1 - (f.y + y);            // texture rows are bottom-up
                    if (texY < 0 || texY >= h) continue;
                    for (int x = 0; x < f.width; x++)
                    {
                        int texX = f.x + x;
                        if (texX < 0 || texX >= w || px[texY * w + texX].a < 16) continue;
                        if (highest < 0) highest = y;
                        lowest = y;
                        sumX += x;
                        n++;
                    }
                }
                if (lowest < 0) continue; // empty frame
                bottoms.Add(f.height - 1 - lowest);           // transparent rows under the feet
                centers.Add((int)(sumX / n));
                if (hipX < 0f) hipX = HipX(px, w, h, f, highest, lowest);
            }
            if (bottoms.Count == 0) return new Vector2(0.5f, 0f);

            baselineSpread = bottoms.Max() - bottoms.Min();
            centerSpread = centers.Max() - centers.Min();
            bottoms.Sort();
            int median = bottoms[bottoms.Count / 2];
            float pivotX = hipX >= 0f ? hipX / frames[0].width : 0.5f;
            return new Vector2(Mathf.Clamp01(pivotX), Mathf.Clamp01(median / (float)frames[0].height));
        }

        /// <summary>Centroid x of the opaque pixels in the hip band (50-65% of the body's height), in frame pixels.</summary>
        static float HipX(Color32[] px, int w, int h, RectInt f, int top, int bottom)
        {
            int body = bottom - top;
            long sumX = 0, n = 0;
            for (int y = top + (int)(0.50f * body); y < top + (int)(0.65f * body); y++)
            {
                int texY = h - 1 - (f.y + y);
                if (texY < 0 || texY >= h) continue;
                for (int x = 0; x < f.width; x++)
                {
                    int texX = f.x + x;
                    if (texX < 0 || texX >= w || px[texY * w + texX].a < 16) continue;
                    sumX += x;
                    n++;
                }
            }
            return n > 0 ? sumX / (float)n : -1f;
        }

        static void Slice(TextureImporter importer, List<RectInt> frames, int sheetHeight, Vector2 pivot, string name)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // Keep sprite IDs stable across re-imports so clips and scenes keep their references.
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = new SpriteRect[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                string spriteName = FrameName(name, i);
                rects[i] = new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(f.x, sheetHeight - f.y - f.height, f.width, f.height), // atlas is top-left origin
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    spriteID = existing.TryGetValue(spriteName, out var id) ? id : GUID.Generate(),
                };
            }
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
        }

        // ------------------------------------------------------------------ clip

        static void WriteClip(string path, Sprite[] sprites, int[] durationsMs, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.frameRate = 1000f; // keys land on exact milliseconds

            var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
            float t = 0f;
            for (int i = 0; i < sprites.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = t, value = sprites[i] };
                t += durationsMs[i] / 1000f;
            }
            keys[sprites.Length] = new ObjectReferenceKeyframe { time = t, value = sprites[sprites.Length - 1] }; // hold the last frame its full duration

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ helpers

        static string FrameName(string name, int i) => $"{name}_{i:00}";

        static string SafeName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.Length > 0 ? sb.ToString() : "dreamlayer_sprite";
        }

        static ZipArchiveEntry Find(ZipArchive archive, string name) =>
            archive.Entries.FirstOrDefault(e => e.FullName == name) ??
            archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + name)); // ZIPs with a top-level folder

        static string ReadText(ZipArchive archive, string name)
        {
            var e = Find(archive, name);
            if (e == null) return null;
            using (var r = new StreamReader(e.Open(), Encoding.UTF8)) return r.ReadToEnd();
        }

        static byte[] ReadBytes(ZipArchive archive, string name)
        {
            var e = Find(archive, name);
            if (e == null) return null;
            using (var s = e.Open())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        static string Summary(Result r)
        {
            var frames60 = r.durationsMs.Select(ms => Mathf.RoundToInt(ms * 60f / 1000f));
            return $"[DreamLayer] Imported {r.name}: {r.frames} frames, {r.totalSeconds:0.###} s, {(r.loop ? "loop" : "once")}, " +
                   $"pivot ({r.pivot.x:0.00}, {r.pivot.y:0.00}), {r.pixelsPerUnit} px/unit. " +
                   $"Durations ms [{string.Join(", ", r.durationsMs)}] = 60fps frames [{string.Join(", ", frames60)}]. " +
                   $"Feet baseline spread {r.baselineSpreadPx}px, horizontal center spread {r.centerSpreadPx}px. → {r.clipPath}";
        }

        static string ReportJson(Result r, string zip, JsonObject atlas)
        {
            string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
            var frames60 = r.durationsMs.Select(ms => Mathf.RoundToInt(ms * 60f / 1000f));
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"source_zip\": \"{Esc(zip)}\",");
            sb.AppendLine($"  \"imported_utc\": \"{DateTime.UtcNow:o}\",");
            sb.AppendLine($"  \"action\": \"{Esc(atlas.GetString("action", ""))}\",");
            sb.AppendLine($"  \"frames\": {r.frames},");
            sb.AppendLine($"  \"loop\": {(r.loop ? "true" : "false")},");
            sb.AppendLine($"  \"total_seconds\": {r.totalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"durations_ms\": [{string.Join(", ", r.durationsMs)}],");
            sb.AppendLine($"  \"durations_60fps_frames\": [{string.Join(", ", frames60)}],");
            sb.AppendLine($"  \"pivot\": [{r.pivot.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}, {r.pivot.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}],");
            sb.AppendLine($"  \"pixels_per_unit\": {r.pixelsPerUnit.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"feet_baseline_spread_px\": {r.baselineSpreadPx},");
            sb.AppendLine($"  \"horizontal_center_spread_px\": {r.centerSpreadPx},");
            sb.AppendLine($"  \"sheet\": \"{Esc(r.sheetPath)}\",");
            sb.AppendLine($"  \"clip\": \"{Esc(r.clipPath)}\",");
            sb.AppendLine($"  \"controller\": \"{Esc(r.controllerPath)}\",");
            sb.AppendLine($"  \"warnings\": [{string.Join(", ", r.warnings.Select(w => $"\"{Esc(w)}\""))}]");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Auto-imports DreamLayer sprite ZIPs dropped under <see cref="DreamLayerImportRules.Root"/>.
    /// Runs the import directly (asset operations are allowed here); a deferred delayCall didn't fire reliably.
    /// </summary>
    class DreamLayerZipWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!DreamLayerImportRules.IsManaged(path) || !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                try { DreamLayerSpriteSheetImporter.Import(path); }
                catch (Exception e) { Debug.LogError($"[DreamLayer] Couldn't import {path}: {e.Message}"); }
            }
        }
    }
}
