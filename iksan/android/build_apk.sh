#!/bin/bash
# 익산타이쿤 APK (WebView 래퍼). apt: aapt apksigner dalvik-exchange(dx) zipalign libandroid-23-java
set -e; set -o pipefail
cd "$(dirname "$0")"
OUT=build; rm -rf $OUT; mkdir -p $OUT/classes $OUT/stage/assets/www
AJ=/usr/lib/android-sdk/platforms/android-23/android.jar
javac -nowarn -source 8 -target 8 -encoding UTF-8 -cp $AJ -d $OUT/classes $(find src -name '*.java') 2>&1 | { grep -v "JAVA_TOOL\|warning\|^Note" || true; }
/usr/lib/android-sdk/build-tools/debian/dx --dex --output=$OUT/classes.dex $OUT/classes 2>&1 | { grep -v JAVA_TOOL || true; }
G=../game; mkdir -p $OUT/stage/assets/www/{src,data,web}
cp $G/src/*.js $OUT/stage/assets/www/src/; cp $G/data/*.json $OUT/stage/assets/www/data/; cp $G/web/* $OUT/stage/assets/www/web/
aapt package -f -M AndroidManifest.xml -S res -I $AJ -F $OUT/base.apk
cp $OUT/classes.dex $OUT/stage/
( cd $OUT/stage && aapt add -f ../base.apk classes.dex $(find assets -type f) >/dev/null )
zipalign -f 4 $OUT/base.apk $OUT/aligned.apk
[ -f debug.keystore ] || keytool -genkeypair -keystore debug.keystore -storepass android -keypass android -alias debug -keyalg RSA -keysize 2048 -validity 36500 -dname "CN=IksanTycoon Debug" 2>&1 | { grep -v JAVA_TOOL || true; }
apksigner sign --ks debug.keystore --ks-pass pass:android --key-pass pass:android --out IksanTycoon.apk $OUT/aligned.apk 2>&1 | { grep -v JAVA_TOOL || true; }
apksigner verify IksanTycoon.apk 2>&1 | { grep -v JAVA_TOOL || true; }; ls -la IksanTycoon.apk
