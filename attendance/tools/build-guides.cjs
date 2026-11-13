// 사용법 안내서 만들기: 캡처 이미지 → A4 페이지 PNG + PDF
// 사용: node tools/build-guides.cjs <캡처폴더> <출력폴더>
const { chromium } = require('playwright');
const fs = require('fs'), path = require('path');
const SH = process.argv[2], OUT = process.argv[3];
fs.mkdirSync(OUT, { recursive: true });
const img = (n) => 'data:image/png;base64,' + fs.readFileSync(path.join(SH, n + '.png')).toString('base64');

const CSS = `
@page{size:210mm 297mm;margin:0}
*{box-sizing:border-box}
body{margin:0;background:#fff;color:#222;font-family:"Noto Sans CJK KR","Noto Sans KR","Malgun Gothic","Apple SD Gothic Neo",system-ui,sans-serif;font-size:11.5pt;line-height:1.55}
.page{width:210mm;height:297mm;padding:13mm 14mm 14mm;position:relative;overflow:hidden;page-break-after:always;background:#fff}
.hd{display:flex;justify-content:space-between;align-items:center;color:#5d6b66;font-size:10pt;border-bottom:1.6px solid #2f5d50;padding-bottom:3mm}
.hd b{color:#2f5d50}
h1{font-size:25pt;margin:6mm 0 1mm;color:#1f3a33;line-height:1.25}
.sub{color:#555;font-size:12.5pt;margin-bottom:5mm}
h2{font-size:14pt;margin:5mm 0 2mm;color:#1f3a33}
.foot{position:absolute;left:14mm;right:14mm;bottom:7mm;color:#8a938f;font-size:9pt;display:flex;justify-content:space-between}
.badge{display:inline-flex;align-items:center;justify-content:center;width:6.2mm;height:6.2mm;border-radius:50%;background:#e01b1b;color:#fff;font-weight:700;font-size:9.5pt;flex:none;margin-top:.6mm}
.leg{list-style:none;margin:2mm 0 0;padding:0}.leg li{display:flex;gap:2.4mm;align-items:flex-start;margin:0 0 1.6mm;font-size:10.8pt;line-height:1.45}
.shot{border:1px solid #c9c4b6;border-radius:2.5mm;overflow:hidden;background:#f6f4ef}.shot img{display:block;width:100%}
.phone{margin:0 auto}
.cols{display:flex;gap:7mm}.col{flex:1;min-width:0}
.cap{font-weight:700;color:#1f3a33;margin:0 0 2mm;font-size:11.5pt}
.box{border:1.4px solid #2f5d50;border-radius:2.5mm;padding:3.4mm 4.2mm;margin:3mm 0}
.box.warn{border-color:#e01b1b}.box h3{margin:0 0 1.5mm;font-size:12pt;color:#1f3a33}.box.warn h3{color:#c01515}
.box ul{margin:0;padding-left:5mm}.box li{margin:0 0 1mm}
.steps{display:flex;gap:4mm;margin:3mm 0 5mm}.step{flex:1;border:1.4px solid #2f5d50;border-radius:2.5mm;padding:4mm}
.step .n{display:inline-flex;width:9mm;height:9mm;border-radius:50%;background:#2f5d50;color:#fff;font-weight:700;font-size:14pt;align-items:center;justify-content:center;margin-bottom:2mm}
.step b{display:block;font-size:12.5pt;margin-bottom:1mm}.step span{color:#444;font-size:10.8pt}
table{border-collapse:collapse;width:100%;font-size:10.5pt}th,td{border:1px solid #c9c4b6;padding:1.6mm 2.4mm;text-align:left;vertical-align:top}th{background:#f1efe7}
.chat{border:1px solid #c9c4b6;border-radius:3mm;background:#f6f4ef;padding:4mm;margin:3mm 0}
.bubble{background:#fff;border:1px solid #d6d1c4;border-radius:3mm;padding:3mm 4mm;font-size:10.8pt;max-width:120mm}
.note{color:#666;font-size:9.8pt}
.red{color:#d11}
`;
const hd = (who, title) => `<div class="hd"><span><b>국가무형유산 출석체크</b> · ${who}</span><span>${title}</span></div>`;
const foot = (n, total) => `<div class="foot"><span>빨간 박스와 번호는 화면의 해당 위치를 가리킵니다 · 화면의 이름은 모두 가상의 예시입니다</span><span>${n} / ${total}</span></div>`;
const leg = (items, cols) => `<ul class="leg" ${cols ? `style="columns:${cols};column-gap:6mm"` : ''}>${items.map(([n, t]) => `<li style="break-inside:avoid"><span class="badge">${n}</span><span>${t}</span></li>`).join('')}</ul>`;
const shot = (n, w) => `<div class="shot ${w ? 'phone' : ''}" ${w ? `style="width:${w}mm"` : ''}><img src="${img(n)}"></div>`;

