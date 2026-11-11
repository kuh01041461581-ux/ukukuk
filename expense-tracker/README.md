# 일상경비·공공요금 관리 (Windows 프로그램)

더블클릭으로 실행하는 exe 프로그램입니다(설치 불필요, Windows 10/11 + Edge WebView2).

- **일상경비 집행현황**: 사무관리비·공공운영비·업무추진비·여비 — 지출액 입력, 집행률·잔액·막대그래프, 행별 지급예정 메모
- **공공요금 집행현황**: 전기·상하수도·통신 — 월별 고지서 수령 체크, 금액 입력, 자동 합계, 계약·납부번호(누르면 복사), 월별 그래프
- **불러오기**: 합본예산서(PDF) → 총예산액·공공요금 예산 / 통계목별 조회(엑셀) → 실집행액·원인행위

데이터는 `%APPDATA%\일상경비관리\data.json`에 자동 저장되고 하루 한 번 `backups\`에 백업됩니다.
처음 실행하면 기존 엑셀(일상경비·공공요금)에서 항목·번호·금액을 가져오는 버튼이 나옵니다.
계약번호·계좌번호는 exe에 들어 있지 않고, 사용자가 엑셀에서 직접 가져옵니다(공개 저장소에 개인 데이터가 올라가지 않음).

## 받는 곳
저장소 **Releases → 일상경비·공공요금 관리 최신 빌드 (exe)** 의 `ExpenseManager.exe`
(Actions 탭 → Build 일상경비관리 exe 에서도 받을 수 있습니다)

## 개발
```bash
pip install -r requirements.txt
python tests/test_api.py      # 저장·엑셀 읽기 시험
python build.py               # app/ui.html 생성 (화면)
python app.py                 # 창으로 실행
python make_icon.py           # 아이콘(assets/icon.ico) 다시 만들기 (Pillow 필요)
```
화면은 `src/` (HTML 틀 `app.template.html`, `style.css`, `js/*.js`), PDF 읽기는 `vendor/` 의 PDF.js(Apache-2.0).
