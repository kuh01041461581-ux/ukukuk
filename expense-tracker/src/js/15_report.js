/* ===== 과 요약 보고서: 과장·국장 보고용 / 행정사무감사 자료용 (통계목별 조회 엑셀 기준) =====
   기본은 [요약] 한 화면(핵심 숫자 + 요약 문장 + 주의 사업만). 자세히 보려면 [상세]. */
UI.rep = { sort: 'budget', flagOnly: false, view: 'easy', easyAll: false };
const NATURE = { '1': '인건비', '2': '물건비 (운영비·여비·업무추진비 등)', '3': '이전지출 (보조금 등)', '4': '자산취득 (시설비 등)' };
const natureOf = code => NATURE[String(code || '')[0]] || `기타 (통계목 ${String(code || '?')[0]}번대)`;
const thr = () => ({ low: +(S.opt && S.opt.thrLow) || 20, hot: +(S.opt && S.opt.thrHot) || 95 });

/* 불러올 때마다 그 시점 숫자를 저장해 두었다가 직전 자료와 비교 */
function pushHist(biz) {
  const per = {}; let bud = 0, paid = 0, commit = 0;
  biz.entries.filter(e => (e.budget || 0) > 0 || (e.paid || 0) > 0).forEach(e => { const r = per[e.biz] || (per[e.biz] = [0, 0]); r[0] += e.budget || 0; r[1] += e.paid || 0; bud += e.budget || 0; paid += e.paid || 0; commit += e.commit || 0; });
  S.hist = (S.hist || []).filter(h => h.asOf !== biz.asOf); S.hist.push({ asOf: biz.asOf, file: biz.file, bud, paid, commit, per });
  S.hist.sort((a, b) => String(a.asOf).localeCompare(String(b.asOf))); S.hist = S.hist.slice(-36);
}
function histPrev() { const h = S.hist || []; const i = h.findIndex(x => x.asOf === (S.biz && S.biz.asOf)); return i > 0 ? h[i - 1] : (i < 0 && h.length ? h[h.length - 1] : null); }

