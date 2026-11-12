/* ===== 공공요금 집행현황 탭 ===== */
const ST_TIP = { 0: '미수령 — 아직 고지서를 받지 못함', 1: '수령 · 미납 — 고지서는 받았고 아직 납부 전', 3: '납부 완료', 2: '청구 없음(2천원 미만 합산 등)' };
function manKey(n) { return n >= 1e8 ? (n / 1e8).toFixed(1) + '억' : n >= 1e4 ? Math.round(n / 1e4).toLocaleString('ko-KR') + '만' : String(Math.round(n)); }

function visSeries() { const v = UI.util.series; return CATS.filter(([k]) => v[k] && S.util[k]); }
function monthlyChart() {
  const vis = visSeries();
  if (!vis.length) return '<div class="empty">위의 체크박스에서 보고 싶은 요금을 선택하세요</div>';
  const data = MON.map((_, m) => vis.map(([k]) => monthTotal(S.util[k], m)));
  const maxV = Math.max(1, ...data.map(a => Math.max(...a)));
  const mag = Math.pow(10, Math.floor(Math.log10(maxV))); let nice = mag; for (const f of [1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 10]) { if (maxV <= f * mag) { nice = f * mag; break; } }
  const W = 900, H = 250, L = 46, R = 10, Tp = 18, B = 26, iw = W - L - R, ih = H - Tp - B;
  const slot = iw / 12, grp = slot * 0.8, bw = grp / vis.length, showVal = vis.length === 1;
  let g = '';
  for (let i = 0; i <= 4; i++) { const y = Tp + ih - ih * i / 4; g += `<line class="gd" x1="${L}" x2="${W - R}" y1="${y}" y2="${y}"/><text x="${L - 6}" y="${y + 4}" text-anchor="end">${manKey(nice * i / 4)}</text>`; }
  data.forEach((a, m) => {
    const gx = L + slot * m + (slot - grp) / 2;
    a.forEach((v, i) => {
      const k = vis[i][0], nm = vis[i][1]; const h = v / nice * ih; const x = gx + i * bw + 1, w = Math.max(2, bw - 2);
      if (v) {
        const y = Tp + ih - h, r = Math.min(5, w / 2), c = CATCOLOR[k];
        g += `<path class="barflat" d="M${x},${Tp + ih}V${y + r}Q${x},${y} ${x + r},${y}H${x + w - r}Q${x + w},${y} ${x + w},${y + r}V${Tp + ih}Z" fill="${c}" data-tip="${MON[m]}분 ${nm}\n${won(v)}원"/>`;
        if (showVal) g += `<text x="${x + w / 2}" y="${y - 6}" text-anchor="middle" style="fill:var(--text);font-weight:600">${manKey(v)}</text>`;
      }
    });
    g += `<text x="${L + slot * (m + 0.5)}" y="${H - 8}" text-anchor="middle">${m + 1}월</text>`;
  });
  const defs = '';
  return `<svg viewBox="0 0 ${W} ${H}" width="100%" role="img" aria-label="월별 공공요금 지출 막대그래프(${vis.map(x => x[1]).join('·')})">${defs}${g}<line class="ax" x1="${L}" x2="${W - R}" y1="${Tp + ih}" y2="${Tp + ih}"/></svg>`;
}

