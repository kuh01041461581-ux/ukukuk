# 인수인계 (새 세션용)

## 목표 (최신·확정)
웹판(`iksan/`)은 **보류**. 진짜 서울타이쿤2를 우리 Java ARM-Thumb VM(`st2android/`)에서 돌리고, 데이터/에셋을 모딩해 **익산타이쿤**을 만든다.
순서: ① 리소스 오버라이드+텍스트 재패킹 도구 → ② 지명/대사 익산화 → ③ 랜드마크 스프라이트 몇 개만 새로 그려 교체(미륵사지 석탑·왕궁리 오층석탑·익산역…) → ④ 신규 시스템 오버레이(선거/4년임기·연임 최대2회·엔딩/계속, 뒷돈→감사→탄핵, 정치력, 국회·도청 방문, 피로, 실제 달력 익산 행사).
UX: 서울타이쿤처럼 권역별 맵, 시청에서 지표(피로/지지도/예산/자재/인력)·커맨드 일괄.

## 저장소/브랜치
`kuh01041461581-ux/ukukuk`, 브랜치 `ccr-21696a78-hbj0yc`. PR 만들지 말 것. 원작 에셋·텍스트는 공개 배포 금지(개인용). 응답은 한국어, 빌드 후 APK는 SendUserFile.

## 구성
- `st2android/` : ST2 호스트(ThumbVm/Host/Lib/GfxLib/Surf), 앱(`app/`), `build_apk.sh`, `test/JvmRun.java`(JVM 하니스; env MOD=모드폴더, SHOT, VERBOSE…)
- `seoultycoon/`(ST1 J2ME 호환 APK), `docs/PROJECT_NOTES.md`, `docs/ST2_ANALYSIS.md` (상세 분석)
- 모딩: `tools/st2_mod_build.py` + `st2android/mod/text_map.json`
  - `replace` : 텍스트 .d(u8[32] 길이표+CP949) 치환 후 재패킹(문자열 255바이트 초과 불가)
  - `image_replace` : 코드이미지 `client.bin1116` 내 UTF-16LE 문자열 **같은 글자수** 치환 (지역바 강동→동부, 석촌호수→서동공원 등)
  - 산출물은 `st2android/mod/`(gitignore). 앱은 assets `st2mod/` 우선 → `st2/`.
- 에셋 원본은 `work/`(gitignore, 없으면 기존 방식으로 재추출: PROJECT_NOTES 참고).
- `.m`=PNG 스프라이트시트, 권역맵=64x64 청크 4x4 + 건물스프라이트 고정 좌표. 배경 교체는 가능, 부지배치/권역 수 변경은 미해독이라 어려움.

## 빌드/테스트
`cd st2android && ./build_apk.sh` (apt aapt/dx/apksigner 필요; 람다는 Delambda). JVM 테스트: `javac -d /tmp/jt src/.../*.java test/JvmRun.java; MOD=$PWD/mod java -cp /tmp/jt JvmRun work/st2/client.bin1116 work/st2 OUT font.ttf 700 "150:1:53,..."` (cc2 세이브가 남으니 OUT 새로 만들 것).

## 다음 할 일
1. 이번 모드로 APK 빌드·실기 확인(지역바/건물명 익산화). 건물 메뉴명 등 `.m` 이미지에 박힌 글자는 스프라이트 교체 필요.
2. 대사 .d 익산화 확대(text_map 보강), 랜드마크 스프라이트 제작(ST2 팔레트/크기에 맞춤, 30x81 등).
3. 오버레이 시스템(호스트 쪽 훅) 설계.
## 새 세션에 필요한 것
네트워크 허용(ko/en.wikipedia.org, iksan.go.kr, data.go.kr, namu.wiki), 사용자 제공 사진(웹 이미지 열람 불가), 가능하면 Artifact 미리보기.
## 주의
Thumb VM은 ARM `bx pc` 스텁 대신 매직주소 호스트함수 사용; 클래스 vtable 전파 금지(isObj=false); 날짜는 int[12] 중 유효한 최신값 선택.
