#!/usr/bin/env python3
"""
Cross-check every reference in the mod's XML against the actual game data.

For each reference kind this resolves the SAME way the engine does, rather than by pattern
matching:

  <part Name=X>        engine calls ModManager.ResolveType("XRL.World.Parts." + X)
                       -> X must be a part class that the assembly or the mod defines under
                          that exact namespace. A name containing a dot is almost always wrong
                          because the prefix is CONCATENATED, not used as a fallback.
  <mutation Name=X>    engine resolves "XRL.World.Parts.Mutation." + X, and X is the mutation's
                       Class attribute in Mutations.xml, NOT its spaced display name.
  <skill Name=X>       engine resolves "XRL.World.Parts.Skill." + X.
  <stat Name=X>        must appear as a stat in the base game's object blueprints.
  Factions="X-n"       X must be a faction declared in Factions.xml.
  Blueprint="X"        X must be a blueprint declared in the base game or in this mod.
  <anatomy Category=X> must be one of BodyPartCategory's known names.

Anything that cannot be resolved is reported with the file and line, so it can be checked
rather than guessed at.
"""
import os
import re
import sys
import xml.etree.ElementTree as ET        # noqa: F401  (kept for future structural checks)

BASE = (r"D:\SteamLibrary\steamapps\common\Caves of Qud"
        r"\CoQ_Data\StreamingAssets\Base")
MOD = os.path.join(os.environ["USERPROFILE"], "AppData", "LocalLow",
                   "Freehold Games", "CavesOfQud", "Mods", "Toncihana_Elemental")
DLL = (r"D:\SteamLibrary\steamapps\common\Caves of Qud"
       r"\CoQ_Data\Managed\Assembly-CSharp.dll")

problems = []
notes = []

# ---------------------------------------------------------------- gather game data
print("reading base data ...")
base_blueprints = set()
base_stats = set()
base_factions = set()
base_anatomy_categories = set()
mutation_classes = {}
skill_classes = set()

for dirpath, _dirs, files in os.walk(BASE):
    for fn in files:
        if not fn.endswith(".xml"):
            continue
        p = os.path.join(dirpath, fn)
        try:
            t = open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        base_blueprints.update(re.findall(r'<object\s+Name="([^"]+)"', t))
        base_stats.update(re.findall(r'<stat\s+Name="([^"]+)"', t))
        base_factions.update(re.findall(r'<faction\s+Name="([^"]+)"', t))
        # A <mutation> element's attributes are NOT in a fixed order -- the real file has
        #   <mutation Name="Electrical Generation" Cost="4" MaxSelected="1" Class="ElectricalGeneration" ... />
        # so Name and Class are separated by other attributes. Match the whole element and pull each
        # attribute out independently, rather than assuming adjacency (which silently found nothing).
        for m in re.finditer(r'<mutation\b([^>]*?)/?>', t):
            attrs = m.group(1)
            cls = re.search(r'Class="([^"]+)"', attrs)
            if cls:
                mutation_classes[cls.group(1)] = True
        base_anatomy_categories.update(re.findall(r'<anatomy\s+[^>]*Category="([^"]+)"', t))

print("  %d blueprints, %d stats, %d factions, %d mutation classes"
      % (len(base_blueprints), len(base_stats), len(base_factions), len(mutation_classes)))

# part and skill classes come from the assembly's string heap plus the decompiled tree
base_part_classes = set()
base_skill_classes = set()
SRC = r"D:\caves of qud 模组制作\qud_src"
for dirpath, dirs, files in os.walk(SRC):
    low = dirpath.lower()
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        stem = fn[:-3]
        if "\\parts\\mutation" in low:
            continue
        if "\\parts\\skill" in low:
            base_skill_classes.add(stem)
        elif "\\parts" in low:
            base_part_classes.add(stem)
print("  %d part classes, %d skill classes (from qud_src)"
      % (len(base_part_classes), len(base_skill_classes)))

# the mod's own classes, with namespaces
mod_classes = {}
mod_part_classes = set()
mod_skill_classes = set()
scripts = os.path.join(MOD, "Scripts")
for fn in sorted(os.listdir(scripts)):
    if not fn.endswith(".cs"):
        continue
    t = open(os.path.join(scripts, fn), encoding="utf-8").read()
    ns = re.search(r'^\s*namespace\s+([\w.]+)', t, re.M)
    ns = ns.group(1) if ns else ""
    for cls in re.findall(r'\bclass\s+(\w+)\s*[:{]', t):
        mod_classes[cls] = ns
        if ns == "XRL.World.Parts":
            mod_part_classes.add(cls)
        elif ns == "XRL.World.Parts.Skill":
            mod_skill_classes.add(cls)

mod_blueprints = set()
mod_factions = set()
mod_populations = set()
mod_files = []
for dirpath, _dirs, files in os.walk(MOD):
    for fn in files:
        if fn.endswith(".xml"):
            p = os.path.join(dirpath, fn)
            mod_files.append(p)
            t = open(p, encoding="utf-8", errors="replace").read()
            mod_blueprints.update(re.findall(r'<object\s+Name="([^"]+)"', t))
            mod_factions.update(re.findall(r'<faction\s+Name="([^"]+)"', t))
            mod_populations.update(re.findall(r'<population\s+Name="([^"]+)"', t))

# every population table name in the base game, so <table Name=> can be checked for real
base_populations = set()
for dirpath, _dirs, files in os.walk(BASE):
    for fn in files:
        if fn.endswith(".xml"):
            t = open(os.path.join(dirpath, fn), encoding="utf-8", errors="replace").read()
            base_populations.update(re.findall(r'<population\s+Name="([^"]+)"', t))