function utilCatChips(u) {
  return `<div class="catchips" role="group" aria-label="요금 종류">${todoSummary().map(t => `<button class="todo-chip ${u.cat === t.k ? 'sel' : ''} ${t.missing || t.unpaid ? 'on' : ''}" style="--c:${CATCOLOR[t.k]}" data-act="cat" data-v="${t.k}"><i></i><b>${t.nm}</b>${t.unpaid ? `<span class="badge bad">미납 ${t.unpaid}건 · ${won(t.amt)}원</span>` : ''}${t.missing ? `<span class="badge warn">미수령 ${t.missing}건</span>` : ''}${t.missing || t.unpaid ? '' : '<span class="badge good">완료</span>'}</button>`).join('')}</div>`;
}
function utilControls(sec) {
  const u = UI.util;
  return `<div class="row noprint ctlrow" style="margin-left:auto">
    ${(sec.tips || []).length ? `<span class="muted" style="font-size:12px" data-tip="${esc(sec.tips.join('\n'))}">※ ${esc(sec.tips[0])}</span>` : ''}
    <input type="search" data-u="q" data-nav="q" value="${esc(u.q)}" placeholder="이 종류에서 검색" style="border:1px solid var(--line);border-radius:8px;padding:6px 10px;background:var(--surface-2);width:190px">
    <label class="row" style="gap:4px;font-size:12px"><input type="checkbox" data-u="inactive" ${u.showInactive ? 'checked' : ''}> 미사용 포함</label></div>`;
}
function utilStat() {
  const u = UI.util; const grand = sum(CATS, ([k]) => S.util[k] ? catTotal(S.util[k]) : 0);
  let h = `<div class="cat-cards" style="margin-top:0">`;
  for (const [k, nm] of CATS) {
    const s = S.util[k]; if (!s) continue; const tot = catTotal(s), bud = S.utilBudget[k];
    h += `<div class="cat-card" style="--c:${CATCOLOR[k]}"><div class="row" style="justify-content:space-between"><b>${nm}</b><span class="muted">${s.items.filter(i => !i.inactive).length}곳</span></div>
      <div class="big">${won(tot)}<span style="font-size:13px;font-weight:400"> 원</span></div>
      <div class="row" style="margin:6px 0"><span class="muted">연 예산</span><input class="cellin" style="width:130px;border-color:var(--line)" data-u="budget" data-cat="${k}" data-nav="ub-${k}" value="${bud ? won(bud) : ''}" placeholder="미입력" inputmode="numeric"><span class="muted">원</span></div>
      ${bud ? `${barHTML(tot, 0, bud)}<div class="row" style="justify-content:space-between;margin-top:4px"><b>${pct1(pct(tot, bud))}</b><span class="muted">잔액 ${won(bud - tot)}원</span></div>` : '<div class="hint">예산을 입력하면 집행률 막대가 나옵니다 ([파일 불러오기]에서 자동 계산 가능)</div>'}
      </div>`;
  }
  h += `</div>
  <div class="card"><h2>월별 공공요금 지출 <span class="muted" style="font-weight:400;font-size:12px">체크한 요금만 막대로 보입니다 · 선택 합계 ${won(sum(visSeries(), ([k]) => catTotal(S.util[k])))}원</span></h2>
    <div class="legend noprint" role="group" aria-label="표시할 요금">${CATS.map(([k, nm]) => S.util[k] ? `<label class="serchk" style="--c:${CATCOLOR[k]}"><input type="checkbox" data-u="series" data-cat="${k}" ${u.series[k] ? 'checked' : ''}><i></i>${nm}</label>` : '').join('')}
      <button class="btn sm" data-act="series-all">3개 모두</button></div>
    ${monthlyChart()}
    <details class="fold"><summary>표로 보기</summary><div class="tbl-wrap"><table class="t"><thead><tr><th>구분</th>${MON.map(x => `<th>${x}</th>`).join('')}<th>합계</th></tr></thead><tbody>
    ${CATS.map(([k, nm]) => { const s = S.util[k]; if (!s) return ''; return `<tr><td>${nm}</td>${MON.map((_, i) => `<td class="num">${won(monthTotal(s, i))}</td>`).join('')}<td class="num"><b>${won(catTotal(s))}</b></td></tr>`; }).join('')}
    <tr class="tot"><td>합계</td>${MON.map((_, i) => `<td class="num">${won(sum(CATS, ([k]) => S.util[k] ? monthTotal(S.util[k], i) : 0))}</td>`).join('')}<td class="num">${won(grand)}</td></tr></tbody></table></div></details>
  </div>`;
  return h;
}
function utilFwd() {
  const parts = CATS.map(([k, nm]) => { const sec = S.util[k]; if (!sec || !(sec.forwards || []).length) return '';
    return `<div class="card" style="margin-top:0;margin-bottom:12px"><h2>${sec.title} — 타 계·부서 전달처 (${sec.forwards.length}건)</h2><div class="tbl-wrap fit"><table class="t"><thead><tr><th>관리 항목</th><th>번호</th><th>전달처</th><th>비고</th></tr></thead><tbody>
      ${sec.forwards.map(f => `<tr><td>${esc(f.name)}</td><td class="nolist">${esc(f.numbers)}</td><td>${esc(f.to)}</td><td>${esc(f.memo)}</td></tr>`).join('')}</tbody></table></div></div>`; }).join('');
  return parts || '<div class="empty">전달처 안내 정보가 없습니다</div>';
}
function renderUtil() {
  const P = $('#p-util'); const u = UI.util; u.sub = u.sub || 'pay'; const snap = scrollSnap(P);
  const sec = S.util[u.cat]; if (!sec) { P.innerHTML = '<div class="empty">공공요금 데이터가 없습니다</div>'; return; }
  if (u.month == null) u.month = defaultMonth(sec);
  const m = u.month, dm = defaultMonth(sec);
  const SUBS = [['pay', '납부 확인'], ['stat', '현황 · 그래프'], ['year', '연간 표'], ['fwd', '전달처 안내']];
  let h = `<div class="row noprint subbar"><div class="pills subtabs" role="tablist">${SUBS.map(([k, n]) => `<button class="pill" role="tab" aria-pressed="${u.sub === k}" data-act="usub" data-v="${k}">${n}</button>`).join('')}</div>
    <span class="spacer"></span><button class="btn sm" data-act="find" data-tip="모든 요금 종류에서 번호·장소로 찾기">찾기 Ctrl+F</button>
    <button class="btn sm primary" data-act="payform" data-tip="스캔 후 만든 지출 폼 엑셀을 올리면 기록에서 찾아 납부 완료로 바꿉니다">지출 폼 엑셀로 납부 처리</button>
    ${u.sub === 'pay' || u.sub === 'year' ? '<button class="btn sm" data-act="additem">＋ 항목</button><button class="btn sm" data-act="tsv">표 복사</button><button class="btn sm" data-act="printu">인쇄</button>' : ''}</div>`;
  if (u.sub === 'pay') h += `<div class="row chiprow">${utilCatChips(u)}${utilControls(sec)}</div><div class="card payc"><div id="u-body">${monthView(sec, m, dm)}</div></div>`;
  else if (u.sub === 'year') h += `<div class="row chiprow">${utilCatChips(u)}${utilControls(sec)}</div><div class="card payc"><div id="u-body">${yearView(sec, dm)}</div></div>`;
  else if (u.sub === 'stat') h += utilStat();
  else h += utilFwd();
  P.innerHTML = h; scrollRestore(P, snap); restoreFocus();
  const q = $('[data-nav="q"]', P); if (q && document.activeElement === q) { q.setSelectionRange(q.value.length, q.value.length); }
}

