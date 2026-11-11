#!/usr/bin/env python3
"""피처폰 게임 APK 조립 스크립트 (aapt2/Android SDK 없이 동작).
사용: python3 build_apk.py <www 폴더> <출력.apk>
필요: lib/ 아래 apksig.jar, dx.jar, android11.jar (README 참고), pyaxml(pip), JDK
"""
import os, subprocess, sys, zipfile, glob, shutil
import pyaxml

HERE = os.path.dirname(os.path.abspath(__file__))
www, out = sys.argv[1], sys.argv[2]
PACKAGE = sys.argv[3] if len(sys.argv) > 3 else None   # 예: net.featurephone.g.maple_mage
LABEL = sys.argv[4] if len(sys.argv) > 4 else None     # 앱 이름
ORIENT = sys.argv[5] if len(sys.argv) > 5 else None    # 화면 방향: 0 가로, 1 세로(기본)
B = os.path.join(HERE, 'build'); shutil.rmtree(B, ignore_errors=True); os.makedirs(B + '/classes'); os.makedirs(B + '/tools')
LIB = lambda n: os.path.join(HERE, 'lib', n)

def run(*a, **k):
    r = subprocess.run(a, capture_output=True, text=True, **k)
    if r.returncode: sys.exit(f'FAILED: {" ".join(a)}\n{r.stdout}\n{r.stderr}')

# 1) 자바 컴파일 -> dex (dx 는 클래스 버전 50 까지만 읽는다)
run('javac', '-nowarn', '--release', '8', '-cp', LIB('android11.jar'), '-d', B + '/classes',
    *glob.glob(HERE + '/src/net/featurephone/games/*.java'))
for f in glob.glob(B + '/classes/**/*.class', recursive=True):
    b = bytearray(open(f, 'rb').read()); b[6:8] = b'\x00\x32'; open(f, 'wb').write(b)
run('java', '-cp', LIB('dx.jar'), 'com.android.dx.command.Main', '--dex', '--output=' + B + '/classes.dex', B + '/classes')

# 2) 매니페스트 -> 바이너리 XML
mtext = open(HERE + '/AndroidManifest.xml', encoding='utf8').read()
if PACKAGE: mtext = mtext.replace('package="net.featurephone.games"', f'package="{PACKAGE}"')
if LABEL: mtext = mtext.replace('android:label="피처폰 게임"', f'android:label="{LABEL}"')
if ORIENT: mtext = mtext.replace('android:screenOrientation="1"', f'android:screenOrientation="{ORIENT}"')
a = pyaxml.AXML(); a.from_xml(mtext)
manifest = a.pack()

# 3) APK(zip) 조립
unsigned = B + '/unsigned.apk'
with zipfile.ZipFile(unsigned, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    z.writestr('AndroidManifest.xml', manifest)
    z.write(B + '/classes.dex', 'classes.dex')
    for root, _, files in os.walk(www):
        for f in sorted(files):
            full = os.path.join(root, f)
            z.write(full, 'assets/www/' + os.path.relpath(full, www).replace(os.sep, '/'))

# 4) 서명 (키스토어가 없으면 새로 만든다. 같은 키스토어로 서명해야 덮어 설치(업데이트)가 된다)
ks = HERE + '/featurephone.p12'
if not os.path.exists(ks):
    run('keytool', '-genkeypair', '-keystore', ks, '-storetype', 'PKCS12', '-storepass', 'featurephone', '-keypass', 'featurephone',
        '-alias', 'games', '-keyalg', 'RSA', '-keysize', '2048', '-validity', '10000', '-dname', 'CN=FeaturePhoneGames')
# v2 서명(안드로이드 7.0+). apksig 가 JDK 21 에서 v1 을 못 만들어서 minSdk 를 24 로 올렸다.
run('javac', '-nowarn', '-cp', LIB('apksig.jar'), '-d', B + '/tools', HERE + '/src/Sign.java')
run('java', '--add-exports', 'java.base/sun.security.x509=ALL-UNNAMED', '-cp', B + '/tools:' + LIB('apksig.jar'), 'Sign', ks, 'featurephone', unsigned, out)
print('OK', out, os.path.getsize(out) // 1024, 'KB')
