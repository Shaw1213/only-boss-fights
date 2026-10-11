# Boss 2: The King, draft v1 (2026-10-10)

*Status: greybox built and bot-verified (`Assets/Scenes/Boss02.unity`, entered from the lobby's crown, which opens once the Knight is beaten). Numbers are
starting values for playtests. Story (Shawn, 2026-10-10): beaten by the hero, the knight went looking for power, and
power made him king; he froze his court so no one could take the crown from him. Name: "The Frozen King".*

The hero kit and the danger language are shared with every fight: [boss-01.md](boss-01.md) §1–2.

## What he teaches: positioning

The Knight asks **"can you read the glint?"** (parry). The King asks **"where are you standing?"**

- **He doesn't chase.** He walks slower (1.8 vs 2.5 units/s) and backs away to cast from about 7 units, so the hero
  has to close the distance through his lances. When the hero arrives, he cleaves.
- **He commands the floor.** His area attacks land **where the hero is standing**, marked on the floor first. Standing
  still is what gets you hit. This is the third cue of the danger language (the floor marker: move out), which the
  Knight never used, so fight 2 introduces it.
- **Element: ice.** His lances are parryable spells: parry one to harvest ice, and the next charged heavy or riposte
  **freezes him** for 1.5 s. Fight 1 taught harvest with fire; fight 2 hands the player the control element.

## One villain: what carries over from the Knight

| Knight | King | Why |
|---|---|---|
| Overhead Cleave: 700 ms windup, 900 ms punish | **Royal Cleave**: identical timing | The parry timing learned in fight 1 still works. "One villain" is felt in the hands, not just seen |
| Three-Strike Combo: 400 ms, then a hit every 250 ms; parry all 3 → stagger | **Frost Volley**: three lances in the same rhythm; parry all 3 → stagger | Same rhythm, now at range |
| Shockwave Slam: red glow = dash | Red still means dash; the King adds the floor marker = move | The language grows, it never changes |

## Phase 1

| Attack | Windup (telegraph) | Active | Recovery (punish) | Cue | Answer |
|---|---|---|---|---|---|
| **Royal Cleave** | 700 ms, raises the blade | 4 f | **900 ms** | White glint | Parry (Knight timing) or dash away |
| **Frost Lance** | 550 ms, points at the hero; an ice shard flies at chest height | travels at 9 u/s | 500 ms | White glint, ice blue | **Parry it → harvest ice**, or dash through |
| **Glacier Rise** | 750 ms, frost spreads **under the hero** | 8 f | 600 ms | Floor marker | Walk or dash out of the mark |

## Phase 2 (at 50% HP): the court freezes over

Windups stay the same (learned timing still works); gaps between moves shrink (0.35–0.6 s, was 0.7–1.1 s).

| Attack | What changes | Answer |
|---|---|---|
| **Royal Cleave** | Unchanged | Parry |
| **Frost Volley** | Three lances in the Knight's combo rhythm | Parry each (all 3 → stagger, and the last one harvests), or dash through |
| **Twin Rise** | Glacier Rise, then a second eruption on **wherever the hero moved to** (500 ms warning) | Keep moving; dash the second |
| **Procession** | Spikes erupt in a line from him toward the hero, one after another (~270 ms apart); the line keeps its direction if the hero dashes through | Dash through the line, or stay out of reach (~7 units) |
| **Glacier Rise** | Unchanged | Move out |

Phase-2 music switches to *Determined Pursuit* (shared with the Knight's phase 2 for now).

## Numbers

Boss 100 HP (phase 2 at 50), same stagger meter as the Knight. All data:
`Assets/Data/Boss02/` (built by `AgentScripts/BuildBoss02.cs`; re-running it keeps tuned values).

| Step | Startup | Active | Recovery | Notes |
|---|---|---|---|---|
| K_RoyalCleave | 42 | 4 | 54 | hitbox 2.8 × 3.0 |
| K_FrostLance | 33 | 4 | 30 | ice, projectile 9 u/s at 0.8 height; no melee hitbox |
| K_GlacierRise | 45 | 8 | 36 | at the hero, 1.8 × 2.2, unparryable |
| K_Volley1–3 | 24 / 9 / 15 | 3 / 3 / 4 | 3 / 3 / 60 | the three-strike rhythm |
| K_TwinRise1–2 | 45 / 30 | 8 / 8 | 6 / 36 | the second re-targets |
| K_Procession1–4 | 36 / 10 / 10 / 10 | 6 each | 0 / 0 / 0 / 40 | offsets 1.8 / 3.4 / 5.0 / 6.6, facing locked |

**Bot run (2026-10-10):** the scripted bot (perfect parries, dashes, walks out of marks) wins in 32.2 s with 4/7 HP
(it beats the Knight in ~28 s). It took one hit from Twin Rise and two from Frost Volley (it ripostes after the first
parry instead of waiting for the second lance), so the volley punishes greed as intended. A human fight should land
around 2 minutes.

## What was added to the systems (small, shared)

- `AttackData.atHero`: the hitbox lands where the hero stood when the windup began; the floor marker follows it.
- `BossMove.lockFacing`: a multi-step move keeps its first step's facing (Procession).
- **Parried projectiles now feed the stagger meter** like parried swings, and count toward a full-parry stagger while
  their move is still playing. Before, parrying the Knight's Flame Wave filled nothing; now it adds the same 25 as any
  parry. The Knight's balance moves slightly in the hero's favor.

## Art (needs Shawn's credit approval; nothing generated yet)

Reference chain: knight master → **King form reference** (edit: same helm and plume or a crowned helm, a heavy royal
mantle, the knight's sword or a scepter, ice-pale palette) → every King pose from that reference.

| Asset | Op | Credits |
|---|---|---|
| King form reference (1–2 tries) | edit | 1–2 |
| Idle, cleave windup, cleave strike, cast windup, cast release, floor cast (hand to the ground), recovery, hurt, staggered, dead | edit ×10 | 10 |
| Phase-2 showcase ("same king, frost climbing his armor") | edit | 1 |
| Throne room arena + phase-2 frozen version | edit ×2 | 2 |
| **Total** | | **~14–15** |

- Walking: he barely walks, so a 2-pose stately step (2 edits) or one walk sprite sheet (~3–6 credits) can wait;
  the first pass can reuse idle with a slow glide.
- **Effects are Unity code (0 credits):** the ice shard, the frost patch on the floor (it must read far better than the
  greybox's thin red bar: "zero deaths without a visible telegraph"), the spikes erupting, the frozen tint.
- **Done (2026-10-11):** art in (13 credits, see the devlog). Floor attacks (Glacier Rise, Twin Rise, Procession) are
  ice now (Shawn: "change the red floor attack to ice spikes"): a frost patch spreads and brightens under the target
  through the windup, then a cluster of cel-shaded ice spikes bursts up across the hitbox (center first, back spikes
  behind the fighters and front ones in front), holds while it's live and shatters. `BossConfig.floorStyle =
  IceSpikes`; `Assets/Scripts/Feel/IceSpikes.cs`. The red sword-trail arc no longer plays on these.

## Open questions (Shawn)

- His own fight music, or keep the shared fight tracks?
