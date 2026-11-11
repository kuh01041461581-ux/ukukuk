/* ===== 설정·백업 / 시작 ===== */
function renderSettings() {
  const P = $('#p-settings'); const lb = S.lastBackup ? new Date(S.lastBackup) : null;
  P.innerHTML = `
  <div class="card"><h2>백업 · 복원</h2>
    <p class="sub" style="margin:0 0 10px">${IS_EXE ? '입력한 내용은 <b>이 PC의 AppData 폴더</b>에 자동 저장됩니다. PC를 바꾸거나 포맷할 때를 대비해 <b>가끔 백업 파일을 다른 곳에 저장</b>해 두세요.' : '입력한 내용은 <b>이 브라우저 안</b>에 자동 저장됩니다. 브라우저 방문 기록·사이트 데이터를 지우거나 다른 PC에서 열면 보이지 않으니, <b>가끔 백업 파일을 저장</b>해 두세요.'} (마지막 백업: ${lb ? lb.toLocaleString('ko-KR') : '없음'})</p>
    <div class="row"><button class="btn primary" data-act="backup">백업 파일 저장 (.json)</button><button class="btn" data-act="restore">백업 파일 불러오기</button></div></div>
  ${IS_EXE ? `<div class="card"><h2>엑셀에서 가져오기</h2><p class="sub" style="margin:0 0 10px">엑셀 파일을 끌어 놓거나 선택하면 일상경비·공공요금을 자동으로 구분해 다시 읽어 옵니다(지금 입력된 내용이 바뀌니 먼저 백업하세요).</p>
    <div class="row"><button class="btn" data-act="xl-daily">엑셀 파일 선택 (일상경비·공공요금)</button><button class="btn" data-act="datadir">데이터 폴더 열기</button></div>
    <p class="muted" style="margin:8px 0 0">데이터는 자동으로 <b>내 PC의 일상경비관리 폴더(AppData)</b>에 저장되고, 하루 한 번 자동 백업(30일)됩니다.</p></div>` : ''}
  <div class="card"><h2>기준일 · 연도</h2>
    <div class="row"><label class="fld" style="margin:0">연도<input type="number" id="s-year" value="${S.year}" style="width:100px"></label>
    <button class="btn" data-act="newyear">새 연도 시작 (번호·항목은 유지, 금액만 비우기)</button></div></div>
  <div class="card"><h2>초기화</h2><p class="sub" style="margin:0 0 10px">처음 상태(빈 장부)로 되돌립니다. 지금까지 입력한 내용은 사라지니 먼저 백업하세요.</p><button class="btn danger" data-act="reset">처음 상태로 되돌리기</button></div>
  <div class="card help"><h2>사용법 요약</h2><ul style="margin:0;padding-left:18px">
    <li><b>일상경비</b> 탭: 지출액 칸에 실집행액을 넣으면 집행률·잔액·막대가 바로 바뀝니다. <code>1,000,000+250,000</code> 처럼 계산식도 됩니다. 지급 예정 금액은 '지급예정' 칸의 ＋ 버튼으로 적어 두세요.</li>
    <li><b>공공요금</b> 탭: 전기·상하수도·통신별로 '월별 입력'에서 고지서 상태(☐미수령 → ☑수령 → –청구없음)를 누르고 금액을 입력합니다. 합계는 자동 계산됩니다. 계약번호·납부번호를 누르면 복사됩니다.</li>
    <li><b>PDF 불러오기</b> 탭: 합본예산서(PDF)를 올리면 총예산액이, 통계목별 조회 엑셀을 올리면 실집행액(지출액)이 채워집니다. 적용 전에 바뀌는 값을 미리 보여 드립니다.</li>
    <li>화면 맨 위 <b>기준일</b>을 바꾸면 '경과율' 점선이 따라 움직입니다.</li></ul></div>`;
}
$('#p-settings').addEventListener('click', e => {
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act;
  if (a === 'xl-daily' || a === 'xl-util') pickFiles('.xlsx,.xlsm');
  else if (a === 'datadir') api().open_data_dir();
  else if (a === 'backup') { S.lastBackup = new Date().toISOString(); save(); download(`일상경비_공공요금_백업_${today()}.json`, JSON.stringify(S, null, 1)); renderSettings(); updateBackupChip(); }
  else if (a === 'restore') {
    const p = $('#pick'); p.accept = '.json,application/json'; p.value = '';
    p.onchange = async () => { try { const d = JSON.parse(await p.files[0].text()); if (!d.lines || !d.util) throw 0; if (confirm('현재 내용을 백업 파일로 바꿉니다. 계속할까요?')) { S = d; save(); init(); toast('불러왔습니다'); } } catch (er) { toast('올바른 백업 파일이 아닙니다'); } };
    p.click();
  } else if (a === 'reset') {
    if (confirm('처음 상태로 되돌릴까요? 입력한 내용이 모두 사라집니다.') && confirm('정말 되돌릴까요? (백업을 먼저 저장했는지 확인하세요)')) { S = window.__SEED__ ? deepCopy(window.__SEED__) : emptyState(); save(); init(); toast('처음 상태로 되돌렸습니다'); }
  } else if (a === 'newyear') {
    const y = +$('#s-year').value; if (!y || !confirm(`${y}년 새 장부를 시작합니다.\n- 지출·지급예정·비고·월별 금액이 비워집니다(예산액은 유지 → 새 예산서를 불러와 갱신하세요)\n- 올해 월별 금액은 '전년' 비교값으로 보관됩니다\n먼저 백업 파일을 저장하셨나요?`)) return;
    S.year = y; S.asOf = y + '-01-01'; S.memo = '';
    S.lines.forEach(l => { l.spent = 0; l.scheduled = []; l.note = ''; l.ehojo = null; });
    for (const k in S.util) S.util[k].items.forEach(it => { it.dec = it.amt[11]; it.py = it.amt.slice(); it.amt = Array(12).fill(null); it.rcv = Array(12).fill(it.inactive ? 2 : 0); });
    S.utilBudget = { elec: null, water: null, tel: null }; UI.util.month = null; save(); init(); toast(`${y}년 장부를 시작했습니다`);
  }
});
$('#p-settings').addEventListener('change', e => { if (e.target.id === 's-year') { /* 값만 보관, 새 연도 시작 버튼에서 사용 */ } });

