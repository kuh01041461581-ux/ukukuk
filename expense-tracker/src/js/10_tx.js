/* ===== 지급명령(실집행) 건별 내역: 월별 소비량 · 어떤 항목에 썼는지 ===== */
const TX_NAME_CODE = { '사무관리비': '20101', '공공운영비': '20102', '행사운영비': '20103', '국내여비': '20201', '국외업무여비': '20203', '시책추진업무추진비': '20303', '부서운영업무추진비': '20304', '기관운영업무추진비': '20301', '연구용역비': '20701', '재료비': '20601', '시설비': '40101', '시설부대비': '40103', '자산및물품취득비': '40501' };
const TX_RE = {
  date: /지급명령일|지급일|결의일|집행일|지출일|전표일|일자|날짜/,
  biz: /세부사업/,
  code: /통계목|편성목|세목/,
  amt: /집행액|지급명령액|지급액|지출액|결의액|금액/,
  memo: /적요|지출건명|건명|내용|제목|사유|용도/,
  vendor: /거래처|채주|수령인|지급처|업체|상호|계약상대|상대방/,
};
const TX_LABEL = { date: '날짜', biz: '세부사업', code: '통계목(세목)', amt: '금액', memo: '적요(내용)', vendor: '거래처' };
const hnorm = h => String(h || '').replace(/\s/g, '');

function txAutoMap(headers) {
  const m = {}; const hs = headers.map(hnorm);
  for (const key of ['date', 'biz', 'code', 'amt', 'memo', 'vendor']) {
    let idx = -1;
    if (key === 'amt') { for (const re of [/집행액/, /지급명령액/, /지급액/, /지출액/, /결의액/, /금액/]) { idx = hs.findIndex(h => re.test(h) && !/예산|배정|누계|잔액/.test(h)); if (idx >= 0) break; } }
    else idx = hs.findIndex(h => TX_RE[key].test(h));
    m[key] = idx;
  }
  return m;
}
function normDate(v) {
  v = String(v == null ? '' : v).trim(); if (!v) return '';
  let m = v.match(/^(\d{4})[-./](\d{1,2})[-./](\d{1,2})/); if (m) return `${m[1]}-${m[2].padStart(2, '0')}-${m[3].padStart(2, '0')}`;
  m = v.match(/^(\d{4})(\d{2})(\d{2})$/); if (m) return `${m[1]}-${m[2]}-${m[3]}`;
  m = v.match(/^(\d{2})[-./](\d{1,2})[-./](\d{1,2})$/); if (m) return `20${m[1]}-${m[2].padStart(2, '0')}-${m[3].padStart(2, '0')}`;
  m = v.match(/(\d{4})\s*년\s*(\d{1,2})\s*월\s*(\d{1,2})\s*일/); if (m) return `${m[1]}-${m[2].padStart(2, '0')}-${m[3].padStart(2, '0')}`;
  return '';
}
function parseAmt(r, col) {
  const typed = r.nums.find(n => n[0] === col); if (typed) return Math.round(typed[2]);
  const t = String(r.cells[col] || '').replace(/[,원\s]/g, ''); return /^-?\d+$/.test(t) ? parseInt(t, 10) : null;
}
function txParse(rows, map) {
  const out = [];
  for (const r of rows) {
    const d = normDate(r.cells[map.date]); const amt = map.amt >= 0 ? parseAmt(r, map.amt) : null;
    if (!d || amt == null || amt === 0) continue;
    const codeCell = map.code >= 0 ? String(r.cells[map.code] || '') : '';
    let code = (codeCell.match(/\d{5}/) || [])[0] || ''; let name = codeCell.replace(/\d{5}/, '').trim();
    if (!code) { const k = Object.keys(TX_NAME_CODE).find(n => hnorm(codeCell).includes(n)); if (k) code = TX_NAME_CODE[k]; }
    out.push({ d, biz: map.biz >= 0 ? String(r.cells[map.biz] || '').trim() : '', code, name, amt,
      memo: map.memo >= 0 ? String(r.cells[map.memo] || '').trim() : '', vendor: map.vendor >= 0 ? String(r.cells[map.vendor] || '').trim() : '' });
  }
  return out;
}
const txKey = t => [t.d, t.biz, t.code, t.amt, t.memo, t.vendor].join('|');
function txMerge(list) {   // 같은 건은 한 번만(여러 번 불러와도 중복 없음), 새 건만 추가
  const have = new Map(); (S.tx || []).forEach(t => have.set(txKey(t), (have.get(txKey(t)) || 0) + 1));
  const inc = new Map(); let added = 0; S.tx = S.tx || [];
  for (const t of list) { const k = txKey(t); inc.set(k, (inc.get(k) || 0) + 1); if ((have.get(k) || 0) < inc.get(k)) { S.tx.push({ id: uid('T'), ...t }); added++; } }
  S.tx.sort((a, b) => a.d < b.d ? -1 : a.d > b.d ? 1 : 0); return added;
}
function txLineOf(t) {
  if (t._l !== undefined) return t._l;
  const nb = normName(t.biz);
  let l = S.lines.find(x => x.code === t.code && nb && (normName(x.biz) === nb || normName(x.biz).includes(nb) || nb.includes(normName(x.biz))));
  if (!l && !nb) l = S.lines.filter(x => x.code === t.code).length === 1 ? S.lines.find(x => x.code === t.code) : null;
  Object.defineProperty(t, '_l', { value: l || null, enumerable: false, writable: true }); return t._l;
}

