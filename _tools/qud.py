#!/usr/bin/env python3
"""qud.py -- query tool for the Caves of Qud mechanism database.

Built from the base game's own data plus its code, so answers cite real
evidence (file:line) instead of guessing.

Examples
--------
  # how do I make something consume charge and fire a projectile?
  qud.py mech EnergyAmmoLoader
  qud.py mech Projectile
  qud.py mech MissileWeapon

  # every mechanism whose name mentions charge
  qud.py find charge

  # what mechanisms does a freeze ray actually use?
  qud.py item "Freeze Ray"

  # what does this part do across the whole base game?
  qud.py use MeleeWeapon --limit 15

  # which skills/parts exist in a category
  qud.py list part --filter ammo
"""
from __future__ import annotations

import argparse
import json
import os
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DB_MECH = os.path.join(HERE, os.pardir, "qud_db", "qud_mechanisms.sqlite")
DB_BLOCKS = os.path.join(HERE, os.pardir, "qud_db", "qud_blocks.sqlite")

SAFETY_LABEL = {
    "xml": "OK  ",     # base game writes this in XML -- copy freely
    "config": "TRY ",  # real field, never seen in XML -- you are first
    "runtime": "SKIP",  # internal state; meaningless in XML
}


def conn(path):
    if not os.path.exists(path):
        sys.exit("找不到数据库 %s\n请先运行 extract_blocks.py 和 index_mechanisms.py" % path)
    c = sqlite3.connect(path)
    c.row_factory = sqlite3.Row
    return c


# --------------------------------------------------------------------------- #

def resolve_mech(c, part):
    """Resolve a user-supplied mechanism name to exactly one row.

    Eight short names exist as two different classes in two namespaces
    (Shield is both a part and a skill; StairsDown is both a part and a zone
    builder).  Silently picking one answers the wrong question, so when the
    name is ambiguous we show the candidates and make the caller choose.
    Returns (row, candidates).  row is None when a choice is needed.
    """
    rows = c.execute("SELECT * FROM mechanisms WHERE full=? OR part=?"
                     " ORDER BY kind, full", (part, part)).fetchall()
    if len(rows) == 1:
        return rows[0], []
    if not rows:
        return None, []
    return None, rows


def find_mech(c, part, kind=None):
    """Resolve, optionally disambiguated by kind (or a dotted name)."""
    if kind:
        r = c.execute("SELECT * FROM mechanisms WHERE (full=? OR part=?) AND kind=?",
                      (part, part, kind)).fetchone()
        if r:
            return r, []
    return resolve_mech(c, part)


def cmd_code(args):
    """Show a mechanism's methods (with bodies) -- the 'how it works' half."""
    c = conn(DB_MECH)
    row, cands = find_mech(c, args.part, args.kind)
    if cands:
        print("%r 对应 %d 个类，请指定：" % (args.part, len(cands)))
        for r in cands:
            print("    %-44s [%s]" % (r["full"], r["kind"]))
        return 1
    if not row:
        print("没有机制 %r" % args.part)
        return 1
    full = row["full"]
    ms = c.execute(
        "SELECT name,ret,args,attrs,line,body FROM mech_method WHERE full=?"
        " ORDER BY line", (full,)).fetchall()
    ev = c.execute(
        "SELECT event,role,is_min FROM mech_event WHERE full=? ORDER BY role,event",
        (full,)).fetchall()
    print("=" * 78)
    print("%s   方法 %d 个 / 事件绑定 %d 条" % (full, len(ms), len(ev)))
    print("=" * 78)
    if ev:
        reg = [r for r in ev if r["role"] == "register"]
        hnd = [r for r in ev if r["role"] in ("want", "handle")]
        if reg:
            print("\n【注册的字符串事件】%d 条" % len(reg))
            for r in reg:
                print("   %s" % r["event"])
        if hnd:
            print("\n【处理的 MinEvent】%d 条" % len(hnd))
            names = sorted({r["event"] for r in hnd})
            for i in range(0, len(names), 2):
                print("   " + "  ".join("%-40s" % n for n in names[i:i + 2]))
    want = args.method.lower() if args.method else None
    for r in ms:
        if want and want not in r["name"].lower():
            continue
        print("\n--- %s %s(%s)   %s:%s ---"
              % (r["ret"], r["name"], r["args"] or "", "src", r["line"]))
        body = r["body"] or ""
        if not body.strip():
            print("   (空实现)")
            continue
        lines = body.splitlines()
        limit = args.body or 40
        for L in lines[:limit]:
            print("   " + L.rstrip())
        if len(lines) > limit:
            print("   ...（共 %d 行，用 --body %d 看更多）" % (len(lines), len(lines)))
    return 0


