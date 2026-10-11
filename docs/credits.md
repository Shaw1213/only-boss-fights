# Credits

Shown on the itch page. Everything third-party in the build is listed here with its license.

## Art
- All character, environment and prop art: generated with **DreamLayer** (dreamlayer.io) from our own references,
  then cleaned up locally (cutout, defringe, pose normalization: `Tools/`). Generation log: [credits-log.md](credits-log.md).
- Runtime effects, generated in code (no assets): flames, the shockwave, slash arcs, contact shadows, the King's ice
  spikes, the Unknown One's tentacles (floor attacks, the wave, the root), the landing dust and cracks.

## Sound effects
- **Kenney** (kenney.nl), CC0 1.0 (public domain; credit not required, given anyway):
  - *Impact Sounds*: armor / metal / body hits, footsteps, the stagger bell, rock crunch.
  - *RPG Audio*: cloth (dash), sword draw (entering a fight).
  - *Interface Sounds*: title confirm, the sealed-relic bong.
  - License files: `Assets/Audio/SFX/Kenney/<pack>/License.txt`.
- **Synthesized for this game** (`Tools/synth_sfx.py`): sword whooshes, the parry and clash rings, the flash counter,
  the telegraph glint and danger snarl, fire, low booms, the charge hum, the harvest chime, the relic charge and
  entry, and the victory / defeat stings.

## Music
All CC0 1.0 (public domain; credit not required, given anyway), from OpenGameArt. Leveled to matching loudness
and converted to OGG for the game (`Tools/prepare_music.py`); otherwise unedited.
- **Fight:** *Epic Boss Battle [Seamlessly Looping]* by **Juhani Junkala**. https://opengameart.org/content/boss-battle-music
- **Fight, phase 2:** *Determined Pursuit* by **Emma_MA**. https://opengameart.org/content/determined-pursuit-epic-orchestra-loop
- **Lobby:** *Artifact Room* by **Fupi**. https://opengameart.org/content/artifact-room
- **Ending:** *Cathedral in the forest* by **congusbongus**. https://opengameart.org/content/cathedral-in-the-forest-ambient-loop

## Code and tools
- Game, Unity importer and pipeline tools: Shawn Zhang, with Claude Code.
