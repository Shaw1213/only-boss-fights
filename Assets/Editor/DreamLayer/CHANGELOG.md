# Changelog

## 1.0.0 (2026-10-11)

Built and used for the DreamLayer Game Jam game *Only Boss Fights: The Fallen Knight* (Unity 6000.6.4f1, WebGL).

- Sprite-sheet ZIP import: sliced from atlas.json's rects, one shared feet pivot (horizontally on the first frame's
  hip, or atlas `pivot_x`), an AnimationClip keyed on each frame's `frame_durations_ms`, a controller, and an
  import report (durations in 60 fps frames, feet and center jitter, warnings such as `kept_background_frames`).
- Per-folder import rules for single images (sprite type, pixels per unit, pivot, max size, compression), with a
  multiple-of-4 warning for WebGL compression. Overridable per project in
  `ProjectSettings/DreamLayerImportRules.json` (Tools → DreamLayer → Create Import Rules File).
- Tools → DreamLayer → Generate Window: generate / edit / cutout through the DreamLayer CLI with a credit
  confirmation, an idempotency key and a provenance file written before submitting; free Recover by execution id.
- `Tools~/`: the Python cleanup scripts used alongside it (local background removal for `background: keep` sheets,
  drift and size normalization, foot-locked timing).