function visibleItems(sec) {
  const u = UI.util;
  return sec.items.filter(i => (u.showInactive || !i.inactive) && matchItem(i, u.q));
}
function numLines(sec, it) {
  const c = (lbl, v) => v ? `<span>${lbl} <b data-copy="${esc(v)}" data-tip="누르면 복사">${esc(v)}</b></span>` : '';
  return `<div class="numline">${it.carrier ? `<span class="badge gray">${esc(it.carrier)}</span>` : ''}${c(sec.no1Label, it.no1)}${c(sec.no2Label, it.no2)}</div>`;
}
function itemCell(sec, it) {
  return `<div class="nolist"><b>${esc(it.name)}</b></div>${numLines(sec, it)}
    ${it.forward ? `<span class="badge warn">전달처: ${esc(it.forward)}</span>` : ''}${it.inactive ? '<span class="badge gray">미사용/사용자 납부</span>' : ''}
    ${it.memo ? `<span class="hint">${esc(it.memo)}</span>` : ''}`;
}
/* 체크박스 한 칸 */
const chk = (label, attrs, on, cls = '') => `<label class="chk ${cls}"><input type="checkbox" ${attrs} ${on ? 'checked' : ''}><i></i><span>${label}</span></label>`;
function statusCell(it, m, ctx = '') {
  const st = stateOf(it, m), at = `data-id="${it.id}" data-m="${m}"`;
  return `<div class="stcell">${chk('수령', `data-u="rcv" ${at}`, st === 1 || st === 3)}${chk('납부', `data-u="paid" ${at}`, st === 3, 'pay')}${chk('청구없음', `data-u="none" ${at}`, st === 2, 'mini')}</div>`;
}

