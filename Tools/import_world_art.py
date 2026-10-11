"""
Imports the DreamLayer world art with provenance sidecars (<name>.dreamlayer.json):
  - backgrounds -> Assets/Art/DreamLayer/Backgrounds/, fitted to the game camera (Tools/fit_background.py).
    A phase-2 edit is fitted with the same numbers as its calm version, so they line up pixel for pixel.
  - props (the lobby relics, cut from one generated sheet) -> Assets/Art/DreamLayer/Props/, cut out locally
    (Tools/clean_cutout.py, 0 credits).

Usage:
  python -I Tools/import_world_art.py            # imports everything in the tables below
  python -I Tools/import_world_art.py lobby      # only the named entries
"""
import json
import os
import subprocess
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = "Assets/Art/DreamLayer/Backgrounds"
PROPS_OUT = "Assets/Art/DreamLayer/Props"

# name: (raw image, operation, fit args, ground row in the fitted image, note)
# Ground row = where the fighters' feet stand: 25% into the floor band, just in front of the back step.
BACKGROUNDS = {
    "arena1": ("ArtRaw/world/arena1.png", "text_to_image (generate, 16:9)",
               ["--floor-row", "1280", "--stretch", "1.84"], 1219.5, "Boss 1 arena: the ruined hall of the sun knights"),
    "arena1_burning": ("ArtRaw/world/arena1-burning.png", "image_to_image (edit of arena1)",
                       ["--floor-row", "1280", "--stretch", "1.84"], 1219.5, "Boss 1 phase 2: the same hall, now burning"),
    # The lobby's floor was a thin strip, and the hero stood on the pedestals' base line while drawn in front of
    # them, so he read as floating. The strip in front of the pedestals (below their base outline, row 1302) is
    # stretched 2x into a real floor, and he stands 25% into it, in front of the pedestals.
    # The King's throne room: its floor's back edge is row 1116, so the feet (row 1219.5, shared with arena1) stand a
    # third of the way into the floor without stretching. Imported as generated.
    "arena2": ("ArtRaw/world/arena2.png", "text_to_image (generate, 16:9)", [], 1219.5,
               "Boss 2 arena: the Frozen King's throne room"),
    "arena2_frozen": ("ArtRaw/world/arena2-frozen.png", "image_to_image (edit of arena2)", [], 1219.5,
                      "Boss 2 phase 2: the same throne room, frozen over"),
    # The final fight: the King's throne room after the god took it (an edit of arena2: same camera, floor back edge
    # row 1098 vs 1116), and its phase 2, the wall torn open (an edit of arena3). Same feet row as the throne room.
    "arena3": ("ArtRaw/final/arena3.png", "image_to_image (edit of arena2)", [], 1219.5,
               "Final arena: the throne room, taken by the Unknown One"),
    "arena3_torn": ("ArtRaw/final/arena3-torn.png", "image_to_image (edit of arena3)", [], 1219.5,
                    "Final arena phase 2: the god wakes and the throne room tears open"),
    # The ending illustration: the knight's defeated pose (fight 1) composited into arena3 as the reference, then
    # edited: the god destroyed, the tentacles turned to stone, dawn light, the knight at rest.
    "ending": ("ArtRaw/ending/ending.png", "image_to_image (edit of a composite: knight-defeated in arena3)", [], 1219.5,
               "Ending: the knight at rest in the throne room at dawn"),
    "lobby": ("ArtRaw/world/lobby.png", "text_to_image (generate, 16:9)",
              ["--floor-row", "1302", "--stretch", "2.0"], 1233, "Lobby: a moonlit sanctuary with three pedestals for the relics"),
}

# name: (sheet, crop box in the sheet, note)
PROPS = {
    "relic_sword": ("ArtRaw/world/relics.png", (250, 110, 630, 1340), "the Knight's relic: his longsword (ring pommel, red grip)"),
    "relic_crown": ("ArtRaw/world/relics.png", (870, 380, 1550, 1020), "the King's relic: the crown"),
    "relic_eye": ("ArtRaw/world/relics.png", (1690, 480, 2330, 1020), "the Unknown One's relic: the eye (placeholder design)"),
}


def provenance(raw):
    base = os.path.splitext(raw)[0]
    result = json.load(open(base + ".result.json", encoding="utf-8-sig"))  # PowerShell writes a BOM
    prompt = open(base + ".prompt.txt", encoding="utf-8").read().strip()
    return result, prompt


def main():
    os.chdir(ROOT)
    only = set(sys.argv[1:])
    os.makedirs(OUT, exist_ok=True)
    for name, (raw, op, fit, ground_row, note) in BACKGROUNDS.items():
        if only and name not in only:
            continue
        if not os.path.exists(raw):
            print(f"{name}: {raw} not generated yet, skipped")
            continue
        dst = f"{OUT}/{name}.png"
        if fit:
            res = subprocess.run([sys.executable, "-I", "Tools/fit_background.py", raw, "--out", dst] + fit,
                                 capture_output=True, text=True, check=True).stdout.strip()
        else:
            Image.open(raw).convert("RGB").save(dst)
            res = f"{raw} -> {dst} (unfitted)"
        result, prompt = provenance(raw)
        sidecar = {
            "asset": dst,
            "source": raw,
            "operation": op,
            "execution_id": result["execution_id"],
            "idempotency_key": result["idempotency_key"],
            "credits": 1,
            "prompt": prompt,
            "note": note,
            "pipeline": {
                "fit": ("Tools/fit_background.py " + " ".join(fit) + " (floor stretched so feet land on it, top trimmed)") if fit else "none",
                "ground_row_px": ground_row,
            },
        }
        with open(f"{OUT}/{name}.dreamlayer.json", "w", encoding="utf-8", newline="\n") as f:
            json.dump(sidecar, f, indent=2, ensure_ascii=False)
        print(res)

    os.makedirs(PROPS_OUT, exist_ok=True)
    for name, (sheet, box, note) in PROPS.items():
        if only and name not in only:
            continue
        if not os.path.exists(sheet):
            print(f"{name}: {sheet} not generated yet, skipped")
            continue
        crop = f"ArtRaw/world/{name}.crop.png"
        Image.open(sheet).convert("RGB").crop(box).save(crop)
        dst = f"{PROPS_OUT}/{name}.png"
        out = subprocess.run([sys.executable, "-I", "Tools/clean_cutout.py", crop, "--out", dst, "--no-preview"],
                             capture_output=True, text=True, check=True).stdout
        cut = json.loads(out)
        result, prompt = provenance(sheet)
        sidecar = {
            "asset": dst,
            "source": f"{sheet} (crop {list(box)})",
            "operation": "text_to_image (generate, 16:9): one sheet for all three relics",
            "execution_id": result["execution_id"],
            "idempotency_key": result["idempotency_key"],
            "credits": "1 for the sheet of 3",
            "prompt": prompt,
            "note": note,
            "pipeline": {"cutout": "Tools/clean_cutout.py (local, 0 credits)",
                         "background_removed_pct": cut.get("background_removed_pct"),
                         "enclosed_gaps_removed": cut.get("enclosed_gaps_removed")},
        }
        with open(f"{PROPS_OUT}/{name}.dreamlayer.json", "w", encoding="utf-8", newline="\n") as f:
            json.dump(sidecar, f, indent=2, ensure_ascii=False)
        print(f"{name}: {crop} -> {dst} ({cut.get('output_size')})")


if __name__ == "__main__":
    main()