/* 숫자 칸을 떠날 때 콤마 형식 복원 */
document.addEventListener('focusout', e => { const t = e.target; if (t.matches && t.matches('input.cellin[inputmode="numeric"]')) { const v = parseNum(t.value); if (v != null && !isNaN(v)) t.value = won(v); } });

const TABS = [['daily', '일상경비 집행현황'], ['util', '공공요금 집행현황'], ['import', '파일 불러오기'], ['settings', '설정·백업']];
const RENDER = { daily: renderDaily, util: renderUtil, import: renderImport, settings: renderSettings };
function showTab(t) {
  UI.tab = t; $$('#tabs button').forEach(b => b.setAttribute('aria-selected', b.dataset.t === t));
  TABS.forEach(([k]) => { $('#p-' + k).hidden = k !== t; }); RENDER[t](); window.scrollTo(0, 0);
}
function updateBackupChip() {
  const c = $('#bk'); const lb = S.lastBackup ? new Date(S.lastBackup) : null; const days = lb ? (Date.now() - lb) / 86400000 : 999;
  c.textContent = lb ? '백업 ' + (lb.getMonth() + 1) + '/' + lb.getDate() : '백업 안 함'; c.classList.toggle('warn', days > 14); c.title = '[설정·백업] 탭에서 백업 파일을 저장하세요';
}
function init() {
  $('#ttl').textContent = `${S.year} 일상경비·공공요금 관리`; document.title = `${S.year} 일상경비·공공요금 관리`;
  $('#asof').value = S.asOf; updateBackupChip();
  $('#tabs').innerHTML = TABS.map(([k, n]) => `<button role="tab" data-t="${k}" aria-selected="${k === UI.tab}">${n}</button>`).join('');
  UI.util.month = null; UI.imp = null; showTab(UI.tab);
}
$('#tabs').addEventListener('click', e => { const b = e.target.closest('button[data-t]'); if (b) showTab(b.dataset.t); });
$('#asof').addEventListener('change', e => { if (e.target.value) { S.asOf = e.target.value; save(); RENDER[UI.tab](); } });
$('#theme').addEventListener('click', () => {
  const r = document.documentElement; const cur = r.dataset.theme || 'auto'; const nx = cur === 'auto' ? 'dark' : cur === 'dark' ? 'light' : 'auto';
  if (nx === 'auto') delete r.dataset.theme; else r.dataset.theme = nx; try { localStorage.setItem('ilsang-theme', nx); } catch (e) { } toast('화면: ' + { auto: '자동', dark: '어둡게', light: '밝게' }[nx]);
});
try { const th = localStorage.getItem('ilsang-theme'); if (th && th !== 'auto') document.documentElement.dataset.theme = th; } catch (e) { }
window.addEventListener('beforeprint', () => { if (!$$('.tab-panel.printing').length) $('#p-' + UI.tab).classList.add('printing'); });
window.addEventListener('afterprint', () => $$('.tab-panel').forEach(p => p.classList.remove('printing')));

(async () => {
  try {
    if (IS_EXE) { await waitApi(); const raw = await api().load_state(); S = raw ? JSON.parse(raw) : emptyState(); }
    else S = loadState();
  } catch (e) { S = emptyState(); alert('저장된 데이터를 읽지 못해 빈 장부로 시작합니다: ' + (e.message || e)); }
  if (!S.utilBudget) S.utilBudget = { elec: null, water: null, tel: null };
  for (const k of ['elec', 'water', 'tel']) if (!S.util[k]) S.util[k] = emptySec(k);
  init();
})();
