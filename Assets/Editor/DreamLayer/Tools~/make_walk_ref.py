"""
The reference for a walk sprite sheet: a character's cut-out on a white square canvas, left of center with room in
front (facing right), so a cycle that drifts forward (DreamLayer ignores "walking on the spot") never pushes the weapon
out of its cell. The knight's walk sheet used this framing (character ~81% of the canvas height, ~9.5% from the left);
a character with a long reach in front (the Unknown One's claw arm) gets a smaller share so the front stays clear.

Usage:
  python -I Tools/make_walk_ref.py CUT.png --out REF.png [--height 0.81] [--left 0.095] [--bottom 0.11]
"""
import argparse

from PIL import Image


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cut", help="a cut-out with transparency (clean_cutout.py output)")
    ap.add_argument("--out", required=True)
    ap.add_argument("--height", type=float, default=0.81, help="character height as a share of the canvas")
    ap.add_argument("--left", type=float, default=0.095, help="left margin as a share of the canvas")
    ap.add_argument("--bottom", type=float, default=0.11, help="floor margin under the feet as a share of the canvas")
    args = ap.parse_args()

    cut = Image.open(args.cut).convert("RGBA")
    cut = cut.crop(cut.split()[-1].getbbox())
    side = round(cut.height / args.height)
    if cut.width + args.left * side > side:
        raise SystemExit(f"{cut.width} px wide does not fit a {side} px canvas at --left {args.left}: lower --height")
    canvas = Image.new("RGBA", (side, side), (255, 255, 255, 255))
    x = round(args.left * side)
    y = side - round(args.bottom * side) - cut.height
    canvas.alpha_composite(cut, (x, max(0, y)))
    canvas.convert("RGB").save(args.out)
    front = side - (x + cut.width)
    print(f"{args.out}: {side} x {side}, character {cut.width} x {cut.height} at ({x}, {max(0, y)}), "
          f"{front} px ({front / side:.0%}) clear in front")


if __name__ == "__main__":
    main()
