#!/usr/bin/env python3
"""
Minimal ECMA-335 CLI metadata reader for .NET assemblies.

Reads type/method/field/property definitions and property signatures directly
from the metadata tables, so NO dependency resolution is required (unlike
System.Reflection, which loses types to ReflectionTypeLoadException).

Usage:
    python dump_metadata.py <assembly.dll> [name-filter ...]

Prints a report for every type whose full name contains one of the filters
(or every type if no filters are given).
"""
import struct
import sys
from collections import namedtuple


# ---------------------------------------------------------------- blob reader
class Blob:
    def __init__(self, data, pos=0):
        self.d = data
        self.p = pos

    def u8(self):
        v = self.d[self.p]
        self.p += 1
        return v

    def u16(self):
        v = struct.unpack_from("<H", self.d, self.p)[0]
        self.p += 2
        return v

    def u32(self):
        v = struct.unpack_from("<I", self.d, self.p)[0]
        self.p += 4
        return v

    def u64(self):
        v = struct.unpack_from("<Q", self.d, self.p)[0]
        self.p += 8
        return v

    def compressed_uint(self):
        """ECMA-335 II.23.2 compressed unsigned integer."""
        b0 = self.d[self.p]
        if b0 & 0x80 == 0:
            self.p += 1
            return b0
        if b0 & 0xC0 == 0x80:
            v = ((b0 & 0x3F) << 8) | self.d[self.p + 1]
            self.p += 2
            return v
        if b0 & 0xE0 == 0xC0:
            v = ((b0 & 0x1F) << 24) | (self.d[self.p + 1] << 16) | \
                (self.d[self.p + 2] << 8) | self.d[self.p + 3]
            self.p += 4
            return v
        raise ValueError("bad compressed uint at %d" % self.p)

    def compressed_int(self):
        return self.compressed_uint()

    def bytes(self, n):
        v = self.d[self.p:self.p + n]
        self.p += n
        return v


# ---------------------------------------------------------------- PE / metadata
def find_metadata_root(data):
    if data[:2] != b"MZ":
        raise ValueError("not a PE file")
    pe_off = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe_off:pe_off + 4] != b"PE\0\0":
        raise ValueError("bad PE signature")
    coff = pe_off + 4
    num_sections = struct.unpack_from("<H", data, coff + 2)[0]
    opt_size = struct.unpack_from("<H", data, coff + 16)[0]
    opt_off = coff + 20
    magic = struct.unpack_from("<H", data, opt_off)[0]
    if magic == 0x20B:      # PE32+
        dd_off = opt_off + 112
    elif magic == 0x10B:    # PE32
        dd_off = opt_off + 96
    else:
        raise ValueError("unknown optional header magic 0x%x" % magic)
    # data directory entry 14 = CLI header
    cli_rva, cli_size = struct.unpack_from("<II", data, dd_off + 14 * 8)

    sections = []
    sec_off = opt_off + opt_size
    for i in range(num_sections):
        base = sec_off + i * 40
        name = data[base:base + 8].rstrip(b"\0").decode("ascii", "replace")
        vsize, vaddr = struct.unpack_from("<II", data, base + 8)
        raw_size, raw_ptr = struct.unpack_from("<II", data, base + 16)
        sections.append((name, vaddr, vsize, raw_ptr, raw_size))

    def rva_to_off(rva):
        # Only raw_size bytes actually exist in the file, even when the
        # section's virtual size is larger (bss-like padding).
        for name, vaddr, vsize, raw_ptr, raw_size in sections:
            if vaddr <= rva < vaddr + raw_size:
                return raw_ptr + (rva - vaddr)
        raise ValueError("RVA 0x%x not in any section" % rva)

    cli_off = rva_to_off(cli_rva)
    md_rva, md_size = struct.unpack_from("<II", data, cli_off + 8)
    return rva_to_off(md_rva), sections, rva_to_off


