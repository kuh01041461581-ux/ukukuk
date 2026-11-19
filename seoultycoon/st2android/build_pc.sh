#!/bin/bash
# 익산타이쿤 2 PC 판(마우스 클릭 지원) 빌드: IksanTycoon-PC.jar (자바 8 이상). 안드로이드 빌드가 만든 build/stage 의 에셋(원본+익산 모드)을 jar 에 넣는다.
set -e
cd "$(dirname "$0")"
[ -d build/stage/assets/st2mod ] || ./build_apk.sh
P=build/pc; rm -rf $P; mkdir -p $P/classes
javac -nowarn --release 8 -encoding UTF-8 -d $P/classes src/com/seoultycoon/st2/*.java pc/com/seoultycoon/st2pc/*.java 2>&1 | { grep -v "JAVA_TOOL\|warning\|^Note" || true; }
printf 'Main-Class: com.seoultycoon.st2pc.St2Pc\n' > $P/manifest.txt
# PC 판은 여자 시장 상체를 양복(body_j)으로: 앱 판 에셋을 복사해 fb1 만 바꾼다(앱 판은 드레스 body_c)
rm -rf $P/assets && cp -r build/stage/assets $P/assets
BODY=${PC_BODY:-j} python3 -I ../tools/body_final.py >/dev/null && cp mod/fb1.d $P/assets/st2mod/fb1.d
BODY=${BODY:-c} python3 -I ../tools/body_final.py >/dev/null                       # mod 는 앱 판 상태로 되돌린다
jar cfm IksanTycoon-PC.jar $P/manifest.txt -C $P/classes . -C $P/assets .
ls -la IksanTycoon-PC.jar
