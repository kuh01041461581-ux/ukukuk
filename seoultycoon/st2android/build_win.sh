#!/bin/bash
# 윈도우용 묶음 만들기: IksanTycoon.exe(실행기) + IksanTycoon-PC.jar + jre(윈도우용 자바 런타임) → IksanTycoon-Windows.zip
# 필요: apt gcc-mingw-w64-x86-64, 윈도우용 JRE zip 경로를 JRE_ZIP 으로 (예: Temurin 17 jre x64 windows). 먼저 ./build_pc.sh 로 jar 를 만든다.
set -e
cd "$(dirname "$0")"
: "${JRE_ZIP:?JRE_ZIP=윈도우용 JRE zip 경로}"
W=build/win; rm -rf $W; mkdir -p $W/IksanTycoon
x86_64-w64-mingw32-gcc -O2 -municode -mwindows -o $W/IksanTycoon/IksanTycoon.exe ../tools/win_launcher.c
cp IksanTycoon-PC.jar $W/IksanTycoon/
( cd $W && unzip -q "$JRE_ZIP" -d jretmp && mv jretmp/*/ IksanTycoon/jre && rm -rf jretmp )
( cd $W && rm -f ../../IksanTycoon-Windows.zip && zip -qr ../../IksanTycoon-Windows.zip IksanTycoon )
ls -la IksanTycoon-Windows.zip
