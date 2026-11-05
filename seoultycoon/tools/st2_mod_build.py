"""서울타이쿤2 텍스트 모드 빌더.
assets/st2_text/*.json(원본 추출본) 에 st2android/mod/text_map.json 의 치환표를 적용하고,
게임이 읽는 .d 형식(u8[32] 길이표 + CP949 문자열)으로 다시 묶어 st2android/mod/<이름>.d 에 쓴다.
사용: python3 -I tools/st2_mod_build.py   (출력 파일은 앱/PC검증기가 원본보다 먼저 읽는다)"""
import os, json, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = root + '/assets/st2_text'; mod = root + '/st2android/mod'
rules = json.load(open(mod + '/text_map.json', encoding='utf-8'))['replace']
n = 0
for f in sorted(os.listdir(src)):
    if not f.endswith('.json'): continue
    strs = json.load(open(os.path.join(src, f), encoding='utf-8'))
    out = []
    for s in strs:
        for a, b in rules: s = s.replace(a, b)
        out.append(s)
    if out == strs:
        p = f'{mod}/{f[:-5]}.d'
        if os.path.exists(p): os.remove(p)
        continue
    enc = [s.encode('cp949', 'replace') for s in out]
    if len(enc) != 32 or any(len(e) > 255 for e in enc): sys.exit(f'{f}: 문자열 수/길이 초과')
    data = bytes(len(e) for e in enc) + b''.join(enc)
    open(f'{mod}/{f[:-5]}.d', 'wb').write(data); n += 1
print(n, '개 .d 생성 →', mod)

# ---- 코드 이미지(client.bin1116) 안의 UTF-16 문자열 패치: 글자 수가 같은 치환만 허용 ----
img = bytearray(open(root + '/work/st2/client.bin1116', 'rb').read()); total = 0
for a, b in json.load(open(mod + '/text_map.json', encoding='utf-8')).get('image_replace', []):
    if len(a) != len(b): sys.exit(f'이미지 치환은 같은 글자 수만: {a} -> {b}')
    ea, eb = a.encode('utf-16le'), b.encode('utf-16le'); c = img.count(ea)
    img = img.replace(ea, eb); total += c; print(f'  {a} -> {b}: {c}곳')
open(mod + '/client.bin1116', 'wb').write(img); print('이미지 패치', total, '곳 → client.bin1116')
