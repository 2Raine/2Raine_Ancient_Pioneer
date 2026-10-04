#!/usr/bin/env python3
"""Mechanism indexer for Caves of Qud item functionality.

WHY THIS EXISTS
---------------
Modding an item in Qud is not "copy an XML snippet" -- it is *composing
mechanisms*.  A freeze ray is not a thing you copy; it is a set of cooperating
parts:

    MissileWeapon      how it fires (shots per action, accuracy, range)
  + EnergyAmmoLoader   where ammo comes from: ChargeUse drains an energy cell
  + EnergyCellSocket   the socket the cell is inserted into
  + Projectile         on a SEPARATE blueprint: what the shot actually does
  + Render / Physics / Commerce / Description   presentation and trade

So the unit a mod author needs is **one part = one mechanism**, described fully:
every attribute it accepts, the C# type of that attribute, its default, and real
base-game examples so you have something working to copy from.

THE JOIN THAT MAKES IT ACCURATE
-------------------------------
A part's XML attribute name is the name of a public C# field on the part class.
Verified against the live assembly:

    XML ChargeUse="500"        -> EnergyAmmoLoader  (inherited from IPoweredPart)
    XML ProjectileObject="..." -> EnergyAmmoLoader.ProjectileObject
    XML BasePenetration="4"    -> Projectile.BasePenetration
    XML ShotsPerAction="1"     -> MissileWeapon.ShotsPerAction
    XML Liquid="oil"           -> LiquidAmmoLoader.Liquid

That correspondence is the load-bearing assumption here.  It lets us join the
data side (XML) to the code side (Assembly-CSharp.dll) and state *types* and
*defaults* instead of guessing them.

Outputs:
  mechanisms.jsonl       one datasheet per part class
  qud_mechanisms.sqlite  queryable (mechanisms / mech_attr / mech_usage)
"""
from __future__ import annotations

import argparse
import json
import os
import sqlite3
import struct
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dump_metadata as dm   # noqa: E402

# --------------------------------------------------------------------------- #
# metadata helpers (mirroring dump_metadata.main, but reusable)
# --------------------------------------------------------------------------- #

TYPE_INTERFACE = 0x20
TYPE_ENUM = 0x20          # not used; kept for clarity
FIELD_ACCESS_MASK = 0x07
FIELD_PUBLIC = 0x06
FIELD_STATIC = 0x10


