# 서울타이쿤 복원 → 익산타이쿤 개발 인수인계 노트

> 이 문서는 **다음 세션/다음 개발자(익산타이쿤 개발)** 가 처음부터 다시 조사하지 않도록 지금까지 알아낸 것·만든 것·쓰는 법·남은 일을 모은 것이다. 세부 분석은 `ST2_ANALYSIS.md`, 사용법은 `../README.md`.

## 0. 한 줄 요약
- 원작 두 편(피처폰)을 받아 **1편은 안드로이드 APK 로 이식 완료**(`android/SeoulTycoon1.apk`), **2편은 원본 ARM 코드를 PC 에뮬레이터로 실행해 타이틀·메인 메뉴·메뉴 조작까지 성공**(`tools/wipi_rt.py`). 2편 APK 는 자바 이식(`st2android/`)이 남음.
- 최종 목표 **익산타이쿤**: 도트 기반, 스마트폰/PC, 터치 조작. 아래 §7 에 재사용 전략.

## 1. 폴더 지도 (`seoultycoon/`)
| 경로 | 내용 |
|---|---|
| `original/` | 받은 원본 zip 2개(1편 WIPI/SKT jar, 2편 SKT 네이티브). **복원의 근거 자료** |
| `tools/setup_work.sh` | `original/` → `work/` 작업 폴더 재생성(work 는 git 제외) |
| `reference_src/st1/` | 1편 디컴파일 자바(CFR). 난독화 클래스명(q, aq, s, ar, c …) 그대로 |
| `harness/` | 1편을 **PC(AWT)** 에서 실행하는 J2ME/SKT API 스텁 + 러너(`play_st1.sh`, `run_st1.sh`) |
| `android/` | 1편 **APK 빌드**(`build_apk.sh`): 호환 레이어(`src/`), 조작 UI(GameView), 한글 글꼴·MIDI 변환 |
| `assets/` | 추출된 도트 PNG/사운드/텍스트(JSON). 1편 `st1/`, 2편 `st2/`·`st2_pak/`·`st2_text/` |
| `tools/` | 추출기·변환기·에뮬레이터·디스어셈블러·Ghidra 스크립트 (아래 §2) |
| `docs/` | 분석 노트, 스크린샷, Ghidra 디컴파일 결과 `st2_decomp.c.gz` |
| `st2android/` | 2편 안드로이드 이식용 자바(현재 `ThumbVm.java` Thumb 인터프리터 초안) |

## 2. 도구 목록과 쓰는 법
| 도구 | 용도 | 예 |
|---|---|---|
| `tools/setup_work.sh` | work/ 복구 | `./tools/setup_work.sh` |
| `tools/extract_pak.py` | 2편 `data1.pak` 풀기 (173 PNG) | `python3 -I tools/extract_pak.py work/st2/data1.pak out` |
| `tools/extract_text_st2.py` | 2편 텍스트 `.d` → JSON | `python3 -I tools/extract_text_st2.py` |
| `tools/export_assets.py` | `.d/.m` 를 png/mmf/bin 으로 분류 | `python3 -I tools/export_assets.py` |
| `tools/contact_sheet.py` | PNG 미리보기 시트 | `python3 -I tools/contact_sheet.py 폴더 out.png 14` |
| `tools/smaf2mid.py` | 야마하 MMF → 표준 MIDI | `python3 -I tools/smaf2mid.py a.mmf a.mid` |
| `tools/PatchCharset.java` | 1편 바이트코드의 `new String(byte[])/getBytes()` → EUC-KR 고정(ASM) | `build_apk.sh` 가 자동 호출 |
| `tools/wipi_rt.py` | **2편 실행기**(Unicorn+호스트 런타임) | `python3 -I tools/wipi_rt.py work/st2/client.bin1116 work/st2 /tmp/out` (환경변수 아래) |
| `tools/disasm_thumb.py` | 재배치 이미지 디스어셈블(리터럴 풀이) | `python3 -I tools/disasm_thumb.py /tmp/st2_relocated.bin 0x74b38 0x74c48` |
| `tools/st2_emu_probe.py` | 재배치 이미지 `/tmp/st2_relocated.bin` 생성 | `python3 -I tools/st2_emu_probe.py work/st2/client.bin1116` |
| `tools/ghidra/*.java` | Ghidra 헤드리스 디컴파일 | `DecompAll.java`, `DecompOne.java` |
| `android/build_apk.sh` | 1편 APK 빌드·서명 | apt: `aapt apksigner dalvik-exchange zipalign libandroid-23-java libasm-java` |