class Metadata:
    """Parses #~ (compressed) metadata tables."""

    # table id -> name
    TABLES = {
        0x00: "Module", 0x01: "TypeRef", 0x02: "TypeDef", 0x04: "Field",
        0x06: "MethodDef", 0x08: "Param", 0x09: "InterfaceImpl",
        0x0A: "MemberRef", 0x0B: "Constant", 0x0C: "CustomAttribute",
        0x0D: "FieldMarshal", 0x0E: "DeclSecurity", 0x0F: "ClassLayout",
        0x10: "FieldLayout", 0x11: "StandAloneSig", 0x12: "EventMap",
        0x14: "Event", 0x15: "PropertyMap", 0x17: "Property",
        0x18: "MethodSemantics", 0x19: "MethodImpl", 0x1A: "ModuleRef",
        0x1B: "TypeSpec", 0x1C: "ImplMap", 0x1D: "FieldRVA",
        0x20: "Assembly", 0x21: "AssemblyProcessor", 0x22: "AssemblyOS",
        0x23: "AssemblyRef", 0x24: "AssemblyRefProcessor",
        0x25: "AssemblyRefOS", 0x26: "File", 0x27: "ExportedType",
        0x28: "ManifestResource", 0x29: "NestedClass", 0x2A: "GenericParam",
        0x2B: "MethodSpec", 0x2C: "GenericParamConstraint",
    }

    def __init__(self, data, md_off):
        self.data = data
        self.off = md_off
        b = Blob(data, md_off)
        if b.u32() != 0x424A5342:
            raise ValueError("bad metadata signature")
        b.u32()          # major/minor version
        b.u32()          # reserved
        ver_len = b.u32()
        b.bytes(ver_len)
        b.u16()          # flags
        n_streams = b.u16()
        if not n_streams:
            # Some images have a 0 in the stream-count slot in this position;
            # fall back to scanning the stream directory.
            n_streams = struct.unpack_from("<H", data, md_off + 0x0E)[0] or 5
        self.streams = {}
        for _ in range(n_streams):
            s_off = b.u32()
            s_size = b.u32()
            name = bytearray()
            while b.d[b.p] != 0:
                name.append(b.d[b.p])
                b.p += 1
            b.p += 1
            b.p = (b.p + 3) & ~3
            self.streams[name.decode("ascii")] = (md_off + s_off, s_size)

        self.strings_off = self.streams.get("#Strings", (0, 0))[0]
        self.blobs_off = self.streams.get("#Blob", (0, 0))[0]
        self.guids_off = self.streams.get("#GUID", (0, 0))[0]

        # ---- table stream
        t_off = self.streams["#~"][0]
        tb = Blob(data, t_off)
        tb.u32()                       # reserved
        tb.u8()                        # major
        tb.u8()                        # minor
        self.heap_sizes = tb.u8()
        tb.u8()                        # reserved
        valid = tb.u64()
        sorted_mask = tb.u64()
        self.rows = {}
        for i in range(64):
            if valid & (1 << i):
                self.rows[i] = tb.u32()

        self.str_idx = 4 if (self.heap_sizes & 0x01) else 2
        self.guid_idx = 4 if (self.heap_sizes & 0x02) else 2
        self.blob_idx = 4 if (self.heap_sizes & 0x04) else 2

        # simple index sizes for coded/simple index resolution
        self.tbl_rows = {i: self.rows.get(i, 0) for i in range(64)}
        self._table_start = tb.p
        self._compute_layout()

    # ---- heap accessors
    def string(self, idx):
        if idx == 0:
            return ""
        p = self.strings_off + idx
        end = self.data.index(b"\0", p)
        return self.data[p:end].decode("utf-8", "replace")

    def blob(self, idx):
        if idx == 0:
            return b""
        b = Blob(self.data, self.blobs_off + idx)
        n = b.compressed_uint()
        return b.bytes(n)

    # ---- convenience lookups used by the IL disassembler ----
    def user_string(self, idx):
        """Read a #US (user string) heap entry; the blob is UTF-16 with a trailing flag byte."""
        blob = self.blob(idx)
        if len(blob) <= 1:
            return ""
        return blob[:-1].decode("utf-16-le", "replace")

    def _type_display(self, table, ridx):
        if table == 0x02:
            r = self.row(0x02, ridx)
            ns = r["Namespace"]
            return (ns + "." if ns else "") + r["Name"]
        if table == 0x01:
            r = self.row(0x01, ridx)
            ns = r["Namespace"]
            return (ns + "." if ns else "") + r["Name"]
        if table == 0x1B:
            r = self.row(0x1B, ridx)
            return "<spec>"
        return "Type#%d" % ridx

    def type_name_of(self, table, ridx):
        return self._type_display(table, ridx)

    def memberref_class(self, coded):
        table, ridx = coded
        if table is None or ridx == 0:
            return "?"
        return self._type_display(table, ridx)

    def method_name(self, ridx):
        """Return 'Type::Method' for a MethodDef row."""
        r = self.row(0x06, ridx)
        owner = "?"
        for i in range(1, self.rows.get(0x02, 0) + 1):
            tr = self.row(0x02, i)
            start = tr["MethodList"]
            end = (self.row(0x02, i + 1)["MethodList"]
                   if i < self.rows.get(0x02, 0) else self.rows.get(0x06, 0) + 1)
            if start <= ridx < end:
                ns = tr["Namespace"]
                owner = (ns + "." if ns else "") + tr["Name"]
                break
        return owner + "::" + r["Name"]

    def field_name(self, ridx):
        r = self.row(0x04, ridx)
        owner = "?"
        for i in range(1, self.rows.get(0x02, 0) + 1):
            tr = self.row(0x02, i)
            start = tr["FieldList"]
            end = (self.row(0x02, i + 1)["FieldList"]
                   if i < self.rows.get(0x02, 0) else self.rows.get(0x04, 0) + 1)
            if start <= ridx < end:
                ns = tr["Namespace"]
                owner = (ns + "." if ns else "") + tr["Name"]
                break
        return owner + "::" + r["Name"]

    # ---- index sizing
    def _simple_idx_size(self, table_id):
        return 4 if self.tbl_rows.get(table_id, 0) >= 0x10000 else 2

    def _coded_idx_size(self, bits, tables):
        max_rows = max(self.tbl_rows.get(t, 0) for t in tables)
        return 4 if max_rows >= (1 << (16 - bits)) else 2

    # column layouts: (name, kind, arg)
    LAYOUT = {
        0x00: [("Generation", "u16"), ("Name", "str"), ("Mvid", "guid"),
               ("EncId", "guid"), ("EncBaseId", "guid")],
        0x01: [("ResolutionScope", "coded", (2, (0x00, 0x1A, 0x23, 0x01))),
               ("Name", "str"), ("Namespace", "str")],
        0x02: [("Flags", "u32"), ("Name", "str"), ("Namespace", "str"),
               ("Extends", "coded", (2, (0x02, 0x01, 0x1B))),
               ("FieldList", "idx", 0x04), ("MethodList", "idx", 0x06)],
        0x04: [("Flags", "u16"), ("Name", "str"), ("Signature", "blob")],
        0x06: [("RVA", "u32"), ("ImplFlags", "u16"), ("Flags", "u16"),
               ("Name", "str"), ("Signature", "blob"), ("ParamList", "idx", 0x08)],
        0x08: [("Flags", "u16"), ("Sequence", "u16"), ("Name", "str")],
        0x09: [("Class", "idx", 0x02), ("Interface", "coded", (2, (0x02, 0x01, 0x1B)))],
        0x0A: [("Class", "coded", (3, (0x02, 0x01, 0x1B, 0x06, 0x1B))),
               ("Name", "str"), ("Signature", "blob")],
        0x0B: [("Type", "u8"), ("Padding", "u8"), ("Parent", "coded", (2, (0x04, 0x08, 0x17))), ("Value", "blob")],
        0x0C: [("Parent", "coded", (5, (0x06, 0x04, 0x01, 0x02, 0x08, 0x17, 0x14, 0x1B, 0x00))),
               ("Type", "coded", (3, (0x06, 0x04, 0x01))), ("Value", "blob")],
        0x0D: [("Parent", "coded", (1, (0x04, 0x08))), ("NativeType", "blob")],
        0x0E: [("Action", "u16"), ("Parent", "coded", (2, (0x02, 0x06, 0x20))), ("PermissionSet", "blob")],
        0x0F: [("PackingSize", "u16"), ("ClassSize", "u32"), ("Parent", "idx", 0x02)],
        0x10: [("Offset", "u32"), ("Field", "idx", 0x04)],
        0x11: [("Signature", "blob")],
        0x12: [("Parent", "idx", 0x02), ("EventList", "idx", 0x14)],
        0x14: [("EventFlags", "u16"), ("Name", "str"), ("EventType", "coded", (3, (0x02, 0x01, 0x1B)))],
        0x15: [("Parent", "idx", 0x02), ("PropertyList", "idx", 0x17)],
        0x16: [("Signature", "blob")],
        0x17: [("Flags", "u16"), ("Name", "str"), ("Type", "blob")],
        0x18: [("Semantics", "u16"), ("Method", "idx", 0x06), ("Association", "coded", (1, (0x14, 0x17)))],
        0x19: [("Class", "idx", 0x02), ("MethodBody", "coded", (1, (0x06, 0x0A))),
               ("MethodDeclaration", "coded", (1, (0x06, 0x0A)))],
        0x1A: [("Name", "str")],
        0x1B: [("Signature", "blob")],
        0x1C: [("MappingFlags", "u16"), ("MemberForwarded", "coded", (1, (0x04, 0x06))),
               ("ImportName", "str"), ("ImportScope", "idx", 0x1A)],
        0x1D: [("RVA", "u32"), ("Field", "idx", 0x04)],
        0x20: [("HashAlgId", "u32"), ("MajorVersion", "u16"), ("MinorVersion", "u16"),
               ("BuildNumber", "u16"), ("RevisionNumber", "u16"), ("Flags", "u32"),
               ("PublicKey", "blob"), ("Name", "str"), ("Culture", "str")],
        0x21: [("Processor", "u32")],
        0x22: [("OSPlatformID", "u32"), ("OSMajorVersion", "u32"), ("OSMinorVersion", "u32")],
        0x23: [("MajorVersion", "u16"), ("MinorVersion", "u16"), ("BuildNumber", "u16"),
               ("RevisionNumber", "u16"), ("Flags", "u32"), ("PublicKeyOrToken", "blob"),
               ("Name", "str"), ("Culture", "str"), ("HashValue", "blob")],
        0x24: [("Processor", "u32"), ("AssemblyRef", "idx", 0x23)],
        0x25: [("OSPlatformID", "u32"), ("OSMajorVersion", "u32"), ("OSMinorVersion", "u32"),
               ("AssemblyRef", "idx", 0x23)],
        0x26: [("Flags", "u32"), ("Name", "str"), ("HashValue", "blob")],
        0x27: [("Flags", "u32"), ("TypeDefId", "u32"), ("TypeName", "str"),
               ("TypeNamespace", "str"), ("Implementation", "coded", (2, (0x26, 0x23, 0x27)))],
        0x28: [("Offset", "u32"), ("Flags", "u32"), ("Name", "str"),
               ("Implementation", "coded", (2, (0x26, 0x23, 0x27)))],
        0x29: [("NestedClass", "idx", 0x02), ("EnclosingClass", "idx", 0x02)],
        0x2A: [("Number", "u16"), ("Flags", "u16"), ("Owner", "coded", (1, (0x02, 0x06))), ("Name", "str")],
        0x2B: [("Owner", "coded", (1, (0x2A, 0x1B))), ("Constraint", "coded", (2, (0x02, 0x01, 0x1B)))],
        0x2C: [("Id", "u16"), ("Name", "str")],
    }

    def _compute_layout(self):
        """Compute byte offsets of every present table."""
        offsets = {}
        pos = self._table_start
        for tid in sorted(self.rows):
            offsets[tid] = pos
            row_size = self._row_size(tid)
            pos += row_size * self.rows[tid]
        self.table_offsets = offsets

    def _row_size(self, tid):
        layout = self.LAYOUT.get(tid)
        if layout is None:
            raise ValueError("no layout for table 0x%02X (%s)"
                             % (tid, self.TABLES.get(tid, "?")))
        size = 0
        for col in layout:
            kind = col[1]
            if kind == "u8":
                size += 1
            elif kind == "u16":
                size += 2
            elif kind == "u32":
                size += 4
            elif kind == "str":
                size += self.str_idx
            elif kind == "guid":
                size += self.guid_idx
            elif kind == "blob":
                size += self.blob_idx
            elif kind == "idx":
                size += self._simple_idx_size(col[2])
            elif kind == "coded":
                size += self._coded_idx_size(col[2][0], col[2][1])
            else:
                raise ValueError("unknown column kind " + kind)
        return size

    def row(self, tid, index):
        """Return dict of decoded columns for 1-based row `index`."""
        layout = self.LAYOUT[tid]
        base = self.table_offsets[tid] + (index - 1) * self._row_size(tid)
        b = Blob(self.data, base)
        out = {}
        for col in layout:
            name, kind = col[0], col[1]
            if kind == "u8":
                out[name] = b.u8()
            elif kind == "u16":
                out[name] = b.u16()
            elif kind == "u32":
                out[name] = b.u32()
            elif kind == "str":
                out[name] = self.string(b.u32() if self.str_idx == 4 else b.u16())
            elif kind == "guid":
                out[name] = b.u32() if self.guid_idx == 4 else b.u16()
            elif kind == "blob":
                out[name] = self.blob(b.u32() if self.blob_idx == 4 else b.u16())
            elif kind == "idx":
                out[name] = b.u32() if self._simple_idx_size(col[2]) == 4 else b.u16()
            elif kind == "coded":
                bits, tables = col[2]
                sz = self._coded_idx_size(bits, tables)
                raw = b.u32() if sz == 4 else b.u16()
                tag = raw & ((1 << bits) - 1)
                ridx = raw >> bits
                out[name] = (tables[tag] if tag < len(tables) else None, ridx)
            else:
                raise ValueError(kind)
        return out

    def table(self, tid):
        return [self.row(tid, i) for i in range(1, self.rows.get(tid, 0) + 1)]