// ═══════════ 출석자 안내서 ═══════════
const A = (() => {
  const T = 6, who = '출석자용', P = [];
  P.push(`${hd(who, '출석 방법 안내')}
  <h1>출석 방법 안내</h1><div class="sub">카카오톡으로 받은 링크를 눌러 출석합니다</div>
  <div class="steps">
    <div class="step"><div class="n">1</div><b>링크를 누릅니다</b><span>카톡으로 받은 출석 링크를 누릅니다. 매주 같은 링크입니다.</span></div>
    <div class="step"><div class="n">2</div><b>처음 한 번만 등록</b><span>소속 단체, 이름, 전화번호 뒤 4자리로 본인임을 확인합니다.</span></div>
    <div class="step"><div class="n">3</div><b>다음부터는 자동</b><span>링크만 누르면 이름을 고르지 않아도 자동으로 출석됩니다.</span></div>
  </div>
  <div class="chat"><div class="note" style="margin-bottom:2mm">카카오톡으로 이런 메시지가 옵니다 (예시)</div>
    <div class="bubble"><b>[출석 안내] 판소리 기초</b><br>매주 금요일 17:50~19:20<br>교육 당일 아래 링크를 눌러 출석해 주세요. (매주 같은 링크입니다)<br>위치 권한을 허용해야 합니다<br><span style="color:#2a5db0;text-decoration:underline">https://○○○○.workers.dev/c/○○○○○○</span></div></div>
  <div class="box warn"><h3>꼭 지켜 주세요</h3><ul>
    <li><b>교육장 안에서</b> 눌러야 출석됩니다. (강의 시작 30분 전부터 가능)</li>
    <li>휴대폰의 <b>위치(GPS)를 켜고</b>, 허용 창이 나오면 <b>[허용]</b>을 누릅니다.</li>
    <li>처음 등록한 방법 그대로, <b>늘 카카오톡에서 링크를 눌러</b> 여세요.</li>
    <li>등록한 휴대폰은 <b>본인 전용</b>입니다. 다른 분의 이름으로는 출석할 수 없습니다.</li></ul></div>
  <div class="box"><h3>미리 알아 두세요</h3><ul>
    <li>전화번호 뒤 4자리는 <b>담당자가 등록해 둔 번호</b>입니다. 틀렸다고 나오면 담당자에게 확인해 주세요.</li>
    <li>이름이 목록에 없으면 담당자에게 알려 주세요.</li>
    <li>이 안내서의 화면과 이름은 모두 예시입니다.</li></ul></div>
  ${foot(1, T)}`);
  P.push(`${hd(who, '처음 한 번만')}
  <h1>처음 한 번만 — 본인 등록하고 출석하기</h1><div class="sub">링크를 눌러서 아래 순서대로 하면 끝입니다</div>
  <div class="cols">
    <div class="col"><div class="cap">① 링크를 열면</div>${shot('a1_first', 56)}${leg([[1, '처음이면 이 안내가 나옵니다'], [2, '<b>위치 확인됨</b>이 보여야 합니다. 허용 창이 뜨면 [허용]']])}</div>
    <div class="col"><div class="cap">② 본인 정보 입력</div>${shot('a2_filled', 56)}${leg([[1, '소속 단체 누르기'], [2, '본인 이름 누르기'], [3, '전화번호 뒤 4자리'], [4, '[등록하고 출석하기]']])}</div>
    <div class="col"><div class="cap">③ 완료</div>${shot('a3_done', 56)}${leg([[1, '<b>출석되었습니다</b>'], [2, '이 휴대폰이 본인 전용으로 등록됨']])}</div>
  </div>${foot(2, T)}`);
  P.push(`${hd(who, '두 번째부터')}
  <h1>다음부터는 — 링크만 누르면 끝</h1><div class="sub">이름도 번호도 다시 입력하지 않습니다</div>
  <div class="cols">
    <div class="col"><div class="cap">강의 링크를 누르면 자동으로 출석됩니다</div>${shot('a4_auto', 62)}${leg([[1, '위치가 확인되면 <b>버튼을 누르지 않아도</b> 바로 출석 처리됩니다']])}</div>
    <div class="col"><div class="cap">이미 출석했다면 이렇게 보입니다</div>${shot('a4b_recognized', 62)}${leg([[1, '이 휴대폰의 주인 이름이 보입니다'], [2, '이미 출석한 상태']])}</div>
  </div>
  <h2>출석 시간 (담당자가 바꿀 수 있습니다)</h2>
  <table><tr><th>시간</th><th>결과</th></tr>
    <tr><td>강의 시작 30분 전 ~ 시작 10분 후</td><td><b style="color:#2e7d32">출석</b></td></tr>
    <tr><td>시작 10분 후 ~ 30분 후</td><td><b style="color:#d98200">지각</b></td></tr>
    <tr><td>시작 30분 후부터</td><td><b class="red">마감</b> — 담당자에게 말씀해 주세요</td></tr></table>
  ${foot(3, T)}`);
  P.push(`${hd(who, '위치가 안 될 때')}
  <h1>위치 때문에 출석이 안 될 때</h1><div class="sub">가장 많이 생기는 문제입니다</div>
  <div class="cols">
    <div class="col"><div class="cap">① 위치 권한이 꺼져 있을 때</div>${shot('a5_denied', 58)}${leg([[1, '안내대로 <b>카카오톡 앱의 위치 권한</b>을 켭니다<br>안드로이드: 설정 → 앱 → 카카오톡 → 권한 → 위치 → 허용<br>아이폰: 설정 → 카카오톡 → 위치 → 앱을 사용하는 동안'], [2, '켠 뒤 <b>[위치 다시 확인]</b>']])}</div>
    <div class="col"><div class="cap">② 교육장에서 멀다고 나올 때</div>${shot('a6_far', 58)}${leg([[1, '<b>교육장 안</b>에서 다시 눌러 주세요. 실내에서 오차가 크면 창가나 입구 쪽에서 다시 시도해 보세요. 계속 안 되면 담당자에게 말씀해 주세요.']])}</div>
  </div>${foot(4, T)}`);
  P.push(`${hd(who, '그 밖의 경우')}
  <h1>그 밖의 경우</h1><div class="sub">이런 화면이 나오면 이렇게 하세요</div>
  <div class="cols">
    <div class="col"><div class="cap">③ 다른 휴대폰에 등록되어 있다고 나올 때</div>${shot('a7_other_phone', 56)}${leg([[1, '이 이름은 이미 다른 휴대폰에 등록되어 있다는 뜻입니다'], [2, '휴대폰을 바꾸셨다면 <b>[담당자에게 기기 변경 요청]</b>을 누르고, 담당자가 승인하면 링크를 다시 열어 주세요']])}</div>
    <div class="col"><div class="cap">④ 오늘 강의가 없는 날</div>${shot('a8_notToday', 56)}${leg([[1, '강의 날이 아니거나 휴강입니다. 안내된 <b>다음 강의 날</b>에 같은 링크를 다시 눌러 주세요']])}</div>
  </div>
  ${foot(5, T)}`);
  P.push(`${hd(who, '자주 묻는 질문')}
  <h1>자주 묻는 질문</h1><div class="sub">궁금한 점은 이 쪽을 먼저 확인해 보세요</div>
  <table>
    <tr><th style="width:62mm">궁금한 점</th><th>이렇게 하세요</th></tr>
    <tr><td><b>이름을 잘못 눌러서 등록했어요</b></td><td>담당자에게 바로 말씀해 주세요. 담당자가 등록을 초기화해 드립니다.</td></tr>
    <tr><td><b>휴대폰을 바꿨어요<br>카카오톡을 다시 설치했어요</b></td><td>링크를 열어 같은 정보를 입력하면 나오는 <b>[담당자에게 기기 변경 요청]</b>을 누르세요. 담당자가 승인하면 다시 링크만 누르면 됩니다.</td></tr>
    <tr><td><b>다른 분 이름으로 대신 출석해도 되나요?</b></td><td>안 됩니다. 등록한 휴대폰은 <b>본인 전용</b>입니다.</td></tr>
    <tr><td><b>집이나 밖에서 미리 출석할 수 있나요?</b></td><td>안 됩니다. <b>교육장 안에서</b>, 강의 시작 30분 전부터 가능합니다.</td></tr>
    <tr><td><b>위치 허용을 거절했어요</b></td><td>4쪽의 방법으로 카카오톡 앱의 위치 권한을 켠 뒤 <b>[위치 다시 확인]</b>을 누르세요.</td></tr>
    <tr><td><b>시간이 지나 마감됐어요</b></td><td>담당자에게 말씀하시면 직접 출석 처리해 드립니다.</td></tr>
    <tr><td><b>전화번호 뒤 4자리가 틀렸다고 나와요</b></td><td>담당자에게 등록된 번호를 확인하세요. 연속으로 10번 틀리면 10분간 쉽니다.</td></tr>
    <tr><td><b>이름이 목록에 없어요</b></td><td>담당자에게 명부 등록을 요청하세요.</td></tr>
    <tr><td><b>링크를 잃어버렸어요</b></td><td>담당자에게 링크를 다시 받으세요. 매주 같은 링크입니다.</td></tr></table>
  <div class="box" style="margin-top:6mm"><h3>담당자 연락처</h3>
    <div style="height:11mm;border-bottom:1px solid #999;margin-top:2mm">담당자 이름 :</div>
    <div style="height:11mm;border-bottom:1px solid #999">연락처 :</div></div>
  ${foot(6, T)}`);
  return P;
})();

