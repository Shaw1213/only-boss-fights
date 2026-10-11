# Boss fight design — draft v2

*v2 (after first playtest: "parry is hard to use"): attack-parry (clash), mouse controls, a timed
telegraph cue, a more forgiving parry button, riposte only when the boss is open, and EARLY / LATE
callouts.*

Side-view boss rush (see CLAUDE.md §5). This doc covers the **hero kit** (shared by every fight),
the **danger language** (shared by every form of the villain), and **Boss 1**.

All numbers are **starting values at 60 fps**, to be tuned in the greybox. In code they are data
(ScriptableObjects), editable in the Inspector while playing.

---

## 1. Hero kit (decided: side view · move · dash · parry · attack forms)

| Verb | Input (mouse + keyboard) | Startup | Active | Recovery | Notes |
|---|---|---|---|---|---|
| Move | A / D | instant | — | short ease-out | No jump (add only if a fight needs vertical play) |
| **Dash** | Space (or Shift / L) | 2 f | **12 f invulnerable** | 8 f | 0.4 s cooldown; the safe answer to everything |
| **Parry** | Right click (or K) | 0 f | **14 f window** | 16 f on whiff | Success → riposte window + stagger meter |
| **Clash** (attack-parry) | Left click into a hit | — | **first 10 f of a neutral attack** | — | Same reward as a parry. Combo follow-ups can't clash, so mashing doesn't work |
| **Light combo** | Left click ×3 (or J) | 5 f | 3 f | 12 f (hit 3: 18 f) | Hit 3 is stronger and commits you |
| **Charged heavy** | hold left click ≥ 0.35 s, release | 4 f after release | 4 f | 20 f | Vulnerable while charging; dash cancels; a released heavy can clash; sprite-sheet showcase animation |
| **Riposte** | Left click within 0.5 s of a parry/clash | 3 f | 4 f | 14 f | Only while the boss is open (not mid-combo); guaranteed hit; big damage + stagger |
| **Flash Counter** *(added 2026-10-10; easier 2026-10-10)* | Dash (neutral or toward him) within **0.7 s** of a **clash**; the dash also cancels a swing, charge or parry started after the clash. A gold glint over the hero shows the window | 4 f — the step through him | 4 f | 14 f | The clash's own reward: invulnerable flash step to his far side, slash from behind, 5 dmg + 30 stagger, **breaks his combo** (works mid-combo, unlike the riposte). Dashing away stays a backdash |
| Dash attack *(optional)* | Left click at end of dash | 4 f | 6 f lunge | 14 f | Gap-closer, low damage; first thing to cut |

- **Input buffer:** 120 ms for every verb (an early press fires on the first valid frame). Hit-stop freezes don't
  count toward it: a press made during a freeze is still fresh when the game moves again.
- **Health:** 5 hits, shown as chunks. 1 s invulnerability after being hit.
- **Solid bodies** *(2026-10-10, Shawn)*: the hero can't walk through the boss and his lunges stop at the hero
  (bodies are the hurtbox widths). The **dash** and the flash counter's step go through him: they're the way out
  of a corner. A boss in a leap is out of the way; if he lands on the hero, the hero is eased out from under him.
  Toggles: `BossConfig.solidBody`, `HeroConfig.dashPassesThroughBoss`.
- **Gamepad:** same actions (stick / A dash / X attack / B or RB parry), optional.
- **Toggles for testing:** `dedicatedParryEnabled` and `attackParryEnabled` in HeroConfig.
- **Callouts:** PARRY! / CLASH! on success; EARLY (pressed up to 0.65 s before a hit you took),
  LATE (pressed within 0.25 s after), RED = DASH (tried to parry an unparryable hit).

### Harvest: steal the boss's magic (Shawn's idea, 2026-10-08; built in the greybox, numbers are starting values)

The hero is a **spellblade**: a slender sword with a rune line that carries magic. Harvest makes the
parry feed that magic.
- Some boss attacks are **elemental spells: fire, ice, wood**. All of them are parryable and show the
  white glint like any parryable hit.
- **Parry or clash an elemental attack → harvest it.** The blade's rune line takes the element's color.
  He holds one element at a time, and a new harvest replaces the old one.
- **Spend it** on the next charged heavy (the crescent wave becomes elemental) or riposte:

  | Element | Role | Effect (starting values) |
  |---|---|---|
  | Fire | damage | +50% damage, plus a short burn |
  | Ice | control | freezes the boss ~1.5 s (a free punish window) |
  | Wood | survival | roots the boss and heals 1 health chunk |

- The danger language stays intact. Harvestable means parryable means white glint; unparryable spells
  keep the red glow (dash). **Fire is drawn orange-yellow, never the warning red.**
- **Villain mapping (suggestion):**
  - Knight, phase 2: his blade ignites (fire), the first taste of the god's power, so harvest is taught
    at the end of fight 1.
  - King: ice (a frozen court).
  - The Unknown One: wood (roots and tentacle vines), remixed with fire and ice.
