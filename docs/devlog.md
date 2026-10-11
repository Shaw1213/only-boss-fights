# Devlog — DreamLayer Jam

Running log of the whole project: decisions, why, results, numbers. Raw material for the itch.io
devlog posts. DreamLayer-specific experiments (prompts, drift, credits) live in
[dreamlayer-notes.md](dreamlayer-notes.md); this file links to them.

**Possible post angles so far**
- "An AI agent playtested my boss fight before I did" — scripted bot through real input.
- "Why my parry felt bad, and the 3-part fix" — the *when* cue, the window, the miss penalty.
- "Reading DreamLayer's docs before spending a credit" — what's validated, what isn't.
- "Why my hero walks instead of runs" — generated sprite cycles hold up when every frame is a planted key pose (a
  walk) and fall apart in the in-betweens (a run's flight and arm swing). Two run sheets, 7.1 credits, 0 shipped;
  Shawn: "I think is the problem of gen ai sprite". See "Back to the walk" (2026-10-11).
- "One villain, three forms, one image edited three times" — knight → King → the Unknown One as a chain of edits,
  and what survived each step (helm, plume, crown, the sun that becomes an eye).
- "Same canvas is not same scale" — measuring pose drift (up to 22%) before it reaches the game.

---

## 2026-10-07 (Wed) — Day 1

### Setup: an agent-driven Unity pipeline
- Unity 6.6 (6000.6.4f1) project, driven from Claude Code through Unity's official CLI: the agent
  edits scenes and assets, compiles, enters Play mode and builds **inside the open Editor**.
- Installed Unity's official agent plugin plus a few community skills (Unity C# correctness,
  combat design, game feel), each reviewed first: markdown only, no scripts or hooks.
- Cleaned the URP template (tutorial files, Version Control package), switched to the URP
  **2D Renderer**, put the project under git with LFS for art and audio.

### Read the DreamLayer docs before spending a credit (0 credits)
- Full docs + the official agent plugin repo + the npm packages. Details:
  [dreamlayer-notes.md → entry 01](dreamlayer-notes.md).
- Headline: their `unity-sprite-import` skill has **never been run in Unity** (their own test plan
  says so). Checked against their sample ZIP: wrong looping, centered pivot instead of feet, fixed
  128 pixels per unit, uncompressed textures in `Resources/` (bloats WebGL), atlas JSON that Unity's
  built-in parser can't read. → The importer PR opportunity, testable with zero credits.
- Credit facts that shape the plan: charged only on delivery; sprite price depends only on frame
  count (7 frames = 5.8), frame size is free; sprite jobs can't be cancelled.

### Concept: a boss rush — one villain, many forms, a gallery
- **2D, side view** because DreamLayer makes images only. In 2D almost every visible pixel can come
  from it, and its strengths (one consistent character + edits) are 2D strengths.
- **Only boss fights** (no levels): all combat design, small cast, big illustrated characters.
- **Hook:** every boss is the *same villain* in a different form, each form a painting in a
  gallery in its own art style. Keeping one identity across styles *is* DreamLayer's core feature.
- Design rule that came out of it: pick identity markers that survive a style change (silhouette,
  one signature item, proportions), not colors or fine detail.

### Design draft v1 → [docs/design/boss-01.md](design/boss-01.md)
- Hero: move, dash (i-frames), parry, light combo, charged heavy, riposte. Frame data for each.
- Shared "danger language": white glint = parry, red glow = dash, floor marker = move.
- Boss 1 teaches parry: Overhead Cleave, Lunge Thrust, Three-Strike combo; phase 2 adds the first
  feint and the first unparryable attack. A juice table per event, strongest effects reserved
  for the boss kill.

### Greybox, built and verified by the agent
- Colored rectangles; every number is a ScriptableObject tunable while playing. Enum state
  machines for hero and boss, rectangle hitboxes, global non-stacking hit-stop, trauma screen shake.
- Frame-rate safe: an active window can't be skipped by a slow frame (matters in WebGL).
- **Verification:** a scripted bot drove the hero through a *virtual keyboard and mouse* (the same
  input path a player uses). Every mechanic passed. A perfect-parry bot beat the boss in
  **10.9 s** against a 1.5–2.5 min target → balance note: riposte damage vs. boss HP.
- Lesson from my own harness: unfocused, the Editor ran the game at ~770 fps, so the bot's
  frame-count waits were meaningless. Rewrote it to wait in game seconds.

### Playtest 1: "parry is hard to use" → parry rework (design v2)
- Diagnosis: (1) no *when* signal: the glint showed and pulsed through the whole windup;
  (2) a tight 10-frame window; (3) a harsh 20-frame miss penalty.
- Fix:
  - **Clash:** attack into a hit to parry it (first 10 frames of a neutral attack). Combo
    follow-ups can't clash, so mashing fails (the bot mashed at 20 clicks/s and still got hit).
  - **Timed cue:** the glint pops in exactly 18 frames (0.3 s) before the hit. Pose = *what*,
    flash = *when*.
  - Parry window 14 frames, miss penalty 16; riposte only when the boss is open (not mid-combo);
    callouts PARRY! / CLASH! / EARLY / LATE / RED = DASH.
  - Mouse controls: left click attack, right click parry, Space dash.
- Result: "now it feels better." Bot checks 7/7.
- **Jump considered and parked:** the dash's i-frames are the safety net jam players need; a jump
  only earns its place if a boss is designed around vertical play.

