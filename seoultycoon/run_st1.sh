#!/bin/sh
# 서울타이쿤1 원본(J2ME/SKT)을 PC에서 헤드리스로 구동하고 스크린샷을 저장한다.
# 사용: ./run_st1.sh <출력폴더> "wait:6000,key:53,wait:1500,shot:a"
# 키: 53=확인(5), 50/52/54/56=상/좌/우/하
cd "$(dirname "$0")/harness" || exit 1
[ -d out/Runner.class ] || { mkdir -p out && javac -nowarn -d out $(find src -name '*.java'); }
java -cp out:../work/st1/jar -Dfile.encoding=EUC-KR -Djava.awt.headless=true -Dxfile.dir=/tmp/xfile Runner "$@"
