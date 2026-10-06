#!/usr/bin/env python3
"""Validate the Toncihana mod: XML well-formedness, JSON validity, referenced files exist."""
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

MOD = os.path.join(
    os.environ["USERPROFILE"],
    "AppData", "LocalLow", "Freehold Games", "CavesOfQud", "Mods", "Toncihana_Elemental")

problems = []
ok = []

# ---------------- XML ----------------
xml_files = []
for root, _dirs, files in os.walk(MOD):
    for f in files:
        if f.lower().endswith(".xml"):
            xml_files.append(os.path.join(root, f))

for path in sorted(xml_files):
    rel = os.path.relpath(path, MOD)
    try:
        tree = ET.parse(path)
        r = tree.getroot()
        ok.append("XML  %-46s root=<%s> children=%d" % (rel, r.tag, len(list(r))))
    except Exception as exc:
        problems.append("XML  %-46s FAILED: %s" % (rel, exc))

# ---------------- no comment blocks in data files ----------------
# House rule for this mod: the XML files carry NO comments. All rationale lives in
# Toncihana_制作笔记与调参参考.md instead, because comments made the data files unpleasant to read.
# A comment block also cannot contain "--", which has silently broken parsing five times here.
for path in sorted(xml_files):
    rel = os.path.relpath(path, MOD)
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()
    n = text.count("<!--")
    if n:
        problems.append("STYLE %-45s has %d comment block(s); this mod keeps XML comment-free"
                        % (rel, n))
    else:
        ok.append("STYLE %-45s no comments" % rel)

# ---------------- naming convention (author = 2Raine) ----------------
# Content tied to the Toncihana character is prefixed 2Raine_Toncihana_, everything else 2Raine_.
# C# identifiers cannot start with a digit, so class names carry an extra leading "A".
#
# Names that are deliberately NOT ours are exempt: any <skill Name="..."> or <mutation Name="...">
# we reference is a vanilla entry we are borrowing (Tactics_Run, Survival_Camp, Cudgel,
# ElectricalGeneration, ElectromagneticPulse, Regeneration), so it must keep its original spelling.
# Renaming those would be the error, not the fix.
#
# NOTE the vanilla naming trap these last two sit on. A mutation's XML entry carries BOTH a spaced
# display Name and a Class holding the C# type:
#     <mutation Name="Electrical Generation"  Class="ElectricalGeneration"  ... />
#     <mutation Name="Electromagnetic Pulse"  Class="ElectromagneticPulse"  ... />
# Creature blueprints reference the CLASS, unspaced -- every vanilla <mutation Name=...> on a
# creature is unspaced. So the borrowing has to use "ElectricalGeneration", never
# "Electrical Generation", and the validator below would otherwise flag it as a naming breach.
# Creature is vanilla's base creature blueprint: this mod merges a part onto it with
# Load="Merge" so that every creature gets it, which is the supported way to reach them all.
BORROWED_OK = re.compile(r"^(Tactics_|Survival_|CookingAndGathering|Cudgel$|"
                         r"ElectricalGeneration$|ElectromagneticPulse$|Regeneration$|"
                         r"Creature$)")
PREFIX = re.compile(r"^(A?2Raine_)")
TILE_OK = re.compile(r"^[A-Za-z_]+/")     # vanilla tile paths like Creatures/caste_16.bmp

name_reports = []
for path in sorted(xml_files):
    rel = os.path.relpath(path, MOD)
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()
    for pat, kind in (
            (r'<genotype\s+Name="([^"]+)"', "genotype"),
            (r'<subtype\s+Name="([^"]+)"', "subtype"),
            (r'<class\s+ID="([^"]+)"', "subtype-class"),
            (r'<skill\s+Name="([^"]+)"', "borrowed-skill"),
            (r'<power\s+Name="([^"]+)"', "power"),
            (r'<mutation\s+Name="([^"]+)"', "mutation"),
            (r'<object\s+Name="([^"]+)"', "object"),
    ):
        for m in re.finditer(pat, text):
            name_reports.append((kind, m.group(1), rel))

