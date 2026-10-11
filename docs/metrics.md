# Metrics

Numbers for the dogfooding notes and the devlog. Definitions are fixed up front so numbers stay
comparable across assets. Update after every batch.

## Summary (running totals)

| Metric | Value | How it's computed |
|---|---|---|
| Starting balance | 100 | `dreamlayer balance --json` (48.0 after the lobby set) |
| Credits spent | 82.3 | sum of [credits-log.md](credits-log.md) (balance 17.7 on 2026-10-11) |
| Paid calls | 39 | rows in credits-log.md |
| Outputs received | 39 | images / sheets returned |
| Assets kept (in the build) | 9 | hero idle/slash/charge/step (+ run for the lobby), knight idle/windup/strike/lunge |
| **Credits per kept asset** | – | credits spent ÷ assets kept |
| **Rejection rate** | – | outputs discarded ÷ outputs received |
| Median scale drift across poses | – | see definition |
| Worst weapon-length drift | −30% (hero slash vs. master, against head size) | see definition |
| Worst anchor jitter (animations) | run sheet: 55 px of 256 as delivered → 0 after snapping; 512 px step sheet: 113 px forward drift → 19 px sway | px at gameplay size |
| Assets with cutout edge problems | 33 of 33 locally keyed: light rim (half-blended edge kept opaque; 52–78% of edge px on sheets / relics) → 0 after `defringe.py` | count, with type |
| Median time prompt → in-engine | – | minutes |

## Definitions

- **Credits per kept asset** — every credit spent (including rejects, cutouts, upscales) ÷ assets
  that ship in the build.
- **Rejection rate** — outputs discarded ÷ outputs received.
- **Scale drift %** — measure a *rigid* feature (head height, or torso width) in px, with the pose
  and the reference at the same canvas size: `(pose − reference) ÷ reference × 100`.
  Don't use the full bounding box — crouches and lunges change it legitimately.
  DreamLayer's own blog reports 15–20% size drift; target ≤ 5%.
- **Weapon-length drift %** — the same formula, applied to the weapon.
- **Anchor jitter (px)** — the largest deviation of the ground-contact point across the frames of
  one animation, after the sheet's own alignment, measured at gameplay display size.
- **Cutout edge problems** — per asset: halo / fringe, holes, surviving ground shadow, or a
  `kept_background_frames` entry from a sprite sheet.
- **Time prompt → in-engine** — wall clock from submitting the request to the sprite showing in the
  Unity scene with correct import settings.

## Per-asset log

