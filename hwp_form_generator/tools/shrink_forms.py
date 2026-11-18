# -*- coding: utf-8 -*-
"""forms/*/template.hwpx 안의 큰 그림을 줄여 프로그램 용량을 낮춘다(제자리 덮어쓰기, 다시 돌려도 안전).
   사용: python tools/shrink_forms.py"""
import io
import os
import sys
import zipfile

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
from hwpgen import tinyimg  # noqa: E402


def main():
    before = after = 0
    for d in sorted(os.listdir(os.path.join(HERE, "forms"))):
        p = os.path.join(HERE, "forms", d, "template.hwpx")
        if not os.path.exists(p):
            continue
        zin = zipfile.ZipFile(p)
        items = [(i, zin.read(i.filename)) for i in zin.infolist()]
        changed = False
        out = io.BytesIO()
        with zipfile.ZipFile(out, "w") as z:
            for info, data in items:
                if info.filename.startswith("BinData/"):
                    new = tinyimg.shrink(data, info.filename)
                    changed |= new is not data
                    data = new
                zi = zipfile.ZipInfo(info.filename, date_time=info.date_time)
                zi.compress_type = info.compress_type
                z.writestr(zi, data)
        b = os.path.getsize(p)
        if changed:
            with open(p, "wb") as f:
                f.write(out.getvalue())
        before += b
        after += os.path.getsize(p)
    print("%.1f MB → %.1f MB" % (before / 1e6, after / 1e6))


if __name__ == "__main__":
    main()
