# Final boss: the Unknown One, draft v1 (2026-10-10)

*Status: greybox built and bot-verified (`Assets/Scenes/BossFinal.unity`, entered from the lobby's eye once the
Knight and the King are cleared). Numbers are starting values for playtests. Story (Shawn, 2026-10-10): the crown wasn't
enough; he went looking for more power, the Unknown One answered, and he became its body. At the finale the knight he
was holds the god still: he chooses to lose. Ending built (epilogue lines after the final blow).*

The hero kit and the danger language are shared with every fight: [boss-01.md](boss-01.md) §1–2.
The forms before it: the Knight ([boss-01.md](boss-01.md), teaches parry) and the King ([boss-02.md](boss-02.md),
teaches positioning).

## The idea: the exam

The Knight taught **parry**. The King taught **positioning**. The final form asks for **both at once** and pays off
**harvest**: it is the only fight with all three elements, and wood (the only heal in the game) has to be earned by
parrying.

### One villain: the god wearing the knight

- **Look:** the knight's helm and plume survive (the identity anchor); tendrils grow where the cape was.
- **The sun becomes the eye.** The Knight's sun emblem (on his tabard), the King's sun (on his crown), and in phase 2
  the emblem **opens as an eye**. One visual thread through all three forms, and a strong DreamLayer before/after
  ("same character, the emblem is now an eye").
- **Story told through the danger language: the glint is the knight's honor.** He always shows his strike before he
  swings. The god's own attacks never glint: they mark the floor (move) or glow red (dash). In this fight, reading
  "is this the knight or the god?" is the same skill as reading "parry or move?".

## Phase 1: the vessel (the knight's moves, the god's reach)

| Attack | Windup | Active | Recovery | Cue | Answer |
|---|---|---|---|---|---|
| **Hollow Cleave** | 700 ms (Knight timing) | 4 f | 900 ms | White glint | Parry |
| **Tendril Lash** | 550 ms | 6 f, reaches ~4.7 units | 600 ms | White glint, green | **Parry → harvest wood** (root him, heal 1) |
| **Grasping Ground** | 750 ms, tendrils under the hero | 8 f | 600 ms | Floor marker | Move out |
| **Bind and Strike** | marker under the hero (670 ms), then a 400 ms lunge at wherever the hero went | 8 f + 8 f | 600 ms | Floor marker, then white glint | **Move, then parry** |

## Phase 2: the god wakes (at 50%)

The emblem opens as an eye; the arena tears (DreamLayer edit). Gaps between moves shrink (0.3–0.55 s).

| Attack | What it asks | Answer |
|---|---|---|
| **Three-Element Volley** | fire, ice and wood lances in the Knight's combo rhythm (400 ms, then every 250 ms) | Each parry replaces the held element, so **the last one you parry is the power you keep**: choose under pressure. Parry all 3 → stagger |
| **Procession** | the King's marching tendril line | Dash through, or stay out of reach |
| **Shockwave Slam** | the Knight's red ground wave | Dash |
| **Maw** | the floor opens 6 units wide around the hero (900 ms; 2 damage, so a longer warning) | Run out fast, or dash through it |
| Hollow Cleave, Tendril Lash, Bind and Strike | stay | |

## v2 (2026-10-10): harder, with a leap

Shawn: "too easy, make something special like jumping up and smash for more damage".

- **Leap Smash** (phase 1): he jumps out of reach (his hurtbox goes up with him), his mark follows the hero along the
  floor, then **locks 20 frames before he lands** (the mark turns bright and thick, the danger snarl sounds) and he
  slams down on it for **3 damage**. Getting out is rewarded with a 900 ms punish window where he landed.
- **Heaven Fall** (phase 2): the Leap Smash, then 0.3 s later a red-glow slam where he landed sends a **shockwave
  each way**. Move, then dash. (Firing the waves on the landing frame itself would spawn them on top of a hero who just
  dodged the mark: unfair, so the slam is its own telegraphed beat.)
- Heavier hits: Hollow Cleave and the Bind strike 1 → 2 damage. Shorter gaps: phase 1 0.45–0.8 s, phase 2 0.2–0.4 s.
- His spells now land on the hero too: the lash roots, the volley burns / chills / roots (see boss-01.md, Harvest).
- **Bot (v2):** wins in 47–48 s with 1, 4 and 7 HP left (v1: 38–45 s); the finale fired in all three.
  `AgentScripts/BotSpellChecks.cs` checks the leap (out of reach, mark tracks then locks, 3 damage on a hero who stays,
  none on one who walks out after the lock, a shockwave each way) and the three statuses.