bad_names = [(k, n, w) for k, n, w in name_reports
             if not PREFIX.match(n)
             and not (k == "borrowed-skill" and BORROWED_OK.match(n))
             and not BORROWED_OK.match(n)]

if bad_names:
    for kind, name, where in bad_names:
        problems.append("NAME  %-20s %-42s does not start with 2Raine_ (%s)"
                        % (kind, name, where))
else:
    ok.append("NAME  all %d declared identifiers carry the 2Raine_ prefix "
              "(borrowed vanilla names exempt)" % len(name_reports))

# ---------------- JSON ----------------
for name in ("manifest.json", "workshop.json", "modconfig.json"):
    path = os.path.join(MOD, name)
    if not os.path.exists(path):
        if name == "manifest.json":
            problems.append("JSON %-46s MISSING (required)" % name)
        else:
            ok.append("JSON %-46s absent (optional, fine)" % name)
        continue
    try:
        with open(path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
        ok.append("JSON %-46s valid, %d keys" % (name, len(data)))
    except Exception as exc:
        problems.append("JSON %-46s INVALID: %s" % (name, exc))

# ---------------- every Class= must resolve to a real C# class ----------------
# This is the check that was MISSING when a rename broke new-game creation. Renaming the C#
# classes to add the leading "A" left the skills XML still pointing at the old names, and the
# failure was an ArgumentNullException deep inside Activator.CreateInstance during world
# generation -- the game hung at "creating world" with nothing in the XML to point at.
#
# Any Class= we declare ourselves must name a class that actually exists in Scripts/.
script_classes = set()
# class name -> namespace, so a <part Name=> can be checked for the namespace the engine will
# actually look in. See the part-ref check further down for why this matters.
script_class_namespaces = {}
script_dir = os.path.join(MOD, "Scripts")
if os.path.isdir(script_dir):
    for fn in os.listdir(script_dir):
        if not fn.endswith(".cs"):
            continue
        with open(os.path.join(script_dir, fn), "r", encoding="utf-8") as fh:
            body = fh.read()
        ns_match = re.search(r'^\s*namespace\s+([\w.]+)', body, re.M)
        ns = ns_match.group(1) if ns_match else ""
        for cls in re.findall(r'\bclass\s+(\w+)\s*[:{]', body):
            script_classes.add(cls)
            script_class_namespaces[cls] = ns
        # nested classes are indented; the regex above already catches them

class_refs = []          # (declared name, file)
for path in sorted(xml_files):
    rel = os.path.relpath(path, MOD)
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()
    for m in re.finditer(r'\bClass="([^"]*)"', text):
        val = m.group(1).strip()
        if not val:
            continue
        # a Class= may be namespaced (Toncihana.Foo) and may list several, comma separated
        for one in val.split(","):
            one = one.strip()
            if one:
                class_refs.append((one, rel))

if not script_classes:
    problems.append("REF  no C# class definitions found in Scripts/ at all")
else:
    unresolved = []
    for one, rel in class_refs:
        short = one.split(".")[-1]
        # only our own declarations are checked; XRL.* / vanilla types are left alone
        if short in script_classes:
            continue
        if one.startswith("XRL.") or one.startswith("ConsoleLib."):
            continue
        unresolved.append((one, rel))
    if unresolved:
        for one, rel in unresolved:
            problems.append("REF  Class=\"%s\" (%s) names no C# class in Scripts/ -- "
                            "this crashes new-game creation" % (one, rel))
    else:
        ok.append("REF  all %d declared Class= values resolve to a C# class" % len(class_refs))

# ---------------- every <part Name=... /> must resolve too ----------------
# Parts are matched by C# CLASS NAME, so a stale part name fails silently: the part is simply never
# attached and the mechanic it carries does nothing. That happened to the physiology part during the
# rename -- the race kept loading, but its damage/deflection/healing rules were gone.
#
# NAMESPACE IS PART OF THE NAME, and this check used to miss that. The engine resolves a part with
#     ModManager.ResolveType("XRL.World.Parts." + Part)
# (see ActionForwarder, GameObject.AddPart(string), Zone.GetPart). The prefix is CONCATENATED, not
# used as a fallback -- so a part class of ours that lives in namespace `Toncihana` is NOT found by
# the bare name. It has to be written fully qualified:
#     <part Name="Toncihana.A2Raine_BornEquipped" ... />
# Writing the bare name produced, at load time,
#     MODERROR ... Could not find XRL.World.Parts.A2Raine_BornEquipped, element ignored.
# which silently cost us the head natural weapon, the spirit stone, AND cascaded into the priest
# never being created. The old check passed it because the bare name did match a class in Scripts/.
part_refs = []
for path in sorted(xml_files):
    rel = os.path.relpath(path, MOD)
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()
    for m in re.finditer(r'<part\s+Name="([^"]+)"', text):
        val = m.group(1).strip()
        # only audit names that look like ours; vanilla part names are not our business
        if val.startswith(("2Raine", "A2Raine", "ARaine", "Toncihana.")):
            part_refs.append((val, rel))

bad_parts = []
for val, rel in part_refs:
    leaf = val.rsplit(".", 1)[-1]
    if leaf not in script_classes:
        bad_parts.append((val, rel,
                          "names no C# class in Scripts/ -- the part will never attach and its "
                          "rules will silently do nothing"))
        continue
    ns = script_class_namespaces.get(leaf, "")
    # The engine only reaches classes under XRL.World.Parts by bare name.
    if ns != "XRL.World.Parts" and "." not in val:
        bad_parts.append((val, rel,
                          "is declared in namespace '%s', but the engine resolves parts as "
                          "'XRL.World.Parts.' + name -- write it fully qualified as '%s.%s'"
                          % (ns or "(global)", ns or "", leaf)))

if bad_parts:
    for v, r, why in bad_parts:
        problems.append("REF  <part Name=\"%s\"> (%s) %s" % (v, r, why))
elif part_refs:
    ok.append("REF  all %d declared <part Name=> values resolve, namespace included" % len(part_refs))

# ---------------- <anatomy Category=...> must be one the engine knows ----------------
# BodyPartCategory.GetCode switches on a fixed list of names and throws on anything else. An unknown
# category aborts the WHOLE Bodies.xml, so the anatomy silently never exists and every creature that
# uses it falls back or fails. This validator let 'Category="Elemental"' through because it was not
# checking categories at all.
VALID_BODY_CATEGORIES = {
    "Animal", "Arthropod", "Plant", "Fungal", "Protoplasmic", "Cybernetic", "Mechanical",
    "Metal", "Mollusk", "Wooden", "Stone", "Glass", "Leather", "Bone", "Chitin", "Plastic",
    "Cloth", "Psionic",
}
cat_refs = []
for path in sorted(xml_files):
    rel = os.path.relpath(path, MOD)
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()
    for m in re.finditer(r'<anatomy\s+[^>]*Category="([^"]+)"', text):
        cat_refs.append((m.group(1).strip(), rel))

bad_cats = [(c, r) for c, r in cat_refs if c not in VALID_BODY_CATEGORIES]
if bad_cats:
    for c, r in bad_cats:
        problems.append("REF  <anatomy Category=\"%s\"> (%s) is not a BodyPartCategory the engine "
                        "knows; the whole Bodies.xml will fail to load. Valid: %s"
                        % (c, r, ", ".join(sorted(VALID_BODY_CATEGORIES))))
elif cat_refs:
    ok.append("REF  all %d <anatomy Category=> values are valid BodyPartCategories" % len(cat_refs))

# ---------------- <skill Name=...> in a subtype must resolve to a C# class ----------------
# This is the check that the world-creation crash needed. GameObject.AddSkill(string) resolves
#   XRL.World.Parts.Skill.<the name you pass>
# via XRL.ModManager.ResolveType, so the name in <skill Name="..."> inside Subtypes.xml has to be
# the C# CLASS name, not the pretty skill name. When the two differed we got
# Activator.CreateInstance(null) and the game died at "creating world".
declared_skills = set()      # every <skill>/<power> Class= we declare
for path in sorted(xml_files):
    rel = os.path.relpath(path, MOD)
    if not rel.lower().endswith("skills.xml"):
        continue
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()
    for m in re.finditer(r'<(?:skill|power)\s+Name="([^"]+)"', text):
        declared_skills.add(m.group(1))
    for m in re.finditer(r'<(?:skill|power)\s+[^>]*?Class="([^"]+)"', text, re.S):
        declared_skills.add(m.group(1))

skill_refs = []
for path in sorted(xml_files):
    rel = os.path.relpath(path, MOD)
    if rel.lower().endswith("skills.xml"):
        continue
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()
    # only the borrow-into-character lists matter: <skills><skill Name=... /></skills>
    for block in re.finditer(r'<skills>(.*?)</skills>', text, re.S):
        for m in re.finditer(r'<skill\s+Name="([^"]+)"', block.group(1)):
            skill_refs.append((m.group(1), rel))

bad_skill_refs = []
for name, rel in skill_refs:
    if name in declared_skills:
        continue
    # vanilla skills are borrowed all the time and are not ours to check
    if not name.startswith(("2Raine", "A2Raine", "ARaine")):
        continue
    bad_skill_refs.append((name, rel))

if bad_skill_refs:
    for name, rel in bad_skill_refs:
        problems.append("REF  <skill Name=\"%s\"> (%s) does not match any <skill Class=> we declare "
                        "-- AddSkill resolves XRL.World.Parts.Skill.<name>, so this is null and "
                        "crashes new-game creation" % (name, rel))
elif skill_refs:
    ok.append("REF  all %d <skill Name=> grants resolve to a declared skill class" % len(skill_refs))

# ---------------- every Tile=/PreviewImage= target must exist ----------------
tile_re = re.compile(r'(?:Tile|PreviewImage|ImagePath)="([^"]+)"')
for path in sorted(xml_files):
    rel = os.path.relpath(path, MOD)
    text = open(path, "r", encoding="utf-8").read()
    for m in tile_re.finditer(text):
        ref = m.group(1)
        # Tiles under the mod's own Textures/2Raine_Toncihana folder must exist here; anything else is a
        # reference into the base game's own art (Mutations/..., UI/..., creatures/...) and is
        # resolved by the game, not by this mod.
        if not ref.startswith("2Raine_Toncihana/"):
            continue
        # mod textures are referenced WITHOUT the Textures/ prefix
        cand = os.path.join(MOD, "Textures", *ref.split("/"))
        cand2 = os.path.join(MOD, *ref.split("/"))
        if os.path.exists(cand) or os.path.exists(cand2):
            ok.append("TILE %-46s -> %s" % (rel, ref))
        else:
            problems.append("TILE %-46s -> MISSING file for %s" % (rel, ref))

# manifest PreviewImage
mf = os.path.join(MOD, "manifest.json")
if os.path.exists(mf):
    pv = json.load(open(mf, encoding="utf-8")).get("PreviewImage")
    if pv and not os.path.exists(os.path.join(MOD, *pv.split("/"))):
        problems.append("TILE manifest.json -> MISSING %s" % pv)

# ---------------- blueprint references used by the genotype ----------------
gen = os.path.join(MOD, "Genotypes.xml")
sub = os.path.join(MOD, "Subtypes.xml")
body = os.path.join(MOD, "ObjectBlueprints", "2Raine_2Raine_Toncihana_Bodies.xml")
sdir = os.path.join(MOD, "Scripts")


def cs_sources():
    out = {}
    for f in os.listdir(sdir):
        if f.endswith(".cs"):
            out[f] = open(os.path.join(sdir, f), encoding="utf-8").read()
    return out


def has_class(full_name):
    """True if some .cs declares `class <SimpleName>` (namespace checked loosely)."""
    simple = full_name.split(".")[-1]
    return any(("class " + simple) in txt for txt in cs_sources().values())


# every embark module Class= must have a matching C# class
emb = os.path.join(MOD, "2Raine_Toncihana_EmbarkModules.xml")
if os.path.exists(emb):
    etext = open(emb, encoding="utf-8").read()
    for m in re.finditer(r'<module\s+Class="([^"]+)"', etext):
        cls = m.group(1)
        if has_class(cls):
            ok.append("REF  embark module Class=%s has a C# definition" % cls)
        else:
            problems.append("REF  embark module Class=%s has NO C# definition" % cls)
    # the module must actually mention the boot event it relies on
    if "BOOTEVENT_BOOTPLAYEROBJECT" in "".join(cs_sources().values()):
        ok.append("REF  a Scripts/*.cs listens for BOOTEVENT_BOOTPLAYEROBJECT")

# a genotype must NOT declare <mutation>: GenotypeEntry has no such field, so it is silently
# dropped. This check exists because that mistake already cost one debugging round.
if os.path.exists(gen):
    gtext0 = open(gen, encoding="utf-8").read()
    if re.search(r'<mutation\s', gtext0):
        problems.append("REF  Genotypes.xml contains a <mutation> element, which the genome "
                        "parser IGNORES (GenotypeEntry has no mutation field)")
    else:
        ok.append("REF  Genotypes.xml declares no <mutation> (correct: granted in code instead)")

if os.path.exists(gen) and os.path.exists(sub) and os.path.exists(body):
    gtext = open(gen, encoding="utf-8").read()
    stext = open(sub, encoding="utf-8").read()
    btext = open(body, encoding="utf-8").read()

    m = re.search(r'BodyObject="([^"]+)"', gtext)
    if m:
        if 'Name="%s"' % m.group(1) in btext:
            ok.append("REF  genotype BodyObject=%s defined in 2Raine_Toncihana_Bodies.xml" % m.group(1))
        else:
            problems.append("REF  genotype BodyObject=%s NOT defined" % m.group(1))

    m = re.search(r'Subtypes="([^"]+)"', gtext)
    if m:
        if 'ID="%s"' % m.group(1) in stext:
            ok.append("REF  genotype Subtypes=%s defined in Subtypes.xml" % m.group(1))
        else:
            problems.append("REF  genotype Subtypes=%s NOT defined" % m.group(1))

    mutxml_raw = open(os.path.join(
        MOD, "ObjectBlueprints", "2Raine_2Raine_Toncihana_Mutations.xml"), encoding="utf-8").read()
    # Strip XML comments first: they legitimately contain example <mutation .../> snippets that
    # would otherwise be validated as if they were real declarations.
    mutxml = re.sub(r"<!--.*?-->", "", mutxml_raw, flags=re.S)

    m = re.search(r'Class="([^"]+)"', mutxml)
    if m:
        cls = m.group(1)
        if has_class(cls):
            ok.append("REF  mutation Class=%s has a C# definition" % cls)
        else:
            problems.append("REF  mutation Class=%s has NO C# definition" % cls)

    # The engine resolves a mod mutation's C# type through its Name=, so Name must equal Class or
    # AddMutation silently returns -1 (proven in game: 'Overcharged Electrical Generation' -> -1,
    # while vanilla 'Regeneration' -> 0). Vanilla may differ (Flaming Ray / FlamingRay) because the
    # engine already knows those types -- so only OUR entries are checked here.
    # DisplayName= is the legitimate way to show different text on the mutation screen.
    for mm in re.finditer(r'<mutation\b([^>]*)/?>', mutxml):
        attrs = mm.group(1)
        n = re.search(r'Name="([^"]+)"', attrs)
        c = re.search(r'Class="([^"]+)"', attrs)
        if not (n and c):
            continue
        if n.group(1) != c.group(1):
            problems.append(
                "REF  mutation Name=%r != Class=%r -- for a MOD mutation the engine resolves the "
                "type BY NAME, so AddMutation would return -1 and it would never appear. Make them "
                "equal and use DisplayName= for the pretty text."
                % (n.group(1), c.group(1)))
        else:
            ok.append("REF  mutation Name==Class (%s) -- resolvable" % n.group(1))

    # EVERY part class that lives in Scripts/ must have an XML entry of the RIGHT KIND, or the game
    # never learns it exists and it can never be granted. This is the check whose absence let four
    # finished ability classes sit in the mod for several rounds while none of them appeared in game.
    #
    # Mutations -> ObjectBlueprints/2Raine_Toncihana_Mutations.xml (<mutation Class=...>)
    # Skills    -> 2Raine_Toncihana_Skills.xml (<skill Class=...>)
    skills_path = os.path.join(MOD, "2Raine_2Raine_Toncihana_Skills.xml")
    skxml = ""
    if os.path.exists(skills_path):
        skxml = re.sub(r"<!--.*?-->", "",
                       open(skills_path, encoding="utf-8").read(), flags=re.S)
    else:
        problems.append("REF  2Raine_Toncihana_Skills.xml is MISSING")

    decl_mut = set(re.findall(r'<mutation\b[^>]*?\bClass="([^"]+)"', mutxml))
    # Skills declare their abilities as child <power Class="..."> nodes, so both are collected.
    decl_skl = set(re.findall(r'<skill\b[^>]*?\bClass="([^"]+)"', skxml))
    decl_skl |= set(re.findall(r'<power\b[^>]*?\bClass="([^"]+)"', skxml))
    granted_src = "".join(cs_sources().values())

    # the parent skill must be the thing actually granted
    parent = re.search(r'<skill\b[^>]*?\bClass="([^"]+)"', skxml)
    if parent:
        if re.search(r'\bclass\s+%s\s*:' % re.escape(parent.group(1)), granted_src):
            ok.append("REF  parent skill %s exists in code" % parent.group(1))
        if ('"%s"' % parent.group(1)) in granted_src:
            ok.append("REF  parent skill %s is granted by name from code" % parent.group(1))
        else:
            problems.append("REF  parent skill %s is never granted" % parent.group(1))

    # the abilities must be declared as POWERS of that skill, not as loose top-level skills
    n_powers = len(re.findall(r'<power\b', skxml))
    if n_powers:
        ok.append("REF  %d abilities are declared as <power> children of the parent skill" % n_powers)
    else:
        problems.append("REF  no <power> children found -- the abilities are not grouped under a skill")

    # Hidden="true" makes a skill invisible in the skill screen, and it takes its powers with it.
    # This bit the mod once already. Vanilla defines 21 skills and only Nonlinearity is Hidden,
    # because that one is granted by the Precognition mutation instead of being bought.
    hidden = re.findall(r'<skill\b[^>]*\bHidden="true"', skxml)
    if hidden:
        problems.append(
            'REF  %d <skill> element(s) carry Hidden="true", which removes them AND their powers '
            "from the skill screen. Remove the attribute unless that is deliberate." % len(hidden))
    else:
        ok.append("REF  no <skill> is Hidden, so the tree shows in the skill screen")

    for fname, txt in sorted(cs_sources().items()):
        for cls in re.findall(r'class\s+(\w+)\s*:\s*[^\n{]*BaseMutation', txt):
            if cls in decl_mut:
                ok.append("REF  BaseMutation class %s is registered in 2Raine_Toncihana_Mutations.xml" % cls)
            else:
                problems.append(
                    "REF  class %s extends BaseMutation but has NO <mutation Class=\"%s\"> entry."
                    % (cls, cls))
        # Abstract base classes are shape, not content: they must NOT have an XML entry, because
        # nothing is ever granted by their name.
        for cls in re.findall(r'(?<!abstract )class\s+(\w+)\s*:\s*[^\n{]*BaseSkill', txt):
            if cls in decl_skl:
                ok.append("REF  BaseSkill class %s is registered in 2Raine_Toncihana_Skills.xml" % cls)
            else:
                problems.append(
                    "REF  class %s extends BaseSkill but has NO <skill Class=\"%s\"> entry."
                    % (cls, cls))
        for cls in re.findall(r'abstract class\s+(\w+)\s*:\s*[^\n{]*BaseSkill', txt):
            if cls in decl_skl:
                problems.append("REF  abstract class %s has an XML entry, but nothing grants it" % cls)
            else:
                ok.append("REF  abstract base %s correctly has no XML entry" % cls)
        # every registered class must also be granted by name somewhere in the code
        for cls in list(re.findall(r'class\s+(\w+)\s*:\s*[^\n{]*Base(?:Mutation|Skill)', txt)):
            if cls in decl_mut or cls in decl_skl:
                if ('"%s"' % cls) in granted_src:
                    ok.append("REF  %s is granted by name from code" % cls)
                else:
                    problems.append(
                        "REF  %s is registered but nothing ever grants it by name, so no character "
                        "will ever have it." % cls)

    # every ability class must be reachable: declared in the skills xml AND granted by the code
    # path that attaches the parent skill (the parent attaches them, so the mutator names them).
    for cls in sorted(decl_skl):
        reachable = ('"%s"' % cls) in granted_src
        if not reachable:
            problems.append("REF  ability %s is declared in the skills xml but nothing references it "
                            "in code, so it can never be granted" % cls)
        else:
            ok.append("REF  ability %s is referenced by the granting code" % cls)

    # every mutation name the code grants must match the mutations xml Name= exactly
    over = re.search(r'Overcharged\s*=\s*"([^"]+)"', "".join(cs_sources().values()))
    if over and ('Name="%s"' % over.group(1)) in mutxml:
        ok.append("REF  code grants '%s', which is the declared Name=" % over.group(1))
    elif over:
        problems.append("REF  code grants '%s' but the mutations xml declares no such Name="
                        % over.group(1))

    # legacy check kept for completeness: does the code mention the plain vanilla Regeneration name
    if '"Regeneration"' in "".join(cs_sources().values()):
        ok.append("REF  code grants vanilla 'Regeneration' by its exact Name")

    # The innate mutations are handed over in code, so check that the module really grants each of
    # them by the same name the mutations file / vanilla defines.
    module_src = ""
    for f, txt in cs_sources().items():
        if "2Raine_Toncihana_Mutator" in txt or "2Raine_Toncihana_GenotypeModule" in txt:
            module_src += txt
    if not module_src:
        problems.append("REF  mutation-granting module not found in Scripts/")
    else:
        for want in ('"2Raine_Toncihana_Stormcharge"', '"Regeneration"'):
            if want in module_src:
                ok.append("REF  innate mutation granted by module: %s" % want.strip('"'))
            else:
                problems.append("REF  module does NOT grant %s" % want.strip('"'))
        for want in ('2Raine_Toncihana_StormCalling',):
            if want in module_src:
                ok.append("REF  innate skill granted by module: %s" % want)
            else:
                problems.append("REF  module does NOT grant skill %s" % want)
        for want in ('2Raine_Toncihana_ThunderLordDecree', '2Raine_Toncihana_ThunderFire',
                     '2Raine_Toncihana_ThunderStep', '2Raine_Toncihana_LightningSnake'):
            if want in module_src:
                ok.append("REF  ability part verified in diagnostic: %s" % want)
            else:
                problems.append("REF  module does not verify ability part %s" % want)
        if re.search(r'<mutation\s', gtext):
            problems.append("REF  Genotypes.xml still declares <mutation>, which is ignored")

# tiles that reference the MOD's own Textures must exist
print("=" * 78)
for line in ok:
    print("  OK   " + line)
print("=" * 78)
if problems:
    print("PROBLEMS (%d):" % len(problems))
    for p in problems:
        print("  !!   " + p)
    sys.exit(1)
print("all checks passed")