function repData() {
  const B = S.biz; if (!B) return null;
  const E = B.entries.filter(e => (e.budget || 0) > 0 || (e.paid || 0) > 0 || (e.commit || 0) > 0);
  const el = elapsed(), th = thr(), prev = histPrev();
  const bizMap = new Map(), natMap = new Map();
  E.forEach(e => {
    let r = bizMap.get(e.biz); if (!r) { r = { biz: e.biz, budget: 0, commit: 0, paid: 0 }; bizMap.set(e.biz, r); }
    r.budget += e.budget || 0; r.commit += e.commit || 0; r.paid += e.paid || 0;
    const nk = natureOf(e.code); let n = natMap.get(nk); if (!n) { n = { name: nk, budget: 0, commit: 0, paid: 0 }; natMap.set(nk, n); }
    n.budget += e.budget || 0; n.commit += e.commit || 0; n.paid += e.paid || 0;
  });
  const rows = [...bizMap.values()].map(r => {
    r.remain = r.budget - r.paid; r.rate = r.budget > 0 ? r.paid / r.budget * 100 : 0; r.gap = Math.max(0, r.commit - r.paid); r.flags = [];
    if (r.budget > 0 && r.paid <= 0) r.flags.push(['gray', '미집행']);
    else if (r.budget > 0 && r.rate < el - th.low) r.flags.push(['warn', '집행 저조']);
    if (r.budget > 0 && (r.rate >= th.hot || r.remain < 0)) r.flags.push(['bad', r.remain < 0 ? '예산 초과' : '잔액 소진 임박']);
    const pv = prev && prev.per && prev.per[r.biz]; r.dlt = pv && pv[0] > 0 ? r.rate - pv[1] / pv[0] * 100 : null;
    return r;
  });
  const nats = [...natMap.values()].map(n => ({ ...n, remain: n.budget - n.paid, rate: n.budget > 0 ? n.paid / n.budget * 100 : 0, gap: Math.max(0, n.commit - n.paid) })).sort((a, b) => b.budget - a.budget);
  const T = { budget: sum(rows, r => r.budget), paid: sum(rows, r => r.paid), commit: sum(rows, r => r.commit) };
  T.remain = T.budget - T.paid; T.rate = T.budget > 0 ? T.paid / T.budget * 100 : 0; T.gap = Math.max(0, T.commit - T.paid); T.rateAll = T.budget > 0 ? Math.max(T.commit, T.paid) / T.budget * 100 : 0;
  T.dlt = prev && prev.bud > 0 ? T.rate - prev.paid / prev.bud * 100 : null;
  return { rows, nats, T, el, B, th, prev };
}
function repSorted(rows) {
  const by = { budget: (a, b) => b.budget - a.budget, rate: (a, b) => (a.budget > 0 ? a.rate : 999) - (b.budget > 0 ? b.rate : 999), remain: (a, b) => b.remain - a.remain, name: (a, b) => a.biz.localeCompare(b.biz, 'ko') };
  let L = rows.filter(r => r.budget > 0 || r.paid > 0); if (UI.rep.flagOnly) L = L.filter(r => r.flags.length);
  return L.sort(by[UI.rep.sort] || by.budget);
}
function repSummaryText(D) {
  const { T, el, rows, th } = D; const diff = T.rate - el;
  const low = rows.filter(r => r.flags.some(f => f[1] === '집행 저조')), zero = rows.filter(r => r.flags.some(f => f[1] === '미집행')), hot = rows.filter(r => r.flags.some(f => f[0] === 'bad'));
  const s = [];
  s.push(`<b>${esc(S.asOf)}</b> 기준 예산현액 <b>${fu(T.budget)}${ul()}</b> 중 <b>${fu(T.paid)}${ul()}</b>을 집행하여 집행률은 <b>${pct1(T.rate)}</b>이며(연 경과율 ${pct1(el)} 대비 <b class="${diff < -10 ? 'neg' : ''}">${diff >= 0 ? '+' : '−'}${Math.abs(diff).toFixed(1)}%p</b>), 잔액은 <b>${fu(T.remain)}${ul()}</b>입니다.`);
  if (T.gap > 0) s.push(`계약·결의(원인행위)는 했으나 아직 집행되지 않은 금액이 <b>${fu(T.gap)}${ul()}</b>이며, 이를 포함하면 ${pct1(T.rateAll)}입니다.`);
  if (low.length) s.push(`집행률이 경과율보다 ${th.low}%p 이상 낮은 사업이 <b>${low.length}개</b>(예산 ${fu(sum(low, r => r.budget))}${ul()})입니다: ${[...low].sort((a, b) => b.budget - a.budget).slice(0, 4).map(r => esc(r.biz)).join(', ')}${low.length > 4 ? ' 외' : ''}.`);
  if (zero.length) s.push(`집행 실적이 없는 사업이 <b>${zero.length}개</b>(예산 ${fu(sum(zero, r => r.budget))}${ul()})입니다.`);
  if (hot.length) s.push(`집행률 ${th.hot}% 이상이거나 예산을 초과한 사업이 <b>${hot.length}개</b>입니다: ${hot.slice(0, 4).map(r => esc(r.biz)).join(', ')}.`);
  return s.map(x => `<li>${x}</li>`).join('');
}
function repBarRow(name, r, big) {
  return `<div class="rb${big ? ' big' : ''}"><span class="rbn">${esc(name)}</span><span class="rbb">${barHTML(r.paid, r.gap, r.budget)}</span><span class="rbv"><b>${pct1(r.rate)}</b></span><span class="rbs">예산 ${fu(r.budget)} · 집행 ${fu(r.paid)} · <b>잔액 ${fu(r.remain)}</b></span></div>`;
}
const dltTxt = d => d == null || Math.abs(d) < 0.05 ? '' : `<span class="dlt ${d > 0 ? 'up' : 'dn'}" data-tip="직전 자료 대비 집행률 변화">${d > 0 ? '+' : '−'}${Math.abs(d).toFixed(1)}</span>`;

