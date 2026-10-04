#!/usr/bin/env python3
"""
Analyse a Qud .rpm map so a hand-placed object can be put in a cell that is known to be
walkable and empty, instead of guessing coordinates.

Reads Joppa.rpm, classifies every cell (floor tile / wall / water / furniture / occupant),
and prints a candidate list of free floor cells, plus a coarse ASCII map so the placement
can be judged by eye.
"""
import sys
import re
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

RPM = sys.argv[1] if len(sys.argv) > 1 else (
    r"D:\SteamLibrary\steamapps\common\Caves of Qud"
    r"\CoQ_Data\StreamingAssets\Base\Joppa.rpm")

root = ET.fromstring(open(RPM, "rb").read())
W = int(root.get("Width", 80))
H = int(root.get("Height", 25))
print("map %dx%d, %d cells" % (W, H, len(list(root))))

cells = {}
for c in root:
    x, y = int(c.get("X")), int(c.get("Y"))
    cells[(x, y)] = [o.get("Name") for o in c]

# A cell's FIRST object is the terrain (floor/wall); the rest is furniture/occupants.
terrain = {}
for k, objs in cells.items():
    terrain[k] = objs[0] if objs else None

tc = Counter(terrain.values())
print()
print("terrain kinds (top 25):")
for name, n in tc.most_common(25):
    print("    %-34s %d" % (name, n))

WALLISH = re.compile(r"Wall|Stone|Rock|Boulder|Tree|Fence|Door|Gate|Pillar|Column", re.I)
WATERISH = re.compile(r"Water|Pool|Puddle|Brine|Sludge|Goo|Liquid", re.I)


def kind(name):
    if name is None:
        return "?"
    if WATERISH.search(name):
        return "~"
    if WALLISH.search(name):
        return "#"
    return "."


print()
print("ASCII map  ( . floor   # wall/structure   ~ liquid   ? empty )")
print("     " + "".join(str(x // 10 % 10) for x in range(W)))
print("     " + "".join(str(x % 10) for x in range(W)))
for y in range(H):
    row = "".join(kind(terrain.get((x, y))) for x in range(W))
    print("  %2d %s" % (y, row))

# Occupied = anything beyond the terrain object sitting in the cell.
print()
print("cells with extra objects beyond terrain (first 30):")
n = 0
for y in range(H):
    for x in range(W):
        objs = cells.get((x, y), [])
        if len(objs) > 1:
            n += 1
            if n <= 30:
                print("    (%2d,%2d) %s" % (x, y, ", ".join(objs[1:])))
print("    ... total %d occupied cells" % n)

# Free candidates: floor terrain, nothing else, and all 8 neighbours also floor
print()
print("FREE FLOOR CELLS with all 8 neighbours also floor (candidates for placement):")
free = []
for y in range(1, H - 1):
    for x in range(1, W - 1):
        if len(cells.get((x, y), [])) != 1:
            continue
        if kind(terrain.get((x, y))) != ".":
            continue
        ok = True
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if len(cells.get((x + dx, y + dy), [])) != 1:
                    ok = False
                    break
                if kind(terrain.get((x + dx, y + dy))) != ".":
                    ok = False
                    break
            if not ok:
                break
        if ok:
            free.append((x, y))

print("    %d such cells" % len(free))
for x, y in free[:40]:
    print("    (%2d,%2d) terrain=%s" % (x, y, terrain.get((x, y))))