/* ---- 불러오기 창 ---- */
const TXI = { rows: [], file: '', map: {}, headers: [] };
function openTxImport(rows, file) {
  // 시트 하나만 사용: 날짜·금액 열이 있는 시트 우선
  const bySheet = {}; rows.forEach(r => (bySheet[r.sheet] = bySheet[r.sheet] || []).push(r));
  const best = Object.values(bySheet).sort((a, b) => b.length - a.length)[0] || [];
  TXI.rows = best; TXI.file = file; TXI.headers = (best[0] && best[0].header && best[0].header.length) ? best[0].header : (best[0] ? best[0].cells.map((_, i) => '열' + (i + 1)) : []);
  TXI.map = txAutoMap(TXI.headers); drawTxImport();
}
function drawTxImport() {
  const parsed = txParse(TXI.rows, TXI.map); const sumAmt = sum(parsed, t => t.amt); const ds = parsed.map(t => t.d).sort();
  const linked = parsed.filter(t => txLineOf(t)).length;
  const sel = key => `<select class="fsel" data-txm="${key}"><option value="-1">(없음)</option>${TXI.headers.map((h, i) => `<option value="${i}" ${TXI.map[key] === i ? 'selected' : ''}>${esc(hnorm(h) || ('열' + (i + 1)))}</option>`).join('')}</select>`;
  const need = TXI.map.date < 0 || TXI.map.amt < 0;
  openDlg(`<div class="txiwrap"><div class="row" style="justify-content:space-between"><h3 style="margin:0">지급명령 내역 불러오기 <span class="muted" style="font-weight:400;font-size:12px">${esc(TXI.file)}</span></h3><button class="btn sm" data-txa="close">✕</button></div>
    <p class="sub" style="margin:6px 0 10px">열 이름을 보고 자동으로 맞췄습니다. 틀리면 아래에서 바꾸세요. 같은 내역을 여러 번 불러와도 중복되지 않습니다.</p>
    <div class="txmap">${['date', 'amt', 'memo', 'vendor', 'biz', 'code'].map(k => `<label class="fld" style="margin:0">${TX_LABEL[k]}${sel(k)}</label>`).join('')}</div>
    ${need ? '<p class="note-box" style="margin-top:10px">날짜와 금액 열을 찾지 못했습니다. 위에서 직접 골라 주세요.</p>' : ''}
    <div class="kpis" style="margin:12px 0 8px"><div class="kpi"><div class="k">읽은 건수</div><div class="v">${parsed.length.toLocaleString('ko-KR')}건</div></div><div class="kpi"><div class="k">금액 합계</div><div class="v">${won(sumAmt)}원</div></div><div class="kpi"><div class="k">기간</div><div class="v" style="font-size:15px">${ds[0] || '-'} ~ ${ds[ds.length - 1] || '-'}</div></div><div class="kpi"><div class="k">내 항목과 연결</div><div class="v">${linked}건</div></div></div>
    <div class="tbl-wrap" style="max-height:26vh;overflow:auto"><table class="t"><thead><tr><th>날짜</th><th>세부사업</th><th>통계목</th><th>적요</th><th>거래처</th><th>금액</th></tr></thead><tbody>${parsed.slice(0, 8).map(t => `<tr><td>${t.d}</td><td>${esc(t.biz)}</td><td>${esc(t.code)} ${esc(t.name)}</td><td>${esc(t.memo)}</td><td>${esc(t.vendor)}</td><td class="num">${won(t.amt)}</td></tr>`).join('') || '<tr><td colspan="6" class="empty">읽을 수 있는 줄이 없습니다</td></tr>'}</tbody></table></div>
    <div class="row" style="justify-content:flex-end;margin-top:12px"><button class="btn" data-txa="close">닫기</button><button class="btn primary" data-txa="apply" ${parsed.length ? '' : 'disabled'}>${parsed.length.toLocaleString('ko-KR')}건 불러오기</button></div></div>`,
    d => { d.classList.add('pfbox'); d.onclose = () => d.classList.remove('pfbox'); });
}
$('#dlg').addEventListener('change', e => { const k = e.target.dataset.txm; if (!k || !$('.txiwrap')) return; TXI.map[k] = +e.target.value; drawTxImport(); });
$('#dlg').addEventListener('click', e => {
  const b = e.target.closest('[data-txa]'); if (!b) return;
  if (b.dataset.txa === 'close') { closeDlg(); return; }
  if (b.dataset.txa === 'apply') { const added = txMerge(txParse(TXI.rows, TXI.map)); save(); closeDlg(); UI.dsub = 'tx'; UI.dsub && showTab('daily'); toast(`${added.toLocaleString('ko-KR')}건을 새로 불러왔습니다 (전체 ${(S.tx || []).length.toLocaleString('ko-KR')}건)`); }
});

