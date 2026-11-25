/* ===== 보고서 출력 탭: 제출용 문서(A4) — 종이처럼 보이는 화면 그대로 인쇄·PDF 저장 ===== */
const docOpt = () => Object.assign({ main: true, daily: true, util: true, ori: 'landscape' }, (S.opt && S.opt.doc) || {});

function docDailyHTML(n) {
  const L = visLines(); if (!L.length) return '';
  const T = groupTotals(L), row = (nm, g) => `<tr${nm === '계' ? ' class="of-tot"' : ''}><td>${esc(nm)}</td><td class="num">${fuP(g.budget)}</td><td class="num">${fuP(g.spent)}</td><td class="num">${fuP(g.sch)}</td><td class="num">${fuP(g.remain)}</td><td class="num">${g.rate.toFixed(1)}</td></tr>`;
  return `<div class="of-sec">${n}. 일상경비 집행 현황 <span class="of-unit">(단위: ${ul()}, %)</span></div>
    <table class="t of-tbl"><thead><tr><th>구분</th><th>예산</th><th>실집행액</th><th>지급예정</th><th>집행잔액</th><th>집행률(%)</th></tr></thead><tbody>${row('계', T)}${GROUPS.map(g => { const Ls = L.filter(l => l.group === g); return Ls.length ? row(g, groupTotals(Ls)) : ''; }).join('')}</tbody></table>`;
}
function docUtilHTML(n) {
  const ts = todoSummary(); if (!ts.length) return '';
  return `<div class="of-sec">${n}. 공공요금 현황 <span class="of-unit">(단위: ${ul()})</span></div>
    <table class="t of-tbl"><thead><tr><th>구분</th><th>관리 곳 수</th><th>올해 지출 합계</th><th>미납(수령 후 미처리)</th><th>미납 금액</th><th>미수령</th></tr></thead><tbody>${ts.map(t => { const sec = S.util[t.k]; return `<tr><td>${t.nm}</td><td class="num">${sec.items.filter(i => !i.inactive).length}</td><td class="num">${fuP(catTotal(sec))}</td><td class="num">${t.unpaid}건</td><td class="num">${fuP(t.amt)}</td><td class="num">${t.missing}건</td></tr>`; }).join('')}</tbody></table>`;
}
function renderDoc() {
  const P = $('#p-doc'); const D = repData();
  if (!D) { P.innerHTML = `<div class="card"><h2>보고서 출력</h2><div class="empty">통계목별 조회 엑셀을 불러오면 제출용 보고서(A4)가 만들어집니다.<br><br><button class="btn primary" data-act="docpick">통계목별 조회 엑셀 선택</button></div></div>`; return; }
  const o = docOpt(); let n = 3 + (o.main ? 0 : -1); const secs = [];
  if (o.daily && S.lines.length) secs.push(docDailyHTML(++n + (o.main ? 0 : 0)));
  if (o.util && todoSummary().length) secs.push(docUtilHTML(++n));
  const extra = secs.join('');
  const chk = (id, lbl, on) => `<label class="row" style="gap:5px"><input type="checkbox" id="${id}" ${on ? 'checked' : ''}> ${lbl}</label>`;
  P.innerHTML = `<div class="doc-ctl noprint"><div class="row" style="gap:14px;flex-wrap:wrap;align-items:center">
      <b>제출용 보고서</b><span class="muted" style="font-size:12px">아래 종이 모양 그대로 인쇄됩니다</span><span style="flex:1"></span>
      <div class="pills" role="group" aria-label="단위"><span class="muted" style="align-self:center;font-size:12px;margin-right:4px">단위</span>${UNITS.map(u => `<button class="pill sm" aria-pressed="${ul() === u[0]}" data-act="dunit" data-v="${u[0]}">${u[0]}</button>`).join('')}</div>
      <label class="row" style="gap:5px;font-size:13px">용지 <select id="dc-ori" class="fsel"><option value="landscape" ${o.ori === 'landscape' ? 'selected' : ''}>가로</option><option value="portrait" ${o.ori === 'portrait' ? 'selected' : ''}>세로</option></select></label>
      <button class="btn sm" data-act="dtsv">사업별 표 복사</button><button class="btn primary" data-act="dprint">인쇄 / PDF 저장</button></div>
    <div class="row" style="gap:16px;flex-wrap:wrap;margin-top:8px;font-size:13px"><span class="muted">포함할 내용</span>${chk('dc-main', '주요 사항(저조·미집행·소진임박)', o.main)}${chk('dc-daily', '일상경비 요약', o.daily)}${chk('dc-util', '공공요금 요약', o.util)}</div></div>
    <div class="paper ${o.ori}" id="doc-paper"><div class="paper-h"><h1>${esc(S.year)}년 ${esc(S.dept || '')} 예산 집행 현황</h1>
      <div class="paper-meta"><span>기준일 ${esc(S.asOf)}</span><span>출력일 ${esc(today())}</span><span>자료: 통계목별 조회(${esc(D.B.file)})</span></div></div>
      ${renderOfficial(D, { noMain: !o.main, extra })}</div>`;
}
function docSet(k, v) { S.opt = S.opt || {}; S.opt.doc = { ...docOpt(), [k]: v }; save(); UI._noKeep = true; renderDoc(); }
$('#p-doc').addEventListener('click', e => {
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act;
  if (a === 'dunit') { S.opt = S.opt || {}; S.opt.repUnit = b.dataset.v; save(); UI._noKeep = true; renderDoc(); }
  else if (a === 'docpick') pickFiles('.xlsx,.xlsm');
  else if (a === 'dprint') {
    let st = $('#docpage'); if (!st) { st = document.createElement('style'); st.id = 'docpage'; document.head.appendChild(st); }
    st.textContent = `@page{size:A4 ${docOpt().ori};margin:10mm}`;
    $$('.tab-panel').forEach(p => p.classList.toggle('printing', p.id === 'p-doc')); window.print();
  } else if (a === 'dtsv') {
    const D = repData(); if (!D) return; const rows = repSorted(D.rows); const [, ud, udec] = unitOf(), q = v => (v / ud).toFixed(udec);
    copyText([[`세부사업 (단위: ${ul()})`, '예산현액', '지출원인행위액', '결의액', '실집행액', '집행잔액', '집행률(%)', '비고(사유)'], ...rows.map(r => [r.biz, q(r.budget), q(r.commit), q(r.resolved), q(r.paid), q(r.remain), r.rate.toFixed(1), (S.repNote && S.repNote[r.biz]) || '']), ['계', q(D.T.budget), q(D.T.commit), q(D.T.resolved), q(D.T.paid), q(D.T.remain), D.T.rate.toFixed(1), '']].map(r => r.join('\t')).join('\n'), '사업별 표를 복사했습니다 (엑셀에 붙여넣기)');
  }
});
$('#p-doc').addEventListener('change', e => {
  const t = e.target;
  if (t.id === 'dc-ori') docSet('ori', t.value); else if (t.id === 'dc-main') docSet('main', t.checked); else if (t.id === 'dc-daily') docSet('daily', t.checked); else if (t.id === 'dc-util') docSet('util', t.checked);
  else if (t.dataset.rn !== undefined) { S.repNote = S.repNote || {}; const v = t.value.trim(); if (v) S.repNote[t.dataset.rn] = v; else delete S.repNote[t.dataset.rn]; save(); }
});
window.addEventListener('afterprint', () => { const st = $('#docpage'); if (st) st.remove(); });
