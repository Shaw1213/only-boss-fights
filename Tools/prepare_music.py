"""
Brings the downloaded music tracks into the project at matched loudness.

Downloaded tracks are mastered at very different levels (Epic Boss Battle measures -6.8 LUFS, Artifact Room -15.2),
so playing them at one music volume would make one blast and the other whisper. This measures each track's
integrated loudness (EBU R128, via ffmpeg), turns it down to its target, and encodes OGG Vorbis into
Assets/Audio/Music/. Targets only ever attenuate, so nothing clips. Each output is decoded again to check the sample
count matches the source exactly (the tracks are seamless loops; a padded or trimmed file would click or gap).

The music volume in the game (SoundBank.musicVolume) is applied on top of these levels.

Usage:
  python -I Tools/prepare_music.py --src <folder with the downloaded files> [--ffmpeg <path to a recent ffmpeg>]
  (an old ffmpeg's Vorbis encoder pads the end of the file; the sample check catches that)
"""
import argparse
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = "Assets/Audio/Music"
QUALITY = 7  # libvorbis -q (about 224 kbps); Unity re-encodes on import, so start from a near-transparent copy

# output name: (downloaded file, target LUFS, credit). Fights sit louder than the lobby's ambience.
TRACKS = {
    "fight_epic_boss_battle": ("boss_epic.wav", -16.0,
                               "Juhani Junkala, Epic Boss Battle [Seamlessly Looping], CC0, https://opengameart.org/content/boss-battle-music"),
    "fight_phase2_determined_pursuit": ("boss_pursuit.wav", -16.0,
                                        "Emma_MA, Determined Pursuit, CC0, https://opengameart.org/content/determined-pursuit-epic-orchestra-loop"),
    "lobby_artifact_room": ("lobby_artifact.ogg", -19.0,
                            "Fupi, Artifact Room, CC0, https://opengameart.org/content/artifact-room"),
    "lobby_alt_cathedral_in_the_forest": ("lobby_cathedral.ogg", -19.0,
                                          "congusbongus, Cathedral in the forest, CC0, https://opengameart.org/content/cathedral-in-the-forest-ambient-loop"),
}


FFMPEG = "ffmpeg"


def ffmpeg(*args):
    return subprocess.run([FFMPEG, "-nostats", *args], capture_output=True, text=True, errors="replace")


def integrated_lufs(path):
    err = ffmpeg("-i", path, "-af", "ebur128", "-f", "null", "-").stderr
    found = re.findall(r"^\s+I:\s+(-?[\d.]+) LUFS", err, re.M)
    if not found:
        sys.exit(f"could not measure loudness of {path}")
    return float(found[-1])  # the summary comes last


def sample_count(path):
    # Decode to raw PCM and count frames (2 channels x 16 bit = 4 bytes per frame).
    raw = subprocess.run([FFMPEG, "-nostats", "-loglevel", "error", "-i", path, "-f", "s16le", "-ac", "2", "-ar", "44100", "-"],
                         capture_output=True).stdout
    return len(raw) // 4


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", required=True, help="folder holding the downloaded tracks")
    ap.add_argument("--ffmpeg", default="ffmpeg", help="ffmpeg executable")
    a = ap.parse_args()
    global FFMPEG
    FFMPEG = a.ffmpeg

    os.makedirs(os.path.join(ROOT, OUT), exist_ok=True)
    for name, (file, target, credit) in TRACKS.items():
        src = os.path.join(a.src, file)
        dst = os.path.join(ROOT, OUT, name + ".ogg")
        lufs = integrated_lufs(src)
        gain = min(0.0, target - lufs)
        r = ffmpeg("-y", "-i", src, "-af", f"volume={gain:.2f}dB", "-c:a", "libvorbis", "-q:a", str(QUALITY), dst)
        if r.returncode != 0:
            sys.exit(r.stderr[-2000:])
        n_src, n_dst = sample_count(src), sample_count(dst)
        print(f"{name}: {lufs:.1f} LUFS -> {integrated_lufs(dst):.1f} (gain {gain:+.1f} dB), "
              f"{n_dst / 44100:.2f} s, samples {'match' if n_src == n_dst else f'DIFFER ({n_src} vs {n_dst})'}, "
              f"{os.path.getsize(dst) / 1e6:.1f} MB  [{credit}]")


if __name__ == "__main__":
    main()