/* 집행 추이: 불러온 시점별 집행률 vs 경과율 */
function repTrend() {
  const h = (S.hist || []).slice(-12); if (h.length < 2) return '';
  const W = 640, H = 150, pl = 36, pr = 16, pt = 14, pb = 30, X = i => pl + (W - pl - pr) * (h.length === 1 ? 0.5 : i / (h.length - 1)), Y = v => pt + (H - pt - pb) * (1 - Math.min(100, Math.max(0, v)) / 100);
  const el = a => { const d = new Date(a + 'T00:00:00'); const y = d.getFullYear(); return isNaN(d) ? 0 : ((d - new Date(y, 0, 1)) / 86400000 + 1) / ((new Date(y + 1, 0, 1) - new Date(y, 0, 1)) / 86400000) * 100; };
  const pts = h.map((x, i) => [X(i), Y(x.bud > 0 ? x.paid / x.bud * 100 : 0), x.bud > 0 ? x.paid / x.bud * 100 : 0, x.asOf]);
  const ep = h.map((x, i) => [X(i), Y(el(x.asOf))]);
  return `<div class="card"><h2>집행 추이 <span class="muted" style="font-weight:400;font-size:12px">자료를 불러온 시점별 · 파란 선 = 집행률, 점선 = 연 경과율</span></h2>
    <svg viewBox="0 0 ${W} ${H}" class="trend" role="img" aria-label="집행률 추이">
      ${[0, 50, 100].map(v => `<line x1="${pl}" x2="${W - pr}" y1="${Y(v)}" y2="${Y(v)}" stroke="var(--edge,#e6e9f0)"/><text x="${pl - 6}" y="${Y(v) + 4}" text-anchor="end" style="font-size:10px;fill:var(--text-2)">${v}%</text>`).join('')}
      <polyline points="${ep.map(p => p.join(',')).join(' ')}" fill="none" stroke="var(--text-2)" stroke-width="1.5" stroke-dasharray="4 4"/>
      <polyline points="${pts.map(p => p[0] + ',' + p[1]).join(' ')}" fill="none" stroke="var(--accent,#2a78d6)" stroke-width="2.5"/>
      ${pts.map(p => `<circle cx="${p[0]}" cy="${p[1]}" r="4.5" fill="var(--accent,#2a78d6)" stroke="#fff" stroke-width="2" data-tip="${p[3]}\n집행률 ${pct1(p[2])}"/><text x="${p[0]}" y="${p[1] - 9}" text-anchor="middle" style="font-size:11px;font-weight:700;fill:var(--text)">${pct1(p[2])}</text><text x="${p[0]}" y="${H - 10}" text-anchor="middle" style="font-size:10px;fill:var(--text-2)">${p[3].slice(5).replace('-', '/')}</text>`).join('')}
    </svg></div>`;
}
function repNoteCell(r) {
  const v = (S.repNote && S.repNote[r.biz]) || '';
  if (!r.flags.length && !v) return '';   // 주의 표시가 없는 사업은 입력칸을 감춰 화면을 단순하게
  return `<div class="rnote"><input class="cellin rni" data-rn="${esc(r.biz)}" value="${esc(v)}" placeholder="${r.flags.length ? '사유·계획 입력' : ''}" aria-label="${esc(r.biz)} 사유·메모">${v ? `<span class="rpn">${esc(v)}</span>` : ''}</div>`;
}
function repRowHTML(r, i) {
  return `<tr><td class="muted">${i + 1}</td><td class="nm2">${esc(r.biz)}</td><td class="num">${fu(r.budget)}</td><td class="num">${fu(r.paid)}</td><td class="num remv ${r.remain < 0 ? 'neg' : ''}">${fu(r.remain)}</td><td class="pcol"><div class="rate">${barHTML(r.paid, r.gap, r.budget)}<b>${pct1(r.rate)}</b>${dltTxt(r.dlt)}</div></td><td>${r.flags.map(f => `<span class="badge ${f[0] === 'bad' ? 'bad' : f[0] === 'warn' ? 'warn' : 'gray'}">${f[1]}</span>`).join(' ')}${repNoteCell(r)}</td></tr>`;
}
function repTable(rows, T, withSortBar) {
  return `<div class="unitcap">(단위: ${ul()})</div><div class="tbl-wrap"><table class="t reptbl"><thead><tr><th>#</th><th>세부사업</th><th>예산현액</th><th>집행액</th><th>잔액</th><th style="min-width:210px">집행률</th><th style="min-width:220px">표시 · 사유/메모</th></tr></thead><tbody>
    ${rows.map(repRowHTML).join('') || '<tr><td colspan="7" class="empty">해당하는 사업이 없습니다</td></tr>'}
    <tr class="tot"><td></td><td>합계</td><td class="num">${fu(T.budget)}</td><td class="num">${fu(T.paid)}</td><td class="num remv">${fu(T.remain)}</td><td class="pcol"><b>${pct1(T.rate)}</b></td><td></td></tr></tbody></table></div>`;
}
function renderReport() {
  const P = $('#p-report'); const D = repData();
  if (!D) { P.innerHTML = `<div class="card"><h2>과 요약 보고서</h2><div class="empty">통계목별 조회 엑셀을 불러오면 과 전체 예산 요약 보고서가 만들어집니다.<br><br><button class="btn primary" data-act="reppick">통계목별 조회 엑셀 선택</button></div></div>`; return; }
  const { T, nats, el } = D, full = UI.rep.view === 'full'; const diff = T.rate - el;
  const kpi = (lbl, v, sub, cls) => `<div class="repk ${cls || ''}"><div class="k">${lbl}</div><div class="v">${v}</div><div class="s">${sub || ''}</div></div>`;
  const flagged = D.rows.filter(r => r.flags.length).sort((a, b) => b.budget - a.budget);
  const dailyT = S.lines.length ? groupTotals(visLines()) : null; const ts = todoSummary();
  const dprev = D.prev && T.dlt != null ? `<br>직전 자료(${esc(D.prev.asOf)}) 대비 ${T.dlt >= 0 ? '+' : '−'}${Math.abs(T.dlt).toFixed(1)}%p` : '';
  const head = `<div class="rep-head"><div><h1>${esc(S.year)}년 ${esc(S.dept || '')} 예산 집행 현황</h1><div class="muted">기준일 ${esc(S.asOf)} · 자료: ${esc(D.B.file)} (통계목별 조회 · 집행액=집행누계액)${UI.rep.view === 'sum' || UI.rep.view === 'full' ? ` · <b>단위: ${ul()}</b>` : ''}</div></div>
      <div class="row noprint"><div class="pills" role="group" aria-label="보기">${[['easy', '쉬운 보기'], ['sum', '요약'], ['full', '상세'], ['graph', '그래프'], ['official', '공식 양식']].map(([k, n]) => `<button class="pill sm" aria-pressed="${UI.rep.view === k}" data-act="repview" data-v="${k}">${n}</button>`).join('')}</div>${UI.rep.view === 'easy' ? '' : `<div class="pills" role="group" aria-label="단위"><span class="muted" style="align-self:center;font-size:12px;margin-right:4px">단위</span>${UNITS.map(u => `<button class="pill sm" aria-pressed="${ul() === u[0]}" data-act="repunit" data-v="${u[0]}">${u[0]}</button>`).join('')}</div>`}<button class="btn sm" data-act="reptsv" data-tip="사업별 표를 엑셀·한글에 붙여넣을 수 있게 복사합니다">사업별 표 복사</button><button class="btn primary sm" data-act="repprint">인쇄 / PDF 저장</button></div></div>`;
  if (UI.rep.view === 'easy') { P.innerHTML = `<div class="rep" id="rep-doc">${head}${renderEasy(D)}</div>`; return; }
  if (UI.rep.view === 'graph') { P.innerHTML = `<div class="rep" id="rep-doc">${head}${renderGraphView(D)}</div>`; return; }
  if (UI.rep.view === 'official') { P.innerHTML = `<div class="rep" id="rep-doc">${head}${renderOfficial(D)}</div>`; return; }
  P.innerHTML = `<div class="rep" id="rep-doc">${head}
    <div class="repks">${kpi('예산현액', fu(T.budget) + '<small>' + ul() + '</small>', '과 전체')}${kpi('집행액', fu(T.paid) + '<small>' + ul() + '</small>', `집행률 <b>${pct1(T.rate)}</b><br>원인행위 포함 ${pct1(T.rateAll)}`)}${kpi('남은 예산 (잔액)', fu(T.remain) + '<small>' + ul() + '</small>', `<span class="${diff < -10 ? 'neg' : ''}">경과율 ${pct1(el)} 대비 ${diff >= 0 ? '▲' : '▼'} ${Math.abs(diff).toFixed(1)}%p</span>${dprev}`, 'main')}${kpi('계약·결의 후 미집행', fu(T.gap) + '<small>' + ul() + '</small>', '원인행위 − 집행액')}</div>
    ${UI.rep.view === 'sum' ? reconNotice() : ''}
    <div class="repsum card"><h2>요약</h2><ul>${repSummaryText(D)}</ul></div>
    ${repTrend()}
    <div class="legend rep-legend"><span><i class="lg1"></i>집행액</span><span><i class="lg2"></i>계약·결의 후 미집행</span><span><i class="lg3"></i>남은 예산</span><span>┆ 연 경과율 ${pct1(el)}</span></div>
    ${full ? `${reconCardHTML(T)}<div class="card"><h2>성질별 (통계목 대분류) <span class="muted" style="font-weight:400;font-size:12px">통계목 코드 첫 자리 기준</span></h2>${nats.map(n => repBarRow(n.name, n, true)).join('')}</div>
    <div class="card reptblcard"><div class="row" style="justify-content:space-between;margin-bottom:6px"><h2 style="margin:0">사업별 집행 현황 <span class="muted" style="font-weight:400;font-size:12px">${repSorted(D.rows).length}개 사업</span></h2>
      <div class="row noprint"><div class="pills" role="group" aria-label="정렬">${[['budget', '예산 큰 순'], ['rate', '집행률 낮은 순'], ['remain', '잔액 큰 순'], ['name', '사업명순']].map(([k, n]) => `<button class="pill sm" aria-pressed="${UI.rep.sort === k}" data-act="repsort" data-v="${k}">${n}</button>`).join('')}</div>
      <label class="row" style="gap:5px"><input type="checkbox" id="rep-flag" ${UI.rep.flagOnly ? 'checked' : ''}> 주의 사업만</label></div></div>${repTable(repSorted(D.rows), T)}</div>
    ${dailyT ? `<div class="card"><h2>일상경비 <span class="muted" style="font-weight:400;font-size:12px">사무관리비·공공운영비·업무추진비·여비${S.lines.some(l => l.hidden) ? ' · 숨긴 항목 제외' : ''}</span></h2>${GROUPS.map(g => { const Ls = visLines().filter(l => l.group === g); if (!Ls.length) return ''; const gt = groupTotals(Ls); return repBarRow(g, { paid: gt.spent, gap: gt.sch, budget: gt.budget, rate: gt.rate, remain: gt.remain }); }).join('')}</div>` : ''}
    ${ts.length ? `<div class="card"><h2>공공요금 <span class="muted" style="font-weight:400;font-size:12px">올해 지출 합계와 고지서 처리 현황</span></h2><div class="reput">${ts.map(t => { const sec = S.util[t.k]; return `<div class="ru"><b>${t.nm}</b><span class="rv">${fu(catTotal(sec))}${ul()}</span><span class="muted">${sec.items.filter(i => !i.inactive).length}곳 · ${t.unpaid ? `미납 ${t.unpaid}건 ${fu(t.amt)}${ul()}` : '미납 없음'}${t.missing ? ` · 미수령 ${t.missing}건` : ''}</span></div>`; }).join('')}</div></div>` : ''}`
    : `<div class="card reptblcard"><div class="row" style="justify-content:space-between;margin-bottom:6px"><h2 style="margin:0">주의가 필요한 사업 <span class="muted" style="font-weight:400;font-size:12px">${flagged.length}개${flagged.length > 8 ? ' 중 예산이 큰 8개' : ''} · 사유·계획을 적어 두면 인쇄에 같이 나옵니다</span></h2><button class="btn sm noprint" data-act="repview" data-v="full">전체 사업 보기</button></div>
      ${flagged.length ? repTable(flagged.slice(0, 8), T) : '<div class="empty" style="padding:14px">✓ 주의가 필요한 사업이 없습니다</div>'}</div>`}
    <div class="muted rep-foot">※ 집행액은 통계목별 조회의 집행누계액(실집행액)이며, 원인행위는 계약·결의 등으로 확정된 금액입니다. 이월 예산은 제외하고 당해예산만 집계했습니다. 「집행 저조」는 집행률이 연 경과율보다 ${D.th.low}%p 이상 낮은 경우, 「잔액 소진 임박」은 집행률 ${D.th.hot}% 이상인 경우입니다(설정에서 변경). 이 보고서는 프로그램이 자동 집계한 참고 자료입니다.</div></div>`;
}
$('#p-report').addEventListener('click', e => {
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act;
  if (a === 'repgraphall') { UI.rep.graphAll = !UI.rep.graphAll; UI._noKeep = true; renderReport(); }
  else if (a === 'repunit') { S.opt = S.opt || {}; S.opt.repUnit = b.dataset.v; save(); UI._noKeep = true; renderReport(); }
  else if (a === 'repsort') { UI.rep.sort = b.dataset.v; renderReport(); }
  else if (a === 'reconfix') { const n = reconApply([b.dataset.id]); toast(n ? 'e-호조 값으로 맞췄습니다' : '이미 같습니다'); UI._noKeep = true; renderReport(); }
  else if (a === 'reconfixall') { const rc = reconcile(); if (rc && confirm(`${rc.diffs.length}곳의 일상경비 지출을 e-호조 값으로 바꿉니다. 계속할까요?`)) { const n = reconApply(); toast(`${n}곳을 e-호조 값으로 맞췄습니다`); UI._noKeep = true; renderReport(); } }
  else if (a === 'repeasyall') { UI.rep.easyAll = !UI.rep.easyAll; UI._noKeep = true; renderReport(); }
  else if (a === 'repview') { UI.rep.view = b.dataset.v; UI._noKeep = true; renderReport(); window.scrollTo(0, 0); }
  else if (a === 'repprint') { $$('.tab-panel').forEach(p => p.classList.toggle('printing', p.id === 'p-report')); window.print(); }
  else if (a === 'reppick') pickFiles('.xlsx,.xlsm');
  else if (a === 'reptsv') {
    const D = repData(); if (!D) return; const rows = repSorted(D.rows);
    const [, ud, udec] = unitOf(), q = v => (v / ud).toFixed(udec);
    const tsv = [[`세부사업 (단위: ${ul()})`, '예산현액', '지출원인행위액', '지출액', '집행잔액', '집행률(%)', '비고(사유)'], ...rows.map(r => [r.biz, q(r.budget), q(r.commit), q(r.paid), q(r.remain), r.rate.toFixed(1), (S.repNote && S.repNote[r.biz]) || '']), ['계', q(D.T.budget), q(D.T.commit), q(D.T.paid), q(D.T.remain), D.T.rate.toFixed(1), '']].map(r => r.join('\t')).join('\n');
    copyText(tsv, '사업별 표를 복사했습니다 (엑셀에 붙여넣기)');
  }
});
$('#p-report').addEventListener('change', e => {
  const t = e.target;
  if (t.id === 'rep-flag') { UI.rep.flagOnly = t.checked; renderReport(); }
  else if (t.dataset.rn !== undefined) { S.repNote = S.repNote || {}; const v = t.value.trim(); if (v) S.repNote[t.dataset.rn] = v; else delete S.repNote[t.dataset.rn]; save(); }
});
