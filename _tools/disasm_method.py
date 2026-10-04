#!/usr/bin/env python3
"""
Disassembles one method body from an assembly, by reading the CLI MethodDef RVA and
walking the IL. Enough of an opcode table to follow calls, constants, branches and
field access -- which is what is needed to read a rule like "how many arcs".

Usage: disasm_method.py <dll> <TypeName> <MethodName> [signature-substring]
"""
import struct
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dump_metadata as dm

# ---- operand shapes per opcode (only the ones that matter here)
OPERANDS = {
    0x00: 0, 0x01: 0, 0x02: 0, 0x03: 0, 0x04: 0, 0x05: 0, 0x06: 0, 0x07: 0,
    0x08: 0, 0x09: 0, 0x0A: 0, 0x0B: 0, 0x0C: 0, 0x0D: 0, 0x0E: 0, 0x0F: 0,
    0x10: 0, 0x11: 0, 0x12: 0, 0x13: 0, 0x14: 0, 0x15: 0, 0x16: 0, 0x17: 0,
    0x18: 0, 0x19: 0, 0x1A: 0, 0x1B: 0, 0x1C: 0, 0x1D: 0, 0x1E: 0, 0x1F: 0,
    0x20: 4, 0x21: 8, 0x22: 4, 0x23: 2, 0x24: 1, 0x25: 0, 0x26: 0, 0x27: 0,
    0x28: 4, 0x29: 1, 0x2A: 0, 0x2B: 1, 0x2C: 1, 0x2D: 4, 0x2E: 4, 0x2F: 4,
    0x30: 4, 0x31: 4, 0x32: 4, 0x33: 4, 0x34: 4, 0x35: 4, 0x36: 4, 0x37: 4,
    0x38: 4, 0x39: 4, 0x3A: 4, 0x3B: 4, 0x3C: 4, 0x3D: 4, 0x3E: 4, 0x3F: 4,
    0x40: 4, 0x41: 4, 0x42: 4, 0x43: 4, 0x44: 4, 0x45: 4, 0x46: 0, 0x47: 0,
    0x48: 4, 0x49: 4, 0x4A: 4, 0x4B: 4, 0x4C: 4, 0x4D: 4, 0x4E: 4, 0x4F: 4,
    0x50: 4, 0x51: 4, 0x52: 4, 0x53: 4, 0x54: 4, 0x55: 0, 0x56: 4, 0x57: 0,
    0x58: 0, 0x59: 0, 0x5A: 0, 0x5B: 0, 0x5C: 0, 0x5D: 0, 0x5E: 0, 0x5F: 4,
    0x60: 4, 0x61: 4, 0x62: 4, 0x63: 4, 0x64: 4, 0x65: 4, 0x66: 4, 0x67: 4,
    0x68: 4, 0x69: 4, 0x6A: 4, 0x6B: 4, 0x6C: 4, 0x6D: 4, 0x6E: 4, 0x6F: 4,
    0x70: 4, 0x71: 1, 0x72: 4, 0x73: 4, 0x74: 4, 0x75: 4, 0x76: 4, 0x77: 4,
    0x78: 4, 0x79: 4, 0x7A: 4, 0x7B: 4, 0x7C: 4, 0x7D: 4, 0x7E: 4, 0x7F: 4,
    0x80: 4, 0x81: 4, 0x82: 4, 0x83: 4, 0x84: 4, 0x85: 4, 0x86: 4, 0x87: 4,
    0x88: 4, 0x89: 4, 0x8A: 4, 0x8B: 4, 0x8C: 4, 0x8D: 4, 0x8E: 4, 0x8F: 4,
    0x90: 4, 0x91: 4, 0x92: 4, 0x93: 4, 0x94: 4, 0x95: 4, 0x96: 2, 0x97: 4,
    0x98: 4, 0x99: 4, 0x9A: 4, 0x9B: 4, 0x9C: 4, 0x9D: 4, 0x9E: 4, 0x9F: 4,
    0xA0: 4, 0xA1: 4, 0xA2: 4, 0xA3: 4, 0xA4: 4, 0xA5: 4, 0xA6: 4, 0xA7: 4,
    0xA8: 4, 0xA9: 4, 0xAA: 4, 0xAB: 4, 0xAC: 4, 0xAD: 4, 0xAE: 4, 0xAF: 4,
    0xB0: 4, 0xB1: 4, 0xB2: 4, 0xB3: 4, 0xB4: 4, 0xB5: 4, 0xB6: 4, 0xB7: 4,
    0xB8: 4, 0xB9: 4, 0xBA: 4, 0xBB: 4, 0xBC: 4, 0xBD: 4, 0xBE: 4, 0xBF: 4,
    0xC0: 4, 0xC1: 4, 0xC2: 4, 0xC3: 4, 0xC4: 4, 0xC5: 4, 0xC6: 4, 0xC7: 4,
    0xC8: 4, 0xC9: 4, 0xCA: 4, 0xCB: 4, 0xCC: 4, 0xCD: 4, 0xCE: 4, 0xCF: 4,
    0xD0: 4, 0xD1: 4, 0xD2: 4, 0xD3: 4, 0xD4: 4, 0xD5: 4, 0xD6: 4, 0xD7: 4,
    0xD8: 4, 0xD9: 4, 0xDA: 4, 0xDB: 4, 0xDC: 4, 0xDD: 4, 0xDE: 4, 0xDF: 4,
    0xE0: 0,
    # two-byte opcodes
    0xFE00: 2, 0xFE01: 2, 0xFE02: 2, 0xFE03: 2, 0xFE04: 2, 0xFE05: 2, 0xFE06: 4,
    0xFE07: 0, 0xFE08: 0, 0xFE09: 2, 0xFE0A: 2, 0xFE0B: 2, 0xFE0C: 2, 0xFE0D: 2,
    0xFE0E: 2, 0xFE0F: 0, 0xFE10: 0, 0xFE11: 0, 0xFE12: 0, 0xFE13: 0, 0xFE14: 0,
    0xFE15: 1, 0xFE16: 2, 0xFE17: 4, 0xFE18: 4, 0xFE19: 0, 0xFE1A: 0, 0xFE1B: 0,
    0xFE1C: 0, 0xFE1D: 0, 0xFE1E: 0,
}

