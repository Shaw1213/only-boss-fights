# Dogfooding DreamLayer — experiment notes

Working log for the devlog **"Dogfooding DreamLayer in a real Unity combat pipeline"**.
One entry per asset or experiment, in DreamLayer's own blog format: what held, what drifted,
what it cost. **Every friction point ends with a proposed fix or feature.**

- The whole-project log (design, builds, playtests) is [devlog.md](devlog.md).
- Paid calls are logged line by line in [credits-log.md](credits-log.md).
- Numbers roll up into [metrics.md](metrics.md).
- Assets are scored with [game-readiness-rubric.md](game-readiness-rubric.md).
- Images worth showing in the devlog (rejected frames, before/after) go in `docs/media/`.

---

## Devlog draft (update as we go — assembled on Sunday, not written from scratch)

**Working title:** Dogfooding DreamLayer in a real Unity combat pipeline

1. **Setup** — the game (one-boss duel, WebGL), the pipeline (Claude Code → DreamLayer MCP/CLI → Unity importer), the budget (~100 credits).
2. **Locking the references** — hero / boss / arena: attempts, what made the keeper, cost.
3. **Poses and consistency** — what held, what drifted, with drift numbers.
4. **A combat animation as frame data** — sprite-sheet attack mapped to startup / active / recovery; anchor stability; hit-frame readability.
5. **Into Unity** — what the `unity-sprite-import` skill got right and wrong in a real Unity 6 editor; what the importer fixes (PR link).
6. **The numbers** — credits per kept asset, rejection rate, prompt-to-engine time.
7. **What I'd build next** — top 3 product suggestions, ranked by impact on game devs.

**Lines worth keeping for the post:**
-

---

## Entries

<!-- Copy the template below for each asset or experiment. Newest at the bottom. -->

### 01 — Reading the docs and the sample sprite ZIP (0 credits)
- **Date / asset:** 2026-10-07 · all of docs.dreamlayer.io, the `dreamlayer-agent-plugin` repo, `examples/source/walk.zip` (7 frames, 128 px, walk preset)
- **Goal:** understand the API and the Unity import path before spending any credits.
- **What held:**
  - Reliability is designed well: idempotency keys, recovery with `status` / `download`, credits held at start and charged only on delivery, failed runs free.
  - Sprite pricing is a published formula and depends only on frame count; frame size is free.
  - Atlas rects reconstruct each frame PNG exactly.
- **What drifted (docs vs. reality):**
  - `llms.txt` still says beta.3; npm has CLI `0.4.0-beta.4` and MCP `0.4.0-beta.5`.
  - The README bundled in MCP beta.5 says one credit = $0.17; the pricing page says $0.25.
    Confirmed live: `dreamlayer balance --json` (CLI 0.4.0-beta.4) returns `"credit_usd": "0.17"`,
    because the CLI doesn't ask for `pricing=current`. → Fix: have the CLI and MCP request current
    pricing by default.
  - Some pages describe a submit-time 402 for insufficient credits; the newer contract reports it as a failed execution.
  - `cutout_unusable_many` is documented but missing from the OpenAPI reason enum.
  - `llms.txt` "Product truth" and `/index` list the V1 operations without `sprite_sheet`.
  - Sample `atlas.json`: `padding: 1`, but the cells sit edge to edge (x = 0, 128, 256); `duration_ms` is 2833 while `frame_durations_ms` sums to 2830.
- **`unity-sprite-import` skill** (their own test plan: "no Unity editor run has been completed"), checked against the sample and the script source:
  - Loop is decided from `animation_mode` only and ignores `sampling: "cycle"`, so a looping custom animation plays once.
  - Pixels-per-unit is hard-coded to 128, so the default 512 px frames are 4 units tall.
  - `alignment: "ground_anchor"` is ignored; the pivot stays centered, so feet aren't anchored.
  - Frames are Uncompressed and placed in `Resources/`, which bloats a WebGL build (≈1 MB per 512² frame).
  - One hard-coded `DreamLayerDemo/` folder, so a second animation collides with the first.
  - `atlas.json` `frames` is an object keyed by frame name, which Unity's built-in `JsonUtility` can't read.
- **Credits spent vs. kept:** 0 → n/a
- **Friction → proposed fix / feature:**
  - Docs and package READMEs drift apart → generate `llms.txt`, package READMEs and OpenAPI enums from one source, and check them in CI.
  - Atlas fields disagree with the actual layout → make `padding` and `duration_ms` match the data, or document what they mean.
  - Keyed `frames` object → also emit an ordered `frames` array so engines' built-in JSON parsers work; keep the hash for TexturePacker compatibility.
  - Unity import is an unvalidated Python-generated demo → ship a validated Unity package that sets the pivot from `alignment`, pixels-per-unit from `cell`, looping from `sampling`, and compressed, non-`Resources` textures. *(This is the PR opportunity: it can be built and tested against the sample ZIP with zero credits.)*