## Finale (15% HP): the knight's last stand

The eye flickers, the helm **glints one last time**, and **the knight holds the god's body still** for 4 s: "The
knight holds him. Strike!" The hero lands the final blow. It happens once per fight. The glint that taught the player
to parry becomes the villain's last act, so the game ends on a story beat, not just an empty health bar.

- **Built:** `BossConfig.finaleAtHealth` / `finaleHoldSeconds` / `finaleText` (any boss can use it; only the final
  form does). A long stagger: hits and ripostes land. The HUD marks the threshold on the health bar in phase 2 and
  shows the line while he's held; the boss's glint shows the whole time.

## Numbers

Boss 120 HP (phase 2 at 50%, finale at 15%), walk 2.2 units/s. All data: `Assets/Data/BossFinal/` (built by
`AgentScripts/BuildBossFinal.cs`; re-running it keeps tuned values).

| Step | Startup | Active | Recovery | Notes |
|---|---|---|---|---|
| U_HollowCleave | 42 | 4 | 54 | hitbox 2.8 × 3.0 |
| U_TendrilLash | 33 | 6 | 36 | wood, hitbox 4.2 × 0.9 from 0.5 to 4.7 in front |
| U_GraspingGround | 45 | 8 | 36 | at the hero, 1.8 × 2.2 |
| U_Bind + U_BindStrike | 40 / 24 | 8 / 8 | 0 / 36 | marker at the hero, then a 4.5-unit lunge |
| U_VolleyFire / Ice / Wood | 24 / 9 / 15 | 3 / 3 / 4 | 3 / 3 / 60 | the three-strike rhythm, one element each |
| U_Procession1–4 | 36 / 10 / 10 / 10 | 6 | 0 / 0 / 0 / 40 | facing locked |
| U_ShockwaveSlam | 36 | 6 | 40 | red, ground projectile |
| U_Maw | 54 | 8 | 48 | at the hero, 6.0 × 2.2, 2 damage |

**Bot runs (2026-10-10):** wins in 38–45 s with 4–7/7 HP (it beats the Knight in ~28 s, the King in ~32 s). Wood
heals show up in every run. In a full fight the finale held him at 16 HP and the bot finished him 2.8 s later. A
scripted check: held at 17/120 (not at 20), for exactly 4.00 s, never twice.

## Art (built 2026-10-11, 14 credits)

The King's body, taken by the god: an edit of the King's reference (`ArtRaw/final/final-ref-b.png`). Helm, plume,
crown (cracked) and sword survive; the sun crest is a sun around the god's eye; a huge clawed tentacle arm replaces
his free arm. The silhouettes follow the danger language: the sword raised = the knight's cleave (glint, parry), the
claw thrust = the god's lash and lances (parry, harvest), the claw sunk into the floor = the floor opens (move),
airborne then crouched = the leap (move, then punish). Phase 2's pose opens the eye. The arena is the King's throne
room, taken (tentacles, a weeping eye where the sun was), torn open onto a void with a giant golden eye in phase 2.
Wired by `AgentScripts/ApplyFinalArt.cs`; details and numbers in the devlog and dogfooding entry 20.
His floor attacks, his ground wave and his root on the hero are his tentacles (`Tentacles.cs`, drawn at runtime):
a dark pool telegraphs, then they burst up; the wave's tentacles glow red (dash); rooted, they hold the hero's legs.

Original plan:

| Asset | Op | Credits |
|---|---|---|
| Unknown One form reference (from the King or knight reference: tendrils, the emblem as an eye) | edit | 1–2 |
| ~6 poses: idle, cleave windup/strike, lash, cast, hurt/held | edit ×6 | 6 |
| Phase-2 showcase: "the emblem opens as an eye" | edit | 1 |
| The corrupted arena + its torn phase-2 version | edit ×2 | 2 |
| **Total** | | **~10–12** |

Tendrils, floor eruptions, the volley shards and the finale's glint are Unity effects (0 credits).

## Open (Shawn)

- Scope: if time runs short, the final fight is the first thing to cut (CLAUDE.md §12).
