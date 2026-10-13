# DeskCalendar

바탕화면 위젯형 달력 + 할 일 / 메모. 디자인: Notion · Linear · Windows 11 풍 다크 테마.

## exe 받기 (Windows)
GitHub **Actions → Build Windows exe → 최신 실행 → Artifacts → DeskCalendar-windows** 에서 `DeskCalendar.exe` 를 내려받아 실행합니다.
(직접 빌드하려면 Windows에서 `build.bat` 실행 → `dist\DeskCalendar.exe`)

## 사용법
- **Ctrl+Alt+Space** : 어디서든 위젯을 앞으로 불러 오늘 날짜에 바로 입력
- 입력창에 `14:30 팀 회의` 처럼 쓰면 시간 태그로 저장
- 할 일 더블클릭 = 수정, 체크 = 완료, 달력 휠 = 월 이동
- 우클릭 메뉴: 위치 고정, 투명도, 자동 실행, 종료
- 데이터: `%APPDATA%\DeskCalendar\data.json`

## 로드맵
1. 모바일 앱 + QR 코드 페어링 동기화
2. 온나라 공문 붙여넣기 → 일정 자동 등록