function monthView(sec, m, dm) {
  const u = UI.util; const items = visibleItems(sec); const act = sec.items.filter(i => !i.inactive);
  const n = act.length, cnt = s => act.filter(i => stateOf(i, m) === s).length;
  const recv = cnt(1) + cnt(3), paid = cnt(3), uAmt = unpaidAmount(sec, m);
  const keep = { all: () => true, miss: i => !i.inactive && stateOf(i, m) === 0, unpaid: i => !i.inactive && stateOf(i, m) === 1, todo: i => needsAction(i, m) }[u.filter] || (() => true);
  const rows = items.filter(keep);
  const tot = monthTotal(sec, m), prev = m > 0 ? monthTotal(sec, m - 1) : null, pyT = sum(sec.items, i => i.py ? i.py[m] : 0);
  const L = lastFilled(sec);
  let h = `<div class="pills mo-pills" role="group" aria-label="월 선택">${MON.map((nm, i) => { const ac = actionCount(sec, i); const un = unpaidItems(sec, i).length; return `<button class="pill" aria-pressed="${i === m}" data-act="month" data-v="${i}">${nm}분${i <= L && ac ? `<span class="n ${un && un === ac ? 'red' : ''}" data-tip="미수령 ${missingCount(sec, i)}건 · 미납 ${un}건">${ac}</span>` : ''}</button>`; }).join('')}</div>
    <div class="sumline"><b style="font-size:16px">${MON[m]}분</b>
      <span class="badge ${recv === n ? 'good' : 'warn'}">수령 ${recv}/${n}</span><span class="badge ${paid === n ? 'good' : 'bad'}">납부 ${paid}/${n}</span>
      ${cnt(0) ? `<span class="badge warn">미수령 ${cnt(0)}건</span>` : ''}${cnt(1) ? `<span class="badge bad">미납 ${cnt(1)}건 · ${won(uAmt)}원</span>` : ''}${cnt(2) ? `<span class="badge gray">청구없음 ${cnt(2)}</span>` : ''}
      <span>입력 합계 <b>${won(tot)}원</b></span>${prev != null ? `<span class="muted">전월 ${won(prev)}원</span>` : ''}${pyT ? `<span class="muted">전년 같은 달 ${won(pyT)}원</span>` : ''}
      <span class="spacer"></span><div class="pills">${[['all', '전체'], ['todo', '처리 필요'], ['miss', '미수령'], ['unpaid', '미납']].map(([k, nm]) => `<button class="pill" aria-pressed="${u.filter === k}" data-act="filter" data-v="${k}">${nm}</button>`).join('')}</div></div>`;
  if (!rows.length) return h + `<div class="empty">${u.filter === 'all' ? '표시할 항목이 없습니다' : '해당하는 고지서가 없습니다 👍'}</div>`;
  h += `<div class="tbl-wrap fit fit-pay"><table class="t"><thead><tr><th style="width:220px">고지서 확인</th><th>관리 항목 · 번호</th><th style="width:160px">${MON[m]}분 금액(원)</th><th>전월</th><th>전년 같은 달</th></tr></thead><tbody>`;
  for (const it of rows) {
    const st = stateOf(it, m), a = it.amt[m], p = it.py ? it.py[m] : null, pv = m > 0 ? it.amt[m - 1] : null;
    const diff = a && p ? (a - p) / p * 100 : null;
    h += `<tr class="${it.inactive ? 'inactive' : ''} st${st}">
      <td>${statusCell(it, m)}</td>
      <td class="name">${itemCell(sec, it)}<button class="btn sm noprint" data-act="edititem" data-id="${it.id}">편집</button></td>
      <td><input class="cellin" data-u="amt" data-id="${it.id}" data-m="${m}" data-nav="a-${it.id}" value="${a == null ? '' : won(a)}" inputmode="numeric" aria-label="${esc(it.name)} ${MON[m]}분 금액">
          <span class="stlabel s${st}">${{ 0: '미수령', 1: '수령 · 미납', 3: '납부 완료', 2: '청구없음' }[st]}</span></td>
      <td class="num muted">${pv ? won(pv) : ''}</td>
      <td class="num muted">${p ? won(p) : ''}${diff != null && Math.abs(diff) >= 20 ? `<span class="hint ${diff > 0 ? 'diff-up' : 'diff-dn'}">${diff > 0 ? '▲' : '▼'} ${Math.abs(diff).toFixed(0)}%</span>` : ''}</td></tr>`;
  }
  return h + `<tr class="tot"><td></td><td>합계 (표시 항목)</td><td class="num">${won(sum(rows, i => i.amt[m]))}</td><td class="num">${prev != null ? won(sum(rows, i => i.amt[m - 1])) : ''}</td><td class="num">${won(sum(rows, i => i.py ? i.py[m] : 0))}</td></tr></tbody></table></div>`;
}

