#!/bin/bash
# 클라우드 세션 시작 때 PC 쪽 시험(공문·한글·업무 규칙)을 돌릴 수 있게 파이썬 부품을 설치한다
set -euo pipefail
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi
cd "${CLAUDE_PROJECT_DIR:-.}"
PY=python3
command -v python3.12 >/dev/null 2>&1 && PY=python3.12   # tkinter 가 있는 쪽 (화면 시험용)
# pyinstaller 는 exe 빌드 전용(깃허브에서 함)이라 시험에는 넣지 않는다
grep -v -i '^pyinstaller' requirements.txt > /tmp/req-test.txt
$PY -m pip install --quiet --break-system-packages -r /tmp/req-test.txt pyflakes 2>&1 | tail -3 || true
echo "export PYTHONIOENCODING=utf-8" >> "${CLAUDE_ENV_FILE:-/dev/null}"
