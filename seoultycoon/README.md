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