def cmd_events(args):
    """List mechanisms bound to an event, or the events a mechanism binds to."""
    c = conn(DB_MECH)
    if args.what:
        rows = c.execute(
            "SELECT part, full, event, role, is_min FROM mech_event"
            " WHERE event LIKE ? GROUP BY part, event ORDER BY part LIMIT ?",
            ("%" + args.what + "%", args.limit)).fetchall()
        if not rows:
            print("没有机制绑定事件 %r" % args.what)
            near = c.execute("SELECT DISTINCT event FROM mech_event"
                             " WHERE event LIKE ? LIMIT 12",
                             ("%" + args.what[:4] + "%",)).fetchall()
            if near:
                print("相近事件: " + ", ".join(r["event"] for r in near))
            return 1
        print("绑定事件 %r 的机制（%d 条）:" % (args.what, len(rows)))
        for r in rows:
            print("  %-30s %-10s %-9s min=%d" % (r["part"], r["event"], r["role"], r["is_min"]))
        return 0
    print("最常被监听的事件 TOP 30：")
    for r in c.execute(
            "SELECT event, COUNT(DISTINCT full) n FROM mech_event"
            " WHERE role IN ('register','want','handle')"
            " GROUP BY event ORDER BY n DESC LIMIT 30"):
        print("  %-46s %4d 个机制" % (r["event"], r["n"]))
    return 0


def cmd_impl(args):
    """Search method bodies for an identifier/pattern."""
    c = conn(DB_MECH)
    rows = c.execute(
        "SELECT m.part, m.full, m.name, m.ret, m.args, m.line, m.body"
        "  FROM mech_method m"
        " WHERE m.body LIKE ?"
        "   AND EXISTS(SELECT 1 FROM mechanisms x WHERE x.full=m.full)"
        " GROUP BY m.full, m.name, m.line LIMIT ?",
        ("%" + args.term + "%", args.limit)).fetchall()
    if not rows:
        print("没有方法体包含 %r" % args.term)
        return 1
    print("方法体包含 %r 的机制（%d 条）:" % (args.term, len(rows)))
    for r in rows:
        sig = "%s(%s)" % (r["name"], (r["args"] or ""))
        if r["name"] == ".ctor":
            sig = "构造器 %s" % (r["args"] or "")   # args already holds "Class()"
        else:
            sig = "%s %s" % (r["ret"], sig)
        print("\n  %-26s %s   line %s" % (r["part"], sig, r["line"]))
        hit = [L.strip() for L in (r["body"] or "").splitlines() if args.term in L]
        for L in hit[:3]:
            print("      %s" % L[:110])
    return 0