`wipi_rt.py` 환경변수: `ROUNDS`(스케줄 라운드), `KEYS=라운드:유형:키,…`(유형 1=눌림 2=뗌 4=길게; 키 53=확인, 56/50/52/54=방향 아래/위/왼/오), `TRACE=1`(호스트 호출 전체 로그), `PROF=1`(핫 블록), `COVER=1`(실행 명령어 집계), `TXTLOG=1`(그리기 로그), `TEXT_DY`/`MENU_DX`/`MENU_DY`(글자 위치 보정), `FONTSZ`.
필요 패키지(pip): `unicorn capstone pillow fonttools`.

## 3. 원작 1편 (서울타이쿤1) 구조
- 플랫폼: SKT SK-VM(J2ME 계열) MIDlet. `Seoul`(MIDlet) → `q`(Canvas, 메인 루프) → 화면 객체들.
- **핵심 클래스(추정 역할)**: `q` 캔버스·키 큐·타이머 루프(`h[]` 타이머), `aq` 화면 베이스(상태 `u`: 1=초기화 2=실행 3=복귀, 그리기 헬퍼), `s` 자원 로더/세이브/이미지 글꼴(`f0..f11`)/표(`mx`), `aj` 게임 상태(직렬화), `ar` 메인 도시 화면(최대, 1,794줄), `c` 타이틀/메뉴, `ao` HTTP(랭킹), `ak` 로딩 바, `j`+`am` 이름 입력(IME 대체 필요).
- **리소스**: `*.d`=PNG/MMF/데이터. 세이브 `cc` = 1바이트 합 + XOR/회전 스트림(`s.c`/`s.a`, `s.java` 참고). 텍스트·표는 `s.a("t"+번호)` 형태로 암호화 파일에서 복호.
- `s.java` 의 문자열 배열 `b[]` 에 서울 지명/시설(석촌호수, 롯데월드, 서울역, 강남아파트 …)과 정책·직업 목록이 있음 → **익산 버전의 지명·정책으로 치환할 지점**.
- 화면 176×(208+16). 키코드: 방향 141/146/142/145(+숫자 2/8/4/6), 확인 148/53, 취소 8, `*`=42 `#`=35, 190=추정 메뉴키, 194/195=볼륨.
- **1편에서 실제 쓰는 키(바이트코드 switch 분석)**: 방향 141/142/145/146(+50/52/54/56), 확인 148/53, 취소 8, 0(48, 도시 화면 `ar`), 190(메인메뉴 ae·t·m, 용도 미확정), 194/195(볼륨). `*`/`#` 는 쓰지 않음 → 방향키 버튼 모드는 방향·확인·취소·0 만 배치.
- 주의: 문자열은 **EUC-KR** 가정(안드로이드는 UTF-8 이라 `Cp` 도우미로 고정), 글자 폭은 `getBytes().length` 로 계산.

## 4. 원작 2편 (서울타이쿤2) 구조
- 실체: **WIPI Java 게임을 ARM Thumb 로 선컴파일한 이미지** `client.bin1116`(자체 클래스 링커·GC 포함). 앱 클래스 `Seoul2`, 게임 루프 클래스 `ac`(Card 서브클래스, Runnable).
- 리소스: `.d`(텍스트: `u8[32]` 길이표 + CP949 문자열 / 숫자표 `*x.d`), `.m`/`.d` PNG, `P/data1.pak`(건물·지형 타일 173 PNG), `P/cc2`(세이브/설정, 앱 저장소 `cc2`·`data1.pak` 로 접근).
- 화면 240×320(앱 백버퍼 240×296 = 320 − 안내바 24).
- 텍스트 JSON(`assets/st2_text/`): 대사 `l*`, 사건 `ab*`, 뉴스 `m*/n*`, 도움말 `y0` 등 — 게임 기획/대본 자료로 **익산판 대사 작성 시 형식 참고**.
- 메인 메뉴 라벨은 글자 시트 `12.m`(56×96, 8행) 를 클립으로 잘라 그림; 아이콘 스트립 `11.m`; 막대 `8.m/9.m`.

