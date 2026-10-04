#!/usr/bin/env python3
"""
Recolour a 3-colour Qud tile: black -> #ffffff, white -> #d74200. Transparency is preserved.

Usage: recolour_tile.py <input.png> <output.png>
"""
import collections
import os
import sys

from PIL import Image

BLACK_TO = (0xFF, 0xFF, 0xFF)
WHITE_TO = (0xD7, 0x42, 0x00)


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    src, dst = sys.argv[1], sys.argv[2]

    im = Image.open(src).convert("RGBA")
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    s, d = im.load(), out.load()

    n_black = n_white = n_clear = 0
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = s[x, y]
            if a == 0:
                n_clear += 1
                continue
            # anything on the dark side counts as the foreground colour
            if r < 128 and g < 128 and b < 128:
                d[x, y] = BLACK_TO + (255,)
                n_black += 1
            else:
                d[x, y] = WHITE_TO + (255,)
                n_white += 1

    out.save(dst)
    print("wrote %s  %s" % (dst, out.size))
    print("  black -> #%02X%02X%02X : %d px" % (BLACK_TO + (n_black,)))
    print("  white -> #%02X%02X%02X : %d px" % (WHITE_TO + (n_white,)))
    print("  transparent kept      : %d px" % n_clear)
    print("  result colours        : %s"
          % collections.Counter(out.getdata()).most_common())


if __name__ == "__main__":
    main()
