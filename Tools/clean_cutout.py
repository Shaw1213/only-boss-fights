"""
Free local background removal for DreamLayer images generated on a plain flat background.

Why: DreamLayer's paid cutout costs a credit per image; our style (thick dark outlines on a flat
white background) can be keyed out locally. Steps follow the approach in DreamLayer's own sprite
cleanup notes, reimplemented here with NumPy + Pillow only:

  1. Estimate the background color from the image border; erase guide lines drawn across it.
  2. Remove only background that is CONNECTED TO THE BORDER (flood fill), so white inside the
     outlines (emblems, highlights) is kept. A coarse pass (blocked by outlines) + full-res refine.
     Then remove enclosed gaps (arm/torso) that are exactly the background color and not tiny.
  3. Peel light halo pixels from the edge inward (the dark outline stops the peel).
  4. Remove a thin ground line / floor slab drawn under the feet (and, with --floor-shadow, a grey cast shadow there).
  5. Drop tiny opaque specks (textured backgrounds).
  6. Defringe: un-blend the anti-aliased edge from the background color (Tools/defringe.py), so the cutout
     has no light rim on a dark scene.
  7. Crop to the subject, pad, and make width/height multiples of 4 (WebGL DXT compression needs it).
  8. Write a review preview over dark and saturated backgrounds, and print a JSON report.

Usage:
  python -I Tools/clean_cutout.py IN.png [--out OUT.png] [--tol 24] [--pad 8] [--gap-core 4] [--floor-shadow]
      [--no-preview] [--no-defringe]
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # -I leaves the script's own folder off the path
from defringe import defringe  # noqa: E402


def dilate(m):
    out = m.copy()
    out[1:, :] |= m[:-1, :]
    out[:-1, :] |= m[1:, :]
    out[:, 1:] |= m[:, :-1]
    out[:, :-1] |= m[:, 1:]
    return out


def geodesic_fill(seed, allowed, max_iter=5000):
    """Grow `seed` through `allowed` pixels (4-connected) until it stops changing."""
    cur = seed & allowed
    count = int(cur.sum())
    for _ in range(max_iter):
        nxt = dilate(cur) & allowed
        n = int(nxt.sum())
        if n == count:
            break
        cur, count = nxt, n
    return cur


def small_blobs(mask, min_px):
    """Opaque connected components (8-connected) smaller than min_px, as a mask.
    Run-length labeling with union-find: a textured background leaves a few hundred tiny specks; labeling runs
    instead of pixels keeps it fast in plain numpy."""
    parent = []

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    runs = []  # (y, x0, x1, label)
    prev = []
    for y in range(mask.shape[0]):
        row = mask[y]
        if not row.any():
            prev = []
            continue
        d = np.diff(np.concatenate(([0], row.astype(np.int8), [0])))
        starts, ends = np.where(d == 1)[0], np.where(d == -1)[0]
        cur = []
        for x0, x1 in zip(starts, ends):
            label = len(parent)
            parent.append(label)
            for px0, px1, pl in prev:
                if px0 <= x1 and px1 >= x0:  # overlapping or diagonal-touching runs (8-connected)
                    a, b = find(label), find(pl)
                    if a != b:
                        parent[a] = b
            cur.append((x0, x1, label))
            runs.append((y, x0, x1, label))
        prev = cur
    sizes = {}
    for y, x0, x1, label in runs:
        r = find(label)
        sizes[r] = sizes.get(r, 0) + (x1 - x0)
    out = np.zeros_like(mask)
    for y, x0, x1, label in runs:
        if sizes[find(label)] < min_px:
            out[y, x0:x1] = True
    return out


def border_mask(shape):
    m = np.zeros(shape, bool)
    m[0, :] = m[-1, :] = m[:, 0] = m[:, -1] = True
    return m


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input")
    ap.add_argument("--out", help="output PNG (default: <input>.cut.png)")
    ap.add_argument("--tol", type=float, default=24.0, help="color distance from the background that still counts as background")
    ap.add_argument("--pad", type=int, default=8, help="transparent padding around the cropped subject (px)")
    ap.add_argument("--gap-tol", type=float, default=10.0, help="enclosed gaps: max color distance from the background")
    ap.add_argument("--gap-core", type=int, default=4, help="enclosed gaps: erosion steps a gap must survive to be removed")
    ap.add_argument("--keep-enclosed", action="store_true", help="never remove enclosed regions")
    ap.add_argument("--keep-lines", action="store_true", help="don't remove guide lines drawn across the image")
    ap.add_argument("--no-crop", action="store_true", help="keep the input canvas size (sprite-sheet frames)")
    ap.add_argument("--no-preview", action="store_true")
    ap.add_argument("--no-defringe", action="store_true", help="keep the edge as keyed (light rim on dark scenes)")
    ap.add_argument("--floor-shadow", action="store_true",
                    help="clear grey (unsaturated, mid-tone) pixels near the subject's bottom: a drawn floor shadow")
    ap.add_argument("--floor-shadow-band", type=float, default=0.08, help="--floor-shadow: band height, fraction of the subject")
    ap.add_argument("--min-blob", type=int, default=600,
                    help="drop opaque specks smaller than this many pixels (textured backgrounds); 0 = keep everything")
    args = ap.parse_args()

    src = Image.open(args.input).convert("RGBA")
    a = np.asarray(src).astype(np.int16)
    rgb = a[..., :3]
    h, w = rgb.shape[:2]

    # 1. Background color = median of the OPAQUE border pixels (sprite-sheet cells can have a transparent
    #    padding ring); pixels that are already transparent count as background.
    alpha_in = a[..., 3]
    band = np.zeros(alpha_in.shape, bool)
    band[:6, :] = band[-6:, :] = band[:, :6] = band[:, -6:] = True
    opaque_band = band & (alpha_in > 0)
    bg = np.median(rgb[opaque_band] if opaque_band.any() else rgb[band], axis=0)
    dist = np.sqrt(((rgb - bg) ** 2).sum(-1))
    bglike = (dist < args.tol) | (alpha_in == 0)

    # 0. Guide / ground lines drawn across the image (a prompt saying "same ground line" got literal
    #    lines): thin, low-saturation rows covering a large part of the width. A pixel is repainted
    #    as background only where the pixels a few rows above AND below are background, so the
    #    character itself is never touched.
    lines_removed = 0
    if not args.keep_lines:
        sat = rgb.max(-1) - rgb.min(-1)
        lineish = ~bglike & (sat < 30) & (rgb.mean(-1) > 90)
        rows = np.where(lineish.mean(axis=1) > 0.25)[0]
        if rows.size:
            band = np.zeros(h, bool)
            for y in rows:
                band[max(0, y - 2): y + 3] = True
            k = 6
            above = np.zeros_like(bglike)
            below = np.zeros_like(bglike)
            above[k:] = bglike[:-k]
            below[:-k] = bglike[k:]
            hit = band[:, None] & lineish & above & below
            lines_removed = int(hit.sum())
            rgb = rgb.copy()
            rgb[hit] = bg.astype(np.int16)
            dist = np.sqrt(((rgb - bg) ** 2).sum(-1))
            bglike = (dist < args.tol) | (alpha_in == 0)

    # 2. Border-connected background. Coarse pass at 1/4 scale where a cell is background only if
    #    ALL its pixels are (so thin outlines still block), then refine at full resolution.
    f = 4
    hc, wc = h // f, w // f
    coarse_allowed = bglike[: hc * f, : wc * f].reshape(hc, f, wc, f).all(axis=(1, 3))
    coarse = geodesic_fill(border_mask(coarse_allowed.shape), coarse_allowed)
    up = np.zeros((h, w), bool)
    up[: hc * f, : wc * f] = np.repeat(np.repeat(coarse, f, 0), f, 1)
    background = geodesic_fill((up | border_mask((h, w))) & bglike, bglike)

    # 2b. Enclosed gaps (e.g. between an arm and the torso): background the outlines fully enclose,
    #     so the border fill can't reach it. Removed only if it is (nearly) the exact background
    #     color AND contains a solid core of at least (2*gap_core+1)^2 px: opening by reconstruction.
    #     Small highlights, eye whites and off-white emblems survive. Characters should avoid large
    #     pure-white areas (use cream / off-white), or pass --keep-enclosed.
    gaps_removed = 0
    if not args.keep_enclosed:
        candidate = (dist < args.gap_tol) & ~background
        core = candidate.copy()
        for _ in range(args.gap_core):
            core = ~dilate(~core)  # erode
        gaps = geodesic_fill(core, candidate)
        gaps_removed = int(gaps.sum())
        background |= gaps
    alpha = np.where(background, 0, 255).astype(np.uint8)
    enclosed_light = int((bglike & ~background).sum())  # e.g. a white emblem inside the outlines

    # 3. Halo peel: light, near-background pixels on the edge become transparent (2 passes).
    halo_removed = 0
    for _ in range(2):
        transparent = alpha == 0
        edge = (alpha > 0) & dilate(transparent)
        halo = edge & (dist < args.tol * 2.5)
        halo_removed += int(halo.sum())
        alpha[halo] = 0

    # 4. Ground line / floor slab: in the bottom band, clear opaque pixels that stick out sideways
    #    beyond the feet (the row a little above the band).
    line_removed = 0
    rows = np.where((alpha > 0).any(axis=1))[0]
    if rows.size:
        y0, y1 = int(rows[0]), int(rows[-1])
        band = max(3, int(0.015 * (y1 - y0)))
        ref = y1 - band - 2
        ref_x = np.where(alpha[ref] > 0)[0] if ref > y0 else np.array([])
        if ref_x.size:
            lo, hi = int(ref_x[0]) - 4, int(ref_x[-1]) + 4
            for y in range(y1 - band, y1 + 1):
                xs = np.where(alpha[y] > 0)[0]
                if xs.size and (xs[0] < lo or xs[-1] > hi):
                    outside = np.zeros(w, bool)
                    outside[:lo] = True
                    outside[hi + 1:] = True
                    hit = outside & (alpha[y] > 0)
                    line_removed += int(hit.sum())
                    alpha[y][hit] = 0

    # 4b. A drawn floor shadow (the King's cleave strike): grey, unsaturated, mid-tone pixels in the bottom 8%. The
    #     characters' own colors there (gold, pale blue steel, dark outlines) are either saturated or dark, so they stay.
    shadow_removed = 0
    if args.floor_shadow:
        rows = np.where((alpha > 0).any(axis=1))[0]
        if rows.size:
            y0, y1 = int(rows[0]), int(rows[-1])
            top = y1 - max(4, int(args.floor_shadow_band * (y1 - y0)))
            sat = rgb.max(-1) - rgb.min(-1)
            val = rgb.mean(-1)
            grey = (alpha > 0) & (sat < 20) & (val > 70) & (val < 225)
            # Columns with no foot in the band (no saturated or light pixel nearby) hold only shadow: clear everything
            # unsaturated there, the shadow's dark core included.
            foot = (alpha > 0) & ((sat >= 25) | (val >= 225))
            foot[:top] = False
            cols = foot.any(axis=0)
            near = cols.copy()
            for k in range(1, 7):
                near[k:] |= cols[:-k]
                near[:-k] |= cols[k:]
            bare = (alpha > 0) & (sat < 20) & ~near[None, :]
            hit = grey | bare
            hit[:top] = False
            shadow_removed = int(hit.sum())
            alpha[hit] = 0

    # 5. Specks: a textured off-white background (DreamLayer sometimes returns paper grain) leaves tiny opaque
    #    flecks that pass the key and would stretch the crop to the whole canvas.
    specks_removed = 0
    if args.min_blob > 0:
        specks = small_blobs(alpha > 0, args.min_blob)
        specks_removed = int(specks.sum())
        alpha[specks] = 0

    # 6. Defringe: the edge pixels the key keeps are a blend of the outline and the background (a light rim on
    #    dark scenes). Un-blend them against the measured background color: foreground color at a partial alpha.
    defringed = 0
    if not args.no_defringe:
        rgba, defringed, _ = defringe(np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), alpha]), tuple(float(c) for c in bg))
        rgb, alpha = rgba[..., :3].astype(np.int16), rgba[..., 3]

    # 7. Crop to the subject + padding; width/height rounded up to multiples of 4.
    ys, xs = np.where(alpha > 0)
    out_img = np.dstack([rgb.astype(np.uint8), alpha])
    if ys.size and not args.no_crop:
        top, bottom = max(0, ys.min() - args.pad), min(h, ys.max() + 1 + args.pad)
        left, right = max(0, xs.min() - args.pad), min(w, xs.max() + 1 + args.pad)
        out_img = out_img[top:bottom, left:right]
    ch, cw = out_img.shape[:2]
    ph, pw = (-ch) % 4, (-cw) % 4
    if (ph or pw) and not args.no_crop:
        out_img = np.pad(out_img, ((0, ph), (0, pw), (0, 0)))
    out_path = args.out or os.path.splitext(args.input)[0] + ".cut.png"
    Image.fromarray(out_img, "RGBA").save(out_path)

    # Remaining light fringe on the final edge (should be ~0 for clean results): fully opaque edge pixels closer
    # to the background color than to black, i.e. at least half background. (The old test, within 3x the key
    # tolerance of the background, missed the half-blended rim entirely.)
    fa = out_img[..., 3]
    frgb = out_img[..., :3].astype(np.float64)
    fedge = (fa == 255) & dilate(fa == 0)
    fringe = int((fedge & (np.sqrt(((frgb - bg) ** 2).sum(-1)) < np.sqrt((frgb ** 2).sum(-1)))).sum())

    # 8. Review preview: subject over dark gray and magenta, side by side.
    preview_path = None
    if not args.no_preview:
        cut = Image.fromarray(out_img, "RGBA")
        tiles = []
        for color in [(32, 32, 40, 255), (255, 0, 200, 255)]:
            tile = Image.new("RGBA", cut.size, color)
            tile.alpha_composite(cut)
            tiles.append(tile)
        sheet = Image.new("RGBA", (cut.width * 2, cut.height))
        sheet.paste(tiles[0], (0, 0))
        sheet.paste(tiles[1], (cut.width, 0))
        scale = 512 / sheet.height
        sheet = sheet.resize((max(1, int(sheet.width * scale)), 512), Image.LANCZOS)
        preview_path = os.path.splitext(out_path)[0] + ".preview.png"
        sheet.convert("RGB").save(preview_path)

    print(json.dumps({
        "input": args.input,
        "input_size": [w, h],
        "background_rgb": [int(round(c)) for c in bg],
        "background_removed_pct": round(100.0 * float(background.mean()), 1),
        "guide_line_pixels_removed": lines_removed,
        "enclosed_gap_pixels_removed": gaps_removed,
        "enclosed_light_pixels_kept": enclosed_light,
        "halo_pixels_removed": halo_removed,
        "ground_line_pixels_removed": line_removed,
        "floor_shadow_pixels_removed": shadow_removed,
        "speck_pixels_removed": specks_removed,
        "defringed_pixels": defringed,
        "output": out_path,
        "output_size": [int(out_img.shape[1]), int(out_img.shape[0])],
        "remaining_light_fringe_pixels": fringe,
        "preview": preview_path,
    }, indent=2))


if __name__ == "__main__":
    main()
