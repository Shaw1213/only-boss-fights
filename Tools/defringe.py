"""
Removes the light rim that local background keying leaves around a cutout (sprite PNGs and sprite-sheet ZIPs).

DreamLayer returns art on a white (or off-white) background. The line art's anti-aliased edge pixels are a blend
of the dark outline and that white; a key that only drops near-white pixels keeps them fully opaque, so on a
dark scene the character wears a thin white outline (half of the edge pixels on our locally keyed sheets).

Because the background color is known, each edge pixel can be un-blended ("color to alpha"): split into the
foreground color at a partial alpha, C = a*F + (1-a)*B. Only a thin band is touched: opaque pixels reachable from
the transparent area within --depth pixels, and the walk stops at the dark outline (luminance below --dark), so
light fills inside the outline (a white shirt, eye whites, silver hair) are never reached.

Usage:
  python -I Tools/defringe.py FILE.png|FILE.zip [...] [--bg 255,255,255] [--depth 2] [--dark 90] [--out OUT] [--dry-run]
In place unless --out (single input). A ZIP's sheet.png cells are processed and frames/ rewritten from them.
"""
import argparse
import io
import json
import zipfile

import numpy as np
from PIL import Image


def dilate(m):
    out = m.copy()
    out[1:] |= m[:-1]
    out[:-1] |= m[1:]
    out[:, 1:] |= m[:, :-1]
    out[:, :-1] |= m[:, 1:]
    return out


def defringe(rgba, bg=(255, 255, 255), depth=2, dark=90.0):
    """Returns (new RGBA array, pixels changed, alpha removed in pixel-equivalents)."""
    rgb = rgba[..., :3].astype(np.float64)
    a = rgba[..., 3].astype(np.float64) / 255.0
    opaque = a > 0
    lum = rgb.mean(-1)

    # The edge band: grow from the transparent area through light pixels; dark pixels (the outline) join the band
    # but end the walk there.
    band = np.zeros(a.shape, bool)
    reach = ~opaque
    for _ in range(depth):
        grow = dilate(reach) & opaque & ~band
        band |= grow
        reach = grow & (lum >= dark)

    # Color to alpha against the known background, per channel: how far the pixel is from the background,
    # relative to the furthest it could be in that direction.
    B = np.array(bg, np.float64)
    C = rgb[band]
    below = np.where(B > 0, (B - C) / np.maximum(B, 1e-6), 0.0)
    above = np.where(B < 255, (C - B) / np.maximum(255.0 - B, 1e-6), 0.0)
    ac = np.clip(np.max(np.where(C < B, below, above), axis=1), 0.0, 1.0)
    safe = np.maximum(ac, 1e-6)[:, None]
    F = np.where(ac[:, None] > 0, (C - B) / safe + B, C)

    out = rgba.copy()
    new_a = a[band] * ac
    out[..., :3][band] = np.clip(np.round(F), 0, 255).astype(np.uint8)
    out[..., 3][band] = np.clip(np.round(new_a * 255.0), 0, 255).astype(np.uint8)
    changed = int((np.abs(out.astype(int) - rgba.astype(int)).sum(-1) > 0).sum())
    removed = float((a[band] - new_a).sum())
    return out, changed, removed


def process_png(path, out, args):
    img = np.array(Image.open(path).convert("RGBA"))
    new, changed, removed = defringe(img, args.bg, args.depth, args.dark)
    if not args.dry_run:
        Image.fromarray(new).save(out or path)
    return {"file": path, "changed_px": changed, "alpha_removed_px": round(removed)}


def process_zip(path, out, args):
    with zipfile.ZipFile(path) as z:
        entries = {n: z.read(n) for n in z.namelist()}
    atlas = json.loads(entries["atlas.json"])
    sheet = np.array(Image.open(io.BytesIO(entries["sheet.png"])).convert("RGBA"))
    changed = removed = 0
    frame_names = sorted(n for n in entries if n.startswith("frames/") and n.endswith(".png"))
    keys = sorted(atlas["frames"])
    for key, frame_name in zip(keys, frame_names):
        r = atlas["frames"][key]["frame"]
        cell = sheet[r["y"]:r["y"] + r["h"], r["x"]:r["x"] + r["w"]]
        new, c, rem = defringe(cell, args.bg, args.depth, args.dark)
        sheet[r["y"]:r["y"] + r["h"], r["x"]:r["x"] + r["w"]] = new
        buf = io.BytesIO()
        Image.fromarray(new).save(buf, "PNG")
        entries[frame_name] = buf.getvalue()
        changed += c
        removed += rem
    buf = io.BytesIO()
    Image.fromarray(sheet).save(buf, "PNG")
    entries["sheet.png"] = buf.getvalue()
    atlas["defringed"] = {"bg": list(args.bg), "depth": args.depth, "dark": args.dark}
    entries["atlas.json"] = json.dumps(atlas, indent=2).encode("utf-8")
    if not args.dry_run:
        with zipfile.ZipFile(out or path, "w", zipfile.ZIP_DEFLATED) as z:
            for n, data in entries.items():
                z.writestr(n, data)
    return {"file": path, "frames": len(keys), "changed_px": changed, "alpha_removed_px": round(removed)}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("inputs", nargs="+")
    ap.add_argument("--bg", default="255,255,255", help="background color the art was keyed from, R,G,B")
    ap.add_argument("--depth", type=int, default=2, help="band width in pixels from the transparent area")
    ap.add_argument("--dark", type=float, default=90.0, help="luminance below which a pixel counts as outline (ends the band)")
    ap.add_argument("--out", help="output path (single input only)")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    args.bg = tuple(int(v) for v in args.bg.split(","))
    if args.out and len(args.inputs) > 1:
        ap.error("--out needs a single input")
    for path in args.inputs:
        rep = process_zip(path, args.out, args) if path.lower().endswith(".zip") else process_png(path, args.out, args)
        print(json.dumps(rep))


if __name__ == "__main__":
    main()
