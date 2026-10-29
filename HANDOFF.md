# 인수인계 — 초과근무 기록 알리미 (C# WinForms 트레이 앱)

이 폴더는 **사용자 본인의 앱 소스**예요. 익산시청 내부용 도구이고, 사용자는 공무원(어르신 포함)이에요.
새 작업에서는 이 PC(윈도우 11, 일반 PC)에서 **직접 빌드하고 측정**하며 성능 문제를 풀어 주세요.

## 사용자 선호 (항상 지킬 것)
- 답변은 **간결한 한국어**. 윈도우에서 직접 확인하지 못한 것은 솔직하게 말하기 (이전에 "깔끔하다"고 단정했다가 틀려서 신뢰를 잃은 적 있음).
- 매 수정 후 exe를 사용자에게 전달.
- 아주 부드럽고 자연스러운 애니메이션. 단순·고급·부드러운 곡선 디자인, 검정/흰 테두리 금지.
- 어르신도 보기 쉽게, 글자가 잘리면 안 됨. exe는 작게. 재시작이 필요한 방식 금지.
- exe는 하나만: `초과근무기록_알리미.exe` (Noto Sans KR 내장, 자동실행 옵션 포함).

## 빌드
- Windows: `build.bat 출력.exe` (Roslyn csc 필요 — C# 8+ 문법을 씀. 구형 .NET Framework csc 4.0은 컴파일 안 됨. Visual Studio Build Tools 또는 .NET SDK 필요)
- 리눅스(이전 환경): `build.sh` (Mono 4.5 참조, Roslyn)
- 리소스: mascot.png, noto/m.gz→noto.m.gz, noto/b.gz→noto.b.gz, pagoda.png, manual.png, outfits/*.png→outfit.<이름>.png
- 폰트: PrivateFontCollection으로 Noto 서브셋(KS X 1001 2350자 등)을 로드. 없는 글자는 맑은 고딕으로 폴백(`Fonts.Covers/Fallback`).

## 현재 최대 문제: 버벅임 (사용자가 가장 답답해함)
증상: **화면(쪽) 전환, 마룡이 클릭, 메뉴 이동, 테마 변경**이 항상 끊김. 일반 PC(가상 데스크톱 아님), 윈도우 11.

### 진단 도구 (이미 있음)
- `%AppData%\OvertimeReminder\perf.on` 파일을 만들면 `perf.log`에 기록: 50ms 넘는 UI 끊김, "한 프레임 처리", `Anim.Mark("동작")` 이름.
- 로그 맨 위에 "이 exe를 만든 시각"이 찍힘 (예전 exe가 돌고 있는지 구분용).
- 쪽 이동은 3구간 마크: 화면 사진 / 덮개 표시 / 쪽 바꿈 완료.

### 사용자 로그에서 얻은 숫자
- 이미 가 본 쪽 재진입: 전환당 55~80ms 정지 (쪽 바꿈 단계 25~50ms).
- 처음 가는 쪽: 100~155ms (핸들 미리 만들기를 넣어도 그대로 → 텍스트 측정·폰트 글리프 준비 비용 의심).
- 테마 변경: 85~190ms (누를 때마다).
- 앱 첫 실행 시 홈 창: 320~510ms.
- 홈에 가만히 있어도 50~160ms 끊김이 간헐적으로 있었음 (배너 반복 그리기 의심).

### 이미 시도한 것
1. 홈 배너 `SkyBanner.Pulse()`: 항상 60fps 동기 그리기(`Invalidate+Update`) → 그리는 시간(PaintMs) 기반 감속, 대기 중 20fps, 활동 중 60fps.
2. `WashPanel`의 번짐 배경(창 크기 PathGradient 2개)이 **모든 컨트롤 그릴 때마다** 다시 그려지던 것을 → 공유 비트맵 캐시(1/4 크기로 그려 확대) + 조각 복사.
3. 설정 저장 비동기(`SaveAsync`), 시작 워밍업(`Warm()`), 말풍선(Bubble) 재사용, 마룡이 연타 시 하트 제한.
4. 쪽 이동: 현재 화면 사진(CopyFromScreen) → FadeVeil → 쪽 교체 → 덮개 걷기. 느린 PC 감지(`NoteSlow`, `LowPower`): 쪽 이동 60ms/테마 100ms 초과가 3번 연속이면 사진·덮개 없이 즉시 전환.
5. 창이 뜬 뒤 다른 쪽 컨트롤 핸들을 90ms 간격으로 미리 생성.
6. 테마 변경: ThemeVeil(사진 덮개) + `ThemeFx.Apply`(즉시 재색칠).

### 의심되는 근본 원인 / 다음에 해 볼 것
- 모든 컨트롤이 `Ctl : Control`(윈도우 핸들 보유, UserPaint, OptimizedDoubleBuffer)이라 쪽당 수십 개 HWND를 개별 더블버퍼로 그림. → **측정 후** 컨트롤 수 줄이기(한 패널에서 직접 그리기) 또는 `WS_EX_COMPOSITED` 검토.
- 쪽 교체 시 동기 `pages[idx].Update()` → 사진 덮개 아래에서 다음 프레임에 맡기기 vs. 쪽 비트맵 미리 렌더링(DrawToBitmap 계열)해서 즉시 보여주기.
- 텍스트: `Gfx.Text`가 개인 폰트(Noto)일 때 GDI+ 경로(`TextRenderer` 미사용) → 느림 가능. 글자 측정(`Fit`) 캐시는 있음. 폰트 렌더링 경로 비교 측정 필요.
- 마룡이 클릭: `SkyBanner.Poke()` → `MaryongGame.Tap()` → `Bubble.Say`. 측정 필요.
- 그리기 전체를 GPU로 옮기려면 WPF/Direct2D 재작성이 필요(큰 작업) — 최후의 수단.
- **해야 할 일 1순위**: 이 PC에서 Stopwatch/ETW(또는 dotnet-trace, PerfView)로 쪽 이동·테마 변경·마룡이 클릭 한 번의 시간 구성(레이아웃/페인트/GC/CopyFromScreen)을 실제로 측정.

## 코드 지도 (주요 파일)
- Design.cs: 테마 부품(Ctl, TLabel, TButton, TToggle …), `Fonts`, `Gfx`, `Wash`/`WashPanel`, `SkyBanner`(홈 배너: 하늘·시계·마룡이).
- Settings.cs: 설정창 `SettingsForm`, 쪽 구성(`pages[0..9]`), `go/swap` 쪽 이동, 홈 새로고침(`refreshHome`), NC 히트테스트로 가장자리 크기 조절.
- Insarang.cs: `SetTheme`, 인사랑 열기. Anim.cs: `FrameTimer`, `Anim.Run`, 트윈, `ThemeFx.Apply`, perf 로그.
- Popup.cs: 알림 팝업(AlertPopup). Fx.cs: 위에서 내려오는 알림 띠(AlertBanner), Confetti. Toast.cs: 트레이 위 토스트. Care.cs: 말풍선(Bubble)·건강 멘트.
- Features.cs: 기록(Records), 가장자리 글로우(GlowCore/GlowWin). Game.cs/Game2.cs: 마룡이 오락실(미니게임 12종).
- ProgramExt.cs: 설정 저장, Warm, 점심 메뉴, 마스코트/의상, LowPower.
- 한도: 월 57시간(설정 가능), 연 480시간(`Program.YearLimit` 상수).

## 최근 완료 (참고)
- 팝업·알림 띠·토스트·말풍선의 모서리 정리(Win11 DWM 둥근 모서리, 사각 폴백), 가장자리 글로우를 오로라 톤으로.
- 미니게임: 스트레스 사격(군복 마룡이 한 발 공기총), 짝맞추기(마룡이 옷 8쌍), 장애물 점프(짧게/길게 구분).
- 첫 설치 시 그림 사용설명서 자동 표시(`manual.seen`), 연 480시간 한도 안내 보강.

## 윈도우 미검증 항목 (솔직하게 말할 것)
Noto 글꼴 렌더링, 애니메이션 체감, 설정창 가장자리 리사이즈, 게임 화면, 첫 실행 시 설명서 자동 표시.

## 2026-10-14 직접 측정 결과 (Windows 11, i5-12400F, RTX 3060)
측정 도구: `OT_BENCH=1 OT_DIR=<임시폴더> OT_FX=1 exe` 로 실행하면 쪽 20번·테마 4번·마룡이 클릭·홈 가만히를 자동으로 돌려 `OT_DIR\bench.txt` 에 적어요 (Bench.cs). 사용 중인 앱과 설정이 섞이지 않게 `OT_DIR` 이 있으면 별도 폴더·별도 뮤텍스를 써요.
- 진짜 원인: 쪽 이동 때 알약(`pillY`)이 매 프레임 `side.Invalidate()` + 7개 NavItem 전부 다시 그림 → 전환당 SidePanel 30회(약 100ms)·NavItem 210회(약 75ms). 예전 첫 멈춤(45ms)이 애니메이션 앞부분을 삼켜서 가려져 있었음.
- 고친 것: ① 알약이 걸친 NavItem 만 무효화, pillY 의 Owner 제거 ② 창 사진을 입력 없는 동안 백그라운드 스레드에서 미리 찍어 두고(캐시) 클릭 시 사용, 없으면 BitBlt(`Grab.Screen`) ③ 덮개를 먼저 띄우고 쪽 교체·메뉴 애니메이션·테마 색 교체는 다음 차례(BeginInvoke)에 → 멈칫은 움직임 '시작 전'에 끝남.
- 결과(전→후, 최대 끊김): 쪽 이동 동기 33~83ms → 약 8ms, 최대 끊김 50~97ms → 약 30ms(측정 타이머 한계), 테마 동기 55~113ms → 약 10ms. 마룡이 클릭은 원래 1~3ms 로 문제 없었음. 홈 가만히 최대 31~35ms.
- 남은 것: 테마 변경 때 덮개 밑에서 SkyBanner 가 계속 그려짐(약 3ms×40회), 앱 첫 실행 시 홈 창 320~510ms 는 아직 손대지 않음.
## 2026-10-14 미니게임 수정
- 풍선(스트레스 사격)·지렁이가 안 나온 원인: 리눅스(Mono) 참조로 빌드한 exe 가 윈도우 런타임에서 `String.Split(char, StringSplitOptions)`·`Queue<T>`(mscorlib 4.0) 를 못 찾음(error.log). 윈도우 csc(build.bat)로 빌드하면 해결 — 앞으로 항상 build.bat 로 빌드할 것.
- 2048 방향키 안 먹음: 폼 안의 TextBox 때문에 방향키를 창이 포커스 이동용으로 먼저 가져감 → `MaryongGame.ProcessCmdKey` 에서 직접 받아 게임에 전달. 마우스로 끌어서 미는 `Swipe` 도 추가(MiniGame.Swipe).
- 야구 스윙: 아래쪽(190°)에서 위쪽(40°)으로, 공이 오는 쪽으로 휘두르도록 변경 (BatGame.Draw).
- 점프 게임 그래픽은 새 빌드에서 재현되지 않음(오프스크린·실제 창 모두 정상). 사용자 확인 필요.
- 점검용: `OT_BENCH=1 OT_BENCH_GAMES=<폴더>` 게임 화면 PNG, `OT_BENCH_LIVE=<폴더>` 실제 창 + 키 입력 시험 (Bench.cs).
- 점프 게임에 코인(+10, 낭떠러지·장애물 위 아치)과 낭떠러지(Kind 3, 6초 이후 등장, 빠지면 종료) 추가. 점검: OT_BENCH_JUMPBOT=<폴더> 로 자동 점프 봇 시험 (봇이 2분 통과·코인 약 155개).

- 석탑 쌓기를 균형 물리 방식으로 재설계(TowerGame, Key=tower2): 크레인→낙하, 위 돌 전체의 무게중심이 접촉면을 벗어나면 그 윗부분이 쏠려 무너짐, 기회 3번, 6층부터 바람, 9층 완성 보너스. 점검: OT_BENCH_TOWERBOT=<폴더> (정확 봇 295층, 대충 봇 17층, 막 누르면 0~2층).

- 야구공 치기를 물리 방식으로 재작성(BatGame, 옆모습): 방망이=손잡이를 축으로 도는 캡슐, 공과 닿은 점의 면 방향·방망이 속도로 충격량 계산(Collide/StartFlight), 비행은 중력 포물선, 비거리로 안타/장타/홈런 판정. 앞쪽에서 맞으면 파울(너무 일찍), 늦으면 땅볼·헛스윙. 점검: OT_BENCH_BATBOT=<폴더> (타이밍별 결과 분포).

- 새 물리 게임 4종 추가 (Game3.cs): 농구 슛(BasketGame, hoop) · 볼링(BowlGame, bowl) · 낚시(FishGame, fish) · 새총(SlingGame, sling). 마우스 끌어당겨 놓기는 GameInput.Hold 상승/하강 + Mx/My 로 구현(MouseDown 때 Mx/My 도 갱신). 오락실은 이제 16개 게임(3쪽). 점검: OT_BENCH_NEW / OT_BENCH_SLING / OT_BENCH_MOUSE (Bench.cs).

- 마룡이 그림 교체: 의상 54종은 Downloads 의 merged-image-2026-10-14T12-11-45.png(2790x1800, 알파 포함)에서 오려 outfits/ 에 이름 매핑해 교체(시트 순서 = 이름순 목록에서 마지막 9종[축구…흑백한복]을 맨 앞으로 옮긴 것). 기본 마룡이 mascot.png 는 스크린샷_2026-06-24_230948-removebg-preview.png(525x475)를 여백 잘라 교체. 원본 오리기 도구는 세션 임시폴더(sheetcut/cut.cs)에 있었음. 옛 그림 백업은 임시폴더 outfits_old_backup. 의상 원본 자체가 포스터처럼 뭉개진 질감(약 262x305)이라 더 큰 원본이 생기면 같은 방식으로 교체.

## 2026-10-14 저녁: 마룡이 그림 교체(3차) + 게임 손질
- 의상 54종: Downloads 의 `다채로운 의상의 귀여운 마스코트 스티커 시트.png`(1536x1024, 진짜 투명 배경)에서 오려 교체. 시트 순서 = 이름순 목록에서 마지막 9종(축구…흑백한복)을 맨 앞으로 옮긴 것. 오리기 도구: 세션 임시폴더 sheetcut/cut.cs (알파 시트는 `cut.exe 시트 출력폴더 999 16 0 none 0`). 한 마리가 약 150x170px 이라 크게 확대하면 흐림 — 더 큰 원본이 생기면 같은 방식으로 교체.
- 기본 마룡이 mascot.png: 스크린샷_2026-06-24_230948-removebg-preview.png (선명).
- 게임마다 입는 옷(MiniGame.Costume → Program.OutfitImage): 별 받기=우주비행사, 점프=캠핑, 야구=어린이날, 석탑=근로자의날, 시간=정장, 2048=과학의날, 타자=독서, 지렁이=식목일, 농구=축구, 볼링=여름, 낚시=바다의날_낚시, 새총=산의날. 메뉴 카드에 의상 마룡이 표시.
- 고친 것: 시간 맞추기를 정밀 시계(Stopwatch)로(프레임 지연 영향 제거), 수박 합치기 나이 계산 2배 버그, 점프 배경(달·흐르는 언덕), 농구 체육관 벽·조명, 야구 관중석·대기 중 방망이는 타자 뒤.
## 2026-10-14 밤: 홈 잘림·전환 번쩍임·창 열기 렉
- 원인 1(홈 잘림): 홈 쪽 내용 최대 높이 562 > 쪽 높이 540 → 창 논리 높이 HH 640→680. 작은 화면 자동 축소(Dpi.Fit, 글꼴 pt 에도 곱함) 기준도 680. (이 PC 는 4K 200% 라 화면 때문은 아니었음)
- 원인 2(전환 번쩍임): 별도 덮개 창(FadeVeil/ThemeVeil) 방식 → 앱 창 안의 TransHost(Anim.cs)로 교체. 옛 화면 사진을 먼저 덮고 → 뒤에서 쪽을 바꿔 DrawToBitmap 으로 새 화면을 그려 두고 → 새 화면을 겹쳐 미끄러뜨림(InOutSine 420ms, 테마는 520ms). 비트맵은 DrawImage 에 픽셀 사각형을 명시(DPI 자동 확대 방지). 측정: 변화가 9~10프레임에 고르게 분포.
- 원인 3(창 열기 380ms): 닫으면 숨기기(SettingsForm.HideOnClose) + 앱 시작 1.2초 뒤 Warm 에서 PrebuildSettings 로 미리 만들어 숨김 → 열기 약 35ms. 숨는 동안 clk/skyTm 정지. 숨은 상태에서 색이 바뀌면 폼을 없애고 다음에 새로 만듦(SetTheme). Mem 정리는 '보이는 창이 없을 때'로 조건 변경.
- 점검 도구(Bench.cs): OT_BENCH_FRAMES(전환 프레임 연속 촬영·홈 캡처·패널 높이), OT_BENCH_REOPEN(미리 만들기→열기→숨김→다시 열기).
## 2026-10-14 밤(2): 다리 잘림·클레이 사격·야구 3D 재작성
- 다리 잘림 원인: 오리기 도구(cut.cs)가 투명 시트에서도 '칸 경계 ±10px' 창으로 잘라 발이 베어졌음. 알파 시트는 창 없이 통째로 오리고, 몸 가까이 붙은 작은 덩어리(발·꼬리·소품)도 포함하도록 수정 → 의상 54종 재적용. (키가 칸 높이보다 큰 시트이니 창 클립 금지)
- 사격: 풍선(아래→위, 여러 번 맞혀야 터짐) → 클레이 접시(양옆에서 포물선, 한 발에 깨짐, 쿨다운 0.38초). 이름 '클레이 사격'.
- 야구(BatGame) 3차원 재작성: 근거 — 실제 스윙은 수평 회전, 접촉은 몸 앞, 타이밍이 빠르면 당겨 치기·늦으면 밀어 치기(MLB The Show 방향 타격), 좋은 타구 발사각 약 8~32도. 홈 뒤 시점 원근 투영, 방망이=수평면에서 도는 캡슐(손잡이 축), 충돌은 법선 충격량, 비행은 중력+공기저항, 비거리·방향·발사각으로 판정, 미니맵에 타구 방향. 점검: OT_BENCH_BATBOT(타이밍별 방향·발사각·비거리), OT_BENCH_BATSHOTS(장면 캡처).
- 프레임: 오락실은 vsync(주사율)에 맞춰 돌며 이 PC 모니터는 60Hz(59Hz) → 60fps. 한 프레임 그리기 2~8ms(쓰다듬기는 큰 그림 보간을 Bilinear 로 줄임). 점검: OT_BENCH_GAMEFPS, OT_BENCH_REALFPS.
## 2026-10-14 쪽 전환 렉 수정
- TransHost 알파 합성을 GDI+ ColorMatrix(프레임당 30~60ms)에서 멀티코어 정수 블렌드로 교체. 실제 마우스 클릭 테스트(OT_BENCH_REALINPUT, OT_NOCAP=1)에서 최대 멈춤 120~540ms -> 약 50~60ms(클릭 직후 DrawToBitmap 1회분).
- 효과음: Kenney/WobbleBoxx CC0. 비밀코드: 위 위 아래 아래 아래 좌 우 좌 우 (설정창).

## 2026-10-15
- 비밀코드: 위 위 아래 아래 좌 우 좌 우 (8키). 미확인 알림은 40초마다 소리+팝업 재상단(ProgramExt WatchPopup nag).
- Fx2.cs Glints: Kenney Particle Pack(CC0) 스프라이트 fx/*.png 로 코인/클레이/새총/탑 연출. build.bat은 UTF-8(BOM 없음)로만 저장할 것.

## 2026-10-15 (2)
- 농구: 골대를 오른쪽 벽에 붙이고 위아래로만 움직이게 변경(HoopX, hy). 야구: 스트라이크존 3x3 격자, 공 통과지점 십자 표시, 가상 방망이(DrawGhostBat).

## 2026-10-15 (3)
- 볼링 전면 재작성(Game3.cs BowlGame): 공 뒤 3D 원근 시점, 위치→누른 채 좌우로 끌어 스핀→힘 게이지 놓기, 뒤쪽에서 휘는 hook(Hk), 핀 10개 2D 원 물리+쓰러지는 핀, 핀 접근 시 카메라 이동. 벤치(OT_BENCH_NEW) 볼링 전략 테스트 포함. 벤치 스크린샷은 DPI 200%라 텍스트 위치가 어긋나 보이는 것이 정상.

## 2026-10-15 (4) 클릭 멈춤 추가 단축
- 메뉴 전환: swap 의 pages[idx].Update() 생략(덮개 아래라 낭비), DrawToBitmap -> FastPrint(WM_PRINT 를 비트맵 DC 에 직접) 24ms -> 약 15ms. 실측(NOCAP) 클릭 직후 최대 멈춤 약 44~60ms(첫 클릭은 콜드라 100ms+). 남은 병목: 새 화면 렌더링 약 15ms + 입력 도착 지연 최대 1프레임.

## 2026-10-15 (5) 마리오파티식 공통 연출 + 롤백 지점
- 롤백: 소스 백업 ot-src\backup_v9 (새버전9 상태), exe 는 Downloads 새버전9.exe.
- MaryongGame: 시작 3-2-1-시작! 카운트다운(introT), 종료 '끝!' 배너(finT), 점수 증가 시 HUD 팝(hudBump), 화면 흔들림(MiniGame.Shake). 농구 ON FIRE(3연속)+그물 출렁임, 낚시 캐스팅 타이밍 바(castPhase). 메뉴 알약/선택 표시를 go() 맨 앞으로 이동.

- 업무 메뉴 하위 탭 순서: 공문 도우미(page 7) → 기한(page 6) → 수당(8). navGroup/subNames/lastSub 변경.

## 2026-10-15 (6) 업무탭 잘림·홈 렉·마룡이 정리 (새버전12)
- FastPrint: WM_PRINT 를 자식 겹침 순서(아래->위)대로 하나씩 출력(Tree). 기존 PRF_CHILDREN 은 겹친 형제 순서를 못 지켜 업무탭 공문 도우미 탭 조각이 잘려 보였음.
- TransHost.ReleaseTo: 덮개를 걷을 때 마지막 그림을 SetDIBitsToDevice 로 쪽에 바로 얹고 RedrawWindow(VALIDATE)로 재그리기 요청 제거 -> 전환 끝 약 30ms 멈춤 제거. 되돌리기: TransHost.FastRelease=false.
- SkyBanner.CalmIdle(홈): 가만히 있을 땐 둥실/자동 동작/고빈도 그리기 없음(0.25초 간격), 마우스 대거나 클릭할 때만 반응. MascotPic.CalmIdle(사이드바)도 동일. sky.Restart 는 전환이 끝난 뒤 시작.
- 마룡이 옷: 하루 한 번만 결정(PickOutfit + outfit_today.txt in DataDir).
- 주의: 벤치는 반드시 OT_DIR 지정해서 실행할 것(미지정 시 실제 데이터 폴더 랭킹 파일에 봇 점수가 기록됨 -> 이번에 실제로 발생, 사용자에게 보고).

## 2026-10-15 (7) 새버전13: 새총 고무줄 연출·연속 격파, 야구 타이밍 판정
- SlingGame: 당길수록 가늘어지는 고무줄+가죽, 발사 후 출렁임(snapT), 한 발에 여러 민원 격파 시 연속 보너스(shotKills).
- BatGame: 스윙 타이밍 판정(timeMsg): 퍼펙트(|오차|<22ms) +10, 빠름/늦음 표시.
- 교훈: 'XX' 같은 임시 치환 문자열을 Rep1 에 쓰면 실제로 매칭되어 코드가 망가짐(한 번 발생, 복구함).

## 2026-10-15 (8) 새버전14: 오락실 전체 개편 2차 (조사 기반)
- 점프: 코요테 타임(0.09s)·점프 버퍼(0.12s)·아슬아슬 보너스(+5)·코인 콤보(x2..). 시간맞추기: PERFECT(±0.05s)+연속 보너스. 카드: 콤보(40*min(콤보,5))+마지막 속도 보너스. 2048: 되돌리기 3회(U/Z/Backspace/버튼)+합칠 때 이펙트. 공문타자: 빠르게 치면 시간+1~2초, 연속 보너스, 오타 안내. 지렁이: 5개마다 황금 별(+30, 3초 느려짐). 마룡이 쓰다듬기: 연속/10번마다 행복 폭발. 수박: 연쇄 보너스. 별 받기: 이펙트.
- 알려진 구형 테스트: OT_BENCH_LIVE 의 g2048 항목은 옛 필드 b 를 읽어 NullReference(2048 재작성 전 코드, 무시 가능).

## 2026-10-15 (9) 새버전15: 에셋 품질 + 홈 진단
- 에셋(looneybits 야구·농구 팩, CC0): spr\ball(야구공 192px), bat(방망이), coin1-4(금화 회전 프레임), bball/bball2(농구공). Spr 클래스(Fx2.cs)가 임베드 리소스 spr.<이름>.png 를 그려요. 야구 공/방망이, 농구공, 점프 코인에 적용.
- 홈 진단(OT_BENCH_HOMEIDLE): 홈에서 가만히 8초: UI 스레드는 한 번도 막히지 않음(핑 최대 6ms), 배너 204프레임 평균 15.8ms. WM_TIMER 기반 끊김 측정은 타이머 기아 때문에 틀릴 수 있음 -> 핑 방식으로 재야 함. 사용자 실제 데이터 폴더에 perf.on 생성(끊김 기록용).

## 2026-10-15 (10) 최종 인수인계
- 최종 exe 와 인수인계_최종.md(모바일 이식·조작법 포함)를 Downloads 에 둠.
