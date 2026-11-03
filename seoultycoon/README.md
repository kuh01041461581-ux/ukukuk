# 서울타이쿤 → 익산타이쿤 프로젝트

목표: 원작 **서울타이쿤**(2005~2006 SKT 피처폰 게임)을 복원하고, 도트 기반으로 **Unity**에서 스마트폰/PC용 **익산타이쿤**을 개발한다.

## 받은 파일 분석 (`original/`)
| 파일 | 정체 | 형식 |
|---|---|---|
| `seoul_tycoon1_wipi.zip` | **서울타이쿤 1** (치즈케익프로덕션, 2006-02) | SKT WIPI/SK-VM **J2ME(Java) MIDlet**. `0000000007.jar` 안에 난독화된 .class 46개 + 리소스 `.d` 216개(PNG 181, MMF 사운드 7, 데이터 28) |
| `seoul_tycoon2_skt.zip` (업로드 2개는 완전히 동일) | **서울타이쿤 2** (넥슨모바일, 2006-11, "Stand Alone") | SKT **네이티브 ARM(Thumb) 바이너리** `client.bin1116`(자바 아님) + `.d` 254 / `.m` 73 + `P/data1.pak`(PNG 173개 묶음) + `P/cc2`(세이브) |

- 1편은 클래스가 있어 **완전 디컴파일 가능**(`reference_src/st1/*.java`, CFR). 2편은 기계어라 로직 복원은 디스어셈블이 필요(미착수); 에셋은 전부 추출 완료.
- 1편이 사용하는 단말 API는 매우 작음(그리기: 이미지/사각형/선/문자열뿐, 사운드, 파일 저장 `XFile`, 진동) → 에뮬레이션/포팅이 쉬움.
- 문자열은 EUC-KR(CP949)로 저장됨 → 반드시 `-Dfile.encoding=EUC-KR` / C# `Encoding.GetEncoding(949)`.
- 화면 176×(208+16), 키코드: 2/4/6/8 방향, 5 확인, 194/195 소프트키.

## 현재 상태 (마일스톤 1)
- [x] 원본 1편을 **그대로** PC에서 구동 (`harness/`: J2ME/SKT API 스텁 + AWT). 타이틀→메뉴→새 게임→시장 취임 연출까지 스크린샷으로 확인.
- [x] 에셋 추출: `assets/st1`, `assets/st2`, `assets/st2_pak` (PNG/MMF/바이너리 분류), 시트 도구 `tools/contact_sheet.py`
- [ ] Unity 포팅 (아래 계획) / 2편 ARM 로직 분석 / 익산 데이터(지도·건물·정책) 제작

## 서울타이쿤2 분석 결과 (마일스톤 2)
- `client.bin1116`: **자기 재배치형 ARM Thumb 이미지**. 시작 스텁(0x0~0x64)이 재배치 테이블(10,879개)을 적용하고 BSS(0x45c)를 0으로 채운 뒤 진입 구조체(함수 포인터 테이블, 파일오프셋 0xacac0)를 반환. `tools/st2_emu_probe.py`(Unicorn)로 스텁 실행·재배치 성공 확인.
- **정체 확정: WIPI 플랫폼의 Java→ARM AOT 이미지**(자체 런타임 포함) — 상세는 `docs/ST2_ANALYSIS.md`. 커널/JB 인터페이스 규약은 **아직 미해독** → 2편 코드를 그대로 에뮬레이션하려면 API 호출부를 역추적해 스텁을 만들어야 함(대규모 작업).
- **데이터는 해독 완료**
  - `P/data1.pak` = `u32 총크기 | u32 개수 | [name(8) offset(4) size(4)]×N | 데이터` → PNG 173개 (`tools/extract_pak.py`)
  - 텍스트 `.d` 41개 = `u8[32] 문자열 길이표 + CP949 문자열` (대사 l*, 사건 ab*, 뉴스 m*/n*, 메뉴 y0) → `assets/st2_text/*.json` (`tools/extract_text_st2.py`)
  - 이진 테이블 `abx/lx/lxs/mx/nx/yx.d`: 게임 수치/이벤트 테이블(포맷 미해독, 1편 `reference_src/st1` 의 `aj`·`s` 클래스와 구조가 유사 → 비교 해독 예정)
- 방침: 2편은 에셋·텍스트를 모두 확보했으므로, **1편 디컴파일 코드를 기준 로직으로 삼아 Unity에서 재구현**하고 2편 데이터 테이블은 해독되는 대로 이식.

## 서울타이쿤1 안드로이드 APK (마일스톤 3)
`android/SeoulTycoon1.apk` (디버그 서명, minSdk 16 / target 29) — 폰에서 "출처를 알 수 없는 앱" 허용 후 설치.
- 방식: 원본 게임 클래스를 **수정 없이** dex 로 변환해 넣고, 피처폰 API(`javax.microedition.lcdui`, `com.skt.m`, `com.xce`)를 안드로이드(`android.graphics`)로 다시 구현한 호환 레이어(`android/src`) 위에서 실행.
- **터치/마우스 조작**: 게임 화면을 **탭 = 확인**, **쓸어넘기기(스와이프) = 방향키**(밀고 있으면 연속 이동), **길게 누르기 = 취소**. 화면 아래에 **피처폰 모양 가상 키패드**(좌메뉴·우메뉴·통화·취소, 방향키+OK, 1~9 와 *·0·#, 숫자 밑 ABC 표기)가 있고 (좌메뉴=190 코드는 추정), 블루투스/PC 키보드(방향키·Enter·Backspace·숫자)와 뒤로가기(=취소)도 지원. 시장 이름 입력은 안드로이드 입력창으로 대체.
- 빌드: `android/build_apk.sh` (apt: `aapt apksigner dalvik-exchange zipalign libandroid-23-java`).
- **한계**: ① 이 개발 환경에는 안드로이드 에뮬레이터/실기기가 없어 APK를 **실행해 보지는 못했음**(PC 하니스에서는 동일 원본 코드가 정상 동작 확인, APK는 빌드·서명·구조 검증만) ② 효과음/BGM(MMF 형식)은 아직 무음 ③ 메뉴 항목을 직접 터치로 고르는 기능은 아직 없고(스와이프+탭으로 선택) 개선 예정.

## 사용법
```
./play_st1.sh                 # PC 창에서 원본 1편 플레이 (Java 필요) 방향키/Enter, Z·X 소프트키
./run_st1.sh out "wait:6000,key:53,wait:1500,shot:a"   # 헤드리스 구동+스크린샷(검증용)
python3 -I tools/extract_pak.py work/st2/data1.pak out  # 2편 pak 추출
python3 -I tools/export_assets.py
```

## Unity 포팅 계획
1. **J2ME 호환 레이어(C#)**: `Graphics`/`Image`/`Canvas`를 `Texture2D`(Point 필터, 176×224 정수배 확대, PPU 1)로 구현 — 하니스의 스텁과 1:1 대응.
2. `reference_src/st1`을 C#으로 변환(문법 거의 동일, 난독화된 클래스명은 의미 있는 이름으로 순차 리네임) 후 하니스 스크린샷과 **프레임 단위 비교**로 검증.
3. 입력: 터치 가상패드 + 키보드. 빌드: Android(APK/AAB), Windows/WebGL.
4. 이후 데이터(건물·지도·텍스트)를 익산 버전으로 교체 → 익산타이쿤.
