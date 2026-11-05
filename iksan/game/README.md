# 익산타이쿤 코어 (v0.1)

- `src/engine.js` — UI 와 무관한 코어(브라우저/Node 공용): 월 진행, 예산(시 수입+정부지원), 정치력, 피로·입원, 시 밖 방문(도청/국회), 뒷돈·감사·탄핵, 4년 임기 선거·연임(최대 3기=12년)·엔딩 선택.
- `data/config.json`(수치·권역), `data/events.json`(달력/랜덤/뒷돈 이벤트; 개최월 미검증 행사는 `month:null, needsVerification` 이라 엔진이 건너뜀).
- `web/` — 최소 UI(모바일 우선, Galmuri11 도트 글꼴, OFL). 실행: `python3 -m http.server --directory . 8765` 후 `http://localhost:8765/web/index.html` (`?seed=숫자` 로 시드 고정).
- 테스트: `node test/sim.test.mjs` (30시드 자동 플레이: 청렴=연임·완주, 방치=낙선, 뒷돈=대부분 탄핵 등 불변식·밸런스 검증).
- 다음: 도트 타일 맵(권역·구역), 이벤트 확충, 정책/비서, APK 래퍼.

## v0.2 추가
- 타이틀 → **시장 선택(남/여 + 이름)** → 게임. `Game({player:{gender,name}})`.
- **랜드마크**: 미륵사지·왕궁리·익산역 앞 부지는 처음에 *유적터/빈 부지*, `restore_mireuk`/`restore_wanggung`/`station_dev` 로 복원·개발하면 석탑·역사가 지도에 나타남(일반 건물은 이 부지에 지을 수 없음).
- 지도: 장식 건물·나무, 3종 풀/산, 도로 차선, 물결 애니메이션(절차적 도트). `web/sprites.js`, `web/map.js`.
- **컷신**: 이벤트·선거·엔딩마다 `scene` id(`data/events.json`)로 16:9 도트 장면 재생(`web/cutscenes.js`, 192x108 논리 해상도, 남/여 시장·비서 초상 포함). 현재는 절차적 초안이며 정식 일러스트로 교체 예정(SCENES 함수 단위).
- 디버그: `?skip=1` 로 타이틀 생략, `window.__game()` 로 엔진 접근.
