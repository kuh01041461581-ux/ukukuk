# 초과근무 알리미 (마룡이)

윈도우 트레이에 상주하며 초과근무 확인 시간을 알려 주는 C# WinForms 앱이에요.
업무 도구(공문 도우미·셀 합계·화면 캡처·그림 속 글자 복사·기능 찾기), 마룡이 펫, 운세·타로, 오락실 미니게임이 들어 있어요.

> 이 브랜치(`overtime-reminder`)는 이 앱 전용이에요. 같은 저장소의 다른 브랜치(달력 등)와 섞이지 않아요.

## 폴더

| 위치 | 내용 |
|---|---|
| `*.cs` | 소스 (Program·Settings = 본체, Tools·Capture·Ocr·Palette = 업무 도구, Game*.cs = 오락실, Tarot*·Fortune* = 운세, Pet·Raise = 마룡이) |
| `spr/` `outfits/` `tarot/` `sfx/` `sfxz/` `fx/` `bg/` `noto/` | exe 안에 넣는 그림·소리·글꼴 |
| `_dims.txt` | 일부 그림의 원래 크기 (그림을 줄여 넣어도 비율 유지용) |
| `build.bat` · `build.rsp` | 윈도우 빌드 |
| `build.sh` | 리눅스 빌드 |
| `Secrets.example.cs` | 공개하면 안 되는 값의 틀 (`Secrets.cs`는 저장소에 안 올라가요) |
| `docs/` | 에셋 요청 프롬프트 등 |

## 빌드

- **윈도우**: `build.bat` → `out\OvertimeReminder.exe`
  (Visual Studio Build Tools 또는 .NET SDK의 Roslyn csc 필요)
- **리눅스**: `./build.sh` → `out/OvertimeReminder.exe` (Mono 참조 어셈블리 + .NET SDK)
- `Secrets.cs`가 없으면 `Secrets.example.cs`를 복사해서 빌드해요 (공휴일 자동 받기 키만 빈 값).
- exe는 10MB 이하로 유지해요 (그림은 128~160색으로 줄여서 넣어요).

## 버전별 릴리스 하는 방법 (앱 자동 업데이트와 연결)

앱은 이 저장소의 릴리스 중 **태그가 `ot-`로 시작**하는 가장 새 것을 찾고, 그 안의 **`OvertimeReminder.exe`** 를 받아요.

1. 버전 올리기: `Updater.cs`의 `AppVersion`과 `AssemblyInfo.cs`의 두 버전 숫자를 같이 올려요 (예: `1.4.5` / `1.4.5.0`).
2. 빌드해서 `OvertimeReminder.exe`를 만들어요.
3. GitHub → 이 저장소 → 오른쪽 **Releases** → **Draft a new release**
   - **Choose a tag**: `ot-v1.4.5` 처럼 입력 → *Create new tag*
   - **Target**: `overtime-reminder` 브랜치 선택 ← 중요 (다른 브랜치를 고르면 달력 빌드가 같이 돌아요)
   - **Release title**: `초과근무 알리미 v1.4.5`
   - 설명칸: 바뀐 점 몇 줄 (앱의 업데이트 창에 앞부분이 보여요)
   - 아래 칸에 `OvertimeReminder.exe` 끌어다 놓기 (파일 이름은 영어로 — GitHub는 한글 이름을 바꿔 버려요)
   - **Set as a pre-release** 는 체크하지 않아요 (체크하면 앱이 무시해요)
4. **Publish release**. 끝.

앱에서는 트레이 메뉴 "업데이트 확인"이나 기능 찾기(Alt+F12) → "업데이트 확인"으로 바로 받을 수 있어요.
PC에 있는 exe 이름(예: `초과근무 알리미.exe`)은 업데이트해도 그대로 유지돼요.

### 주의
- 태그 숫자는 앱의 `AppVersion`보다 커야 업데이트로 떠요. 같은 태그를 다시 쓰지 말고 숫자를 올려요.
- 잘못 올렸으면 그 릴리스를 지우거나 *pre-release* 로 바꾸면 앱이 무시해요.