| # | Date | Asset | Op | Credits | Outputs | Kept | Scale drift % | Anchor jitter px | Cutout issues | Prompt → engine | Rubric |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2026-10-08 | knight-test-001 (style test, rejected: too generic) | generate | 1 | 1 | 0 | – (first image) | – | ground line + off-white bg; removed locally for free (0.7 s), 201 light edge px left | 23.4 s to image (not imported yet) | – |
| 2 | 2026-10-08 | style-board-001 (4 anime styles in a 2×2, style reference only) | generate | 1 | 1 | 0 | ≈0 between panels (same pose and plates) | – | none (pure white, no ground line) | 24.7 s to image | – |
| 3 | 2026-10-08 | cast-sheet-001 + 002 → hero + knight masters | edit ×2 | 2 | 2 | 2 masters | – (masters) | – | 002: two drawn guide lines + an enclosed arm gap; all removed locally | ~50 s per edit (not imported yet) | – |
| 4 | 2026-10-08 | pilot: hero slash, knight cleave windup (+11 failed requests, free) | edit ×2 | 2 | 2 | 2 | reframed: hero ×1.37, knight ×0.89 (normalized); hero blade −30% vs. head | – | knight: enclosed arm gap removed | ~45 s per pose + ~5 min import |
| 5 | 2026-10-08 | pilot: hero charge, knight strike + lunge (+ ~20 failed, free) | edit ×3 | 3 | 3 | 3 | by helm/face: knight ×0.82–1.15, hero ×1.36; hero blade +6% (charge) | – | strike: big sword/leg gap removed | ~45 s per pose |
| 6 | 2026-10-08 | hero run sprite sheet, `background: keep` (1 failed try, free) | sprite ×1 | 2.9 | 1 (7 frames) | 1 | blade angle/length changes every frame | **55 px baseline spread → 0 after `--snap-feet`**; 8 px horizontal | transparent 2 px padding ring fooled the key (fixed); 41–75 px fringe | 83 s + ~2 min local |
| 7 | 2026-10-08 | side-view reference + combat-step sheet (custom prompt, 8 frames, keep) | edit + sprite | 4.3 | 2 | 2 | sword angle constant across 8 frames | 0–4 px before snapping; 23 px horizontal sway | none beyond the usual fringe (46–61 px) | 95 s + ~2 min local |
| 8 | 2026-10-09 | smoothness batch: 11 poses + 512 px slash sheet (+23 failed, free) | edit ×11 + sprite | 13.9 | 12 | 12 | outputs 0.82–1.6× the master; **4/11 came back facing left** | slash sheet: 0 px baseline, 76 px horizontal sway | glow smear: 540–860 px fringe | ~45 s per pose, 89 s sheet |
| 9 | 2026-10-09 | combat-step sheet again at 512 px (same prompt, first try) | sprite | 3.3 | 1 (8 frames) | 1 | sword angle 26.5–29.9°; **forward drift 113 px (22% of the cell)** → 19 px sway after `--remove-drift` | 0 px baseline after snapping; pivot was 0.36 units off the poses (fixed in the importer) | blade tip cut off by the cell in 3/8 frames (8 / 29 / 52 px of 194), rebuilt locally | 109 s + ~5 min local |
| 10 | 2026-10-10 | world art: arena + burning edit, lobby, relics sheet, knight phase-2, key art (+1 failed, free) | generate ×3 + edit ×3 | 6 | 6 | 6 (8 sprites) | phase-2 knight 20% small by helm ruler → body ruler; key-art hero aged down | floors in the bottom 11–14% (needed ~20%): stretched 1.84× locally | relics clean; phase-2 knight: glow halos + ash splatter | 23–67 s per image + ~10 min fitting/wiring |
| 11 | 2026-10-10 | Boss 1: knight walk sheet, slam windup + impact, flame windup + swing (1 rejected: two swords) | sprite + edit ×5 | 8.3 | 6 | 5 | walk 3.04x the idle by height and width; slam poses: helm ruler x0.8 | walk: 0 px baseline after snapping, 97 px drift → 15 px sway; no edge clipping (wide canvas) | slam: drawn ground cracks; flames keyed cleanly | 40–82 s per job |
| 12 | 2026-10-10 | lobby set: relaxed idle (sword on back) + relaxed walk generated from it | edit + sprite | 4.3 | 2 | 2 | walk frame 1 = idle, 2.43x by height and width | **walk drift 0.4 px/frame (in place, first time)**; 0 px baseline after snapping | idle: paper-grain specks (75 px) → new despeckle step | 64 s + 72 s |
| 14 | 2026-10-11 | lobby run sheet (custom prompt, 8 frames, 512 px, keep) | sprite | 3.3 | 1 (8 frames) | 1 | frame 1 = the lobby idle (2.44x by height); same size through the run | 1.8 px/frame drift; stance frames on one floor row after snapping, airborne frames 19-34 px up (kept) | none beyond the usual (0-2 px fringe) | 93 s + ~20 min local (cleanup, timing, wiring, bot) | – |
| 13 | 2026-10-11 | Boss 2: King reference (4 tries) + 7 poses + throne room calm / frozen (+2 failed, free) | edit ×12 + generate ×1 | 13 | 13 | 10 | **poses vs. the idle: +0 / 3 / 6 / 10 / 12 / 14 / 22% (median +10%)** on the same canvas; fixed with per-pose rulers after a playtest caught it | feet on the bottom edge after normalizing | pale blade highlights taken for background (`--gap-core`); a drawn floor shadow (`--floor-shadow`) | ~45 s per edit |
| 15 | 2026-10-11 | Final boss: 2 reference options + 9 poses + 1 retry, arena calm / torn | edit ×14 | 14 | 14 | 12 | **poses vs. the reference: 0.85x to 1.22x (median ~1.1x)**, measured before import this time; the tentacle arm shrank in 2 poses, doubled in 1 | hip anchor set per pose (the claw arm pulls the automatic one sideways) | drawn floor shadows (2) and a red effect burst (1) erased locally; paper-grain backgrounds in 3 (white patches through tentacle loops → wider gap tolerance) | 54-93 s per edit (retry 184 s) + ~1.5 h measuring, cleanup, wiring, bots | 9/10 |
| 16 | 2026-10-11 | lobby run v2 (custom prompt, 9 frames, 512 px, keep): arms swinging | sprite | 3.8 | 1 (9 frames) | 1 | frame 1 = the idle (308 px, as v1) | 0.5 px/frame drift; feet within 3 px after snapping | none beyond the usual (0-2 px fringe) | 77 s + ~15 min local (cleanup, gym, wiring) | – |
| 17 | 2026-10-11 | ending illustration: edit of a composite reference (knight cut-out in the final arena) | edit | 1 | 1 | 1 | knight kept at his pasted size | – | none (full-frame illustration) | 95 s + ~30 min wiring (fade, push-in, subtitles) | – |
| 18 | 2026-10-11 | boss walks: the King and the Unknown One (custom prompt, 8 frames, 512 px, keep) | sprite ×2 | 6.6 | 2 (16 frames) | 2 | **zoom across the cycle: King +22%, Unknown One +17%** → normalized to frame 1; Flipbook 2.95x / 3.52x the idle | 9.9 / 3.4 px/frame drift removed; feet within 2 px; Unknown One pivot 0.37 units off (claw arm) → atlas pivot_x | King: none beyond the usual; Unknown One: white slivers in tentacle loops (≤67 px/frame) | 78 s / 65 s + ~1 h local (cleanup, scale tool, pivot, foot lock, wiring, captures, bots) | – |
| 19 | 2026-10-11 | README art: repo header (edit of a composite) + hero idle sheet (idle preset, 8 frames, 512 px, keep) | edit + sprite | 4.3 | 2 | 2 | header: all four characters held; idle: 6% breathing, no zoom | idle: `ground_anchor`, feet 0 px off after the first pass; **a second sword appeared on his back in 5 of 8 frames** | idle: faint grey smudge by the face (kept faint) | 97 s / 94 s + ~30 min local (title, cleanup, GIF) | – |

## Animation frame data

At 60 fps, one game frame ≈ 16.7 ms. Convert `frame_durations_ms` to game frames to read
startup / active / recovery.

| Animation | Frames | frame_durations_ms | Startup (ms / f) | Active (ms / f) | Recovery (ms / f) | Hit frame readable? | Max anchor jitter px | Notes |
|---|---|---|---|---|---|---|---|---|