def cmd_skills(args):
    """Skill tree -> power -> C# implementation, with prerequisites."""
    c = conn(DB_MECH)
    cb = conn(DB_BLOCKS)
    if args.skill:
        rows = cb.execute(
            "SELECT power, attr, value FROM power_attr WHERE skill LIKE ?",
            ("%" + args.skill + "%",)).fetchall()
        if not rows:
            tree = [r[0] for r in cb.execute(
                "SELECT DISTINCT skill FROM power_attr LIMIT 40")]
            print("没有技能树 %r。可用: %s" % (args.skill, ", ".join(tree)))
            return 1
    else:
        rows = cb.execute("SELECT power, attr, value FROM power_attr").fetchall()

    # power -> {attr: value}
    powers = {}
    for p, a, v in rows:
        powers.setdefault(p, {})[a] = v

    impl = {r["power"]: r for r in c.execute(
        "SELECT power, class, resolved, method_count, event_count FROM power_class")}

    if not args.skill:
        trees = cb.execute(
            "SELECT skill, COUNT(DISTINCT power) n FROM power_attr"
            " GROUP BY skill ORDER BY skill").fetchall()
        print("%-34s %s" % ("技能树", "power 数"))
        print("-" * 48)
        for t in trees:
            print("%-34s %d" % (t["skill"], t["n"]))
        print("\n（用 qud.py skills <技能名> 看某个技能树的完整结构）")
        return 0

    name = rows[0][0] if False else None
    skill = cb.execute("SELECT DISTINCT skill FROM power_attr WHERE skill LIKE ?",
                       ("%" + args.skill + "%",)).fetchone()[0]
    print("=" * 78)
    print("技能树 %s   —— %d 个 power" % (skill, len(powers)))
    print("=" * 78)
    print("%-22s %-5s %-4s %-30s %s" % ("power", "花费", "属性", "C# 实现", "前置"))
    print("-" * 78)
    for pname in sorted(powers):
        p = powers[pname]
        im = impl.get(pname)
        cls = (im["class"] if im else "") or ""
        mc = ("(%d 方法)" % im["method_count"]) if im and im["method_count"] else ""
        print("%-22s %-5s %-4s %-30s %s"
              % (pname[:21], p.get("Cost", ""), (p.get("Attribute", "") or "")[:4],
                 (cls + " " + mc)[:30], p.get("Prereq", "")))
    print("\n（用 qud.py code <Class> 看某个 power 的实现）")
    return 0


def cmd_mech(args):
    """Print the full datasheet for one mechanism (= one part)."""
    c = conn(DB_MECH)
    row, cands = find_mech(c, args.part, args.kind)
    if cands:
        print("%r 对应 %d 个不同的类，请指定一个：" % (args.part, len(cands)))
        for r in cands:
            print("    %-44s [%s]  %d 个属性" % (r["full"], r["kind"], r["attr_count"]))
        print("\n用法: qud.py mech <全名>    或    qud.py mech %s --kind %s"
              % (args.part, cands[0]["kind"]))
        return 1
    if not row:
        near = c.execute("SELECT DISTINCT part FROM mechanisms WHERE part LIKE ? LIMIT 10",
                         ("%" + args.part + "%",)).fetchall()
        print("没有名为 %r 的机制。" % args.part)
        if near:
            print("你是不是想找: " + ", ".join(r["part"] for r in near))
        return 1
    j = json.loads(row["json"])
    print("=" * 78)
    print("%s   [%s]" % (j["part"], j["kind"]))
    print("=" * 78)
    print("继承链 : %s" % "  ->  ".join(j["chain"]))
    print("本体用量: %d 处蓝图使用" % j["usage_count"])
    nm = c.execute("SELECT COUNT(*) FROM mech_method WHERE full=?", (j["full"],)).fetchone()[0]
    ne = c.execute("SELECT COUNT(*) FROM mech_event WHERE full=?", (j["full"],)).fetchone()[0]
    if nm or ne:
        print("实现   : %d 个成员 / %d 条事件绑定   (qud.py code %s)" % (nm, ne, j["part"]))
    if j["examples"]:
        print("真实范例:")
        for owner, f, line in j["examples"][:5]:
            print("    %-34s %s:%s" % (owner, f, line))
    if j["observed_values"]:
        print()
        print(" 图例: [OK]=本体XML用过(照抄)  [TRY]=代码有/XML没见过(可试)  [SKIP]=运行时内部状态")
    print()
    attrs = j["attrs"]
    xml_at = sorted(a for a, m in attrs.items() if m["safety"] == "xml")
    try_at = sorted(a for a, m in attrs.items() if m["safety"] == "config")
    skip_at = sorted(a for a, m in attrs.items() if m["safety"] == "runtime")

    def dump(names, title):
        if not names:
            return
        print("--- %s (%d) ---" % (title, len(names)))
        for a in names:
            m = attrs[a]
            vals = j["observed_values"].get(a, [])
            vs = ("   取值: " + ", ".join("%s(%dx)" % (v[0], v[1]) for v in vals[:4])) if vals else ""
            print("  %s %-30s %-12s %s%s"
                  % (SAFETY_LABEL[m["safety"]], a, m["type"],
                     "[%s]" % m["declared_in"].rsplit(".", 1)[-1], vs))
        print()

    dump(xml_at, "属性：本体 XML 实际使用过")
    if args.all:
        dump(try_at, "属性：代码里有、本体 XML 未使用（谨慎尝试）")
        dump(skip_at, "属性：运行时内部状态（不要写）")
        foreign = j.get("observed_foreign") or {}
        if foreign:
            print("--- 注意：XML 里挂在同名部件上、但不属于这个类的属性 (%d) ---"
                  % len(foreign))
            for a in sorted(foreign):
                print("   %s   （属于另一个同名类，被本类忽略）" % a)
            print()
    else:
        nf = len(j.get("observed_foreign") or {})
        extra = ("；另有 %d 个同名异类属性被过滤" % nf) if nf else ""
        print("（另有 %d 个未在 XML 出现过的代码字段、%d 个运行时字段%s；加 --all 查看）"
              % (len(try_at), len(skip_at), extra))
    return 0


