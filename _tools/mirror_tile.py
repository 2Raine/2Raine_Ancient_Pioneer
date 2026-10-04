#!/usr/bin/env python3
"""
Mirror a Qud tile horizontally, preserving the 3-colour scheme.

Qud tiles are authored black/white on transparency, so a horizontal flip is a pure pixel
operation: colour roles are untouched and the sprite still renders through the same
ColorString/DetailColor pair.

Usage: mirror_tile.py <input.png> <output.png>
"""
import collections
import sys

from PIL import Image, ImageOps


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    src, dst = sys.argv[1], sys.argv[2]

    im = Image.open(src).convert("RGBA")
    out = ImageOps.mirror(im)          # left-right flip
    out.save(dst)

    print("mirrored %s -> %s  %s" % (src, dst, out.size))
    print("  colours before: %s" % collections.Counter(im.getdata()).most_common())
    print("  colours after : %s" % collections.Counter(out.getdata()).most_common())
    print("  identical to source? %s" % (list(im.getdata()) == list(out.getdata())))


if __name__ == "__main__":
    main()