NAMES = {
    0x00: "nop", 0x02: "brk", 0x06: "add", 0x07: "sub", 0x08: "mul", 0x09: "div",
    0x0A: "div.un", 0x0B: "rem", 0x0D: "shl", 0x0E: "shr", 0x0F: "shr.un",
    0x10: "and", 0x11: "or", 0x12: "xor", 0x13: "not", 0x14: "neg", 0x16: "not",
    0x17: "neg", 0x1A: "ldind", 0x20: "ldc.i4", 0x21: "ldc.i8", 0x22: "ldc.r4",
    0x23: "ldc.r8", 0x25: "dup", 0x26: "pop", 0x27: "jmp", 0x28: "call",
    0x29: "calli", 0x2A: "ret", 0x2B: "br.s", 0x2C: "brfalse.s", 0x2D: "brtrue.s",
    0x2E: "beq.s", 0x2F: "bge.s", 0x30: "bgt.s", 0x31: "ble.s", 0x32: "blt.s",
    0x33: "bne.un.s", 0x37: "bge.un.s", 0x38: "br", 0x39: "brfalse", 0x3A: "brtrue",
    0x3B: "beq", 0x3C: "bge", 0x3D: "bgt", 0x3E: "ble", 0x3F: "blt",
    0x40: "bne.un", 0x45: "switch", 0x46: "ldind.i1", 0x47: "ldind.u1",
    0x48: "ldind.i2", 0x49: "ldind.u2", 0x4A: "ldind.i4", 0x4B: "ldind.i8",
    0x4C: "ldind.r4", 0x4D: "ldind.r8", 0x4E: "ldind.ref", 0x4F: "stind.ref",
    0x50: "stind.i1", 0x51: "stind.i2", 0x52: "stind.i4", 0x53: "stind.i8",
    0x54: "stind.r4", 0x55: "stind.r8", 0x56: "add.ovf", 0x58: "add.ovf.un",
    0x59: "mul.ovf", 0x5A: "mul.ovf.un", 0x5B: "sub.ovf", 0x5C: "sub.ovf.un",
    0x5D: "endfinally", 0x5E: "leave", 0x5F: "leave.s", 0x60: "stind.i",
    0x61: "conv.i1", 0x62: "conv.i2", 0x63: "conv.i4", 0x64: "conv.i8",
    0x65: "conv.r4", 0x66: "conv.r8", 0x67: "conv.u4", 0x68: "conv.u8",
    0x69: "callvirt", 0x6A: "cpobj", 0x6B: "ldobj", 0x6C: "ldstr", 0x6D: "newobj",
    0x6E: "castclass", 0x6F: "isinst", 0x70: "conv.r.un", 0x71: "unbox",
    0x72: "throw", 0x73: "ldfld", 0x74: "ldflda", 0x75: "stfld", 0x76: "ldsfld",
    0x77: "ldsflda", 0x78: "stsfld", 0x79: "stobj", 0x7A: "conv.ovf.i1.un",
    0x7B: "conv.ovf.i2.un", 0x7C: "conv.ovf.i4.un", 0x7D: "conv.ovf.i8.un",
    0x7E: "conv.ovf.u1.un", 0x7F: "conv.ovf.u2.un", 0x80: "conv.ovf.u4.un",
    0x81: "conv.ovf.u8.un", 0x82: "conv.ovf.i.un", 0x83: "conv.ovf.u.un",
    0x84: "box", 0x85: "newarr", 0x86: "ldlen", 0x87: "ldelema", 0x88: "ldelem.i1",
    0x8C: "ldelem.i4", 0x8E: "ldelem.ref", 0x90: "ldelem.i1", 0x91: "ldelem.u1",
    0x92: "ldelem.i2", 0x93: "ldelem.u2", 0x94: "ldelem.i4", 0x95: "ldelem.u4",
    0x96: "ldelem", 0x97: "ldelem.ref", 0x9C: "stelem.i4", 0xA2: "stelem.ref",
    0xA3: "ldelem", 0xA4: "stelem", 0xA5: "unbox.any", 0xB3: "conv.ovf.i1",
    0xB4: "conv.ovf.u1", 0xB5: "conv.ovf.i2", 0xB6: "conv.ovf.u2", 0xB7: "conv.ovf.i4",
    0xB8: "conv.ovf.u4", 0xB9: "conv.ovf.i8", 0xBA: "conv.ovf.u8",
    0xC2: "refanyval", 0xC3: "ckfinite", 0xC6: "mkrefany", 0xD0: "ldtoken",
    0xD1: "conv.u2", 0xD2: "conv.u1", 0xD3: "conv.i", 0xD4: "conv.ovf.i",
    0xD5: "conv.ovf.u", 0xD6: "add.ovf", 0xD7: "add.ovf.un", 0xD8: "mul.ovf",
    0xD9: "mul.ovf.un", 0xDA: "sub.ovf", 0xDB: "sub.ovf.un", 0xDC: "endfault",
    0xDD: "leave", 0xDE: "leave.s", 0xE0: "conv.u",
    0xFE00: "arglist", 0xFE01: "ceq", 0xFE02: "cgt", 0xFE03: "cgt.un",
    0xFE04: "clt", 0xFE05: "clt.un", 0xFE06: "ldftn", 0xFE09: "ldarg",
    0xFE0A: "ldarga", 0xFE0B: "starg", 0xFE0C: "ldloc", 0xFE0D: "ldloca",
    0xFE0E: "stloc", 0xFE15: "initobj", 0xFE16: "constrained.", 0xFE17: "cpblk",
    0xFE18: "initblk", 0xFE1D: "sizeof",
}

