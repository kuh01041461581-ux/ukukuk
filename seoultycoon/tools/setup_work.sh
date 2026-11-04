#!/bin/bash
# original/ 의 zip 에서 작업용 폴더(work/)를 다시 만든다.  work/st1/jar = 1편 jar 내용, work/st2 = 2편 jar 내용 + data1.pak + cc2
set -e
cd "$(dirname "$0")/.."
T=$(mktemp -d)
rm -rf work && mkdir -p work/st1 work/st2
unzip -q -o original/seoul_tycoon1_wipi.zip -d $T/a && unzip -q -o $T/a/0000000007.jar -d work/st1/jar 2>/dev/null || true
cp $T/a/0000000007.msd work/st1/
unzip -q -o original/seoul_tycoon2_skt.zip -d $T/b && unzip -q -o $T/b/01036924.jar -d work/st2
cp $T/b/P/data1.pak $T/b/P/cc2 work/st2/
echo "work/st1/jar: $(ls work/st1/jar | wc -l) files, work/st2: $(ls work/st2 | wc -l) files"
