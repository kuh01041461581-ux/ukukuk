/* ===== 지출 폼 엑셀 → 기록에서 찾아 '납부 완료' 처리 ===== */
const PF = { rows: [], unmatched: [], file: '' };
const digitsOf = s => String(s || '').replace(/\D/g, '');
const ID_HDR = /번호|계약|고객|수전|계좌|납부|전자|관리/;
const AMT_HDR = [/공급가액/, /금액/, /청구/, /납부액|납부금/, /지출/, /결의|지급/, /합계|계/, /요금/];
const NOT_AMT_HDR = /사업자|생년|우편|전화|휴대폰|부가세|공제|번호|코드|계좌/;

function pfItems() {
  const out = [];
  for (const [k, nm] of CATS) { const sec = S.util[k]; if (!sec) continue; for (const it of sec.items) {
    const nums = [it.no1, it.no2].map(digitsOf).filter(d => d.length >= 7);
    if (nums.length) out.push({ k, nm, sec, it, nums });
  } }
  return out;
}
function pfAmount(r, usedCols) {
  const cands = [];
  r.nums.forEach(([c, h, v]) => { const hh = h || (r.header[c] || ''); if (!usedCols.has(c) && v > 0 && !NOT_AMT_HDR.test(hh)) cands.push({ c, h: hh, v: Math.round(v) }); });
  r.cells.forEach((t, c) => { if (/^\d{1,3}(,\d{3})+(원)?$/.test(t) && !usedCols.has(c)) cands.push({ c, h: r.header[c] || '', v: parseInt(t.replace(/\D/g, ''), 10) }); });
  if (!cands.length) return null;
  for (const re of AMT_HDR) { const f = cands.find(x => re.test(x.h)); if (f) return f.v; }
  const big = cands.filter(x => x.v >= 100).sort((a, b) => b.v - a.v);
  return (big[0] || cands[0]).v;
}
/* 입금명세 문구로 월 알아내기
   전기: (2026년 09월 01일 ~ 2026년 09월 30일) → 사용기간 끝 + 5일이 속한 달의 청구분
   상하수도: (09/15~10/14) → 부과기간 끝나는 달의 다음 달 청구분 */
function pfMonthByText(r, k) {
  for (const t of r.cells) {
    if (!t) continue;
    let m = t.match(/(\d{4})\s*년\s*(\d{1,2})\s*월\s*(\d{1,2})\s*일\s*~\s*(\d{4})\s*년\s*(\d{1,2})\s*월\s*(\d{1,2})\s*일/);
    if (m) {
      const e = new Date(+m[4], +m[5] - 1, +m[6]);
      if (k === 'water') { e.setMonth(e.getMonth() + 1, 1); } else e.setDate(e.getDate() + 5);
      if (e.getFullYear() === S.year) return e.getMonth();
      continue;
    }
    m = t.match(/(\d{1,2})\s*\/\s*(\d{1,2})\s*~\s*(\d{1,2})\s*\/\s*(\d{1,2})/);
    if (m) {
      const em = +m[3], ed = +m[4]; if (em < 1 || em > 12) continue;
      const e = new Date(S.year, em - 1, ed);
      if (k === 'water') { e.setMonth(e.getMonth() + 1, 1); } else e.setDate(e.getDate() + 5);
      if (e.getFullYear() === S.year) return e.getMonth();
    }
  }
  return null;
}
function pfMonth(r, usedCols) {
  const order = r.cells.map((t, c) => c).sort((a, b) => (/귀속|사용|청구|대상|월/.test(r.header[b] || '') ? 1 : 0) - (/귀속|사용|청구|대상|월/.test(r.header[a] || '') ? 1 : 0));
  for (const c of order) {
    if (usedCols.has(c)) continue; const t = r.cells[c]; if (!t || /^\d{4}-\d{2}-\d{2}$/.test(t)) continue;
    let m = t.match(/(\d{1,2})\s*월\s*분/); if (m && +m[1] >= 1 && +m[1] <= 12) return +m[1] - 1;
    m = t.match(/^(20\d\d)[-./]?(0?[1-9]|1[0-2])$/); if (m && +m[1] === S.year) return +m[2] - 1;
  }
  return null;
}
function pfGuessMonth(it) {
  for (let m = 0; m < 12; m++) if (stateOf(it, m) === 1) return m;   // 수령했지만 미납인 가장 빠른 달
  for (let m = 0; m < 12; m++) if (stateOf(it, m) === 0 && m <= lastFilledOf(it)) return m;
  return Math.max(0, lastFilledOf(it));
}
function lastFilledOf(it) { let L = -1; for (let m = 0; m < 12; m++) if (it.rcv[m] === 1 || it.amt[m] > 0) L = m; return L; }