### 02 — First generation: knight style test (1 credit)
- **Date / asset:** 2026-10-08 · `ArtRaw/knight/knight-test-001.png` (a test; the art style isn't decided yet)
- **Goal:** see how DreamLayer interprets our draft art direction and knight design before
  committing to a master reference; check whether free local background removal works.
- **Reference:** none (text only)
- **Operation:** `generate`, aspect 3:4 requested
- **Exact prompt:** `ArtRaw/knight/knight-test-001.prompt.txt` (identity anchors: T-visor helm, red
  fin-shaped crest, silver plate + gauntlets, red tabard with a white sun, longsword; style: bold-outline
  cel-shaded, flat colors, thick even outlines, one shadow tone, no gradients; plain flat white
  background, no ground / shadow / text)
- **execution_id / idempotency key:** `c91503de-9a38-40f0-940d-d9ffbe54f241` / `dlj-knight-test-001`
- **What held:**
  - Every identity anchor appeared.
  - Side view in profile facing right; full body with generous margins.
  - Clean, closed dark outlines and a readable silhouette.
  - Sword held low and forward in the near hand, as asked.
  - Result in 23.4 s.
- **What drifted:**
  - **Asked for 3:4, got 2048×2048 square.** The CLI does send `aspect_ratio: "3:4"` (cli.js),
    and the server's own status reports 2048×2048. So the server or the routed model ignored it.
  - **A thin ground line under the feet,** despite "no ground, no floor shadow".
  - **Off-white background (247, 247, 243),** not pure white.
  - **The style landed nearer "flat vector" than "bold cel-shaded":** the outlines are medium, not
    thick, and the armor has soft gradient highlights despite "no gradients".
  - The "T-shaped visor" reads as a slit in profile. That's expected from the side; not a model fault.
- **Rejected attempts:** 1. Shawn: "definitely not" the style he's looking for. Too clean and
  generic: the prompt's "simple shapes, flat colors, limited palette, no texture" wording pulls these
  models toward flat corporate vector art. A distinctive look needs specific style traits (line
  quality, shadow shapes, rendering, palette mood), not just "cel-shaded".
- **Credits spent vs. kept:** 1 → 0 (kept only as a reference for what not to do)
- **Rubric score:** not scored (the rubric is on hold until the art style is decided)
- **Workaround used:** free local cleanup (`Tools/clean_cutout.py`, 0.7 s):
  - border-connected flood fill, so the white sun emblem inside the outlines was kept;
  - halo peel;
  - ground-line removal;
  - crop to 728×1460, padded to multiples of 4 for WebGL compression.

  It looks clean over dark gray and magenta, so a paid cutout (1 credit) isn't needed for this style.
- **Friction → proposed fix / feature:**
  - Aspect ratio ignored → honor `aspect_ratio` for every routed model, or **reject / warn** when the
    routed model can't do it. Today the request silently returns square.
  - Output size not in the CLI's `generate` JSON (only in `status`) → return `width` / `height` in
    the generate result so pipelines can check without a second call.
  - "No ground" not respected → a **"game asset / sprite-ready" mode**: pure flat background, no ground
    line, no cast shadow, subject centered with margin. It would make free local keying reliable
    and remove the need for paid cutouts on outlined styles.

### 03 — Validating `unity-sprite-import` in a real Unity editor, then fixing it natively (0 credits)
- **Date / asset:** 2026-10-08 · their sample `examples/source/walk.zip` (7 frames, 128 px, walk)
- **Goal:** the plugin's own test plan says no Unity run had been done. Run it, record what works,
  and fix what a real game needs.
- **What held:**
  - The full test plan passes in Unity 6000.6.4f1.
  - Their script's output is byte-identical to the committed example.
  - It compiles with no errors or warnings.
  - The menu demo works.
  - Frames advance at 399–411 ms against the manifest's 400/410.
  - It loops, with no console output.
  - Record: [dreamlayer-unity-validation.md](dreamlayer-unity-validation.md).
- **What drifted (for real game use):**
  - Centered pivot: feet 0.5 units below the object.
  - Pixels per unit hard-coded to 128 (512 px frames would be 4 units tall).
  - WebGL textures uncompressed (RGBA32).
  - Everything in `Resources/`.
  - One hard-coded folder.
  - Loop detection ignores `sampling: "cycle"`.
  - `kept_background_frames` ignored.
  - Needs Python plus a manual copy.
  - Their atlas also disagrees with itself (`duration_ms` 2833 vs. per-frame sum 2830).
- **Credits spent vs. kept:** 0
- **Workaround used:** a native Unity editor importer (`Assets/Editor/DreamLayer/`): drop the ZIP in →
  - slices from the atlas rects;
  - sets the pivot at the feet from the alpha baseline;
  - builds a clip keyed on `frame_durations_ms`;
  - loops from `sampling`;
  - compressed textures (DXT5), no `Resources`;
  - warnings, plus a report with 60 fps frame data and anchor jitter (0 px feet, 2 px center on their sample).

  Measured Animator playback: 399–411 ms, looping.
- **Friction → proposed fix / feature:**
  - Unity import is unvalidated and Python-based → contribute the validation record + a native
    importer (issue + PR to `dreamlayer-agent-plugin`).
  - `alignment` is a name only → also export the pivot as numbers (`pivot: {x, y}` normalized),
    so every engine can place the feet without measuring.
  - `frames` as a keyed object → add an ordered `frames` array alongside it for engines' built-in
    JSON parsers.
  - `duration_ms` ≠ sum of `frame_durations_ms` → make them agree, or drop `duration_ms`.

### 04 — Anime style board: one knight, four styles in one image (1 credit)
- **Date / asset:** 2026-10-08 · `ArtRaw/style/style-board-001.png` (a style test, not a final design)
- **Goal:** compare four anime directions side by side for 1 credit instead of 4, so Shawn can pick
  the locked style.
- **Reference:** none (text only)
- **Operation:** `generate`, aspect 1:1
- **Exact prompt:** `ArtRaw/style/style-board-001.prompt.txt`
  - Panels: TV-anime cel (top-left), dark fantasy (top-right), fighting-game sprite (bottom-left),
    painterly key visual (bottom-right).
  - Same knight in each: **open-faced** helm with the face visible, red **fin-shaped** crest, silver
    plate, red tabard with a white sun, longsword. Side view in profile, white background.
- **execution_id / idempotency key:** `6cdcbc29-c694-4ed5-95df-1b387c813daa` / `dlj-style-board-001`
- **What held:**
  - A clean 2×2 grid with gutters. Pure white panel backgrounds, no ground line this time.
  - **Identity was extremely consistent across the four panels:** same pose, proportions, armor
    plates, belt, sword angle and emblem. This is the consistency DreamLayer sells, and here it held
    even with no reference.
  - The tabard, sun emblem and longsword appeared as asked. Result in 24.7 s.
- **What drifted:**
  - **The style instructions per panel were under-applied.** The four panels are nearly the same
    drawing with different rendering:
    - "dark fantasy" became mainly a recolor (dark gray armor, darker red), not heavy black shadow
      shapes;
    - "fighting-game sprite" became a slightly cooler, softer-lined version of top-left;
    - only "painterly" really differs (gradients, metallic rendering).

    The consistency that helps poses works against a style comparison: the model homogenizes the
    panels.
  - **Face ignored:** asked for an open-faced helm with the face visible; got a closed visor in all four.
  - **Crest became a feather plume** (asked: solid fin-shaped crest; plumes cut out worse).
  - **Three-quarter view, not profile,** in all four panels (test 001 did give a profile).
  - Proportions read semi-realistic rather than anime; with the face hidden, little "anime" shows at all.
- **Verdict:** Shawn picked the **top-left panel (TV-anime cel)** as the locked art style. Cropped for free to `ArtRaw/style/style-anime-cel-ref.png`. The knight design itself (closed visor, plume) isn't approved yet.
- **Credits spent vs. kept:** 1 → the locked style reference (0 in-game assets)
- **Workaround used:** none needed; the panels are cropped locally for review.
- **Friction → proposed fix / feature:**
  - Multi-panel sheets blend the per-panel instructions → a **"variations" mode**: N styles of one
    prompt in one job (separate images, one price), so style exploration doesn't depend on
    grid-prompt tricks.
  - Explicit design traits silently replaced (open face → closed visor, fin crest → plume, profile →
    3/4) → **flag which constraints the result doesn't satisfy** (a cheap check pass, or a list in
    the `done` event), so pipelines catch drift before a human does.

### 05 — Cast sheet: adding the hero next to the approved knight (2 credits)
- **Date / asset:** 2026-10-08 · `ArtRaw/cast/cast-sheet-001.png`, `cast-sheet-002.png` → cropped
  `ArtRaw/cast/hero-master.cut.png`, `knight-master.cut.png`
- **Goal:** design the hero in the locked style and lock both masters (and their relative size) in one image.
- **Reference:** a **local composite**, because V1 takes one reference image per request. The style
  board's top-left knight was cut out locally and placed on the right of a 1280×1280 white canvas
  (`ArtRaw/cast/cast-ref-001.png`), with the left side empty for the hero.
- **Operation:** `edit` ×2. 002 was an edit of 001 that changed only the hero's pose.
- **Exact prompts:** `ArtRaw/cast/cast-sheet-001.prompt.txt`, `cast-sheet-002.prompt.txt`
- **execution_id / idempotency key:** `b742f155-…` / `dlj-cast-sheet-001` · `c91cdf98-…` / `dlj-cast-sheet-002`
- **What held:**
  - **The hero matched almost every anchor on the first try:** messy silver-white hair, visible face
    with a determined look, navy high-collar coat, cream shirt, black fingerless gloves, rune bracer,
    a teal rune line glowing on the blade, boots, no cape / scarf / helmet.
  - **Style transfer from the reference was excellent.** The hero's line weight, cel shadows and
    color handling match the knight; they look like one show.
  - **The knight survived both edits.** In 001 he was redrawn at a larger scale with the same design;
    in 002 he is essentially unchanged.
  - **The pose-only edit (002) changed only the pose.** Face, hair, outfit and colors held.
  - ~50 s per edit.
- **What drifted:**
  - **The canvas was reframed.** The knight was 839 px tall on a 1280² canvas; the output is 2048²
    with him ~1500 px tall. "Same size, same position" isn't honored literally, so a composite can set
    up the layout but not exact pixels.
  - **The coat came out longer than asked** (to mid-calf, with flared tails; "ends above the knee" was ignored).
  - **A second rune band on the upper arm** (teal was asked only on the bracer and the blade).
  - **001: the hero's sword crossed in front of the knight's leg,** so neither could be cropped cleanly. → 002.
  - **002: "both feet on the same ground line as the knight" was drawn as two literal gray lines**
    across the whole image, one under each character.
  - The sword changed hands between 001 and 002 (irrelevant: sprites are mirrored in game anyway).
- **Rejected attempts:** 001 kept only as the reference for 002 (design OK, layout not croppable).
- **Credits spent vs. kept:** 2 → 2 masters (hero + knight); 1 credit per kept master.
- **Workaround used:** free local cleanup (`Tools/clean_cutout.py`), extended twice in this session:
  - **enclosed gaps:** background enclosed between an arm and the torso is removed when it exactly
    matches the background and survives a 4-step erosion (white emblems and highlights are kept);
  - **guide lines:** thin gray rows across the image are erased wherever the pixels above and below
    are background, so the character is untouched.

  Hero 764×1712 and knight 1056×1936, clean over dark and magenta.