## 5. 2편 실행기(`wipi_rt.py`)의 확정 ABI — **가장 값진 기록**
(세부는 `ST2_ANALYSIS.md` 진행기록 1~4)
1. 이미지는 자기재배치형: 스텁(0x0)을 `r0=0x45c` 로 실행 → 진입 구조체(파일 0xacac0). 초기화 함수 **0x7b1ec**(인자: 플래그바이트*, GC루트 3단, 클래스베이스=0, 기본형배열 클래스표, 5번째=env표). 로드 콜백 **0x7b02c**. 앱 `Seoul2` 클래스 C=0xac804.
2. **호스트 표**: `env[0]`=인터페이스 조회("WIPIC_knlInterface","WIPIC_incMemInterface","WIPI_JBInterface"), `env[8]`=클래스 이름 해석, `env[11]`=할당. **JB 표**: `[1]`심볼 해석/new, `[2]`배열 생성·예외, `[3]`aastore/multianewarray, `[6]`newarray, `[8/9]`monitor, `[10]`클래스 초기화(`<clinit>`), `[11]`문자열 상수(**u16 길이+UTF-16LE**), `[12]`네이티브 정적 호출.
3. **객체**: 참조=핸들(칸→몸체), 헤더 `(Class<<5)`(→클래스는 64MB 미만 주소), 필드 `몸체+4+오프셋`, 배열 `[4]=길이,[8..]=원소`. 인스턴스 메소드 호출 `r1=this`, 64비트 반환 `r0:r1`, 정적 호스트 메소드는 `JB[12](M+8핸들, 인자배열)`.
4. 메소드 참조 슬롯 인코딩 `(멤버문자열idx<<16)|(클래스이름idx<<1)|1`, 이미지 내부 호출은 0x1c 바이트 표(0xa073c) 인라인 해석.
5. 예외는 setjmp/longjmp(0x7e038 계열), 스레드는 협력형 스케줄러(`Thread.sleep` 에서 양보).
6. **쓰이는 CPU 명령은 Thumb-1 약 35종**(+BL 쌍) → 자바 인터프리터 이식이 현실적(`st2android/ThumbVm.java`).

## 5-1. 인게임 렌더링 보정(2편) — 원인 기록
- **글자 위치**: 메뉴/하단바/상단바 라벨은 `drawString` 이 아니라 **글자 시트 PNG 를 `setClip`+`drawImage` 로 한 줄씩 잘라** 그림. 시트 행 간격과 게임이 가정한 간격이 다른 시트가 있어 보정(`MENU_DX/DY`, `SHEET46_DY`, 문자열은 `TEXT_DY`).
- **호스트 클래스 필드**: 앱이 `TextComponent.imHandler/m_cPos/m_td` 를 직접 읽음 → 호스트 클래스에 **인스턴스 크기(12바이트)와 고정 필드 오프셋**을 줘야 하위 앱 클래스 필드와 안 겹침(`HOST_SIZE`,`FIELD_OFFS`).
- **vtable**: 호스트 가상메소드 슬롯은 **전역 유일 번호(128칸)**, 슈퍼 체인(Component←TextComponent←TextFieldComponent)을 따라 하위 클래스 vtable 에 전파. **256/512칸으로 늘리면 부팅 실패**(원인 미규명) — 128 유지.
- **세이브**: 원본 `cc2` 는 기기별 인코딩이라 그대로 못 씀(읽으면 크기 -79 배열 오류) → 처음엔 비운 채 시작.
- 상단 바 `임기 □년 □월`: 글자 시트 `45.m`(77×11)의 조각은 `[임기 0..22][년 22..32][월 32..42][코인 42..52]` 이고, **숫자 칸은 게임이 그리지 않아 비어 있음**(이름 입력 이후에도 동일) → 호스트가 숫자 시트 `48.m`(50×9, 숫자 폭 5)로 직접 채움(`draw_date_digits`, 환경변수 `DATE=년,월` 기본 `1,1`). (두 자리 `01년 01월` 채움도 시도했으나 글자에 붙어 더 어색해 한 자리로 유지) 현재는 **고정값**이라 진행에 따라 변하지 않음. **상단 바는 '어색함' 미해결 — 게임 원본이 숫자를 그리는 코드 경로(날짜 필드)를 찾아 연동하는 것이 정석** — 게임의 날짜 필드를 찾으면 연동할 것.