function pfMatch(formRows) {
  const items = pfItems(); const matched = [], unmatched = [];
  for (const r of formRows) {
    let best = null;
    const idCells = r.cells.map((t, c) => ({ c, d: digitsOf(t), typedNum: r.nums.some(n => n[0] === c) })).filter(x => x.d.length >= 7 && (!x.typedNum || ID_HDR.test(r.header[x.c] || '')));
    for (const cand of items) for (const nd of cand.nums) for (const cell of idCells) {
      if (cell.d.includes(nd)) { const score = nd.length; if (!best || score > best.score || (score === best.score && best.cand !== cand)) best = { cand, nd, col: cell.c, score, tie: best && score === best.score && best.cand !== cand }; }
    }
    if (!best) { unmatched.push(r); continue; }
    const used = new Set([best.col]); const it = best.cand.it;
    const amount = pfAmount(r, used), mm = pfMonth(r, used);
    let month = mm, src = mm != null ? 'form' : null;
    if (month == null) { const t = pfMonthByText(r, best.cand.k); if (t != null) { month = t; src = 'text'; } }
    if (month == null && amount != null) {   // 금액이 같고 아직 처리 안 된 달
      for (let m = 0; m < 12; m++) if (it.amt[m] === amount && stateOf(it, m) !== 3 && stateOf(it, m) !== 2) { month = m; src = 'amt'; break; }
    }
    if (month == null) { month = pfGuessMonth(it); src = 'guess'; }
    matched.push({ r, cand: best.cand, it, k: best.cand.k, nm: best.cand.nm, num: it.no1 && digitsOf(it.no1).includes(best.nd) ? it.no1 : it.no2, amount, month, src, memo: (r.cells.find(t => /요금\(|~/.test(t)) || '').slice(0, 60), guessed: src === 'guess', tie: !!best.tie, on: true });
  }
  // 같은 항목·같은 달이 중복되면 첫 줄만 기본 선택
  const seen = new Set(); matched.forEach(x => { const key = x.it.id + '|' + x.month; if (seen.has(key)) x.on = false; seen.add(key); });
  return { matched, unmatched };
}

function openPayForm(rows, file) {
  const { matched, unmatched } = pfMatch(rows); PF.rows = matched; PF.unmatched = unmatched; PF.file = file;
  drawPayForm();
}
function pfRowHTML(x, i) {
  const it = x.it, st = stateOf(it, x.month), cur = it.amt[x.month];
  const done = st === 3 && (x.amount == null || cur === x.amount);
  const warn = [];
  if (done) warn.push('<span class="badge good">이미 납부 처리됨</span>');
  if (x.amount != null && cur > 0 && cur !== x.amount && !done) warn.push(`<span class="badge warn">금액 다름 (기존 ${won(cur)})</span>`);
  if (x.amount == null) warn.push('<span class="badge warn">금액을 못 찾음</span>');
  if (!done && x.amount != null && cur === x.amount) warn.push('<span class="badge good" data-tip="기록에 이미 같은 금액이 있습니다">금액 일치</span>');
  if (x.guessed) warn.push('<span class="badge gray" data-tip="폼에서 월을 찾지 못해 처리가 필요한 가장 빠른 달로 정했습니다. 맞는지 확인하세요">월 추정</span>');
  else if (x.src === 'amt') warn.push('<span class="badge gray" data-tip="폼에 월 정보가 없어, 같은 금액이 기록된 미처리 달로 정했습니다">금액으로 월 확인</span>');
  if (x.tie) warn.push('<span class="badge warn">여러 항목과 일치</span>');
  return `<tr class="${done ? 'st3' : ''}"><td><label class="chk"><input type="checkbox" data-pf="on" data-i="${i}" ${x.on ? 'checked' : ''}><i></i></label></td>
    <td><span class="badge" style="background:color-mix(in srgb,${CATCOLOR[x.k]} 18%,transparent);color:var(--text)">${x.nm}</span> <b>${esc(it.name.replace(/\n/g, ' '))}</b><div class="numline"><span>${esc(x.num)}</span><em>· 폼 ${esc(((x.r.file || '').match(/(\d{8})\.xls/) || [])[1] || '')} ${x.r.r}행${x.memo ? ' · ' + esc(x.memo) : ''}</em></div></td>
    <td><select data-pf="month" data-i="${i}" class="fsel">${MON.map((n, m) => `<option value="${m}" ${m === x.month ? 'selected' : ''}>${n}분</option>`).join('')}</select></td>
    <td><input class="cellin" style="width:120px;border-color:var(--edge-dark)" data-pf="amt" data-i="${i}" value="${x.amount == null ? '' : won(x.amount)}" inputmode="numeric"></td>
    <td class="muted">${{ 0: '미수령', 1: '수령 · 미납', 3: '납부 완료', 2: '청구없음' }[st]}${cur ? ' · ' + won(cur) + '원' : ''}</td><td>${warn.join(' ')}</td></tr>`;
}
function drawPayForm() {
  const n = PF.rows.filter(x => x.on).length;
  openDlg(`<div class="pfwrap"><div class="row" style="justify-content:space-between"><h3 style="margin:0">지출 폼에서 납부 처리 <span class="muted" style="font-weight:400;font-size:12px">${esc(PF.file)}</span></h3><button class="btn sm" data-pfa="close" aria-label="닫기">✕</button></div>
    <p class="sub" style="margin:6px 0 10px">폼의 계약번호·납부번호를 기록에서 찾아, 체크한 줄을 <b>수령 + 납부 완료</b>로 바꾸고 금액을 채웁니다. 적용 전에 월과 금액을 확인하세요.</p>
    ${PF.rows.length ? `<div class="tbl-wrap" style="max-height:52vh;overflow:auto"><table class="t"><thead><tr><th>적용</th><th>찾은 항목 · 번호</th><th>월</th><th>금액(원)</th><th>현재 상태</th><th>확인</th></tr></thead><tbody>${PF.rows.map(pfRowHTML).join('')}</tbody></table></div>`
      : '<div class="empty">폼의 번호와 일치하는 고지서 기록을 찾지 못했습니다.<br>폼에 계약번호(또는 납부번호)가 들어 있는지 확인해 주세요.</div>'}
    ${PF.unmatched.length ? `<details class="fold" style="margin-top:8px"><summary>찾지 못한 행 ${PF.unmatched.length}건</summary><div class="tbl-wrap" style="max-height:22vh;overflow:auto"><table class="t"><tbody>${PF.unmatched.slice(0, 80).map(r => `<tr><td class="muted">${esc(r.sheet)} ${r.r}행</td><td>${esc(r.cells.filter(Boolean).join(' · '))}</td></tr>`).join('')}</tbody></table></div></details>` : ''}
    <div class="row" style="justify-content:space-between;margin-top:12px"><span class="muted">선택 ${n}건 / 찾음 ${PF.rows.length}건</span><span class="row"><button class="btn" data-pfa="close">닫기</button><button class="btn primary" data-pfa="apply" ${n ? '' : 'disabled'}>선택한 ${n}건 납부 처리</button></span></div></div>`,
    d => { d.classList.add('pfbox'); d.onclose = () => d.classList.remove('pfbox'); });
}
$('#dlg').addEventListener('change', e => {
  const t = e.target, f = t.dataset.pf; if (!f || !$('.pfwrap')) return; const x = PF.rows[+t.dataset.i]; if (!x) return;
  if (f === 'on') x.on = t.checked;
  else if (f === 'month') { x.month = +t.value; x.guessed = false; }
  else if (f === 'amt') { const v = parseNum(t.value); x.amount = v && !isNaN(v) ? v : null; }
  drawPayForm();
});
$('#dlg').addEventListener('click', e => {
  const b = e.target.closest('[data-pfa]'); if (!b) return;
  if (b.dataset.pfa === 'close') { closeDlg(); return; }
  if (b.dataset.pfa === 'apply') {
    let n = 0; PF.rows.filter(x => x.on).forEach(x => { if (x.amount) setAmt(x.it, x.month, x.amount); setRcv(x.it, x.month, 1); setPaid(x.it, x.month, true); n++; });
    save(); closeDlg(); if (UI.tab === 'util') renderUtil(); else if (UI.tab === 'daily') renderDaily(); toast(`${n}건을 납부 완료로 처리했습니다`);
  }
});
