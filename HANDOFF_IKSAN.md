# 익산타이쿤 인수인계서 (루트 `HANDOFF.md`는 다른 프로젝트 것 — 무시)

## 0. 새 세션 첫 메시지 (그대로 붙여넣기)
> `ccr-21696a78-hbj0yc` 브랜치를 체크아웃하고 저장소 루트의 `HANDOFF_IKSAN.md`를 읽고 이어서 진행해줘. 루트 `HANDOFF.md`는 다른 프로젝트라 무시. 다음 순서: ① 익산 모드 APK 실기 확인 ② 대사 텍스트 치환 확대 ③ 랜드마크 스프라이트 제작 ④ 신규 시스템 오버레이.

## 1. 저장소·규칙
- 저장소 `kuh01041461581-ux/ukukuk`, 작업 브랜치 **`ccr-21696a78-hbj0yc`** (기본 브랜치 아님!). PR 금지(요청 시만).
- 커밋 끝줄: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` / `Claude-Session: <세션 URL>`
- 답변은 한국어. APK 빌드 후 SendUserFile로 전달. 원작 그래픽·텍스트는 개인용(공개 배포 금지, 추출물은 gitignore).

## 2. 목표 (확정, 사용자 합의)
웹판(`iksan/`)은 "웹방식은 별로"라 **보류**. 원래 기획 = **서울타이쿤 도트 활용 + 익산 랜드마크 추가**.
진짜 서울타이쿤2(ST2)를 우리가 만든 Java ARM-Thumb VM에서 돌리고 데이터/에셋을 모딩한다.
단계: ① 리소스 오버라이드+텍스트 재패킹 도구(완료) → ② 지명/대사 익산화(진행: 지명 일부) → ③ 새로 그리는 건 몇 개 랜드마크만(미륵사지 석탑, 왕궁리 오층석탑, 익산역 등)을 ST2 팔레트·크기에 맞춰 교체 → ④ 호스트 쪽 오버레이 시스템: 선거/4년 임기 후 투표·연임 최대 2회(12년)·엔딩/계속, 뒷돈→감사→탄핵, 정치력, 국회·도청 방문/예산확보, 피로, 실제 달력 연동 익산 행사.
UX 요구: 권역별 맵 분리, 스크롤 맵 금지, 시청에서 지표(피로/지지도/예산/자재/인력) 확인 + 커맨드 일괄 실행.
사용자 질문 "지도모양도 익산으로?" 답: 배경 청크 그림 교체는 가능, 부지 배치·권역 수 변경은 부지 데이터 미해독이라 어려움.

## 3. 현재 상태
- 완료: ST1 APK(J2ME 호환), ST2 APK(`seoultycoon/st2android/SeoulTycoon2.apk`; 속도설정, 십자키 모드, 시청 팝업 수정), JVM 하니스, 익산 모드 1차.
- 익산 모드 1차 APK 전달함 — **실기 확인 대기**(지역바 "동부|서동공원" 등). JVM에서 패치 이미지 부팅은 확인, 지역바 화면 자체는 미확인.
- 아직 서울: 대사의 "시장님" 등, `.m` 이미지에 박힌 글자(건물 메뉴명 등).

## 4. 디렉터리 (모두 `seoultycoon/` 아래)
- `st2android/src/com/seoultycoon/st2/` ThumbVm(인터프리터)·Host(클래스링크/객체모델/스레드)·Lib(java.*)·GfxLib(lcdui)·Surf(소프트웨어 그래픽)·Platform
- `st2android/app/…/St2Activity, AndroidPlatform` 안드로이드 앱. 리소스 우선순위: assets `st2mod/` → `st2/`. 이미지도 `pf.resource("client.bin1116")`.
- `st2android/test/JvmRun.java` JVM 하니스(env: MOD, SHOT, VERBOSE, DT, WATCH…).
- `st2android/build_apk.sh` 빌드(aapt/dx/apksigner/zipalign, `tools/Delambda.java`로 람다 제거, 디버그 키스토어).
- `tools/st2_mod_build.py` + `st2android/mod/text_map.json`:
  - `replace`: 텍스트 `.d`(u8[32] 길이표 + CP949) 치환·재패킹. 문자열 255바이트 초과/개수≠32면 오류.
  - `image_replace`: `client.bin1116` 내 UTF-16LE 문자열을 **같은 글자 수로만** 치환(오프셋 보존).
  - 산출물 `st2android/mod/*.d`, `client.bin1116` (gitignore).
- `docs/PROJECT_NOTES.md`, `docs/ST2_ANALYSIS.md` 상세 분석. `work/`(gitignore)에 추출 원본(`work/st2/…`) — 없으면 PROJECT_NOTES 절차로 재추출.
- `iksan/` 웹 프로토타입(보류, 조사 문서 `iksan/docs/IKSAN_TYCOON_DESIGN.md`에 지리·행사·랜드마크 사실 정리됨 → 재활용).

## 5. 기술 메모
- ST2 = WIPI Java AOT→ARM Thumb 이미지(BASE 0x1000000). 지역/장소명은 이미지 내 UTF-16LE 상수(u16 길이+문자열).
- `.m` = PNG 스프라이트시트(45.m 상단바, 40.m 라벨, 41.m 아이콘, 17.m 바, 42.m 현황 라벨…), `.d` = 텍스트/데이터/MMF(s1–s9→MIDI), `data1.pak`, 세이브 `cc2`.
- 권역뷰 = 64x64 배경 청크 4x4 + 건물 스프라이트(30x81 등) 고정 좌표.
- 상태 int[12] {년,월,일,200,10,10,0,50,0,0,1459,0}; 날짜는 int[12] 배열 중 유효한 최신값 선택.
- 키: ST2 숫자패드(2=50 상,8=56 하,4=52,6=54,5=53 확인,0=48 취소,#=지도,*=정보).
- 함정: ARM `bx pc` 스텁 → 매직주소 호스트함수; 클래스 vtable 전파 금지(isObj=false); 람다 → Delambda 필수; JvmRun은 OUT 폴더를 매번 새로(cc2 잔존); 쉘 변수는 호출 간 유지 안 됨.
- 테스트 예: `javac -d /tmp/jt st2android/src/com/seoultycoon/st2/*.java st2android/test/JvmRun.java; MOD=$PWD/st2android/mod java -cp /tmp/jt JvmRun work/st2/client.bin1116 work/st2 OUT st2android/…/galmuri11.ttf 700 "150:1:53,152:2:53"` (키 형식 라운드:타입:키).

## 6. 다음 할 일
1. 사용자 실기 피드백 반영(지역바/건물명).
2. `text_map.json` 확대: 대사·직함(시장→?) 등, 이미지 문자열 후보는 `client.bin1116`에서 UTF-16LE 검색.
3. 랜드마크 스프라이트: 미륵사지 석탑(사용자 사진 기준 6층, 윗부분 비대칭 붕괴·동탑 9층·목탑지), 왕궁리 오층석탑+궁성, 익산역(기존역 개축), 춘포 구역사 등. 사진은 사용자가 제공(웹 이미지 열람 불가). ST2 팔레트/크기에 맞춤.
4. 오버레이 시스템 설계(호스트 훅).
5. 세션 환경: 네트워크 허용 목록(ko/en.wikipedia.org, iksan.go.kr, data.go.kr, namu.wiki, k-calc.com) 새 세션에서 적용 필요; Artifact 미리보기 선택.