function yearView(sec, dm) {
  const rows = visibleItems(sec);
  const lg = (s, t) => `<span class="ycell"><span class="dot s${s}" style="display:inline-block"></span></span> ${t}`;
  let h = `<p class="muted" style="margin:0 0 8px">칸 앞의 네모를 누르면 ${lg(0, '미수령')} → ${lg(1, '수령·미납')} → ${lg(3, '납부완료')} → ${lg(2, '청구없음')} 순으로 바뀝니다.</p>
  <div class="tbl-wrap fit fit-year"><table class="t"><thead><tr><th class="sticky-l" style="min-width:230px">관리 항목</th><th>’${String(S.year - 1).slice(2)}.12</th>${MON.map((x, i) => `<th>${x}분</th>`).join('')}<th>합계</th></tr></thead><tbody>
  <tr class="tot"><td class="sticky-l" style="background:var(--accent-soft)">합계 (전체)</td><td class="num">${won(sum(sec.items, i => i.dec))}</td>${MON.map((_, i) => `<td class="num">${won(monthTotal(sec, i))}</td>`).join('')}<td class="num">${won(catTotal(sec))}</td></tr>`;
  for (const it of rows) {
    h += `<tr class="${it.inactive ? 'inactive' : ''}"><td class="sticky-l name"><div class="nolist"><b>${esc(it.name)}</b></div>${numLines(sec, it)}${it.memo ? `<span class="hint">${esc(it.memo)}</span>` : ''}<button class="btn sm noprint" data-act="edititem" data-id="${it.id}">편집</button></td>
      <td class="num muted">${it.dec ? won(it.dec) : ''}</td>
      ${MON.map((_, i) => { const st = stateOf(it, i); return `<td><div class="ycell"><button class="dot s${st}" data-act="cycle" data-id="${it.id}" data-m="${i}" data-tip="${MON[i]}분 · ${ST_TIP[st]}" aria-label="${MON[i]} 고지서 상태: ${ST_TIP[st]}"></button><input class="cellin" data-u="amt" data-id="${it.id}" data-m="${i}" data-nav="y${i}-${it.id}" value="${it.amt[i] == null ? '' : won(it.amt[i])}" inputmode="numeric"></div></td>`; }).join('')}
      <td class="num"><b>${won(itemTotal(it))}</b></td></tr>`;
  }
  return h + '</tbody></table></div>';
}