def cmd_find(args):
    c = conn(DB_MECH)
    rows = c.execute(
        "SELECT part, ns, kind, attr_count, usage_count FROM mechanisms"
        " WHERE part LIKE ? ORDER BY usage_count DESC, part LIMIT ?",
        ("%" + args.term + "%", args.limit)).fetchall()
    print("%-30s %-26s %-11s %5s %7s" % ("机制(类名)", "命名空间", "类别", "属性", "用量"))
    print("-" * 84)
    for r in rows:
        print("%-30s %-26s %-11s %5d %7d"
              % (r["part"], r["ns"], r["kind"], r["attr_count"], r["usage_count"]))
    if not rows:
        print("(无匹配)")
    return 0


def cmd_attr(args):
    """Which mechanisms accept a given attribute name?"""
    c = conn(DB_MECH)
    rows = c.execute(
        "SELECT a.full, a.part, m.ns, a.type, a.declared_in, a.safety, a.observed"
        "  FROM mech_attr a JOIN mechanisms m ON m.full = a.full"
        " WHERE a.attr=?"
        " ORDER BY (a.safety='xml') DESC, a.part LIMIT ?",
        (args.attr, args.limit)).fetchall()
    if not rows:
        print("没有机制接受属性 %r（可能是拼写问题，或它不是 XML 可写属性）" % args.attr)
        return 1
    total = c.execute("SELECT COUNT(DISTINCT full) FROM mech_attr WHERE attr=?",
                      (args.attr,)).fetchone()[0]
    print("接受属性 %r 的机制（共 %d 个，显示前 %d）:" % (args.attr, total, len(rows)))
    for r in rows:
        vals = json.loads(r["observed"] or "[]")
        vs = ("  例: " + ", ".join(v[0] for v in vals[:3])) if vals else ""
        print("  [%s] %-28s %-8s %-11s [%s]%s"
              % (SAFETY_LABEL.get(r["safety"], "?"), r["part"], r["type"],
                 r["ns"].rsplit(".", 1)[-1] if r["ns"] else "",
                 r["declared_in"].rsplit(".", 1)[-1], vs))
    return 0


