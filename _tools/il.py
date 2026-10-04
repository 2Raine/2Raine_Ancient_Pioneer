#!/usr/bin/env python3
"""
A correct-enough IL disassembler for reading one method's logic.

An earlier attempt desynced because it treated every opcode as one byte. This one handles the
0xFE-prefixed two-byte opcodes before anything else, which is what was breaking alignment.

Usage: il.py <TypeName> <MethodName>
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dump_metadata as dm

DLL = (r"D:\SteamLibrary\steamapps\common\Caves of Qud"
       r"\CoQ_Data\Managed\Assembly-CSharp.dll")

# opcode -> (name, operand kind, size). kind: ''|i1|i2|i4|i8|f4|f8|tok|br_s|br|switch
OPS = {
    0x00: ("nop", "", 0), 0x01: ("break", "", 0), 0x02: ("ldarg.0", "", 0),
    0x03: ("ldarg.1", "", 0), 0x04: ("ldarg.2", "", 0), 0x05: ("ldarg.3", "", 0),
    0x06: ("ldloc.0", "", 0), 0x07: ("ldloc.1", "", 0), 0x08: ("ldloc.2", "", 0),
    0x09: ("ldloc.3", "", 0), 0x0A: ("stloc.0", "", 0), 0x0B: ("stloc.1", "", 0),
    0x0C: ("stloc.2", "", 0), 0x0D: ("stloc.3", "", 0),
    0x0E: ("ldarg.s", "i1", 1), 0x0F: ("ldarga.s", "i1", 1), 0x10: ("starg.s", "i1", 1),
    0x11: ("ldloc.s", "i1", 1), 0x12: ("ldloca.s", "i1", 1), 0x13: ("stloc.s", "i1", 1),
    0x14: ("ldnull", "", 0),
    0x15: ("ldc.i4.m1", "", 0), 0x16: ("ldc.i4.0", "", 0), 0x17: ("ldc.i4.1", "", 0),
    0x18: ("ldc.i4.2", "", 0), 0x19: ("ldc.i4.3", "", 0), 0x1A: ("ldc.i4.4", "", 0),
    0x1B: ("ldc.i4.5", "", 0), 0x1C: ("ldc.i4.6", "", 0), 0x1D: ("ldc.i4.7", "", 0),
    0x1E: ("ldc.i4.8", "", 0),
    0x1F: ("ldc.i4.s", "i1", 1), 0x20: ("ldc.i4", "i4", 4), 0x21: ("ldc.i8", "i8", 8),
    0x22: ("ldc.r4", "f4", 4), 0x23: ("ldc.r8", "f8", 8),
    0x25: ("dup", "", 0), 0x26: ("pop", "", 0), 0x27: ("jmp", "tok", 4),
    0x28: ("call", "tok", 4), 0x29: ("calli", "tok", 4), 0x2A: ("ret", "", 0),
    0x2B: ("br.s", "br_s", 1), 0x2C: ("brfalse.s", "br_s", 1), 0x2D: ("brtrue.s", "br_s", 1),
    0x2E: ("beq.s", "br_s", 1), 0x2F: ("bge.s", "br_s", 1), 0x30: ("bgt.s", "br_s", 1),
    0x31: ("ble.s", "br_s", 1), 0x32: ("blt.s", "br_s", 1), 0x33: ("bne.un.s", "br_s", 1),
    0x34: ("bge.un.s", "br_s", 1), 0x35: ("bgt.un.s", "br_s", 1), 0x36: ("ble.un.s", "br_s", 1),
    0x37: ("blt.un.s", "br_s", 1),
    0x38: ("br", "br", 4), 0x39: ("brfalse", "br", 4), 0x3A: ("brtrue", "br", 4),
    0x3B: ("beq", "br", 4), 0x3C: ("bge", "br", 4), 0x3D: ("bgt", "br", 4),
    0x3E: ("ble", "br", 4), 0x3F: ("blt", "br", 4), 0x40: ("bne.un", "br", 4),
    0x41: ("bge.un", "br", 4), 0x42: ("bgt.un", "br", 4), 0x43: ("ble.un", "br", 4),
    0x44: ("blt.un", "br", 4), 0x45: ("switch", "switch", 0),
    0x46: ("ldind.i1", "", 0), 0x47: ("ldind.u1", "", 0), 0x48: ("ldind.i2", "", 0),
    0x49: ("ldind.u2", "", 0), 0x4A: ("ldind.i4", "", 0), 0x4B: ("ldind.i8", "", 0),
    0x4C: ("ldind.r4", "", 0), 0x4D: ("ldind.r8", "", 0), 0x4E: ("ldind.ref", "", 0),
    0x4F: ("stind.ref", "", 0), 0x50: ("stind.i1", "", 0), 0x51: ("stind.i2", "", 0),
    0x52: ("stind.i4", "", 0), 0x53: ("stind.i8", "", 0), 0x54: ("stind.r4", "", 0),
    0x55: ("stind.r8", "", 0),
    0x58: ("add", "", 0), 0x59: ("sub", "", 0), 0x5A: ("mul", "", 0), 0x5B: ("div", "", 0),
    0x5C: ("div.un", "", 0), 0x5D: ("rem", "", 0), 0x5E: ("rem.un", "", 0),
    0x5F: ("and", "", 0), 0x60: ("or", "", 0), 0x61: ("xor", "", 0), 0x62: ("shl", "", 0),
    0x63: ("shr", "", 0), 0x64: ("shr.un", "", 0), 0x65: ("neg", "", 0), 0x66: ("not", "", 0),
    0x67: ("conv.i1", "", 0), 0x68: ("conv.i2", "", 0), 0x69: ("conv.i4", "", 0),
    0x6A: ("conv.i8", "", 0), 0x6B: ("conv.r4", "", 0), 0x6C: ("conv.r8", "", 0),
    0x6D: ("conv.u4", "", 0), 0x6E: ("conv.u8", "", 0),
    0x6F: ("callvirt", "tok", 4), 0x70: ("cpobj", "tok", 4), 0x71: ("ldobj", "tok", 4),
    0x72: ("ldstr", "tok", 4), 0x73: ("newobj", "tok", 4), 0x74: ("castclass", "tok", 4),
    0x75: ("isinst", "tok", 4), 0x76: ("conv.r.un", "", 0),
    0x79: ("unbox", "tok", 4), 0x7A: ("throw", "", 0),
    0x7B: ("ldfld", "tok", 4), 0x7C: ("ldflda", "tok", 4), 0x7D: ("stfld", "tok", 4),
    0x7E: ("ldsfld", "tok", 4), 0x7F: ("ldsflda", "tok", 4), 0x80: ("stsfld", "tok", 4),
    0x81: ("stobj", "tok", 4),
    0x8C: ("box", "tok", 4), 0x8D: ("newarr", "tok", 4), 0x8E: ("ldlen", "", 0),
    0x8F: ("ldelema", "tok", 4), 0x90: ("ldelem.i1", "", 0), 0x91: ("ldelem.u1", "", 0),
    0x92: ("ldelem.i2", "", 0), 0x93: ("ldelem.u2", "", 0), 0x94: ("ldelem.i4", "", 0),
    0x95: ("ldelem.u4", "", 0), 0x96: ("ldelem.i8", "", 0), 0x97: ("ldelem.r4", "", 0),
    0x98: ("ldelem.r8", "", 0), 0x99: ("ldelem.ref", "", 0),
    0x9A: ("stelem.i", "", 0), 0x9B: ("stelem.i1", "", 0), 0x9C: ("stelem.i2", "", 0),
    0x9D: ("stelem.i4", "", 0), 0x9E: ("stelem.i8", "", 0), 0x9F: ("stelem.r4", "", 0),
    0xA0: ("stelem.r8", "", 0), 0xA1: ("stelem.ref", "", 0), 0xA2: ("ldelem", "tok", 4),
    0xA3: ("stelem", "tok", 4), 0xA4: ("unbox.any", "tok", 4),
    0xA5: ("ldtoken", "tok", 4),
    0xB3: ("conv.ovf.i1", "", 0), 0xB4: ("conv.ovf.u1", "", 0), 0xB5: ("conv.ovf.i2", "", 0),
    0xB6: ("conv.ovf.u2", "", 0), 0xB7: ("conv.ovf.i4", "", 0), 0xB8: ("conv.ovf.u4", "", 0),
    0xB9: ("conv.ovf.i8", "", 0), 0xBA: ("conv.ovf.u8", "", 0),
    0xC2: ("refanyval", "tok", 4), 0xC3: ("ckfinite", "", 0), 0xC6: ("mkrefany", "tok", 4),
    0xD0: ("ldtoken", "tok", 4), 0xD1: ("conv.u2", "", 0), 0xD2: ("conv.u1", "", 0),
    0xD3: ("conv.i", "", 0), 0xD4: ("conv.ovf.i", "", 0), 0xD5: ("conv.ovf.u", "", 0),
    0xD6: ("add.ovf", "", 0), 0xD7: ("add.ovf.un", "", 0), 0xD8: ("mul.ovf", "", 0),
    0xD9: ("mul.ovf.un", "", 0), 0xDA: ("sub.ovf", "", 0), 0xDB: ("sub.ovf.un", "", 0),
    0xDC: ("endfinally", "", 0), 0xDD: ("leave", "br", 4), 0xDE: ("leave.s", "br_s", 1),
    0xDF: ("stind.i", "", 0), 0xE0: ("conv.u", "", 0),
    # two-byte
    0xFE00: ("arglist", "", 0), 0xFE01: ("ceq", "", 0), 0xFE02: ("cgt", "", 0),
    0xFE03: ("cgt.un", "", 0), 0xFE04: ("clt", "", 0), 0xFE05: ("clt.un", "", 0),
    0xFE06: ("ldftn", "tok", 4), 0xFE07: ("ldvirtftn", "tok", 4),
    0xFE09: ("ldarg", "i2", 2), 0xFE0A: ("ldarga", "i2", 2), 0xFE0B: ("starg", "i2", 2),
    0xFE0C: ("ldloc", "i2", 2), 0xFE0D: ("ldloca", "i2", 2), 0xFE0E: ("stloc", "i2", 2),
    0xFE0F: ("localloc", "", 0), 0xFE11: ("endfilter", "", 0),
    0xFE12: ("unaligned.", "i1", 1), 0xFE13: ("volatile.", "", 0),
    0xFE14: ("tail.", "", 0), 0xFE15: ("initobj", "tok", 4),
    0xFE16: ("constrained.", "tok", 4), 0xFE17: ("cpblk", "", 0), 0xFE18: ("initblk", "", 0),
    0xFE1A: ("rethrow", "", 0), 0xFE1C: ("sizeof", "tok", 4),
    0xFE1D: ("refanytype", "", 0), 0xFE1E: ("readonly.", "", 0),
}


def read_body(data, rva_to_off, rva):
    off = rva_to_off(rva)
    b = dm.Blob(data, off)
    first = b.u8()
    if (first & 0x03) == 0x02:
        return b.bytes(first >> 2)
    b = dm.Blob(data, off)
    b.u16(); b.u16()
    size = b.u32()
    b.u32()
    return b.bytes(size)


def tok(md, t):
    table = t >> 24
    ridx = t & 0xFFFFFF
    try:
        if table == 0x0A:
            r = md.row(0x0A, ridx)
            return "%s::%s" % (md.memberref_class(r["Class"]), r["Name"])
        if table == 0x06:
            return md.method_name(ridx)
        if table == 0x04:
            return md.field_name(ridx)
        if table == 0x70:
            return repr(md.user_string(ridx))
        if table in (0x01, 0x02):
            return md.type_name_of(table, ridx)
    except Exception:
        pass
    return "tok%02X#%d" % (table, ridx)


def main():
    tname, mname = sys.argv[1], sys.argv[2]
    data = open(DLL, "rb").read()
    md_off, sections, rva_to_off = dm.find_metadata_root(data)
    md = dm.Metadata(data, md_off)

    for ti in range(1, md.rows.get(2, 0) + 1):
        tr = md.row(2, ti)
        if tr["Name"] != tname:
            continue
        ms = tr["MethodList"]
        nx = (md.row(2, ti + 1)["MethodList"]
              if ti < md.rows.get(2, 0) else md.rows.get(6, 0) + 1)
        for mi in range(ms, min(nx, md.rows.get(6, 0) + 1)):
            mr = md.row(6, mi)
            if mr["Name"] != mname or mr["RVA"] == 0:
                continue
            try:
                ret, ps, fl = dm.SigParser(md, mr["Signature"]).method_sig()
            except Exception:
                ret, ps = "?", []
            code = read_body(data, rva_to_off, mr["RVA"])
            print("=== %s.%s :: %s(%s)   %d bytes" % (tname, mname, ret, ", ".join(ps), len(code)))
            i = 0
            while i < len(code):
                start = i
                b0 = code[i]; i += 1
                key = b0
                if b0 == 0xFE and i < len(code):
                    key = 0xFE00 | code[i]; i += 1
                info = OPS.get(key)
                if info is None:
                    print("  IL_%04X  <unknown %02X>" % (start, key))
                    continue
                name, kind, size = info
                val = ""
                if kind == "i1":
                    val = str(struct.unpack_from("<b", code, i)[0]); i += 1
                elif kind == "i2":
                    val = str(struct.unpack_from("<h", code, i)[0]); i += 2
                elif kind == "i4":
                    val = str(struct.unpack_from("<i", code, i)[0]); i += 4
                elif kind == "i8":
                    val = str(struct.unpack_from("<q", code, i)[0]); i += 8
                elif kind == "f4":
                    val = "float %g" % struct.unpack_from("<f", code, i)[0]; i += 4
                elif kind == "f8":
                    val = "float %g" % struct.unpack_from("<d", code, i)[0]; i += 8
                elif kind == "tok":
                    val = tok(md, struct.unpack_from("<I", code, i)[0]); i += 4
                elif kind == "br_s":
                    d = struct.unpack_from("<b", code, i)[0]; i += 1
                    val = "-> IL_%04X" % (i + d)
                elif kind == "br":
                    d = struct.unpack_from("<i", code, i)[0]; i += 4
                    val = "-> IL_%04X" % (i + d)
                elif kind == "switch":
                    n = struct.unpack_from("<I", code, i)[0]; i += 4
                    base = i + 4 * n
                    tgts = []
                    for _ in range(n):
                        tgts.append("IL_%04X" % (base + struct.unpack_from("<i", code, i)[0]))
                        i += 4
                    val = " ".join(tgts)
                print("  IL_%04X  %-16s %s" % (start, name, val))


if __name__ == "__main__":
    main()