## 6. 남은 일 (우선순위)
1. 2편: **(완료) 튜토리얼에서 시청 선택 → 시청 메뉴(현황/…) → 현황 창(재정·균형·분야·지지도 탭) 진입 확인**(`docs/st2_citymenu.png`, 1118라운드 오류 없음). **(완료) 게임시작 → 새 게임 도시 화면+김비서 대사까지 실행됨**(스크린샷 `docs/st2_newgame.png`, 재현 `KEYS=150:1:53,152:2:53,230:1:53,232:2:53,270:1:53,272:2:53,290:1:53,292:2:53 ROUNDS=400`). 이후: **(진행) 대사 넘기기 → 시장선택 → 이름 입력창('시장님 존함') 도달**(`docs/st2_nameinput.png`). **(해결) 기본 이름 `DEFAULT_NAME`(기본 '시장')을 `TextComponent.getString` 이 돌려주게 해서 통과 → 튜토리얼 진행**(세금수입 창, 지역이동 안내, 시청 'OK:ENTER' 표시, 정보창; `docs/st2_tutorial.png`, 라운드 ~810까지 오류 없음). 그 다음 지도 조작·건설 메뉴 등 인게임 입력 검증, 필요한 WIPI API 추가(Font, `Clip/Player` 소리, `TextField` 이름 입력, `Network`).
2. 2편 안드로이드: `ThumbVm` + 호스트 런타임(`wipi_rt.py`)을 자바로 이식 → 1편 `GameView`(터치·키패드·조이스틱) 연결 → `SeoulTycoon2.apk`. 소리는 `smaf2mid.py` 로 MIDI 변환.
3. 1편: 메뉴 항목 직접 터치 선택, 효과음 확인, 실기기 검증(미실시).
4. 숫자표(`*x.d`)·1편 `mx`/`nx` 표 포맷 해독 → 건물/정책/이벤트 수치 데이터화.

## 7. 익산타이쿤 재사용 전략
- **엔진 재사용 A (빠름)**: 1편 자바 엔진 + 데이터 치환. 이미 APK 파이프라인·터치·소리·글꼴이 있음. 바꿀 것: 지명/시설 문자열(`s.b[]`), 텍스트(`t*.d`), 건물/타일 이미지(`assets/*`와 같은 규격 PNG), 맵 데이터. 장점: 안정적, 단점: 난독화 코드·176×224 해상도.
- **새 엔진 B (확장성)**: Unity(또는 다른 엔진)로 재작성하되 **규칙·수치·텍스트 구조는 1편 디컴파일(`reference_src/st1`)과 2편 JSON 을 설계서로 사용**, 그래픽은 도트(타일 PNG) 규격을 유지. 텍스트 인코딩은 처음부터 UTF-8.
- **도트 규격 참고**: 2편 건물 스프라이트(`assets/st2_pak`)는 아이소메트릭, 건물 ~64×48~100 px 대, 지형 타일 ~64×? — `tools/contact_sheet.py` 로 전체 목록 확인.
- **저작권 주의**: 원작 그래픽·소리·텍스트는 참고용. 익산판은 **새로 그린 도트·새로 쓴 텍스트**로 제작할 것. `original/`·`assets/` 는 공개 배포하지 말 것.