/* 이벤트 */
const PU = $('#p-util');
PU.addEventListener('change', e => {
  const t = e.target, u = t.dataset.u; if (!u) return;
  if (u === 'inactive') { UI.util.showInactive = t.checked; renderUtil(); return; }
  if (u === 'series') { UI.util.series[t.dataset.cat] = t.checked; if (!visSeries().length) { UI.util.series[t.dataset.cat] = true; toast('최소 한 개는 선택해야 합니다'); } renderUtil(); return; }
  if (u === 'q') return;
  const v = parseNum(t.value);
  if (u === 'budget') { S.utilBudget[t.dataset.cat] = v && !isNaN(v) ? v : null; save(); renderUtil(); return; }
  const sec = S.util[UI.util.cat]; const it = sec.items.find(x => x.id === t.dataset.id); if (!it) return; const m = +t.dataset.m;
  if (u === 'rcv') { setRcv(it, m, t.checked ? 1 : 0); save(); renderUtil(); return; }
  if (u === 'paid') { setPaid(it, m, t.checked); save(); renderUtil(); return; }
  if (u === 'none') { setRcv(it, m, t.checked ? 2 : 0); save(); renderUtil(); return; }
  if (Number.isNaN(v)) { toast('숫자나 계산식만 입력할 수 있습니다'); renderUtil(); return; }
  setAmt(it, m, v); save(); renderUtil();
});
PU.addEventListener('input', e => {
  if (e.target.dataset.u === 'q') { UI.util.q = e.target.value; pendingFocus = 'q'; UI._noKeep = true; renderUtil(); }
});
PU.addEventListener('keydown', e => { if (e.target.matches('input.cellin')) { const n = e.target.dataset.nav; enterNext(e, n.startsWith('y') ? n.split('-')[0] + '-' : n.slice(0, 2)); } });
PU.addEventListener('focusin', e => { if (e.target.matches('input.cellin')) { const v = parseNum(e.target.value); e.target.value = v == null || isNaN(v) ? '' : String(v); e.target.select(); } });
PU.addEventListener('click', e => {
  const cp = e.target.closest('[data-copy]'); if (cp) { copyText(cp.dataset.copy, '복사했습니다: ' + cp.dataset.copy); return; }
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act, u = UI.util;
  if (['cat', 'usub', 'month', 'filter', 'todo'].includes(a)) UI._noKeep = true;
  if (a === 'series-all') { CATS.forEach(([k]) => UI.util.series[k] = true); renderUtil(); return; }
  if (a === 'find') { openFind(); return; }
  if (a === 'payform') { pickFiles('.xlsx,.xlsm', 'form'); return; }
  if (a === 'usub') { u.sub = b.dataset.v; renderUtil(); window.scrollTo(0, 0); return; }
  if (a === 'todo') { u.cat = b.dataset.cat; u.sub = 'pay'; u.filter = 'todo'; u.q = ''; u.month = defaultMonth(S.util[u.cat]); renderUtil(); return; }
  if (a === 'cat') { u.cat = b.dataset.v; u.month = null; renderUtil(); }
  else if (a === 'month') { u.month = +b.dataset.v; renderUtil(); }
  else if (a === 'filter') { u.filter = b.dataset.v; renderUtil(); }
  else if (a === 'cycle') { const it = S.util[u.cat].items.find(x => x.id === b.dataset.id); cycle4(it, +b.dataset.m); save(); renderUtil(); }
  else if (a === 'additem') itemDialog(null);
  else if (a === 'edititem') itemDialog(b.dataset.id);
  else if (a === 'tsv') copyText(utilTSV(S.util[u.cat]), '표를 복사했습니다 — 엑셀에 붙여넣으세요');
  else if (a === 'printu') { $$('.tab-panel').forEach(p => p.classList.toggle('printing', p.id === 'p-util')); window.print(); }
});
function utilTSV(sec) {
  const rows = [['관리 항목', sec.no1Label, sec.no2Label, ...MON.map(x => x + '분'), '합계']];
  for (const it of sec.items) rows.push([it.name.replace(/\n/g, ' '), it.no1, it.no2, ...it.amt.map(v => v == null ? '' : v), itemTotal(it)]);
  rows.push(['합계', '', '', ...MON.map((_, i) => monthTotal(sec, i)), catTotal(sec)]);
  return rows.map(r => r.join('\t')).join('\n');
}

