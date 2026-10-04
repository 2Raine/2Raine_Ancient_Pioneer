#!/usr/bin/env python3
"""Minimal ASAR archive reader.

An .asar file is:
    [8 bytes: 4-byte LE uint32 = 4, 4-byte LE uint32 = header-pickle size]
    [4 bytes: LE uint32 header-string size][header JSON][file data...]

Used here only to *inspect* the DeepSeek Harness bundle and pull out individual
files by path, without unpacking 115 MB.
"""
from __future__ import annotations

import json
import struct
import sys


class Asar:
    def __init__(self, path):
        self.path = path
        with open(path, "rb") as fh:
            # Layout (verified against this archive):
            #   [0:4]   LE uint32 = 4            size of the size pickle
            #   [4:8]   LE uint32                header pickle size
            #   [8:12]  LE uint32                = pickle size - 4
            #   [12:16] LE uint32 = hsize        header JSON string size
            #   [16:16+hsize]                    header JSON
            #   [16+hsize:]                      concatenated file contents
            fh.seek(12)
            hsize = struct.unpack("<I", fh.read(4))[0]
            self.header = json.loads(fh.read(hsize).decode("utf-8"))
            self.base = 16 + hsize
        self._fh = open(path, "rb")

    def walk(self, node=None, prefix=""):
        """Yield (path, entry) for every file entry."""
        if node is None:
            node = self.header
        for name, ent in node.get("files", {}).items():
            p = prefix + "/" + name if prefix else name
            if "files" in ent:
                yield from self.walk(ent, p)
            else:
                yield p, ent

    def read(self, path):
        entry = self.header
        for part in path.split("/"):
            entry = entry["files"][part]
        self._fh.seek(self.base + int(entry["offset"]))
        return self._fh.read(int(entry["size"]))

    def text(self, path):
        return self.read(path).decode("utf-8", "replace")


def main():
    a = Asar(sys.argv[1])
    pat = sys.argv[2].lower() if len(sys.argv) > 2 else None
    files = list(a.walk())
    print("总文件数: %d" % len(files))
    if pat:
        hits = [(p, e) for p, e in files if pat in p.lower()]
        print("匹配 %r 的: %d" % (pat, len(hits)))
        for p, e in hits[:200]:
            print("   %9d  %s" % (int(e["size"]), p))
    else:
        import collections
        top = collections.Counter(p.split("/")[0] for p, _ in files)
        for k, v in top.most_common(40):
            print("   %6d  %s" % (v, k))


if __name__ == "__main__":
    main()
