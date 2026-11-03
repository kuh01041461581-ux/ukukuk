# 서울타이쿤2 `client.bin1116` 분석 노트

## 정체
- **WIPI(한국 무선인터넷 표준) 플랫폼용 "Java → ARM Thumb 선(先)컴파일(AOT)" 이미지.** 문자열 증거: `ExeInterface`, `WIPI_exe`, `WIPI_JBInterface`, `WIPIC_knlInterface`, `WIPICX_incMemInterface`, `org/kwis/msp/lcdui/{Graphics,Image,Display,Card,Font,Jlet,...}`, 앱 클래스명 `Seoul2`, 메소드 `paint/keyNotify/processEvent/startApp/pauseApp/resumeApp/destroyApp`.
- 즉 2편도 원래는 자바 게임(난독화 + AOT 변환). 클래스 구조체/GC 루트 등록/예외(`ArrayIndexOutOfBounds`, `NullPointer` …)까지 이미지 안에 **자체 런타임**이 들어 있음. (`tools/disasm_thumb.py`)
- 앱이 쓰는 외부 API는 45개 클래스(java.io/lang/util 일부 + `org.kwis.msp.{lcdui,media,io,handset,lwc}`), 1편과 같은 규모.

## 파일/이미지 구조
- 시작 스텁(0x0–0x64): 자기 재배치(10,879 항목) + BSS(0x45c) 0 채움 → 진입 구조체(파일 0xacac0) 반환. Unicorn 으로 검증 (`tools/st2_emu_probe.py`).
- 진입 구조체: 이름 `WIPI_exe`, 콜백 포인터들(0x7b02d, 0x7b18d, 0x7d181, 0x7e051, 0x7d68d …), 미구현 슬롯은 기본 핸들러 0x7ac55(경고 로그 출력).
- 커널 인터페이스 `P`(전역 포인터, 예 0x10ad7f0): 함수 포인터 표. `P[0]`=printf류 로그, `P[7]`(0x1c)=루트/메모리 등록 등. GCC `_call_via_rX` 베니어(0x82620~)로 호출.
- 외부/내부 메소드 이름·시그니처 표: 파일 0x71443~0x8a820 (`(IIII)Z+processEvent` 형식, 748개).

## 실행하려면 필요한 것 (미완)
1. `WIPIC_knlInterface`/`WIPI_JBInterface` 함수 표의 슬롯별 의미를 호출 인자 추적으로 확정(Unicorn 훅으로 슬롯 호출 로깅 → 구현).
2. 앱이 부르는 `org.kwis.msp.lcdui.*` 호출(그리기/이미지/키 이벤트)을 1편 안드로이드 호환 레이어(`android/src`)에 연결.
3. 앱 콜백(paint/keyNotify/processEvent) 호출 규약(객체 레이아웃) 확정.
→ 소규모가 아닌 별도 프로젝트 규모의 역공학 작업.

## 2편 데이터
- `P/data1.pak` (PNG 173), 텍스트 `.d` 41개(`u8[32]` 길이표 + CP949), 숫자 테이블 `*x.d`(미해독).