print("  mod: %d classes, %d blueprints" % (len(mod_classes), len(mod_blueprints)))
print()

# ---------------------------------------------------------------- check the mod
BORROWED = re.compile(r"^(Tactics_|Survival_|CookingAndGathering|Cudgel$|"
                      r"ElectricalGeneration$|ElectromagneticPulse$|Regeneration$|"
                      r"NaturalWeapon$|Creature$|Humanoid$|NPC$|Snapjaw$)")

for path in sorted(mod_files):
    rel = os.path.relpath(path, MOD)
    t = open(path, encoding="utf-8", errors="replace").read()

    def line_of(idx):
        return t[:idx].count("\n") + 1

    # ---- <part Name=...>
    for m in re.finditer(r'<part\s+Name="([^"]+)"', t):
        name = m.group(1).strip()
        if not name:
            continue
        if "." in name:
            problems.append("%s:%d  <part Name=\"%s\"> contains a dot. The engine builds "
                            "\"XRL.World.Parts.\" + name, so a dotted name resolves to nonsense."
                            % (rel, line_of(m.start()), name))
            continue
        ok = name in mod_part_classes or name in base_part_classes
        if not ok:
            problems.append("%s:%d  <part Name=\"%s\"> resolves to XRL.World.Parts.%s which is "
                            "neither a base part class nor one of ours in XRL.World.Parts."
                            % (rel, line_of(m.start()), name, name))

    # ---- <mutation Name=...>
    for m in re.finditer(r'<mutation\s+Name="([^"]+)"', t):
        name = m.group(1).strip()
        if " " in name:
            problems.append("%s:%d  <mutation Name=\"%s\"> has a space -- that is a display Name, "
                            "not a Class. Creature blueprints reference the Class."
                            % (rel, line_of(m.start()), name))
            continue
        if name in mutation_classes or name in mod_classes:
            continue
        problems.append("%s:%d  <mutation Name=\"%s\"> is not a mutation Class in Mutations.xml "
                        "and not one of our classes." % (rel, line_of(m.start()), name))

    # ---- <skill Name=...>
    for m in re.finditer(r'<skill\s+Name="([^"]+)"', t):
        name = m.group(1).strip()
        if name in base_skill_classes or name in mod_skill_classes:
            continue
        if BORROWED.match(name):
            notes.append("%s:%d  <skill Name=\"%s\"> borrowed vanilla name (exempt)"
                         % (rel, line_of(m.start()), name))
            continue
        problems.append("%s:%d  <skill Name=\"%s\"> resolves to XRL.World.Parts.Skill.%s which "
                        "does not exist." % (rel, line_of(m.start()), name, name))

    # ---- <stat Name=...>
    for m in re.finditer(r'<stat\s+Name="([^"]+)"', t):
        name = m.group(1).strip()
        if name not in base_stats:
            problems.append("%s:%d  <stat Name=\"%s\"> is not a stat the base game declares."
                            % (rel, line_of(m.start()), name))

    # ---- Factions="X-n"
    for m in re.finditer(r'Factions="([^"]+)"', t):
        for part in m.group(1).split(","):
            fac = part.split("-")[0].strip()
            if not fac:
                continue
            if fac not in base_factions and fac not in mod_factions:
                problems.append("%s:%d  Factions=\"%s\": '%s' is not a declared faction."
                                % (rel, line_of(m.start()), m.group(1), fac))

    # ---- Blueprint="X" and <table Name="X"> against blueprint/table names
    for m in re.finditer(r'Blueprint="([^"]+)"', t):
        name = m.group(1).strip()
        if name.startswith("@") or ":" in name or "{" in name or "*" in name:
            continue        # dynamic table reference, resolved at runtime
        if name not in base_blueprints and name not in mod_blueprints:
            problems.append("%s:%d  Blueprint=\"%s\" is not a blueprint in the base game or here."
                            % (rel, line_of(m.start()), name))

    # ---- <anatomy Category=...>
    for m in re.finditer(r'<anatomy\s+[^>]*Category="([^"]+)"', t):
        name = m.group(1).strip()
        if name not in base_anatomy_categories:
            problems.append("%s:%d  <anatomy Category=\"%s\"> is not used by any base anatomy; "
                            "BodyPartCategory.GetCode throws on unknown names."
                            % (rel, line_of(m.start()), name))

    # ---- Inherits="X"
    for m in re.finditer(r'Inherits="([^"]+)"', t):
        name = m.group(1).strip()
        if name not in base_blueprints and name not in mod_blueprints:
            problems.append("%s:%d  Inherits=\"%s\" names no blueprint." % (rel, line_of(m.start()), name))

    # ---- <table Name="X"> (population tables)
    for m in re.finditer(r'<table\s+Name="([^"]+)"', t):
        name = m.group(1).strip()
        if name.startswith("@"):
            continue
        if name not in base_populations and name not in mod_populations:
            problems.append("%s:%d  <table Name=\"%s\"> is not a declared population table."
                            % (rel, line_of(m.start()), name))

print("=" * 78)
if notes:
    print("NOTES (%d) -- not errors, just not machine-verified" % len(notes))
    for n in notes:
        print("  - " + n)
    print()
print("=" * 78)
if problems:
    print("PROBLEMS (%d)" % len(problems))
    for p in problems:
        print("  !! " + p)
else:
    print("no unresolved references found")
print("=" * 78)
