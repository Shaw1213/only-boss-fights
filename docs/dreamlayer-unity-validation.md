# DreamLayer `unity-sprite-import`: Unity editor validation record

Executed the repository's own [`docs/unity-editor-test-plan.md`](https://github.com/TheDesignFounder/dreamlayer-agent-plugin/blob/main/docs/unity-editor-test-plan.md)
in a real Unity editor. Intended as the `research/` record the plan asks for, and as evidence for an
issue / pull request.

- **Repository:** TheDesignFounder/dreamlayer-agent-plugin @ `a0244b6` (2026-09-27), MIT
- **Unity:** 6000.6.4f1 (Unity 6.6), Windows 11, URP with the 2D Renderer, active build target WebGL
- **Python:** 3.12 (run with `-I`)
- **Date:** 2026-10-08
- **Credits spent:** 0 (local import of the repo's own sample, `examples/source/walk.zip`)

## Steps and results

| Step (from the test plan) | Result |
|---|---|
| 1. `scripts/import_sprite.py --engine unity --zip examples/source/walk.zip --out <new dir>` | ✅ `{"engine": "unity", "frames": 7, "duration_ms": 2830, ...}`; output byte-identical to `examples/unity/Assets/DreamLayerDemo` |
| 2. Copy `Assets/DreamLayerDemo` into a Unity project | ✅ |
| 3. Scripts compile, no compiler errors | ✅ No errors **and no warnings** |
| 4. Tools → DreamLayer → Create sprite demo | ✅ Creates `DreamLayer sprite demo` (SpriteRenderer + DreamLayerFramePlayer) and a demo camera (ortho size 1.5) when there is no main camera |
| 5. Press Play | ✅ Runs |

## "What must be true"

| Criterion | Result |
|---|---|
| Seven sprites load from `Resources/DreamLayerDemo`, `walk_01`–`walk_07` | ✅ |
| Each 128×128, real alpha, Sprite, `alphaIsTransparency`, no compression | ✅ (type Sprite, Single, PPU 128, mipmaps off, Bilinear) |
| Frames advance on the manifest's durations: 400, 410, 400, 410, 400, 410, 400 ms | ✅ Measured each change in game time: 399–411 ms per frame over two full cycles |
| The animation loops (`loop: true`) | ✅ `walk_07 → walk_01` observed twice |
| No console errors or warnings from the DreamLayer scripts during Play | ✅ None |

**Verdict: the Unity sample passes its test plan in Unity 6000.6.4f1.**

## Limitations found (outside the test plan, relevant to real game use)

1. **Pivot is centered.** Sprites keep the default pivot (0.5, 0.5), so the feet sit 0.5 units below
   the GameObject's origin. The atlas says `alignment: "ground_anchor"`, which should map to a
   bottom-center pivot so characters stand on the ground.
2. **Pixels-per-unit is hard-coded to 128.** Correct for the 128 px sample. The API's default
   `frame_size` is 512, which would make a character 4 units tall, overflowing the demo camera's
   3-unit view. PPU should come from the bundle's `cell` (or a project setting).
3. **WebGL ships the frames uncompressed** (the WebGL automatic format is RGBA32): 64 KB per 128 px
   frame, ~1 MB per 512 px frame. Everything under `Resources/` is always included in a build, used
   or not.
4. **One hard-coded `DreamLayerDemo` folder.** A second animation collides with the first; the
   script refuses an existing output folder, and copying into `Assets/` merges.
5. **Looping falls back to the preset name.** `loop` comes from `animation_mode`, or, when absent
   (as in the sample), only `walk` / `run` / `idle` loop. `sampling: "cycle"` is ignored, so a
   looping custom animation from a bundle without `animation_mode` would play once and stop.
6. **Not an in-editor workflow.** It needs Python, a command line and a manual copy, and it
   ignores the bundle's own `sheet.png` / `atlas.json` (it rebuilds from `frames/`), `enlarged`,
   `scale` and **`kept_background_frames`** (frames where the cutout failed).
7. **The atlas can't be read with Unity's built-in `JsonUtility`.** `frames` is an object keyed by
   frame name, which matters for any C# importer reading `atlas.json` directly.

## Suggested fix (being built in this project)

A native Unity editor importer (no Python):
- Drop a DreamLayer sprite ZIP under `Assets/Art/DreamLayer/` → slice `sheet.png` from the
  `atlas.json` rects (y-flipped).
- Pivot from `alignment` (`ground_anchor` → bottom-center); PPU from `cell` (or a project setting).
- Compressed textures, no `Resources`.
- Build an `AnimationClip` timed by `frame_durations_ms`, looping from `animation_mode` /
  `sampling` / preset.
- Warn on `kept_background_frames`; keep a provenance sidecar.
- Ship as a standalone drop-in folder with a README.