## 역공학 진행 기록 (에뮬레이터 `tools/wipi_emu.py`, `tools/wipi_run.py`)
Unicorn 으로 재배치된 이미지를 올리고 호스트 함수를 "매직 주소"에 걸어 호출을 가로채는 틀을 만들었고, 다음을 **확정**했다.
1. **초기화 함수 = 파일 0x7b1ec**, 인자 5개. 5번째 인자(스택)가 호스트 `env` 표(함수 포인터 표). `env[0](name,-1,-1,0,0)` = 인터페이스 조회 → `"WIPIC_knlInterface"`, `"WIPICX_incMemInterface"`(, `"WIPI_JBInterface"`) 를 요청. `knl[33]()` 은 C 라이브러리 함수 17개 표를 돌려줌. 이 단계는 에뮬레이터에서 끝까지 통과(`init -> 0`).
2. **링커 구조**: 앱 이미지 안에 클래스 링커/vtable 빌더가 통째로 들어 있고(0x7ad64 등), 외부 심볼(클래스·메소드·필드)은 `env[8](slot, "이름문자열", 문자열표)` 로 호스트에 이름으로 해석을 요청한다(결과를 이미지 슬롯에 기록). 이름은 `org/kwis/msp/lcdui/Card`, `(IIII)Z+processEvent` 처럼 전부 평문 → 필요한 API 목록을 정확히 알 수 있음.
3. `env[11](size,…)` = 객체 할당, `env[7]` = GC 루트 등록, `env[0x2c/4]` = 메모리 할당.
4. 막힌 지점: 호스트가 돌려주는 **외부 클래스 핸들이 이미지 내부 클래스 디스크립터와 같은 레이아웃**(플래그 @0x12, 링크정보 @8, 멤버 수 @0x18/0x1a/0x1e/0x20 …)이어야 링커가 계속 진행한다. 즉 `java.lang.*`/`org.kwis.*` 를 이 레이아웃으로 흉내 내는 **미니 자바 런타임**을 호스트에 구현해야 함.

### 다음 단계
- 링커 코드(0x7ad64~0x7b02c)를 읽어 클래스 디스크립터 레이아웃 확정 → 호스트 클래스 합성기 작성
- 외부 메소드 호출 규약(this/인자/반환) 확정 후 `Graphics`/`Image`/`Display`/`Card` 를 안드로이드 레이어에 연결
- 보조: Ghidra(ARM:LE:32:v8T, 로드주소 0x1000000)로 의사 C 확보 시 속도 향상 (github 릴리스 다운로드는 가능한 것으로 보임)
- **가장 빠른 대안**: 서울타이쿤2의 *다른 통신사/기종용 jar*(KTF·LGT·SKT-GVM 등 자바 클래스가 평문으로 들어 있는 판)을 구하면 1편과 같은 방식(디컴파일 → 안드로이드 레이어)으로 며칠이 아니라 바로 실행 가능.