## 8. 함정/교훈 (시간 절약용)
- 안드로이드 기본 글꼴 12px 무안티앨리어싱 → 한글 깨짐 ⇒ 도트 글꼴(Galmuri11) 번들. 문자열은 EUC-KR.
- dl.google.com·maven.google.com 은 이 환경에서 차단 ⇒ 안드로이드 빌드는 **apt 패키지(aapt/dx/apksigner/zipalign/android-23.jar)** 로.
- Ghidra 는 noreturn 오판으로 함수가 잘림 ⇒ 어셈블리/동적 후킹 병행(`disasm_thumb.py`, Unicorn 훅).
- 호스트 클래스는 **링크 전에 vtable 슬롯을 미리 확보**(64칸)해야 앱 서브클래스가 슬롯을 잘못 쓰지 않음.
- 파일 읽기 위치는 `File` 과 `openDataInputStream` 이 **공유**해야 함(안 그러면 pak 인덱스가 어긋남).
- 백그라운드 장시간 실행은 Monitor/타임아웃 사용, `pkill` 금지(자기 셸 종료됨).


## 부록: ST2 자바 포팅 / APK (2편 안드로이드)
- 위치 `st2android/`: `src/com/seoultycoon/st2/`(ThumbVm=Thumb 인터프리터, Host=호스트 런타임/스레드/입력, Lib=java.* 이식, GfxLib=lcdui 이식, Surf=소프트웨어 그래픽, Platform=OS 의존 인터페이스), `app/com/seoultycoon/st2app/`(St2Activity, St2View=ST1 GameView 파생 3모드 조작, AndroidPlatform), `test/JvmRun.java`(PC 검증기), `build_apk.sh`(→ `SeoulTycoon2.apk`).
- PC 검증: `javac -d /tmp/jt st2android/src/com/seoultycoon/st2/*.java && javac -cp /tmp/jt -d /tmp/jt st2android/test/JvmRun.java && java -cp /tmp/jt JvmRun work/st2/client.bin1116 work/st2 /tmp/out android/assets/galmuri11.ttf 500 "150:1:53,152:2:53,..."` (사전에 `/tmp/out/save/data1.pak` 복사 필요, 없으면 '추가 다운로드' 대화상자가 뜸). 환경변수 VERBOSE=1 → /tmp/jtrace.txt 에 호스트 호출 로그.
- **함정(해결됨)**: ① 이미지에 ARM 모드 스텁(memset 0x7d838, memcpy 0x7d8ec, 64비트 나눗셈 0x7da24, setjmp 0x7e038, longjmp 0x7e050)이 있어 Thumb 만 구현한 VM 이 죽음 → Host.patchArmStubs 가 Thumb 트램폴린(`ldr r3,[pc]; bx r3`)으로 호스트 구현에 연결. ② 파이썬 원본의 `propagate` 는 Object 전파가 사실상 비활성(`is_obj` 항상 False) — 활성화하면 가짜 클래스 항목의 vtable 이 이미지 데이터(슬롯 표)를 덮어씀. 자바판도 `isObj=false` 로 동일하게 유지.
- dx(1.16) 는 람다(invokedynamic)를 변환하지 못해 안드로이드에서 NoSuchMethodError(metafactory) 발생 → `tools/Delambda.java`(ASM)로 람다를 클래스로 풀어 준 뒤 dx 에 넣는다(build_apk.sh 가 수행). PC 검증 시에는 `-XX:+UnlockDiagnosticVMOptions -XX:-BytecodeVerificationRemote`. dx 경로: /usr/lib/android-sdk/build-tools/debian/dx
- ST2 키코드(WIPI): 위 50/아래 56/왼 52/오 54/확인 53, 숫자 '0'~'9'. **소프트키·취소 키코드는 미확정(-6/-5 추정)** — 실기기 검증 필요.
- 미완: 상단바 날짜(임기 N년 M월) 자동 연동, 이름 입력은 첫 실행 시 대화상자(기본 '시장'), 화면 터치 직접선택(ST1 TouchNav 같은 것)은 ST2 에 아직 없음(탭=확인), 안드로이드 실기기 미검증.

