# 서무 종합프로그램 (소스)

`python3 seomu/build.py` → `seomu/dist/서무종합프로그램.html` (단일 파일, 브라우저에서 바로 열기)

- `shell.html` 화면 틀(사이드바·탭) / `tools/*.html` 각 도구 / `shared/` 공용 코드(이름 걸러내기, 기간제 계산, 공휴일, 공통 디자인, 양식 템플릿)
- 새 도구 **기간제 인부임**(`tools/wage.html`, Alt+9): 명부 엑셀 + 4대보험 고지서 → 최저시급 계산 → 지급명세서 엑셀(임금지급 명세서·은행이체·개인별), 일일점검부, 청구서. 이호조 거래처입력 엑셀(`shared/ihojo_template.xlsx`, `tests/make_ihojo_template.py`)과 한글(HWPX) 일일점검부·청구서(`shared/hwpx.js`, 한글이 만든 빈 문서 골격 `shared/skeleton.hwpx` 위에 표를 얹음 — python-hwpx 의 Skeleton.hwpx, Apache-2.0)도 만든다. HWPX 는 `hwpx-validate` 스키마 검증까지 확인했고 실제 한글 프로그램에서 열어 보지는 못했다.
- `shared/wage_template.xlsx` 는 사용자 양식에서 개인정보를 지우고 서식만 남긴 것(`tests/make_template.py`).
- 시험(가짜 데이터): `node tests/test_personname.js`, `NODE_PATH=<xlsx 설치 폴더> node tests/test_wagecalc.js`, Playwright `tests/e2e_wage.mjs`, `tests/e2e_outtrip.mjs` (`python3 tests/make_fixtures.py` 로 시험 파일 생성)
- 실제 주민번호·계좌·고지서는 커밋하지 않는다. 주민번호 뒷자리는 읽자마자 버린다.

## 급여 확정 알고리즘 (기간제 인부임)
사용자가 준 2026.10 지급명세서·청구서·이호조 양식에서 읽어 낸 규칙이며, 화면의 "급여는 이렇게 확정됩니다"와 각 근로자의 "계산 근거"에 같은 내용이 나온다.
1. 근무일 = 근로 요일 − 휴관 요일 (공휴일 근무 여부·달력 수정·이 달 입사일 반영)
2. 1일 단가 = 최저시급 × 1일 소정근로시간(7) · 기본급 = 근무일 × 1일 단가
3. 주휴수당 1주 = 주 소정근로시간/40 × 8 × 시급(10원 미만 절사), 주 15시간 이상 · 그 주 소정근로일이 이 달 안에 모두 있고 개근한 주만
4. 월차수당 = 주휴 대상자가 한 달 개근, 입사 1년 미만이면 1일분 · 산출액(청구금액) = 기본급 + 주휴 + 월차 + 추가근무
5. 4대보험 = 고지서(이름+생년월일)에서 근로자 부담(공제)·사업장 부담
6. **소득세 = 근로소득 간이세액표**(소득세법 시행령 별표 2, `shared/taxtable.js`)에서 월급여(=산출액) 천원 구간 × 공제대상가족 수(8~20세 자녀는 1명당 +1). 샘플 1,653,940원·가족 1명 → 12,020원이 표와 정확히 일치. 지방소득세 = 소득세 × 10%(10원 미만 절사) = 1,200원
7. 실지급액 = 산출액 − (건강+연금+고용+소득세+지방소득세) · 총 지출 = 산출액 + 사업장 부담
8. 서류: 지급명세서 엑셀 · 이호조 거래처입력 엑셀 · 일일점검부/청구서(한글·인쇄) — 같은 계산값
간이세액표 갱신: `pdftotext -layout 별표2.pdf t.txt && python3 seomu/tests/make_taxtable.py t.txt`
