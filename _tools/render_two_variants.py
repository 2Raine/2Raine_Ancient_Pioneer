#!/usr/bin/env python3
"""
Render a 3-colour Qud tile in two mutually inverted colour assignments.

Qud's tile convention: the sprite is authored in pure black + pure white on transparency, and the
game maps
    BLACK -> the object's ColorString (foreground)
    WHITE -> the object's DetailColor
So "A vs B" means which of #0096ff / #ffffff plays which role. Both are produced here.

Outputs, into the working directory:
    test1_A_黑变蓝白变白.png    black -> #0096ff, white -> #ffffff
    test1_B_黑变白蓝变白.png    black -> #ffffff, white -> #0096ff
    test1_双版本预览.png        both at 10x on a dark background, for eyeballing
"""
import os
from PIL import Image, ImageDraw

SRC = (r"C:\Users\16064\.dsh\attachments\v1\objects\a4"
       r"\a4a220b4592e514be3037443bd9e8155bc71ce184a567d914c1da9e763b7a591")
OUTDIR = r"D:\caves of qud 模组制作"

BLUE = (0x00, 0x96, 0xFF)
WHITE = (0xFF, 0xFF, 0xFF)


def render(src, foreground, detail):
    """BLACK -> foreground, WHITE -> detail. Transparency preserved."""
    im = Image.open(src).convert("RGBA")
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    s, d = im.load(), out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = s[x, y]
            if a == 0:
                continue
            d[x, y] = ((foreground if (r < 128 and g < 128 and b < 128) else detail)) + (255,)
    return out


a = render(SRC, BLUE, WHITE)     # black -> blue,  white -> white
b = render(SRC, WHITE, BLUE)     # black -> white, white -> blue

pa = os.path.join(OUTDIR, "test1_A_黑变蓝白变白.png")
pb = os.path.join(OUTDIR, "test1_B_黑变白蓝变白.png")
a.save(pa)
b.save(pb)
print("wrote", os.path.basename(pa), a.size)
print("wrote", os.path.basename(pb), b.size)

# --- side-by-side proof sheet on a dark game-like background, so both read correctly
SCALE = 10
CW, CH = 16 * SCALE, 24 * SCALE
PAD, LAB = 16, 28
items = [
    ("original\n(black/white)", Image.open(SRC).convert("RGBA")),
    ("A: black->#0096ff\nwhite->#ffffff", a),
    ("B: black->#ffffff\nwhite->#0096ff", b),
]
W = PAD + len(items) * (CW + PAD)
H = PAD + CH + LAB + PAD
sheet = Image.new("RGB", (W, H), (0x14, 0x12, 0x20))
dr = ImageDraw.Draw(sheet)
for i, (label, im) in enumerate(items):
    x = PAD + i * (CW + PAD)
    # flatten onto the dark background so the image is unambiguous in any viewer
    bg = Image.new("RGBA", im.size, (0x14, 0x12, 0x20, 255))
    bg.alpha_composite(im)
    big = bg.resize((CW, CH), Image.NEAREST)
    sheet.paste(big, (x, PAD))
    for j, line in enumerate(label.split("\n")):
        dr.text((x, PAD + CH + 4 + j * 11), line, fill=(0xC0, 0xC0, 0xD0))

ps = os.path.join(OUTDIR, "test1_双版本预览.png")
sheet.save(ps)
print("wrote", os.path.basename(ps), sheet.size)
