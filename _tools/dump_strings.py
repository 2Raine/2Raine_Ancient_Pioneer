#!/usr/bin/env python3
"""Dump the #Strings heap of an assembly and filter by regex."""
import re
import struct
import sys

sys.path.insert(0, r"D:\caves of qud 模组制作\_tools")
import dump_metadata as dm

path = sys.argv[1]
pattern = re.compile(sys.argv[2], re.I)

data = open(path, "rb").read()
md_off, _, _ = dm.find_metadata_root(data)
md = dm.Metadata(data, md_off)

off, size = md.streams["#Strings"]
end = off + size
names = []
p = off
while p < end:
    q = data.find(b"\0", p, end)
    if q < 0:
        break
    if q > p:
        names.append(data[p:q].decode("utf-8", "replace"))
    p = q + 1

hits = sorted({n for n in names if pattern.search(n)})
print("# %d / %d strings match" % (len(hits), len(names)))
for n in hits:
    print(n)