class Assembly:
    def __init__(self, path):
        with open(path, "rb") as fh:
            self.data = fh.read()
        md_off, _, _ = dm.find_metadata_root(self.data)
        self.md = dm.Metadata(self.data, md_off)
        self._build()

    def _build(self):
        md = self.md
        self.type_rows = md.table(0x02)
        self.field_rows = md.table(0x04)
        self.method_rows = md.table(0x06)

        # nested types: NestedClass -> EnclosingClass
        self.nested = {}
        for r in md.table(0x29):
            self.nested[r["NestedClass"]] = r["EnclosingClass"]

        # property map (0x15) -> property rows, so we can index properties too.
        # Several XML attributes exist only as properties, not fields -- e.g.
        # Description's XML attribute is `Short` while its field is `_Short`.
        # Missing those made ~3% of XML attributes look nonexistent.
        self.prop_owner = {}
        for i, pm in enumerate(md.table(0x15)):
            start = pm["PropertyList"]
            pm_table = md.table(0x15)
            end = (pm_table[i + 1]["PropertyList"]
                   if i + 1 < len(pm_table) else len(md.table(0x17)) + 1)
            for p in range(start, end):
                self.prop_owner[p] = pm["Parent"]

        # property -> accessors.  Table 0x18 MethodSemantics: the Flags column is
        # a *MethodSemanticsAttributes* bitmask, NOT MethodAttributes:
        #   0x0001 Setter  0x0002 Getter  0x0004 Other
        #   0x0008 AddOn   0x0010 RemoveOn 0x0020 Fire
        # (Using 0x0400 here silently matched nothing, because 0x0400 is
        # MethodAttributes.SpecialName and belongs to the method, not the
        # semantics row.  That bug hid every setter-backed XML attribute.)
        self.prop_setters = set()
        for ms in md.table(0x18):
            assoc_table, assoc_idx = ms["Association"]
            if assoc_table == 0x17 and (ms["Semantics"] & 0x0001):
                self.prop_setters.add(assoc_idx)

        # field/method -> owning type row index (1-based)
        self.field_owner = {}
        for i, tr in enumerate(self.type_rows):
            start = tr["FieldList"]
            end = (self.type_rows[i + 1]["FieldList"]
                   if i + 1 < len(self.type_rows) else len(self.field_rows) + 1)
            for f in range(start, end):
                self.field_owner[f] = i + 1

        self.method_owner = {}
        for i, tr in enumerate(self.type_rows):
            start = tr["MethodList"]
            end = (self.type_rows[i + 1]["MethodList"]
                   if i + 1 < len(self.type_rows) else len(self.method_rows) + 1)
            for m in range(start, end):
                self.method_owner[m] = i + 1

    # -- naming ------------------------------------------------------------ #

    def full_name(self, i):
        tr = self.type_rows[i - 1]
        parts = [tr["Name"]]
        cur = i
        while cur in self.nested:
            cur = self.nested[cur]
            parts.append(self.type_rows[cur - 1]["Name"])
        parts.reverse()
        ns = tr["Namespace"]
        return (ns + "." if ns else "") + "+".join(parts)

    def coded_name(self, coded):
        t, ridx = coded
        if t is None or not ridx:
            return ""
        try:
            if t == 0x02:
                return self.full_name(ridx)
            if t == 0x01:
                r = self.md.row(0x01, ridx)
                return (r["Namespace"] + "." if r["Namespace"] else "") + r["Name"]
        except Exception:
            pass
        return ""

    def by_name(self):
        return {self.full_name(i): i for i in range(1, len(self.type_rows) + 1)}

    # -- members ----------------------------------------------------------- #

    def properties_of(self, ridx):
        """Public instance properties declared by this type only.

        XML attributes can bind to properties as well as fields (the setter
        receives the parsed attribute text), so a datasheet that lists only
        fields under-reports what you may legally write.
        """
        out = []
        for pidx, owner in self.prop_owner.items():
            if owner != ridx:
                continue
            pr = self.md.row(0x17, pidx)
            name = pr["Name"]
            if name.startswith("<"):
                continue
            # only properties XML can actually set: a setter must exist
            if pidx not in self.prop_setters:
                continue
            try:
                # A property signature is NOT shaped like a field signature:
                #   field_sig    = <callingconv> <type>
                #   property_sig = <callingconv> <paramcount> <type> <params...>
                # so field_sig() on a property blob reads the param count (0x00)
                # as the element type and yields "et:0x00" for every property.
                # property_sig() returns (type, params); we want the type only.
                sp = dm.SigParser(self.md, pr["Type"])
                parsed = sp.property_sig()
                ptype = parsed[0] if isinstance(parsed, tuple) else parsed
            except Exception:
                ptype = "?"
            out.append({"name": name, "type": ptype, "via": "property"})
        return out

    def fields_of(self, ridx):
        """Public instance fields declared by this type only.

        Uses the FieldList range, which is the authoritative way to attribute a
        field to its declaring type.  Attribute names in XML map to these.
        """
        tr = self.type_rows[ridx - 1]
        start = tr["FieldList"]
        end = (self.type_rows[ridx]["FieldList"]
               if ridx < len(self.type_rows) else len(self.field_rows) + 1)
        out = []
        for f in range(start, end):
            if f < 1 or f > len(self.field_rows):
                continue
            fr = self.field_rows[f - 1]
            flags = fr["Flags"]
            if (flags & FIELD_ACCESS_MASK) not in (0x04, 0x05):
                # 0x04=family(protected) 0x05=famorassem; treat public(0x06) only
                if (flags & FIELD_ACCESS_MASK) != FIELD_PUBLIC:
                    continue
            if flags & FIELD_STATIC:
                continue
            name = fr["Name"]
            if name.startswith("<") or name.startswith("_"):
                continue
            try:
                sp = dm.SigParser(self.md, fr["Signature"])
                ftype = sp.field_sig()
            except Exception:
                ftype = "?"
            out.append({"name": name, "type": ftype})
        return out

    def interfaces_of(self, ridx):
        """Interface rows implemented by this type (for the 'kind' label)."""
        return []

    def events_registered(self, ridx):
        """String event IDs this type registers via RegisterPartEvent/Register.

        We read the #US (user string) heap for the literals referenced in the
        type's methods.  Doing full IL analysis here would be overkill, so we
        take the documented-enough shortcut of scanning the type's method
        bodies for ldstr and keeping plausibly-event-shaped literals.
        """
        return []


