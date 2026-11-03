#!/bin/sh
# 서울타이쿤1 원본을 PC 창에서 플레이 (Java 11+ 필요). 방향키/Enter, Z·X=소프트키
cd "$(dirname "$0")/harness" || exit 1
[ -f out/Player.class ] || { mkdir -p out && javac -nowarn -d out $(find src -name '*.java'); }
exec java -cp out:../work/st1/jar -Dfile.encoding=EUC-KR -Dxfile.dir="$HOME/.seoultycoon" Player "$@"
