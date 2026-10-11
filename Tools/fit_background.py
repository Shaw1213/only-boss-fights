"""
Fits a generated side-view background to the game's camera: stretches the floor band so the fighters'
feet land on the floor, then trims the top to keep the aspect ratio.

DreamLayer's 16:9 arena came back with the floor in the bottom 11% (rows 1280-1440 of 1440), but the
fighters stand 20% up the view. Scaling the whole image up enough would have cropped away most of the
hall; stretching the plain stone floor (and trimming the top of the column capitals) keeps the hall.

Usage:
  python -I Tools/fit_background.py IN.png --out OUT.png --floor-row 1280 --stretch 1.84 [--crop-top N]
  (--crop-top defaults to whatever keeps the original height)
"""
import argparse

from PIL import Image


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input")
    ap.add_argument("--out", required=True)
    ap.add_argument("--floor-row", type=int, required=True, help="first row of the floor band (its back edge)")
    ap.add_argument("--stretch", type=float, required=True, help="vertical stretch of the floor band")
    ap.add_argument("--crop-top", type=int, help="rows to trim from the top (default: keep the original height)")
    args = ap.parse_args()

    im = Image.open(args.input).convert("RGB")
    w, h = im.size
    wall = im.crop((0, 0, w, args.floor_row))
    floor = im.crop((0, args.floor_row, w, h))
    floor = floor.resize((w, round(floor.height * args.stretch)), Image.LANCZOS)
    out = Image.new("RGB", (w, wall.height + floor.height))
    out.paste(wall, (0, 0))
    out.paste(floor, (0, wall.height))
    crop = args.crop_top if args.crop_top is not None else out.height - h
    out = out.crop((0, crop, w, out.height))
    out.save(args.out)
    print(f"{args.input}: floor {h - args.floor_row} -> {floor.height} rows, trimmed {crop} from the top, "
          f"floor back edge now at row {args.floor_row - crop} of {out.height} -> {args.out}")


if __name__ == "__main__":
    main()
