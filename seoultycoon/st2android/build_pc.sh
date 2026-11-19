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
MAY="fb0 fb1 fb2 fd0 fd1 fd2 fh0 fh1 fh2 fh3 fh4 fh5 fh6 $(for i in $(seq 0 17); do printf 'ff%d ' $i; done)"
BODY=${PC_BODY:-j} BODY_SCALE=1.0 NECK_EXT=${PC_NECK_EXT:-2} ../tools/mayor_parts.sh            # PC: 양복, 목은 옷깃까지만 잇는다(어깨 위로 늘리지 않음)
for n in $MAY; do [ -f mod/$n.d ] && cp mod/$n.d $P/assets/st2mod/$n.d; done
BODY=${BODY:-c} BODY_SCALE=${BODY_SCALE:-1.12} ../tools/mayor_parts.sh                              # mod 는 앱 판 상태로 되돌린다
jar cfm IksanTycoon-PC.jar $P/manifest.txt -C $P/classes . -C $P/assets .
ls -la IksanTycoon-PC.jar
