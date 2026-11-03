#!/bin/bash
# 서울타이쿤1 안드로이드 APK 빌드 (apt: aapt apksigner dalvik-exchange zipalign libandroid-23-java 필요)
set -e
cd "$(dirname "$0")"
OUT=build; rm -rf $OUT; mkdir -p $OUT/classes
AJ=/usr/lib/android-sdk/platforms/android-23/android.jar
javac -nowarn -source 8 -target 8 -encoding UTF-8 -cp $AJ -d $OUT/classes $(find src -name '*.java') 2>&1 | { grep -v "JAVA_TOOL\|warning\|^Note" || true; }
# 원본 게임 클래스(ref: ../work/st1/jar) 를 함께 dex 로
mkdir -p $OUT/orig && cp ../work/st1/jar/*.class $OUT/orig/
# 한글 깨짐 방지: new String(byte[])/getBytes() 를 EUC-KR 고정 도우미(Cp)로 치환 (ASM)
ASM=/usr/share/java/asm-9.7.jar:/usr/share/java/asm-tree-9.7.jar
mkdir -p $OUT/tool && javac -nowarn -cp $ASM -d $OUT/tool ../tools/PatchCharset.java 2>&1 | { grep -v JAVA_TOOL || true; }
java -cp $OUT/tool:$ASM PatchCharset $OUT/orig 2>&1 | { grep -v JAVA_TOOL || true; }
dalvik-exchange --dex --no-strict --output=$OUT/classes.dex $OUT/classes $OUT/orig 2>&1 | { grep -v JAVA_TOOL || true; }
# 리소스(.d) 는 APK 루트에 둔다 (Class.getResourceAsStream 용)
aapt package -f -M AndroidManifest.xml -S res -I $AJ -F $OUT/base.apk
cp $OUT/classes.dex $OUT/orig/ 2>/dev/null; cp $OUT/classes.dex $OUT/stage_classes.dex
( cd $OUT && mkdir -p stage && cp classes.dex stage/ && cp ../../work/st1/jar/*.d stage/ && python3 -I ../../tools/convert_audio_res.py stage && mkdir -p stage/assets && cp ../assets/galmuri11.ttf stage/assets/ && cd stage && aapt add -f ../base.apk classes.dex *.d assets/galmuri11.ttf >/dev/null )
zipalign -f 4 $OUT/base.apk $OUT/aligned.apk
[ -f debug.keystore ] || keytool -genkeypair -keystore debug.keystore -storepass android -keypass android -alias debug -keyalg RSA -keysize 2048 -validity 36500 -dname "CN=SeoulTycoon Debug" 2>&1 | { grep -v JAVA_TOOL || true; }
apksigner sign --ks debug.keystore --ks-pass pass:android --key-pass pass:android --out SeoulTycoon1.apk $OUT/aligned.apk 2>&1 | { grep -v JAVA_TOOL || true; }
apksigner verify SeoulTycoon1.apk 2>&1 | { grep -v JAVA_TOOL || true; }; ls -la SeoulTycoon1.apk
