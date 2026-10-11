# itch.io page draft: Only Boss Fights: The Fallen Knight

Paste the sections below into the itch.io editor. Image files are in `Builds/itch/` (upload them where marked).
Fill in the two links marked **[LINK]**.

---

## Upload settings (Edit game)

- **Title:** Only Boss Fights: The Fallen Knight
- **Short description / tagline:** A boss rush where every boss is the same knight, falling further. Read, parry, dash, punish.
- **Classification:** Game · **Kind of project:** HTML
- **Upload:** `Builds/itch-webgl.zip` → tick "This file will be played in the browser"
- **Embed options:** Viewport **1280 × 720** (the game scales to any 16:9 size: 1280 × 720 is big on most screens; 960 × 540 if
  it feels too wide on a laptop) · Fullscreen button ON · Mobile friendly OFF · Automatically start on page load OFF ("Click to launch")
- **Cover image:** `cover.png` (630 × 500; `cover@2x.png` if it accepts the larger one)
- **Screenshots:** `screenshots/1-title-lobby.png` … `6-ending.png`, then the three GIFs from `gifs/`
- **Genre:** Action · **Tags:** boss-rush, boss-battle, 2d, anime, souls-like, parry, ai-art, dreamlayer, unity
- **Theme (Edit theme):** banner `banner.png` (1280 x 400: as wide as the 1280 game embed, so itch shows it 1:1; use
  `banner-960.png` if the embed is 960 wide; not a 2x file, itch squeezes a wider one into the column). Colors:
  background #100C16; **BG2 (the page column, white by default) #16121C**; text #E8E4EC; links / buttons #ECC478

---

## Page text

![Only Boss Fights: The Fallen Knight](banner.png)

**The realm's finest knight had never lost a duel. Then he lost to you.**

He swore he would never be weak again. Power made him a king; the crown wasn't enough. Beneath the throne,
something answered.

**Only Boss Fights** is a short boss rush: no levels, no filler, just three fights against one villain in three
forms. Each form teaches you something the next one tests.

- **The Fallen Knight** teaches you to **parry**: every strike he means comes with a white glint.
- **The Frozen King** teaches you to **move**: ice erupts from the floor and lances fly across his throne room.
- **The Unknown One** asks for both at once, and remixes everything that came before.

**What you can do**
- **Parry** on the glint: a clean parry opens a riposte; parry a whole combo and he staggers.
- **Flash counter:** dash right after a clash to slip through him and strike from behind.
- **Harvest:** parry a boss's fire, ice or wood spell and your blade keeps it, for your next charged heavy or riposte.
- **Red means dash:** some attacks can't be parried. Read the cue, then answer it.
- Two phases per boss, instant retry, and an ending.

**Controls** (click the game first: that also turns the sound on)

| | Keyboard / mouse |
|---|---|
| Move | A / D |
| Dash (invulnerable) | Space |
| Attack (hold to charge) | Left click / J |
| Parry | Right click / K |
| Enter a relic (lobby) | stand still at it, or W / E |
| Restart the fight · Back to the lobby | R · Esc |

Fights last 1-3 minutes. The Crown opens after the Knight, the Eye after the King. First time? The fight teaches as
you go: the first time each move matters, time stops and shows you the key.

---

## How DreamLayer was used

**Every character, every arena, the relics, the ending illustration and the cover were made with DreamLayer**
(95 of the jam's 100 credits, 72 requests: 56 edits, 10 sprite sheets, 6 generations). Effects (flames, ice
spikes, tentacles, slash arcs), the UI and the code were built by hand; the sound is CC0 (credits below).

**One villain, three forms, one image edited step by step.** The game's hook is DreamLayer's core feature:
keeping one character consistent through edits. The Knight's reference was edited into the Frozen King, the King
into the Unknown One, and every pose of each form is an edit of that form's reference.

![One villain, three forms](dreamlayer/1-villain-chain.png)

**One room, three edits.** The King's throne room was generated once, then edited: frozen over for his phase 2,
taken by the god for the final fight, torn open for its phase 2. The floor line held through every edit, so the
phases crossfade in the game.

![One room, three edits](dreamlayer/2-one-room.png)

**Built as a greybox first, then dressed.** Every fight was playable and tuned with boxes before any art; the
DreamLayer poses were swapped in on the same frame data.

![Greybox to art](dreamlayer/3-greybox-to-art.png)

![The Unknown One's poses](dreamlayer/5-unknown-one-poses.png)

**Two references in one.** A request takes one reference image, so the ending is an edit of a composite: the
Knight's defeated pose from the first fight, pasted into the final arena.

![The ending composite](dreamlayer/4-ending-composite.png)

**What worked, what broke, and what I'd build.** I logged every request (prompt, credits, what held, what
drifted) and turned it into a dogfooding write-up: identity held across three forms; poses drifted up to 22% in
scale on the same canvas (so I measured and normalized every one); generated walk cycles worked, run cycles
didn't; and a request can't take more than one reference. [the full notes](https://github.com/Shaw1213/only-boss-fights#what-worked-and-what-didnt)

**A DreamLayer → Unity importer.** DreamLayer's Unity sprite import had never been run in Unity, so I built and
validated one: sprite-sheet ZIPs become sliced sprites and animation clips with each frame's own duration, feet
pivots, per-folder import rules for WebGL, a provenance file next to every image, and an editor window to generate
from inside Unity. [github.com/Shaw1213/only-boss-fights](https://github.com/Shaw1213/only-boss-fights/tree/main/Assets/Editor/DreamLayer)

---

## Credits

- **Design, direction and playtesting:** Shawn Zhang. Built in Unity 6 with **Claude Code** (an AI coding agent)
  writing most of the implementation from my design, and **DreamLayer** for all the art.
- **Music** (CC0, OpenGameArt): *Epic Boss Battle* by Juhani Junkala · *Determined Pursuit* by Emma_MA · *Artifact
  Room* by Fupi · *Cathedral in the forest* by congusbongus
- **Sound effects:** Kenney (CC0): Impact Sounds, RPG Audio, Interface Sounds; the rest synthesized for the game.
- Made for the **DreamLayer Game Jam #1**.
