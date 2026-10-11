"""
Takes the size drift out of a cleaned DreamLayer sprite-sheet ZIP: each frame is rescaled so the character is as tall
as in the reference frame, anchored on the feet (the shared baseline) and the torso's x, so a walk doesn't swell and
shrink with every step.

DreamLayer's walk sheets can zoom in across the cycle: the King's 8-frame walk grew 22% in height (307 -> 375 px) and
19% in shoulder width from frame 1 to frame 8, with the same pose at both ends of the loop. Height is measured from the
feet to the top of the head, skipping a plume or other swaying part by color (--skip-red: red-dominant pixels, the
plume; the tabard below the head doesn't matter).

--match-idle IDLE.png (the character's idle sprite, its pivot at the bottom center) also prints the Flipbook scale that
makes the reference frame as tall as the idle, and writes "pivot_x" into atlas.json so the torso sits the same distance
from the pivot as in the idle: the walk starts where the idle stands. (The importer's own hip measurement is pulled
forward by the Unknown One's claw arm.)

--clear-white (dark characters only) makes near-white opaque pixels transparent: background slivers the cutout left in
narrow gaps (between the Unknown One's tentacle loops and his armor). Never on a pale character: the King's armor and
blade highlights are near-white.

Run after clean_sprite_zip.py (--snap-feet --remove-drift): it needs transparent frames on one baseline.

Usage:
  python -I Tools/normalize_sheet_scale.py IN.zip --out OUT.zip [--ref 1] [--skip-red] [--match-idle IDLE.png] [--clear-white]
"""
import argparse
import io
import json
import zipfile

import numpy as np
from PIL import Image


def measure(im, skip_red):
    """(top, bottom, torso_x) of the opaque pixels, the top ignoring red-dominant pixels if asked."""
    a = np.asarray(im).astype(int)
    body = a[:, :, 3] > 128
    if skip_red:
        r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
        body &= ~((r > 90) & (r > 1.8 * g) & (r > 1.5 * b))
    ys, xs = np.nonzero(body)
    top, bottom = ys.min(), np.nonzero(a[:, :, 3] > 128)[0].max()
    upper = body[top:top + int((bottom - top) * 0.4)]
    torso_x = float(np.median(np.nonzero(upper)[1]))
    return top, bottom, torso_x


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input")
    ap.add_argument("--out", required=True)
    ap.add_argument("--ref", type=int, default=1, help="1-based frame whose size the others are scaled to")
    ap.add_argument("--skip-red", action="store_true", help="measure the head's top without red-dominant pixels (a plume)")
    ap.add_argument("--clear-white", action="store_true", help="dark characters: clear near-white leftover background")
    ap.add_argument("--match-idle", help="idle sprite (bottom-center pivot): print the Flipbook scale, write atlas pivot_x")
    args = ap.parse_args()

    src = zipfile.ZipFile(args.input)
    names = src.namelist()
    atlas_name = next(n for n in names if n.endswith("atlas.json"))
    sheet_name = next(n for n in names if n.endswith("sheet.png"))
    atlas = json.loads(src.read(atlas_name))
    frame_names = sorted(n for n in names if "frames/" in n and n.endswith(".png"))
    frames = [Image.open(io.BytesIO(src.read(n))).convert("RGBA") for n in frame_names]
    if args.clear_white:
        cleared = []
        for i, f in enumerate(frames):
            a = np.asarray(f).copy()
            white = (a[:, :, 3] > 0) & (a[:, :, 0] > 225) & (a[:, :, 1] > 225) & (a[:, :, 2] > 225)
            a[white, 3] = 0
            frames[i] = Image.fromarray(a)
            cleared.append(int(white.sum()))
        print("near-white pixels cleared per frame:", cleared)

    marks = [measure(f, args.skip_red) for f in frames]
    ref_h = marks[args.ref - 1][1] - marks[args.ref - 1][0]
    out_frames = []
    for i, (f, (top, bottom, tx)) in enumerate(zip(frames, marks)):
        s = ref_h / max(1, bottom - top)
        big = f.resize((max(1, round(f.width * s)), max(1, round(f.height * s))), Image.LANCZOS)
        # Anchor: the feet stay on the same row, the torso on the same column.
        out = Image.new("RGBA", f.size, (0, 0, 0, 0))
        dx, dy = round(tx - tx * s), round(bottom - bottom * s)
        out.paste(big, (dx, dy), big)
        out_frames.append(out)
        print(f"{frame_names[i].split('/')[-1]}: {bottom - top + 1} px tall -> x{s:.3f}")

    # Rebuild the sheet from the atlas rects (keyed by frame name).
    sheet = Image.open(io.BytesIO(src.read(sheet_name))).convert("RGBA")
    out_sheet = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
    for n, f in zip(frame_names, out_frames):
        r = atlas["frames"][n.split("/")[-1][:-4]]["frame"]
        out_sheet.paste(f, (r["x"], r["y"]))
    atlas["local_scale_normalized"] = {"ref_frame": args.ref, "skip_red": args.skip_red}
    if args.match_idle:
        idle = Image.open(args.match_idle).convert("RGBA")
        it, ib, itx = measure(idle, args.skip_red)
        rt, rb, rtx = measure(out_frames[args.ref - 1], args.skip_red)
        scale = (ib - it) / (rb - rt)
        torso_from_pivot = (itx - idle.width * 0.5) / scale  # in sheet pixels
        cell = out_frames[0].width
        atlas["pivot_x"] = round((rtx - torso_from_pivot) / cell, 4)
        print(f"match {args.match_idle}: Flipbook scale {scale:.3f}, pivot_x {atlas['pivot_x']}")

    with zipfile.ZipFile(args.out, "w", zipfile.ZIP_DEFLATED) as dst:
        for n in names:
            if n == atlas_name:
                dst.writestr(n, json.dumps(atlas, indent=2))
            elif n == sheet_name:
                b = io.BytesIO(); out_sheet.save(b, "PNG"); dst.writestr(n, b.getvalue())
            elif n in frame_names:
                b = io.BytesIO(); out_frames[frame_names.index(n)].save(b, "PNG"); dst.writestr(n, b.getvalue())
            else:
                dst.writestr(n, src.read(n))
    print("wrote", args.out)


if __name__ == "__main__":
    main()