function itemDialog(id) {
  const sec = S.util[UI.util.cat]; const isNew = !id;
  const it = isNew ? { id: uid(UI.util.cat), name: '', no1: '', no2: '', carrier: '', memo: '', inactive: false, dec: null, amt: Array(12).fill(null), rcv: Array(12).fill(0), paid: Array(12).fill(false), py: null, forward: '' } : sec.items.find(x => x.id === id);
  openDlg(`<h3>${isNew ? '항목 추가' : '항목 편집'} — ${esc(sec.title)}</h3>
    <label class="fld">관리 항목 (장소·용도)<textarea id="in" rows="2">${esc(it.name)}</textarea></label>
    <label class="fld">${esc(sec.no1Label)}<input id="n1" value="${esc(it.no1)}"></label>
    <label class="fld">${esc(sec.no2Label)}<input id="n2" value="${esc(it.no2)}"></label>
    ${UI.util.cat === 'tel' ? `<label class="fld">통신사<input id="ca" value="${esc(it.carrier)}"></label>` : ''}
    <label class="fld">전달처 (고지서를 넘겨줄 계·부서)<input id="fw" value="${esc(it.forward || '')}"></label>
    <label class="fld">메모<textarea id="me" rows="2">${esc(it.memo)}</textarea></label>
    <label class="row" style="gap:6px;margin-bottom:8px"><input type="checkbox" id="ia" ${it.inactive ? 'checked' : ''}> 미사용 / 사용자 납부 (수령 확인·미수령 집계에서 제외)</label>
    <div class="row" style="justify-content:space-between;margin-top:10px"><span>${isNew ? '' : '<button class="btn danger" data-d="del">삭제</button>'}</span>
    <span class="row"><button class="btn" data-d="cancel">취소</button><button class="btn primary" data-d="save">저장</button></span></div>`, d => {
    d.onclick = ev => {
      const b = ev.target.closest('[data-d]'); if (!b) return; const k = b.dataset.d;
      if (k === 'cancel') closeDlg();
      else if (k === 'del') { if (confirm('이 항목을 삭제할까요? (월별 금액도 함께 삭제됩니다)')) { sec.items = sec.items.filter(x => x.id !== it.id); save(); closeDlg(); renderUtil(); } }
      else if (k === 'save') {
        it.name = $('#in', d).value.trim() || '(이름 없음)'; it.no1 = $('#n1', d).value.trim(); it.no2 = $('#n2', d).value.trim(); it.forward = $('#fw', d).value.trim(); it.memo = $('#me', d).value.trim();
        if ($('#ca', d)) it.carrier = $('#ca', d).value.trim();
        const was = it.inactive; it.inactive = $('#ia', d).checked;
        if (it.inactive && !was) it.rcv = it.rcv.map(x => x === 0 ? 2 : x);
        if (!it.paid) it.paid = Array(12).fill(false);
        if (!it.inactive && was) it.rcv = it.rcv.map((x, i) => x === 2 && !it.amt[i] ? 0 : x);
        if (isNew) sec.items.push(it); save(); closeDlg(); renderUtil();
      }
    };
  });
}