- **ST2 라벨 시트 문제(해결)**: 상단바 `45.m`(임기/년/월/일), 시청 팝업 `40.m`(현황/비서/행사/정보)는 글자 폭이 원본 폰용 클립 경계(22/10/10, 20/18/19/21)와 달라 글자가 반쪽씩 잘림 → GfxLib.special() 이 이 조각 그리기를 가로채 Galmuri 폰트(그림자 포함)로 글자 단위로 다시 그림. 날짜는 게임 상태 배열 int[12]={년,월,일,200(?),10,10,0,50,0,0,[10]=우상단 표시값(초기 1459),0} 의 [0..2] 를 힙에서 찾아 읽음(GfxLib.readDate: 날짜 범위가 맞는 int[12] 중 가장 늦은 날짜 채택 — 행동마다 복사본이 생기고 날짜는 증가만 하므로; 우상단 숫자와 [10] 일치 여부로 고르면 행동 후 값이 달라져 1/1/1 로 되돌아가는 버그가 있었음). 초기 1/1/1 확인, 시간 진행 후 값 변화는 미검증(튜토리얼에서 시간이 멈춰 있음). 우상단 1459 는 '남은 일수'로 가정했던 것이 근거 없음(1460 리터럴은 필드 오프셋일 뿐) — 용도 미확정. 아이콘 `41.m`(10x36)은 12행 칸으로 잘라 이웃 아이콘 침범 제거.
- 취소 키는 하단 안내 "0:EXIT" 에 따라 숫자 0(48), 확인 5(53). 소리: Player.play 의 receiver(t)=Clip, a[0]=loop. 화면 확대는 정수 배율(올림)로 키운 뒤 쌍선형으로 줄여 도트 일그러짐 방지.

- **프레임/속도(해결 시도)**: ST2 게임은 `Thread.sleep(0)` + `currentTimeMillis` 바쁜 대기로 프레임 간격을 맞추며 지도 이동 애니메이션 간격이 **약 150ms(6.7fps)**. 이전 구현은 시간 호출마다 가상시간을 2ms 올려 속도가 CPU 에 좌우돼 들쭉날쭉했음 → 이제 시간은 실제 경과만 흐르고(`Host.tick`, 2ms 단위 스케줄, 같은 시각을 4회 연속 읽으면 바쁜 대기로 보고 `yieldNow`), 설정에서 **게임 속도 1/1.5/2/3배**(`Host.timeScale`, ST2 기본 2배). ST1 은 `PatchCharset` 이 `System.currentTimeMillis`/`Thread.sleep(J)` 를 `Cp.now/Cp.sleep`(배율 적용)으로 치환(기본 1.5배). 화면 갱신은 vsync(`postInvalidateOnAnimation`), ST2 확대 비트맵은 프레임이 바뀔 때만 다시 만듦.
- **시청 팝업 라벨 버그(해결)**: 선택된 항목이 먼저 그려져서 '그리는 순서'로 라벨을 정하면 커서를 옮길 때 '현황'이 따라다님 → 시트 속 위치(srcx=clip.x-x ∈ {0,20,38,57})로 라벨 결정.
- **시청 팝업 마름모 버튼(해결)**: 시트 `17.m`(48x34)는 빨강[0,14) 파랑[14,28) 가는띠[28,34) 인데 게임은 18행 칸(빨강 0, 파랑 18)·폭 58 을 가정해 클립이 어긋나 깨짐 → GfxLib.special 이 선택(빨강)/비선택(파랑) 14행만 골라 그리고, 아이콘(`41.m` 10x36 = 그래프[0,7) 얼굴[7,17) 폭죽[17,27) 서류[27,36))·라벨(`40.m`)을 막대 좌표(curBarX/Y) 기준으로 배치.
