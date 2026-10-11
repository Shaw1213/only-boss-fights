"""
The "How DreamLayer was used" images for the itch.io page (the jam asks to show which assets came from DreamLayer,
ideally as before/after or reference -> result). Local compositing only, no credits.

  Builds/itch/dreamlayer/1-villain-chain.png    knight reference -> King -> Unknown One -> phase 2 (each an edit)
  Builds/itch/dreamlayer/2-one-room.png         throne room -> frozen -> taken -> torn (each an edit)
  Builds/itch/dreamlayer/3-greybox-to-art.png   the same moment of the fight, greybox and with the art
  Builds/itch/dreamlayer/4-ending-composite.png composite reference -> the ending illustration
  Builds/itch/dreamlayer/5-unknown-one-poses.png the Unknown One's 10 poses at their in-game scale

Usage: python -I Tools/make_itch_panels.py
"""
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Builds", "itch", "dreamlayer")
BG = (22, 18, 28)
PANEL = (34, 29, 42)
GOLD = (236, 196, 120)
TEXT = (232, 228, 236)
MUTED = (160, 152, 172)
LABEL = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", 30)
SMALL = ImageFont.truetype("C:/Windows/Fonts/georgia.ttf", 24)
ARROW = ImageFont.truetype("C:/Windows/Fonts/seguisym.ttf", 54)


def p(path):
    return os.path.join(ROOT, path)


def fit(im, w, h, bg=PANEL):
    """`im` scaled to fit w x h, centered on a panel (keeps transparency over the panel color)."""
    im = im.convert("RGBA")
    s = min(w / im.width, h / im.height)
    im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
    panel = Image.new("RGBA", (w, h), bg + (255,))
    panel.alpha_composite(im, ((w - im.width) // 2, (h - im.height) // 2))
    return panel


def strip(items, cell_w, cell_h, title, note=None, arrows=True):
    """A row of (image, label, sublabel) cells with arrows between them, a title above and a note below."""
    pad, gap = 40, 70 if arrows else 24
    top = 90
    bottom = 70 if note else 30
    w = pad * 2 + len(items) * cell_w + (len(items) - 1) * gap
    h = top + cell_h + 80 + bottom
    out = Image.new("RGBA", (w, h), BG + (255,))
    d = ImageDraw.Draw(out)
    d.text((pad, 28), title, font=LABEL, fill=GOLD)
    x = pad
    for i, (im, label, sub) in enumerate(items):
        out.alpha_composite(fit(im, cell_w, cell_h), (x, top))
        d.text((x + 4, top + cell_h + 14), label, font=LABEL, fill=TEXT)
        if sub:
            d.text((x + 4, top + cell_h + 50), sub, font=SMALL, fill=MUTED)
        if arrows and i < len(items) - 1:
            d.text((x + cell_w + gap // 2 - 18, top + cell_h // 2 - 36), "\u2192", font=ARROW, fill=GOLD)
        x += cell_w + gap
    if note:
        d.text((pad, h - 52), note, font=SMALL, fill=MUTED)
    return out.convert("RGB")


def main():
    os.makedirs(OUT, exist_ok=True)

    chain = [
        (Image.open(p("ArtRaw/cover/knight-ref.cut.png")), "The Knight", "the reference"),
        (Image.open(p("ArtRaw/king/king-ref-t4.cut.png")), "The Frozen King", "edit of the Knight"),
        (Image.open(p("ArtRaw/final/final-ref-b.cut.png")), "The Unknown One", "edit of the King"),
        (Image.open(p("ArtRaw/final/final-phase2.cut.png")), "Phase 2", "edit: the god wakes"),
    ]
    strip(chain, 420, 520, "One villain, three forms: one image, edited step by step",
          "Helm, plume, crown and sword survive every edit; the sun on his chest becomes the god's eye.").save(
        os.path.join(OUT, "1-villain-chain.png"))

    room = [
        (Image.open(p("ArtRaw/world/arena2.png")), "The throne room", "generated (fight 2)"),
        (Image.open(p("ArtRaw/world/arena2-frozen.png")), "Frozen over", "edit: the King's phase 2"),
        (Image.open(p("ArtRaw/final/arena3.png")), "Taken by the god", "edit: the final fight"),
        (Image.open(p("ArtRaw/final/arena3-torn.png")), "Torn open", "edit: the final phase 2"),
    ]
    strip(room, 520, 293, "One room, three edits: the same floor line, so phases crossfade in the game").save(
        os.path.join(OUT, "2-one-room.png"))

    grey = [
        (Image.open(p("docs/media/knight-greybox-now.png")), "Greybox", "the fight was built and tuned first"),
        (Image.open(p("docs/media/knight-art-now.png")), "With DreamLayer art", "same frame data, the art swapped in"),
    ]
    strip(grey, 760, 428, "The same moment of the fight, before and after the art").save(
        os.path.join(OUT, "3-greybox-to-art.png"))

    ending = [
        (Image.open(p("ArtRaw/ending/ending-ref.png")), "Composite reference", "the Knight's defeated pose pasted into the final arena"),
        (Image.open(p("ArtRaw/ending/ending.png")), "The ending", "one edit: the god gone, dawn, the knight at rest"),
    ]
    strip(ending, 760, 428, "Two references in one image (a request takes one reference)").save(
        os.path.join(OUT, "4-ending-composite.png"))

    names = ["idle", "cleave_windup", "cleave_strike", "lash", "floor_cast", "leap", "smash", "hurt", "defeated", "phase2"]
    sprites = [Image.open(p(f"Assets/Art/DreamLayer/Characters/Final/final_{n}.png")).convert("RGBA") for n in names]
    # Same pixel scale as the game (every pose normalized to the idle's), two rows of five on floor lines.
    s = 0.5
    rows = [list(zip(sprites[:5], names[:5])), list(zip(sprites[5:], names[5:]))]
    pad, gap = 40, 14
    row_w = [sum(int(im.width * s) for im, _ in r) + gap * (len(r) - 1) for r in rows]
    row_h = [max(int(im.height * s) for im, _ in r) for r in rows]
    w = pad * 2 + max(row_w)
    h = 90 + sum(rh + 60 for rh in row_h) + 20
    out = Image.new("RGBA", (w, h), BG + (255,))
    d = ImageDraw.Draw(out)
    d.text((pad, 28), "The Unknown One: 10 poses, all edits of one reference, at their in-game scale", font=LABEL, fill=GOLD)
    y = 90
    for r, rw, rh in zip(rows, row_w, row_h):
        x = pad + (max(row_w) - rw) // 2
        floor = y + rh
        for im, n in r:
            im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
            out.alpha_composite(im, (x, floor - im.height))
            d.text((x + 4, floor + 12), n.replace("_", " "), font=SMALL, fill=MUTED)
            x += im.width + gap
        d.line([(pad, floor), (w - pad, floor)], fill=(80, 70, 92), width=2)
        y = floor + 60
    out.convert("RGB").save(os.path.join(OUT, "5-unknown-one-poses.png"))
    print("wrote", sorted(os.listdir(OUT)))


if __name__ == "__main__":
    main()