def cmd_item(args):
    """Show a blueprint's composition -- the mechanism recipe."""
    c = conn(DB_BLOCKS)
    row = c.execute("SELECT * FROM blocks WHERE name=? ORDER BY kind LIMIT 1",
                    (args.name,)).fetchone()
    if not row:
        rows = c.execute("SELECT name, kind FROM blocks WHERE name LIKE ? LIMIT 12",
                         ("%" + args.name + "%",)).fetchall()
        print("找不到蓝图 %r。" % args.name)
        if rows:
            print("相近的: " + ", ".join("%s(%s)" % (r["name"], r["kind"]) for r in rows))
        return 1
    j = json.loads(row["json"])
    print("=" * 78)
    print("%s   [%s]   %s:%s" % (j["name"], j["kind"], j.get("file"), j.get("line")))
    if j.get("inherits"):
        print("继承 : %s" % j["inherits"])
    print("=" * 78)
    if j.get("parts"):
        print("\n【机制组成】%d 个部件：" % len(j["parts"]))
        for p in j["parts"]:
            a = p["attrs"]
            name = a.get("Name", "?")
            rest = " ".join('%s="%s"' % (k, v) for k, v in a.items() if k != "Name")
            print("  %-26s %s" % (name, rest))
    for label, key in (("属性", "stats"), ("技能", "skills"), ("变异", "mutations"),
                       ("标签", "tags"), ("背包", "inventory")):
        if j.get(key):
            print("\n【%s】" % label)
            for e in j[key]:
                a = e["attrs"]
                print("  " + " ".join('%s="%s"' % (k, v) for k, v in a.items()))
    if j.get("properties"):
        print("\n【property】")
        for e in j["properties"]:
            print("  " + " ".join('%s="%s"' % (k, v) for k, v in e["attrs"].items()))
    if args.xml:
        print("\n【可粘贴的 XML】")
        print('<object Name="%s"%s>' % (j["name"],
              ' Inherits="%s"' % j["inherits"] if j.get("inherits") else ""))
        for p in j.get("parts", []):
            a = p["attrs"]
            print('  <part %s />' % " ".join('%s="%s"' % (k, v) for k, v in a.items()))
        print("</object>")
    return 0


def cmd_use(args):
    """Every place a part is used, so you can see the range of real configs."""
    c = conn(DB_BLOCKS)
    rows = c.execute(
        "SELECT owner, owner_kind, file, line, attrs FROM part_usage"
        " WHERE part=? ORDER BY owner LIMIT ?", (args.part, args.limit)).fetchall()
    if not rows:
        print("本体没有蓝图使用部件 %r" % args.part)
        return 1
    print("部件 %r 的本体用法（%d 条）:" % (args.part, len(rows)))
    for r in rows:
        a = json.loads(r["attrs"])
        rest = " ".join('%s="%s"' % (k, v) for k, v in a.items() if k != "Name")
        print("  %-30s %-9s %s:%s" % (r["owner"], r["owner_kind"], r["file"], r["line"]))
        if rest:
            print("      %s" % rest)
    return 0


def cmd_list(args):
    c = conn(DB_MECH)
    q = "SELECT part, attr_count, usage_count FROM mechanisms WHERE kind=?"
    p = [args.kind]
    if args.filter:
        q += " AND part LIKE ?"
        p.append("%" + args.filter + "%")
    q += " ORDER BY usage_count DESC, part LIMIT ?"
    p.append(args.limit)
    rows = c.execute(q, p).fetchall()
    print("%-36s %6s %8s" % ("机制", "属性数", "本体用量"))
    print("-" * 54)
    for r in rows:
        print("%-36s %6d %8d" % (r["part"], r["attr_count"], r["usage_count"]))
    total = c.execute("SELECT COUNT(*) FROM mechanisms WHERE kind=?", (args.kind,)).fetchone()[0]
    print("\n(%s 类别共 %d 个机制，显示前 %d)" % (args.kind, total, len(rows)))
    return 0


def cmd_stats(args):
    c = conn(DB_MECH)
    print("=== 机制数据库总览 ===")
    for r in c.execute("SELECT kind, COUNT(*) n, SUM(usage_count) u FROM mechanisms"
                       " GROUP BY kind ORDER BY n DESC"):
        print("  %-12s %5d 个机制   %8d 次本体使用" % (r["kind"], r["n"], r["u"] or 0))
    print()
    print("  属性分级:")
    for r in c.execute("SELECT safety, COUNT(*) n FROM mech_attr GROUP BY safety ORDER BY n DESC"):
        print("    %-9s %6d" % (r["safety"], r["n"]))
    cb = conn(DB_BLOCKS)
    print()
    for r in cb.execute("SELECT kind, COUNT(*) n FROM blocks GROUP BY kind ORDER BY n DESC"):
        print("  %-12s %5d 个蓝图条目" % (r["kind"], r["n"]))

    # Join integrity: every XML attribute should resolve to a field/property on
    # the part class.  Reported rather than assumed, so a game update that
    # renames something shows up here instead of silently degrading answers.
    print()
    print("=== XML <-> C# 吻合率（健康度指标）===")
    tot = bad = 0
    offenders = []
    for r in c.execute("SELECT part, json FROM mechanisms"):
        j = json.loads(r["json"])
        tot += 1
        miss = j.get("attrs_in_xml_not_in_code") or []
        if miss:
            bad += 1
            offenders.append((r["part"], miss))
    print("  机制总数            : %d" % tot)
    print("  完全吻合            : %d" % (tot - bad))
    print("  有属性对不上        : %d  (%.2f%%)" % (bad, 100.0 * bad / tot if tot else 0))
    print("  属性级吻合率        : %.2f%%" % (100.0 * (tot - bad) / tot if tot else 0))
    if offenders and args.verbose:
        print("  对不上的机制:")
        for p, ms in offenders[:20]:
            print("    %-30s %s" % (p, ms))
    elif offenders:
        print("  （加 --verbose 列出这 %d 个机制）" % len(offenders))
    return 0


