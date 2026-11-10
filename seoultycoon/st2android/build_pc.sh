#!/bin/bash
# 익산타이쿤 2 PC 판(마우스 클릭 지원) 빌드: IksanTycoon-PC.jar (자바 8 이상). 안드로이드 빌드가 만든 build/stage 의 에셋(원본+익산 모드)을 jar 에 넣는다.
set -e
cd "$(dirname "$0")"
[ -d build/stage/assets/st2mod ] || ./build_apk.sh
P=build/pc; rm -rf $P; mkdir -p $P/classes
javac -nowarn --release 8 -encoding UTF-8 -d $P/classes src/com/seoultycoon/st2/*.java pc/com/seoultycoon/st2pc/*.java 2>&1 | { grep -v "JAVA_TOOL\|warning\|^Note" || true; }
printf 'Main-Class: com.seoultycoon.st2pc.St2Pc\n' > $P/manifest.txt
jar cfm IksanTycoon-PC.jar $P/manifest.txt -C $P/classes . -C build/stage/assets .
ls -la IksanTycoon-PC.jar
