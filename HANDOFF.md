# 인수인계 (새 작업이 이어받을 때 먼저 읽기)

브랜치: `claude/desktop-widget-todo-app-ac7twd` (PR 은 사용자가 요청할 때만). 커밋 메시지 끝에 항상:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_012anZ1Bdp8r3prMqVkFTmB4
```

## 무엇인가
공무원용 일정 앱. 사용자는 개발 환경이 없어서 **빌드는 전부 GitHub Actions**, 결과는 Releases 로 받는다.
- **PC(Windows)**: `desk_calendar.py` (Python tkinter, 바탕화면 위젯 달력 + 할 일/메모), `glass.py`(유리형 배경), `sticky.py`(바탕화면 스티커 메모), `work.py`(업무 폴더·D-day), `hwpdoc.py`(한글/docx 읽기), `gongmun.py`·`analyze.py`·`quickadd.py`(공문·붙여넣기·빠른 입력 해석), `core.py`(데이터·동기화·반복), `cloudsync.py`·`relay/worker.js`(암호화 동기화)
- **안드로이드/아이폰**: `mobile/` (Flutter 3.24.5). 홈 화면 위젯 3종은 `mobile/android/app/src/main/` 의 코틀린 + res
- **아이폰 웹앱**: `https://kuh01041461581-ux.github.io/ukukuk/` (Build Web 워크플로가 Pages 에 배포, PWA/오프라인 지원)
- **Python ↔ Dart 는 같은 규칙**: tests/*.json 픽스처로 맞춘다 (quickadd, gongmun, analyze, core). 한쪽을 바꾸면 반대쪽과 픽스처도 같이.

## 릴리스 (Releases 의 고정 태그)
`latest-build`(PC exe + DeskCalendar-Setup.exe), `latest-android`(APK), `latest-ios`(IPA 서명 전), `latest-web`(웹 zip), `latest-web-safari`(사파리 엔진 시험 화면).
(`latest-suite`, `latest-seomu` 는 다른 작업 브랜치의 산출물이라 이 앱과 무관)

## 지켜야 할 것
- 사용자의 공공데이터 **공휴일 API 키 커밋 금지**(설정 화면에서 입력, 로컬 저장). 토큰·키를 코드에 넣지 말 것. 안드로이드 keystore 는 커밋돼 있음(알려진 절충).
- **실제 공문 내용 커밋 금지** — 시험은 가짜 문서만. 연결 코드/QR 노출 금지.
- 업무 기능은 **폴더·D-day 만**. 업무 보고, 제출 현황, 양식 배포/취합은 만들지 않는다(사용자는 취합 부서가 아니라 과 구성원).
- 시험이 사용자 실제 데이터(%APPDATA%\DeskCalendar)를 건드리지 않게 임시 폴더를 쓴다(tests/test_sticky_inbox.py 참고).

## 디자인 방향 (사용자가 계속 강조)
- 깔끔하고 차분하게: **달력 칸과 일정만 보이고 둘러싼 테두리는 없앤다**. 남기는 것: 오늘 칸, 상단 도구줄, 선택한 칸 외곽선. 유리형·카드형 공통.
- 버튼은 둥글고 세련되게, 호버·눌림 반응(PC `Pill` 클래스, 폰 `Pressable`+햅틱).
- 공문 가져온 일정 이름은 "주제 + 무엇을 제출하는지/종류"(예: "공유재산 실태조사 제출기한"), 원문 문장은 메모에.
- 스티커: 날짜 스티커 + 날짜 없는 칸(보관함) 스티커, 일정이 없으면 포스트잇처럼 메모만, 머리 줄 ＋ 로 새 스티커, 접은 건 한 줄 고정(켤 때도 접힌 채/펼친 채 그대로 제자리).

- 글꼴: 오픈소스 **노토 산스(Noto Sans CJK KR, SIL OFL)** 를 앱에 넣어 기본으로 쓴다. `assets/fonts`(PC, `AddFontResourceExW` 로 프로그램 안에서만 등록, PyInstaller `--add-data`) / `mobile/assets/fonts`(pubspec `NotoSansKR`; 예전 Pretendard 내려받기 스크립트 `fetch_font.sh` 는 충돌해서 없앰). 한글·영문·기호만 추린 부분집합(각 1.7MB). 못 쓰면 맑은 고딕.
- 유리형 머리 줄(달 이름·버튼·요일)은 투명으로 두면 바탕화면에 묻혀 안 보이므로 칸과 같은 은은한 유리 띠를 깐다(테두리 없음).
- 공문 일정 이름·메모는 **줄이지 않는다**(`…` 로 자르는 코드를 없앰). 화면에서 자리가 모자랄 때만 달력 칸이 `…` 로 줄여 보인다.

- 입력 칸 글자 크기: 한글 조합 중인 글자는 윈도우 입력기가 따로 그려 칸 글자와 크기가 달랐다 → `sync_ime_font()` 가 Entry/Text 포커스·키 입력 때 입력기에 칸의 글꼴을 알려 줌(윈도우만, 실기기 확인 필요). 스티커의 완료 줄도 일반 줄과 같은 크기(7pt)로 맞춤.
- 스티커는 작업표시줄에 안 보이게 `WS_EX_TOOLWINDOW` + **보이지 않는 주인 창 하나의 소유 창**(`StickyManager.owner_hwnd`, 소유된 창은 작업표시줄 단추가 없음)으로 만들고, Tk 기본 깃털 아이콘 대신 `assets/app.ico` 를 기본 아이콘으로 쓴다(PyInstaller `--add-data assets;assets`). 트레이 메뉴에 '스티커 메모 새로 만들기 / 모두 숨기기·보이기'가 있다(실기기 확인 필요).

- 메모 연동: 스티커 메모 ↔ 오른쪽 패널 메모는 같은 데이터(날짜별 memo)를 본다. `_commit` 이 스티커와 패널 메모 칸을 함께 갱신(`_pull_panel_memo`), 패널은 이 칸에서 고친 적이 있을 때만 저장(`edit_modified`)해서 옛 글로 덮어쓰지 않는다. 시험: tests/test_memo_sync.py

- 스티커 닫기/열기: 닫은 스티커는 `conf["stickies_closed"]` 에 자리·설정째 남고, '스티커' 버튼은 닫은 것들을 한꺼번에 다시 띄우고 **새 스티커도 하나 더** 붙인다. ☰ 메뉴·트레이의 '모두 열기'는 닫은 것만 되살리고 새로 만들지 않는다(스티커가 하나도 없을 때만 새로).

- 메모 서식(스티커·오른쪽 패널 공용 `sticky.MemoFormat`): 저장은 폰·검색과 같은 글자(`**굵게**` `~~취소~~` `{빨강|글}` `1.` `- ` `- [ ]`)지만 화면에서는 표시를 감추고 진짜 서식(굵게, ☐/☑ 누르면 토글+취소선, •, 번호 이어쓰기, `- `·`[] ` 손으로 치면 자동 변환, Ctrl+B)으로 보여 준다. `get_markup()`/`set_markup()` 으로 오간다. 달력 칸 미리보기는 `strip_markup()`. 폰 앱도 같은 규칙: `mobile/lib/memo_rich.dart` 의 `RichMemoController`(글자마다 서식 배열을 들고 있는 TextEditingController, 한글 조합·Enter 이어쓰기·`- `/`[] ` 자동 변환·체크 누르기)와 `MemoEditor`(서식 버튼 줄+입력 칸)를 날짜 시트 메모·메모 대화상자에 쓴다. 시험: mobile/test/memo_rich_test.dart. 저장 글자 규칙을 바꾸면 PC(sticky.py)와 폰(memo_rich.dart) 양쪽을 같이.

- 메모 지움 동기화: 메모를 지워도 그날 항목에 `memo_ts` 를 남기고(90일) 동기화 때 빈 메모도 ts 와 함께 내보낸다(`core.export_state` / `core.dart exportState`). 안 그러면 다른 기기의 옛 메모가 되살아난다. 시험: tests/test_memo_revive.py, mobile/test/memo_revive_test.dart.
- 스티커 접기/펴기는 시간 기반 easeInOutCubic(약 0.28초), 안쪽 내용은 `place` 로 크기를 고정해 두고 창만 움직인다. 스티커마다 글자 크기 `conf["fs"]`(0.7~2.4): 도구줄 A＋/A－, 메모 위 Ctrl+휠, 우클릭 메뉴 '글자 크기'.
- 카드형 머리 줄에도 '스티커' 버튼(유리형과 같은 `open_all`).

- 공문 일정 이름(사용자가 실제 공문 2건으로 확인): 제목 자체가 '… 제출(요청)' 이고 제출기한이 하나면 **제목에서 연도·'요청'만 뺀 것이 그대로 일정 이름**(`submit_title` / `submitTitle`; 예 '제10차 ○○위원회 심의안건 제출', '공유재산 실태조사 결과 제출'). '관련문서 ○○과-991(2026.3.16.)호' 같은 문서번호 옆 발송 날짜는 일정으로 넣지 않는다(`_is_doc_ref_date`). 실제 공문은 커밋하지 않고 가짜 문서로만 시험(tests/gongmun_cases.json).
- 달력에서 마우스 휠로 주·월이 넘어가지 않는다(Ctrl+휠은 글자 크기). 주 이동은 오늘 옆 ‹ › 로만.

- 스티커는 자리마다 **한 장**: 날짜 스티커는 날짜마다, '오늘'은 오늘 날짜로 세고, 날짜 없는 칸은 하나(`StickyManager.key_of`). `add()` 는 이미 있으면 새로 만들지 않고 앞으로 가져온다. 머리 줄 ＋ 는 다음 날짜(스티커 없는 첫 날짜, `add_next`). 닫을 때 내용(메모·일정)이 있는 스티커만 닫아 둔 목록에 남는다. 켤 때 중복 스티커는 정리(`_dedupe`).

- 빠른 입력(Ctrl+Alt+Space): 달력을 앞으로 불러오지 않고 `CommandBar(anywhere=True)` 를 마우스가 있는 모니터(`work_area_at`)에 띄운다. 이미 열려 있으면 닫힘. 단축키가 이미 쓰이면 Ctrl+Shift+Space → Ctrl+Alt+Q 로 등록하고 알림. 입력 해석은 quickadd.py/quickadd.dart(붙여 쓴 날짜 261024·20261024, 끝에 붙인 중요/긴급/낮음). 시험: tests/test_quickbar.py, tests/quickadd_cases.json.
- 날짜 상세 창(DayDetail, 더블클릭)은 일정이 많을 때 어느 줄 위에서든 휠로 스크롤(`_bind_wheel_tree`), 창도 더 크게. 오른쪽 패널·말풍선은 기존 그대로(말풍선은 14개까지, '날짜를 눌러 전체 보기').

## 실행·시험
```
python tests/test_fixtures.py ; python tests/test_work.py ; python tests/test_sticky.py ; python tests/test_sticky_inbox.py   # 뒤 둘은 Tk 필요
cd mobile && flutter test        # 17개
```
클라우드(리눅스)에선 `xvfb-run -a python3.12 ...` 로 화면 없이 UI 시험. 안드로이드 위젯·윈도우 실제 화면은 **사용자 PC/폰에서만** 확인 가능.

## 상태 (정직하게)
- 실기기에서 아직 못 본 것: 안드로이드 위젯 3종(크기 조절·눌림·마감 임박·＋버튼), 폰 앱 테두리 없앤 화면·햅틱, PC 알약 버튼 호버/눌림·칸 호버 글로우. 코틀린은 CI 에서 컴파일 성공까지만 확인.
- 로컬(사용자 PC) 세션에서 한 것: 스티커 메모(서식 포함), 공문 일정 이름에 제출물 표시, 사용법 창 글씨, 배경 판 제거.
- 공문 규칙은 실제 공문이 필요: 사용자가 공문 문단과 원하는 이름을 주면 gongmun.py / mobile/lib/gongmun.dart 와 픽스처를 같이 보강.
- 폰 앱의 "배경 없애기"는 대상이 불명확해 안 건드림(스크린샷 필요).

## 다음에 할 것 (사용자 요청: 더 직관적이고 사용감 좋게)
- 스티커·달력·폰 화면을 처음 쓰는 사람도 헤매지 않게: 숨은 기능(우클릭·더블클릭) 발견성, 빈 상태 안내, 버튼 이름 일관성.
- 실제 화면(PC·에뮬레이터)에서 눈으로 보고 다듬기 — 로컬 세션에서 진행하는 게 좋다.
