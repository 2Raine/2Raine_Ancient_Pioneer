#!/usr/bin/env python3
"""Extract sections from the dumped metadata report by type-name substring."""
import sys

SRC = sys.argv[1]
OUT = sys.argv[2]
KEYS = sys.argv[3:]

lines = open(SRC, "r", encoding="utf-8", errors="replace").read().splitlines()

# Build section index: header line -> (start, end)
starts = [i for i, l in enumerate(lines) if l.startswith("=== ")]
secs = []
for n, s in enumerate(starts):
    e = starts[n + 1] if n + 1 < len(starts) else len(lines)
    secs.append((lines[s], s, e))

picked = []
for hdr, s, e in secs:
    body = hdr[4:]
    name = body.split(" : ")[0]
    # strip "class public" etc
    for k in KEYS:
        if k in name:
            picked.append((hdr, s, e))
            break

with open(OUT, "w", encoding="utf-8") as fh:
    for hdr, s, e in picked:
        fh.write("\n".join(lines[s:e]) + "\n")
print("matched %d sections -> %s" % (len(picked), OUT))
