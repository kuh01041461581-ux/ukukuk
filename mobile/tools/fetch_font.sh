#!/usr/bin/env bash
# Pretendard(SIL OFL) 글꼴을 내려받아 앱에 넣는다. 실패하면 조용히 넘어가 기기 기본 글꼴을 쓴다.
cd "$(dirname "$0")/.."
BASE="https://cdn.jsdelivr.net/gh/orioncactus/pretendard@v1.3.9/packages/pretendard/dist/public/static/alternative"
mkdir -p assets/fonts
ok=1
for w in Regular Medium SemiBold Bold ExtraBold; do
  curl -fsSL --retry 3 --max-time 120 -o "assets/fonts/Pretendard-$w.ttf" "$BASE/Pretendard-$w.ttf" || ok=0
done
if [ "$ok" = 1 ] && ! grep -q "family: Pretendard" pubspec.yaml; then
  python3 - <<'PY'
p = "pubspec.yaml"
s = open(p, encoding="utf-8").read()
block = """\nflutter:\n  fonts:
    - family: Pretendard
      fonts:
        - asset: assets/fonts/Pretendard-Regular.ttf
        - asset: assets/fonts/Pretendard-Medium.ttf
          weight: 500
        - asset: assets/fonts/Pretendard-SemiBold.ttf
          weight: 600
        - asset: assets/fonts/Pretendard-Bold.ttf
          weight: 700
        - asset: assets/fonts/Pretendard-ExtraBold.ttf
          weight: 800
"""
s = s.replace("\nflutter:\n", block, 1)
open(p, "w", encoding="utf-8").write(s)
print("Pretendard 글꼴 적용")
PY
else
  echo "글꼴을 받지 못해 기기 기본 글꼴을 사용합니다"
  rm -rf assets/fonts
fi
