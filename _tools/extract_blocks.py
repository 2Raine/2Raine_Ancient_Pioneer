#!/usr/bin/env python3
"""Caves of Qud blueprint block extractor (phase 1: the data side).

Splits the base game's XML blueprints into structured, queryable "blocks" so a
mod author can look up an existing object's pattern and reassemble it.

Design notes
------------
* We parse each XML *file* exactly once with lxml, which gives us
  `element.sourceline` -- so every extracted block can be traced back to
  `File.xml:LINE`.  That traceability is the whole point: a block you cannot
  locate in the game's own files is not a reference, it is a rumour.
* We do NOT try to interpret game semantics.  We record structure:
  tag, attributes, nesting and text.  Interpretation is the mod author's job
  (and the wiki's), so the database stores evidence, not opinions.
* Objects are emitted as a *pattern*: the object's own attributes plus its
  direct children grouped by tag.  That maps 1:1 onto how you actually write a
  blueprint, which is what makes reassembly cheap.

Outputs (written next to --outdir):
  blocks.jsonl   one JSON object per blueprint, block, skill, mutation, ...
  qud_blocks.sqlite  same data, indexed for querying

Usage:
  python extract_blocks.py --base <StreamingAssets/Base> --outdir <dir>
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sqlite3
import sys
from collections import Counter

from lxml import etree

# --------------------------------------------------------------------------- #
# XML sanitising
# --------------------------------------------------------------------------- #
#
# The base game's XML is full of *bare* numeric character references with no
# terminating semicolon -- e.g. `AmmoChar="&amp;Y&#15;"` or `Mark="&#11;"`.
# These are code page 437 glyph codes: the game's own parser accepts them, but
# they are not well-formed XML 1.0, so a strict parser refuses the entire file
# (which is how three of the biggest files were being dropped completely).
#
# Note the deliberate two-step: the "no semicolon" form is only treated as a
# character reference when what follows is not a digit.  A plain `&#10;` is
# already legal XML and must be left alone.

_BARE_REF = re.compile(r"&#(\d+)(?!\d)(?!;)")
_REF = re.compile(r"&#(\d+);")

# XML 1.0 legal chars: #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
def _xml_legal(cp: int) -> bool:
    return cp in (0x9, 0xA, 0xD) or 0x20 <= cp <= 0xD7FF or 0xE000 <= cp <= 0xFFFD


def sanitise_xml(text: str) -> tuple[str, dict]:
    """Make the game's non-well-formed numeric refs parseable.

    Returns the rewritten text plus a small report of what was changed, so the
    extraction can be audited rather than silently 'fixed'.
    """
    report = {"bare_refs": 0, "dropped_illegal": 0}

    def fix_bare(m):
        report["bare_refs"] += 1
        cp = int(m.group(1))
        return "&#x%X;" % cp if _xml_legal(cp) else ""

    text = _BARE_REF.sub(fix_bare, text)

    # Any remaining reference to a character XML forbids is unrepresentable in
    # a well-formed document; drop it and record the loss.
    def drop_bad(m):
        cp = int(m.group(1))
        if _xml_legal(cp):
            return m.group(0)
        report["dropped_illegal"] += 1
        return ""

    text = _REF.sub(drop_bad, text)
    return text, report


# --------------------------------------------------------------------------- #
# helpers
# --------------------------------------------------------------------------- #

# Attributes that carry per-blueprint metadata rather than behaviour.
LOAD_ATTRS = {"Load", "Inherits", "Name", "ID", "Class"}


def attrs_of(el) -> dict:
    """Attributes as a plain dict, with the XML-escape quirk preserved as-is.

    Values keep their raw text: `&amp;O` stays `&O` because lxml already
    decoded the entity.  We deliberately do NOT re-escape, because mod authors
    paste these into new XML and need to see the real value together with a
    note that `&` must be written `&amp;` in a file.
    """
    return {k: v for k, v in el.attrib.items()}


def text_of(el) -> str:
    """Collapsed text content, or '' when the element only holds children."""
    t = el.text or ""
    if len(el):
        # mixed content: keep the leading text only, children carry the rest
        t = t.strip()
    return " ".join(t.split())


def child_summary(el) -> list[dict]:
    """One shallow record per direct child: tag, attrs, text, line."""
    out = []
    for c in el:
        if not isinstance(c.tag, str):      # comments / PIs
            continue
        rec = {
            "tag": c.tag,
            "attrs": attrs_of(c),
            "line": c.sourceline,
        }
        txt = text_of(c)
        if txt:
            rec["text"] = txt
        out.append(rec)
    return out


def group_children(el) -> dict[str, list[dict]]:
    """Direct children grouped by tag -- the reassembly-friendly view."""
    g: dict[str, list[dict]] = {}
    for rec in child_summary(el):
        g.setdefault(rec["tag"], []).append(rec)
    return g


# --------------------------------------------------------------------------- #
# per-root-type extraction
# --------------------------------------------------------------------------- #

def extract_objects(tree, path, relpath) -> list[dict]:
    """<objects><object .../></objects> -> one block per object."""
    root = tree.getroot()
    if root.tag != "objects":
        return []
    blocks = []
    for obj in root:
        if not isinstance(obj.tag, str) or obj.tag != "object":
            continue
        name = obj.get("Name") or obj.get("ID")
        if not name:
            continue
        groups = group_children(obj)
        # An object is "interesting" as a reusable pattern if it composes parts.
        blocks.append({
            "kind": "object",
            "name": name,
            "root": "objects",
            "file": relpath,
            "line": obj.sourceline,
            "inherits": obj.get("Inherits", ""),
            "load": obj.get("Load", ""),
            "attrs": attrs_of(obj),
            "parts": groups.get("part", []),
            "stats": groups.get("stat", []),
            "skills": groups.get("skill", []),
            "mutations": groups.get("mutation", []),
            "tags": groups.get("tag", []),
            "properties": groups.get("property", [])
                        + groups.get("intproperty", [])
                        + groups.get("boolproperty", []),
            "inventory": groups.get("inventoryobject", []),
            "other": {k: v for k, v in groups.items()
                      if k not in {"part", "stat", "skill", "mutation", "tag",
                                   "property", "intproperty", "boolproperty",
                                   "inventoryobject"}},
        })
    return blocks


def extract_flat_root(tree, path, relpath, root_tag, kind, name_attr="Name") -> list[dict]:
    """Generic handler for <skills>, <mutations>, <mods>, ...

    These files nest one level of grouping (e.g. <category>) before the leaf
    element, so we walk the whole tree looking for the leaf tag instead of
    assuming a fixed depth.  Hard-coding the depth is how extractors silently
    start dropping entries after a game update.
    """
    root = tree.getroot()
    if root.tag != root_tag:
        return []
    out = []
    for el in root.iter():
        if not isinstance(el.tag, str):
            continue
        if el is root or el.tag != kind:
            continue
        name = el.get(name_attr) or el.get("ID")
        if not name:
            continue
        # parent chain gives us the grouping (e.g. category "Physical")
        chain = []
        p = el.getparent()
        while p is not None and isinstance(p.tag, str) and p is not root:
            chain.append(p.get("Name") or p.tag)
            p = p.getparent()
        chain.reverse()
        rec = {
            "kind": kind,
            "name": name,
            "root": root_tag,
            "file": relpath,
            "line": el.sourceline,
            "group_path": chain,
            "attrs": attrs_of(el),
            "children": child_summary(el),
        }
        txt = text_of(el)
        if txt:
            rec["text"] = txt
        out.append(rec)
    return out


def extract_skills(tree, path, relpath) -> list[dict]:
    """<skills><skill ...><power .../></skill></skills>

    A skill tree is a *composite* block: the tree itself plus its powers, and
    the powers carry the load-bearing data (Class, Cost, Minimum, Prereq).
    Indexing only the tree loses the thing you actually assemble a build from,
    so we emit the tree once *with* its powers, and each power as a row too.
    """
    root = tree.getroot()
    if root.tag != "skills":
        return []
    out = []
    for sk in root:
        if not isinstance(sk.tag, str) or sk.tag != "skill":
            continue
        name = sk.get("Name")
        if not name:
            continue
        powers = []
        for pw in sk:
            if not isinstance(pw.tag, str) or pw.tag != "power":
                continue
            powers.append({
                "tag": "power",
                "attrs": attrs_of(pw),
                "line": pw.sourceline,
                "text": text_of(pw),
            })
        out.append({
            "kind": "skill",
            "name": name,
            "root": "skills",
            "file": relpath,
            "line": sk.sourceline,
            "attrs": attrs_of(sk),
            "powers": powers,
            "power_count": len(powers),
        })
    return out


def extract_part_attrs(records):
    """Flatten part attributes into rows for autocomplete.

    This is the table that answers "what attribute names does part X take, and
    what values do those attributes actually hold in the base game?" -- i.e. the
    difference between guessing an attribute name and copying a real one.
    """
    rows = []
    for rec in records:
        for p in rec.get("parts", []):
            part = p["attrs"].get("Name")
            if not part:
                continue
            for k, v in p["attrs"].items():
                if k == "Name":
                    continue
                rows.append((part, k, v, rec["name"], rec.get("kind"),
                             rec.get("file"), p.get("line")))
    return rows


def extract_power_attrs(records):
    """One row per (skill, power, attribute) for skill powers."""
    rows = []
    for rec in records:
        for p in rec.get("powers", []):
            power = p["attrs"].get("Name")
            if not power:
                continue
            for k, v in p["attrs"].items():
                if k == "Name":
                    continue
                rows.append((rec["name"], power, k, v, p.get("line")))
    return rows


ROOT_HANDLERS = [
    ("objects", lambda t, p, r: extract_objects(t, p, r)),
    ("skills", lambda t, p, r: extract_skills(t, p, r)),
    ("mutations", lambda t, p, r: extract_flat_root(t, p, r, "mutations", "mutation")),
    ("activatedabilities",
     lambda t, p, r: extract_flat_root(t, p, r, "activatedabilities", "ability", "Command")),
    ("mods", lambda t, p, r: extract_flat_root(t, p, r, "mods", "mod", "Part")),
    ("bodies", lambda t, p, r: extract_flat_root(t, p, r, "bodies", "bodyparttype", "Type")),
    ("bodies", lambda t, p, r: extract_flat_root(t, p, r, "bodies", "anatomy")),
    ("genotypes", lambda t, p, r: extract_flat_root(t, p, r, "genotypes", "genotype")),
    ("subtypes", lambda t, p, r: extract_flat_root(t, p, r, "subtypes", "subtype")),
]


def parse_file(path, relpath):
    parser = etree.XMLParser(recover=False, huge_tree=True)
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        raw = fh.read()
    clean, report = sanitise_xml(raw)
    try:
        root = etree.fromstring(clean.encode("utf-8"), parser)
    except etree.XMLSyntaxError as exc:
        return [], {"file": relpath, "error": str(exc)}
    tree = root.getroottree()
    out = []
    matched = False
    for tag, fn in ROOT_HANDLERS:
        if root.tag == tag:
            matched = True
            out.extend(fn(tree, path, relpath))
    if not matched:
        return [], {"file": relpath, "skipped_root": root.tag}
    if report["bare_refs"] or report["dropped_illegal"]:
        out.append({"_meta": True, "kind": "_meta", "name": relpath,
                    "root": root.tag, "file": relpath, "line": 0,
                    "sanitise": report})
    return out, None


# --------------------------------------------------------------------------- #
# storage
# --------------------------------------------------------------------------- #

DDL = """
CREATE TABLE IF NOT EXISTS blocks (
    id        INTEGER PRIMARY KEY,
    kind      TEXT NOT NULL,
    name      TEXT NOT NULL,
    root      TEXT,
    file      TEXT,
    line      INTEGER,
    inherits  TEXT,
    grp       TEXT,               -- comma-joined group_path
    json      TEXT NOT NULL       -- the whole record, verbatim
);
CREATE INDEX IF NOT EXISTS ix_blocks_kind ON blocks(kind);
CREATE INDEX IF NOT EXISTS ix_blocks_name ON blocks(name);
CREATE INDEX IF NOT EXISTS ix_blocks_file ON blocks(file);