# ---------------------------------------------------------------- signature parsing
ELEMENT_TYPES = {
    0x01: "void", 0x02: "bool", 0x03: "char", 0x04: "sbyte", 0x05: "byte",
    0x06: "short", 0x07: "ushort", 0x08: "int", 0x09: "uint", 0x0A: "long",
    0x0B: "ulong", 0x0C: "float", 0x0D: "double", 0x0E: "string",
    0x0F: "TypedReference", 0x10: "IntPtr", 0x11: "UIntPtr", 0x12: "object",
    0x13: "class", 0x14: "array", 0x15: "genericinst", 0x16: "typedbyref",
    0x18: "native int", 0x19: "native uint", 0x1B: "fnptr", 0x1C: "object",
    0x1D: "sizedarray", 0x1E: "mvar", 0x1F: "cvar", 0x45: "PTR",
    0x1F: "cvar",
}


class SigParser:
    def __init__(self, md, blob):
        self.md = md
        self.b = Blob(blob)

    def _resolve_code(self, code):
        """Return a readable name for a TypeDefOrRefOrSpecEncoded token."""
        tag = code & 0x03
        ridx = code >> 2
        try:
            if tag == 0:   # TypeDef
                r = self.md.row(0x02, ridx)
                ns = r["Namespace"]
                return (ns + "." if ns else "") + r["Name"]
            if tag == 1:   # TypeRef
                r = self.md.row(0x01, ridx)
                ns = r["Namespace"]
                return (ns + "." if ns else "") + r["Name"]
            if tag == 2:   # TypeSpec
                r = self.md.row(0x1B, ridx)
                sub = SigParser(self.md, r["Signature"])
                return sub.type_name(self.md)
        except Exception as exc:
            return "<code %d:%d %s>" % (tag, ridx, exc)
        return "<code tag%d:%d>" % (tag, ridx)

    def type_name(self, md, depth=0):
        """Parse one type from the current position; returns a readable string."""
        b = self.b
        if depth > 24:
            return "<deep>"
        et = b.u8()
        if et == 0x01:
            return "void"
        if et in ELEMENT_TYPES and et not in (0x13, 0x1E, 0x14, 0x15, 0x1D, 0x45, 0x1B):
            return ELEMENT_TYPES[et]
        if et == 0x13:             # VAR (generic *type* parameter)
            return "!" + str(b.compressed_uint())
        if et in (0x1F, 0x20):     # CMOD_REQD / CMOD_OPT
            mod = self._resolve_code(b.compressed_uint())
            return "[" + mod + "]" + self.type_name(md, depth + 1)
        if et == 0x21:             # GENERICINST with explicit kind byte
            kind = b.u8()
            return self.type_name(md, depth + 1)
        if et == 0x1D:             # SZARRAY
            return self.type_name(md, depth + 1) + "[]"
        if et == 0x14:             # ARRAY
            inner = self.type_name(md, depth + 1)
            rank = b.compressed_uint()
            numsizes = b.compressed_uint()
            for _ in range(numsizes):
                b.compressed_uint()
            numlo = b.compressed_uint()
            for _ in range(numlo):
                b.compressed_uint()
            return inner + "[" + "," * max(rank - 1, 0) + "]"
        if et == 0x15:             # GENERICINST
            kind = b.u8()          # CLASS or VALUETYPE
            raw = b.compressed_uint()
            name = self._resolve_code(raw)
            argc = b.compressed_uint()
            args = [self.type_name(md, depth + 1) for _ in range(argc)]
            return name + "<" + ", ".join(args) + ">"
        if et == 0x1B:             # FNPTR (method sig follows)
            return "fnptr"
        if et == 0x45:             # PTR
            return self.type_name(md, depth + 1) + "*"
        if et == 0x1E:             # MVAR (generic method parameter)
            return "!!" + str(b.compressed_uint())
        return "et:0x%02X" % et

    def _typedeforref(self, raw):
        return self._resolve_code(raw)

    def method_sig(self):
        b = self.b
        flags = b.u8()
        if flags & 0x10:
            b.compressed_uint()    # generic param count
        nparams = b.compressed_uint()
        ret = self.type_name(self.md)
        params = []
        for _ in range(nparams):
            if b.d[b.p] in (0x41, 0x42):   # SENTINEL / CMOD
                b.u8()
            while b.d[b.p] in (0x1F, 0x20):   # CMOD_REQD / CMOD_OPT
                b.u8()
                b.compressed_uint()
            params.append(self.type_name(self.md))
        return ret, params, flags

    def field_sig(self):
        b = self.b
        b.u8()                     # calling convention / 0x06
        return self.type_name(self.md)

    def property_sig(self):
        b = self.b
        b.u8()                     # 0x08 | 0x20
        nparams = b.compressed_uint()
        t = self.type_name(self.md)
        params = [self.type_name(self.md) for _ in range(nparams)]
        return t, params


