# Asset list

Every visual asset the game needs, where it comes from, and what it costs. Draft (2026-10-08),
pending the style pick and the hero concept. Update the status column as assets are kept.

- **Op:** `gen` = generate (text only) · `edit` = one reference image + words · `sprite` = sprite sheet ·
  `local` = made from existing images for free (crop, recolor, flip) · `unity` = built in Unity.
- **Reference** is the approved image the request points at. Everything chains back to the two masters.
- Credits are estimates before rejects; plan on ~1 in 5 failing.

## Masters (lock first; everything else inherits from these)

| Asset | Op | Reference | Credits | Status |
|---|---|---|---|---|
| Style pick | (board done) | – | 0 | **done: top-left anime cel** (`ArtRaw/style/style-anime-cel-ref.png`) |
| **Cast sheet**: hero + knight side by side, same style, true relative size | edit ×2 | local composite of the knight | 2 (spent) | done: `ArtRaw/cast/cast-sheet-002.png` |
| Hero master (cropped from the cast sheet) | local | cast sheet | 0 | **approved** (`ArtRaw/cast/hero-master.cut.png`) |
| Knight master (cropped from the cast sheet) | local | cast sheet | 0 | cut out: `ArtRaw/cast/knight-master.cut.png` |

## Hero (seen every second of play; gets the real animation)

| Asset | Op | Reference | Credits | Status |
|---|---|---|---|---|
| Run cycle | sprite (run preset, loop, 256 px) | hero master | 2.9–5.8 | |
| Charged heavy attack | sprite (once, 7 f, 256 px) | hero master | 2.9–5.8 | |
| Poses: idle, light slash A, light slash B, dash, parry, hurt, defeated | edit ×7 | hero master | ~7 | |

Sprite costs are 2.9 if `background: keep` + free local cutout works (one test sheet decides), else 5.8.

## Villain: one character, three forms

| Asset | Op | Reference | Credits | Status |
|---|---|---|---|---|
| **Knight** (Boss 1) poses: idle, cleave windup, cleave strike, lunge windup, lunge, combo strike, slam windup (phase 2), flame-wave swing (phase 2, blade on fire), staggered, defeated | edit ×~9 | knight master | ~9 | |
| Knight phase-2 showcase ("same knight, armor cracking") | edit | knight idle | 1 | **done** (1): `knight_phase2.png`, shown during the transformation |
| **King** form reference | edit | knight master | 1–2 | needs story beats |
| King poses (attacks depend on the Boss 2 design) | edit ×~7 | king reference | ~7 | |
| King phase-2 showcase | edit | king idle | 1 | |
| **Unknown One's body** form reference | edit | king reference | 1–2 | needs story beats |
| Unknown One poses | edit ×~7 | form reference | ~7 | |
| Unknown One phase-2 showcase | edit | form idle | 1 | |
| Boss portraits / name cards | local (crops of poses) | – | 0 | |

Phase-2 changes on the other poses are done in Unity (tint, emission, particles), not regenerated.

## World

| Asset | Op | Reference | Credits | Status |
|---|---|---|---|---|
| Lobby background | gen | style clause | 1–2 | **done** (1): `Backgrounds/lobby.png` |
| Relics: sword, crown, eye (one sheet, cropped) | gen | style clause | 1 | **done** (1): `Props/relic_*.png` |
| Arena 1: the knight's place | gen | style clause | 1 | **done** (1): `Backgrounds/arena1.png` (+ burning phase-2 edit, 1) |
| Arena 2: the king's throne room | edit ("same art style and camera, new place") | arena 1 | 1 | |
| Arena 3: the Unknown One's realm | edit | arena 1 | 1 | |
| Phase-2 edits of the 3 arenas ("same scene, now cracked / burning / corrupted") | edit ×3 | each arena | 3 | |

## Presentation

| Asset | Op | Reference | Credits | Status |
|---|---|---|---|---|
| Title logo / key art | gen or edit | cast sheet | 1–2 | key art **done** (1): `ArtRaw/world/keyart.png`; logo needs the game's name |
| Cutscene key panels (~2 per boss intro) | edit ×~6 | form references | ~6 | |
| UI frame (health bars, name card plate) | gen | style clause | 1 | optional |
| itch.io cover image (630×500) | local (composite) | – | 0 | **done**: `docs/media/itch-cover-630x500.png` |

## Not DreamLayer (free)

| Asset | Source |
|---|---|
| Slash arcs, sparks, dust, hit flashes, glint / red glow cues, blob shadows | Unity (particles, additive sprites, 2D lights) |
| Health bars, damage numbers, callouts | Unity UI |
| SFX and music | free packs, credited in `docs/credits.md` |
| Font | one free font, credited |

## Totals

| Group | Credits |
|---|---|
| Masters | ~1–3 |
| Hero | ~13–19 |
| Villain (3 forms) | ~33–35 |
| World | ~8–9 |
| Presentation | ~8–9 |
| **Total** | **~63–75 of 98, leaving ~25–35 for rejects** |