/* ---- 화면 ---- */
function txScope() {
  const u = UI.tx; const q = String(u.q || '').split(/\s+/).map(normQ).filter(Boolean);
  return (S.tx || []).filter(t => {
    const l = txLineOf(t);
    if (u.line !== 'all') { if (!l || l.id !== u.line) return false; }
    else if (u.group !== 'all') { if (!l || l.group !== u.group) return false; }
    if (q.length) { const hay = normQ([t.memo, t.vendor, t.biz, t.name, t.code, l ? l.label : '', String(t.amt)].join(' ')); if (!q.every(x => hay.includes(x))) return false; }
    return true;
  });
}
function txMonthChart(list, sel) {
  const tot = Array(12).fill(0), cnt = Array(12).fill(0); list.forEach(t => { const m = +t.d.slice(5, 7) - 1; tot[m] += t.amt; cnt[m]++; });
  const maxV = Math.max(1, ...tot); const mag = Math.pow(10, Math.floor(Math.log10(maxV))); let nice = mag; for (const f of [1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 10]) { if (maxV <= f * mag) { nice = f * mag; break; } }
  const W = 760, H = 230, L = 50, R = 8, Tp = 22, B = 26, iw = W - L - R, ih = H - Tp - B, slot = iw / 12, bw = slot * 0.62;
  let g = ''; for (let i = 0; i <= 4; i++) { const y = Tp + ih - ih * i / 4; g += `<line class="gd" x1="${L}" x2="${W - R}" y1="${y}" y2="${y}"/><text x="${L - 6}" y="${y + 4}" text-anchor="end">${manKey(nice * i / 4)}</text>`; }
  tot.forEach((v, m) => {
    const x = L + slot * m + (slot - bw) / 2, h = v / nice * ih, y = Tp + ih - h, r = Math.min(5, bw / 2), dim = sel != null && sel !== m;
    if (v) g += `<path class="barflat txbar" data-act="txmonth" data-m="${m}" style="opacity:${dim ? .35 : 1};cursor:pointer" d="M${x},${Tp + ih}V${y + r}Q${x},${y} ${x + r},${y}H${x + bw - r}Q${x + bw},${y} ${x + bw},${y + r}V${Tp + ih}Z" fill="var(--accent)" data-tip="${MON[m]}\n${won(v)}원 (${cnt[m]}건)\n누르면 이 달만 보기"/>`;
    g += `<text x="${x + bw / 2}" y="${H - 8}" text-anchor="middle" ${sel === m ? 'style="fill:var(--text);font-weight:700"' : ''}>${m + 1}월</text>`;
    if (v && bw > 20) g += `<text x="${x + bw / 2}" y="${y - 5}" text-anchor="middle" style="fill:var(--text)">${manKey(v)}</text>`;
  });
  return `<svg viewBox="0 0 ${W} ${H}" width="100%" role="img" aria-label="월별 지급명령 금액">${g}<line class="ax" x1="${L}" x2="${W - R}" y1="${Tp + ih}" y2="${Tp + ih}"/></svg>`;
}
function dailyTxTab() {
  UI.tx = UI.tx || { group: 'all', line: 'all', month: null, q: '' }; const u = UI.tx;
  if (!(S.tx || []).length) return `<div class="card" style="margin-top:12px;text-align:center;padding:34px 20px"><h2 style="font-size:17px">지출 내역(지급명령)을 불러오면 월별 소비량과 사용처가 보입니다</h2>
    <p class="sub">e-호조에서 <b>지급명령(실집행) 내역</b>을 엑셀로 내려받아 올리세요. 날짜 · 세부사업 · 통계목 · 적요(내용) · 거래처 · 금액 열을 자동으로 찾습니다.</p>
    <div class="row" style="justify-content:center;margin-top:12px"><button class="btn primary" data-act="txpick">지급명령 내역 엑셀 불러오기</button></div></div>`;
  const base = txScope(); const list = u.month == null ? base : base.filter(t => +t.d.slice(5, 7) - 1 === u.month);
  const total = sum(list, t => t.amt), baseTotal = sum(base, t => t.amt);
  const months = Array(12).fill(0); base.forEach(t => { months[+t.d.slice(5, 7) - 1] += t.amt; });
  const used = months.filter(v => v > 0).length, peak = months.indexOf(Math.max(...months));
  const groups = [...new Set(S.lines.map(l => l.group))];
  const lines = S.lines.filter(l => u.group === 'all' || l.group === u.group);
  // 사용처 순위: 거래처가 있으면 거래처, 없으면 적요 앞부분
  const agg = new Map(); list.forEach(t => { const k = (t.vendor || t.memo || '(내용 없음)').slice(0, 22); agg.set(k, (agg.get(k) || 0) + t.amt); });
  const top = [...agg.entries()].sort((a, b) => b[1] - a[1]).slice(0, 8), topMax = top.length ? top[0][1] : 1;
  const unlinked = (S.tx || []).filter(t => !txLineOf(t)).length;
  return `<div class="row noprint txbar-top">
      <label class="muted">구분 <select class="fsel" data-txf="group"><option value="all">전체</option>${groups.map(g => `<option ${u.group === g ? 'selected' : ''}>${g}</option>`).join('')}</select></label>
      <label class="muted">사업 <select class="fsel" data-txf="line" style="max-width:240px"><option value="all">전체</option>${lines.map(l => `<option value="${l.id}" ${u.line === l.id ? 'selected' : ''}>${esc(l.label)}${l.group === '공공운영비' || l.group === '사무관리비' ? ' (' + l.group + ')' : ''}</option>`).join('')}</select></label>
      <input type="search" data-txf="q" data-nav="txq" value="${esc(u.q)}" placeholder="적요·거래처 검색" style="border:1px solid var(--line);border-radius:8px;padding:6px 10px;background:var(--surface-2);width:200px">
      ${u.month != null ? `<span class="badge warn">${MON[u.month]}만 보는 중 <button class="btn sm" data-act="txmonth" data-m="${u.month}" style="margin-left:4px">해제</button></span>` : ''}
      <span class="spacer"></span><span class="muted">전체 ${(S.tx || []).length.toLocaleString('ko-KR')}건${unlinked ? ` · 내 항목과 안 이어진 ${unlinked}건` : ''}</span>
      <button class="btn sm" data-act="txpick">내역 불러오기</button><button class="btn sm danger" data-act="txclear">내역 지우기</button></div>
    <div class="hero" style="grid-template-columns:repeat(4,1fr);margin-top:10px">
      <div class="kpi"><div class="k">${u.month != null ? MON[u.month] : '기간'} 지급명령 합계</div><div class="v">${won(total)}원</div><div class="s">${list.length.toLocaleString('ko-KR')}건</div></div>
      <div class="kpi"><div class="k">월평균 (지출 있는 달 기준)</div><div class="v">${won(used ? baseTotal / used : 0)}원</div><div class="s">${used}개월</div></div>
      <div class="kpi"><div class="k">가장 많이 쓴 달</div><div class="v">${used ? MON[peak] : '-'}</div><div class="s">${won(Math.max(...months))}원</div></div>
      <div class="kpi"><div class="k">최근 지출일</div><div class="v" style="font-size:20px">${base.length ? base[base.length - 1].d : '-'}</div><div class="s">${base.length ? esc((base[base.length - 1].memo || base[base.length - 1].vendor || '').slice(0, 20)) : ''}</div></div></div>
    <div class="txgrid">
      <div class="card" style="margin:0"><h2>월별 소비량 <span class="muted" style="font-weight:400;font-size:12px">막대를 누르면 그 달만 봅니다</span></h2>${txMonthChart(base, u.month)}</div>
      <div class="card" style="margin:0"><h2>어디에 썼나 <span class="muted" style="font-weight:400;font-size:12px">${(S.tx || []).some(t => t.vendor) ? '거래처' : '적요'} 기준 상위 8</span></h2>
        ${top.map(([k, v]) => `<div class="usage"><div class="un" title="${esc(k)}">${esc(k)}</div><div class="bar"><span class="seg paid" style="width:${(v / topMax * 100).toFixed(1)}%"></span></div><div class="uv"><b>${won(v)}</b><span class="muted"> ${pct(v, total).toFixed(0)}%</span></div></div>`).join('') || '<div class="empty">내역이 없습니다</div>'}</div></div>
    <div class="card" style="margin-top:12px"><h2>지급명령 내역 <span class="muted" style="font-weight:400;font-size:12px">${list.length.toLocaleString('ko-KR')}건${list.length > (u.limit || 150) ? ' · 최근 ' + (u.limit || 150) + '건만 표시' : ''}</span></h2>
      <div class="tbl-wrap fit fit-tx"><table class="t"><thead><tr><th>날짜</th><th>사업</th><th>통계목</th><th>적요</th><th>거래처</th><th>금액</th></tr></thead><tbody>${list.slice(-(u.limit || 150)).reverse().map(t => { const l = txLineOf(t); return `<tr><td>${t.d}</td><td>${l ? esc(l.label) : `<span class="muted">${esc(t.biz || '-')}</span>`}</td><td class="muted">${esc(t.name || (l ? l.group : t.code))}</td><td>${esc(t.memo)}</td><td>${esc(t.vendor)}</td><td class="num"><b>${won(t.amt)}</b></td></tr>`; }).join('') || '<tr><td colspan="6" class="empty">조건에 맞는 내역이 없습니다</td></tr>'}</tbody></table></div>${list.length > (u.limit || 150) ? `<div style="text-align:center;margin-top:8px"><button class="btn sm" data-act="txmore">더 보기 (+300건)</button></div>` : ''}</div>`;
}
$('#p-daily').addEventListener('change', e => {
  const k = e.target.dataset.txf; if (!k) return; UI.tx = UI.tx || { group: 'all', line: 'all', month: null, q: '' };
  if (k === 'q') return; UI.tx[k] = e.target.value; if (k === 'group') UI.tx.line = 'all'; UI._noKeep = true; renderDaily();
});
$('#p-daily').addEventListener('input', e => { if (e.target.dataset.txf === 'q') { UI.tx.q = e.target.value; pendingFocus = 'txq'; UI._noKeep = true; renderDaily(); } });
$('#p-daily').addEventListener('click', e => {
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act;
  if (a === 'txmonth') { UI.tx.month = UI.tx.month === +b.dataset.m ? null : +b.dataset.m; UI._noKeep = true; renderDaily(); }
  else if (a === 'txmore') { UI.tx.limit = (UI.tx.limit || 150) + 300; renderDaily(); }
  else if (a === 'txpick') pickFiles('.xlsx,.xlsm', 'tx');
  else if (a === 'txclear') { if (confirm(`불러온 지급명령 내역 ${(S.tx || []).length.toLocaleString('ko-KR')}건을 모두 지웁니다. 계속할까요?`)) { S.tx = []; save(); renderDaily(); } }
});
