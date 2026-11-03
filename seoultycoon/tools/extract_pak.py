"""서울타이쿤2 P/data1.pak 추출기.
포맷: u32 파일크기 | u32 항목수 | 항목[ name(8, NUL패딩) | u32 offset | u32 size ] ... | 데이터(offset은 데이터영역 기준)
사용: python3 -I tools/extract_pak.py data1.pak outdir
"""
import struct, sys, os
def main(src, out):
    d = open(src, 'rb').read()
    total, n = struct.unpack('>II', d[:8])
    assert total == len(d), (total, len(d))
    base = 8 + n * 16
    os.makedirs(out, exist_ok=True)
    idx = []
    for i in range(n):
        e = d[8 + i * 16: 8 + (i + 1) * 16]
        name = e[:8].split(b'\0')[0].decode('ascii')
        off, size = struct.unpack('>II', e[8:16])
        open(os.path.join(out, name), 'wb').write(d[base + off: base + off + size])
        idx.append((name, off, size))
    print(n, 'files; end =', max(o + s for _, o, s in idx), 'of', len(d) - base)
if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
