# Image-API best practices for this project's game assets

How we make game assets with DreamLayer (2026-10-08). Built from two research passes:
DreamLayer's own material (docs, all three blog posts with real credit counts and rejected frames,
the official agent-plugin skills and cleanup scripts) and industry practice (Google's image-model
prompting guide, Ludo, Scenario/Layer, Unity docs, practitioner write-ups). Sources at the end.
Evidence note: the drift and reject numbers are DreamLayer's own (self-reported, but measured on
the same service we use). No independent benchmark exists.

---

## The rules (TL;DR)

1. **Get the master reference right before spending anywhere else.** Everything inherits from it.
   Budget several attempts; DreamLayer's own rider reference took 5.
2. **One identity clause and one style clause per character, pasted word for word** into every
   prompt. Changing the wording drifts the result, and the drift is the prompt's fault, not the model's.
3. **3–5 identity anchors, stated first** (helm, crest, tabard + emblem, sword). Anything the model
   invents that we keep (e.g. the far side of the armor) gets written into every later prompt in
   identical words.
4. **Chain references:** master → one edit per form (it becomes that form's reference) → that form's
   poses. When a family of poses keeps failing, **change the reference (nearest approved pose), not the prompt.**
5. **Change one thing per request.** Never stack unreviewed edits; go back to the approved image.
6. **Always: full body, side view in profile facing right, whole figure + weapon in frame with
   margin, legs and feet described, plain flat white background, no ground / shadow / scenery / text.**
7. **Never ask for "transparent background"**: the model paints a fake checkerboard. Transparency
   comes from cleanup (local) or the paid `cutout`.
8. **Review gates:** one generation at a time for references; a **3-pose pilot** per form before the
   full set; a **contact-sheet review before paying for any cutout**.
9. **Normalize offline:** one shared scale per character (anchor on head/torso, not the bounding box,
   because swords and lunges change it), feet on one baseline, bottom/feet pivot.
10. **Keep glows, auras, smears, tentacle wisps, shadows and slash effects OUT of the sprites:** build
    them in Unity (particles, additive sprites, blob shadow, 2D lights).
11. **Use the sprite-sheet op for the hero attack** (cheaper and more consistent than frame by frame;
    weapon length held). Boss poses stay pose-to-pose + tweens.
12. **Log everything** (prompt, operation, reference, execution_id, idempotency key, credits, verdict)
    and **recover with `status`/`download`, never resubmit.**
13. **No artist or franchise names in prompts.** Describe visual traits instead (it also helps Originality).

---

## 1. Pipeline and review gates

| Stage | Operation | Gate before moving on | Typical cost |
|---|---|---|---|
| 1. Master reference (knight) | `generate` (3:4) | Shawn approves one image; it becomes canon | 1 per attempt, plan 3–5 |
| 2. Form references (king, god-body) | `edit` of the master ("same character, now …") | Identity anchors still read? | 1–3 each |
| 3. Pilot poses | `edit` of the form reference, 3 poses | Compare identity, scale, weapon length, style | 3 per form |
| 4. Full pose set | `edit`, one pose per request | Contact-sheet review (all poses side by side) | ~1.3–1.5 per kept pose |
| 5. Cleanup | **local** key-out (free); paid `cutout` only for soft-edged hero assets | Check edges over light, dark and saturated colors | 0 (or 1) |
| 6. Normalize + import | local script + Unity importer | In-game check at 960×540: no halo, no foot jitter | 0 |
| Hero attack animation | `sprite` op, 7 frames, `once`, 256 px | Review `preview.gif` at game speed, check `kept_background_frames` | ~5.8 |
| Arenas / phase-2 arenas | `generate` (16:9) then `edit` ("same scene, now …") | Grayscale check: characters must stand out | 1–3 each |
| Relics, UI pieces | `generate` or `edit` from an approved asset as style reference | Matches the style clause? | 1 each |

- Run jobs **one at a time** (2-job limit, and we review each). Save the idempotency key **before**
  submitting. A dropped connection = `status` / `download` with the execution_id, never a resubmit.
- DreamLayer's frame-by-frame experiments rejected **13–25%** of generations, at **~2.3 credits per
  kept frame**. Plan on ~1 in 5 failing.

## 2. Prompt templates

Order matters: **operation → keep-clause → identity anchors → pose → framing → style → lighting → background.**

**Style clause (locked 2026-10-08; paste verbatim)**: the style board's top-left panel
(`ArtRaw/style/style-anime-cel-ref.png`), chosen by Shawn.
> Modern high-fantasy TV anime cel style: crisp dark line art with slight weight variation (thicker
> outer contour, thinner interior lines), two-tone cel shading with sharp-edged shadow shapes, one small
> hard highlight on metal, saturated but controlled colors in clean flat fills, consistent light from
> the upper left.

*(Replaced: "Bold-outline cel-shaded illustration: flat colors, thick even outlines, … no gradients,
no texture, limited palette". It came out as generic flat vector art; see dogfooding entry 02.)*

**Background clause (locked)**
> Plain flat solid white background with nothing else in it. No ground, no floor shadow, no scenery,
> no text, no watermark.

**Master reference (knight): `generate`, 3:4**
> Full-body character design of a young knight. [IDENTITY CLAUSE]. Neutral ready stance, side view in
> profile facing right, both feet flat on one line, longsword held low and forward in the near hand.
> Whole body and sword inside the frame with space above the crest and below the feet. [STYLE CLAUSE]
> [BACKGROUND CLAUSE]

**Identity clause (knight; draft until the master is approved, then frozen)**
> Tall rounded great helm with a narrow T-shaped visor and a tall solid red fin-shaped crest; silver
> plate armor built from simple shapes with plate gauntlets; a red tabard with a white sun emblem on
> the chest; a straight steel longsword.

**Pose edit: `edit`, reference = the form's approved image**
> Keep this exact same character from the input image: [IDENTITY CLAUSE]. Same style, same scale, same
> plain flat white background. Side view in profile facing right. Pose: [phase label, e.g. WINDUP of an
> overhead cleave] — [feet position], [knees], [torso lean ~angle], [arms], [sword angle and which hand].
> Whole body and sword inside the frame. [BACKGROUND CLAUSE]

Pose lines read like animation notes: phase label (anticipation / windup / impact / recovery), each
foot, torso lean in degrees, arms, weapon angle. **Always describe the legs.** Unstated legs turned
DreamLayer's wave into a stride. **Restate the side view** on any pose where the body twists.

**Form edit (king, god-body): `edit`, reference = master**
> Keep this exact same character from the input image: [IDENTITY CLAUSE]. Change only: [the one form
> change, e.g. "he is now a king: a gold crown fitted over the same helm, a long red royal cloak over
> the same tabard"]. Same pose, same framing, same canvas, same style. [BACKGROUND CLAUSE]

**Phase-2 / "corrupted" edit: `edit`**
> Keep everything in the input image exactly the same, including composition, framing and canvas.
> Change only: [one change, e.g. "cracks of glowing green spread across the armor"].

Prefer **in-engine phase-2 changes** (tint / emission shader, 2D lights, particles) for the poses, and
spend credits on one or two showcase edits (idle + arena) for the before/after.

**Arena: `generate`, 16:9**
> Side-view 2D game arena background, [place], built around a flat open floor across the lower third
> for the fight, [mood]. Painterly cel-shaded background art with softer, lower-contrast values than the
> characters, no characters, no text.

## 3. Consistency: what to check (the drift checklist)

Compare every new image to its approved reference, at the same head height:

| Check | Reject if |
|---|---|
| Identity anchors (helm shape, crest, tabard + emblem, sword) | any anchor missing or changed |
| Scale (head / torso height after normalizing) | drift > ~10% (DreamLayer saw up to 15–20%) |
| Weapon length | > ~10% different (DreamLayer saw up to ⅓ frame by frame) |
| View | not in profile facing right (back to camera, three-quarter) |
| Asymmetric details | swapped sides |
| Palette (no color correction) | a new color, or an anchor color shifted |
| Hands | extra/merged fingers visible at gameplay size (gauntlets and grip poses help) |
| Invented details | a new detail we didn't approve; if we keep it, add it to the identity clause |

## 4. Background removal and cleanup (local, free)

Generate on **plain flat white** (DreamLayer's tested default). Our thick dark outlines make a natural
matte edge. Cleanup, following DreamLayer's own `clean_cutout.py`:
1. **Flood-fill the background from the image border only.** White *inside* the outline (the sun
   emblem, highlights, eyes) is untouched.
2. Peel thin white halo pixels from the edge inward; the dark outline stops the peel.
3. Keep the largest blob (drops specks).
4. Remove flat, wide grey slabs touching the bottom (a surviving floor shadow).
5. Erode ~1–2 px at the edge, then inspect over light, dark and saturated backgrounds.

Use the paid `cutout` only for soft-edged hero assets. AI cutouts keep partial alpha (hair, smoke)
and still leave shadows and white edges, so clean those too.

## 5. Scale, pivots and Unity import

- **Normalize before import:** crop to alpha bounds, anchor on the head/torso band (DreamLayer's
  assembler ignores thin columns like a raised blade), apply one shared scale per character, feet on a
  common baseline. Size attack cells around the **longest reach**.
- **Pre-size sources to the final size** (characters ≤ 1024, backgrounds ≤ 2048) so Unity doesn't
  resample, and keep **width/height multiples of 4**. Otherwise WebGL's DXT compression silently falls
  back to uncompressed RGBA. Or pack each character/form into a **Sprite Atlas**.

**Unity import checklist (AI sprites, URP 2D, WebGL)**
- [ ] Sprite (2D and UI); Single for poses, Multiple for sheets; **pivot at the feet**, the same for every pose
- [ ] One Pixels Per Unit for the whole project
- [ ] Alpha Is Transparency on; mipmaps off; Read/Write off; Bilinear filter (illustration, not pixel art); Clamp
- [ ] Max Size: characters 1024, backgrounds 2048, UI 1024
- [ ] Width and height multiples of 4, or packed in an atlas; the inspector shows DXT/BC, not RGBA32
- [ ] One Sprite Atlas per character/form, padding ≥ 4
- [ ] Sheets: slice from `atlas.json` rects, shared pivot, clip timing from **`frame_durations_ms`**, warn on `kept_background_frames`
- [ ] Sprite-Lit-Default material, so 2D lights unify everything (baked lighting stays flat)
- [ ] Raw and rejected images live **outside `Assets/`**; each kept PNG has a `.dreamlayer.json` sidecar
- [ ] In-game check at 960×540: no halos, feet don't jitter, hit frames readable at gameplay zoom
- [ ] After the WebGL build: read the size report for uncompressed or oversized textures

These are the AssetPostprocessor's job (CLAUDE.md §7): per-folder defaults, plus a warning on sizes that aren't multiples of 4.

## 6. Animation

- **Boss poses:** a few strong key poses (idle, windups, actives, recovery, stagger, defeat) + tweens,
  squash/stretch, holds and hit-stop. Generated in-betweens flicker and morph when played together.
- **A windup must change the silhouette**, not just flash a color (our glint is the *when*; the pose is the *what*).
- **Hero attack via the `sprite` op:** `once` mode, 7 frames, 256 px (frame size is free), motion in
  `options.animation_prompt`, cell sized for the longest reach. The fastest frames get a blade smear:
  treat it as a built-in swoosh or use the neighbors as the hit frame. Then map `frame_durations_ms` to
  startup / active / recovery and check foot/anchor jitter and weapon length.
- **flipX for facing:** design asymmetric items (sword hand, emblem) so a mirrored sprite still reads.

## 7. Budget (100 credits)

- Spend generously on master references, cheaply on variations. Reuse in-engine instead of
  generating: flipX, tints, a separate weapon sprite, particles, 2D lights.
- Frame by frame costs ~2 credits per frame with cutouts; the `sprite` op is 5.8 for 7 frames with
  cutout included.
- Hold back **~20–25 credits** for rejects.

## 8. Provenance, disclosure, credit

- Our own log is the only durable provenance: DreamLayer strips metadata from uploads, and Unity
  re-encodes textures. Use `credits-log.md`, the `.dreamlayer.json` sidecars and the dogfooding notes.
- **itch.io:** fill in the generative-AI disclosure (tags graphics as AI-generated).
- **Jam rule:** explain how DreamLayer was used and which assets came from it. A reference-to-final /
  before-after strip is "ideal" per the jam page. Mirror DreamLayer's own blog format: real outputs,
  exact prompts, rejected frames, credit counts.
- Document the human work too (selection, compositing, animation, cleanup).

## 9. Failure modes → fixes

| Failure | Fix |
|---|---|
| Identity drift between poses | Verbatim identity clause; chain from approved images; regenerate only the miss |
| Unseen details re-invented each time | Approve once, then name them in every prompt in identical words |
| Shows its back / three-quarter view | Restate "side view in profile facing right" |
| Far pose collapses into a default (stride, gallop) | Use the nearest approved pose as the reference; fewer, stronger poses |
| Bad hands | Gauntlets, grip/fist poses, a targeted edit; judge at gameplay scale |
| Scale drift 15–20% | Normalize offline by head/torso height + feet baseline |
| Weapon length drift | Measure it; reject > ~10%; or a separate weapon sprite |
| Floor shadow / grey slab survives | Prompt against it; remove by the bottom-slab rule; blob shadow in Unity |
| White halo | Border flood-fill + halo peel + 1–2 px erode; Alpha Is Transparency |
| Fake checkerboard | Never ask for transparency |
| Text / watermark artifacts | "no text, no watermark"; crop |
| Inconsistent lighting | Locked lighting phrase; one cel shadow tone; mood from 2D lights |
| Sword hand swaps when mirrored | Design for flipX |

---

## Sources

- DreamLayer blog: [sprite sheet from one reference](https://dreamlayer.io/blog/sprite-sheet-generator-from-one-reference) ·
  [sprite animation private beta](https://dreamlayer.io/blog/sprite-animation-generator-private-beta) ·
  [character turnaround](https://dreamlayer.io/blog/character-turnaround-sheet-from-one-reference) ·
  [cleanup scripts README](https://dreamlayer.io/marketing/blog/sprites/tools/README.md)
- DreamLayer site and docs: [/gaming](https://dreamlayer.io/gaming) · [/oc-maker](https://dreamlayer.io/oc-maker) ·
  [docs](https://docs.dreamlayer.io) (/agent-workflows, /agent-api/sprite-sheet, /agent-api/image-to-image, /cli/automation)
- [DreamLayer agent plugin](https://github.com/TheDesignFounder/dreamlayer-agent-plugin) (skills, workflow contract, Unity test plan)
- [Google: prompting Gemini image models](https://developers.googleblog.com/en/how-to-prompt-gemini-2-5-flash-image-generation-for-the-best-results/) ·
  [checkerboard instead of transparency](https://discuss.ai.google.dev/t/how-can-i-make-the-background-transparent/111698)
- [Ludo: game asset generation](https://ludo.ai/docs/game-asset-generation) ·
  [Layer: IP consistency guide](https://www.layer.ai/blog/ip-consistency-ai-generation-guide-game-studios) ·
  [Unity AI sprite generator](https://unity.com/blog/unity-ai-sprite-generator)
- Unity: [TextureImporter](https://docs.unity3d.com/Manual/class-TextureImporter-type-specific.html) ·
  [alphaIsTransparency](https://docs.unity3d.com/ScriptReference/TextureImporter-alphaIsTransparency.html) ·
  [WebGL texture compression](https://docs.unity3d.com/Manual/webgl-texture-compression.html) ·
  [Sprite Atlas](https://docs.unity3d.com/Manual/class-SpriteAtlas.html) ·
  [OnPreprocessTexture](https://docs.unity3d.com/ScriptReference/AssetPostprocessor.OnPreprocessTexture.html)
- Readability and animation: [visual confusion](https://www.gamedeveloper.com/design/how-to-reduce-visual-confusion-in-your-game) ·
  [enemy telegraphing](https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing) · [game animation](https://www.gameanim.com/?p=115)
- Disclosure: [jam page](https://itch.io/jam/dreamlayer-jam-1) · [itch.io AI disclosure](https://itch.io/docs/creators/quality-guidelines#ai-disclosure) ·
  [US Copyright Office AI report](https://www.copyright.gov/ai/)