SIMPLE = {
    0x15: "ldc.i4.m1", 0x16: "ldc.i4.0", 0x17: "ldc.i4.1", 0x18: "ldc.i4.2",
    0x19: "ldc.i4.3", 0x1A: "ldc.i4.4", 0x1B: "ldc.i4.5", 0x1C: "ldc.i4.6",
    0x1D: "ldc.i4.7", 0x1E: "ldc.i4.8", 0x0A: "stloc.0", 0x0B: "stloc.1",
    0x0C: "stloc.2", 0x0D: "stloc.3", 0x0E: "ldloc.0", 0x0F: "ldloc.1",
    0x10: "ldloc.2", 0x11: "ldloc.3", 0x12: "ldloca.s", 0x13: "stloc.s",
    0x14: "ldnull", 0x02: "ldarg.0", 0x03: "ldarg.1", 0x04: "ldarg.2",
    0x05: "ldarg.3",
}


def main():
    dll, tname, mname = sys.argv[1], sys.argv[2], sys.argv[3]
    sigfilter = sys.argv[4] if len(sys.argv) > 4 else None

    data = open(dll, "rb").read()
    md_off, sections, rva_to_off = dm.find_metadata_root(data)
    # find_metadata_root returns (offset, sections, rva_to_off) -- keep the helper
    md = dm.Metadata(data, md_off)

    target = None
    for i in range(1, md.rows.get(2, 0) + 1):
        r = md.row(2, i)
        if r["Name"] == tname:
            target = i
            break
    if target is None:
        print("type not found: " + tname)
        return

    row = md.row(2, target)
    ms = row["MethodList"]
    nx = md.row(2, target + 1)["MethodList"] if target < md.rows.get(2, 0) else md.rows.get(6, 0) + 1
    for mi in range(ms, min(nx, md.rows.get(6, 0) + 1)):
        mr = md.row(6, mi)
        if mr["Name"] != mname:
            continue
        try:
            ret, ps, fl = dm.SigParser(md, mr["Signature"]).method_sig()
        except Exception:
            ret, ps = "?", []
        sig = "%s(%s)" % (ret, ", ".join(ps))
        if sigfilter and sigfilter not in sig:
            continue
        rva = mr["RVA"]
        if rva == 0:
            print("=== %s :: no body (abstract/extern)" % sig)
            continue
        off = rva_to_off(rva)
        b = dm.Blob(data, off)
        first = b.u8()
        if (first & 0x03) == 0x02:
            # tiny header: bits 2..9 are the code size, no locals, no extra sections
            codesize = first >> 2
        else:
            # fat header: 12 bytes, code size at offset 4
            b = dm.Blob(data, off)
            flags = b.u16()
            b.u16()                  # maxstack
            codesize = b.u32()
            b.u32()                  # local var sig token
        code = b.bytes(codesize)
        print("=== %s.%s :: %s   (%d bytes of IL)" % (tname, mname, sig, codesize))
        print_il(code, md)
        print()


