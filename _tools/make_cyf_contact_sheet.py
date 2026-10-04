#!/usr/bin/env python3
"""
Contact sheet for "CYF Expansion - Legendary Pariahs" (workshop id 3596634370).

That mod ships 128 player-character tiles, all 16x24 and all in Qud's 3-colour tile scheme
(transparent + pure black + pure white), which the game recolours at render time:
    black -> Foreground from the XML
    white -> DetailColor from the XML
It declares them in ChooseYourFighter.xml as, e.g.
    <model ID="Meph_001Antelope1Tile" Name="{{M| - }}{{K|00}}{{M|1 - {{K|the}} Puma Eater}}">
      <tile Path="Characters\\Meph_001Antelope1.png" Foreground="w" DetailColor="W" />

This renders every tile the way the game would, labelled with its index and race, so a specific
character can be picked by eye.
"""
import os
import re
from PIL import Image, ImageDraw

MOD = r"D:\SteamLibrary\steamapps\workshop\content\333640\3596634370"
TEXDIR = os.path.join(MOD, "Textures", "Characters")
OUT = r"D:\caves of qud 模组制作\CYF_LegendaryPariahs_总览.png"

# Qud palette for the Foreground/DetailColor codes seen in this mod's XML
PALETTE = {
    "w": (0xC8, 0xC8, 0xC8), "W": (0xFF, 0xFF, 0xFF),
    "k": (0x5A, 0x5A, 0x5A), "K": (0x00, 0x00, 0x00),
    "m": (0x9C, 0x6A, 0xFF), "M": (0xC8, 0xA0, 0xFF),
    "c": (0x00, 0xC8, 0xC8), "C": (0x00, 0xFF, 0xFF),
    "r": (0xC8, 0x00, 0x00), "R": (0xFF, 0x00, 0x00),
    "g": (0x00, 0xC8, 0x00), "G": (0x00, 0xFF, 0x00),
    "b": (0x00, 0x00, 0xC8), "B": (0x00, 0x00, 0xFF),
    "y": (0xC8, 0xC8, 0x00), "Y": (0xFF, 0xFF, 0x00),
    "o": (0xC8, 0x7D, 0x00), "O": (0xFF, 0xA0, 0x00),
}


def load_colours():
    """Read each tile's Foreground/DetailColor straight out of the mod's own XML."""
    xmlpath = os.path.join(MOD, "ChooseYourFighter.xml")
    text = open(xmlpath, encoding="utf-8").read()
    info = {}
    for m in re.finditer(
            r'<tile\s+Path="([^"]+)"\s+Foreground="([^"]*)"\s+DetailColor="([^"]*)"', text):
        path, fg, det = m.group(1), m.group(2), m.group(3)
        info[os.path.basename(path.replace("\\", "/"))] = (fg, det)
    return info


def recolour(path, fg, det):
    im = Image.open(path).convert("RGBA")
    f = PALETTE.get(fg[:1], (0xC8, 0xC8, 0xC8))
    d = PALETTE.get(det[:1], (0xFF, 0xFF, 0xFF))
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    src, dst = im.load(), out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = src[x, y]
            if a == 0:
                continue
            dst[x, y] = ((f if (r < 128 and g < 128 and b < 128) else d)) + (255,)
    return out


info = load_colours()
files = sorted(f for f in os.listdir(TEXDIR) if f.lower().endswith(".png"))

COLS, SCALE = 16, 3
CELL_W, CELL_H = 16 * SCALE, 24 * SCALE
LABEL_H = 13
PAD = 6
BG = (0x10, 0x0E, 0x1C)

rows = (len(files) + COLS - 1) // COLS
W = PAD + COLS * (CELL_W + PAD)
H = PAD + rows * (CELL_H + LABEL_H + PAD)

sheet = Image.new("RGB", (W, H), BG)
draw = ImageDraw.Draw(sheet)

for i, fn in enumerate(files):
    cx = PAD + (i % COLS) * (CELL_W + PAD)
    cy = PAD + (i // COLS) * (CELL_H + LABEL_H + PAD)

    fg, det = info.get(fn, ("w", "W"))
    tile = recolour(os.path.join(TEXDIR, fn), fg, det)
    sheet.paste(tile.resize((CELL_W, CELL_H), Image.NEAREST), (cx, cy), tile.resize((CELL_W, CELL_H), Image.NEAREST))

    # label: zero-padded index plus the race part of the filename
    m = re.match(r"Meph_(\d+)([A-Za-z]+?)(\d)\.png$", fn)
    idx = m.group(1) if m else str(i + 1)
    race = m.group(2) if m else fn
    draw.text((cx, cy + CELL_H + 1), idx + " " + race[:11], fill=(0xB0, 0xB0, 0xC0))

sheet.save(OUT)
print("wrote", OUT, sheet.size, "tiles:", len(files))
