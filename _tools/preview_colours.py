#!/usr/bin/env python3
"""
Show what the mod's character tile renders as, given the blueprint's colour codes.

Qud maps the authored black/white sprite through the blueprint:
    BLACK -> ColorString   (the blueprint now says &B = "blue")
    WHITE -> DetailColor   (Y = "white")
This applies that mapping on a dark game-like background purely so the result can be checked by
eye. The PNG shipped in the mod stays black/white.
"""
import os
from PIL import Image, ImageDraw

MOD = os.path.join(
    os.environ["USERPROFILE"],
    "AppData", "LocalLow", "Freehold Games", "CavesOfQud", "Mods", "Toncihana_Elemental")
TEX = os.path.join(MOD, "Textures", "Toncihana")

# "blue" (#0096ff) and "white" (#ffffff), i.e. the codes B and Y from vanilla Colors.xml
BLUE = (0x00, 0x96, 0xFF)
WHITE = (0xFF, 0xFF, 0xFF)
BG = (0x14, 0x12, 0x20)


def render(path, fg, det):
    im = Image.open(path).convert("RGBA")
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    s, d = im.load(), out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = s[x, y]
            if a:
                d[x, y] = ((fg if (r < 128 and g < 128 and b < 128) else det)) + (255,)
    return out


body = render(os.path.join(TEX, "toncihana_body.png"), BLUE, WHITE)

SCALE = 10
CW, CH = 16 * SCALE, 24 * SCALE
PAD, LAB = 16, 30
items = [
    ("in-game\n&B blue on Y white", body),
    ("authored file\n(black/white)", Image.open(os.path.join(TEX, "toncihana_body.png")).convert("RGBA")),
]

W = PAD + len(items) * (CW + PAD)
H = PAD + CH + LAB + PAD
sheet = Image.new("RGB", (W, H), BG)
dr = ImageDraw.Draw(sheet)
for i, (label, im) in enumerate(items):
    x = PAD + i * (CW + PAD)
    flat = Image.new("RGBA", im.size, BG + (255,))
    flat.alpha_composite(im)
    sheet.paste(flat.resize((CW, CH), Image.NEAREST), (x, PAD))
    for j, line in enumerate(label.split("\n")):
        dr.text((x, PAD + CH + 4 + j * 11), line, fill=(0xC0, 0xC0, 0xD0))

out = r"D:\caves of qud 模组制作\Toncihana_渲染效果.png"
sheet.save(out)
print("wrote", out, sheet.size)