def read_token(md, tok):
    table = tok >> 24
    ridx = tok & 0xFFFFFF
    names = {0x01: "TypeRef", 0x02: "TypeDef", 0x04: "Field", 0x06: "Method",
             0x0A: "MemberRef", 0x11: "StandAloneSig", 0x1B: "TypeSpec",
             0x70: "UserString"}
    if table == 0x70:
        try:
            return '"%s"' % md.user_string(ridx)
        except Exception:
            return "userstring#%d" % ridx
    if table == 0x0A:
        try:
            r = md.row(0x0A, ridx)
            return "%s::%s" % (md.memberref_class(r["Class"]), r["Name"])
        except Exception:
            return "MemberRef#%d" % ridx
    if table == 0x06:
        try:
            return md.method_name(ridx)
        except Exception:
            return "Method#%d" % ridx
    if table == 0x04:
        try:
            return md.field_name(ridx)
        except Exception:
            return "Field#%d" % ridx
    if table in (0x01, 0x02, 0x1B):
        try:
            return md.type_name_of(table, ridx)
        except Exception:
            return "%s#%d" % (names.get(table, "?"), ridx)
    return "%s#%d" % (names.get(table, "tok%02X" % table), ridx)


def print_il(code, md):
    i = 0
    while i < len(code):
        start = i
        op = code[i]
        i += 1
        key = op
        if op == 0xFE:
            op2 = code[i]
            i += 1
            key = 0xFE00 | op2
        name = NAMES.get(key) or SIMPLE.get(key) or ("op_%02X" % key)
        extra = ""
        n = OPERANDS.get(key)
        if name == "switch":
            cnt = struct.unpack_from("<I", code, i)[0]
            i += 4 + 4 * cnt
            extra = "(%d targets)" % cnt
        elif n is None:
            extra = "?"
        elif n == 1:
            extra = str(code[i]); i += 1
        elif n == 2:
            extra = str(struct.unpack_from("<h", code, i)[0]); i += 2
        elif n == 4:
            v = struct.unpack_from("<i", code, i)[0]
            i += 4
            if name in ("call", "callvirt", "newobj", "ldfld", "ldflda", "stfld",
                        "ldsfld", "ldsflda", "stsfld", "ldstr", "ldtoken", "box",
                        "castclass", "isinst", "unbox.any", "newarr"):
                extra = "0x%08X  %s" % (v & 0xFFFFFFFF, read_token(md, v & 0xFFFFFFFF))
            elif name in ("br.s", "brfalse.s", "brtrue.s", "beq.s", "bge.s",
                          "bgt.s", "ble.s", "blt.s", "bne.un.s"):
                extra = "IL_%04X" % (i + v)
            elif name in ("br", "brfalse", "brtrue", "beq", "bge", "bgt", "ble",
                          "blt", "bne.un", "leave", "leave.s"):
                extra = "IL_%04X" % (i + v)
            else:
                extra = str(v)
        elif n == 8:
            extra = str(struct.unpack_from("<q", code, i)[0]); i += 8
        print("  IL_%04X: %-12s %s" % (start, name, extra))


if __name__ == "__main__":
    main()
