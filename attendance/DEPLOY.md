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

## 업데이트·운영
- 코드를 고친 뒤에는 `npx wrangler deploy` 만 다시 실행하면 됩니다. 데이터(출석 기록)는 그대로 유지됩니다.
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
