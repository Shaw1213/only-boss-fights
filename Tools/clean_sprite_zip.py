"""
Free background removal for a DreamLayer sprite-sheet ZIP made with `background: keep` (about half the
price of `remove`). Each frame is keyed out with clean_cutout.py (no crop, so the cells stay aligned),
then sheet.png and frames/ are rebuilt and the ZIP is written back out with the same atlas.json, so the
Unity importer treats it like a normal transparent sheet. atlas.json gets "local_cutout": true.

--snap-feet moves each frame down so its lowest opaque row sits on one shared baseline (the lowest
foot in the set). DreamLayer's ground_anchor alignment can leave a run cycle hopping (55 px of
baseline spread in a 256 px sheet); for a side-view game the feet should stay on the floor.

--airborne (with --snap-feet) names frames where both feet are off the floor (a run's flight phase, 1-based).
Snapping them would pull the feet down to the floor and dip the body mid-jump; instead each moves by the mean
shift of the nearest grounded frames on either side (wrapping), so the body stays continuous and the lift is kept.

--remove-drift takes out forward travel baked into a cycle: it fits a straight line to the head's x
(the topmost opaque rows) across the frames and shifts each frame back by it, keeping the natural sway.
The 512 px combat-step sheet walked 113 px (22% of the cell) across 8 frames, so its loop jumped back;
the game moves the character itself, so a cycle should stay in place.

Usage:
  python -I Tools/clean_sprite_zip.py IN.zip --out OUT.zip [--tol 24] [--snap-feet [--airborne 3,4]] [--remove-drift] [--gap-core 2]
"""
import argparse
import io
import json
import os
import subprocess
import sys
import tempfile
import zipfile

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input")
    ap.add_argument("--out", required=True)
    ap.add_argument("--tol", default="24")
    ap.add_argument("--snap-feet", action="store_true")
    ap.add_argument("--remove-drift", action="store_true")
    ap.add_argument("--airborne", default="", help="with --snap-feet: 1-based frames in the air, moved with their grounded neighbors")
    ap.add_argument("--gap-core", help="passed to clean_cutout.py: smaller removes narrower enclosed gaps (e.g. 2 for the "
                                       "sliver of background between two legs in a feet-together frame); default 4")
    args = ap.parse_args()

    src = zipfile.ZipFile(args.input)
    names = src.namelist()
    atlas_name = next(n for n in names if n.endswith("atlas.json"))
    sheet_name = next(n for n in names if n.endswith("sheet.png"))
    atlas = json.loads(src.read(atlas_name))
    sheet = Image.open(io.BytesIO(src.read(sheet_name))).convert("RGBA")
    out_sheet = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
    cuts = {}
    cleaned = {}
    report = []

    with tempfile.TemporaryDirectory() as tmp:
        for key, f in atlas["frames"].items():
            r = f["frame"]
            cell = sheet.crop((r["x"], r["y"], r["x"] + r["w"], r["y"] + r["h"]))
            cin, cout = os.path.join(tmp, f"{key}.png"), os.path.join(tmp, f"{key}.cut.png")
            cell.save(cin)
            cmd = [sys.executable, "-I", os.path.join(HERE, "clean_cutout.py"), cin, "--out", cout, "--no-crop", "--no-preview", "--tol", args.tol]
            if args.gap_core:
                cmd += ["--gap-core", args.gap_core]
            res = subprocess.run(cmd, capture_output=True, text=True)
            if res.returncode != 0:
                raise RuntimeError(res.stderr)
            rep = json.loads(res.stdout)
            cuts[key] = Image.open(cout).convert("RGBA")
            report.append([key, rep["background_removed_pct"], rep["remaining_light_fringe_pixels"], 0])

    def bottom(im):
        rows = np.nonzero((np.asarray(im)[..., 3] > 128).any(axis=1))[0]
        return int(rows[-1]) if rows.size else im.height - 1

    def head_x(im):
        a = np.asarray(im)[..., 3] > 128
        rows = np.nonzero(a.any(axis=1))[0]
        if not rows.size:
            return im.width / 2
        top = int(rows[0])
        return float(np.nonzero(a[top:top + max(4, int(im.height * 0.06))])[1].mean())

    def touches_edge(im, band=6):
        a = np.asarray(im)[..., 3] > 128
        return bool(a[:band].any() or a[:, :band].any() or a[:, -band:].any())

    # Frames the cell cut off (e.g. a blade tip past the edge): recorded for patch_blade_tip.py.
    clipped = [row[0] for row in report if touches_edge(cuts[row[0]])]
    baseline = max(bottom(c) for c in cuts.values()) if args.snap_feet else None
    drift = None
    if args.remove_drift:
        xs = [head_x(cuts[row[0]]) for row in report]
        drift = float(np.polyfit(np.arange(len(xs)), xs, 1)[0])  # px per frame
    # Vertical shifts: grounded frames put their lowest foot on the baseline; airborne ones follow their neighbors.
    airborne = {int(v) - 1 for v in args.airborne.split(",") if v.strip()} if args.snap_feet else set()
    shifts = [baseline - bottom(cuts[row[0]]) if baseline is not None else 0 for row in report]
    n = len(report)
    for i in sorted(airborne):
        def nearest(step):
            j = (i + step) % n
            while j in airborne and j != i:
                j = (j + step) % n
            return shifts[j]
        shifts[i] = int(round((nearest(-1) + nearest(1)) / 2))
    for i, row in enumerate(report):
        key = row[0]
        cut = cuts[key]
        dx = -int(round(drift * i)) if drift is not None else 0
        dy = shifts[i]
        row[3] = dy
        row.append(dx)
        if dx or dy:
            moved = Image.new("RGBA", cut.size, (0, 0, 0, 0))
            moved.alpha_composite(cut, (max(0, dx), dy), (max(0, -dx), 0))
            cut = moved
        r = atlas["frames"][key]["frame"]
        out_sheet.paste(cut, (r["x"], r["y"]))
        buf = io.BytesIO()
        cut.save(buf, "PNG")
        cleaned[key] = buf.getvalue()

    atlas["local_cutout"] = True
    if args.snap_feet:
        atlas["feet_snapped"] = True
    if airborne:
        atlas["airborne_frames"] = sorted(i + 1 for i in airborne)
    if drift is not None:
        atlas["drift_removed_px_per_frame"] = round(drift, 2)
    if clipped:
        atlas["edge_clipped_frames"] = clipped
    with zipfile.ZipFile(args.out, "w", zipfile.ZIP_DEFLATED) as dst:
        for n in names:
            base = os.path.basename(n)
            stem = os.path.splitext(base)[0]
            if n == sheet_name:
                buf = io.BytesIO()
                out_sheet.save(buf, "PNG")
                dst.writestr(n, buf.getvalue())
            elif n == atlas_name:
                dst.writestr(n, json.dumps(atlas, indent=2))
            elif n.startswith("frames/") and stem in cleaned:
                dst.writestr(n, cleaned[stem])
            else:
                dst.writestr(n, src.read(n))

    for key, pct, fringe, shift, dx in report:
        print(f"{key}: background removed {pct}%, light fringe {fringe} px" + (f", moved down {shift} px" if args.snap_feet else "") +
              (f", moved {dx:+d} px sideways" if drift is not None else ""))
    if drift is not None:
        print(f"forward drift removed: {drift:.1f} px per frame")
    if clipped:
        print(f"cut off at the cell edge: {', '.join(clipped)}")
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()