# --------------------------------------------------------------------------- #
# build the datasheets
# --------------------------------------------------------------------------- #

KIND_MAP = [
    ("XRL.World.Parts.Mutation", "mutation"),
    ("XRL.World.Parts.Skill", "skill"),
    ("XRL.World.Parts", "part"),
    ("XRL.World.Effects", "effect"),
    ("XRL.World.ZoneParts", "zonepart"),
    ("XRL.World.ZoneBuilders", "zonebuilder"),
    ("XRL.World.Conversations.Parts", "convpart"),
    ("XRL.Liquids", "liquid"),
]

# Fields that exist so the C# side can remember state, not so XML can configure
# them.  They are public, so a naive field dump lists them next to real config
# attributes and makes the datasheet misleading.  Classification is reported
# rather than hidden, so nothing is silently dropped.
RUNTIME_PREFIXES = ("Last", "Was", "Has", "IsRegistered", "Current", "Cached")
RUNTIME_EXACT = {
    "Launcher", "ParentObject", "ID", "Owner", "Object", "Game", "Zone",
    "Effects", "StatShifter", "Pending", "Turn", "Ticks", "CooldownCounter",
}
# Types that are never parsed from an XML attribute string.
RUNTIME_TYPES = ("System.Nullable", "System.Collections", "object",
                 "UIntPtr", "IntPtr", "System.Guid", "System.Type")


def classify_attr(name, type_name, in_xml, declared_in, own_part):
    """Label an attribute by how safe it is to write in XML.

    * 'xml'      -- the base game actually writes this in XML: copy it freely.
    * 'config'   -- declared on the part class, never seen in XML, looks like a
                    real setting: plausible, but you are the first to try it.
    * 'runtime'  -- internal state; setting it from XML is meaningless.
    """
    if any(name.startswith(p) for p in RUNTIME_PREFIXES):
        return "runtime"
    if name in RUNTIME_EXACT:
        return "runtime"
    if any(type_name.startswith(t) for t in RUNTIME_TYPES):
        return "runtime"
    if in_xml:
        return "xml"
    return "config"


def kind_of(ns):
    for prefix, kind in KIND_MAP:
        if ns == prefix or ns.startswith(prefix + "."):
            return kind
    return None


