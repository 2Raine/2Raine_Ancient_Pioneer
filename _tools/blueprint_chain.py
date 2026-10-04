#!/usr/bin/env python3
"""
Resolve a Qud blueprint's full inheritance chain and print what each link contributes.

Reads every ObjectBlueprints XML in the base game and in the mod, builds the blueprint table,
then walks `Inherits` from the target upwards, annotating each step with the parts, tags and
stats it adds. This is deliberately mechanical: the point is to show the chain the ENGINE will
walk, not the chain someone believes they wrote.

Usage:
    blueprint_chain.py <BlueprintName> [ModName]
"""
import os
import re
import sys
from collections import OrderedDict

BASE = (r"D:\SteamLibrary\steamapps\common\Caves of Qud"
        r"\CoQ_Data\StreamingAssets\Base\ObjectBlueprints")
MODS = os.path.join(os.environ["USERPROFILE"], "AppData", "LocalLow",
                    "Freehold Games", "CavesOfQud", "Mods")

TARGET = sys.argv[1] if len(sys.argv) > 1 else "2Raine_Elemental"
MODNAME = sys.argv[2] if len(sys.argv) > 2 else "Toncihana_Elemental"

# ---------------------------------------------------------------- load blueprints
blueprints = {}


def load_dir(root, origin):
    if not os.path.isdir(root):
        return
    for dirpath, _dirs, files in os.walk(root):
        for fn in sorted(files):
            if not fn.endswith(".xml"):
                continue
            p = os.path.join(dirpath, fn)
            try:
                t = open(p, encoding="utf-8", errors="replace").read()
            except Exception:
                continue
            if "<object" not in t:
                continue
            for m in re.finditer(r"<object\s+([^>]*?)>(.*?)</object>", t, re.S):
                attrs, body = m.group(1), m.group(2)
                nm = re.search(r'Name="([^"]+)"', attrs)
                if not nm:
                    continue
                inh = re.search(r'Inherits="([^"]+)"', attrs)
                rec = blueprints.setdefault(nm.group(1), {
                    "inherits": inh.group(1) if inh else None,
                    "body": "", "origin": origin, "file": fn})
                # Mod files win, and later entries append.
                if origin.startswith("MOD"):
                    rec["inherits"] = inh.group(1) if inh else rec["inherits"]
                    rec["body"] = body
                    rec["origin"] = origin
                    rec["file"] = fn
                elif rec["body"] == "":
                    rec["body"] = body
                    rec["file"] = fn


load_dir(BASE, "BASE")
load_dir(os.path.join(MODS, MODNAME), "MOD:" + MODNAME)

# ---------------------------------------------------------------- walk the chain
chain = []
cur = TARGET
seen = set()
while cur and cur not in seen:
    seen.add(cur)
    rec = blueprints.get(cur)
    if rec is None:
        chain.append((cur, None))
        break
    chain.append((cur, rec))
    cur = rec["inherits"]


def summarize(body):
    """Pull out the interesting bits of one blueprint body."""
    parts = re.findall(r'<part\s+Name="([^"]+)"([^>]*?)/?>', body)
    tags = re.findall(r'<tag\s+Name="([^"]+)"(?:\s+Value="([^"]*)")?', body)
    stats = re.findall(r'<stat\s+Name="([^"]+)"([^>]*?)/?>', body)
    muts = re.findall(r'<mutation\s+Name="([^"]+)"([^>]*?)/?>', body)
    skills = re.findall(r'<skill\s+Name="([^"]+)"', body)
    invs = re.findall(r'<inventoryobject\s+([^>]*?)/?>', body)
    props = re.findall(r'<(?:intproperty|property|stringproperty)\s+Name="([^"]+)"(?:\s+Value="([^"]*)")?', body)
    return parts, tags, stats, muts, skills, invs, props


print("=" * 78)
print("  INHERITANCE CHAIN: %s" % TARGET)
print("  (mod searched: %s)" % MODNAME)
print("=" * 78)

for depth, (name, rec) in enumerate(chain):
    pad = "  " * depth
    if rec is None:
        print("%s[%d] %s   <-- NOT FOUND in any loaded XML" % (pad, depth, name))
        continue
    print()
    print("%s[%d] %s" % (pad, depth, name))
    print("%s     defined in : %s  (%s)" % (pad, rec["origin"], rec["file"]))
    if rec["inherits"]:
        print("%s     inherits   : %s" % (pad, rec["inherits"]))

    parts, tags, stats, muts, skills, invs, props = summarize(rec["body"])
    if parts:
        print("%s     PARTS:" % pad)
        for pn, pa in parts:
            key = ""
            for k in ("Anatomy", "Factions", "Tile", "DisplayName", "ColorString",
                      "DetailColor", "Hostile", "Wanders"):
                mm = re.search(k + r'="([^"]*)"', pa)
                if mm:
                    key += " %s=%s" % (k, mm.group(1))
            print("%s       %-22s%s" % (pad, pn, key))
    if stats:
        print("%s     STATS: %s" % (pad, ", ".join(
            "%s%s" % (n, re.sub(r'\s+', ' ', a).strip()) for n, a in stats)))
    if muts:
        print("%s     MUTATIONS: %s" % (pad, ", ".join(
            "%s%s" % (n, re.sub(r'\s+', ' ', a).strip()) for n, a in muts)))
    if skills:
        print("%s     SKILLS: %s" % (pad, ", ".join(skills)))
    if invs:
        print("%s     INVENTORY: %s" % (pad, ", ".join(re.sub(r'\s+', ' ', i) for i in invs)))
    if props:
        print("%s     PROPERTIES: %s" % (pad, ", ".join(
            "%s=%s" % (n, v) for n, v in props)))
    if tags:
        print("%s     TAGS: %s" % (pad, ", ".join(
            "%s=%s" % (n, v) if v else n for n, v in tags)))

print()
print("=" * 78)
print("  EFFECTIVE VALUES (ancestor -> descendant; the LAST declaration wins)")
print("=" * 78)
# Walk from the ROOT down, so the last assignment is the one the child shows.
winners = {}
for name, rec in reversed(chain):
    if rec is None:
        continue
    for pn, pa in re.findall(r'<part\s+Name="([^"]+)"([^>]*?)/?>', rec["body"]):
        for attr in ("Anatomy", "Factions", "Hostile", "Wanders", "Tile",
                     "DisplayName", "ColorString", "DetailColor", "RenderString"):
            m = re.search(attr + r'="([^"]*)"', pa)
            if m:
                winners[(pn, attr)] = (m.group(1), name)

for key in sorted(winners):
    pn, attr = key
    val, src = winners[key]
    print("  %-16s %-14s = %-30s  (from %s)" % (pn, attr, val, src))

print()
print("  Note: a child part MERGES onto the inherited part of the same name;")
print("  it does not replace the whole part. Any attribute the child does not")
print("  restate keeps the ancestor's value, which is why e.g. Tile survives.")
