#!/bin/bash
# 시장 얼굴·상체 조각만 다시 만든다(환경변수로 판별 설정: BODY, BODY_SCALE, NECK_EXT, NECK_W). base_head 는 표정 조각을 직접 고치므로 항상 gpt_import 부터 다시 돈다.
set -e
cd "$(dirname "$0")"
python3 -I gpt_import.py >/dev/null; python3 -I faces_hd.py >/dev/null; python3 -I body_final.py >/dev/null; python3 -I base_head.py >/dev/null; python3 -I male_fit.py >/dev/null