## 진행 기록 2 (Ghidra 디컴파일 + 링커 통과)
- **Ghidra 11.1.2 헤드리스**로 재배치 이미지를 디컴파일(`tools/ghidra/DecompAll.java`, 프로세서 `ARM:LE:32:v8`, 베이스 0x1000000, 함수 1,499개) → `docs/st2_decomp.c.gz` (함수 주석의 `file=` 은 파일 오프셋). 재현: `analyzeHeadless <proj> st2 -import st2_relocated.bin -processor ARM:LE:32:v8 -loader BinaryLoader -loader-baseAddr 0x1000000 -postScript DecompAll.java` (relocated.bin 은 `tools/st2_emu_probe.py` 가 만드는 재배치 이미지).
- **클래스 디스크립터 레이아웃(추정, 링커 0x7ad64 해석)**: Class `C`: `+8→Info I`, `+0xc` vtable 배열, `+0x10`(u16) vtable 크기, `+0x12`(u16) 상태(0x80=링크됨, 0x10=인터페이스류). Info `I`: `+0` 이름 char*, `+8` 슈퍼(홀수=문자열표 인덱스·짝수=Class*), `+0xc` 메소드 배열(개수 u16 `+0x18`), `+0x10` 외부 참조 배열(개수 u16 `+0x1e`), `+0x14` 필드 배열(개수 u16 `+0x20`), `+0x1a`(u16) 인스턴스 크기. Method: `+0xc` 시그니처 문자열(앞 1바이트 + 이름), `+0x14`(u16) vtable 슬롯, `+0x16`(u16) 접근 플래그.
- **호스트 인터페이스(확정)**: `env[0]`=이름 조회, `env[8](slot,name,tbl)`=심볼 링크(결과를 slot 에 기록), `env[11](n)`=힙 할당, `JB[11](utf8*)`=문자열 상수 객체 생성(437개), `JB[12]`(=+0x30) 저장됨.
- **에뮬레이터 상태**: `python3 tools/wipi_run.py work/st2/client.bin1116 7b02c` → 로드 콜백(0x7b02c)이 **끝까지 성공(반환 0)**: 외부 클래스 132개 링크(고유 11종: Card, Exception, Object, String, [I, [B, Jlet, Thread, TextFieldComponent, Runnable, Image), 문자열 상수 437개 생성. 
- **다음**: (1) 앱 메소드를 호스트가 부르는 경로(진입 `Seoul2`/Jlet.startApp, paint, keyNotify) 확정 — 진입 구조체 `+0x3c..0x58` 쪽 콜백과 JB 표 사용처 추적, (2) 외부 메소드(`Graphics.fillRect` 등)의 호출 규약 확정 후 호스트 구현, (3) 안드로이드 레이어에 연결.

## 진행 기록 3 (앱 코드 실제 실행 시작)
`python3 tools/wipi_run.py work/st2/client.bin1116 7b02c` 로 로드 콜백 후 `Seoul2.<init>` / `startApp` 을 직접 호출해 **앱 자체 코드가 실행됨**을 확인(호스트 호출이 순서대로 로그에 찍힘).
- **앱 진입점**: 클래스 `Seoul2` = Class `C`@0xac804, Info `I`@0xab7d4. 메소드 5개(Method 구조체, `I+0xc` 배열): `<init>`, `startApp([String)`, `pauseApp`, `resumeApp`, `destroyApp(Z)`. **Method 구조체**: `+0` 코드 포인터(Thumb), `+4` 소속 Class, `+0xc` "접두1바이트+시그니처+이름" 문자열, `+0x14`(u16) vtable 슬롯(startApp=15, pause=16, resume=17, destroy=18), `+0x16`(u16) 플래그.
- **호출 규약**: `r0`=무시(호출 시 대상 주소로 덮임), **`r1`=this**, `r2…`=인자. 정적/가상 호출은 헬퍼(0x7d76c 등)가 메소드 참조 슬롯을 해석한 뒤 `mov pc,r0`.
- **메소드 참조 슬롯 인코딩**: `u32 = (멤버문자열idx << 16) | (클래스이름idx << 1) | 1` (문자열표 0x9cac0). 최초 호출 때 `JB[1](slot, 콜백)` 이 해석해 `slot[0] = Method*` 로 교체. 같은 `JB[1](Class*, …)` 은 `new`(인스턴스 할당)로도 쓰임.
- **JB 표 슬롯(확정)**: `JB[1]` 메소드 해석/new, `JB[2](문자열, 0, …)` 예외 던지기(첫 인자가 이미지 주소이면 이름 문자열, 작은 수이면 `newarray(len, elemsize, class)`), `JB[10](Class)` 클래스 초기화(`<clinit>`), `JB[11](utf8*)` 문자열 상수 생성, `JB[12]` 저장.
- **객체 모델**: 참조값은 **핸들**(몸체를 가리키는 포인터를 담은 칸의 주소). 몸체 `[0]` = 헤더(`classIndex<<5 | 플래그`), 배열은 `[4]`=길이, 원소는 `[8+i*크기]`; 인스턴스 필드는 `몸체+4+오프셋`. → 호스트가 만드는 객체/배열도 이 형식을 따라야 함(현재 에뮬레이터는 단순 객체라 이 지점에서 NPE).
- 호스트 클래스 합성기(`HClass`) 구현됨: `Jlet`(vtable 19칸) 등 외부 클래스를 이미지 링커가 받아들이는 형식으로 생성.
- **다음**: 핸들/헤더 규칙에 맞춘 `new`/`newarray`, 클래스 인덱스 표(`*0x10ad? + (hdr>>5)`) 구현 → `Card`/`Display`/`Graphics`/`Image` 호스트 메소드를 안드로이드 레이어에 연결.
