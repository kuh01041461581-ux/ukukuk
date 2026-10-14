# 서무 업무 자동화 (suite)

서무 업무 도구 5개를 하나의 화면으로 묶은 프로그램입니다. **`서무업무자동화.html` 파일 하나**를 인터넷에 연결된 PC의 브라우저(Chrome/Edge)에서 열면 됩니다. 설치도 서버도 필요 없고, 파일 처리는 모두 브라우저 안에서만 이뤄집니다.

| 메뉴 | 도구 | 원본 |
|---|---|---|
| 여비 › 관내여비 | 관내여비 지급명세서 | `tools/travel.html` |
| 여비 › 관외출장 | 관외출장 여비지출결의서 (HWPX·엑셀) | `tools/outstation.html` |
| 정산 › 급식비 | 급식비(매식비) 정산 | `tools/meal.html` |
| 정산 › 공과금 고지서 | 고지서 AI 인식 → 거래처입력 양식 | `tools/utility.html` |
| 문서 › 얼굴 블러 | HWPX 얼굴 자동 블러 | `tools/blur.html` |

바탕화면 달력(DeskCalendar, Windows/Android)은 별도 프로그램이라 홈 화면에 내려받기 카드로만 연결했습니다.

## 구조

- `shell.html` : 껍데기(사이드바·상단바·홈·설정·다크 모드)
- `theme.css` : **공통 디자인** — 색·글꼴·모서리·버튼·입력·표·모달을 여기서 한 번에 바꿉니다
- `bridge.js` : 도구 ↔ 셸 연결(높이 맞춤, 다크 모드, 모달 위치, 공용 API 키)
- `tools/*.html` : 도구 본문. 각 도구는 iframe 안에서 돌아 서로 충돌하지 않습니다
- `build.py` : 위를 합쳐 `서무업무자동화.html` 생성

```
python3 suite/build.py
```

## 도구를 더 붙이려면

1. `tools/새도구.html` 을 만든다. `<head>` 끝에 `<meta name="tool-id" content="새도구">` 와 `<!--SUITE:THEME-->`, `</body>` 바로 앞에 `<!--SUITE:BRIDGE-->` 를 넣는다. 색은 `var(--primary)`, `var(--text-2)` 같은 `theme.css` 토큰을 쓴다.
2. `build.py` 의 `TOOL_IDS`, `shell.html` 의 `TOOLS` 에 한 줄씩 추가한다.

## API 키

소스에 키를 적지 않습니다. 왼쪽 메뉴 **설정**에서 카카오·오피넷·Gemini·Claude 키를 입력하면 이 브라우저(localStorage)에만 저장되고 각 도구가 가져다 씁니다.
