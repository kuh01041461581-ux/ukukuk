"""서울타이쿤2 data1.pak 재패킹. work/st2/data1.pak 의 항목 중 work/pak_mod/<이름>.png 가 있는 것만 바꿔
st2android/mod/data1.pak 로 쓴다(앱은 assets st2mod/data1.pak 를 먼저 읽는다). 포맷은 tools/extract_pak.py 참고.
사용: python3 -I tools/make_iksan_cityhall.py && python3 -I tools/st2_pak_build.py"""
import struct, os, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src, modd, dst = root + '/work/st2/data1.pak', root + '/work/pak_mod', root + '/st2android/mod/data1.pak'
d = open(src, 'rb').read(); total, n = struct.unpack('>II', d[:8]); base = 8 + n * 16
ents = []
for i in range(n):
    e = d[8 + i * 16: 8 + (i + 1) * 16]; name = e[:8].split(b'\0')[0].decode('ascii'); off, size = struct.unpack('>II', e[8:16])
    ents.append((name, d[base + off: base + off + size]))
swapped = 0
if os.path.isdir(modd):
    for k, (name, data) in enumerate(ents):
        p = f'{modd}/{name[:-2] if name.endswith(".d") else name}.png'
        if os.path.exists(p): ents[k] = (name, open(p, 'rb').read()); swapped += 1
body = b''; table = b''
for name, data in ents:
    table += name.encode('ascii').ljust(8, b'\0') + struct.pack('>II', len(body), len(data)); body += data
out = struct.pack('>II', 8 + len(table) + len(body), n) + table + body
os.makedirs(os.path.dirname(dst), exist_ok=True); open(dst, 'wb').write(out)
print(f'data1.pak: {n}개 중 {swapped}개 교체 → {dst} ({len(out)} bytes, 원본 {len(d)})')
