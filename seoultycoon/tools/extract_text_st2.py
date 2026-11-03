"""서울타이쿤2 텍스트 .d 추출기. 포맷: u8[32] 문자열 길이표 + CP949 문자열 연속. -> assets/st2_text/<name>.json"""
import os, json, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = root + '/work/st2'; out = root + '/assets/st2_text'; os.makedirs(out, exist_ok=True)
n_ok = 0
for f in sorted(os.listdir(src)):
    if not f.endswith('.d'): continue
    b = open(os.path.join(src, f), 'rb').read()
    if b[:4] in (b'\x89PNG', b'MMMD') or len(b) < 33 or sum(b[:32]) + 32 != len(b): continue
    pos, strs = 32, []
    for ln in b[:32]:
        strs.append(b[pos:pos + ln].decode('cp949', 'replace')); pos += ln
    json.dump(strs, open(f'{out}/{f[:-2]}.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1); n_ok += 1
print(n_ok, 'text files')
