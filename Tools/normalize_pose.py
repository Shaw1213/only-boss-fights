"""
Normalize a cut-out character pose for import: one pixel scale per character, the body anchor at the
horizontal center, feet on the bottom edge (Unity pivot = bottom-center), sizes in multiples of 4.

Why: DreamLayer reframes every edit (the character can come back larger or smaller), so poses must be
rescaled to a shared scale, or the character grows and shrinks between frames in game. A rigid feature
measures the scale: the sword blade. For the hero, the blade's glowing teal rune line is found
automatically (--measure-teal); otherwise pass the blade length you measured with --blade.

Usage:
  python -I Tools/normalize_pose.py IN.cut.png --out OUT.png --blade-ref 812 --blade 640 --ref-scale 0.467
      [--anchor-x 310] [--measure-teal]
  python -I Tools/normalize_pose.py IN.cut.png --measure-teal          (just print the blade length)

  --ref-scale   scale applied to the reference (master) image to reach the target art size
  --blade-ref   ruler length in the master image (px); --blade = the same ruler in this image (px)
                (a rigid feature: a sword blade, or the eye line to chin)
                final scale = ref-scale * blade-ref / blade
  --anchor-x    the body's anchor column in IN (px); default = centroid of the hip band (50-65% height)
"""
import argparse
import json
import math
from collections import deque

import numpy as np
from PIL import Image


def teal_blade(rgba):
    """Endpoints and length of the longest teal (rune-line) structure, in input pixels."""
    a = rgba.astype(np.int16)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    teal = (al > 128) & (g > 150) & (b > 130) & (r < 140) & (g - r > 70)
    f = 4
    h, w = teal.shape
    coarse = teal[: h // f * f, : w // f * f].reshape(h // f, f, w // f, f).any(axis=(1, 3))
    seen = np.zeros_like(coarse)
    best = None
    for y0, x0 in zip(*np.nonzero(coarse)):
        if seen[y0, x0]:
            continue
        comp, q = [], deque([(y0, x0)])
        seen[y0, x0] = True
        while q:
            y, x = q.popleft()
            comp.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < coarse.shape[0] and 0 <= nx < coarse.shape[1] and coarse[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
        pts = np.array(comp, float)
        if len(pts) < 3:
            continue
        c = pts.mean(0)
        u, s, vt = np.linalg.svd(pts - c, full_matrices=False)
        proj = (pts - c) @ vt[0]
        length = (proj.max() - proj.min()) * f
        if best is None or length > best[0]:
            p0 = (c + vt[0] * proj.min()) * f
            p1 = (c + vt[0] * proj.max()) * f
            best = (length, p0[::-1].tolist(), p1[::-1].tolist())  # (len, (x,y), (x,y))
    return best


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input")
    ap.add_argument("--out")
    ap.add_argument("--ref-scale", type=float, default=1.0)
    ap.add_argument("--blade-ref", type=float)
    ap.add_argument("--blade", type=float)
    ap.add_argument("--anchor-x", type=float)
    ap.add_argument("--measure-teal", action="store_true")
    ap.add_argument("--side-pad", type=int, default=8)
    args = ap.parse_args()

    img = Image.open(args.input).convert("RGBA")
    a = np.asarray(img)
    report = {"input": args.input, "input_size": [img.width, img.height]}

    if args.measure_teal:
        m = teal_blade(a)
        if m:
            report["teal_blade_px"] = round(m[0], 1)
            report["teal_blade_ends"] = [[round(v) for v in m[1]], [round(v) for v in m[2]]]
            if args.blade is None:
                args.blade = m[0]
    if not args.out:
        print(json.dumps(report, indent=2))
        return

    scale = args.ref_scale
    if args.blade_ref and args.blade:
        scale *= args.blade_ref / args.blade
    report["scale"] = round(scale, 4)

    alpha = a[..., 3] > 0
    ys, xs = np.nonzero(alpha)
    top, bottom, left, right = ys.min(), ys.max(), xs.min(), xs.max()
    if args.anchor_x is None:
        h = bottom - top
        band = alpha[top + int(0.50 * h): top + int(0.65 * h)]
        bys, bxs = np.nonzero(band)
        args.anchor_x = float(bxs.mean()) if bxs.size else (left + right) / 2
    report["anchor_x"] = round(args.anchor_x, 1)

    # Crop to the subject, scale, then place the anchor at the center and the feet on the bottom edge.
    sub = img.crop((left, top, right + 1, bottom + 1))
    nw, nh = max(1, round(sub.width * scale)), max(1, round(sub.height * scale))
    sub = sub.resize((nw, nh), Image.LANCZOS)
    ax = (args.anchor_x - left) * scale
    half = math.ceil(max(ax, nw - ax)) + args.side_pad
    W = half * 2
    W += (-W) % 4
    H = nh + 4
    H += (-H) % 4
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    canvas.alpha_composite(sub, (round(W / 2 - ax), H - nh))
    canvas.save(args.out)
    report["output"] = args.out
    report["output_size"] = [W, H]
    report["body_height_px"] = nh
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
