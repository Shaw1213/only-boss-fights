"""
Builds the itch.io page images from the game's DreamLayer art (no credits: local compositing only).

  Builds/itch/logo.png          the title, gold engraved capitals with a dark outline (transparent)
  Builds/itch/cover.png         630 x 500 cover (the jam listing thumbnail) + cover@2x.png
  Builds/itch/banner.png        1280 x 400 page header, as wide as the 1280 x 720 game embed (itch widens the page
                                column to the embed; a banner wider than the column is squeezed into it and leaves
                                a gap under it, so upload it at the column's width) + banner-960.png for a 960 embed
  Builds/itch/icon.png          512 x 512 icon (the Unknown One's helm and the god's eye)

Usage: python -I Tools/make_itch_assets.py
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Builds", "itch")
FONTS = "C:/Windows/Fonts"
TITLE_FONT = os.path.join(FONTS, "CASTELAR.TTF")   # engraved capitals
SUB_FONT = os.path.join(FONTS, "PERTILI.TTF")      # Perpetua Titling
GOLD_TOP, GOLD_BOTTOM = (255, 236, 170), (196, 132, 40)
INK = (16, 10, 20)


def banner(art, bw):
    """The page banner at bw x bw / 3.2, built at 2x and downsampled once."""
    W, H = art.size
    bh = bw * 10 // 32
    sw, sh = bw * 2, bh * 2
    y0 = int(H * 0.08)
    strip = art.crop((0, y0, W, y0 + int(W * bh / bw))).resize((sw, sh), Image.LANCZOS).convert("RGBA")
    side = Image.new("L", (sw, 1))
    for x in range(sw):
        t = max(0.0, 1.0 - x / (sw * 0.55))
        side.putpixel((x, 0), int(255 * 0.8 * t))
    shade = Image.new("RGBA", (sw, sh), (8, 4, 12, 255))
    shade.putalpha(side.resize((sw, sh)))
    strip.alpha_composite(shade)
    lw = int(sw * 900 / 1920)
    lg = logo(lw)
    strip.alpha_composite(lg, (int(sw * 70 / 1920), (sh - lg.height) // 2))
    return strip.resize((bw, bh), Image.LANCZOS)


def trim_black_bars(im, threshold=14):
    """The cover edit came back with thin black bars top and bottom: crop rows that are almost pure black."""
    a = np.asarray(im.convert("L"), dtype=np.float32)
    rows = np.where(a.mean(axis=1) > threshold)[0]
    cols = np.where(a.mean(axis=0) > threshold)[0]
    return im.crop((int(cols[0]), int(rows[0]), int(cols[-1]) + 1, int(rows[-1]) + 1))


def gold_text(text, font_path, size, tracking=0.06, outline=None, shadow=True):
    """Text in a vertical gold gradient with a dark outline and a soft drop shadow, on a transparent canvas."""
    font = ImageFont.truetype(font_path, size)
    outline = outline if outline is not None else max(2, size // 14)
    # Measure with letter spacing.
    widths = [font.getbbox(ch)[2] - font.getbbox(ch)[0] if ch != " " else size * 0.35 for ch in text]
    gap = size * tracking
    w = int(sum(widths) + gap * (len(text) - 1) + outline * 4 + size * 0.2)
    asc, desc = font.getmetrics()
    h = int(asc + desc + outline * 4 + size * 0.2)
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    x = outline * 2 + size * 0.1
    y = outline * 2
    positions = []
    for ch, cw in zip(text, widths):
        if ch != " ":
            bx = font.getbbox(ch)[0]
            d.text((x - bx, y), ch, font=font, fill=255)
            positions.append((x - bx, y, ch))
        x += cw + gap
    # Outline: the mask grown by `outline` px.
    grown = mask.filter(ImageFilter.MaxFilter(outline * 2 + 1))
    # Gradient fill.
    grad = Image.new("RGB", (w, h))
    gd = ImageDraw.Draw(grad)
    for yy in range(h):
        t = min(1.0, max(0.0, (yy - y) / max(1, asc)))
        c = tuple(int(GOLD_TOP[i] + (GOLD_BOTTOM[i] - GOLD_TOP[i]) * t) for i in range(3))
        gd.line([(0, yy), (w, yy)], fill=c)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    if shadow:
        sh = grown.filter(ImageFilter.GaussianBlur(size * 0.06))
        shadow_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        shadow_layer.putalpha(sh.point(lambda v: int(v * 0.75)))
        out.alpha_composite(shadow_layer, (int(size * 0.03), int(size * 0.05)))
    ink = Image.new("RGBA", (w, h), INK + (255,))
    ink.putalpha(grown)
    out.alpha_composite(ink)
    fill = grad.convert("RGBA")
    fill.putalpha(mask)
    out.alpha_composite(fill)
    # A thin light edge along the top of the letters (engraved-metal glint).
    glint = mask.filter(ImageFilter.MinFilter(3))
    top_edge = Image.fromarray(np.clip(np.asarray(mask, dtype=np.int16) - np.roll(np.asarray(glint, dtype=np.int16), 2, axis=0), 0, 255).astype(np.uint8))
    hl = Image.new("RGBA", (w, h), (255, 250, 225, 0))
    hl.putalpha(top_edge.point(lambda v: int(v * 0.6)))
    out.alpha_composite(hl)
    return out.crop(out.getbbox())


def logo(width):
    """'ONLY BOSS FIGHTS' over 'THE FALLEN KNIGHT', centered, `width` px wide (transparent)."""
    title = gold_text("ONLY BOSS FIGHTS", TITLE_FONT, 200)
    sub = gold_text("THE FALLEN KNIGHT", SUB_FONT, 92, tracking=0.18)
    sub = sub.resize((int(title.width * 0.62), int(sub.height * title.width * 0.62 / sub.width)), Image.LANCZOS)
    gap = int(title.height * 0.12)
    h = title.height + gap + sub.height
    canvas = Image.new("RGBA", (title.width, h), (0, 0, 0, 0))
    canvas.alpha_composite(title, (0, 0))
    canvas.alpha_composite(sub, ((title.width - sub.width) // 2, title.height + gap))
    s = width / canvas.width
    return canvas.resize((width, int(canvas.height * s)), Image.LANCZOS)


def vignette_top(im, height_frac, strength):
    """Darkens the top of the image (behind the title) with a smooth gradient."""
    w, h = im.size
    grad = Image.new("L", (1, h))
    for y in range(h):
        t = max(0.0, 1.0 - y / (h * height_frac))
        grad.putpixel((0, y), int(255 * strength * t * t))
    shade = Image.new("RGBA", (w, h), (8, 4, 12, 255))
    shade.putalpha(grad.resize((w, h)))
    out = im.convert("RGBA")
    out.alpha_composite(shade)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    art = trim_black_bars(Image.open(os.path.join(ROOT, "ArtRaw/cover/cover.png")).convert("RGB"))
    W, H = art.size

    lg = logo(1600)
    lg.save(os.path.join(OUT, "logo.png"))

    # Cover 630 x 500 (1.26): narrower than the 16:9 art, so a crop would lose the hero or the last form. Instead the
    # whole scene sits at the bottom and the title gets its own dark band above it, painted from the art's own top
    # (blurred, darkened) so it reads as one picture.
    big = Image.new("RGBA", (1260, 1000), (8, 4, 12, 255))
    scene = art.resize((1260, int(H * 1260 / W)), Image.LANCZOS).convert("RGBA")
    top = scene.crop((0, 0, 1260, int(scene.height * 0.45))).resize((1260, 1000 - scene.height + 120), Image.LANCZOS)
    top = top.filter(ImageFilter.GaussianBlur(18))
    big.alpha_composite(top, (0, 0))
    big = vignette_top(big, 0.55, 0.9)
    fade = Image.new("L", (1, scene.height))
    for yy in range(scene.height):
        fade.putpixel((0, yy), int(255 * min(1.0, yy / 120.0)))
    scene.putalpha(fade.resize((1260, scene.height)))
    big.alpha_composite(scene, (0, 1000 - scene.height))
    lw = int(1260 * 0.84)
    l2 = logo(lw)
    big.alpha_composite(l2, ((1260 - lw) // 2, max(18, (1000 - scene.height - l2.height) // 2 - 10)))
    big.convert("RGB").save(os.path.join(OUT, "cover@2x.png"))
    big.convert("RGB").resize((630, 500), Image.LANCZOS).save(os.path.join(OUT, "cover.png"))

    # Banner (3.2 : 1): a strip through the eye and the villains' heads, the logo on the left over the dark tentacles.
    # Rendered at each upload width directly (not a 2x file): itch shows the banner at its own pixel width inside the
    # page column, so it must match the column (1280 with our embed, 960 by default).
    for bw, name in ((1280, "banner.png"), (960, "banner-960.png")):
        banner(art, bw).convert("RGB").save(os.path.join(OUT, name))
    stale = os.path.join(OUT, "banner@2x.png")
    if os.path.exists(stale):
        os.remove(stale)  # the 1920 x 600 file itch squeezed into the column, leaving a gap under it

    # Icon 512 x 512: the Unknown One's helm, crown and the god's eye on his chest, from his reference.
    ref = Image.open(os.path.join(ROOT, "ArtRaw/final/final-ref-b.cut.png")).convert("RGBA")
    icon_bg = Image.new("RGBA", (900, 900), (24, 14, 34, 255))
    # A soft violet glow behind him.
    glow = Image.new("L", (900, 900), 0)
    ImageDraw.Draw(glow).ellipse((120, 120, 780, 780), fill=255)
    glow = glow.filter(ImageFilter.GaussianBlur(120))
    tint = Image.new("RGBA", (900, 900), (120, 70, 160, 255))
    tint.putalpha(glow.point(lambda v: int(v * 0.7)))
    icon_bg.alpha_composite(tint)
    crop = ref.crop((380, 0, 1000, 620)).resize((900, 900), Image.LANCZOS)
    icon_bg.alpha_composite(crop)
    icon_bg.convert("RGB").resize((512, 512), Image.LANCZOS).save(os.path.join(OUT, "icon.png"))
    print("wrote", sorted(os.listdir(OUT)))


if __name__ == "__main__":
    main()
