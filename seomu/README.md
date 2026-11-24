# 서무 종합프로그램 (소스)

`python3 seomu/build.py` → `seomu/dist/서무종합프로그램.html` (단일 파일, 브라우저에서 바로 열기)

- `shell.html` 화면 틀(사이드바·탭) / `tools/*.html` 각 도구 / `shared/` 공용 코드(이름 걸러내기, 기간제 계산, 공휴일, 공통 디자인, 양식 템플릿)
- 새 도구 **기간제 인부임**(`tools/wage.html`, Alt+9): 명부 엑셀 + 4대보험 고지서 → 최저시급 계산 → 지급명세서 엑셀(임금지급 명세서·은행이체·개인별), 일일점검부, 청구서. 이호조 지급폼은 양식 대기.
- `shared/wage_template.xlsx` 는 사용자 양식에서 개인정보를 지우고 서식만 남긴 것(`tests/make_template.py`).
- 시험(가짜 데이터): `node tests/test_personname.js`, `NODE_PATH=<xlsx 설치 폴더> node tests/test_wagecalc.js`, Playwright `tests/e2e_wage.mjs`, `tests/e2e_outtrip.mjs` (`python3 tests/make_fixtures.py` 로 시험 파일 생성)
- 실제 주민번호·계좌·고지서는 커밋하지 않는다. 주민번호 뒷자리는 읽자마자 버린다.