# ---------------------------------------------------------------- main
def main():
    path = sys.argv[1]
    filters = sys.argv[2:]
    with open(path, "rb") as fh:
        data = fh.read()

    md_off, _, _ = find_metadata_root(data)
    md = Metadata(data, md_off)

    n_types = md.rows.get(0x02, 0)
    print("# assembly: %s" % path)
    print("# types=%d fields=%d methods=%d props=%d"
          % (n_types, md.rows.get(0x04, 0), md.rows.get(0x06, 0), md.rows.get(0x17, 0)))

    type_rows = md.table(0x02)
    field_rows = md.table(0x04)
    method_rows = md.table(0x06)
    prop_rows = md.table(0x17)
    nested = {}
    for r in md.table(0x29):
        nested[r["NestedClass"]] = r["EnclosingClass"]

    # field/property -> owner type
    field_owner = {}
    for i, tr in enumerate(type_rows):
        start = tr["FieldList"]
        end = type_rows[i + 1]["FieldList"] if i + 1 < len(type_rows) else len(field_rows) + 1
        for fidx in range(start, end):
            field_owner[fidx] = i + 1

    method_owner = {}
    for i, tr in enumerate(type_rows):
        start = tr["MethodList"]
        end = type_rows[i + 1]["MethodList"] if i + 1 < len(type_rows) else len(method_rows) + 1
        for midx in range(start, end):
            method_owner[midx] = i + 1

    prop_owner = {}
    for i, pm in enumerate(md.table(0x15)):
        start = pm["PropertyList"]
        next_start = md.table(0x15)[i + 1]["PropertyList"] if i + 1 < len(md.table(0x15)) else len(prop_rows) + 1
        for pidx in range(start, next_start):
            prop_owner[pidx] = pm["Parent"]

    # method -> (semantics) for property getter/setter
    prop_accessors = {}
    for ms in md.table(0x18):
        assoc_table, assoc_idx = ms["Association"]
        if assoc_table == 0x17:
            prop_accessors.setdefault(assoc_idx, []).append((ms["Semantics"], ms["Method"]))

    # full names, handling nesting
    def full_name(i):
        tr = type_rows[i - 1]
        ns, name = tr["Namespace"], tr["Name"]
        parts = [name]
        cur = i
        while cur in nested:
            cur = nested[cur]
            parts.append(type_rows[cur - 1]["Name"])
        parts.reverse()
        joined = "+".join(parts)
        return (ns + "." if ns else "") + joined

    # build extends names
    def coded_name(coded):
        t, ridx = coded
        if t is None or ridx == 0:
            return ""
        try:
            if t == 0x02:
                return full_name(ridx)
            if t == 0x01:
                r = md.row(0x01, ridx)
                return (r["Namespace"] + "." if r["Namespace"] else "") + r["Name"]
            if t == 0x1B:
                return "<spec>"
        except Exception:
            pass
        return ""

    TYPE_ATTR = {0x01: "public", 0x02: "nested_public", 0x00: "notpublic"}
    out = []
    for i in range(1, n_types + 1):
        fname = full_name(i)
        if filters and not any(f.lower() in fname.lower() for f in filters):
            continue
        tr = type_rows[i - 1]
        ta = tr["Flags"] & 0x07
        kind = "class"
        if tr["Flags"] & 0x20:
            kind = "interface"
        if tr["Extends"][0] == 0x02:
            bn = coded_name(tr["Extends"])
            if bn == "System.Enum":
                kind = "enum"
            elif bn == "System.ValueType":
                kind = "struct"
            elif bn == "System.MulticastDelegate":
                kind = "delegate"
        base = coded_name(tr["Extends"])
        out.append("=== %s %s%s : %s" % (
            kind, TYPE_ATTR.get(ta, hex(ta)), fname, base))

        # fields
        fstart = tr["FieldList"]
        fend = type_rows[i]["FieldList"] if i < n_types else len(field_rows) + 1
        for fidx in range(fstart, fend):
            if fidx - 1 >= len(field_rows):
                break
            fr = field_rows[fidx - 1]
            try:
                ftype = SigParser(md, fr["Signature"]).field_sig()
            except Exception as exc:
                ftype = "<sigerr %s>" % exc
            acc = "public" if fr["Flags"] & 0x07 == 0x06 else \
                  ("private" if fr["Flags"] & 0x07 == 0x01 else "other")
            mods = []
            if fr["Flags"] & 0x10:
                mods.append("static")
            if fr["Flags"] & 0x40:
                mods.append("readonly")
            if fr["Flags"] & 0x20:
                mods.append("initonly")
            out.append("    F %s %s %s %s" % (acc, " ".join(mods), ftype, fr["Name"]))

        # methods
        mstart = tr["MethodList"]
        mend = type_rows[i]["MethodList"] if i < n_types else len(method_rows) + 1
        for midx in range(mstart, mend):
            if midx - 1 >= len(method_rows):
                break
            mr = method_rows[midx - 1]
            try:
                ret, params, mflags = SigParser(md, mr["Signature"]).method_sig()
            except Exception as exc:
                ret, params, mflags = "<sigerr %s>" % exc, [], 0
            acc = "public" if mr["Flags"] & 0x07 == 0x06 else \
                  ("private" if mr["Flags"] & 0x07 == 0x01 else
                   ("family" if mr["Flags"] & 0x07 == 0x04 else "other"))
            mods = []
            if mr["Flags"] & 0x10:
                mods.append("static")
            if mr["Flags"] & 0x40:
                mods.append("virtual")
            if mr["Flags"] & 0x400:
                mods.append("abstract")
            out.append("    M %s %s %s %s(%s)" % (
                acc, " ".join(mods), ret, mr["Name"], ", ".join(params)))

        # properties with accessors
        for pidx, owner in prop_owner.items():
            if owner != i:
                continue
            pr = prop_rows[pidx - 1]
            try:
                ptype, pparams = SigParser(md, pr["Type"]).property_sig()
            except Exception as exc:
                ptype, pparams = "<sigerr %s>" % exc, []
            accs = []
            for sem, mref in prop_accessors.get(pidx, []):
                if mref - 1 < len(method_rows):
                    mname = method_rows[mref - 1]["Name"]
                    accs.append("%s:%s" % (sem, mname))
            out.append("    P %s %s%s { %s }" % (
                ptype, pr["Name"],
                "[" + ", ".join(pparams) + "]" if pparams else "",
                " ".join(accs)))

    sys.stdout.write("\n".join(out) + "\n")


if __name__ == "__main__":
    main()
