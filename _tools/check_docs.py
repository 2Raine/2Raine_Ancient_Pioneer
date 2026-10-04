import io, os, sqlite3

ws = r"D:\caves of qud 模组制作"
A = io.open(os.path.join(ws, "AGENTS.md"), encoding="utf-8").read()
B = io.open(os.path.join(ws, "Qud机制数据库_使用说明.md"), encoding="utf-8").read()
m = sqlite3.connect(os.path.join(ws, "qud_db", "qud_mechanisms.sqlite"))
b = sqlite3.connect(os.path.join(ws, "qud_db", "qud_blocks.sqlite"))
q = lambda c, s: c.execute(s).fetchone()[0]

real = {
    "2,432": q(m, "SELECT COUNT(*) FROM mechanisms"),
    "27,302": q(m, "SELECT COUNT(*) FROM mech_attr"),
    "47,325": q(m, "SELECT COUNT(*) FROM mech_method"),
    "10,728": q(m, "SELECT COUNT(*) FROM mech_event"),
    "1,041": q(m, "SELECT COUNT(DISTINCT event) FROM mech_event"),
    "123": q(m, "SELECT COUNT(*) FROM power_class"),
    "5,727": q(b, "SELECT COUNT(*) FROM blocks"),
    "16,252": q(b, "SELECT COUNT(*) FROM part_usage"),
    "1,705": q(m, "SELECT COUNT(*) FROM mech_attr WHERE safety='xml'"),
    "21,220": q(m, "SELECT COUNT(*) FROM mech_attr WHERE safety='config'"),
    "40": 40,   # empty types, hard-coded claim
}

print("%-9s %-8s %-9s %-10s %s" % ("数字", "数据库", "AGENTS.md", "使用说明", "判定"))
bad = []
for s, v in real.items():
    ia, ib = (s in A), (s in B)
    ok = int(s.replace(",", "")) == v
    if not ok:
        bad.append((s, "与库不符"))
    print("%-9s %-8d %-9s %-10s %s"
          % (s, v, "有" if ia else "缺", "有" if ib else "缺",
             "OK" if ok else "与库不符"))

# Policy: counts must be SINGLE-SOURCED.  Keeping the same figure in two files is
# how they drift apart (a first cross-check already found one mismatch), so a
# count appearing in BOTH files is reported as a violation, not as good news.
dupes = [s for s in real if s in A and s in B]
print()
print("同时写进两份文档的计数:", dupes if dupes else "无（符合单一来源原则）")

print()
print("AGENTS.md  : %d 字节 / 65536 预算 (%.1f%%)" % (len(A.encode()), 100 * len(A.encode()) / 65536))
print("使用说明   : %d 字节" % len(B.encode()))
tok = "& $py $q"
print()
print("AGENTS.md 中命令示例数 :", A.count(tok))
print("使用说明中命令示例数   :", B.count(tok))
print("AGENTS.md 是否指向使用说明:", "是" if "Qud机制数据库_使用说明.md" in A else "否")
print("与数据库不符的计数     :", bad if bad else "无")