### WebGL build for itch.io
- Caught a project default that would have broken the game on itch.io: **Brotli compression with
  no decompression fallback** (itch.io doesn't send the headers). Now Gzip + fallback, 960×540.
- `BuildScript`: build from the open Editor or batch mode, zipped with `index.html` at the root.
- Tested like itch.io serves it (static server, no compression headers): loads, click to focus,
  left/right click and Space work, no right-click menu.
- **13.9 MB** — mostly engine code from packages the game doesn't use. To trim before the final build.
- Gotcha: Unity stalls compiles and builds while it's in the background; the agent now brings it
  to the front first.

### Art style: what AI image generation is actually good at
- For AI-generated game assets the safest style is **flat / cel-shaded illustration with bold
  outlines and a limited palette**: few details → less drift between poses; hard edges → clean
  background removal; strong shapes → readable at gameplay size; tolerates tween animation.
- Avoid for this pipeline: pixel art (DreamLayer doesn't keep a pixel grid), realism and ornate
  detail (drifts), fur / smoke / glow (ruins cutouts), heavy painterly texture on animated
  characters (boils frame to frame). Painterly is fine for backgrounds.

### Villain decided: the fallen knight
- Three pitches were on the table: A (knight → king → god), B (the painter's self-portraits),
  C (one tyrant through history). **Chose A**, with a cosmic-horror twist:
  **knight → king → accepts the offer of a corrupted, Cthulhu-like god ("the Unknown One") →
  becomes the god's body** (final boss).
- **Art direction: bold-outline cel-shaded illustration**, replacing the oil-painting idea.
- More on how the forms connect is coming from Shawn.

### Cutscenes: possible, as a motion comic
- Shawn wants short animated moments connecting the game (e.g. a 10–15 s boss intro: the knight
  is found, turns, flourishes his sword).
- Full frame-by-frame animation is out: DreamLayer makes images, and a 12 s cutscene at 12 fps would
  be ~150 frames. The plan instead: **key poses + camera moves, tweens, shake, sound, one short
  sprite-sheet moment, and a boss name card**. ~2–8 credits per intro; skippable, and skipped on
  retry (a boss rush means many retries). Not built yet.

### The lobby: an Enter the Gungeon-style hub (greybox)
- The game opens on the lobby itself: title over the live scene, hero idling. Click → the camera
  eases onto the hero → walk around. Shawn swapped the gallery/painting idea for **relics you
  touch**: the Knight's sword, the King's crown, and a relic for the Unknown One (placeholder: an
  eye, sealed until both bosses are down).
- **Design problem found while building it:** in a side-view room all relics sit on one line, so
  pure touch-to-enter means you can't walk past the sword to reach the crown. Fix: walking past
  never enters; **stopping at a relic charges it up (0.6 s, it grows and trembles) and pulls you
  in**. W/E enters instantly. The charge-up doubles as anticipation before the fight.
- Win → the fight is marked cleared (saved in the browser), back to the lobby next to that relic,
  title skipped. Esc leaves a fight any time.
- **Bug the bot caught:** the title's "click to play" read `Mouse.current`, which only follows the
  most recently used mouse, so the bot's virtual mouse (or a second pointing device) didn't
  register. Switched to an input action that listens to every device.
- Bot flow test, 9/9: title → zoom; walk past the sword (no entry); stop at it (enters); win →
  back next to it, title skipped; sword cleared, crown "not built yet", eye "sealed".

### DreamLayer connected
- API key saved as a Windows user environment variable (never in a file or command); the official
  CLI (0.4.0-beta.4) reads it from there. Free checks: live key, **100 credits available**, all five
  operations including sprite sheets. 0 credits spent so far.

### Open
- How the forms connect; style per form; identity markers that survive into the god's body;
  what the Unknown One's relic is.
- Cutscene player; Boss 1 fight length; trimming unused packages.

---

## 2026-10-08 (Thu) — Day 2

### Researched how to use an image API for game assets, before spending
- Two research passes: DreamLayer's own material (all three blog posts, docs, the agent plugin's
  skills and cleanup scripts) and industry practice (Google's image-model prompting guide, Ludo,
  Layer, Unity docs, practitioner write-ups). Result: **[image-api-best-practices.md](image-api-best-practices.md)**.
- Numbers worth quoting (DreamLayer's own experiments): frame-by-frame sprites rejected **13–25%**
  of generations at **~2.3 credits per kept frame**; size drifted up to **15–20%** and sword length
  up to **⅓**; their reference took 5 attempts. Their sprite op held weapon length and is far cheaper
  (7 frames = 5.8 with cutout).
- Rules we adopted:
  - lock the master reference first;
  - paste one identity clause and one style clause word for word;
  - chain references and **"change the reference, not the prompt"** when a pose family fails;
  - one change per edit;
  - pilot 3 poses, then review a contact sheet before any cutout;
  - generate on plain white and clean up locally (border flood-fill, halo peel; our thick outlines help);
  - normalize scale on head/torso, not the bounding box;
  - keep effects and shadows out of the sprites;
  - the sprite op for the hero attack;
  - no artist or franchise names in prompts.
- Unity gotcha found: WebGL's DXT compression needs width/height in multiples of 4, so trimmed AI
  sprites can silently stay uncompressed. → The importer will warn, or we pack atlases.
- Knight prompt revised from the research: a solid fin-shaped crest instead of a plume (feathers cut
  out badly), no cape (fewer drift points), plate gauntlets (hands are the weakest area), plain white
  background, legs and the sword hand stated explicitly.

### First credit spent: a knight style test (1 credit, 99 left)
- Shawn approved 1 credit as a **test** (the knight's look and the art style are still open).
- Result in **23.4 s**: every identity anchor present, profile facing right, clean outlines.
  Drift (details in [dreamlayer-notes.md → entry 02](dreamlayer-notes.md)):
  - **asked for 3:4, got a 2048×2048 square** (the CLI sends the ratio; the server ignored it);
  - a ground line despite "no ground";
  - an off-white background;
  - closer to "flat vector" than "bold cel-shaded" (thinner lines, soft gradients).
- **Free background removal works:** `Tools/clean_cutout.py` (NumPy + Pillow) removed the background
  in **0.7 s**: flood fill from the border, so the white emblem inside the outlines stays; halo peel;
  ground line removed; cropped to multiples of 4. Clean over dark and magenta. → No paid cutouts
  needed for an outlined style; that's up to ~10 credits of the budget freed.
- Raw generations live in `ArtRaw/` (outside `Assets/`, tracked with LFS), each with its prompt file
  and a `.dreamlayer.json` provenance file.
- Shawn: "definitely not the style I'm looking for." Too clean and generic: "simple shapes, flat
  colors, limited palette, no texture" pulls these models toward corporate flat vector art. Next: a
  mood board → a specific style guide → a 1-credit style board (4 styles in one image).

### The DreamLayer → Unity importer (tooling deliverable, 0 credits)
- **Validated DreamLayer's `unity-sprite-import`** with their own test plan in Unity 6000.6.4f1.
  Every criterion passes: clean compile, menu demo, frames measured at 399–411 ms vs. 400/410,
  loops, no console output. Nobody had run it in Unity before; their docs said so.
- But for a real game it falls short:
  - centered pivot (feet float 0.5 units below the object);
  - pixels per unit hard-coded to 128;
  - uncompressed in WebGL;
  - everything in `Resources/`;
  - one hard-coded folder;
  - loop detection ignores `sampling`;
  - failed-cutout frames ignored;
  - a Python + copy workflow.

  Record: [dreamlayer-unity-validation.md](dreamlayer-unity-validation.md).
- **Built a native importer** (`Assets/Editor/DreamLayer/`, editor-only assembly, README):
  1. **Drop a sprite ZIP in → done:**
     - sliced from the atlas rects;
     - one pivot at the feet, measured from the sheet's alpha;
     - an AnimationClip keyed on each frame's own duration;
     - looping detected properly;
     - DXT5-compressed, no `Resources`;
     - warnings for failed-cutout frames and atlas inconsistencies;
     - a report with **durations in 60 fps frames and anchor jitter** (frame data, Shawn's angle).

     On DreamLayer's own sample: feet at y = −0.004 (vs. −0.5), Animator playback 399–411 ms, jitter 0 px feet / 2 px center.
  2. **Per-folder import rules** for single images (feet pivot for characters, max sizes for WebGL)
     and a **multiple-of-4 warning** (otherwise WebGL ships the texture uncompressed).
  3. **Tools → DreamLayer → Generate Window:**
     - generate / edit / cutout through the official CLI, run without a shell so any prompt is safe;
     - credit confirmation, idempotency key, and a provenance file written *before* submitting;
     - free Recover by execution id;
     - balance, and the provenance of the selected asset.

     It reads the key from the Windows user-level variable, so Unity didn't need a restart.
- Tested end to end without spending: the ZIP import + Animator timing in Play mode; the window's
  balance and Recover paths driven through its real code (re-downloaded the knight test for free;
  imported with the Characters rules + provenance).
- Bugs found and fixed while testing:
  - the auto-import used a deferred call that never fired, so it now imports directly;
  - the old `TextureImporter.spritesheet` API is removed in Unity 6, so slicing goes through the 2D
    Sprite package's data provider.

### Anime style board (1 credit, 98 left)
- Shawn is leaning toward an anime look. I drafted the style in words (no titles or artist names) and
  ran one image with the same knight in four anime styles: TV-anime cel, dark fantasy, fighting-game
  sprite, painterly key visual.
- **Result in 24.7 s:** a clean grid, pure white backgrounds, no ground line, and the knight nearly
  identical across all four panels. That's good news for pose consistency later.
- **But the styles barely separated:** dark fantasy came out as a recolor, the sprite look as a softer
  copy of the cel panel, and only the painterly panel truly differs. The face I asked for was
  hidden behind a closed visor, the fin crest became a plume, and every panel is three-quarter view
  instead of profile. Lesson: a grid prompt buys consistency, not contrast. To compare styles for
  real, use separate generations. Details: [dreamlayer-notes.md → entry 04](dreamlayer-notes.md).
- **Style locked: Shawn picked the top-left panel (TV-anime cel).** Cropped locally as the style
  reference (`ArtRaw/style/style-anime-cel-ref.png`), and the locked style clause in
  [image-api-best-practices.md](image-api-best-practices.md) now uses its wording. So the 1-credit board
  did its job even though the panels were close: it turned "anime?" into one concrete look.
- **Asset list drafted** ([asset-list.md](asset-list.md)): every visual, its reference, and its cost.
  About 63–75 of the 98 credits, leaving ~25–35 for rejects. Everything traces back to two masters
  (hero + knight) generated together on one cast sheet.
- **Hero brief from Shawn:** age 20–30, anime main-character look, very different from the knight, a
  sword-and-magic fighter inspired by the reference anime. The weapon must come from the attack kit, so
  I proposed four kit-driven options.
- **Hero chosen: the spellblade** (option 2). **Knight look kept:** the board's top-left knight with
  the closed helm.
- **Shawn's new mechanic: Harvest.** Parry or clash a boss's elemental spell (fire / ice / wood) to load
  it into the blade, then spend it on the next heavy or riposte: fire = damage, ice = freeze, wood =
  root + heal. It turns the parry, the core skill, into the source of the hero's magic. Design notes
  in [design/boss-01.md](design/boss-01.md). Art consequence: the hero's teal runes appear nowhere
  else on him, so a shader can recolor them per element for free.
- **Free cutout, improved:** the anime knight cut out cleanly, plume included, but a white gap
  enclosed between the arm and the torso survived (the border flood fill can't reach it). New rule:
  remove enclosed regions that are the exact background color and big enough to survive a 4-step
  erosion. The gap is gone, and the first test's white emblem is still kept. Design rule for the
  characters: no large pure-white areas (cream instead).

### The cast sheet: hero and knight locked together (2 credits, 96 left)
- Instead of designing the hero from words alone, I **edited our approved knight image**: cut him
  out locally, put him on one side of a white canvas, and asked DreamLayer to keep him and add the
  hero beside him. That's the workaround for the one-reference-per-request limit: style and knight
  come straight from the picked panel.
- **First try (52.7 s):** the hero hit almost every point of the brief (silver-white hair, visible
  face, navy high-collar coat, cream shirt, rune bracer, teal rune line on the blade) and matched
  the knight's style perfectly. But his sword crossed in front of the knight's leg, so he couldn't
  be cropped. Drift: a longer coat than asked, plus a second rune band on the upper arm.
- **Retry as an edit of the first (50.2 s), changing only the pose:** the overlap is gone and both
  designs held. New surprise: "feet on the same ground line" was drawn as two literal gray lines
  across the picture. Lesson: describe layout without drawable nouns.
- **Free cleanup handled all of it.** `clean_cutout.py` gained a guide-line eraser (it only touches
  pixels with background above and below), and the masters cut out cleanly: hero 764×1712, knight
  1056×1936. **2 credits → 2 masters.** Details: [dreamlayer-notes.md → entry 05](dreamlayer-notes.md).

### Harvest, built into the greybox (0 credits)
- **Shawn approved the hero master** and the Harvest build.
- **How it plays:** parry or clash an elemental spell and the blade holds its element (an orange,
  ice-blue or green diamond at the sword hand for now; later the rune line's color). The next charged
  heavy fires it as a crescent wave across the arena; a riposte carries it if it lands. Light attacks
  never spend it, and a new harvest replaces the old one.
  - Fire: +50% damage (rounded up), then 3 burn ticks.
  - Ice: the boss's clock stops for 1.5 s. Windups hold, live hitboxes stop, nothing moves.
  - Wood: rooted for 2.5 s (approach and lunges go nowhere), and the hero heals 1.
- **The knight's phase 2 gets a Flame Wave:** a fireball at chest height with the white glint, drawn
  orange. Next to the red, unparryable ground wave it teaches one rule: glint means parry (and steal
  it), red means dash.
- **HUD:** the held element under the health chunks, BURNING / FROZEN / ROOTED next to the boss name,
  and **F3** cycles the element so all three can be tried before the king and the Unknown One exist.
- **Verified by a bot driving virtual input** (`AgentScripts/BotHarvestChecks.cs`), 7/7 on the first run:
  - parried the fireball → holds Fire, "PARRY! +FIRE", no damage taken;
  - fire wave → 8 damage (5 + 3) and 3 burn ticks (32 → 29);
  - ice wave → a Cleave started while frozen held its windup at 0.000 s, then continued after the thaw;
  - wood wave → rooted, the hero healed 3 → 4, a 5-unit Lunge Thrust moved the boss 0.00;
  - a light hit doesn't spend the element; a fire riposte → 9 damage (6 + 3) plus burn;
  - parrying the red slam → "RED = DASH", nothing harvested.

  The existing parry and movement checks produce the same output as before: no regressions.
- **Fixed on the way:** the controls line at the bottom right wrapped and was cut off (older bug).
- **Watch:** one fire harvest is worth ~11 of the boss's 40 HP, and the fight was already short. Tune
  boss HP and the harvest numbers together at the next playtest (all in the Inspector).

### First real art in the fight (art pilot, 2 of 9 credits so far)
- **Pose system:** the greybox boxes now swap for DreamLayer pose sprites by state and attack phase
  (feet planted, flipped to face the opponent, squash/stretch and tints toned down so the drawing
  doesn't smear, the red danger glow kept strong). **F4** flips between art and greybox. Missing poses
  fall back to idle, so art can arrive one pose at a time.
- **Import step** (`Tools/import_poses.py`): local cutout → scale every pose to one shared size using a
  rigid "ruler" → hips centered, feet on the bottom edge → copied into Unity with a provenance file.
  The ruler matters because **DreamLayer reframes every edit**: the hero's slash came back 1.37× larger,
  the knight's windup 0.89×. The hero's ruler is his face (eye line to chin), because his **sword came
  out ~30% shorter in the slash**: the weapon-length drift DreamLayer's own blog warns about.
- **In game:** the knight raises his sword overhead in the windup and the hero lunges in the slash, both
  in the locked anime style. Before/after: `docs/media/boss01-greybox-windup.png` →
  `docs/media/boss01-art-windup.png`.
- **DreamLayer had a rough hour:** 11 of 13 requests failed with "temporarily unavailable" (free). A
  background loop retries with new keys and growing waits. The remaining pilot poses (hero guard +
  charge, knight strike + lunge) and the keep-background run sheet land as the service recovers.
- **Also ready:** `Tools/clean_sprite_zip.py` keys out a keep-background sprite sheet frame by frame and
  rebuilds the ZIP, so the half-price route can feed the same importer.
- **The pilot, finished (7.9 of 9 credits; balance 88.1):** hero idle / slash / charge / run, knight
  idle / windup / strike / lunge, all in game. Lessons:
  - **Rulers:** the knight is measured by helm height, because his master sword points toward the
    camera and made the strike look 37% too big. A prop is only a ruler if it lies flat in the picture.
  - **Scale for gameplay:** at 1.8× the hero, the knight's level thrust went over the hero's head. He
    is now 1.36× (still imposing), with the hurtbox fitted to the drawing. All bot checks unchanged.
  - **The half-price sprite sheet works:** 2.9 credits for a 7-frame run, background removed locally.
    Two fixes on the way: the cells have a 2 px transparent ring that fooled the key, and the run
    hopped (55 px of feet spread). `--snap-feet` puts every frame on the floor, and our Unity importer
    did the rest. The 2 s authored cycle plays 3.3× faster in game.
  - **The hero's guard pose failed 17 times in a row** while everything else eventually succeeded. The
    parry state uses idle + a cyan flash for now.

### The run looked wrong, so it became a combat step (4.3 credits, 83.8 left)
- **Shawn:** "the arm is swinging in a weird way and the leg… not natural." He was right. The run preset
  re-posed the sword in every frame, three frames were leaps, and a sprint played backwards (the hero
  always faces the boss) moonwalks.
- **Free stopgap first:** only the calm frames, played calmer.
- **Real fix:** a side-view reference in a ready stance (1 credit), then a **custom animation prompt**
  (3.3 credits): "combat step, one foot always on the ground, no jumping, the sword stays still, held
  low in front." DreamLayer followed it: the sword keeps one angle in all 8 frames, no hops, and it
  reads right played backwards too. Screenshot: `docs/media/boss01-art-step.png`.
- **Importer fix:** custom animations are labeled `source_canvas`, not `ground_anchor`, so the importer
  centered the pivot (half under the floor). It now measures the feet in character folders whatever
  the label says. A real bug in our tool, found by real use.
- Timing again: the custom cycle is sampled over 5 s; it plays 5.6× faster in game.
- The sprint sheet stays in the project for the lobby, where the hero does turn and run.

### The Animation Gym (0 credits)
- Shawn: animation is one of the most important parts of the game, so it gets its own test bench:
  **`Assets/Scenes/AnimGym.unity`** (`Assets/Scripts/Gym/AnimationGym.cs`).
- **Every clip on its real frame data:** state poses, the movement cycle, every hero attack (plus the
  full light combo), every boss move, step by step, at 60 fps.
- **The timeline shows** startup (amber) / active (red) / recovery (blue), the telegraph cue frame
  (tick above), and where the pose changes (tick below). Click or drag to scrub.
- **Overlays:** hitbox (only on active frames), hurtbox, feet pivot and a 1-unit floor ruler, the
  telegraph glint / red glow, an onion skin of the previous pose, and the other character for scale.
- **Controls:** play/pause, frame stepping, 0.1×–1× speed, flip facing, toggles for squash/lean and for
  applying the lunge distance.
- **Readout:** frame / total, the pose sprite shown, the step's frame data and element, which pose
  covers which phase, and for cycles each frame's ms (and 60 fps frames) plus the in-game playback rate.
- **One source of truth:** the squash/stretch and lean per phase moved into `PoseMotion`, used by the
  fighters and the gym alike, so the gym shows exactly what plays in the fight. All fight bot checks
  unchanged after the refactor.
- **Already useful:** at a glance the hero's light attacks show idle during their 5 startup frames and
  hold the slash pose through 12 recovery frames. The windup → strike pose change lands exactly on the
  knight's first active frame. That's the kind of frame-data read DreamLayer's animation output needs.
  Screenshot: `docs/media/animation-gym.png`.

### Idle ↔ walk, connected (0 credits)
- Shawn: idle and walk "look too different." They did: the idle was the standing master
  (upright, three-quarter view, a crisp 800 px drawing), the walk came from the newer side-on ready
  stance at 256 px, and it was drawn 7% too big.
- **The idle is now the walk's own reference image** (the side-on ready stance, already paid for):
  same stance, angle and sword line. The standing master stays as `hero_stand` for the lobby and
  cutscenes. `idleWorldHeight` follows the new drawing so the hero's pixel scale is unchanged.
- **Exact size:** walk frame 1 *is* that stance, so its height and width give the true multiplier,
  3.64× (633 / 174 px), not the 3.9× estimated from hair-to-chin.
- **Smoothing:** the cycle restarts at frame 1 whenever he starts moving, and idle ↔ walk swaps
  cross-fade over 0.1 s (attack poses keep their snap).
- Left: sharpness. The walk's 256 px frames are softer than the idle drawing. A 512 px regeneration
  would close that (~3.3 credits).
- **Polish compiled and verified:** slash arcs, eased squash/lean, idle breathing and dash afterimages
  all run; every combat bot check is unchanged.

## 2026-10-09 (Fri) — Day 3

### The smoothness batch lands (13.9 credits, 69.9 left)
- **11 new poses and a 512 px light-slash sheet,** all wired and checked in the Animation Gym:
  - **hero:** windup → slash → rising slash → windup → slash as the combo; block as the parry; dash,
    hurt, defeated; the riposte flows block → rising slash;
  - **knight:** the lunge gets a coiled windup (no more 450 ms idle), every big swing ends leaning on
    the sword stuck in the ground (the punish window now reads as "hit him now"), plus hurt,
    staggered, defeated.
- **Light 1 plays a real sprite sequence:** 4 frames from the sheet, retimed onto its frame data with
  the swing on the first active frame (`AttackData.animation`). Lights 2–3 keep single poses, so the
  gym compares the two approaches side by side.
- **DreamLayer notes:** 4 of 11 poses came back facing left (mirrored on import); bowed helms fool a
  head-size ruler (perspective), so body height was used; a reused idempotency key after a failure
  returns a bare `CONFLICT`; the custom slash sequence turns toward the camera and ends in a 3-frame
  hold. Details: dogfooding entry 08.
- Also fixed: the hero's attack assets still carried a default parry-glint cue (an earlier builder edit
  never saved to disk). Invisible in the fight, but the gym showed it.

### Light 1's camera turn fixed, a sharp walk, and sheets that line up with poses (3.3 credits, 66.6 left)
- **The bug:** Light 1's 4th sheet frame turns the hero to face the camera, and it filled the last
  ~125 ms of every Light 1. Now the sheet plays frames 1–3 over startup + active (the swing smear sits
  on the 3 active frames) and recovery returns to the ready stance, which is the sheet's own frame 1,
  so Light 1 also hands off to idle and walk with no jump. (`hero_slash` as the recovery threw the
  head back 445 px, so it lost.) New `AttackData.animationThroughRecovery`.
- **Sharp walk:** the combat-step sheet regenerated at 512 px with the same prompt (3.3 credits, first
  try). It matches the idle's sharpness, but it came back as a different animation: he walked 113 px
  forward out of his cell and the sword tip was cut off in 3 of 8 frames. Fixed locally:
  `clean_sprite_zip.py --remove-drift` puts the cycle back in place (19 px of natural sway left), and
  `Tools/patch_blade_tip.py` rebuilds the three tips from the closest whole frame.
- **A bug that was hiding in the importer:** sprite sheets got a center pivot, but custom-prompt sheets
  keep the reference's framing, so the body sits off-center. Walk frame 1 and Light 1 frame 1 landed
  0.36 units from the idle, a sideways slide every time he started walking or attacking. The importer
  now anchors each sheet on its first frame's hip, the rule single poses already use; all three now
  land on the idle to the pixel.
- Checked in the Animation Gym (every clip has a sprite; captures in `docs/media/light1-walk-fix.png`).
  DreamLayer notes: entry 09.

## 2026-10-10 (Sat) — Day 4

### The world gets its art (6 credits, 60.6 left)
- **Boss 1's arena:** the ruined hall of the sun knights (generated 16:9), with the knight's own sun emblem on
  the banners and the window. **Phase 2 burns it:** a "same scene, now burning" edit fades in over the 1.2 s
  transformation, while the knight shows a new **cracked, molten-armor pose**. That's a ready-made before/after
  for the itch page.
- **The lobby is now a moonlit sanctuary** with the three relics (the knight's sword, the king's crown, the
  Unknown One's eye, all from one generated sheet, cut out for free) on painted pedestals, each with a 2D light
  that follows its state (open / cleared / sealed). The lobby hero uses the fight's real art and walk cycle
  instead of a box.
- **Cover art** for the itch page (cropped to 630×500).
- **Fitting backgrounds to a side-view camera** was the real work: DreamLayer put both floors in the bottom
  11–14% of the image, where fighters need ~20%. `Tools/fit_background.py` stretches the plain floor band and
  trims the top; the lobby got reframed cameras instead. Everything is placed by measured rows, so the feet
  land exactly on the painted floor.
- 6 of 6 kept, 5 on the first try. Drift (re-composed burning edit, ignored "no glow halo", a younger-looking
  hero on the cover, tall pedestals): dogfooding entry 10.
- New: `ArenaBackdrop`, `PoseSet.phaseChange`, `LobbyHero` art mode, `LevelEntrance.glow`,
  `AgentScripts/ApplyWorldArt.cs`, `Tools/import_world_art.py`. Shots: `docs/media/world-*.png`.

### Boss 1, finished (8.3 credits, 52.3 left)
- **The knight walks now** (he used to slide in his idle pose): a heavy 8-frame walk sheet, drift removed,
  timed to his ground speed.
- **Phase 2 has its own poses:** the Shockwave Slam drives the sword into the floor and leaves it there through
  the punish window; the Flame Wave sets the blade on fire in the windup (the telegraph) and drags it along the
  floor to release the wave.
- **Real effects instead of greybox squares** (`ProjectileVisual`, generated at runtime in flat cel bands): a
  flickering wall of fire, a red crest rolling along the floor with afterimages, and crescents in the hero's
  element colors for his harvested waves. F4's greybox view still shows the true hitbox squares.
- **Name card:** "THE FALLEN KNIGHT / First form" over a dark band when you enter from the lobby, skipped on
  instant retries.
- **Release defaults:** the debug readout and hitbox boxes start off; F1–F4 only exist in editor and development
  builds (F3 handed out free elements).
- **Balance:** the scripted bot (perfect parries, attacks every frame) killed him in **12.3 s** against a 1.5–2.5 min
  target. Boss HP 40 → 100 and hero HP 5 → 7: the bot now needs 28 s, which projects to ~1.5–2 min for a good
  human. Starting values for Shawn's playtest.
- All bot suites pass (combat, parry, Harvest, lobby flow). DreamLayer notes: entry 11 (a rejected two-sword slam,
  "walk on the spot" ignored again, small edits that keep framing to the pixel).
- **Bug fix (found by Shawn in the console):** from the second Play session on, the Flame Wave and the slam threw
  `MissingReferenceException` instead of drawing. `ProjectileVisual` cached its generated sprites in static fields;
  this project enters Play mode without a domain reload, so the statics outlived the sprites Unity destroys when Play
  mode ends, and `??=` (a C# null check) didn't notice. It now uses Unity's null check. Also swapped the deprecated
  `DEVELOPMENT_BUILD` directive for `DEBUG`. Verified over two back-to-back sessions: 0 errors, 0 warnings.

### Flash Counter: the clash gets its own move (0 credits)
- Shawn wanted a clash to unlock something extra. Until now a clash (attacking into a hit) gave the same reward as
  a parry, despite being riskier: you commit to an attack and need a 10-frame window.
- **Clash, then dash** (neutral or toward him) within 0.45 s: the hero flash-steps through the knight (invulnerable,
  cyan afterimages), lands on his far side and slashes from behind with a gold arc: 5 damage, 30 stagger, and it
  **breaks his combo** (he flinches in his hurt pose instead of turning around for the next hit). It carries a
  harvested element like the riposte.
- The two defenses now pay off differently: **parry → riposte** (only while he's open), **clash → Flash Counter**
  (any time he's in range, mid-combo included). That makes clashing a combo the expert play.
- Design iterations, caught by the bot before any playtest:
  - The first version used the attack button. It broke clash chains: the Three-Strike's 250 ms rhythm turned
    the second clash press into a counter. Moved to dash, which reads as a flash-*step* and keeps attack free;
    dashing *away* stays a backdash escape.
  - Mid-combo, the knight re-faces the hero every step, so the counter landed and then ate the next hit. Hence
    the combo break (`AttackData.breaksCombo`).
- Tests: three new parry-bot checks (H counter after a clashed Cleave, I mid-combo counter with the rest of the
  combo cancelled, J dash-away stays a dash); every suite passes and the console is clean. The HUD shows
  "FLASH COUNTER! - dash" while the window is open.

### The lobby hero: bigger, and at ease (4.3 credits, 48.0 left)
- Shawn: the hero looked off in the lobby, shorter than the painted altars, and he carried his sword out in a combat
  stance in a quiet sanctuary.
- **A relaxed set just for the lobby:** an idle with the sword sheathed on his back, and a calm walk generated from
  that idle (so frame 1 *is* the idle and the swap is seamless). The fight keeps the combat stance.
- **Proportion:** a lobby-only scale (`LobbyHero.artScale` 1.55): he's 3.2 units tall there, so the pedestal tops reach
  his shoulders and the relics sit just above his head. The walk plays slower in proportion so his feet don't slide.
- The relaxed walk is the first custom cycle that actually stayed in place (0.4 px drift per frame). The idle came
  back on textured paper, so `clean_cutout.py` gained a despeckle step. DreamLayer notes: entry 12.
- Lobby flow bot passes; console clean.

### The lobby set in the Animation Gym, and a foot-locked walk (0 credits)
- Shawn: add the new idle to the Animation Gym and fine-tune it.
- **The gym has a third character, "Hero (lobby)":** the relaxed idle and walk at their lobby size (x1.55) and walk
  speed, read from the Lobby scene's `LobbyHero` when the gym is built, so the two can't drift. The fight hero stands
  beside it at the same pixel scale to compare the drawings.
- **Treadmill view (M on any cycle):** the floor ruler scrolls at the in-game ground speed, so a planted foot that
  doesn't ride the ruler is sliding. It works for the fight hero's step and the knight's walk too.
- **Idle:** the gym's breathing ran on clip time, and a pose clip loops every second, so the breath snapped back
  once a second. It now has its own clock. Breathing moved onto the PoseSet (`idleBreathAmount`, `idleBreathRate`),
  so the gym, the fight and the lobby read the same numbers. The fight's values are unchanged (hero 1.2% / 2.6 s,
  knight 1% / 3.5 s); the relaxed idle breathes slower and a little deeper (1.5% / 3.7 s).
- **Walk, measured with `Tools/foot_travel.py`:**
  - **Hitch:** the planted foot moves 0 px from frame 8 to frame 1 (two feet-together stances), so he stalled once
    a cycle. Frame times are now proportional to the foot's travel, so frame 8 is a 152 ms pass-through.
  - **Skating:** 4 footfalls per cycle, not 2, so the old cycle speed (0.35) played it 1.43x too fast and the feet
    slid. The speed now comes from the measured travel (0.501). At the lobby's 3.6 u/s that's a 1.09 s cycle, about
    220 steps a minute, down from ~316.
  - **Cutout:** a white sliver of background between the legs in frame 8 showed on the dark floor. Re-cleaned with
    `clean_sprite_zip.py --gap-core 2`, which removes only those 219 px; the old settings reproduce the previous
    file byte for byte.
- Walk speed itself is unchanged (`walkSpeedFactor` 0.6). At 0.5 he'd take ~184 steps a minute, if he should look
  calmer still.
- Gym check: every clip of all three characters has a sprite. Lobby flow bot: all 9 steps pass, through the fight.
  Console clean. DreamLayer notes: entry 13. Media: `docs/media/lobby-gym-footlock.png`.

### The white outline around the characters (0 credits)
- Shawn: "why is there a white cut out around it".
- **Cause:** the art comes from DreamLayer on white. The line art's anti-aliased edge is half outline, half white;
  our local key (`clean_cutout.py`) dropped only near-white pixels, so those half-blended pixels stayed fully
  opaque and read as a white outline on dark scenes. Half the edge pixels on the sprite sheets were light gray, three
  quarters on the relics. The cutout tool's own fringe metric missed it (its threshold only counted near-white).
- **Fix:** `Tools/defringe.py` un-blends each edge pixel against the white ("color to alpha"), in a 2 px band that
  stops at the dark outline, so nothing inside the line art changes. Applied to all 33 locally keyed assets (5
  sheets, 3 relics, 25 poses); no `.meta` changed, so every reference held. It's now the default last step of the
  cutout tool, and the fringe metric measures the real thing (2,768 → 3 fringe pixels on the lobby idle).
- Side note: the solid green box in the gym is the hurtbox overlay (H), not the art.
- Checked in the lobby and the gym; console clean. DreamLayer notes: entry 14. Media:
  `docs/media/defringe-before-after.png`.

### The lobby gets a floor (0 credits)
- Shawn: the lobby felt off, as if the hero were levitating: the illustration had no real floor.
- **Why:** the painting's floor was a thin strip, and the hero stood on the pedestals' base line while drawn in
  front of them. Overlap and foot line disagreed about his depth, and he had no shadow.
- **Fix:**
  - The floor strip in front of the pedestals (below their base outline, row 1302) is stretched 2x with
    `fit_background.py` and the top trimmed 138 rows. The pedestals, wall and arches keep their drawing.
  - He stands 25% into that floor, about half a unit in front of the pedestals, so overlapping them now matches
    where his feet are. The explore camera shows more floor under him (ground 18% up the screen, was 14%).
  - A soft contact shadow under his feet (`ContactShadow`, generated at runtime, widens a little while walking).
  - The title shot narrows to the painting's width when the screen is wider than 16:9 (a wide fullscreen window,
    or the Editor's Game view), which also removes thin dark bars at the edges in Editor captures.
- Lobby flow bot: all 9 steps pass, through the fight. Console clean. DreamLayer notes: entry 15. Media:
  `docs/media/lobby-floor-before-after.png`.

### A calmer lobby walk (0 credits)
- Shawn: slow the lobby walk down; the arm swing and the walk looked unnatural.
- **Why it looked off:** the sheet packs 4 footfalls into 8 frames, unevenly spaced (2, 1, 2, 3 frames apart). Frames 8
  and 1 are both feet-together stances, so the gait stuttered once a cycle, and between frames 4 and 5 the arms jump
  from one extreme to the other. At 3.6 units/s that was ~235 steps a minute: the arms flailed.
- **Fix:**
  - The loop is now frames 8,2,3,4,5,6,7. Frame 1 is the idle itself; starting on 8 (feet together, arms down) makes
    idle -> walk seamless, and the double stance is gone.
  - Re-foot-locked for that order (`foot_travel.py --order`, read by `ApplyArt.ApplyFlipbook`): cycle speed 0.537.
  - Lobby walk speed 3.6 -> 2.4 units/s (`walkSpeedFactor` 0.6 -> 0.4): ~157 steps a minute, frames ~120-290 ms.
- The arm reversal between frames 4 and 5 is in the drawings; a regenerated walk with an even gait would fix it.
- Lobby flow bot: all 9 steps pass. Console clean. DreamLayer notes: entry 16. Media:
  `docs/media/lobby-walk-before-after.gif`.

### Sound: the game gets a voice (0 credits)
- Shawn: audio is a big part of the feedback; it had none.
- **System:** `GameAudio.Play(Sfx.X)` from anywhere, created on first use from `Resources/SoundBank` and kept across
  scenes (music carries from the lobby into a fight, with a cross-fade). Effects ignore time scale, so hit-stop freezes
  the picture, not the impact. A pool of 16 sources; each cue picks a random clip plus an optional layer under it,
  with slight pitch variation and a cooldown so bursts don't stack.
- **Data pipeline:** `Tools/build_sound_bank.py` copies only the pack files used, synthesizes the rest
  (`Tools/synth_sfx.py`), and **measures each clip's loudness** (loudest 50 ms RMS) to set cue volumes per category:
  parry loudest (it must feel unlike anything else), then heavy hits, stings, hits, cues, swings, footsteps.
  `AgentScripts/BuildAudio.cs` turns that spec into the SoundBank asset and sets import settings.
- **Sources:** 3 Kenney CC0 packs (impacts, footsteps, cloth, sword draw, UI); everything a pack doesn't have is
  synthesized in numpy: sword whooshes, the parry / clash rings (struck-bar partials), the flash counter shimmer, the
  telegraph glint and danger snarl, fire, low booms, charge hum, harvest chime, relic swells, win / lose stings.
  The first pass had clicks (sounds cut off mid-ring) and hissy whooshes; a spectrogram review caught both.
- **31 cues hooked:** every hero action and hit, parry vs. clash, harvest, the boss's telegraph (a glint = parry,
  a snarl = dash: the danger language is now audible too), swings, slam, flame wave, burn, stagger, phase change,
  death, armored footsteps on the approach, lobby footsteps paced by the measured step length, relic charge / enter /
  sealed, title confirm, the boss name card, victory and defeat.
- Verified: combat, parry and harvest bots, the lobby flow and forced death / phase change fire all 31 cues; console
  clean. 3.9 MB of audio. Music: candidates found (CC0), waiting on Shawn's pick.

### Music: three loops, leveled to sit under the combat (0 credits)
- Shawn approved four CC0 tracks from OpenGameArt to compare in-game: *Epic Boss Battle* (Juhani Junkala) for the
  fight, *Determined Pursuit* (Emma_MA) for phase 2, *Artifact Room* (Fupi) for the lobby, *Cathedral in the forest*
  (congusbongus) as the alternate lobby track. Credits: [credits.md](credits.md).
- **The tracks were mastered 8 dB apart** (Epic Boss Battle -6.8 LUFS, Artifact Room -15.2), so one music volume
  would have made the fight blast and the lobby whisper. `Tools/prepare_music.py` measures integrated loudness and
  only turns tracks down: fights to -16 LUFS, the lobby to -19, with the game's music volume on top.
- **Seamless loops stay seamless:** the tool decodes each OGG again and compares the sample count to the source. The
  machine's first ffmpeg on PATH (a 2013 build) padded 80-256 samples onto three of the four files (a click at
  every loop); ffmpeg 8.1 matches exactly. 45 MB of downloads became 9.8 MB of OGG.
- **Phase 2 gets its own music:** the boss's phase change cross-fades to the phase-2 track; a boss without one keeps
  its fight music. A retry goes back to the phase-1 track; a retry in phase 1 doesn't restart the music.
- Verified in play: lobby, fight and the phase-2 switch each play the right loop at the music volume; console clean.

### Boss 2, the King: designed and playable in greybox (0 credits)
- Shawn: Boss 2 is a must. Design: [design/boss-02.md](design/boss-02.md). Placeholder name "The Frozen King".
- **He teaches positioning.** The Knight asks "can you read the glint?"; the King asks "where are you standing?". He
  walks slowly, backs off to throw **ice lances** (parryable: harvest ice, then freeze him), and makes **ice erupt
  where the hero stands**, marked on the floor first. That's the floor-marker cue from the danger language, unused in
  fight 1. Phase 2 adds a three-lance volley, a second eruption that follows the hero, and a marching line of spikes.
- **One villain, felt in the hands:** his cleave keeps the Knight's exact 700 / 900 ms timing, and the volley keeps the
  Knight's combo rhythm (400 ms, then a hit every 250 ms; parry all three for a stagger).
- **Almost all data:** 12 attacks and 6 moves (`AgentScripts/BuildBoss02.cs`), a scene copied from Boss 1, and the
  lobby's crown relic opened. The systems gained 3 small things: area attacks that land on the hero's spot
  (`AttackData.atHero`), a line attack that keeps its direction (`BossMove.lockFacing`), and **parried projectiles now
  fill the stagger meter** (they filled nothing before, so the Knight's Flame Wave parry was worth less than a swing).
- **Verified:** the fight bot (taught to parry projectiles and step out of floor marks) wins in 32.2 s at 4/7 HP; it
  took hits from Twin Rise and from the volley when it got greedy after the first parry. Boss 1 bots all unchanged
  (combat, parry A-J, harvest A-G). Lobby flow now 10 steps: the eye stays sealed, stopping at the crown enters the King.
- Name card, music and the result screen work as-is. The greybox floor mark (a thin red bar) is too subtle; the art
  pass replaces it with a frost patch. Art plan: ~14-15 credits, waiting on Shawn's approval and the story beat.
  Media: `docs/media/king-greybox-glacier-rise.png`, `king-greybox-frost-lance.png`.

### The flash counter, easier to trigger (0 credits)
- Shawn: the clash's special attack is too hard to trigger. Two causes were in the code, not the player:
  - **The clash's own freeze ate the input.** A clash freezes the game for 9 frames (150 ms), but the input buffer
    timed presses in real time (120 ms). Pressing dash the moment the clash flashed, the natural reaction, often
    expired before the game moved again. Presses are now timed on an input clock that stops during hit-stop, which
    fixes it for every freeze in the game, not just the clash.
  - **Attacking again after a clash locked the reward out.** Clashing comes from attacking, so the next press is
    usually another attack; that swing couldn't be dashed out of, and the 0.45 s window ran out during it.
    Now a dash during a swing, a charge or a parry stance cancels into the counter while the window is open.
- **Window 0.45 s -> 0.7 s.** One reward per clash: starting a riposte, or getting hit, closes the window.
- **A visible "now":** a gold diamond with a dark rim pops over the hero at the clash and fades out over the window's
  last 0.2 s, in the danger language's glint shape (the boss's white glint = parry now; the hero's gold one =
  counter now). A gold tint on the body was tried first: on dark navy art a color tint barely shows.
- Verified: parry bot gains K (attack after the clash, then dash: the swing cancels into the counter) and L (dash
  pressed inside the freeze, timed in real time: counter fires). The old check H had pressed after the freeze, so it
  never caught the bug. A-J, combat and harvest bots unchanged. Media: `docs/media/flash-counter-glint.png`.

### The final boss, the Unknown One: designed and playable in greybox (0 credits)
- Shawn asked for my ideal final boss, then "do it". Design: [design/boss-final.md](design/boss-final.md).
- **The exam:** the Knight taught parry, the King taught positioning; the final form asks for both at once (Bind and
  Strike marks your spot, then lunges at where you stepped: move, then parry) and pays off harvest. All three elements
  appear, and wood, the only heal in the game, is earned by parrying a tendril lash. Phase 2's volley throws fire, ice
  and wood in the Knight's combo rhythm: the last lance you parry is the power you keep.
- **Story through the danger language:** the white glint is the knight's honor (he always shows his strike); the god's
  own attacks never glint, they mark the floor or glow red. The knight's sun emblem becomes the god's eye in phase 2.
- **Finale:** at 15% the knight inside holds the god still for 4 s ("The knight holds him. Strike!") with his glint
  showing one last time, and the hero lands the final blow. Built as `BossConfig.finaleAtHealth` (any boss can use
  it), with a gold threshold mark on the health bar.
- **Almost all data:** 14 attacks and 8 moves remix the Knight's and the King's (`AgentScripts/BuildBossFinal.cs`), a
  scene copied from the King's greybox, and the lobby's eye relic opened (sealed until both bosses are cleared).
- **Verified:** three bot fights won in 38-45 s with 4-7/7 HP, wood heals in each; in a full fight the finale held
  him at 16 HP and the bot finished him 2.8 s later. A scripted check: held at 17/120 (not 20), exactly 4.00 s, never
  twice. Lobby flow unchanged (10/10). Media: `docs/media/final-greybox-finale.png`.
- Art plan: ~10-12 credits, waiting on Shawn's approval and the story beat. It's the first cut if time runs short.

### The final form gets a leap; boss spells now land on the hero (0 credits)
- Shawn: "final form is too easy, make something special like jumping up and smash for more damage", and "for root,
  ice and fire, when the enemy fires them I want the effect to be on the player".
- **Leap Smash:** he jumps out of reach (the hurtbox goes with him), his floor mark follows the hero, locks 20 frames
  before landing (bright mark + the danger snarl), and he lands for 3 (of 7 HP). Phase 2's **Heaven Fall** adds a
  red-glow slam 0.3 s after landing that sends a shockwave each way. First idea was waves on the landing frame; they
  would have spawned on top of a hero who just dodged, so the slam became its own telegraphed beat. Plus heavier hits
  (cleave and bind strike 2 damage) and shorter gaps. Bot: 47-48 s with 1-7 HP left (was 38-45 s).
- **Boss spells on the hero,** each the mirror of what harvest does to him, each with its own answer: fire burns
  (2 more ticks; **a dash puts it out**), ice chills (half walk speed for 2.5 s), wood roots (no walking or dashing for
  1.2 s; parry what comes next). The root is shorter than the boss's fastest follow-up into a floor attack, so a root
  never makes a hit unavoidable. This also reaches the Knight's flame wave and the King's lances.
- New bot `AgentScripts/BotSpellChecks.cs`: the leap (out of reach, the mark tracks then locks, 3 damage on a hero who
  stays, none on one who walks out after the lock, a shockwave each way) and the three statuses (fire 7 -> 5 after a
  tick, a dash puts it out; walk 6.0 -> 3.0 when chilled; rooted: no walking or dash). All pass; Knight bots unchanged.
- **Console clean-up:** the "no audio listeners" log (up to 2,000 lines per test run) came from scene loads: the
  camera's listener left with the old scene while the music kept playing. The sound system now carries the one
  listener across scenes. Four scene loads: 0 logs, 0 warnings, 0 errors. The builders also stopped opening the lobby
  next to a boss scene (two global lights = an error).
- Note: the King bot lost 2 of 3 runs to floor eruptions, while it was busy mashing ripostes (it can't walk mid-swing).
  It's the bot's greed, not a change here; Shawn's playtest will say whether the King is too hard.

### Lobby walk: a bit faster (0 credits)
- Shawn: increase the lobby walk speed. 2.4 -> 3.0 units/s (`walkSpeedFactor` 0.4 -> 0.5), ~196 steps a minute: between
  the slow 2.4 and the 3.6 where the arms flailed. The walk stays foot-locked and the footsteps follow the distance, so
  both speed up with it. Lobby flow bot 10/10.

### The crown waits for the Knight (0 credits)
- Shawn: block the King's crown until the Knight is beaten. The crown relic now requires `boss01` ("Sealed - defeat the
  Knight first"), so the order is Knight -> King -> the Unknown One. Lobby flow bot gains step 3b (the crown refuses
  entry before the Knight falls) and still enters the King after the win: 11/11.

### The story: one villain who can't accept losing (0 credits)
- Shawn's arc: the knight is beaten (fight 1), so he goes looking for power; power makes him king; the crown isn't
  enough, so he seeks power from the Unknown One and becomes its body. Two small additions tie it to what was built:
  **why ice** (he froze his court so no one could take the crown from him) and **why the knight holds the god at the
  finale** (at the very end, the knight he was chooses to lose). The arc starts with a man who never lost and ends
  with him choosing defeat.
- **In the game as text, no cutscenes:** entering a relic now fades to a story card (relic name + one or two lines,
  on screen for 1.5 s + 0.3 s a word, any key skips) before the fight loads; retries skip it. After the final blow,
  black falls over the arena and three epilogue lines appear one by one, then "Thank you for playing", over the
  unused fourth track (*Cathedral in the Forest*) as the ending music. A press shows every line at once, but only
  after the ending starts (a player still mashing from the kill doesn't skip it).
- All the text lives in one file, `AgentScripts/BuildStory.cs`. The eye relic is now "The Eye Beneath the Throne"
  (was "???").
- Verified: lobby flow bot 13/13 (story cards for the Knight and the King, skipped with a key); a scripted ending
  check (a press at 1 s doesn't skip, the ending music plays, a press at 3.6 s shows every line, the next returns to
  the lobby with the final cleared); console clean. Media: `docs/media/story-card-king.png`,
  `docs/media/ending-card.png`.

### Solid bodies: the hero and the boss collide (0 credits)
- Shawn: "make it so me and the enemy can collide". Until now they walked through each other.
- Now the hero stops at his body when walking in, and his lunges stop at the hero (the hit still lands). Rule: whoever
  moved into the other this frame is the one stopped, so neither shoves the other into a wall; if nobody did (he
  landed on the hero from a leap), the hero is eased out at 10 units/s. A wall never traps anyone: what one side
  can't give, the other does.
- **The dash goes through him** (and the flash counter's step, which is built on crossing him). With solid bodies, a
  cornered hero needs a way out, and the dash's invulnerable frames are already "the safe answer to everything".
  It's one toggle (`HeroConfig.dashPassesThroughBoss`) if playtests say otherwise. A boss up in a leap doesn't block.
- No spacing changes needed: every move's closest preferred range (1.5) is beyond body contact (1.15 for the
  Knight, 1.4 for the King and the final form), so he never walks into the hero on his own.
- New bot `AgentScripts/BotBodyChecks.cs`, measured at the end of each frame (what's drawn): walking in stops at
  exactly 1.15; the dash comes out 2.35 past him; his lunge never gets closer than 1.15; overlapping at the wall
  ends 1.15 apart with the hero still inside the arena. All earlier bots unchanged (core, parry A-L incl. the flash
  counter crossing him, harvest A-G, spells A-G); Knight bot 3/3 wins in 26-29 s, final form won in 47.8 s with the
  finale.

### A story beat after each win (0 credits)
- Shawn: after the first boss, a quick story line to introduce what comes next. Beating the Knight only said
  "VICTORY", so his fall into the King happened off-screen between two cards.
- Now the first win over the Knight plays three lines over the darkened arena ("The knight fell to one knee. For the
  first time in his life, he had lost." / "He swore he would never be weak again." / "He went looking for power."),
  then a gold line pointing at what opened: "A new relic wakes: the King's Crown". The King gets the same bridge to
  the Unknown One ("...Beneath the throne, something answered."). Calm lobby music takes over from the fight track.
- **First win only:** a replay shows the normal victory banner (the story is told once; the final ending plays
  every time). Same skip rules as the ending: mashing from the kill doesn't skip it, a later press shows every line.
- The relic cards were rewritten so the two don't repeat each other: the crown card now starts at "Power made him
  king", and the eye card sets up the finale ("He said yes. The Unknown One wears his body now, but somewhere inside,
  the knight remembers.").
- Built on the ending's code (`FightFlow` gained a closing line, a first-win-only flag and a music pick); all text
  still in `AgentScripts/BuildStory.cs`. Verified: lobby flow bot 14/14 (the story after the Knight, a press at 1 s
  doesn't skip it, a press at 3.5 s shows it all, the crown opens); a replay win shows the banner; console clean.
  Media: `docs/media/after-knight-story.png`.

### The name: Only Boss Fights: The Fallen Knight
- Shawn's pick after a round of options (Last Glint, Best of Three, Sore Loser, Kneel...): he wanted something catchy,
  and "Only Boss Fights" says exactly what a jam player gets, in the spirit of *Only Up!*. The subtitle carries the
  identity (the knight, his fall), which the main title can't.
- Title screen: "Only Boss Fights" with "The Fallen Knight" in gold under it. Unity's product name is "Only Boss
  Fights - The Fallen Knight": a colon there would break, because the product name becomes a folder name on Windows
  (save data, standalone builds).
- Lesson: I recompiled while Shawn had just entered Play mode. Unity hot-reloaded the running game, kept the old
  GUI styles, and the new subtitle style came back empty (two errors on the title screen). Fixed so the styles
  rebuild if any is missing, and I now check Play mode before every recompile, not only before scene changes.

## 2026-10-11 (Sun) — Day 5

### The King's art: reference, 7 poses, the throne room (13 credits)
- Shawn approved ~15 credits. **Reference (4 credits):** t1 had a floor-length fur cape; Shawn rejected it ("hard to make
  it move naturally": with ~10 poses and tweens, a cape changes shape completely between poses). t2 dropped the cape
  and restated the style clause (flat cel shading came back). Shawn: more jewelry, bulkier, a fancier armor than the
  Knight's. t3 delivered that but added loose chains round the legs, lost the sword guard and came back on textured
  paper; one corrective edit (t4) fixed all three. Approved.
- **Poses (7 credits, 2 free failures):** a 3-pose pilot (idle = the reference, cleave windup, cast), then cleave
  strike, floor cast, hurt, defeated, phase 2. Every anchor held and every edit came back on the reference's canvas,
  so all King poses got one shared scale (0.647) with no per-pose ruler (wrong: see "The King changed size" below). Failures: one outage (retried with the same key) and
  one content refusal for "HURT, recoiling from a hit" ("reeling, knocked off balance" passed; the Knight's hurt had
  the same problem).
- **Each move family has its own silhouette:** sword raised = the cleave (parry), palm thrust = a lance (parry or
  dash through), sword planted in the floor = the floor erupts (move). The pose says which answer is coming before the
  cue does.
- **Throne room (2 credits):** calm and frozen-over versions; the floor line held between them (row 1116), so they
  crossfade at the phase change. Painted very pale, like the King, so it's tinted much darker in Unity for contrast.
- **Cleanup fixes (free):** the pale frozen blade's broad white highlights were being removed as enclosed background
  (holes in the blade): only gaps with a large solid core are removed now (`--gap-core 16`, 40 for the planted
  sword). New `--floor-shadow` clears a drawn grey floor shadow (cleave strike) without touching the boots.
- Wired by `AgentScripts/ApplyKingArt.cs` (PoseSet, 32 attack poses, hurtbox 2 x 3.35 fitted to the art, backdrop);
  `BuildBoss02` now keeps the art if re-run. Bot: 2 wins out of 2 (38 s, 48 s; the greybox went 1 of 3). Media:
  `docs/media/king-in-game.png`, `king-poses.png`, `king-ref-*.png`.

### The King's floor attacks are ice, not red (0 credits)
- Shawn: "change the red floor attack to ice spikes". The greybox drew them as a thin red bar during the windup and a
  red sword-trail arc on the hit, which also blurred the danger language (red means dash).
- Now: a frost patch spreads and brightens under the target through the windup (pulsing once the danger cue sounds),
  then cel-shaded ice spikes burst up across the hitbox, center first, with back spikes behind the fighters and front
  ones in front for depth; they hold while the hit is live and shatter. Generated at runtime like the contact shadow
  (`IceSpikes.cs`), switched per boss by `BossConfig.floorStyle` (the Unknown One keeps the marker for now).
- Gameplay unchanged (same hitboxes and timing); King bot after the change: one clean run lost with the boss on 3/100,
  the same floor-eruption greed as before. Console clean. Media: `docs/media/king-ice-spikes.png`.

### The King changed size between poses: per-pose rulers (0 credits)
- Shawn: in the King fight "some of the boss's pictures have different size". He was right: I had given every King
  pose the reference's scale because the edits came back on the same 1600 px canvas with the helm and crown looking
  the same size on a contact sheet. Same canvas is not same scale.
- Measured against the idle on rigid parts: template matching of the idle's helm + crown, knee medallion, sabaton,
  belt gems and pauldron sun over scale 0.7-1.5 and rotation +-45 (numpy NCC, no new dependencies), plus opaque /
  gold / red area, then candidate rescales checked side by side. Drawn bigger than the idle: **cleave strike 22%**
  (helm, crown and sabaton agree), defeated 14% (kneeling at 85% of his standing height), floor cast 12%, hurt 10%,
  cast 6%, windup 3%; the phase-2 pose matches (crown and belt at 1.00, the ice adds bulk on purpose).
- Fix: each pose's ruler in `Tools/import_poses.py` is now 100 x that factor, re-imported (no Unity wiring changes:
  same files, same GUIDs). In-game world heights: idle 4.6, cast 4.4, floor cast 4.3, hurt 4.5, windup 4.8 (sword
  raised), strike 3.9 (crouched), defeated 3.4 (kneeling). Console clean.
- Lesson: measure every edit against the reference, even when it "looks the same". The Knight's poses were measured
  one by one and held; the King's were assumed and drifted up to 22%, which is DreamLayer's own published range
  (15-20%). Media: `docs/media/king-pose-scale-fix.png` (before / after), `king-pose-scale-in-game.png`.

### UI pass: from greybox labels to a styled HUD (0 credits)
- Shawn: "let's fix up the UI first". Every screen was still the greybox: Unity's default font in plain white with
  no shadow (the defeat stats over the arena's bright window, the relic labels on the floor), flat colored bars, and
  a single line of control text.
- New `UiKit` (shared by the fight HUD and the lobby, still IMGUI): text with a drop shadow (big text also gets an
  outline), dark bands that fade at their edges behind the name card, the result banner and the title, gold rules,
  framed bars with a gold rim and a pale "ghost" that shows damage just taken and drains after a short hold, diamond
  health pips that flash when lost, and key caps for every control hint (bright as a fight starts, dimmed after
  8 s). Relic labels in gold with a cleared mark; the defeat title in red, victory in gold. Any font placed at
  `Resources/UI/DisplayFont` is picked up for titles and names (none yet: Unity's default).
- Bug on the way: `Mathf.SmoothStep(a, b, t)` is an eased lerp from a to b, not the shader smoothstep, so the bands
  came out at less than half their darkness. And an 8-way outline muddies 12-16 px text: small text gets a shadow only.
- Checked on all 8 screens (title, lobby, relic story, name card, fight HUD, after-win story, victory, defeat) in
  Play mode; console clean; saved progress reset afterwards. Media: `docs/media/ui-before.png`, `ui-after.png`.

### The hero runs in the lobby (3.3 credits)
- Shawn: "switching the lobby walk to running would make more sense". At 3.0 units/s, crossing the hall took ~9 s.
- One sprite job (8 frames, 512 px, background keep, from the lobby walk's reference, so it matches the idle and the
  sword stays on his back): first try, 93 s. Frame 1 is the standing reference; frames 2-8 are the loop, one long
  stride with a real flight phase and a shorter step.
- Cleanup (free): background keyed locally, drift removed, and a new `--airborne` option in
  `Tools/clean_sprite_zip.py`: stance frames put the planted foot on the floor, airborne frames move with their
  neighbors so his feet leave the ground instead of the body dipping mid-jump.
- Timing: the walk is foot-locked, but this run can't be: "on the spot", the planted foot never slides back (0 px over
  three stance frames), so there's no travel to measure. It plays as an even 0.72 s loop at 5.4 units/s (~2.8 steps a
  second, a light jog), with two footsteps per loop. The lobby speed is 0.9x the fight's (was 0.5x).
- Checked in Play mode (steady 5.4 units/s, frames cycling, same size as the idle, shadow under him in the air) and
  with the lobby flow bot: running past a relic still doesn't enter it, stopping does, sealed relics still refuse,
  both fights load. Console clean. Balance 31.7. Media: `docs/media/lobby-run-in-game.png`, `lobby-run-sheet.png`.
- **Fine-tuned (Shawn: "looks weird"):** the 7-frame loop was one wide leap followed by four near-identical
  knee-up frames, so it galloped: leap, shuffle, shuffle, shuffle. The loop is now one stride repeated, frames
  2-3-4-5 (pass, push off, airborne, push off), 4 frames at ~11 fps, one footstep per loop. Head steady within 2 px
  vertically and 4 px sideways; the 1.93-unit step matches the stride drawn, so no skating.
- **The run is in the Animation Gym:** Tab to "Hero (lobby)", then ↑/↓ between "Run (in the game)" and the
  alternatives (two strides, 2-8 as delivered), all at the lobby's size, speed and cadence; `[` `]` nudge the
  playback pace 5% at a time and the readout shows the steps per second, so the pace can be tuned by eye; M scrolls
  the floor ruler at the run speed. Media: `docs/media/lobby-run-fixed.png`.
- **Slower (Shawn: "the play rate is too high"):** 2 steps a second at 4.8 units/s (was ~2.8 at 5.4): 0.125 s a frame,
  and a step now covers 2.4 units against the ~2.2-unit stride drawn, so the feet still don't skate. Also from
  Shawn: "the arm is always up". True: the front arm is raised in every frame of the sheet, so no retiming fixes it;
  it needs a new sheet whose prompt asks for the arms to swing (~3.3 credits, waiting on Shawn).

### The Unknown One's art: the King, taken by the god (14 credits)
- Shawn: "we need to work on the final boss". Approved in two steps: 3 credits for two reference options and the
  arena, then 10 for the poses and the phase-2 arena once he'd picked a look, then 1 retry.
- **The reference is an edit of the King's** (story: the King went to the god for power and became its body), so the
  chain knight -> King -> Unknown One is literally one image edited three times. What survives every form: the
  closed helm, the red plume, the crown (now cracked), the sword (now black iron). **The sun becomes the eye:** the
  King's gold sun crest is now a gold sun around a black eye, the same golden slit-pupil eye as the lobby relic.
  Two options: A (armor corrupted, short tentacles) reframed itself into a waist-up close-up despite "keep the
  framing"; B adds a huge clawed tentacle arm in place of his free arm. Shawn picked B: the claw is the god's, the
  sword is the knight's, which is exactly the fight's danger language (the knight's sword glints: parry; the god's
  claw marks the floor: move).
- **9 poses, all first try:** cleave windup and strike, lash (claw whipping forward), floor cast (claw sunk into the
  floor), hurt, defeated, phase 2 (the eye opens wide, tentacles burst out, gold cracks), plus two for the leap
  Shawn asked about ("for the smash it would need a special animation right"): airborne and the landing crouch.
- **The arena is the King's throne room, taken:** an edit of arena2 (tentacles through the walls, banners torn, the
  sun emblem is now a weeping eye). Phase 2 is an edit of that: the far wall rips open onto a violet void with a giant
  golden eye. Calm -> frozen (fight 2) and calm -> taken -> torn (fight 3) are one room, one before/after chain.
- **What drifted:** the tentacle arm is the least stable part. It shrank to normal size in the windup and the leap,
  and the lash grew a second one. A retry with "exactly one tentacle arm and exactly one claw" fixed the count but
  drew the arm as armor; Shawn kept the first (the striking arm is on model; the extra reads as one more tentacle).
  Two poses drew things that weren't asked for: a grey floor shadow (strike) and a black shadow pool plus a red burst
  under the claw (floor cast; red means dash in our cue language). All erased locally, for free.
- **Scale, measured before import this time** (the King lesson): poses are drawn 0.85x (windup) to 1.22x (strike,
  hurt) the reference, by helm + crown, chest eye, knee and boot matches and side-by-side checks; per-pose rulers in
  `Tools/import_poses.py`. The hip anchor is set per pose too: the claw arm hangs through the hip band and would
  have pulled the automatic anchor sideways.
- **The smash animation (code, 0 credits):** the leap now gathers in a 0.12 s crouch (the landing pose doubles as
  the take-off), stretches on the way up and on the drop, squashes flat on landing, and the landing throws a dust ring
  along the floor, stone chips and a crack that stays through the punish window (`GroundImpact.cs`, generated at
  runtime). Beats in `PoseMotion` (shared with the Animation Gym).
- **Wiring:** `AgentScripts/ApplyFinalArt.cs` (PoseSet, 42 attack poses, hurtbox 2 x 3.5, the taken throne room as
  calm / torn); `BuildBossFinal` keeps the art if re-run. He stands 4.8 units tall (the King 4.6, the hero 2.2).
- **Verified:** spell and leap checks A-G pass with the crouch (out of reach in the air, the mark tracks then locks,
  3 damage on a hero who stays, none on one who leaves, shockwaves both ways, burn / chill / root); bot fight won in
  33.5 s with 6/7 HP and the finale; console clean; Shawn's progress restored. Balance 17.7.
  Media: `docs/media/final-ref-chain.jpg`, `final-poses.jpg`, `final-arena.jpg`, `final-in-game.png`,
  `final-phase2-in-game.png`, `final-leap-landing.png`.

### The god's tentacles: floor attacks, the wave and the root (0 credits)
- Shawn: "what about the wave attack?" and "the root would also be tentacle related right. not green". The final
  fight still borrowed the Knight's red shockwave, the greybox floor marker, and a green tint for the root.
- **`Tentacles.cs`:** tentacles drawn at runtime in the cel style of his DreamLayer art (dark purple, lilac suckers
  on the inner curve, dark outline, lit from the upper left), 6 curl variants drawn once at fight load; a tentacle
  writhes by stepping through them (limited animation, like the anime it imitates) and bursts up / pulls back.
- **Floor attacks** (Grasping Ground, Bind, Procession, Maw): a dark violet pool spreads under the target through the
  windup (pulsing on the danger cue), then a cluster of thick tentacles bursts up across the hitbox, writhes while
  it's live and pulls back (`BossConfig.floorStyle = Tentacles`, like the King's ice spikes). The leap keeps its
  tracking mark.
- **The wave** (Shockwave Slam, Heaven Fall): tentacles burst up one after another where it passes, tinted red over
  the red crest, so "red = dash" still reads (`TentacleWave.cs`).
- **The root:** three tentacles burst up and hold the hero's legs for as long as he's rooted; the green tint and the
  green band at his feet are gone (a violet "grip" color in the HUD). His lash and the volley's wood lance stay green:
  that's the harvest color (parry it and the blade holds wood).
- Verified: spell / leap checks A-G pass; bot won in 34.4 s with the finale; console clean. Media:
  `docs/media/final-tentacles.jpg` (grab, root, Maw), `final-tentacle-wave.jpg`.

### Lobby run v2: the arms swing (3.8 credits)
- Shawn on the first run: "it looks like the arm is always up". True in every frame of that sheet, so no retiming
  could fix it. Regenerated from the same reference with the swing spelled out (each hand forward to chest height,
  back past the hip, opposite the legs, never above the chest), and with **9 frames instead of 8**: DreamLayer returns
  the reference pose as frame 1, so 9 leaves an even 8-frame loop (two steps of four) instead of the 7 that made the
  first loop uneven.
- **Result:** the arms move now (the back hand swings behind the hip, both drop by the hips between steps), still
  fists though; the legs changed character, an upright high-knee jog on the spot instead of a leaning stride. In the
  lobby since Shawn asked "did you put it in": loop 2-9 at 2 steps a second, 4.8 units/s (`UseRunV2` in ApplyArt
  switches back). Both runs are in the Animation Gym ("Run v1 (arm up)", the other loops of v1).
- Verified in the lobby (8 shots running right: 4.8 u/s, size and feet as before); console clean; progress restored.
  Balance 13.9. Media: `docs/media/lobby-run-v2-compare.png`.

### Back to the walk (0 credits)
- Shawn, after both runs: "just use walk ... it looks the most natural. those all look off the running, I think is
  the problem of gen ai sprite". The lobby walk is back exactly as before the runs: frames 8, 2-7, foot-locked (frame
  times follow the planted foot, so the feet don't skate), 3.0 units/s, 4 footsteps per loop. Verified in the lobby
  (steady 3.00 u/s); progress restored.
- The two run sheets (7.1 credits together) stay in the Animation Gym, labeled "not used", as the record of why.
- **Lesson (Shawn's call: "those all look off the running, I think is the problem of gen ai sprite"):** a walk is
  slow enough that every frame can be a clean key pose with a planted foot; a run is mostly in-between motion
  (flight, arm swing, lean), which is where generated cycles fell apart: run v1 repeated near-identical stances and
  froze the front arm up; run v2 fixed the arms but turned the legs into a jog on the spot. Retiming, reordering and
  a regeneration couldn't make either read as natural. For a lobby, a natural walk beats an unconvincing run, and
  the place for generated motion is short, key-pose-driven moves (attacks, the slash sheet) rather than fast cycles.

### The relic flash before each fight (0 credits)
- Shawn: "when I enter room 3 something flashed but did not show. after the words". Recorded the lobby -> fight
  transition frame by frame: after the story card faded out, one frame showed the lobby camera's close-up of the relic
  (the eye, full screen) before the fight appeared. Cause: entering a fight disabled the lobby director, which also
  stopped its black fade (drawn in OnGUI) one frame before the new scene replaced it. Every relic had it; the eye is
  the one you notice.
- Fix: the director keeps drawing solid black while the fight loads instead of disabling itself. Re-recorded: the
  last lobby frame is black, then the fight. Lobby flow bot 14/14 steps; console clean.

### The ending gets an illustration (1 credit)
- Shawn: "I will probably want a page after I killed all bosses. an illustration maybe". The ending was three lines
  on black; the last one, "At the very end, he chose to lose.", is the story's payoff, so it gets a picture.
- **The knight at rest:** his own defeated pose from fight 1, in his original armor, kneeling in the throne room the
  god took, at dawn: the tentacles turned to cracked stone, the eye on the wall closed for good, the sword laid down.
  Made with one edit of a **composite reference** (the knight's cut-out pasted into the final arena on a 16:9
  canvas), because a request takes one reference image: the room and the man both came through.
- **In the game:** after the final blow the screen goes black, the illustration fades in with a slow push-in toward
  him, the three lines play one at a time as subtitles on a dark band over the floor, then "Thank you for playing",
  the stats and the keys. The other two story beats stay on black. (`FightFlow.epilogueArt`, set by BuildStory.)
- Verified: the ending at 2.6 / 4 / 7 / 9.6 / 12.6 / 20 s; the skip / return checks still pass; console clean;
  progress restored. Balance 12.9. Media: `docs/media/ending.jpg`, `ending-ref-composite.jpg`, `ending-in-game.jpg`.

### WebGL build and the itch.io kit (1 credit)
- **WebGL build:** 38.4 MB zip (28.9 MB data, 9.7 MB code), Gzip with the decompression fallback itch needs, the
  Lobby first, all three fights. In the browser: the title, lobby, story card and the Knight fight render with art and
  HUD (Shawn played it in the pane). A browser-only `?qa=1` switch opens every relic and makes F9 drop the boss to
  1 HP, to check the later fights and the ending quickly (`WebTestHooks.cs`; players never see it). Debug keys
  (F1-F4) only exist in editor / development builds. The full browser pass (frame rate, sound, King, final, ending)
  is still to do: a hidden browser pane draws no frames.
- **Cover key art (1 credit):** one DreamLayer edit of a composite (the hero facing the Knight, the King and the
  Unknown One, pasted into the torn throne room): the hook in one picture. The cover is 630 x 500 with a gold
  engraved-capitals title (Castellar / Perpetua Titling, rendered locally), the whole scene below the title band.
- **The kit** (`Builds/itch/`, rebuilt by `Tools/make_itch_assets.py` and `Tools/make_itch_panels.py`): cover,
  banner, icon, logo; six 16:9 screenshots (title, the burning hall, the King's ice, the Maw, the root, the ending);
  three GIFs (the leap smash, the god waking, the Maw); five "How DreamLayer was used" panels (the villain chain,
  one room in three edits, greybox to art, the Unknown One's poses, the ending composite); the page text
  (`docs/itch-page.md`). Balance 11.9 (88.1 of 100 spent).

### A bigger game window on itch (0 credits)
- Shawn: "I also want the game window to be bigger". Unity's default page draws a fixed 960 x 540 canvas with a Unity
  footer, so a bigger itch frame would only add empty space. Our own page template now
  (`Assets/WebGLTemplates/OnlyBossFights`): the game fills whatever frame itch gives it and the whole screen in
  fullscreen, letterboxed to 16:9 so the framing and HUD stay as designed; a gold loading screen with the logo
  instead of Unity's; the game icon in the tab; a click on the game takes keyboard focus. Default size 1280 x 720
  (set the same on itch). Checked in the browser at 1280 x 720 (fills it) and 1600 x 700 (slim side bars).
  Zip 38.4 MB.

### First-time experience: the Knight is the tutorial (0 credits)
- Shawn: "is a tutorial needed? I want player to learn the mechanic with a first time experience". A jam player gives
  a game a couple of minutes; the old answer was a row of key hints at the bottom and the hope that "white glint =
  parry" reads on its own. No tutorial level (it would delay the fun): each mechanic is taught **the first time a fight
  puts it in front of you**, then the coach gets out of the way (`FirstTimeCoach`, drawn by the fight HUD).
- **The parry, Sekiro-style:** the first white glints slow time to 0.3, then stop it 3 frames before the blow lands:
  "WHITE GLINT: PARRY IT · RMB / K · parry now". Time moves again on the press, which is still inside the parry
  window, so the first parries land and *show* the timing. After two guided parries the prompt stays without the pause
  until one lands unaided (that's "learned"); three misses in a row bring the pause back.
- **The same pattern for the rest:** the riposte right after a parry ("PARRIED! HE'S OPEN"), red attacks ("RED: YOU
  CAN'T PARRY THIS · D + Space: dash through it", the pause timed inside the dash's invulnerability), a spell ("FIRE
  SPELL: PARRY IT", "your blade keeps it", then "YOUR BLADE HOLDS FIRE · hold, then release" until it's spent), a clash
  (flash counter), and floor marks (slow motion and "get out of it", no pause).
- **Only honest pauses:** the coach stops time only when the answer will work: the blow (with the rest of its lunge)
  really reaches the hero, the hero is free to act, a riposte would land. The first test paused for a swing that was
  going to miss, so "PARRY NOW" did nothing; the reach check fixed it. A first-timer's first swing also waits for the
  boss's name card to clear.
- What you've learned is saved (with the cleared fights), so a returning player isn't taught twice; Backspace on the
  title resets both. The bots switch the coach off.
- Verified with a scripted "new player" that presses nothing until the coach asks, then the asked key: the Knight
  (two guided parries, then one on its own = learned; riposte; red slam dashed through untouched; fire harvested), the
  King (cleave guided only in reach; ice spell; floor marks) and the Unknown One (a 4.5-unit lunge correctly counted
  as reaching). The fight bot still wins with the coach off; console clean.

### The King and the Unknown One walk (6.6 credits)
- Shawn: "do we have enough credit to generate boss 2 and 3's walking animation? just like boss 1?" Yes: 3.3 each (the
  Knight's setup: custom prompt, 8 frames, 512 px, background kept), 11.9 → 5.3 left. They used to slide toward the
  hero in their idle poses, the King most (the slowest boss, the longest approaches).
- **Both came back right on the first try** (78 s, 65 s): every frame on-model, one sword, one claw arm. The prompts
  were the Knight's plus explicit object counts.
- **Both zoomed in across the cycle** (King +22%, Unknown One +17% from frame 1 to frame 8). New
  `Tools/normalize_sheet_scale.py` scales every frame back to frame 1 (feet to crown, skipping the plume), and with
  `--match-idle` gives the size against the idle sprite and a pivot so the walk starts exactly where the idle stands.
  The Unknown One's claw arm had pulled the importer's automatic hip pivot 0.37 units forward; the importer now
  accepts an atlas `pivot_x`.
- Foot-locked timing (`foot_travel.py`): the King takes 0.95-unit steps (close to the Knight's brisk pace), the
  Unknown One slow 1.8-unit strides with his claw reaching ahead. Wired by `AgentScripts/ApplyBossWalks.cs`.
- Verified: in-arena filmstrips from idle into the walk (no size change, no sideways pop); the fight bot wins the King
  (35 s) and the final fight (36 s); console clean. Dogfooding entry 23, metrics row 18.

### The public repository and its README (4.3 credits)
- Shawn: "Create a new repo public that shows all my work and a complete readme about how I use dream layer, talk about
  what worked what did not ... and dev notes. integrate them together", and "use the rest of 5 credits for creating
  the read me. clean up the folder for sensitive files".
- **One repository, a fresh history:** the whole project (game, DreamLayer art and raw outputs, tools, notes). The
  private history, the project brief (`CLAUDE.md`, private notes) and the agent's local config (`.claude/`) are
  left out. A scan for keys, tokens, personal email and local user paths found nothing in
  the rest. No Git LFS: LFS's free tier allows 1 GB of downloads a month, which a public repo would burn through.
- **The README** ties the game, how DreamLayer was used (60 assets in the game, 99 credits, 74 requests), what worked
  (identity, walks 6 of 6, composites, object counts) and what didn't (the lobby run in three tries, size drift
  inside and between sheets, cycles that travel, extra props), what I'd ask DreamLayer to build, how the pipeline
  ran, and the five days of dev notes together.
- **README art (4.3 credits):** a header that's an edit of a composite (the hero and the villain's three forms on a
  sprite-sheet grid; first try, title added locally), and a fresh `idle` preset sheet as the importer's sample (first
  try; it grew a second sword on the hero's back in 5 of 8 frames, dogfooding entry 24). Balance 1.0.
- **The importer became a standalone tool:** import rules overridable per project
  (`ProjectSettings/DreamLayerImportRules.json`, since a package's files are read-only), the Generate window's folders
  follow the rules, a `package.json` (Add package from disk), the cleanup scripts in `Tools~`, two samples, and a full
  README. **Tested in a fresh, empty Unity 6 project in batch mode: 15 of 15 checks pass** (both samples, the default
  rules, the override and its revert). The game project compiles clean with it.
- The jam page Shawn pasted puts the deadline at **Oct 12, 5:30 AM MT**, seven hours later than our earlier reading.
