"""
Rebuilds a blade tip that a sprite-sheet cell cut off, using the same blade from a frame where it is whole.

DreamLayer's 512 px combat-step sheet let the hero walk forward until the sword tip left the cell (frames
6-8 lost 8 / 29 / 52 px of a ~194 px blade). After clean_sprite_zip.py --remove-drift puts the cycle back in
place, this finds the blade in every frame (its glowing teal line), measures it from the guard to the tip,
and for each frame the cell cut off (atlas "edge_clipped_frames", written by clean_sprite_zip.py; else any
blade shorter than --short x the median) copies the tip from the whole frame with the closest blade angle:
rotated to the short blade's angle and placed at the full length along it, behind the existing pixels.

Usage:
  python -I Tools/patch_blade_tip.py IN.zip --out OUT.zip [--short 0.95] [--tip 80]
"""
import argparse
import io
import json
import zipfile

import numpy as np
from PIL import Image


def blade(im):
    """Line through the blade: (point, unit direction pointing to the tip, guard-end projection, tip projection)."""
    a = np.asarray(im).astype(int)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    m = (al > 128) & (g > 140) & (b > 140) & (r < 150) & (g - r > 50)
    ys, xs = np.nonzero(m)
    if len(xs) < 50:
        return None
    pts = np.stack([xs, ys], 1).astype(float)
    sel = pts[pts[:, 0] > np.percentile(pts[:, 0], 40)]  # the blade, not the rune bracer
    for _ in range(3):
        c = sel.mean(0)
        d = np.linalg.svd(sel - c, full_matrices=False)[2][0]
        d = d if d[0] > 0 else -d
        sel = pts[np.abs((pts - c) @ np.array([-d[1], d[0]])) < 4]
    proj = np.sort((sel - c) @ d)
    gaps = np.nonzero(np.diff(proj) > 6)[0]  # the hand / guard breaks the line: keep the run that ends at the tip
    start = proj[gaps[-1] + 1] if len(gaps) else proj[0]
    return c, d, start, proj[-1]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input")
    ap.add_argument("--out", required=True)
    ap.add_argument("--short", type=float, default=0.95, help="a blade shorter than this x the median is patched")
    ap.add_argument("--tip", type=float, default=80, help="length of the copied tip, px")
    args = ap.parse_args()

    src = zipfile.ZipFile(args.input)
    names = src.namelist()
    atlas_name = next(n for n in names if n.endswith("atlas.json"))
    sheet_name = next(n for n in names if n.endswith("sheet.png"))
    atlas = json.loads(src.read(atlas_name))
    sheet = Image.open(io.BytesIO(src.read(sheet_name))).convert("RGBA")

    frames, lines = {}, {}
    for key, f in atlas["frames"].items():
        r = f["frame"]
        frames[key] = sheet.crop((r["x"], r["y"], r["x"] + r["w"], r["y"] + r["h"]))
        lines[key] = blade(frames[key])
    lengths = {k: v[3] - v[2] for k, v in lines.items() if v is not None}
    clipped = set(atlas.get("edge_clipped_frames") or [k for k, n in lengths.items() if n < np.median(list(lengths.values())) * args.short])
    whole = [k for k in lengths if k not in clipped]
    full = float(np.median([lengths[k] for k in whole]))
    patched = []

    for key, n in lengths.items():
        if key not in clipped or n >= full:
            continue
        c, d, start, _ = lines[key]
        angle = np.arctan2(d[1], d[0])
        donor = min(whole, key=lambda k: abs(np.arctan2(lines[k][1][1], lines[k][1][0]) - angle))
        dc, dd, dstart, dtip = lines[donor]
        tip_d = dc + dd * dtip
        # the donor's tip: opaque pixels near its blade line, within --tip px of the end
        im = np.asarray(frames[donor])
        ys, xs = np.nonzero(im[..., 3] > 0)
        rel = np.stack([xs, ys], 1) - tip_d
        along, across = rel @ dd, np.abs(rel @ np.array([-dd[1], dd[0]]))
        keep = (along > -args.tip) & (along <= 2) & (across < 8)
        patch = Image.new("RGBA", im.shape[1::-1], (0, 0, 0, 0))
        pa = np.asarray(patch).copy()
        pa[ys[keep], xs[keep]] = im[ys[keep], xs[keep]]
        patch = Image.fromarray(pa)
        # rotate about the donor tip to the short blade's angle, then move the tip to full length along it
        rot = np.degrees(np.arctan2(dd[1], dd[0]) - angle)
        patch = patch.rotate(rot, resample=Image.BICUBIC, center=tuple(tip_d))
        target = c + d * (start + full)
        offset = np.round(target - tip_d).astype(int)
        moved = Image.new("RGBA", patch.size, (0, 0, 0, 0))
        moved.alpha_composite(patch, (max(0, offset[0]), max(0, offset[1])), (max(0, -offset[0]), max(0, -offset[1])))
        moved.alpha_composite(frames[key])  # existing pixels stay on top; the patch only fills what's missing
        frames[key] = moved
        patched.append(f"{key}: blade {n:.0f} px -> {full:.0f} px, tip from {donor} (rotated {rot:+.1f} deg)")

    out_sheet = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
    cleaned = {}
    for key, f in atlas["frames"].items():
        r = f["frame"]
        out_sheet.paste(frames[key], (r["x"], r["y"]))
        buf = io.BytesIO()
        frames[key].save(buf, "PNG")
        cleaned[key] = buf.getvalue()
    atlas["blade_tips_patched"] = [p.split(":")[0] for p in patched]
    with zipfile.ZipFile(args.out, "w", zipfile.ZIP_DEFLATED) as dst:
        for nm in names:
            stem = nm.rsplit("/", 1)[-1].rsplit(".", 1)[0]
            if nm == sheet_name:
                buf = io.BytesIO()
                out_sheet.save(buf, "PNG")
                dst.writestr(nm, buf.getvalue())
            elif nm == atlas_name:
                dst.writestr(nm, json.dumps(atlas, indent=2))
            elif nm.startswith("frames/") and stem in cleaned:
                dst.writestr(nm, cleaned[stem])
            else:
                dst.writestr(nm, src.read(nm))
    print(f"full blade {full:.0f} px (median of the {len(whole)} whole frames)")
    print("\n".join(patched) if patched else "nothing to patch")
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()
