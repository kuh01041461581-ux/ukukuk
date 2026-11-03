"""work/ 의 원본 리소스(.d)를 PNG/MMF 로 분류해 assets/ 로 내보낸다 (Unity 임포트용).
.d 파일은 PNG, MMF(MMMD 사운드), 또는 간단한 XOR 류로 보이는 바이너리 데이터(.d 데이터/.m 맵)이다."""
import os, shutil, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
def export(src, dst):
    os.makedirs(dst + '/png', exist_ok=True); os.makedirs(dst + '/mmf', exist_ok=True); os.makedirs(dst + '/bin', exist_ok=True)
    n = {'png': 0, 'mmf': 0, 'bin': 0}
    for f in sorted(os.listdir(src)):
        if not (f.endswith('.d') or f.endswith('.m')): continue
        d = open(os.path.join(src, f), 'rb').read()
        base = f.rsplit('.', 1)[0] + ('_m' if f.endswith('.m') else '')
        if d[:4] == b'\x89PNG': k, ext = 'png', '.png'
        elif d[:4] == b'MMMD': k, ext = 'mmf', '.mmf'
        else: k, ext = 'bin', '.bin'
        open(f'{dst}/{k}/{base}{ext}', 'wb').write(d); n[k] += 1
    print(dst, n)
export(root + '/work/st1/jar', root + '/assets/st1')
export(root + '/work/st2', root + '/assets/st2')
export(root + '/work/st2/pak', root + '/assets/st2_pak')
