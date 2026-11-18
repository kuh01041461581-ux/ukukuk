# Cloudflare 무료 서버 배포 안내

결과: `https://heritage-attendance.<내-계정>.workers.dev` 같은 주소(HTTPS 포함)가 생깁니다.
- 관리자: `주소/admin`
- 구성원 개인 링크: `주소/m/…` (관리자 → 설정 → "구성원 개인 출석 링크"에서 복사)

## 준비 (한 번만)
1. **Cloudflare 가입** (무료, 카드 불필요): https://dash.cloudflare.com/sign-up → 메일 인증
2. **Node.js 설치** (LTS): https://nodejs.org → 터미널에서 `node -v` 로 확인
3. **코드 받기**: GitHub 저장소의 `claude/intangible-heritage-attendance-system-oxdgwk` 브랜치를 ZIP으로 받아 풀고, `attendance` 폴더로 이동

## 배포
```bash
cd attendance
npm install                              # wrangler 설치
npx wrangler login                       # 브라우저가 열리면 "허용"
npx wrangler deploy                      # 처음에는 workers.dev 주소 앞부분을 정하라고 묻습니다
npx wrangler secret put ADMIN_PASSWORD   # 관리자 비밀번호 입력 (코드·GitHub에 남지 않음)
```
- `deploy` 가 끝나면 주소가 출력됩니다. 그 주소 뒤에 `/admin` 을 붙여 접속하세요.
- 비밀번호를 설정하기 전에는 관리자 기능이 잠겨 있습니다(기본 비밀번호가 없습니다).
- 비밀번호를 설정했는데도 거부되면 `npx wrangler deploy` 를 한 번 더 실행하세요.
- 비밀번호는 길고 추측하기 어렵게 정하세요. (10분 안에 10번 틀리면 10분간 차단됩니다)

## 처음 설정 (관리자 화면)
1. 설정 탭 → **교육 장소**: 교육장에서 "현재 위치로 지정" (반경 100~200m 권장)
2. **명부 엑셀 가져오기**로 단체·구성원·전화번호 입력 → **설정 저장**
3. **강의 개설** 탭에서 강의 이름·요일·시간(필요하면 기간·휴강일) 입력 → **강의 저장**
4. 설정 → **구성원 개인 출석 링크**에서 구성원별 [안내문+링크 복사] → 카톡으로 각자에게 전달

## 자동 업데이트 (GitHub에 올라가면 Cloudflare가 알아서 배포)
GitHub 저장소와 Worker를 연결했는데도 자동 배포가 안 되면, 대부분 **어느 브랜치를 볼지** 설정이 다르기 때문입니다.
이 저장소의 기본 브랜치는 다른 프로젝트(`claude/desktop-widget-todo-app-ac7twd`)라서, Cloudflare가 그 브랜치만 지켜보고 있습니다.

1. https://dash.cloudflare.com → **Workers 및 Pages(Compute)** → `heritage-attendance` 선택
2. **설정(Settings) → 빌드(Build)** 의 **Git 리포지토리** 항목에서 [편집] (연결이 없으면 [연결] → GitHub → `ukukuk` 선택)
3. **분기 제어(Branch control)** 의 **프로덕션 분기(Production branch)** 를
   `claude/intangible-heritage-attendance-system-oxdgwk` 로 바꾸고 저장
4. **빌드 구성(Build configuration)** 확인
   - 빌드 명령(Build command): 비워 둠
   - 배포 명령(Deploy command): `npx wrangler deploy`
   - 루트 디렉터리(Root directory / Path): 비워 두거나 `/` (저장소 맨 위의 `wrangler.toml` 이 `attendance` 폴더를 사용합니다. `attendance` 로 지정해도 됩니다)
