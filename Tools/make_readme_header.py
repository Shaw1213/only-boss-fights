"""
The header image of the project's public repository: the DreamLayer edit ArtRaw/readme/readme-header.png (the hero
and the villain's three forms on a sprite-sheet grid, top band left empty) with the title set in the game's gold
engraved capitals. Local compositing only, no credits.

  <out>/header.jpg           1600 x 800, the README header
  <out>/social-preview.png   1280 x 640, GitHub's social preview (Settings -> Social preview)

Usage: python -I Tools/make_readme_header.py OUT_DIR
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_itch_assets import ROOT, SUB_FONT, TITLE_FONT, gold_text  # noqa: E402


def main():
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    art = Image.open(os.path.join(ROOT, "ArtRaw/readme/readme-header.png")).convert("RGBA")
    W, H = art.size

    # The art's top band (above its first gold rule, 14% down) is dark and empty: the title goes there.
    band = int(H * 0.135)
    title = gold_text("ONLY BOSS FIGHTS", TITLE_FONT, 200)
    sub = gold_text("THE FALLEN KNIGHT, MADE WITH DREAMLAYER AND UNITY", SUB_FONT, 92, tracking=0.12)
    tw = int(W * 0.40)
    title = title.resize((tw, int(title.height * tw / title.width)), Image.LANCZOS)
    sw = int(tw * 0.92)
    sub = sub.resize((sw, int(sub.height * sw / sub.width)), Image.LANCZOS)
    gap = int(band * 0.05)
    block = title.height + gap + sub.height
    y = max(8, (band - block) // 2)
    art.alpha_composite(title, ((W - title.width) // 2, y))
    art.alpha_composite(sub, ((W - sub.width) // 2, y + title.height + gap))

    rgb = art.convert("RGB")
    rgb.resize((1600, 800), Image.LANCZOS).save(os.path.join(out_dir, "header.jpg"), quality=88, optimize=True)
    rgb.resize((1280, 640), Image.LANCZOS).save(os.path.join(out_dir, "social-preview.png"), optimize=True)
    print("wrote", os.path.join(out_dir, "header.jpg"), "and social-preview.png")


if __name__ == "__main__":
    main()
