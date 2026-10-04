#!/usr/bin/env python3
"""
Audit every mod-defined identifier against the community "Author_Content" convention,
and flag which ones are ALSO written into save files (renaming those is dangerous).
"""
import os
import re

MOD = os.path.join(os.environ["USERPROFILE"], "AppData", "LocalLow",
                   "Freehold Games", "CavesOfQud", "Mods", "Toncihana_Elemental")

entries = []   # (kind, identifier, file, line)

# ---- XML identifiers
for root, dirs, files in os.walk(MOD):
    for fn in sorted(files):
        if not fn.endswith(".xml"):
            continue
        p = os.path.join(root, fn)
        rel = os.path.relpath(p, MOD)
        text = open(p, encoding="utf-8").read()
        for m in re.finditer(r'<genotype\s+Name="([^"]+)"', text):
            entries.append(("genotype", m.group(1), rel))
        for m in re.finditer(r'<subtype\s+Name="([^"]+)"', text):
            entries.append(("subtype", m.group(1), rel))
        for m in re.finditer(r'<class\s+ID="([^"]+)"', text):
            entries.append(("subtype-class", m.group(1), rel))
        for m in re.finditer(r'<skill\s+Name="([^"]+)"\s*\n?\s*Class="([^"]+)"', text):
            entries.append(("skill", m.group(1), rel))
        for m in re.finditer(r'<power\s+Name="([^"]+)"', text):
            entries.append(("power", m.group(1), rel))
        for m in re.finditer(r'<mutation\s+Name="([^"]+)"', text):
            entries.append(("mutation-Name", m.group(1), rel))
        for m in re.finditer(r'\bClass="(Toncihana\w+)"', text):
            entries.append(("mutation/skill-Class", m.group(1), rel))
        for m in re.finditer(r'<object\s+Name="([^"]+)"', text):
            entries.append(("object", m.group(1), rel))

# ---- C# class names
for fn in sorted(os.listdir(os.path.join(MOD, "Scripts"))):
    if not fn.endswith(".cs"):
        continue
    text = open(os.path.join(MOD, "Scripts", fn), encoding="utf-8").read()
    for m in re.finditer(r'\bclass\s+(Toncihana\w+)\s*:', text):
        entries.append(("C# class", m.group(1), "Scripts/" + fn))

CONVENTION = re.compile(r"^(2Raine_|2Raine\b)")

print("%-22s %-44s %-8s %s" % ("kind", "identifier", "conforms", "where"))
print("-" * 110)
bad = []
seen = set()
for kind, ident, where in entries:
    key = (kind, ident)
    if key in seen:
        continue
    seen.add(key)
    ok = bool(CONVENTION.match(ident))
    if not ok:
        bad.append((kind, ident, where))
    print("%-22s %-44s %-8s %s" % (kind, ident[:44], "yes" if ok else "NO", where))

print()
print("=== does NOT follow the convention: %d ===" % len(bad))
for kind, ident, where in bad:
    print("  %-22s %-44s %s" % (kind, ident, where))
