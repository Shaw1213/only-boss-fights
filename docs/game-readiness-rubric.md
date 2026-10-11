# Game-readiness rubric

A small, repeatable score for whether a DreamLayer output can go into the game. Score every kept
asset, plus a sample of rejects to show *why* they were rejected. Each criterion is 0–2, with
concrete anchors so two people would give the same score.

**Gameplay size** = how big the asset is on screen in the 960×540 build. Fill in once the greybox
arena is blocked out: hero ≈ ___ px tall, boss ≈ ___ px tall.

| # | Criterion | 2 — ship it | 1 — usable with fixes | 0 — reject |
|---|---|---|---|---|
| 1 | **Identity consistency** | Same character at a glance: silhouette, palette, costume and signature item match the reference | Small detail drift (trim, accessory) not noticeable at gameplay size | Reads as a different character, or the signature item changes |
| 2 | **Scale / anchor stability** | Scale drift ≤ 5%; ground contact within ±2 px at gameplay size | Drift 5–15% or jitter ±3–6 px; fixable by rescale / re-pivot | Drift > 15% or jitter > 6 px |
| 3 | **Alpha cleanliness** | Clean edges, no halo, no ground shadow, no holes | Light fringe or shadow, removable locally (chroma key / erode) | Holes in the character, baked background, or needs a paid cutout redo |
| 4 | **Silhouette at gameplay size** | Pose and intent read as a solid black silhouette at gameplay size | Reads in color only | Can't tell the pose or the telegraph |
| 5 | **Import effort** | Importer defaults work: drop in, done | < 5 min of manual work (pivot, crop, re-slice) | Repainting, or > 15 min |
| 6 | **Frame-data usability** *(animations only)* | Startup / active / recovery map cleanly from `frame_durations_ms`; hit frame readable; cell fits the longest reach; smear frames still read | Needs retiming, or one frame dropped / held | Hit frame unreadable, clipping at the cell edge, or jitter large enough to break hitboxes |

**Verdict:** static assets are scored out of 10, animations out of 12.
**Ship** = no zeros, and ≥ 8/10 (static) or ≥ 9/12 (animation).
Any 0 → reject or fix, and log why in [dreamlayer-notes.md](dreamlayer-notes.md).

## How to measure quickly

- **Scale drift:** measure a rigid feature (head height, weapon length) against the reference at the
  same canvas size; formula in [metrics.md](metrics.md).
- **Anchor jitter:** onion-skin the frames aligned on their sheet cells and track the foot / ground
  contact point.
- **Silhouette:** view the sprite as solid black at gameplay size (squint test).
- **Frame data:** convert `frame_durations_ms` to 60 fps frames (÷ 16.7) and label each frame
  startup / active / recovery.
- *Later, if time allows:* the Unity importer can compute alpha bounding boxes and anchor offsets
  per frame, so scores 2 and 3 become automatic.

## Score sheet

| Date | Asset | 1 Identity | 2 Scale / anchor | 3 Alpha | 4 Silhouette | 5 Import | 6 Frame data | Total | Verdict | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
