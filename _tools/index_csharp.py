#!/usr/bin/env python3
"""Index the decompiled C# source to answer "what does this part DO?".

The mechanism DB so far answers "what can I configure?" -- fields, types,
defaults, real base-game values.  This adds the other half: the behaviour.

Two things are extracted per part class:

1. METHODS -- name, signature, and body text.  That is where the arithmetic
   lives ("penetration = BasePenetration + strength modifier", "cooldown = 10").
   Bodies are stored so the query tool can print them; we do not attempt to
   interpret them, because a wrong summary is worse than the real code.

2. EVENTS -- which events the part registers for and handles:
     * Registrar.Register("AwardXP")            -> string event
     * Registrar.Register(ZoneActivatedEvent.ID) -> min-event
     * Object.RegisterPartEvent(this, "X")       -> legacy string event
     * WantEvent(...) || ID == SomeEvent.ID      -> handled min-event
     * HandleEvent(SomeEvent E)                  -> handled min-event
   This is the binding that was missing: it tells you *when* your code runs,
   which is the difference between a mechanism and a decoration.

Input : a directory of .cs files produced by `ilspycmd -p -o <dir>`
Output: csharp.jsonl + tables (mech_method, mech_event) in the mechanism DB
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sqlite3
import sys
from collections import Counter, defaultdict

# --------------------------------------------------------------------------- #
# lightweight C# scanning
# --------------------------------------------------------------------------- #
#
# We are not writing a C# parser.  We use brace-depth tracking to find class
# and method boundaries, which is reliable on decompiler output (it is
# consistently formatted) and cheap.  Regexes then pull out the payload.

RE_NAMESPACE = re.compile(r"^namespace\s+([\w\.]+)")
RE_TYPE = re.compile(
    r"^(?P<indent>\s*)(?:\[[^\]]*\]\s*)*"
    r"(?P<attrs>(?:\[[^\]]*\]\s*)*)"
    r"(?P<mods>(?:public|internal|private|protected|static|sealed|abstract|partial|\s)+)"
    r"(?P<kind>class|struct|interface|enum)\s+(?P<name>[\w`<>]+)")
RE_METHOD = re.compile(
    r"^(?P<indent>\s*)(?P<attrs>(?:\[[^\]]*\]\s*)*)"
    r"(?P<mods>(?:public|private|protected|internal|static|virtual|override|sealed|abstract|async|extern|unsafe|new|\s)+)"
    r"(?P<ret>[\w\.<>\[\]`,\?]+)\s+"
    r"(?P<name>[\w<>`\.]+)\s*\((?P<args>[^)]*)\)\s*$")

RE_REGISTRAR = re.compile(r"\.?Register(?:PartEvent)?\s*\(\s*(?:this\s*,\s*)?\"([^\"]+)\"")
RE_REGISTRAR_MINEVENT = re.compile(r"\.?Register\s*\(\s*([A-Za-z_]\w*Event)\.ID")
RE_WANT_ID = re.compile(r"ID\s*==\s*([A-Za-z_]\w*Event)\.ID")
RE_HANDLE = re.compile(r"\bHandleEvent\s*\(\s*([A-Za-z_]\w*Event)\s+\w+\s*\)")
RE_FIREEVENT_ID = re.compile(r"E\.ID\s*==\s*\"([^\"]+)\"")

# Expression-bodied and get-only properties carry real logic too.  Without
# these, a class whose entire implementation is `public override int Priority
# => int.MinValue;` (e.g. Cudgel_ChargingStrike) indexes as having no members
# at all, which reads as "empty class" instead of "one override".
RE_PROP_EXPR = re.compile(
    r"^(?P<indent>\s*)(?P<attrs>(?:\[[^\]]*\]\s*)*)"
    r"(?P<mods>(?:public|private|protected|internal|static|virtual|override|"
    r"sealed|abstract|new|\s)+)"
    r"(?P<type>[\w\.<>\[\]`,\?]+)\s+"
    r"(?P<name>[\w]+)\s*=>\s*(?P<body>.+?);\s*$")
RE_PROP_GET = re.compile(
    r"^(?P<indent>\s*)(?P<attrs>(?:\[[^\]]*\]\s*)*)"
    r"(?P<mods>(?:public|private|protected|internal|static|virtual|override|"
    r"sealed|abstract|new|\s)+)"
    r"(?P<type>[\w\.<>\[\]`,\?]+)\s+"
    r"(?P<name>[\w]+)\s*$")

# If this ends up in the "return type" slot the signature had no return type,
# which means it was a constructor.  Used to normalise those records.
MODIFIER_WORDS = {
    "public", "private", "protected", "internal", "static", "virtual",
    "override", "sealed", "abstract", "async", "extern", "unsafe", "new",
    "partial", "readonly", "const",
}


def harvest_events(frame, line):
    """Record any event bindings visible on one line of source.

    Called for every line *and* explicitly for member declaration lines, because
    a declaration is often the only place its binding appears -- an override
    written as `public override bool HandleEvent(AIAfterMissileEvent E)`
    mentions the event nowhere else.
    """
    if frame is None:
        return
    for rx, key in ((RE_REGISTRAR, "events_register"),
                    (RE_REGISTRAR_MINEVENT, "events_register"),
                    (RE_WANT_ID, "events_want"),
                    (RE_HANDLE, "events_handle"),
                    (RE_FIREEVENT_ID, "fire_ids")):
        for hit in rx.findall(line):
            if hit not in frame[key]:
                frame[key].append(hit)


def _close_frames(stack, out, depth):
    """Pop every type frame whose body has ended."""
    while stack and stack[-1]["body_depth"] is not None and depth < stack[-1]["body_depth"]:
        t = stack.pop()
        out.append(t)


def scan_file(path):
    """Return per-type records with methods, properties and event bindings.

    Brace-depth walk over a stack of type frames.  Two bugs the earlier
    single-frame version had, both of which silently lost data:
      * depth must be updated on *every* line, including the one that opens a
        type or method -- an early `continue` that skipped it left depth one
        short, so no method ever matched its gate;
      * a nested class must not end the *outer* class.  On closing an inner
        type we have to pop back to the enclosing frame and keep collecting
        there, otherwise the rest of the outer class is discarded (that is how
        ProceduralCookingTriggeredAction lost its ~100 HandleEvent methods to
        its nested EventBinder class).
    """
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()

    namespace = ""
    depth = 0
    stack = []          # enclosing type frames
    out = []            # finished type records
    cur_method = None
    method_depth = None

    def frame():
        return stack[-1] if stack else None

    for lineno, raw in enumerate(lines, 1):
        line = raw.rstrip("\n")
        stripped = line.strip()
        opened = line.count("{")
        closed = line.count("}")
        if not stripped or stripped.startswith("//"):
            depth += opened - closed
            _close_frames(stack, out, depth)
            continue

        m = RE_NAMESPACE.match(stripped)
        if m:
            namespace = m.group(1)

        f = frame()
        at_type_body = f is not None and depth == f["body_depth"]

        # ---- members of the current (innermost) type --------------------- #
        if at_type_body and cur_method is None:
            mm = RE_METHOD.match(line)
            if mm:
                cur_method = {
                    "name": mm.group("name"), "ret": mm.group("ret"),
                    "args": " ".join(mm.group("args").split()),
                    "attrs": mm.group("attrs").strip(),
                    "line": lineno, "body": [], "kind": "method",
                    "decl_line": line,
                }
                method_depth = depth
                if "{" not in line and stripped.endswith(";"):
                    # abstract / interface / extern: no body to collect
                    f["methods"].append(cur_method)
                    cur_method = None
                    method_depth = None
                harvest_events(f, line)
                depth += opened - closed
                _close_frames(stack, out, depth)
                continue
            pm = RE_PROP_EXPR.match(line)
            if pm:
                f["methods"].append({
                    "name": pm.group("name"), "ret": pm.group("type"),
                    "args": "", "attrs": pm.group("attrs").strip(),
                    "line": lineno, "body": pm.group("body").strip(),
                    "kind": "property",
                })
                harvest_events(f, line)
                depth += opened - closed
                _close_frames(stack, out, depth)
                continue
            pg = RE_PROP_GET.match(stripped)
            if (pg and not stripped.endswith(";") and not stripped.endswith(")")
                    and "=" not in stripped
                    and pg.group("name") not in MODIFIER_WORDS):
                cur_method = {
                    "name": pg.group("name"), "ret": pg.group("type"),
                    "args": "", "attrs": pg.group("attrs").strip(),
                    "line": lineno, "body": [], "kind": "property",
                    "decl_line": line,
                }
                method_depth = depth
                harvest_events(f, line)
                depth += opened - closed
                _close_frames(stack, out, depth)
                continue

        # ---- type declaration -------------------------------------------- #
        tm = RE_TYPE.match(line)
        if tm:
            _flush_method(frame(), cur_method)
            cur_method = None
            method_depth = None
            nm = tm.group("name")
            stack.append({
                "namespace": namespace,
                # nested types are Outer+Inner, matching how the .NET metadata
                # names them (and how the mechanisms table keys them)
                "name": nm,
                "full_simple": (stack[-1]["full_simple"] + "+" + nm) if stack else nm,
                "kind": tm.group("kind"),
                "mods": " ".join(tm.group("mods").split()),
                "attrs": tm.group("attrs").strip(),
                "line": lineno,
                "file": path,
                "methods": [],
                # ilspycmd puts the brace on the next line, so arm it below
                "body_depth": None,
                "end_line": None,
                "events_register": [], "events_want": [],
                "events_handle": [], "fire_ids": [],
            })
            depth += opened - closed
            if opened:
                stack[-1]["body_depth"] = depth
            continue

        # ---- harvest bodies and event bindings --------------------------- #
        f = frame()
        if f is not None:
            if cur_method is not None:
                cur_method["body"].append(line)
            harvest_events(f, line)
            # a frame whose brace never showed up on its own line
            if f["body_depth"] is None and opened:
                f["body_depth"] = depth + opened - closed

        depth += opened - closed

        if cur_method is not None and depth <= method_depth:
            _flush_method(frame(), cur_method)
            cur_method = None
            method_depth = None
        _close_frames(stack, out, depth)

    _flush_method(frame(), cur_method)
    while stack:
        t = stack.pop()
        t["end_line"] = len(lines)
        out.append(t)
    return out


def _close_frames(stack, out, depth):
    """Pop every type frame whose body has ended."""
    while stack and stack[-1]["body_depth"] is not None and depth < stack[-1]["body_depth"]:
        t = stack.pop()
        t["end_line"] = t.get("end_line") or 0
        out.append(t)


def _flush_method(cur, meth):
    if cur is None or meth is None:
        return
    # NOTE: the declaration line is NOT re-inserted into the body here -- it is
    # already the first element (added by the per-line loop) and its event
    # bindings are harvested separately by harvest_events().
    # A constructor has no return type, so the signature regex lands the
    # access modifier in `ret` and the class name in `name`:
    #   `public AccelerativeTeleporter()` -> ret='public', name='AccelerativeTeleporter'
    # Detect that and reshuffle into (ret='void', name='.ctor') so the sheet
    # does not read "return type public".
    if meth["ret"].strip() in MODIFIER_WORDS:
        meth["args"] = ((meth["name"] + "(" + meth["args"] + ")").strip()
                        if meth["args"] else meth["name"] + "()")
        meth["name"] = ".ctor"
        meth["ret"] = "void"
    body = "\n".join(meth["body"])
    # strip the outermost braces so the stored body is just the statements
    body = body.strip()
    if body.startswith("{"):
        body = body[1:]
    if body.endswith("}"):
        body = body[:-1]
    meth["body"] = body.strip("\n")
    cur["methods"].append(meth)


def _maybe_close(results, stack, depth):
    return


# --------------------------------------------------------------------------- #
# storage
# --------------------------------------------------------------------------- #

DDL = """
CREATE TABLE IF NOT EXISTS mech_method (
    id     INTEGER PRIMARY KEY,
    full   TEXT NOT NULL,      -- declaring type, fully qualified
    part   TEXT NOT NULL,
    name   TEXT NOT NULL,
    ret    TEXT,
    args   TEXT,
    attrs  TEXT,
    line   INTEGER,
    file   TEXT,
    body   TEXT,
    kind   TEXT                -- 'method' | 'property'
);
CREATE INDEX IF NOT EXISTS ix_mm_full ON mech_method(full);
CREATE INDEX IF NOT EXISTS ix_mm_name ON mech_method(name);
CREATE INDEX IF NOT EXISTS ix_mm_kind ON mech_method(kind);