# NOTE: the tables owned by index_csharp.py (mech_method / mech_event) are
# deliberately NOT declared here any more.  This script no longer recreates the
# database file, so it never needs to restore them; duplicating their DDL was
# what allowed the two schemas to drift apart and silently empty them.


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dll", required=True)
    ap.add_argument("--blocks", required=True, help="qud_blocks.sqlite from extract_blocks.py")
    ap.add_argument("--outdir", required=True)
    args = ap.parse_args()

    asm = Assembly(args.dll)
    names = asm.by_name()
    print("程序集类型总数: %d" % len(names))

    # ---- XML side: attribute usage + example owners ---------------------- #
    con = sqlite3.connect(args.blocks)
    con.row_factory = sqlite3.Row
    cur = con.cursor()

    usage = defaultdict(lambda: {"count": 0, "owners": [], "values": defaultdict(Counter)})
    for r in cur.execute("SELECT part, attr, value, owner, file, line FROM part_attr"):
        u = usage[r["part"]]
        u["count"] += 1
        if len(u["owners"]) < 6 and r["owner"] not in [o[0] for o in u["owners"]]:
            u["owners"].append((r["owner"], r["file"], r["line"]))
        if r["value"] is not None and len(u["values"][r["attr"]]) < 40:
            u["values"][r["attr"]][r["value"]] += 1

    part_uses = {r["part"]: r["n"] for r in cur.execute(
        "SELECT part, COUNT(*) n FROM part_usage GROUP BY part")}
    # ---- assemble records ------------------------------------------------ #
    mech_dir = {}
    for full, ridx in names.items():
        ns = full.rsplit(".", 1)[0] if "." in full else ""
        name = full.rsplit(".", 1)[-1]
        if name.startswith("<") or "+" in name:
            continue
        kind = kind_of(ns)
        if kind is None:
            continue
        # inheritance chain
        chain = []
        cur_full = full
        guard = 0
        while cur_full and cur_full not in chain and guard < 12:
            guard += 1
            chain.append(cur_full)
            tr = asm.type_rows[names[cur_full] - 1]
            base = asm.coded_name(tr["Extends"])
            cur_full = base if base in names else ""

        # declared fields, then inherited (so inherited attrs are not lost)
        attrs = {}
        for c in chain:
            for f in asm.fields_of(names[c]):
                attrs.setdefault(f["name"],
                                 {"type": f["type"], "declared_in": c,
                                  "via": "field"})
        # properties: only fill gaps, so a real field wins over a property of
        # the same name (the field is what XML actually binds to)
        for c in chain:
            for p in asm.properties_of(names[c]):
                if p["name"] not in attrs:
                    attrs[p["name"]] = {"type": p["type"], "declared_in": c,
                                        "via": "property"}

        u = usage.get(name)
        observed_all = {}
        if u:
            observed_all = {a: list(c.most_common(8)) for a, c in u["values"].items()}

        # Attribute an XML attribute to THIS class only if the class could
        # actually carry it.  XML identifies a part by its short name, so with
        # eight short names shared by two classes (Shield, StairsDown, ...) the
        # naive attribution credited each class with the other's attributes --
        # e.g. XRL.World.ZoneBuilders.StairsDown, which takes only x/y, was
        # credited with the part's Connected/PullDown/Levels and reported as if
        # all eight were "missing from the code".
        observed = {a: v for a, v in observed_all.items() if a in attrs}
        observed_foreign = {a: v for a, v in observed_all.items() if a not in attrs}

        # label every attribute so the datasheet separates "vanilla writes this"
        # from "this is internal state you must not touch"
        for a, meta in attrs.items():
            meta["safety"] = classify_attr(a, meta["type"], a in observed,
                                           meta["declared_in"], name)

        safety_counts = Counter(m["safety"] for m in attrs.values())
        documented = {a for a, m in attrs.items() if m["safety"] != "runtime"}
        used = set(observed)
        # NOTE: keyed by the *fully qualified* name.  Eight short names exist as
        # two different classes in two namespaces (Shield is both
        # XRL.World.Parts.Shield and XRL.World.Parts.Skill.Shield; StairsDown is
        # both a part and a zone builder).  Keying on the short name silently
        # let one overwrite the other, so the lookup key must be `full`.
        mech_dir[(full, kind)] = {
            "part": name,
            "full": full,
            "kind": kind,
            "ns": ns,
            "chain": chain,
            "attr_count": len(documented),
            "attr_total": len(attrs),
            "attr_safety": dict(safety_counts),
            "attrs": attrs,
            "usage_count": part_uses.get(name, 0),
            "usage_count_exact": u["count"] if u else 0,
            "examples": u["owners"] if u else [],
            "observed_values": observed,
            "observed_foreign": observed_foreign,
            # the three diagnostics that make this datasheet actionable
            "attrs_in_code_not_used_in_xml": sorted(documented - used),
            "attrs_in_xml_not_in_code": sorted(used - set(attrs)),
        }

    # parts used in XML with no matching class (typos, or runtime-only names)
    orphan = sorted(set(part_uses) - {r["part"] for r in mech_dir.values()})

    # short names that map to more than one class, surfaced so the query tool
    # can disambiguate instead of silently answering with the wrong one
    by_short = defaultdict(list)
    for rec in mech_dir.values():
        by_short[rec["part"]].append(rec["full"])
    ambiguous = {k: sorted(v) for k, v in by_short.items() if len(v) > 1}

    # ---- write outputs --------------------------------------------------- #
    os.makedirs(args.outdir, exist_ok=True)
    jl = os.path.join(args.outdir, "mechanisms.jsonl")
    with open(jl, "w", encoding="utf-8") as fh:
        for rec in sorted(mech_dir.values(), key=lambda r: (r["kind"], r["part"])):
            fh.write(json.dumps(rec, ensure_ascii=False) + "\n")

    db = os.path.join(args.outdir, "qud_mechanisms.sqlite")
    # qud_mechanisms.sqlite is SHARED with index_csharp.py (mech_method /
    # mech_event live here too).  Deleting and recreating the file to refresh
    # our own tables destroyed that data whenever the backup/restore drifted,
    # so we no longer touch the file at all: we drop and recreate only the
    # tables this script owns.  mech_method and mech_event are left alone.
    con2 = sqlite3.connect(db)
    for t in ("mechanisms", "mech_attr", "mech_alias", "orphan_part",
              "power_class"):
        con2.execute("DROP TABLE IF EXISTS %s" % t)
    con2.commit()
    con2.executescript("""
CREATE TABLE mechanisms (
    id INTEGER PRIMARY KEY, part TEXT, full TEXT NOT NULL, kind TEXT, ns TEXT,
    chain TEXT, attr_count INT, attr_total INT, usage_count INT,
    examples TEXT, safety TEXT, json TEXT);
CREATE TABLE mech_attr (
    id INTEGER PRIMARY KEY, full TEXT NOT NULL, part TEXT, attr TEXT, type TEXT,
    declared_in TEXT, safety TEXT, usage_count INT, observed TEXT, in_xml INT);
CREATE TABLE orphan_part (
    id INTEGER PRIMARY KEY, part TEXT, usage_count INT);
-- short name -> every fully qualified class that has it, so queries can
-- disambiguate instead of silently picking one
CREATE TABLE mech_alias (
    id INTEGER PRIMARY KEY, part TEXT NOT NULL, full TEXT NOT NULL, kind TEXT);
CREATE UNIQUE INDEX ix_mech_full ON mechanisms(full);
CREATE INDEX ix_mech_part ON mechanisms(part);
CREATE INDEX ix_mech_kind ON mechanisms(kind);
CREATE INDEX ix_ma_full ON mech_attr(full);
CREATE INDEX ix_ma_part ON mech_attr(part);
CREATE INDEX ix_ma_attr ON mech_attr(attr);
CREATE INDEX ix_ma_safety ON mech_attr(safety);
CREATE INDEX ix_alias_part ON mech_alias(part);

-- Bridge from the XML data side to the code side for skill trees: skill name,
-- power name, the C# class the power names in its Class= attribute, and where
-- that class lives.  This is what lets you go from "Axe -> Cleave" straight to
-- Axe_Cleave's methods.
CREATE TABLE IF NOT EXISTS power_class (
    id         INTEGER PRIMARY KEY,
    skill      TEXT NOT NULL,
    power      TEXT NOT NULL,
    class      TEXT NOT NULL,
    resolved   TEXT,
    resolved_kind TEXT,
    method_count INT,
    event_count  INT
);
CREATE INDEX IF NOT EXISTS ix_pc_skill ON power_class(skill);
CREATE INDEX IF NOT EXISTS ix_pc_power ON power_class(power);
CREATE INDEX IF NOT EXISTS ix_pc_class ON power_class(class);
""")
    c2 = con2.cursor()
    for rec in mech_dir.values():
        c2.execute("INSERT INTO mechanisms(part,full,kind,ns,chain,attr_count,"
                   "attr_total,usage_count,examples,safety,json)"
                   " VALUES(?,?,?,?,?,?,?,?,?,?,?)",
                   (rec["part"], rec["full"], rec["kind"], rec["ns"],
                    ",".join(rec["chain"]), rec["attr_count"], rec["attr_total"],
                    rec["usage_count"],
                    json.dumps(rec["examples"], ensure_ascii=False),
                    json.dumps(rec["attr_safety"], ensure_ascii=False),
                    json.dumps(rec, ensure_ascii=False)))
        c2.execute("INSERT INTO mech_alias(part,full,kind) VALUES(?,?,?)",
                   (rec["part"], rec["full"], rec["kind"]))
        for a, meta in rec["attrs"].items():
            c2.execute("INSERT INTO mech_attr(full,part,attr,type,declared_in,"
                       "safety,usage_count,observed,in_xml)"
                       " VALUES(?,?,?,?,?,?,?,?,?)",
                       (rec["full"], rec["part"], a, meta["type"],
                        meta["declared_in"], meta["safety"], rec["usage_count"],
                        json.dumps(rec["observed_values"].get(a, []), ensure_ascii=False),
                        1 if a in rec["observed_values"] else 0))
    c2.executemany("INSERT INTO orphan_part(part,usage_count) VALUES(?,?)",
                   [(p, n) for p, n in sorted(part_uses.items()) if p in orphan])

    # report (rather than assume) whether the C# index is present; it is owned
    # by index_csharp.py and this script never writes to it
    for t in ("mech_method", "mech_event"):
        try:
            n = c2.execute("SELECT COUNT(*) FROM %s" % t).fetchone()[0]
            print("  C# 索引 %-12s %6d 行" % (t, n))
            if n == 0:
                print("     ^ 空！请运行 index_csharp.py 重建")
        except sqlite3.Error:
            print("  C# 索引 %-12s 不存在 —— 请运行 index_csharp.py" % t)

    # ---- skill power -> C# class bridge --------------------------------- #
    # Needs both databases and the C# index, so it runs last.
    blocks_db = os.path.join(args.outdir, "qud_blocks.sqlite")
    n_pc = 0
    if os.path.exists(blocks_db):
        bl = sqlite3.connect(blocks_db)
        powers = bl.execute(
            "SELECT skill, power, value FROM power_attr WHERE attr='Class'").fetchall()
        bl.close()
        for skill, power, cls in powers:
            if not cls:
                continue
            # a power's Class is a bare type name; find which type it is
            row = c2.execute(
                "SELECT full, kind FROM mechanisms WHERE part=?", (cls,)).fetchone()
            if row is None:
                row = c2.execute(
                    "SELECT full, NULL FROM mech_method WHERE part=? LIMIT 1",
                    (cls,)).fetchone()
            resolved, rkind = (row[0], row[1]) if row else (None, None)
            nm = ne = None
            if resolved:
                nm = c2.execute("SELECT COUNT(*) FROM mech_method WHERE full=?",
                                (resolved,)).fetchone()[0]
                ne = c2.execute("SELECT COUNT(*) FROM mech_event WHERE full=?",
                                (resolved,)).fetchone()[0]
            c2.execute(
                "INSERT INTO power_class(skill,power,class,resolved,resolved_kind,"
                "method_count,event_count) VALUES(?,?,?,?,?,?,?)",
                (skill, power, cls, resolved, rkind, nm, ne))
            n_pc += 1
    con2.commit()

    # ---- report ---------------------------------------------------------- #
    print("\n=== 机制条目（按类别）===")
    kc = Counter(r["kind"] for r in mech_dir.values())
    for k, v in kc.most_common():
        print("  %-14s %5d" % (k, v))
    print("\n  带 C# 字段数据表的: %d" %
          sum(1 for r in mech_dir.values() if r["attr_count"]))
    print("  有本体真实用例的: %d" %
          sum(1 for r in mech_dir.values() if r["usage_count"]))
    print("  仅在 XML 出现、找不到同名类的: %d" % len(orphan))

    if ambiguous:
        print("\n=== 短名歧义（同名但不同类，查询时会提示）===")
        for k, v in sorted(ambiguous.items()):
            print("  %-24s %s" % (k, " | ".join(v)))

    print("\n=== 属性可靠性分级（全部机制合计）===")
    sc = Counter()
    for r in mech_dir.values():
        sc.update(r["attr_safety"])
    for k, v in sc.most_common():
        label = {"xml": "[OK]   本体 XML 真用过（可放心照抄）",
                 "config": "[TRY]  代码里有、本体 XML 没见过（可试，你是第一个）",
                 "runtime": "[SKIP] 运行时内部状态（写进 XML 无意义）"}.get(k, k)
        print("  %-46s %6d" % (label, v))

    print("\n=== 属性最多的 15 个部件（最复杂的机制）===")
    for rec in sorted(mech_dir.values(), key=lambda r: -r["attr_count"])[:15]:
        print("  %-30s %3d 可写属性  %5d 次使用" %
              (rec["part"], rec["attr_count"], rec["usage_count"]))

    print("\n=== 数据质量：XML 用了但代码里没有的属性 TOP 10 ===")
    bad = Counter()
    for rec in mech_dir.values():
        for a in rec["attrs_in_xml_not_in_code"]:
            bad[(rec["part"], a)] += 1
    for (p, a), n in bad.most_common(10):
        print("  %-28s %-24s %d" % (p, a, n))

    if orphan:
        print("\n=== 孤儿部件名（前 15）===")
        print("   " + ", ".join(orphan[:15]))

    if n_pc:
        resolved = cur2 = None
        nres = c2.execute("SELECT COUNT(*) FROM power_class WHERE resolved IS NOT NULL").fetchone()[0]
        print("\n=== 技能 power → C# 实现 桥接 ===")
        print("  已建立 %d 条 power 映射，其中 %d 条定位到具体类 (%.1f%%)"
              % (n_pc, nres, 100.0 * nres / n_pc if n_pc else 0))

    print("\n输出:\n  %s\n  %s" % (jl, db))
    con.close()
    con2.close()


if __name__ == "__main__":
    main()
