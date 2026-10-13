# DeskCalendar 중계 서버 만들기 (무료, 약 10분)

PC 와 폰이 인터넷으로 일정을 주고받을 때 중간에서 **암호화된 덩어리만** 보관하는 작은 서버입니다.
암호 키는 서버에 없고 QR 로만 전달되므로, 서버 운영자도 내용을 볼 수 없습니다.

## 만드는 법 (Cloudflare 무료 계정)
1. https://dash.cloudflare.com 에서 무료 가입 (이메일만 있으면 됨, 카드 불필요)
2. 왼쪽 메뉴 **Storage & Databases → KV → Create** → 이름 `deskcalendar` 로 만들기
3. 왼쪽 메뉴 **Workers & Pages → Create → Hello World(Worker)** → 이름 `deskcalendar-relay` → **Deploy**
4. 이어서 **Edit code** → 기존 코드를 모두 지우고 `relay/worker.js` 내용을 붙여 넣기 → **Deploy**
5. 해당 Worker 의 **Settings → Bindings → Add → KV namespace**
   - Variable name: `KV` (대문자 그대로)
   - KV namespace: 2번에서 만든 `deskcalendar` → **Deploy**
6. Worker 주소(`https://deskcalendar-relay.<내이름>.workers.dev`)를 브라우저로 열어 `{"ok":true,...}` 가 보이면 완료

## 무료 한도
하루 요청 10만 건, KV 쓰기 하루 1,000건 — 개인/소규모 부서 일정 동기화에는 충분합니다.