CREATE TABLE IF NOT EXISTS mech_event (
    id      INTEGER PRIMARY KEY,
    full    TEXT NOT NULL,
    part    TEXT NOT NULL,
    event   TEXT NOT NULL,
    role    TEXT NOT NULL,     -- register | want | handle | fire
    is_min  INTEGER NOT NULL   -- 1 when it is a min-event class, 0 for string event
);
CREATE INDEX IF NOT EXISTS ix_me_full  ON mech_event(full);
CREATE INDEX IF NOT EXISTS ix_me_event ON mech_event(event);
CREATE INDEX IF NOT EXISTS ix_me_part  ON mech_event(part);
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True, help="decompiled .cs directory")
    ap.add_argument("--db", required=True, help="qud_mechanisms.sqlite")
    ap.add_argument("--out", required=True)
    ap.add_argument("--max-body", type=int, default=20000,
                    help="skip bodies larger than this many chars")
    args = ap.parse_args()

    files = []
    for root, _d, fs in os.walk(args.src):
        for f in fs:
            if f.endswith(".cs"):
                files.append(os.path.join(root, f))
    files.sort()
    print("扫描 %d 个 .cs 文件 ..." % len(files))

    types = []
    for i, p in enumerate(files):
        try:
            types.extend(scan_file(p))
        except Exception as exc:
            print("  !! %s: %s" % (p, exc))
        if (i + 1) % 1000 == 0:
            print("    %d/%d" % (i + 1, len(files)))

    print("解析到类型/嵌套类型: %d" % len(types))

    # index by fully-qualified name; nested types use Outer+Inner, matching the
    # convention the mechanisms table and the .NET metadata both use
    by_full = {}
    for t in types:
        simple = t.get("full_simple") or t["name"]
        full = (t["namespace"] + "." if t["namespace"] else "") + simple
        # outermost declaration wins for a given name
        by_full.setdefault(full, t)

    con = sqlite3.connect(args.db)
    cur = con.cursor()
    # Idempotent: without this, re-running appends duplicate rows and every
    # count silently inflates (the first run's 9,437 event rows were still
    # present alongside the second run's, which is why the DB disagreed with
    # the run summary).
    cur.execute("DROP TABLE IF EXISTS mech_method")
    cur.execute("DROP TABLE IF EXISTS mech_event")
    cur.executescript(DDL)

    # only keep rows for types we actually track as mechanisms, plus any type
    # that registers/handles events (those are useful regardless)
    known = {r[0] for r in cur.execute("SELECT full FROM mechanisms")}
    print("机制库中的类型: %d" % len(known))

    n_meth = n_ev = 0
    jl = os.path.join(args.out, "csharp.jsonl")
    with open(jl, "w", encoding="utf-8") as fh:
        for full, t in sorted(by_full.items()):
            part = t["name"]
            rec = {
                "full": full, "part": part, "kind": t["kind"],
                "mods": t["mods"], "attrs": t["attrs"],
                "file": os.path.relpath(t["file"], args.src).replace("\\", "/"),
                "line": t["line"], "namespace": t["namespace"],
                "methods": [], "events": [],
            }
            for meth in t["methods"]:
                body = meth["body"]
                if len(body) > args.max_body:
                    body = body[:args.max_body] + "\n/* ...truncated... */"
                rec["methods"].append({
                    "name": meth["name"], "ret": meth["ret"],
                    "args": meth["args"], "attrs": meth["attrs"],
                    "line": meth["line"], "body": body,
                    "kind": meth.get("kind", "method"),
                })
                cur.execute(
                    "INSERT INTO mech_method(full,part,name,ret,args,attrs,line,file,body,kind)"
                    " VALUES(?,?,?,?,?,?,?,?,?,?)",
                    (full, part, meth["name"], meth["ret"], meth["args"],
                     meth["attrs"], meth["line"], rec["file"], body,
                     meth.get("kind", "method")))
                n_meth += 1
            for role, key in (("register", "events_register"),
                              ("want", "events_want"),
                              ("handle", "events_handle"),
                              ("fire", "fire_ids")):
                for ev in t[key]:
                    is_min = 1 if ev.endswith("Event") and not ev.startswith("Command") else 0
                    rec["events"].append({"event": ev, "role": role, "is_min": is_min})
                    cur.execute(
                        "INSERT INTO mech_event(full,part,event,role,is_min)"
                        " VALUES(?,?,?,?,?)", (full, part, ev, role, is_min))
                    n_ev += 1
            fh.write(json.dumps(rec, ensure_ascii=False) + "\n")
    con.commit()

    print("\n=== 索引结果 ===")
    print("方法总数      : %d" % n_meth)
    print("事件绑定总数  : %d" % n_ev)
    print("唯一事件名    : %d" % cur.execute(
        "SELECT COUNT(DISTINCT event) FROM mech_event").fetchone()[0])
    print("有方法体的机制: %d" % cur.execute(
        "SELECT COUNT(DISTINCT full) FROM mech_method WHERE full IN"
        " (SELECT full FROM mechanisms)").fetchone()[0])

    print("\n=== 被最多机制监听的事件 TOP 20 ===")
    for r in cur.execute(
            "SELECT event, COUNT(DISTINCT full) n FROM mech_event"
            " WHERE role IN ('register','want','handle')"
            " GROUP BY event ORDER BY n DESC LIMIT 20"):
        print("  %-42s %4d 个机制" % (r[0], r[1]))

    print("\n输出: %s" % jl)
    con.close()


if __name__ == "__main__":
    main()