5. 저장 후, 같은 화면의 **배포(Deployments)** 탭에서 빌드가 시작되는지 확인 (다음 커밋부터 자동)
- Worker 이름은 반드시 `heritage-attendance` 여야 합니다(설정 파일의 name 과 다르면 빌드가 실패합니다).
- 빌드가 실패하면 배포 탭의 빌드 기록에서 빨간 줄을 복사해 알려 주세요.
- 이렇게 연결되면 `update-windows.bat` 은 더 이상 쓰지 않아도 됩니다.

## 업데이트·운영
- **가장 쉬운 방법 (윈도우)**: 브라우저로 저장소 ZIP을 내려받은 뒤(다운로드 폴더에 `ukukuk-….zip`), `update-windows.bat` 를 더블클릭하면 그 ZIP을 풀어 설치하고 배포까지 합니다. (처음 한 번 `npx wrangler login` 을 한 PC에서 실행해야 합니다)
- 직접 하려면 새 코드를 받은 뒤 `attendance` 폴더에서 `npm install` → `npx wrangler deploy`. 데이터(출석 기록)는 그대로 유지됩니다.
- **백업**: 관리자 → 내보내기 탭에서 CSV를 주기적으로 받아 두세요. 명부는 "현재 명부 내려받기"로 받을 수 있습니다.
- 데이터는 Cloudflare Durable Object(SQLite)에 저장됩니다. 서버가 재시작·절전되어도 사라지지 않습니다.
- 무료 한도(Workers 하루 10만 요청 등)는 단체 6개·주 20강의 규모에서는 1%도 쓰지 않습니다. (Cloudflare 정책은 바뀔 수 있으니 대시보드의 사용량을 가끔 확인하세요)

## 개인정보 참고
- 저장하는 정보: 단체명, 이름, 전화번호 **뒤 4자리**, 출석 기록, 휴대폰 식별용 임의 값. 위치 좌표는 저장하지 않고 교육장과의 거리(m)만 남깁니다.
- 기관의 개인정보 처리 방침(수집 동의 등)은 별도로 확인하세요.

## 로컬에서 시험하기 (선택)
```bash
cd attendance
echo "ADMIN_PASSWORD=test1234" > .dev.vars   # 커밋되지 않음
npx wrangler dev                              # http://127.0.0.1:8787  (실제 Cloudflare 환경과 같은 방식)
# 또는 Node만으로: ADMIN_PASSWORD=test1234 node server.js  (샘플 데이터 포함, 3000 포트)
```

## 카카오(다음) 지도 사용하기 (선택)
기본 지도는 오픈스트리트맵(키 불필요)입니다. 한국 주소·건물 검색이 더 정확한 **카카오 지도**로 바꾸려면:
1. https://developers.kakao.com 에 로그인 → **내 애플리케이션 → 애플리케이션 추가**
2. 앱 → **플랫폼 키 → JavaScript 키** 를 열고 **JavaScript SDK 도메인** 에 서버 주소(예: `https://heritage-attendance.○○.workers.dev`)를 등록
   (예전 화면에서는 "앱 설정 → 플랫폼 → Web → 사이트 도메인". 주소 끝에 `/` 는 붙이지 않습니다)
3. **제품 설정 → 카카오맵** 의 **사용 설정**을 켭니다 (메뉴 이름은 바뀔 수 있습니다)
   - 이 저장소에는 JavaScript 키(`98e9337e…`)가 기본값으로 들어 있습니다. 다른 앱 키를 쓰려면 아래 4번처럼 바꾸면 됩니다.
   - REST API 키·Admin 키는 지도에 쓰지 않습니다. 넣지 마세요.
4. 관리자 → 설정 → 교육 장소 → **"카카오(다음) 지도로 바꾸기"** 에 키를 붙여넣고 **[연결 시험]** → **[설정 저장]** 후 새로고침
- 키가 틀렸거나 웹 주소가 등록되지 않았으면 자동으로 오픈스트리트맵으로 돌아가고 안내가 나옵니다.
- JavaScript 키는 웹 주소를 등록해 두어야만 동작하는 공개용 키입니다. 출석자 화면에는 전달되지 않고 관리자 화면에서만 쓰입니다.