-- one row per <part Name="X" .../> occurrence, keyed by the part name so you
-- can ask "which blueprints use part Y" and "what attributes does Y take here"
CREATE TABLE IF NOT EXISTS part_usage (
    id        INTEGER PRIMARY KEY,
    block_id  INTEGER NOT NULL REFERENCES blocks(id),
    part      TEXT NOT NULL,
    owner     TEXT NOT NULL,      -- blueprint name
    owner_kind TEXT,
    file      TEXT,
    line      INTEGER,
    attrs     TEXT NOT NULL,      -- JSON object
    selfclose INTEGER              -- 1 when written <part ... /> (no children)
);
CREATE INDEX IF NOT EXISTS ix_part_usage_part ON part_usage(part);
CREATE INDEX IF NOT EXISTS ix_part_usage_owner ON part_usage(owner);

-- one row per <stat Name="X" .../>, so stat ranges can be surveyed
CREATE TABLE IF NOT EXISTS stat_usage (
    id        INTEGER PRIMARY KEY,
    block_id  INTEGER NOT NULL REFERENCES blocks(id),
    stat      TEXT NOT NULL,
    owner     TEXT NOT NULL,
    file      TEXT,
    line      INTEGER,
    attrs     TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_stat_usage_stat ON stat_usage(stat);

-- THE autocomplete table: every attribute observed on every part, with the
-- blueprint it came from and where to look.  Answers "what can I even write in
-- <part Name='MeleeWeapon' .../>" with real base-game evidence plus examples.
CREATE TABLE IF NOT EXISTS part_attr (
    id        INTEGER PRIMARY KEY,
    part      TEXT NOT NULL,
    attr      TEXT NOT NULL,
    value     TEXT,
    owner     TEXT NOT NULL,
    owner_kind TEXT,
    file      TEXT,
    line      INTEGER
);
CREATE INDEX IF NOT EXISTS ix_part_attr_part ON part_attr(part);
CREATE INDEX IF NOT EXISTS ix_part_attr_attr ON part_attr(attr);
CREATE INDEX IF NOT EXISTS ix_part_attr_pa   ON part_attr(part, attr);

-- same idea for skill powers: one row per (skill, attribute) so you can look up
-- "what attributes does a <power/> take" the same way you can for parts
CREATE TABLE IF NOT EXISTS power_attr (
    id     INTEGER PRIMARY KEY,
    skill  TEXT NOT NULL,
    power  TEXT NOT NULL,
    attr   TEXT NOT NULL,
    value  TEXT,
    line   INTEGER
);
CREATE INDEX IF NOT EXISTS ix_power_attr_skill ON power_attr(skill);
CREATE INDEX IF NOT EXISTS ix_power_attr_attr  ON power_attr(attr);
"""


def store(records, db_path, jsonl_path):
    con = sqlite3.connect(db_path)
    # Idempotent: DDL below uses CREATE TABLE IF NOT EXISTS, so without an
    # explicit drop a second run appends a full duplicate set and every count
    # silently doubles (observed: blocks 5,727 -> 11,454).
    for t in ("blocks", "part_usage", "stat_usage", "part_attr", "power_attr"):
        con.execute("DROP TABLE IF EXISTS %s" % t)
    con.commit()
    con.executescript(DDL)
    cur = con.cursor()
    metas = []
    n = 0
    with open(jsonl_path, "w", encoding="utf-8") as jf:
        for rec in records:
            jf.write(json.dumps(rec, ensure_ascii=False) + "\n")
            if rec.get("kind") == "_meta":
                metas.append(rec)
                continue
            n += 1
            cur.execute(
                "INSERT INTO blocks(kind,name,root,file,line,inherits,grp,json)"
                " VALUES(?,?,?,?,?,?,?,?)",
                (rec["kind"], rec["name"], rec.get("root"), rec.get("file"),
                 rec.get("line"), rec.get("inherits", ""),
                 ",".join(rec.get("group_path", [])),
                 json.dumps(rec, ensure_ascii=False)))
            bid = cur.lastrowid
            for p in rec.get("parts", []):
                cur.execute(
                    "INSERT INTO part_usage(block_id,part,owner,owner_kind,file,line,attrs,selfclose)"
                    " VALUES(?,?,?,?,?,?,?,?)",
                    (bid, p["attrs"].get("Name", "?"), rec["name"], rec["kind"],
                     rec.get("file"), p.get("line"),
                     json.dumps(p["attrs"], ensure_ascii=False),
                     0 if len(p["attrs"]) and "children" in p else 1))
            for s in rec.get("stats", []):
                cur.execute(
                    "INSERT INTO stat_usage(block_id,stat,owner,file,line,attrs)"
                    " VALUES(?,?,?,?,?,?)",
                    (bid, s["attrs"].get("Name", "?"), rec["name"],
                     rec.get("file"), s.get("line"),
                     json.dumps(s["attrs"], ensure_ascii=False)))

        cur.executemany(
            "INSERT INTO part_attr(part,attr,value,owner,owner_kind,file,line)"
            " VALUES(?,?,?,?,?,?,?)", extract_part_attrs(records))
        cur.executemany(
            "INSERT INTO power_attr(skill,power,attr,value,line) VALUES(?,?,?,?,?)",
            extract_power_attrs(records))
    con.commit()
    return con, n, metas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True, help="StreamingAssets/Base directory")
    ap.add_argument("--outdir", required=True)
    args = ap.parse_args()

    os.makedirs(args.outdir, exist_ok=True)

    targets = []
    ob = os.path.join(args.base, "ObjectBlueprints")
    if os.path.isdir(ob):
        for f in sorted(os.listdir(ob)):
            if f.lower().endswith(".xml"):
                targets.append(os.path.join(ob, f))
    for f in ("Skills.xml", "Mutations.xml", "HiddenMutations.xml",
              "ActivatedAbilities.xml", "Mods.xml", "Bodies.xml",
              "Genotypes.xml", "Subtypes.xml"):
        p = os.path.join(args.base, f)
        if os.path.isfile(p):
            targets.append(p)

    records, problems = [], []
    for p in targets:
        rel = os.path.relpath(p, args.base)
        recs, prob = parse_file(p, rel)
        records.extend(recs)
        if prob:
            problems.append(prob)
        print("  %-46s %6d blocks" % (rel, len(recs)))

    db = os.path.join(args.outdir, "qud_blocks.sqlite")
    jl = os.path.join(args.outdir, "blocks.jsonl")
    con, n, metas = store(records, db, jl)

    print("\n=== 汇总 ===")
    print("总条目: %d" % n)
    kc = Counter(r["kind"] for r in records if r.get("kind") != "_meta")
    for k, v in kc.most_common():
        print("  %-16s %6d" % (k, v))
    if metas:
        print("\n=== XML 兼容性修复记录（Qud 用非良构的 CP437 裸字符引用）===")
        for m in metas:
            print("  %-46s 裸引用=%d 丢弃非法=%d"
                  % (m["file"], m["sanitise"]["bare_refs"],
                     m["sanitise"]["dropped_illegal"]))

    cur = con.cursor()
    print("\n=== 被使用最多的部件 TOP 30（组装积木热度表）===")
    for part, n, owners in cur.execute(
            "SELECT part, COUNT(*), COUNT(DISTINCT owner) FROM part_usage"
            " GROUP BY part ORDER BY 3 DESC, 2 DESC LIMIT 30"):
        print("  %-34s %6d 次 / %5d 个蓝图" % (part, n, owners))

    print("\n=== 部件总数 ===")
    print("  唯一部件名: %d" % cur.execute(
        "SELECT COUNT(DISTINCT part) FROM part_usage").fetchone()[0])
    print("  部件使用记录: %d" % cur.execute(
        "SELECT COUNT(*) FROM part_usage").fetchone()[0])
    print("  唯一属性名: %d" % cur.execute("SELECT COUNT(DISTINCT stat) FROM stat_usage").fetchone()[0])
    print("  部件属性指纹: %d 条 / %d 个属性名" % (
        cur.execute("SELECT COUNT(*) FROM part_attr").fetchone()[0],
        cur.execute("SELECT COUNT(DISTINCT attr) FROM part_attr").fetchone()[0]))
    print("  技能: %d 棵树 / %d 个 power" % (
        cur.execute("SELECT COUNT(*) FROM blocks WHERE kind='skill'").fetchone()[0],
        cur.execute("SELECT COUNT(DISTINCT power) FROM power_attr").fetchone()[0]))

    print("\n=== 属性名最全的 12 个部件（写模组时的说明书）===")
    for part, attrs, uses in cur.execute(
            "SELECT part, COUNT(DISTINCT attr), COUNT(*) FROM part_attr"
            " GROUP BY part ORDER BY 2 DESC, 3 DESC LIMIT 12"):
        print("  %-30s %3d 个不同属性 / %5d 次使用" % (part, attrs, uses))

    if problems:
        print("\n=== 解析问题 ===")
        for p in problems:
            print("  ", p)

    con.close()
    print("\n输出:")
    print("  %s" % db)
    print("  %s" % jl)


if __name__ == "__main__":
    main()
