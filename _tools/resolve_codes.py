#!/usr/bin/env python3
"""Resolve polymorphic 'type code' values seen in dump_metadata output (et:0x..)."""
import sys

sys.path.insert(0, r"D:\caves of qud 模组制作\_tools")
import dump_metadata as dm

path = r"D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\Assembly-CSharp.dll"
data = open(path, "rb").read()
md_off, _, _ = dm.find_metadata_root(data)
md = dm.Metadata(data, md_off)

codes = [int(a, 16) for a in sys.argv[1:]]
for code in codes:
    tag = code & 0x07 if False else None
print("TypeRef count =", md.rows.get(0x01, 0), " TypeDef count =", md.rows.get(0x02, 0))

for code in codes:
    # polymorphic encoding: (ELEMENT_TYPE_CLASS << 5) | tag
    et = code >> 5
    tag = code & 0x1F
    name = "<unknown>"
    try:
        if et == 0x13:  # CLASS -> TypeDefOrRefOrSpec coded, 2 bits
            t = tag & 0x03
            ridx = tag >> 2
            name = dm.SigParser(md, b"")._resolve_code(tag)
        elif et in (0x11, 0x12):
            name = dm.ELEMENT_TYPES.get(et, "?")
        else:
            name = dm.ELEMENT_TYPES.get(et, "et" + hex(et))
    except Exception as exc:
        name = "<err %s>" % exc
    print("0x%02X -> et=0x%02X tag=%d => %s" % (code, et, tag, name))