- **Built (greybox):** Knight phase 2 has the **Flame Wave**: a parryable fireball at chest height
  (white glint, orange), and the swing itself burns up close. The infused heavy fires a crescent
  instead of its melee hit; a riposte spends the element only if it lands; light attacks never spend it.
  **F3** cycles the held element for testing. Bot-verified: `AgentScripts/BotHarvestChecks.cs`.
- **Art:** the hero's teal rune accents appear nowhere else on him, so a Unity shader recolors them
  per element. Elements are Unity effects, with no extra credits beyond the bosses' casting poses.

**Boss spells that land on the hero (added 2026-10-10, Shawn):** an elemental attack that isn't parried leaves its
element on the hero, the mirror of what harvest does to the boss. Each has its own answer:

| Element | On the hero (starting values) | Answer |
|---|---|---|
| Fire | **Burning:** 2 more ticks of 1 damage, 1 s apart | **A dash puts it out** |
| Ice | **Chilled** 2.5 s: half walk speed, dash recharges half as fast | Play it safe until it wears off |
| Wood | **Rooted** 1.2 s: no walking, no dashing (attack and parry still work) | Parry what comes next |

The root is short enough that the next floor attack can still be escaped (the boss's recovery + idle + windup is
longer than the root). A colored band at the hero's feet, a tint, a callout and a HUD line show the status.

**Risk vs. reward:** dashing is always safe but slow to kill; parry/clash → riposte is the fastest
kill. Parries, clashes and heavies fill the boss's **stagger meter**; full meter = the boss collapses
into a long punish window.

---

## 2. Danger language (every form of the villain uses it)

| Cue | Meaning | Player answer |
|---|---|---|
| **White glint** | Parryable | Parry (or dash) |
| **Red glow** | Unparryable | Dash |
| **Floor marker** | Area attack | Move / dash out |

**Two layers per telegraph:** the windup *pose* says **what** is coming; the cue (glint pop-in, or the
red glow going full) appears **18 frames (0.3 s) before the hit** and says **when**. 18 f ≈ human
reaction time + margin, so "react to the flash" lands inside the 14 f parry window.

Rules (from the combat-design skill):
- Telegraph length grows with damage. Bigger hit = longer, louder warning.
- Telegraphs never lie. **Feints only appear after the player has seen the honest version.**
- **The villain keeps his timing signatures across forms** (windup lengths, combo rhythm), while
  the art style changes. What the player learns in fight 1 still helps in fight 3: "one villain"
  is felt in the gameplay, not just the art.
- Playtest rule: **zero deaths from an attack without a visible telegraph.**

---

## 3. Boss 1 — the first form (teaches: parry)

| Attack | Windup (telegraph) | Active | Recovery = punish window | Cue | Answer |
|---|---|---|---|---|---|
| **Overhead Cleave** (heavy) | 700 ms — raises sword | 4 f | **900 ms** | White glint | Parry → big stagger; or dash away |
| **Lunge Thrust** (gap-closer) | 450 ms — crouches back, leans in | 8 f, crosses half the screen | 500 ms | White glint | Parry, or dash through |
| **Three-Strike Combo** | 400 ms, then a steady 250 ms rhythm; 3rd hit heavier and slower | 3 f each | 700 ms | White glint | Parry each; parry all 3 → instant stagger |

**Phase 2 (at 50% HP):** the painting cracks (DreamLayer edit) and he gets desperate.
- **Windups stay the same** (learned timing still works); **gaps between attacks shrink** (more pressure).
- Combo gains a **delayed 4th hit** — the first feint, only after the rhythm is learned.
- New: **Shockwave Slam** — **red glow**, unparryable ground wave → dash over it. Teaches "not
  everything can be parried" and sets up Boss 2.

**Fight targets:** ~1.5–2.5 min for a skilled player · boss dies in ~25–35 light hits without
parry punishes · instant retry.

**Tuning (2026-10-10):** boss 100 HP (phase 2 at 50), hero 7 HP. The scripted bot (perfect parries,
attacking every frame) kills him in 28 s, down from 12.3 s at 40 HP; a human should land around 1.5–2 min. Starting
values for playtests.

---

## 4. Feedback table (juice budget: Medium–High; top tier only for the boss kill)

| Event | Hit-stop | Shake | Other |
|---|---|---|---|
| Hero hits boss (light) | 3–4 f | small | 2 f white flash on boss |
| Charged heavy / riposte hits | 6–8 f | medium | bigger flash, heavier SFX |
| **Parry success** | 8–10 f | medium | **unique color flash + "clang"** — must not look like any other event |
| Boss staggered | 6 f | medium | boss collapses, meter flashes |
| Hero gets hit | 6 f | medium-large | red vignette, 1 s invulnerable |
| Phase change | 0.5 s freeze | — | crack wipe across the painting |
| Boss defeated | 0.6 s slow-mo | top tier | only use of the strongest effects |

Rules: hit-stop never stacks on rapid hits; shake is trauma-based (accumulates and decays) so it
doesn't pulse; every event has its own sound.

---

## 5. Open questions
- ~~What Boss 2 teaches~~ positioning: see [boss-02.md](boss-02.md). Still open: how the final form remixes both.
- The villain's identity and the three forms / art styles (CLAUDE.md §5).
- Does the hero's look change per painting?
