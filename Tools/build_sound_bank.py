"""
Builds the game's sound bank spec: which clips each sound cue uses, and how loud.

  1. Synthesizes the custom effects (Tools/synth_sfx.py -> Assets/Audio/SFX/Synth/).
  2. Copies only the pack files the cues use into Assets/Audio/SFX/Kenney/<pack>/ (with the pack's License.txt).
  3. Measures each clip's loudness (the loudest 50 ms window, RMS) and sets each cue's volume so its category lands
     on a target level: hits and the parry loud, swings and footsteps softer, telegraph cues clearly audible. Volumes
     come from measurement, not guesswork, so a quiet pack file and a hot synth file end up balanced.
  4. Writes Assets/Audio/sound_bank.json; AgentScripts/BuildAudio.cs turns it into Assets/Resources/SoundBank.asset.

Usage:
  python -I Tools/build_sound_bank.py --packs <folder with impact/ rpg/ interface/ unzipped Kenney packs>
"""
import argparse
import glob
import json
import os
import shutil
import subprocess
import sys
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SYNTH = "Assets/Audio/SFX/Synth"
KENNEY = "Assets/Audio/SFX/Kenney"

# Target loudness per category (dBFS, loudest 50 ms RMS). Parry is the loudest by design: it must feel unlike
# anything else. Footsteps sit far under the combat.
TARGETS = {
    "parry": -9, "big": -10, "hit_heavy": -11, "sting": -13, "hit": -13, "accent": -14, "cue": -14,
    "ui_accent": -16, "swing": -17, "step_boss": -18, "ui": -18, "move": -20, "soft": -21, "step": -25,
}
LAYER_GAIN_DB = -4  # a layer sits under its main clip


def k(pack, pattern):
    return ("kenney", pack, pattern)


def s(pattern):
    return ("synth", None, pattern)


# id: (category, main clips, layer clips, pitch range, cooldown seconds)
CUES = {
    "HeroSwingLight": ("swing", [s("swing_light_*")], [], (0.94, 1.08), 0.03),
    "HeroSwingHeavy": ("swing", [s("swing_heavy_*")], [], (0.95, 1.05), 0.03),
    "HitLight": ("hit", [k("impact", "impactPlate_medium_*")], [k("impact", "impactPunch_medium_*")], (0.95, 1.08), 0.03),
    "HitHeavy": ("hit_heavy", [k("impact", "impactPlate_heavy_*")], [k("impact", "impactPunch_heavy_*")], (0.93, 1.03), 0.03),
    "Parry": ("parry", [s("parry_ring")], [k("impact", "impactMetal_light_*")], (0.98, 1.04), 0.05),
    "Clash": ("parry", [s("clash_ring")], [k("impact", "impactMetal_heavy_*")], (0.96, 1.04), 0.05),
    "FlashCounter": ("accent", [s("flash_counter")], [], (0.98, 1.02), 0.1),
    "Dash": ("move", [k("rpg", "cloth*")], [s("dash")], (0.95, 1.08), 0.05),
    "HeroHurt": ("hit", [k("impact", "impactPunch_heavy_*")], [k("impact", "impactSoft_heavy_*")], (0.95, 1.05), 0.05),
    "HeroDeath": ("hit_heavy", [s("boom_slam")], [k("impact", "impactSoft_heavy_*")], (0.9, 0.95), 0.5),
    "ChargeStart": ("soft", [s("charge")], [], (1.0, 1.0), 0.2),
    "Harvest": ("accent", [s("harvest")], [], (1.0, 1.0), 0.1),
    "WaveRelease": ("accent", [s("wave_release")], [], (0.97, 1.03), 0.1),
    "CueGlint": ("cue", [s("cue_glint")], [], (0.99, 1.01), 0.05),
    "CueDanger": ("cue", [s("cue_danger")], [], (0.98, 1.02), 0.05),
    "BossSwing": ("swing", [s("boss_swing_*")], [], (0.92, 1.02), 0.05),
    "BossSlam": ("hit_heavy", [s("boom_slam")], [k("impact", "impactMining_*")], (0.95, 1.02), 0.1),
    "FlameWave": ("accent", [s("fire_wave")], [], (0.96, 1.04), 0.1),
    "BurnTick": ("soft", [s("fire_tick")], [], (0.9, 1.1), 0.05),
    "BossStagger": ("hit_heavy", [k("impact", "impactBell_heavy_*")], [k("impact", "impactPlate_heavy_*")], (0.95, 1.0), 0.2),
    "PhaseChange": ("big", [s("boom_deep")], [k("impact", "impactBell_heavy_*")], (0.9, 0.9), 0.5),
    "BossDeath": ("big", [s("boom_deep")], [k("impact", "impactPlate_heavy_*")], (0.85, 0.85), 0.5),
    "BossStep": ("step_boss", [k("impact", "footstep_concrete_*")], [k("impact", "impactPlate_light_*")], (0.8, 0.9), 0.15),
    "Footstep": ("step", [k("impact", "footstep_concrete_*")], [], (0.95, 1.1), 0.12),
    "NameCard": ("ui_accent", [s("boom_deep")], [], (1.0, 1.0), 0.5),
    "RelicCharge": ("ui_accent", [s("relic_charge")], [], (1.0, 1.0), 0.3),
    "RelicEnter": ("ui_accent", [s("relic_enter")], [k("rpg", "drawKnife*")], (1.0, 1.0), 0.5),
    "RelicSealed": ("ui", [k("interface", "bong_001")], [], (0.9, 0.95), 0.3),
    "UiConfirm": ("ui", [k("interface", "confirmation_001")], [], (1.0, 1.0), 0.1),
    "Victory": ("sting", [s("sting_victory")], [], (1.0, 1.0), 1.0),
    "Defeat": ("sting", [s("sting_defeat")], [], (1.0, 1.0), 1.0),
}