- **Friction → proposed fix / feature:**
  - Composites get reframed → an **"edit inside this region" mask** (inpaint only the empty side), or a
    "preserve canvas" flag. It would make "add a character next to the approved one" deterministic and
    prevent the overlap that cost a retry.
  - Layout words taken literally ("ground line" → drawn lines) → document it in the prompt guide, or
    have the agent rewrite layout phrases before routing. *(Our fix for the prompt: "feet at the same
    height as the knight's feet", never "ground line".)*
  - One reference image per request → **multi-reference** (style reference + character reference).
    The local composite worked here but adds a manual step.

### 06 — Art pilot: first poses and a sprite sheet into the game (7.9 credits)
- **Date / asset:** 2026-10-08 · `ArtRaw/hero/hero-slash-001.png`, `ArtRaw/knight/knight-cleave-windup-001.png`
  → `Assets/Art/DreamLayer/Characters/{Hero,Knight}/`
- **Goal:** test pose edits from the approved masters before paying for full pose sets; get real art into
  the fight.
- **Reference:** each master, cut out locally and placed on a 1600² white canvas (`ArtRaw/hero/hero-ref.png`,
  `ArtRaw/knight/knight-ref.png`).
- **Operation:** `edit`, one pose per request. Frozen identity clauses in `ArtRaw/*/…-identity.txt`;
  pose lines written like animation notes (phase label, feet, knees, torso angle, arms, blade).
- **What held:**
  - **Identity, both characters:** hair, coat, both rune bands, gloves and the glowing blade line on the
    hero; helm, plume, tabard + sun, belts and ring pommel on the knight. Clean outlines, plain white
    backgrounds, no ground line.
  - **The poses read at gameplay size:** a lunging slash; an overhead windup that changes the silhouette
    (the telegraph rule).
  - ~45 s per pose. Free local cutout worked on both (one enclosed arm gap removed automatically).
- **What drifted:**
  - **Reframing: every output comes back at a different scale.** Hero slash 1.37× the master (eye line
    to chin, 122 vs 89 px); knight windup 0.89× (blade, 865 vs 975 px). Normalized offline before import.
  - **Weapon length: the hero's sword is ~30% shorter in the slash** than in the master, measured against
    his head (teal rune line 277 vs 393 px at art scale). DreamLayer's own blog reports up to ⅓.
  - The knight's windup turns his torso away (the sun emblem moves toward his back), and the plume is smaller.
- **Reliability: 11 of 13 requests failed** with `generation_failed / temporarily_unavailable,
  retryable: true`, all within ~10 s and all free. Two prompts failed 5 times in a row while other
  prompts succeeded in between, so at first it looked prompt-specific. Rewording didn't help, a new
  reference didn't help, and then a knight prompt that had just worked also failed. It was intermittent.
- **Credits spent vs. kept:** 2 → 2 poses (so far)
- **Workaround used:**
  - a background retry loop: a new idempotency key per attempt (a failed execution is terminal), waits
    of 60 s, 120 s, …;
  - `Tools/normalize_pose.py` + `import_poses.py`: a rigid "ruler" per character (hero: eye line to
    chin, because his sword drifts; knight: the blade), hip anchor, feet on the bottom edge.
- **Friction → proposed fix / feature:**
  - Same generic error for outages and (possibly) prompt-specific failures → **distinguish them**:
    `provider_outage` + ETA vs. `content_*` / `input_*`, and expose `retry_after`. A pipeline then knows
    whether to wait, rephrase or change the reference.
  - Every edit reframes the subject → **"keep framing" / fixed-scale edits** for pose sets (same canvas,
    same subject scale as the input), or return the subject's scale/bbox relative to the reference so
    tools can normalize without hand measurement.
  - Weapon length drifts between poses → a **prop-lock** option: mark a region of the reference (the
    sword) as rigid, so its length is held across edits.

- **Update, later the same night (7.9 of 9 credits; balance 88.1):**
  - **Three more poses landed** via the retry loop: hero charge, knight cleave strike, knight lunge.
  - **The knight's ruler had to change.** By blade length the strike came out 1.44× the master, but by
    helm height only 1.15×: in the master his sword points toward the camera (foreshortened). He is
    now measured by helm height (master 235 px; windup 192, strike 270, lunge 265). Lesson: **a rigid
    prop is only a ruler if it lies in the picture plane.**
  - **Hero blade drift by pose (against his head):** slash −30%, charge +6%.
  - **Gameplay scale:** at boss size (1.8× the hero) the knight's level lunge passed over the hero's
    head; the poses assume similar heights. He is now drawn at 1.36×, with a matching hurtbox.
  - **Hero guard/parry: 17 consecutive failures** across three wordings and two references, while
    other poses kept succeeding between attempts. Not imported; the parry state falls back to idle
    plus a cyan tint. *(A prompt-specific failure behind the generic "temporarily unavailable" error.)*
  - **The sprite sheet, half-price route (`background: keep`, 7 frames, 256 px, 2.9 credits):**
    - try 1 failed after 77 s, non-retryable `generation_failed`, free, no detail; try 2 with a
      tighter portrait reference succeeded in 83 s;
    - identity held: hair, coat, glowing blade. **But the sword swings to a different angle and length
      in every frame**, and **frames 1, 2, 5 leap high**: `ground_anchor` left **55 px of feet-baseline
      spread** in 256 px cells (37% of the character's height). In game he hopped;
    - the authored cycle is **2.0 s for 7 frames (≈290 ms per frame)**, slow for a run; it plays 3.3×
      faster in game;
    - the kept background is white inside a **2 px transparent padding ring** per cell, which first
      fooled our border-sampled key. Fixed (sample an opaque band; transparent counts as background).
      Then 89–91% of each frame was removed, 41–75 px of light fringe;
    - `Tools/clean_sprite_zip.py --snap-feet` drops each frame onto the lowest foot → **0 px spread**.
      Our Unity importer then sliced it, set the feet pivot, keyed the clip on `frame_durations_ms` and
      reported the jitter;
    - cells come back ~4.9× smaller than the pose art (hair-to-chin 27 vs 133 px); a `Flipbook.scale`
      multiplier matches them.
  - **Friction → proposed fix / feature (sprites):**
    - `ground_anchor` leaves runs hopping → a **`feet_locked` alignment** (every frame's lowest contact
      point on one baseline) for side-view gameplay, and report the per-frame baseline in atlas.json.
    - The weapon is re-posed in every frame → let the animation prompt **lock the prop's pose**
      ("sword held still, pointing back") or expose a prop-stability option.
    - Run preset timing (≈290 ms per frame) → scale `frame_durations_ms` to the preset's real speed, or
      take a target cycle length.
    - Sprite failures carry no reason (`generation_failed`, not retryable) → say which stage failed
      (pose generation, frame review, cutout) so the caller knows whether to change the reference.

### 07 — Fixing a flailing run: a custom combat-step sheet (4.3 credits)
- **Date / asset:** 2026-10-08 · `ArtRaw/hero/hero-side.png` (reference), `ArtRaw/hero/hero-step-keep.zip`
  → `Assets/Art/DreamLayer/Characters/Hero/hero_step.zip`
- **Problem (Shawn's playtest):** the run preset's arm and legs looked unnatural. Causes:
  - the sword is re-posed in every frame (overhead, behind, straight up, forward), so the arm flails;
  - three of seven frames are split-leg leaps; once snapped to the floor they read as sliding splits;
  - seven frames can't hold two matching steps;
  - the duel game plays the run backwards when retreating, which looks like moonwalking.
- **Fix:**
  1. Stopgap (free): only the calm frames (07 → 04 → 07 → 06), played calmer.
  2. A new reference: an `edit` asking for a full side view in a combat-ready stance (1 credit).
  3. A **custom animation prompt** instead of the run preset: 8 frames, loop, `background: keep` (3.3
     credits). Prompt: "combat step… at least one foot always touches the ground, no jumping… the sword
     stays still, held low in front… the sword arm does not swing…" (`ArtRaw/hero/hero-step.anim.txt`).
- **What held:**
  - **The prompt was obeyed.** The sword keeps one angle in all 8 frames, the torso stays level, there
    are no leaps, and the feet were already grounded (0–4 px spread before our snap).
  - Identity held; first try, 95 s. Looks natural played backwards, which a sprint can't do.
- **What drifted:**
  - "Full side view in profile" was ignored again (three-quarter, though more side-on): third time
    across the project.
  - **Timing: 5,040 ms for 8 frames** (620–660 ms each, sampled from a 5 s source), about 6× too slow
    for gameplay; played 5.6× faster in game.
  - **The atlas says `alignment: "source_canvas"`** for custom animations, so our importer (keyed on
    `ground_anchor`) gave it a centered pivot, and the hero would have stood half under the floor.
  - 23 px horizontal sway of the body center across frames (natural for stepping).
- **Credits spent vs. kept:** 4.3 → 1 reference + 1 movement cycle (8 frames)
- **Workaround used:** importer fix: in feet-pivot folders (`Characters/`, `Props/`) it measures the
  feet whatever the alignment says, and notes it in the report. Pivot (0.5, 0.145), 0 px baseline spread.
- **Friction → proposed fix / feature:**
  - The presets know nothing about props → **presets with a prop slot** ("run, sword held low"), or
    document that custom prompts should be used for armed characters.
  - Custom animations are sampled over ~5 s → **a target cycle length** (or report the intended
    playback speed) so the frame timing is usable as-is.
  - `source_canvas` vs `ground_anchor` changes what a pivot means → **always export a numeric feet
    pivot per frame**, whatever the alignment mode.

### 08 — The smoothness batch: 11 poses + a 512 px slash sheet (13.9 credits)
- **Date / asset:** 2026-10-09 · `ArtRaw/hero/hero-{windup,slash2,block,dash,hurt,dead}.png`,
  `ArtRaw/knight/knight-{lunge-windup-r2,recovery,hurt-r2,staggered,defeated}.png`, `ArtRaw/hero/hero-slash-keep.zip`
- **Goal:** fill the gaps the Animation Gym showed:
  - idle snapping into attacks (no windups);
  - one slash reused for every combo hit;
  - the knight standing idle for 450 ms before his lunge;
  - the punish window with no pose of its own;
  - tint-only hit, stagger and death reactions.
- **What held:**
  - Identity on all 11 poses. The hero's **guard pose finally worked** with new wording ("sword raised
    horizontally to block"), after 17 failures in the pilot.
  - Poses that read as frame data: a windup with the blade drawn back, a rising second slash, a coiled
    lunge windup, a "sword in the ground, winded" recovery. The last one tells the player when to punish.
  - **The 512 px sheet is sharp** (vs. the 256 px walk), at the same price.
- **What drifted:**
  - **Facing: 4 of 11 poses came back facing left** despite "three-quarter view, body facing right":
    hero defeated, knight recovery, staggered, defeated (all kneeling or slumped). Mirrored on import.
  - **Scale, and how to measure it:** the outputs ranged 0.82–1.6× the master. Bowed helms look ~45%
    bigger from above (perspective), so the knight's slumped poses were measured by body height
    against the standing idle instead. Upright helms and the hero's eye-line-to-chin stayed reliable.
  - An invented scabbard on the lunge windup.
  - **The slash sheet turns toward the camera on frame 4,** and frames 5–7 repeat frame 4, because the
    custom animation is sampled at 0, 0.83, 1.67 … 5 s, so the tail is a hold. The built-in glowing
    smear leaves 540–860 px of light fringe after keying.
- **Reliability:**
  - 23 failed attempts across the batch (all free), including two poses with 5 straight failures.
  - Retrying those under the same idempotency keys returned **`CONFLICT` / "The request conflicts with
    the current state"**: a failed key can't be reused, and the error doesn't say so. Fresh keys
    worked first try.
- **Credits spent vs. kept:** 13.9 → 11 poses + 1 sheet (4 of 7 frames used)
- **Workaround used:**
  - `import_poses.py` mirrors flagged poses.
  - Retries now put a timestamp in every key.
  - Attacks can play a retimed sprite sequence (`AttackData.animation` + hit frame): the 4 good
    frames drive Light 1, with frame 3 on its first active frame. Frame-data mapping done in the
    Animation Gym.
- **Friction → proposed fix / feature:**
  - Facing ignored on low poses → a **`facing` parameter** (or auto-mirror to the reference's facing).
  - Key reuse after a failure → **say so in the error** ("idempotency key already used by failed
    execution X; use a new key"), or let a failed key be retried.
  - Custom sprite sampling makes long holds → **sample until the motion settles** (or take a target
    duration), and keep the camera angle of the reference for the whole sequence.

### 09 — Same prompt at 512 px: sharper, but it walked out of its cell (3.3 credits)
- **Date / asset:** 2026-10-09 · `ArtRaw/hero/hero-step512-keep.zip` → `Assets/Art/DreamLayer/Characters/Hero/hero_step.zip`
- **What I tried:** the combat-step sheet from entry 07 again, changing only `--frame-size 256` → `512`
  (same reference, same custom prompt, 8 frames, loop, `background: keep`). Frame size is free, so this
  should be "the same animation, sharper".
- **What held:** identity, the stance, a constant sword angle (26.5–29.9°), feet on one baseline after
  snapping, and the sharpness: the walk now matches the idle drawing. First try, 109 s.
- **What drifted:**
  - **It's a different animation, not a bigger one.** The 256 px sheet stepped in place (head sway
    52 px of 512, back to the start for the loop). The 512 px one **walked forward 113 px (22% of the
    cell) over 8 frames**, so its loop jumps back.
  - **The cell cut the sword off:** as he walked forward the blade tip left the cell in frames 6–8,
    losing 8 / 29 / 52 px of a 194 px blade. Nothing in `atlas.json` says so.
  - **`source_canvas` sheets keep the reference's framing,** so the body sits left of the cell center
    (hip at 0.36 of the width). A center pivot put every sheet frame **0.36 units** away from where the
    matching single pose stands: the hero slid sideways each time he started walking or swung Light 1.
    This was already true of the 256 px sheet; the sharper one made me measure it.
- **Workaround (all local, free):**
  - `clean_sprite_zip.py --remove-drift`: fits a line to the head's x across the cycle and takes it
    out, keeping the natural sway (19 px left). The atlas records `drift_removed_px_per_frame`.
  - The cleaner also records `edge_clipped_frames`; `Tools/patch_blade_tip.py` rebuilds those blade
    tips from the frame with the closest blade angle (rotated ≤1.7°, placed at the full 194 px).
  - Our importer now anchors a sheet's pivot on **its first frame's hip**, the same rule
    `normalize_pose.py` gives single poses. Walk frame 1 and Light 1 frame 1 now land on the idle to
    the pixel (head +65 / hip 0 px for all three).
  - Light 1's 4th sheet frame (turning to face the camera, entry 08) is dropped: the sheet covers
    startup + active (`AttackData.animationThroughRecovery = false`) and recovery returns to the ready
    stance, which is the sheet's own frame 1.
- **Credits spent vs. kept:** 3.3 → 1 sheet (8 of 8 frames, 3 repaired)
- **Friction → proposed fix / feature:**
  - Frame size changes the motion → render once and **downsample for smaller frame sizes** (or say in
    the docs that a new size is a new generation).
  - Root motion baked into cycles → an **`in_place: true` option** for walk/step cycles, or report the
    per-frame root offset in `atlas.json` so engines can remove or use it.
  - Silent clipping → **grow the cell to fit the longest reach** (their own blog names this), or list
    `clipped_frames` in `atlas.json` like `kept_background_frames`.
  - Off-center `source_canvas` frames → put a **body anchor (feet + hip point) per frame** in
    `atlas.json`, so every engine importer can line sheets up with single sprites.

### 10 — The world art batch: arena, burning arena, lobby, relics, a phase-2 knight, key art (6 credits)
- **Date / assets:** 2026-10-10 · `ArtRaw/world/{arena1,arena1-burning,lobby,relics,keyart}.png`, `ArtRaw/knight/knight-phase2.png`
- **What I tried:** the verbatim style clause plus a background clause ("softer, lower-contrast values than the
  characters") for 3 generates (arena, lobby, relics sheet), and 3 edits: "same scene, now burning", "same knight,
  armor cracked", and a key-art composition from the approved cast sheet.
- **What held:**
  - **6 of 6 kept, 5 on the first try** (one temporary failure, free), 23–67 s each.
  - One look across everything: the sun emblem, the knight's reds and the anime cel line all carry into the
    backgrounds and props. The relic sword came back with the knight's ring pommel and red grip from words alone.
  - Layout instructions mostly landed: three pedestals at 23 / 50 / 77% of the width (asked: 25 / 50 / 75%).
  - The phase-2 knight kept every identity anchor (helm, plume, sun emblem, belts, sword).
- **What drifted:**
  - **Side-view floors are thin.** Both backgrounds put the floor in the bottom 11–14%, while a side-view fighter
    needs feet ~20% up the screen. Asked for "the floor's back edge one fifth up"; got one tenth.
  - **"Keep everything exactly the same" re-composed the burning arena:** new banners at both edges, the walls
    rebuilt in blocks, a wider window. The floor line held, so it still lines up.
  - **The lobby's "waist-high" pedestals are taller than the hero** at the scale the room needs.
  - **The phase-2 knight ignored "no glow halo"**, came back on a textured off-white with ash splatter, and the
    edit's reference (`knight-ref.png`) draws a bigger helm than the cast-sheet master, so a helm ruler sized him
    20% small.
  - **Key art:** the hero reads as a young teen (designed 20–30); the knight's sword runs off the top edge despite
    "keep the top quarter calmer for a title"; an invented scabbard. Edits return the reference's aspect, so it's
    square (2048²) where a 16:9 or 630×500 cover was wanted.
- **Workaround (local, free):**
  - `Tools/fit_background.py` stretches the plain floor band (1.84×) and trims the top so feet land on the floor;
    the camera moved up 0.6. The burning edit fits with the same numbers.
  - The lobby is used as generated; its cameras were reframed instead (ground 14% up when exploring).
  - The phase-2 knight is sized by body height against the standing idle (ruler 176 instead of 220).
  - `Tools/import_world_art.py` fits, cuts out and writes provenance sidecars for every background and prop.
- **Credits spent vs. kept:** 6 → 6 assets (3 relics from one sheet = 8 sprites in the game)
- **Friction → proposed fix / feature:**
  - **A game-background mode with a ground line:** "floor at y% of the height", or return the detected floor
    row in the response, so engines can place characters without hand measurement.
  - **A strict "keep composition" edit** (mask or inpaint-only: change surfaces, never layout) for phase-2 and
    day/night variants, where the layout must match pixel for pixel.
  - **`--aspect` on edits**, so key art can be composed from a square reference into 16:9 or a store-cover size.
  - **Honor negative instructions on effects** ("no glow halo") or offer a clean "effects layer off" option,
    since glows and halos break local cutouts.

### 11 — Finishing Boss 1: a walk, slam and fire poses (8.3 credits)
- **Date / assets:** 2026-10-10 · `ArtRaw/knight/knight-walk-keep.zip`, `knight-slam-windup.png`, `knight-slam-r2.png`,
  `knight-flame-windup.png`, `knight-flame-swing.png`
- **What I tried:**
  - A custom walk sheet. The prompt said "walking on the spot", and the reference was a square canvas with the
    knight left of center and wide space in front.
  - Two slam poses (edits of the knight reference).
  - Two "same pose, blade on fire" edits of the existing cleave windup and strike.
- **What held:**
  - **6 of 6 delivered on the first try** (40–82 s each).
  - **Small local edits keep the composition exactly:** the flaming swing's bounding box matches the cleave
    strike's to the pixel, so it reused the same normalization ruler. Compare entry 10, where the same
    "keep everything" wording re-composed a whole scene.
  - **The wide reference canvas worked:** the walk drifted 97 px again, but no frame touched the cell edge, so
    nothing needed rebuilding (entry 09's blade clipping didn't recur).
- **What drifted:**
  - "Walking on the spot" was ignored (12 px of forward travel per frame). That's two custom cycles out of two.
  - The slam windup ignored the reversed, point-down grip.
  - **The first slam impact had two swords** (one driven into the ground, a second lying across his leg):
    1 credit discarded. "He has exactly one sword and no scabbard" fixed it on the retry.
  - The slam poses came from `knight-ref.png`, which draws a bigger helm than the master, so the helm ruler
    gets a 0.8 correction (same as entry 10).
- **Credits spent vs. kept:** 8.3 → 5 assets (1 rejected)
- **Workaround used:** `clean_sprite_zip.py --remove-drift` again; wide reference canvases for any cycle with a weapon;
  explicit object counts in edit prompts.
- **Friction → proposed fix / feature:**
  - **Honor "in place" for cycles,** or expose `in_place: true`. Every game engine moves the character itself.
  - **Duplicate props:** a consistency check (or a "count" constraint) for the reference's signature item; a
    second sword is a hard reject for a game asset.

### 12 — A relaxed lobby set: sword on his back (4.3 credits)
- **Date / assets:** 2026-10-10 · `ArtRaw/hero/hero-lobby-idle.png`, `ArtRaw/hero/hero-lobby-walk-keep.zip`
- **What I tried:** an edit of the side-view reference ("he has put his sword away on his back, stands relaxed"),
  with only the sword line of the identity clause changed; then a custom walk sheet generated *from* that idle
  (wide canvas, "walking on the spot"), so the walk's frame 1 is the idle.
- **What held:**
  - Identity, and both first try.
  - **The walk stayed in place: 0.4 px of drift per frame,** against 12–15 px for the two earlier cycles. That
    makes one of three. The difference: nothing held out in front. Both drifting cycles held a weapon forward
    (the hero's step, the knight's walk).
  - Frame 1 matched the idle exactly: 2.43x by height and width.
- **What drifted:**
  - The scabbard came out short and bulky, ending at his hip, too short for the blade it holds.
  - The background came back as textured off-white again (paper grain). Tiny specks passed the key and
    stretched the cutout's crop to the whole canvas.
- **Workaround:** `clean_cutout.py` now drops opaque specks under 600 px (run-length labeling, plain numpy);
  existing cutouts are unchanged.
- **Credits spent vs. kept:** 4.3 → 2 assets (an idle + an 8-frame walk)
- **Friction → proposed fix / feature:**
  - **Keep the background clause literal:** "plain flat solid white" sometimes returns textured paper. A
    `background: flat` option, or a pure-white fill on request, would make local keying reliable.

### 13 — Walk-cycle timing isn't foot-locked: measuring it (0 credits)
- **Date / assets:** 2026-10-10 · `Assets/Art/DreamLayer/Characters/Hero/hero_lobby_walk.zip` (entry 12's walk)
- **What I tried:** played the relaxed lobby walk in the Animation Gym on a new "treadmill" view (the floor ruler
  scrolls at the in-game ground speed, so a planted foot that doesn't ride the ruler is sliding), and measured the
  planted foot's travel between frames with a new tool, `Tools/foot_travel.py`.
- **What held:** the poses themselves. Every frame's head and hips stay within ~35 px of the idle's, the
  baseline is flat after snapping, and frame 1 is still the idle exactly.
- **What drifted:**
  - **The timing ignores the feet.** `frame_durations_ms` came back near uniform (620–660 ms), but the planted
    foot travels 44–67 px between most frames and **0 px between frames 8 and 1** (two feet-together stances in a
    row). Played at a constant ground speed, that frame pair stalls the body and the foot skates forward.
    The fight's step sheet has the same 0 px hold at its loop point.
  - **4 footfalls per 8 frames.** The cycle covers 380 px of floor. My earlier cycle-speed guess assumed two steps
    per cycle, so the lobby played it 1.43x too fast: the feet slid backward and he took ~316 steps a minute.
  - **An enclosed background sliver** between the legs in frame 8 (219 px, white). It was invisible on the light
    review sheet and obvious on the moonlit lobby's dark floor.
- **Workaround:**
  - `foot_travel.py` traces the boot on the floor in each frame back into the frame before it, lifting the search
    so a heel strike (in the air one frame earlier) still matches. Frame times become proportional to travel,
    and the total travel per cycle sets the speed at which the cycle plays at its authored rate. The Unity side
    (`ApplyArt.ApplyFlipbook`, `footLocked: true`) reads both.
  - Re-cleaned the sheet with `clean_sprite_zip.py --gap-core 2`. With the old settings the output is
    byte-identical to the current file; with the new setting the only change is the sliver.
- **Credits spent vs. kept:** 0
- **Friction → proposed fix / feature:**
  - **Foot-locked `frame_durations_ms` for walk / run cycles:** time each frame by the planted foot's travel (or
    return the travel per frame in `atlas.json`), plus a `ground_speed_px_per_cycle` field. That's what an
    engine needs to sync a cycle to movement without foot sliding, and it's the same measurement the cutout
    step already makes possible.
  - **Loop seam check:** for `loop` cycles, flag a last frame that repeats the first pose (here 0 px of travel
    between them), or drop it.
  - **Review on a dark matte:** enclosed background between limbs hides on a light preview. A dark-background
    preview.gif (or a checkerboard) would surface it before import.

### 14 — The white rim: local keying keeps the half-blended edge (0 credits)
- **Date / assets:** 2026-10-10 · every locally keyed cutout: 5 sprite sheets, 3 relics, 25 poses
- **What I saw:** Shawn asked why the hero had a white cutout line around him. On the dark lobby floor and in the
  Animation Gym, every locally keyed asset wore a thin light outline. It was invisible on the white review sheets.
- **Why:** DreamLayer returns art on white. The line art's anti-aliased edge pixels are a blend of the dark outline
  and that white. `clean_cutout.py` drops pixels near the background color and peels a near-white halo, but a 50/50
  blend (luminance ~140) passes both tests and stays **fully opaque**. On the sheets (hard 0/255 alpha), 52–66% of
  the outermost pixels were light gray; on the relics, 74–78%. The tool even reported a "remaining light fringe",
  but its threshold (within 3x the key tolerance of the background) only counted near-white pixels, so it read
  close to zero while the rim was there.
- **Workaround:** `Tools/defringe.py`, which un-blends each edge pixel against the known background color ("color
  to alpha": C = aF + (1-a)B, solved for the foreground at a partial alpha). It works only on a 2 px band grown from
  the transparent side, and stops at the dark outline, so light fills inside the line art (the shirt, eye whites,
  silver hair, the knight's armor) are never reached. Run in place on all 33 assets; no `.meta` changed, so every
  sprite reference held. It's now the default last step of `clean_cutout.py`, and the fringe metric counts what a
  viewer sees: fully opaque edge pixels closer to the background than to black (hero lobby idle: 2,768 → 3).
- **Credits spent vs. kept:** 0
- **Friction → proposed fix / feature:**
  - **`background: keep` should come with the background color** (`atlas.json` → `background_rgb`), so local keying
    and un-blending don't have to guess it from the border. A flat, exact fill would help more still (entry 12).
  - **Review previews on a dark matte** (entry 13 again): a light rim and enclosed background both hide on white.
  - **When DreamLayer cuts out itself, un-blend the edge** (premultiplied-style decontamination) rather than keep
    the background color in the edge pixels; game scenes are rarely white.

### 15 — The lobby had no floor to stand on (0 credits)
- **Date / asset:** 2026-10-10 · `ArtRaw/world/lobby.png` → `Assets/Art/DreamLayer/Backgrounds/lobby.png`
- **What I saw:** Shawn: the lobby felt off, as if the hero were levitating. The painting's floor was a thin strip
  (the bottom ~10%), so the hero stood on the pedestals' base line while drawn in front of them: overlap said
  "closer than the pedestal", the foot line said "same depth". With no contact shadow, he read as floating.
- **Same failure as the arena (entry 10):** a "side view" environment comes back composed like a postcard, with
  the floor squeezed into the bottom 10–14%. A side-view game needs a floor band of ~20–30% that the characters
  can stand in, in front of the props.
- **Workaround:** `fit_background.py` stretches only the floor strip in front of the pedestals (below their base
  outline) 2x and trims the top, so the pedestals and the wall keep their drawing. The hero now stands 25% into
  that floor, in front of the pedestals, with a soft contact shadow (`ContactShadow`, generated at runtime).
- **Credits spent vs. kept:** 0
- **Friction → proposed fix / feature:**
  - **A ground-plane option for environments:** "floor band 25% of the height, characters stand at row N", or a
    `game_camera: side_view` preset that composes for a playable floor instead of a postcard. Every 2D side-view
    background we generated needed this fix by hand.
  - **Return the floor line:** a horizon / floor-row estimate in the response would let an importer place the
    ground automatically.

### 16 — The walk's rhythm: 4 steps in 8 frames, unevenly spaced (0 credits)
- **Date / asset:** 2026-10-10 · `hero_lobby_walk.zip` (entry 12's relaxed walk)
- **What I saw:** Shawn: the lobby walk's arm swing and steps looked unnatural.
- **What drifted (frame by frame):** the 8 frames hold 4 footfalls, spaced 2, 1, 2 and 3 frames apart. Frames 8 and 1
  are both feet-together stances (the cycle walks out of the idle and back into it), and between frames 4 and 5 the
  arms jump from one extreme to the other with no in-between drawing. Played at a walking pace, the arms flail and
  the gait stutters once a cycle. The arms do alternate correctly with the legs.
- **Workaround:** loop frames 8,2,3,4,5,6,7 (frame 1 is the idle; starting on 8, feet together and arms down, makes
  idle -> walk seamless), foot-lock that order (`foot_travel.py --order`), and walk slower (3.6 -> 2.4 units/s,
  ~235 -> ~157 steps a minute). The one-frame arm reversal (4 -> 5) is in the drawings and stays.
- **Credits spent vs. kept:** 0
- **Friction → proposed fix / feature:**
  - **Walk cycles with a fixed gait structure:** for `walk`/`run` (preset or custom), guarantee 2 steps per loop
    with the classic keys per step (contact, down, passing, up), evenly spaced, and no extra start/stop stance.
    An animator expects exactly that from "8-frame walk"; here the frame budget went to 4 uneven steps.
  - **Return the gait:** which frames are contacts / passing (and which arm leads) in `atlas.json`, so an engine
    can loop, retime or mirror it without a frame-by-frame review.

### 17 — The King's form reference: a beautiful cape the game can't animate (2 credits)
- **Date / asset:** 2026-10-10 · `ArtRaw/king/king-ref.png`, `ArtRaw/king/king-ref-t2.png`
- **Goal:** the second form of the same villain (the knight crowned, ice-themed); every King pose will be an edit of
  this image, animated pose-to-pose with tweens.
- **Reference:** `ArtRaw/knight/knight-ref.png` (the approved knight master)
- **Operation:** edit ×2
- **Exact prompts:** `ArtRaw/king/king-ref.prompt.txt` (t1), `ArtRaw/king/king-ref-t2.prompt.txt` (t2)
- **execution_id / idempotency key:** t1 `5221853c-…` / `dlj-king-ref-10102341-t1`; t2 `c93a14b0-…` / `dlj-king-ref-10102344-t2`
- **What held (t1):** every identity anchor (helm, visor, plume, tabard + sun, both belts, ring-pommel sword) and the
  pose; the crown sat around the helm as asked. As a character design it was the best image of the project.
- **Rejected (t1), by Shawn:** I asked for "a long heavy royal mantle", and got a floor-length fur-trimmed cape twice his
  width. It can't move naturally in our animation: the boss is ~10 poses with tweens, so a cape that size changes shape
  completely between poses and snaps instead of flowing. Also: "pale frost-blue" came out near-white, and the metal was
  glossier than the locked cel style (the style clause wasn't restated in the prompt).
- **t2:** "No cape, no cloak, no mantle, no fur, nothing hanging from his shoulders", plus the style clause pasted verbatim:
  flat cel shading came back, the armor reads ice-blue with gold trim, the sun moved onto the crown. New drift: the
  tabard's chest panel (and its sun emblem) disappeared, and the blade became a spiky ice crystal.
- **Credits spent vs. kept:** 2 → 1 (pending Shawn's approval)
- **Friction → proposed fix / feature:**
  - **Character design is judged as an illustration, not as an animatable asset.** DreamLayer's game focus could add an
    "animation-ready" constraint (or a warning) for references: loose capes, long hair, flowing sleeves and
    trailing cloth are exactly what pose-to-pose and sprite animation can't carry. A tool that knows the output feeds
    animation should steer away from them, or say so, before the asset becomes the root of 10 more edits.
  - **Edits restyle unless told not to:** without the style clause, the edit drifted toward glossy rendering. An
    "inherit the reference's style" flag on `edit` would save pasting the clause into every prompt.

### 18 — Same canvas is not same scale: the King's edits drifted up to 22% (0 credits)
- **Date / asset:** 2026-10-11 · `Assets/Art/DreamLayer/Characters/King/king_*.png`
- **Goal:** 8 poses of one boss, swapped pose-to-pose in the fight, so he must stay one size.
- **Reference:** `ArtRaw/king/king-ref-t4.png` (every pose an edit of it)
- **Operation:** edit ×7 (already logged in the King batch; this entry is about measuring them)
- **What held:** the canvas (1600 px, like the reference), identity, framing; the phase-2 edit (same pose, frost
  added) came back at exactly the reference's scale (crown and belt at 1.00).
- **What drifted:** the character's scale inside the canvas, whenever the pose changed: cleave strike +22% (helm,
  crown and sabatons agree), defeated +14%, floor cast +12%, hurt +10%, cast +6%, windup +3%. Median +10%.
  Playtesting caught it (Shawn: "some of the boss's pictures have different size"), not my review: on a contact
  sheet at thumbnail size, with every pose in a different stance, a 10-20% change doesn't read. In the fight, where
  the idle swaps to the strike in one frame, it does.
- **How I measured it (free, local):** template matching of rigid parts cut from the idle (helm + crown, knee
  medallion, sabaton, belt gems) over scale 0.7-1.5 and rotation +-45, then candidate rescales side by side. A 3/4
  helm doesn't match a profile helm well (NCC 0.25-0.5), so no single part is enough; parts that keep their shape
  across poses (sabatons, knee medallions, the crown) agree within ~5%.
- **Workaround used:** a per-pose ruler in `Tools/import_poses.py` (scale = master ruler / pose ruler).
- **Friction → proposed fix / feature:**
  - **Report the subject's scale relative to the reference** on every edit: DreamLayer already knows the reference
    character; returning "subject drawn at 1.22x the reference" (or keypoints / a head box) in the result would turn
    this half-hour of measuring into a one-line import step.
  - **A "keep subject scale" option for edits** meant to be poses of one character (a sprite-set mode): same canvas,
    same feet line, same head size, only the pose changes. That's what a pose-to-pose game asset needs, and the
    sprite-sheet endpoint already does it within one sheet.

### 19 — A run "on the spot": two steps, one flight, and a planted foot that never slides (3.3 credits)
- **Date / asset:** 2026-10-11 · `Assets/Art/DreamLayer/Characters/Hero/hero_lobby_run.zip`
- **Goal:** the hero runs across the lobby instead of walking (Shawn: "running would make more sense"); same look as
  the lobby idle (sword sheathed on his back), played in place while the game moves him.
- **Reference:** `ArtRaw/hero/hero-lobby-walk-ref.png` (the lobby idle on a white canvas, the walk's reference)
- **Operation:** sprite, custom prompt, loop, 8 frames, 512 px, `background: keep` (3.3 credits)
- **Exact prompt:** `ArtRaw/hero/hero-lobby-run.anim.txt` (asked for "exactly two running steps in the loop, evenly
  timed", knees up, arms opposite the legs, sword fixed on his back, head steady)
- **execution_id / idempotency key:** `157daf97-28ee-4f47-9782-8e252badaf2e` / `dlj-hero-lobby-run-keep-10110201-t1`
- **What held:** identity and scale (frame 1 is the reference at the same size, like the walk), the sheathed sword,
  in place (1.8 px/frame drift), a clean airborne stride with real lift (feet 19-34 px off the floor in frames 3-4).
- **What drifted:** the gait structure again (see note 16). The two steps aren't alike: one long stride with a
  flight phase, then three near-identical stance frames and a foot swap with no flight. The arms barely alternate.
  Frame 1 is the standing reference, not part of the run. `frame_durations_ms` is 620-660 ms per frame: a 5 s loop
  for a run, so the timing is unusable as delivered.
- **Measured:** the "on the spot" planted foot doesn't slide back during stance (0 px between frames 6, 7, 8), so
  my foot-lock tool had nothing to measure: a real treadmill run moves the planted foot back at the running speed.
- **Rejected attempts:** 0
- **Workaround used:** `Tools/clean_sprite_zip.py --snap-feet --airborne 3,4` (new: stance frames put the planted
  foot on the floor, airborne ones move with their neighbors so the lift is kept), and an even cadence (~2.8 steps a
  second at 5.4 units/s) instead of foot locking. First loop: frames 2-8 in order. Shawn: "looks weird": played in
  order it's one leap (3-5) then four near-identical knee-up stances (6, 7, 8, 2), a leap-shuffle-shuffle gallop.
  Fixed loop: one stride repeated, 2-3-4-5 (pass, push off, airborne, push off again), 4 frames, one step per loop;
  the legs look alike at this size, so one step repeated reads as both. The Animation Gym shows it beside the
  two-stride and as-delivered loops. Lesson: a generated cycle's frame order is a suggestion; check which frames
  are the same key pose before trusting the count.
- **Friction → proposed fix / feature:**
  - **Return the gait, not just frames:** which frames are contacts and which are airborne, per foot, in `atlas.json`.
    That's what an engine needs to place footsteps, time the cycle to ground speed and know when not to snap feet.
  - **"On the spot" should mean a treadmill:** the planted foot moves back at a constant speed during stance. Then
    the game can lock it to the floor at any speed. A `ground_speed_px_per_frame` in the atlas would make it exact.
  - **Exclude the reference pose from loops** (or flag it): frame 1 of every custom loop here was the reference.

### 20 — The final form: one image edited three times, and the limb that wouldn't hold (14 credits)
- **Date / asset:** 2026-10-11 · `Assets/Art/DreamLayer/Characters/Final/final_*.png`, `Backgrounds/arena3*.png`
- **Goal:** the last boss is the King's body taken by an eldritch god: recognizably the same man (helm, plume,
  crown, sword) with the god's parts added, in 10 poses swapped pose-to-pose, plus his arena in two phases.
- **Reference:** `ArtRaw/king/king-ref-t4.png` (the approved King) → `ArtRaw/final/final-ref-b.png` (approved by
  Shawn), every pose an edit of that.
- **Operation:** edit ×14 (2 reference options, 9 poses + 1 retry, 2 arena edits). Prompts in `ArtRaw/final/*.prompt.txt`.
- **What held:** identity across three forms from one chain of edits: helm, plume, crown (cracked as asked), the
  sun crest turned into an eye, the sword. Every pose first try, 54-93 s each (one retry took 184 s). The arena
  edits kept the throne room's columns, banners, throne and floor line (row 1098 vs 1116), so both phases line up.
  Phase 2 ("keep the pose, the god wakes") is the strongest image of the batch.
- **What drifted:**
  - **Framing:** option A ignored "keep the framing" and came back as a waist-up close-up on a 2048 canvas (the
    input was 1600): unusable as a full-body pose reference.
  - **A limb's identity:** the huge tentacle arm is the god's signature, and the least stable part: normal-sized in
    the windup and the leap, duplicated in the lash. "Exactly one tentacle arm and exactly one claw" fixed the count
    but turned the striking arm into armor with talons. Fixed count, wrong material: the limb has no stable identity
    the way the helm or crown does.
  - **Effects not asked for:** "no magic effects, no floor shadow" in every prompt, yet the floor cast came back with
    a black shadow pool and a red energy burst under the claw, the strike with a grey cast shadow.
  - **Scale:** 0.85x to 1.22x the reference (median ~1.1x), as with the King.
  - **Background:** 3 of 10 edits came back on paper grain, so background showing through tentacle loops was too
    noisy for the default enclosed-gap tolerance (white patches in game).
- **Rejected attempts:** 2 — option A (framing), the lash retry (arm drawn as armor; Shawn kept the first).
- **Credits spent vs. kept:** 14 credits → 12 kept (reference, 9 poses, 2 arenas).
- **Rubric score:** 9/10 static (identity and style strong) — weakest: scale / anchor stability (needs per-pose
  rulers and anchors).
- **Workaround used:** per-pose scale rulers and hip anchors (template matching of helm, crown, chest eye, knee,
  boot, belt; side-by-side checks), local erasing of drawn shadows and effects before the cutout (boots traced by
  hand where the shadow touched them), a wider gap tolerance for paper-grain edits.
- **Friction → proposed fix / feature:**
  - **Name the parts that must persist, and get a check back:** let an edit take a short list of identity parts
    ("one huge purple tentacle arm with a three-talon claw", "cracked crown") and report whether each is present and
    how many. A limb count is something a vision check can verify before the result is delivered.
  - **Negative constraints as hard filters for game assets:** "no floor shadow, no effects" is a pipeline requirement,
    not a style hint. A `game_asset: true` mode could strip cast shadows and effect overlays, or flag them.
  - **Keep the canvas on "keep the framing" edits:** return the input's canvas size and crop, or say when it changed.
  - **Report subject scale vs. the reference** (as in entry 18): it would turn the hour of measuring into one number.

### 21 — Fixing the arms changed the legs: one sprite prompt, many coupled outcomes (3.8 credits)
- **Date / asset:** 2026-10-11 · `Assets/Art/DreamLayer/Characters/Hero/hero_lobby_run2.zip`
- **Goal:** the lobby run of entry 19, with the arms swinging (in that sheet the front fist stays at chin height in
  every frame).
- **Reference:** `ArtRaw/hero/hero-lobby-walk-ref.png` (as before) · **Operation:** sprite, custom prompt, loop,
  9 frames, 512 px, background keep (quoted ~3.7, charged 3.8).
- **Exact prompt:** `ArtRaw/hero/hero-lobby-run.anim.txt` (the first one kept as `hero-lobby-run-t1.anim.txt`).
- **What held:** identity, scale (frame 1 = the reference again, the same 308 px), in place (0.5 px/frame drift),
  feet within 3 px after snapping; the requested arm swing partly (the back hand swings behind the hip in 2 frames,
  both hands drop by the hips in 2).
- **What drifted:** "relaxed open hands, not fists" was ignored; and the legs changed although the prompt about them
  barely did: the leaning stride with a flight phase became an upright high-knee jog on the spot. The prompt had
  added "a light, relaxed jog" and "four frames each": the model seems to trade stride length for the in-place
  constraint once the motion is described as lighter.
- **Credits spent vs. kept:** 3.8 → 1 sheet in the game (the first run sheet stays in the gym).
- **Workaround used:** 9 frames instead of 8 (the reference comes back as frame 1, so 8 run frames give an even
  two-step loop); both sheets side by side in the Animation Gym so the choice is made in motion, at game size and speed.
- **Friction → proposed fix / feature:**
  - **Edit a sprite sheet** ("same run, arms swinging") instead of regenerating it: keep the legs and timing that
    worked, change one body part, like the image edit endpoint does for stills.
  - **Separate the reference frame from the loop** (or say in atlas.json which frames are the loop), so the frame
    count asked for is the loop length.
- **Outcome:** Shawn compared both runs with the walk in the game and kept the walk ("it looks the most natural ...
  I think is the problem of gen ai sprite"). The walk sheet (entry 16) held up because a walk is all planted key
  poses; both run sheets failed in the in-betweens (flight, arm swing). 7.1 credits of run sheets → 0 shipped.

### 22 — Two references in one: a composite for the ending illustration (1 credit)
- **Date / asset:** 2026-10-11 · `Assets/Art/DreamLayer/Backgrounds/ending.png`
- **Goal:** a key illustration for the ending: a specific character (the knight, as drawn in his defeated pose) in a
  specific place (the final arena), changed by the story (the god gone, dawn).
- **Reference:** `ArtRaw/ending/ending-ref.png`: the knight's cut-out (fight 1's defeated pose) pasted at 42% size
  into `arena3.png` (2560 x 1440), because a request takes one reference image.
- **Operation:** edit. **Prompt:** `ArtRaw/ending/ending.prompt.txt`.
- **What held:** the knight exactly (pose, armor, plume, sword on the floor), the room's layout, the 16:9 canvas.
  The paste seam disappeared: he got the dawn light, a shadow and rubble around him.
- **What drifted:** the throne vanished (only the dais remains); the room came back more painterly than the game's
  cel style (soft gradients, rendered rubble) while the knight stayed cel. As an ending picture it works; as a game
  background it would clash.
- **Credits spent vs. kept:** 1 → 1 (first try, 95 s).
- **Friction → proposed fix / feature:**
  - **Multiple references per request**, with roles ("this character", "this place", "this style"). The composite is
    a workaround that only works when the pieces can be laid out by hand in the final composition.
  - **Style lock across a project:** a project-level style reference that every edit inherits, so a scene edit can't
    drift from cel to painterly.

### 23 — Two boss walks: identity held, size didn't (6.6 credits)
- **Date / assets:** 2026-10-11 · `Assets/Art/DreamLayer/Characters/King/king_walk.zip`,
  `Assets/Art/DreamLayer/Characters/Final/final_walk.zip`
- **Goal:** the King and the Unknown One walk toward the hero instead of sliding in their idle poses (the Knight got
  his walk on Oct 10). Played at gameplay size, starting from the idle with no visible pop.
- **Reference:** each boss's approved form reference, cut out and placed left of center on a white square canvas with
  room in front (`Tools/make_walk_ref.py`; the Unknown One at 68% of the height for his claw arm's reach).
- **Operation:** sprite, custom animation prompt, loop, 8 frames, 512 px, `background: keep`.
  Prompts: `ArtRaw/king/king-walk.anim.txt`, `ArtRaw/final/final-walk.anim.txt` (the Knight's walk prompt, plus
  "exactly one sword" and, for the god, "exactly one huge clawed tentacle arm").
- **execution_id / idempotency key:** `8f9878b5-...` / `dlj-king-walk-keep-10110653-t1`;
  `89a4141f-...` / `dlj-final-walk-keep-10110655-t1`
- **What held:**
  - **2 of 2 on the first try** (78 s and 65 s). Identity held in all 16 frames: crown, plume, the ice sword, the
    jeweled armor; the chest eye, the coiled tentacles, one claw arm, one sword. The object counts in the prompt worked
    (entry 11 had a second sword; entry 20 a doubled limb).
  - Real steps: one foot planted, the other passing; frame 8 returns close to frame 1, so the loop closes.
  - The Unknown One drifted only 3.4 px per frame (the King 9.9): "walking on the spot" is honored more often now.
- **What drifted:**
  - **Both sheets zoomed in across the cycle:** the King grew 22% in height (307 → 375 px) and 19% in shoulder width
    from frame 1 to frame 8, the Unknown One 17%, with the same pose at both ends. Played raw, they'd swell and shrink
    every step. This is the 15–20% size drift from DreamLayer's own sprite posts, inside one sheet.
  - The Unknown One's claw arm rises to point straight ahead for three frames. That's not in the prompt, but it reads
    as reaching for the hero, so it stays.
- **Rejected attempts:** none.
- **Credits spent vs. kept:** 6.6 → 2 sheets (16 frames) kept.
- **Workaround used (all local, free):**
  - `clean_sprite_zip.py --snap-feet --remove-drift`, the King with the default gap setting: the stricter one ate the
    white highlights along his ice blade. The Unknown One got the stricter one, plus clearing near-white slivers left
    in his tentacle loops (he has no real white).
  - New `Tools/normalize_sheet_scale.py`: every frame scaled back to frame 1's height (feet to crown, the plume
    skipped by color), anchored on the feet and the torso. With `--match-idle` it also prints the Flipbook scale
    against the idle sprite and writes a pivot so the walk starts where the idle stands.
  - **The importer's hip pivot was 0.37 units off for the Unknown One:** the claw arm hanging at hip height pulls the
    measurement forward. The importer now takes an optional atlas `pivot_x`; the King's computed value matched the
    measured one (0.34), so the method checks out.
  - `foot_travel.py`: frame times follow the planted foot, so the feet don't skate at the bosses' walk speeds.
- **Friction → proposed fix / feature:**
  - **Scale lock for sprite sheets:** a "keep the reference's scale in every frame" option (or a `scale_spread` in
    atlas.json, like `kept_background_frames`), because a game needs every frame at the same pixel scale.
  - **A pivot / root point in atlas.json:** the body's ground point per frame. "source_canvas" leaves every engine to
    guess, and a big limb breaks the guess.

### 24 — README art: a composite header and an idle preset (4.3 credits)
- **Date / assets:** 2026-10-11 · `ArtRaw/readme/readme-header.png`, `ArtRaw/readme/hero-idle-keep.zip`
- **Goal:** the public repository's header, and a fresh sample sheet for the importer's README walkthrough (art I own,
  so it can ship with the tool).
- **Header:** edit of a composite (the hero and the villain's three forms at their in-game proportions on a plain
  grid). Prompt: `ArtRaw/readme/readme-header.prompt.txt`. **First try, 97 s.** Every character held exactly, with
  one sword each and one claw arm, and the grid became a gold-ruled sprite sheet with a glowing baseline. The empty
  top band I asked for stayed empty, so the title went in locally.
- **Idle sheet:** the `idle` preset (no custom prompt), 8 frames, 512 px, background kept, from the hero's side
  reference. **First try, 94 s.** Presets come back `ground_anchor`, so the feet were already on one row (0 px of
  snapping; custom prompts come back `source_canvas`). Breathing is a 6% rise and fall, with no zoom drift.
- **What drifted:** **a second sword hilt appeared on his back in frames 2-6.** That's the sheathed sword from his
  lobby look, which wasn't in the reference I sent. Also a soft grey smudge by his face in frames 3-6 (breath?),
  which the local cutout mostly kept faint.
- **Credits spent vs. kept:** 4.3 → 2 assets kept (the header, the 8-frame sample).
- **Friction → proposed fix / feature:**
  - **Prop consistency across a character's assets:** a reference-only mode for presets ("use only what's in this
    image"), since the preset pulled in an item from a different asset of the same character.
  - **Report the alignment mode up front:** presets return `ground_anchor` and custom prompts `source_canvas`, which
    changes how a sheet must be cleaned. The docs could say so next to each option.

<!--
### NN — <short title>
- **Date / asset:** 2026-10-__ · `Assets/Art/DreamLayer/<Folder>/<name>.png`
- **Goal:** what the asset has to do in the game (gameplay size, pivot, role in the fight)
- **Reference:** path or `input_asset_id` of the reference used (or "none — text only")
- **Operation:** generate | edit | cutout | upscale | sprite (frames, frame size, mode, action / animation_prompt, background)
- **Exact prompt:**
  ```
  <paste verbatim>
  ```
- **execution_id / idempotency key:** `...` / `...`
- **What held:**
- **What drifted:** (numbers where possible: scale %, anchor px, palette, weapon length)
- **Rejected attempts:** N — why each was rejected
- **Credits spent vs. kept:** X credits → Y assets kept
- **Rubric score:** __/10 (static) or __/12 (animation) — weakest criterion:
- **Workaround used:**
- **Friction → proposed fix / feature:**
  - <friction> → <what DreamLayer could add or change>
-->
