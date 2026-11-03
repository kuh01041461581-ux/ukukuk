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