def decode(path):
    """Mono float samples at 44.1 kHz (wav via the wave module, ogg via ffmpeg)."""
    if path.endswith(".wav"):
        with wave.open(path) as w:
            y = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float64) / 32768.0
            return y.reshape(-1, w.getnchannels()).mean(1) if w.getnchannels() > 1 else y
    raw = subprocess.run(["ffmpeg", "-v", "quiet", "-i", path, "-f", "s16le", "-ac", "1", "-ar", "44100", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.int16).astype(np.float64) / 32768.0


def loudness_db(y):
    win = int(0.05 * 44100)
    if len(y) < win:
        return 20 * np.log10(max(np.sqrt(np.mean(y ** 2)), 1e-9))
    power = np.convolve(y ** 2, np.ones(win) / win, mode="valid")
    return 10 * np.log10(max(power.max(), 1e-12))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--packs", required=True, help="folder holding impact/, rpg/, interface/ (each an unzipped Kenney pack)")
    args = ap.parse_args()
    os.chdir(ROOT)

    subprocess.run([sys.executable, "-I", "Tools/synth_sfx.py", "--out", SYNTH], check=True, capture_output=True)

    def resolve(ref):
        kind, pack, pattern = ref
        if kind == "synth":
            files = sorted(glob.glob(f"{SYNTH}/{pattern}.wav"))
        else:
            src = sorted(glob.glob(os.path.join(args.packs, pack, "**", pattern + ".ogg"), recursive=True))
            files = []
            os.makedirs(f"{KENNEY}/{pack}", exist_ok=True)
            for f in src:
                dst = f"{KENNEY}/{pack}/{os.path.basename(f)}"
                shutil.copyfile(f, dst)
                files.append(dst)
            lic = glob.glob(os.path.join(args.packs, pack, "**", "License.txt"), recursive=True)
            if lic:
                shutil.copyfile(lic[0], f"{KENNEY}/{pack}/License.txt")
        if not files:
            raise SystemExit(f"no files for {ref}")
        return files

    cues = []
    for cue_id, (cat, mains, layers, pitch, cooldown) in CUES.items():
        main_files = [f for r in mains for f in resolve(r)]
        layer_files = [f for r in layers for f in resolve(r)]
        target = TARGETS[cat]
        main_db = float(np.mean([loudness_db(decode(f)) for f in main_files]))
        volume = min(1.0, 10 ** ((target - main_db) / 20))
        layer_volume = 0.0
        if layer_files:
            layer_db = float(np.mean([loudness_db(decode(f)) for f in layer_files]))
            layer_volume = min(1.0, 10 ** ((target + LAYER_GAIN_DB - layer_db) / 20))
        cues.append({"id": cue_id, "category": cat, "clips": main_files, "layer": layer_files,
                     "volume": round(volume, 3), "layerVolume": round(layer_volume, 3),
                     "pitchMin": pitch[0], "pitchMax": pitch[1], "cooldown": cooldown,
                     "measuredDb": round(main_db, 1), "targetDb": target})
        print(f"{cue_id:15s} {cat:10s} {len(main_files)}+{len(layer_files)} clips  {main_db:6.1f} dB -> vol {volume:.2f}"
              + (f", layer {layer_volume:.2f}" if layer_files else ""))

    with open("Assets/Audio/sound_bank.json", "w", encoding="utf-8", newline="\n") as f:
        json.dump({"cues": cues}, f, indent=1)
        f.write("\n")
    used = {c for cue in cues for c in cue["clips"] + cue["layer"]}
    print(f"wrote Assets/Audio/sound_bank.json: {len(cues)} cues, {len(used)} clips")


if __name__ == "__main__":
    main()
