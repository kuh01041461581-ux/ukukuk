# 익산타이쿤 코어 (v0.1)

- `src/engine.js` — UI 와 무관한 코어(브라우저/Node 공용): 월 진행, 예산(시 수입+정부지원), 정치력, 피로·입원, 시 밖 방문(도청/국회), 뒷돈·감사·탄핵, 4년 임기 선거·연임(최대 3기=12년)·엔딩 선택.
- `data/config.json`(수치·권역), `data/events.json`(달력/랜덤/뒷돈 이벤트; 개최월 미검증 행사는 `month:null, needsVerification` 이라 엔진이 건너뜀).
- `web/` — 최소 UI(모바일 우선, Galmuri11 도트 글꼴, OFL). 실행: `python3 -m http.server --directory . 8765` 후 `http://localhost:8765/web/index.html` (`?seed=숫자` 로 시드 고정).
- 테스트: `node test/sim.test.mjs` (30시드 자동 플레이: 청렴=연임·완주, 방치=낙선, 뒷돈=대부분 탄핵 등 불변식·밸런스 검증).
- 다음: 도트 타일 맵(권역·구역), 이벤트 확충, 정책/비서, APK 래퍼.
