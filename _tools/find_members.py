#!/usr/bin/env python3
"""Find types owning members whose names match any of the given substrings."""
import re
import sys

sys.path.insert(0, r"D:\caves of qud 模组制作\_tools")

SRC = sys.argv[1]
OUT = sys.argv[2]
PATTERNS = [re.compile(p, re.I) for p in sys.argv[3:]]

lines = open(SRC, "r", encoding="utf-8", errors="replace").read().splitlines()

# map line index -> section header
hdr = None
sections = []
cur = None
for i, l in enumerate(lines):
    if l.startswith("=== "):
        if cur:
            sections.append(cur)
        cur = [l, i, i]
    elif cur:
        cur[2] = i
if cur:
    sections.append(cur)

out = []
for h, s, e in sections:
    type_name = h[4:].split(" : ")[0]
    hits = []
    for i in range(s, e + 1):
        if PATTERNS and any(p.search(lines[i]) for p in PATTERNS):
            hits.append(lines[i])
    if hits:
        out.append(h)
        out.extend(hits)
        out.append("")

with open(OUT, "w", encoding="utf-8") as fh:
    fh.write("\n".join(out) + "\n")
print("%d/%d sections matched -> %s" % (sum(1 for l in out if l.startswith("=== ")), len(sections), OUT))