// ═══════════ 관리자 안내서 ═══════════
const M = (() => {
  const T = 11, who = '관리자용', P = [];
  P.push(`${hd(who, '전체 흐름')}
  <h1>출석 관리자 사용법</h1><div class="sub">담당자용 — 주소 뒤에 <b>/admin</b>을 붙여 접속합니다 (PC·휴대폰 모두 같은 서버라서 내용이 바로 연동됩니다)</div>
  <div class="steps" style="flex-wrap:wrap">
    <div class="step" style="flex:1 1 30%"><div class="n">1</div><b>처음 설정</b><span>교육 장소, 출석 시간, 명부</span></div>
    <div class="step" style="flex:1 1 30%"><div class="n">2</div><b>강의 개설</b><span>이름·요일·시간 입력</span></div>
    <div class="step" style="flex:1 1 30%"><div class="n">3</div><b>링크 보내기</b><span>카톡으로 한 번만</span></div>
    <div class="step" style="flex:1 1 30%"><div class="n">4</div><b>당일 확인</b><span>현황 보기, 수동 정정</span></div>
    <div class="step" style="flex:1 1 30%"><div class="n">5</div><b>통계</b><span>출석률, 엑셀 내려받기</span></div>
    <div class="step" style="flex:1 1 30%;border-style:dashed"><div class="n" style="background:#777">!</div><b>휴대폰 변경</b><span>기기 변경 요청 승인</span></div>
  </div>
  <div class="cols"><div class="col" style="flex:0 0 52mm"><div class="cap">메뉴 (PC: 왼쪽)</div>${shot('p1b_nav')}</div>
  <div class="col"><div class="cap">메뉴 설명</div><table><tr><th>메뉴</th><th>하는 일</th></tr>
    <tr><td>현황</td><td>날짜별 출석 현황, 링크 공유, 마감</td></tr>
    <tr><td>강의 개설</td><td>강의 만들기·수정, 휴강·기간·대상 단체</td></tr>
    <tr><td>수동 출석</td><td>직접 체크·정정, 기기 초기화</td></tr>
    <tr><td>통계</td><td>단체·강의·구성원별 출석률, CSV</td></tr>
    <tr><td>설정</td><td>장소·시간·명부·단체·개인 링크</td></tr></table>
    <div class="box" style="margin-top:4mm"><h3>휴대폰에서는</h3><ul><li>같은 메뉴가 <b>화면 아래 탭</b>으로 나옵니다.</li><li>현황은 PC에서는 표, 휴대폰에서는 카드로 보입니다.</li></ul></div></div></div>
  ${foot(1, T)}`);
  P.push(`${hd(who, '처음 설정 ①')}
  <h1>1단계 — 로그인, 출석 시간, 교육 장소</h1><div class="sub">설정 메뉴에서 한 번만 하면 됩니다</div>
  <div class="cols"><div class="col" style="flex:0 0 50mm"><div class="cap">로그인</div>${shot('p0_login')}${leg([[1, '관리자 비밀번호'], [2, '[로그인]']])}</div>
  <div class="col"><div class="box" style="margin-top:0"><h3>알아 두세요</h3><ul><li>비밀번호는 서버를 만들 때 정한 것입니다.</li><li>10번 틀리면 10분간 잠깁니다.</li></ul></div></div></div>
  <div class="cap" style="margin-top:3mm">설정 → 출석 인정 시간 · 교육 장소</div>${shot('p2_settings', 160)}
  ${leg([[1, '<b>출석 시간</b>: 시작 30분 전부터 가능, 시작 후 10분까지 출석, 30분까지 지각, 그 뒤 마감 (숫자를 바꿀 수 있음)'], [2, '<b>현장 코드</b>: 보통은 꺼 둡니다. 위치만으로 출석합니다'], [3, '주소·장소 <b>검색</b>'], [4, '교육장에서 누르면 <b>현재 위치</b>로 지정'], [5, '지도를 눌러 핀을 찍거나 끌어서 옮기기 (원 = 출석 인정 범위)'], [6, '<b>반경</b> — 100~200m 권장']], 2)}
  <div class="note">※ 맨 아래 <b>[설정 저장]</b>을 눌러야 반영됩니다. 장소를 지정하지 않으면 링크 출석이 열리지 않습니다. 이 안내서의 지도는 예시 배경이며, 실제 화면에는 지도가 표시됩니다.</div>
  ${foot(2, T)}`);
  P.push(`${hd(who, '처음 설정 ②')}
  <h1>2단계 — 명부 넣기 (엑셀)</h1><div class="sub">설정 → 명부 엑셀 가져오기</div>
  ${shot('p3_roster', 164)}${leg([[1, '엑셀(.xlsx/.xls) 또는 CSV 파일 선택 — 첫 줄: <b>단체 · 이름 · 전화번호</b>'], [2, '<b>[양식 내려받기]</b>로 빈 양식을 받을 수 있습니다'], [3, '<b>미리보기</b>로 인원·새 단체·경고를 확인'], [4, '<b>[명부에 반영]</b> → 아래 <b>[설정 저장]</b>을 눌러야 저장됩니다']], 2)}
  <div class="cap" style="margin-top:3mm">단체와 구성원 직접 고치기</div>${shot('p4_groups', 164)}
  ${leg([[1, '<b>단체 PIN</b> — 단체 대표자가 출석을 대신 입력할 때 씁니다'], [2, '구성원은 한 줄에 한 명: <b>이름 전화번호뒤4자리</b>'], [3, '단체 삭제 (구성원과 기록이 함께 사라지니 주의)']])}
  <div class="box warn"><h3>전화번호 뒤 4자리는 꼭 넣어 주세요</h3>출석자가 처음 본인 등록할 때 이 번호로 확인합니다. 번호가 없는 분은 직접 등록할 수 없고 수동 출석으로 처리해야 합니다. 같은 단체에 같은 이름이 있으면 &quot;김영자(A)&quot;처럼 구분해 주세요.</div>
  ${foot(3, T)}`);
  P.push(`${hd(who, '강의 개설 ①')}
  <h1>3단계 — 강의 개설: 새 강의 만들기</h1><div class="sub">시간과 날짜는 <b>직접 입력</b>하거나 버튼으로 고르세요</div>
  ${shot('p6_lec_form', 168)}
  ${leg([[1, '강의 이름'], [2, '<b>매주 반복</b> 또는 <b>하루만</b>'], [3, '요일 — 여러 개 고르면 요일마다 강의가 만들어집니다'], [4, '<b>대상 단체</b> — 고르지 않으면 모든 단체 (통계에 영향)'], [5, '시작·끝 시간 직접 입력 — 알아서 맞춰 줍니다'], [6, '자주 쓰는 <b>시작 시간</b>·<b>강의 길이</b> 버튼'], [7, '<b>[강의 만들기]</b> — 누르면 바로 저장됩니다']], 2)}
  <table style="margin-top:2mm"><tr><th>이렇게 입력하면</th><th>이렇게 인식</th><th>이렇게 입력하면</th><th>이렇게 인식</th></tr>
    <tr><td>10시 반 · 1030 · 10:30</td><td>10:30</td><td>1120 · 11/20 · 11월 20일</td><td>11월 20일</td></tr>
    <tr><td>오후 2시 · 2pm · 2시</td><td>14:00</td><td>내일 · 모레 · 오늘</td><td>해당 날짜</td></tr></table>
  <div class="note" style="margin-top:1.5mm">※ 오전/오후를 쓰지 않으면 1~7시는 오후로 봅니다. 인식할 수 없는 입력은 빨간 안내가 나옵니다.</div>
  ${foot(3 + 1, T)}`);
  P.push(`${hd(who, '강의 개설 ②')}
  <h1>3단계 — 만든 강의 관리</h1><div class="sub">고치면 <b>바로 자동 저장</b>됩니다 (저장 버튼 없음)</div>
  ${shot('p7_lec_cards', 160)}
  ${leg([[1, '만든 직후 안내 — 여기서 바로 <b>링크를 공유</b>할 수 있습니다'], [2, '<b>저장됨 ✓</b> 표시 — 고칠 때마다 자동 저장'], [3, '시간 수정 — 시작을 바꾸면 끝도 같은 길이로 따라갑니다'], [4, '<b>대상 단체</b> 켜기/끄기'], [5, '<b>휴강일</b> 추가 (예: 11/27) — 그날은 출석이 열리지 않습니다'], [6, '<b>카톡 공유</b> / 링크 복사 — 강의마다 <b>매주 같은 링크</b>']], 2)}
  <div class="box"><h3>출석은 강의 날에 자동으로 열립니다</h3><ul><li>링크를 한 번만 보내 두면, 강의 날에 링크를 누를 때 그날 출석이 <b>자동으로 개설</b>됩니다.</li><li>강의가 없는 날(다른 요일·휴강일)에 누르면 &quot;오늘은 강의가 없는 날입니다 · 다음 강의 ○월 ○일&quot;이 나옵니다.</li><li>기간(시작일~종료일)을 정하면 그 기간에만 열립니다. 하루짜리 강의는 [하루만]을 고르세요.</li></ul></div>
  ${foot(5, T)}`);
  P.push(`${hd(who, '링크 보내기')}
  <h1>4단계 — 링크 보내기</h1><div class="sub">두 가지 링크가 있습니다. 보통은 ①번만 단톡방에 보내면 됩니다</div>
  <table><tr><th></th><th>① 강의 링크 (추천)</th><th>② 개인 링크</th></tr>
    <tr><td><b>어디서</b></td><td>강의 개설 · 현황의 <b>[카톡 공유]</b></td><td>설정 → 구성원 개인 출석 링크</td></tr>
    <tr><td><b>누구에게</b></td><td>단톡방에 한 번 (모두 같은 링크)</td><td>구성원에게 한 명씩</td></tr>
    <tr><td><b>출석자</b></td><td>처음 한 번 본인 등록 후 자동 인식</td><td>링크가 곧 본인 — 등록 절차 없음</td></tr>
    <tr><td><b>매주</b></td><td>매주 같은 링크, 강의 날에 자동 개설</td><td>한 번 보내면 계속 사용</td></tr></table>
  <div class="cap" style="margin-top:4mm">설정 → 구성원 개인 출석 링크</div>${shot('p5_links')}
  ${leg([[1, '전체 링크 목록을 복사 (엑셀용)'], [2, '<b>카톡 공유</b> — 휴대폰에서는 공유 창이 열립니다. 거기서 <b>카카오톡</b>을 고르면 바로 보낼 수 있습니다 (PC에서는 문구 복사)'], [3, '안내문 복사'], [4, '<b>새 링크 발급</b> — 링크가 새어 나갔을 때. 이전 링크와 등록된 휴대폰이 무효가 됩니다']])}
  ${foot(6, T)}`);
  P.push(`${hd(who, '교육 당일')}
  <h1>5단계 — 교육 당일: 현황 보기</h1><div class="sub">날짜별로 어느 단체가 몇 명 출석했는지 한눈에 봅니다</div>
  ${shot('p1_overview')}
  ${leg([[1, '<b>현황</b> 메뉴'], [2, '날짜 이동 (◀ ▶, 직접 선택)'], [3, '단체별 <b>출·지·결·미</b> 인원과 지각·결석자 이름'], [4, '<b>카톡 공유</b> — 휴대폰에서는 공유 창, PC에서는 복사'], [5, '<b>마감 / 다시 열기</b> — 출석을 직접 닫거나 다시 엽니다']])}
  <table style="margin-top:2mm"><tr><th>표시</th><th>뜻</th></tr>
    <tr><td><b style="color:#2e7d32">출</b> 출석 · <b style="color:#d98200">지</b> 지각 · <b class="red">결</b> 결석</td><td>이미 처리된 인원</td></tr>
    <tr><td><b>미</b> 미입력</td><td>아직 출석 처리가 안 된 대상 인원 (마감 후에는 결석으로 계산)</td></tr>
    <tr><td><b>대상 아님</b></td><td>그 강의의 대상 단체가 아닌 단체 (계산에서 제외)</td></tr></table>
  ${foot(7, T)}`);
  P.push(`${hd(who, '수동 정정 · 기기 변경')}
  <h1>수동 출석·정정 / 기기 변경 승인</h1><div class="sub">휴대폰이 어려운 분, 마감 후 도착한 분, 휴대폰을 바꾼 분을 처리합니다</div>
  <div class="cap">수동 출석 메뉴</div>${shot('p8_manual', 160)}
  ${leg([[1, '날짜'], [2, '강의 선택'], [3, '<b>[불러오기]</b>'], [4, '단체 <b>전원 출석</b>'], [5, '<b>출석 / 지각 / 결석</b> — 같은 버튼을 다시 누르면 해제. 지각·결석은 사유 메모 가능'], [6, '<b>[저장]</b>을 눌러야 반영됩니다']], 2)}
  <div class="cap" style="margin-top:3mm">현황 맨 위 — 기기 변경 요청</div>${shot('p9_requests', 160)}
  ${leg([[1, '휴대폰을 바꾼 분이 요청하면 여기에 나타납니다'], [2, '본인이 맞는지 확인하고 <b>승인</b> → 새 휴대폰이 본인 전용이 됩니다'], [3, '거절']])}
  <div class="note">※ 등록된 휴대폰을 비우려면 수동 출석의 <b>[기기 초기화]</b> 또는 설정의 개인 링크 목록에서 <b>[기기 초기화]</b>를 누르세요.</div>
  ${foot(8, T)}`);
  P.push(`${hd(who, '통계')}
  <h1>출석률 통계</h1><div class="sub">단체별·강의별·구성원별 출석률을 봅니다</div>
  ${shot('p10_stats', 148)}
  ${leg([[1, '<b>기간</b>을 정하고 [조회]'], [2, '<b>엑셀(CSV) 내려받기</b> — 날짜·강의·단체·이름·상태가 모두 들어 있습니다'], [3, '<b>단체별</b> 출석·지각·결석과 출석률 막대'], [4, '<b>구성원별</b> (출석률이 낮은 순) — 자주 빠지는 분을 찾을 수 있습니다']], 2)}
  <div class="box"><h3>계산 방법</h3><ul><li>출석률 = (<b>출석 + 지각</b>) ÷ 전체 인원</li><li>대상 단체가 지정된 강의는 <b>대상 단체 구성원만</b> 계산에 넣습니다.</li><li>&quot;실시된 교육&quot; = 그 날 열린 강의입니다. 휴강이면 강의 개설에서 <b>휴강일</b>로 등록해 두세요.</li></ul></div>
  ${foot(9, T)}`);
  P.push(`${hd(who, '휴대폰에서')}
  <h1>휴대폰에서 관리자 쓰기</h1><div class="sub">현장에서 휴대폰으로 보고, 보내고, 정정할 수 있습니다</div>
  <div class="cols">
    <div class="col"><div class="cap">현황</div>${shot('m1_overview', 52)}${leg([[1, '하단 탭으로 메뉴 이동'], [2, '<b>카톡 공유</b> — 공유 창에서 카카오톡 선택'], [3, '단체별 카드']])}</div>
    <div class="col"><div class="cap">강의 개설</div>${shot('m2_lec_form', 52)}${leg([[1, '이름'], [2, '매주 반복 / 하루만'], [3, '요일 고르기'], [4, '시작 시간·길이 버튼']])}</div>
    <div class="col"><div class="cap">수동 출석</div>${shot('m3_manual', 52)}${leg([[1, '강의 선택'], [2, '출석/지각/결석 버튼'], [3, '전원 출석']])}</div>
  </div>
  <div class="box"><h3>팁</h3><ul><li>휴대폰 브라우저 메뉴에서 <b>홈 화면에 추가</b>해 두면 아이콘으로 바로 열립니다.</li><li>PC와 휴대폰은 같은 서버를 쓰므로, 한쪽에서 고친 내용이 다른 쪽에서도 바로 보입니다.</li></ul></div>
  ${foot(10, T)}`);
  P.push(`${hd(who, '운영 체크리스트')}
  <h1>운영 체크리스트 · 자주 생기는 일</h1><div class="sub">처음 시작할 때와 매주 확인할 것</div>
  <div class="cols"><div class="col"><div class="box"><h3>처음 시작할 때</h3><ul>
    <li>□ 교육 장소 지정 (교육장에서 [현재 위치로 지정])</li><li>□ 명부 가져오기 + <b>전화번호 뒤 4자리</b> 확인</li><li>□ 강의 개설 (대상 단체·휴강일)</li><li>□ 강의 링크를 단톡방에 공유</li><li>□ <b>첫 교육 때 현장에서 등록을 도와 주기</b></li><li>□ 소수 인원으로 먼저 시험 (안드로이드·아이폰)</li></ul></div></div>
    <div class="col"><div class="box"><h3>매주</h3><ul>
    <li>□ 휴강이면 휴강일 등록</li><li>□ 교육 후 현황에서 결석·미입력 확인</li><li>□ 마감 후 도착한 분은 수동 출석</li><li>□ 기기 변경 요청 확인·승인</li><li>□ 월 1회 통계에서 <b>CSV 내려받아 보관</b></li></ul></div></div></div>
  <div class="box warn"><h3>문제가 생기면</h3><table>
    <tr><th>증상</th><th>확인·해결</th></tr>
    <tr><td>&quot;교육 장소가 설정되지 않아 출석할 수 없습니다&quot;</td><td>설정 → 교육 장소 지정 후 [설정 저장]</td></tr>
    <tr><td>출석자가 위치가 멀다고 나옴</td><td>반경을 150~200m로 늘려 보기, 창가·입구에서 다시 시도 안내</td></tr>
    <tr><td>&quot;다른 휴대폰에 등록되어 있습니다&quot;</td><td>휴대폰을 바꾼 경우 기기 변경 요청 승인 또는 기기 초기화</td></tr>
    <tr><td>출석 시간이 지나 마감됨</td><td>수동 출석에서 직접 처리 (또는 설정에서 마감 시간 늘리기)</td></tr>
    <tr><td>링크가 다른 사람에게 퍼짐</td><td>개인 링크는 [새 링크 발급]. 강의 링크는 위치와 본인 등록으로 보호됨</td></tr>
    <tr><td>휴강인데 결석으로 계산됨</td><td>강의 개설 카드에서 휴강일 등록</td></tr></table></div>
  ${foot(11, T)}`);
  return P;
})();

(async () => {
  const br = await chromium.launch(); const ctx = await br.newContext({ viewport: { width: 794, height: 1123 }, deviceScaleFactor: 2 });
  const page = await ctx.newPage();
  for (const [name, pages] of [['출석자_사용법', A], ['관리자_사용법', M]]) {
    await page.setContent(`<!doctype html><html lang="ko"><head><meta charset="utf-8"><style>${CSS}</style></head><body>${pages.map((p) => `<section class="page">${p}</section>`).join('')}</body></html>`);
    await page.waitForTimeout(500);
    const els = await page.$$('.page');
    for (let i = 0; i < els.length; i++) {
      const over = await els[i].evaluate((e) => e.scrollHeight > e.clientHeight + 2 ? e.scrollHeight - e.clientHeight : 0);
      if (over) console.log(`  ⚠ ${name} ${i + 1}쪽: 내용이 ${over}px 넘칩니다`);
      await els[i].screenshot({ path: path.join(OUT, `${name}_${String(i + 1).padStart(2, '0')}.png`) });
    }
    await page.pdf({ path: path.join(OUT, `${name}.pdf`), width: '210mm', height: '297mm', printBackground: true });
    console.log('✓', name, els.length + '쪽');
  }
  await br.close();
})();
