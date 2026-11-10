#!/bin/bash
# 서울타이쿤2 안드로이드 APK 빌드 (apt: aapt apksigner dalvik-exchange zipalign libandroid-23-java; 사전: tools/setup_work.sh 로 work/st2 준비)
set -e
set -o pipefail
cd "$(dirname "$0")"
OUT=build; rm -rf $OUT; mkdir -p $OUT/classes $OUT/stage/assets/st2
AJ=/usr/lib/android-sdk/platforms/android-23/android.jar
javac -nowarn -source 8 -target 8 -encoding UTF-8 -cp $AJ -d $OUT/classes src/com/seoultycoon/st2/*.java app/com/seoultycoon/st2app/*.java 2>&1 | { grep -v "JAVA_TOOL\|warning\|^Note" || true; }
# dx 는 람다를 처리하지 못하므로 먼저 익명 클래스로 풀어 준다(tools/Delambda.java)
ASM=/usr/share/java/asm-9.7.jar:/usr/share/java/asm-tree-9.7.jar
mkdir -p $OUT/tool && javac -nowarn -cp $ASM -d $OUT/tool ../tools/Delambda.java 2>&1 | { grep -v JAVA_TOOL || true; }
java -cp $OUT/tool:$ASM Delambda $OUT/classes 2>&1 | { grep -v JAVA_TOOL || true; }
/usr/lib/android-sdk/build-tools/debian/dx --dex --min-sdk-version=26 --output=$OUT/classes.dex $OUT/classes 2>&1 | { grep -v "JAVA_TOOL" || true; }
cp ../work/st2/* $OUT/stage/assets/st2/ ; rm -f $OUT/stage/assets/st2/cc2     # 세이브(cc2)는 포함하지 않는다
python3 -I ../tools/convert_audio_res.py $OUT/stage/assets/st2                  # MMF(s*.d) -> 표준 MIDI
mkdir -p $OUT/stage/assets/st2mod; python3 -I ../tools/st2_mod_build.py; python3 -I ../tools/make_iksan_cityhall.py; python3 -I ../tools/make_iksan_horse.py; python3 -I ../tools/make_iksan_minimap.py; python3 -I ../tools/make_iksan_logo.py; python3 -I ../tools/make_maryong.py; python3 -I ../tools/st2_pak_build.py; cp mod/*.d mod/*.m mod/client.bin1116 mod/data1.pak $OUT/stage/assets/st2mod/ 2>/dev/null || true     # 익산 모드(텍스트 치환 .d)
cp ../android/assets/galmuri11.ttf $OUT/stage/assets/
aapt package -f -M AndroidManifest.xml -S ../android/res -I $AJ -F $OUT/base.apk
cp $OUT/classes.dex $OUT/stage/
( cd $OUT/stage && aapt add -f ../base.apk classes.dex $(find assets -type f) >/dev/null )
zipalign -f 4 $OUT/base.apk $OUT/aligned.apk
[ -f debug.keystore ] || keytool -genkeypair -keystore debug.keystore -storepass android -keypass android -alias debug -keyalg RSA -keysize 2048 -validity 36500 -dname "CN=SeoulTycoon Debug" 2>&1 | { grep -v JAVA_TOOL || true; }
apksigner sign --ks debug.keystore --ks-pass pass:android --key-pass pass:android --out SeoulTycoon2.apk $OUT/aligned.apk 2>&1 | { grep -v JAVA_TOOL || true; }
apksigner verify SeoulTycoon2.apk 2>&1 | { grep -v JAVA_TOOL || true; }; ls -la SeoulTycoon2.apk
