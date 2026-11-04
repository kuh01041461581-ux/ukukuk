#!/usr/bin/env python3
"""게임마다 따로 APK 를 만든다. 사용: python3 build_each.py <기본 www 폴더> <출력 폴더>
각 APK 에는 그 게임 하나만 들어 있고, 실행하면 바로 그 게임이 열린다.
게임 zip 은 www/games/ 에, 이름 목록은 www/games/games.json 에 있어야 한다."""
import json, os, shutil, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__))
base, outdir = sys.argv[1], sys.argv[2]
os.makedirs(outdir, exist_ok=True)
games = json.load(open(os.path.join(base, 'games/games.json'), encoding='utf8'))
for g in games:
    slug = g['file'][:-4]
    tmp = os.path.join(HERE, 'wwwtmp_' + slug)
    shutil.rmtree(tmp, ignore_errors=True)
    shutil.copytree(base, tmp, ignore=shutil.ignore_patterns('games'))
    os.makedirs(tmp + '/games')
    shutil.copy(os.path.join(base, 'games', g['file']), tmp + '/games/')
    json.dump({'autolaunch': True, 'games': [g]}, open(tmp + '/games/games.json', 'w', encoding='utf8'), ensure_ascii=False)
    out = os.path.join(outdir, f"{g['title'].replace(' ', '')}.apk")
    r = subprocess.run([sys.executable, os.path.join(HERE, 'build_apk.py'), tmp, out,
                        'net.featurephone.g.' + slug.replace('-', '_'), g['title']], capture_output=True, text=True)
    print(g['title'], '->', r.stdout.strip().splitlines()[-1] if r.returncode == 0 else 'FAILED\n' + r.stderr[-400:])
    shutil.rmtree(tmp, ignore_errors=True)