def main():
    ap = argparse.ArgumentParser(description="Caves of Qud 机制数据库查询工具")
    sub = ap.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("mech", help="查看一个机制的完整数据表")
    p.add_argument("part", help="类名或全名（同名歧义时用 --kind 指定）")
    p.add_argument("--kind", default=None,
                   help="part/mutation/skill/effect/liquid/convpart/zonebuilder/zonepart")
    p.add_argument("--all", action="store_true", help="连未在 XML 出现的代码字段也列出")
    p.set_defaults(func=cmd_mech)

    p = sub.add_parser("find", help="按名字搜索机制")
    p.add_argument("term")
    p.add_argument("--limit", type=int, default=40)
    p.set_defaults(func=cmd_find)

    p = sub.add_parser("attr", help="反查：哪些机制接受这个属性")
    p.add_argument("attr")
    p.add_argument("--limit", type=int, default=25)
    p.set_defaults(func=cmd_attr)

    p = sub.add_parser("item", help="查看一个蓝图由哪些机制拼成")
    p.add_argument("name")
    p.add_argument("--xml", action="store_true", help="顺带输出可粘贴的 XML")
    p.set_defaults(func=cmd_item)

    p = sub.add_parser("use", help="查看某部件的全部本体用法")
    p.add_argument("part")
    p.add_argument("--limit", type=int, default=10)
    p.set_defaults(func=cmd_use)

    p = sub.add_parser("list", help="列出某类别的机制")
    p.add_argument("kind", help="part/mutation/skill/effect/liquid/convpart/zonebuilder/zonepart")
    p.add_argument("--filter", default=None)
    p.add_argument("--limit", type=int, default=50)
    p.set_defaults(func=cmd_list)

    p = sub.add_parser("stats", help="数据库总览 + 健康度指标")
    p.add_argument("--verbose", action="store_true", help="列出所有属性对不上的机制")
    p.set_defaults(func=cmd_stats)

    p = sub.add_parser("code", help="看一个机制的方法实现与事件绑定（'怎么运作'）")
    p.add_argument("part")
    p.add_argument("--kind", default=None)
    p.add_argument("--method", default=None, help="只看名字含此串的方法")
    p.add_argument("--body", type=int, default=40, help="每个方法体显示多少行")
    p.set_defaults(func=cmd_code)

    p = sub.add_parser("events", help="事件绑定：列出或反查")
    p.add_argument("what", nargs="?", default=None,
                   help="事件名（省略则列出最常被监听的事件）")
    p.add_argument("--limit", type=int, default=40)
    p.set_defaults(func=cmd_events)

    p = sub.add_parser("impl", help="在方法体里搜代码（找实现逻辑）")
    p.add_argument("term")
    p.add_argument("--limit", type=int, default=12)
    p.set_defaults(func=cmd_impl)

    p = sub.add_parser("skills", help="技能树 → power → C# 实现（含前置）")
    p.add_argument("skill", nargs="?", default=None, help="技能名；省略则列出所有技能树")
    p.set_defaults(func=cmd_skills)

    args = ap.parse_args()
    sys.exit(args.func(args))


if __name__ == "__main__":
    main()
