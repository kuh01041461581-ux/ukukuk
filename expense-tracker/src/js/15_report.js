/* ===== 과 요약 보고서: 과장·국장 보고용 / 행정사무감사 자료용 한눈에 보기 (통계목별 조회 엑셀 기준) ===== */
UI.rep = { sort: 'budget', flagOnly: false };
const NATURE = { '1': '인건비', '2': '물건비 (운영비·여비·업무추진비 등)', '3': '이전지출 (보조금 등)', '4': '자산취득 (시설비 등)', '5': '상환지출', '6': '전출금·예비비' };
const natureOf = code => NATURE[String(code || '')[0]] || '기타';

function repData() {
  const B = S.biz; if (!B) return null;
  const E = B.entries.filter(e => (e.budget || 0) > 0 || (e.paid || 0) > 0 || (e.commit || 0) > 0);
  const el = elapsed();
  const bizMap = new Map(), natMap = new Map();
  E.forEach(e => {
    let r = bizMap.get(e.biz); if (!r) { r = { biz: e.biz, budget: 0, commit: 0, paid: 0 }; bizMap.set(e.biz, r); }
    r.budget += e.budget || 0; r.commit += e.commit || 0; r.paid += e.paid || 0;
    const nk = natureOf(e.code); let n = natMap.get(nk); if (!n) { n = { name: nk, budget: 0, commit: 0, paid: 0 }; natMap.set(nk, n); }
    n.budget += e.budget || 0; n.commit += e.commit || 0; n.paid += e.paid || 0;
  });
  const rows = [...bizMap.values()].map(r => {
    r.remain = r.budget - r.paid; r.rate = r.budget > 0 ? r.paid / r.budget * 100 : 0; r.gap = Math.max(0, r.commit - r.paid);
    r.flags = [];
    if (r.budget > 0 && r.paid <= 0) r.flags.push(['gray', '미집행']);
    else if (r.budget > 0 && r.rate < el - 20) r.flags.push(['warn', '집행 저조']);
    if (r.budget > 0 && (r.rate >= 95 || r.remain < 0)) r.flags.push(['bad', r.remain < 0 ? '예산 초과' : '잔액 소진 임박']);
    return r;
  });
  const nats = [...natMap.values()].map(n => ({ ...n, remain: n.budget - n.paid, rate: n.budget > 0 ? n.paid / n.budget * 100 : 0, gap: Math.max(0, n.commit - n.paid) })).sort((a, b) => b.budget - a.budget);
  const T = { budget: sum(rows, r => r.budget), paid: sum(rows, r => r.paid), commit: sum(rows, r => r.commit) };
  T.remain = T.budget - T.paid; T.rate = T.budget > 0 ? T.paid / T.budget * 100 : 0; T.gap = Math.max(0, T.commit - T.paid);
  return { rows, nats, T, el, B };
}
function repSorted(rows) {
  const by = { budget: (a, b) => b.budget - a.budget, rate: (a, b) => (a.budget > 0 ? a.rate : 999) - (b.budget > 0 ? b.rate : 999), remain: (a, b) => b.remain - a.remain, name: (a, b) => a.biz.localeCompare(b.biz, 'ko') };
  let L = rows.filter(r => r.budget > 0 || r.paid > 0); if (UI.rep.flagOnly) L = L.filter(r => r.flags.length);
  return L.sort(by[UI.rep.sort] || by.budget);
}
function repSummaryText(D) {
  const { T, el, rows } = D; const diff = T.rate - el;
  const low = rows.filter(r => r.flags.some(f => f[1] === '집행 저조')), zero = rows.filter(r => r.flags.some(f => f[1] === '미집행'));
  const s = [];
  s.push(`<b>${esc(S.asOf)}</b> 기준 예산현액 <b>${won(T.budget)}원</b> 중 <b>${won(T.paid)}원</b>을 집행하여 집행률은 <b>${pct1(T.rate)}</b>이며(연 경과율 ${pct1(el)} 대비 <b class="${diff < -10 ? 'neg' : ''}">${diff >= 0 ? '+' : '−'}${Math.abs(diff).toFixed(1)}%p</b>), 잔액은 <b>${won(T.remain)}원</b>입니다.`);
  if (T.gap > 0) s.push(`계약·결의(원인행위)는 했으나 아직 집행되지 않은 금액이 <b>${won(T.gap)}원</b>입니다 (원인행위 포함 시 ${pct1(T.budget > 0 ? Math.max(T.commit, T.paid) / T.budget * 100 : 0)}).`);
  if (low.length) s.push(`집행률이 경과율보다 20%p 이상 낮은 사업이 <b>${low.length}개</b>(예산 ${won(sum(low, r => r.budget))}원)입니다: ${low.sort((a, b) => b.budget - a.budget).slice(0, 4).map(r => esc(r.biz)).join(', ')}${low.length > 4 ? ' 외' : ''}.`);
  if (zero.length) s.push(`아직 집행 실적이 없는 사업이 <b>${zero.length}개</b>(예산 ${won(sum(zero, r => r.budget))}원)입니다.`);
  const hot = rows.filter(r => r.flags.some(f => f[0] === 'bad'));
  if (hot.length) s.push(`집행률 95% 이상이거나 예산을 초과한 사업이 <b>${hot.length}개</b>입니다: ${hot.slice(0, 4).map(r => esc(r.biz)).join(', ')}.`);
  return s.map(x => `<li>${x}</li>`).join('');
}
function repBarRow(name, r, big) {
  return `<div class="rb${big ? ' big' : ''}"><span class="rbn">${esc(name)}</span><span class="rbb">${barHTML(r.paid, r.gap, r.budget)}</span><span class="rbv"><b>${pct1(r.rate)}</b></span><span class="rbs">예산 ${won(r.budget)} · 집행 ${won(r.paid)} · <b>잔액 ${won(r.remain)}</b></span></div>`;
}
function renderReport() {
  const P = $('#p-report'); const D = repData();
  if (!D) { P.innerHTML = `<div class="card"><h2>과 요약 보고서</h2><div class="empty">통계목별 조회 엑셀을 불러오면 과 전체 예산 요약 보고서가 만들어집니다.<br><br><button class="btn primary" data-act="reppick">통계목별 조회 엑셀 선택</button></div></div>`; return; }
  const { T, nats, el } = D; const rows = repSorted(D.rows); const diff = T.rate - el;
  const kpi = (lbl, v, sub, cls) => `<div class="repk ${cls || ''}"><div class="k">${lbl}</div><div class="v">${v}</div><div class="s">${sub || ''}</div></div>`;
  const dailyT = S.lines.length ? groupTotals(visLines()) : null;
  const ts = todoSummary();
  P.innerHTML = `<div class="rep" id="rep-doc">
    <div class="rep-head"><div><h1>${esc(S.year)}년 ${esc(S.dept || '')} 예산 집행 현황</h1><div class="muted">기준일 ${esc(S.asOf)} · 자료: ${esc(D.B.file)} (통계목별 조회, 실집행액=집행누계액)</div></div>
      <div class="row noprint"><button class="btn sm" data-act="reptsv" data-tip="사업별 표를 엑셀·한글에 붙여넣을 수 있게 복사합니다">사업별 표 복사</button><button class="btn primary sm" data-act="repprint">인쇄 / PDF 저장</button></div></div>
    <div class="repks">${kpi('예산현액', won(T.budget) + '<small>원</small>', '과 전체(통계목별 조회 기준)')}${kpi('집행액', won(T.paid) + '<small>원</small>', `집행률 <b>${pct1(T.rate)}</b>`)}${kpi('남은 예산 (잔액)', won(T.remain) + '<small>원</small>', `<span class="${diff < -10 ? 'neg' : ''}">경과율 ${pct1(el)} 대비 ${diff >= 0 ? '▲' : '▼'} ${Math.abs(diff).toFixed(1)}%p</span>`, 'main')}${kpi('계약·결의 후 미집행', won(T.gap) + '<small>원</small>', '원인행위 − 집행액')}</div>
    <div class="repsum card"><h2>요약</h2><ul>${repSummaryText(D)}</ul></div>
    <div class="legend rep-legend"><span><i class="lg1"></i>집행액</span><span><i class="lg2"></i>계약·결의 후 미집행</span><span><i class="lg3"></i>남은 예산</span><span>┆ 연 경과율 ${pct1(el)}</span></div>
    <div class="card"><h2>성질별 (통계목 대분류) <span class="muted" style="font-weight:400;font-size:12px">통계목 코드 첫 자리 기준</span></h2>${nats.map(n => repBarRow(n.name, n, true)).join('')}</div>
    <div class="card reptblcard"><div class="row" style="justify-content:space-between;margin-bottom:6px"><h2 style="margin:0">사업별 집행 현황 <span class="muted" style="font-weight:400;font-size:12px">${rows.length}개 사업</span></h2>
      <div class="row noprint"><div class="pills" role="group" aria-label="정렬">${[['budget', '예산 큰 순'], ['rate', '집행률 낮은 순'], ['remain', '잔액 큰 순'], ['name', '사업명순']].map(([k, n]) => `<button class="pill sm" aria-pressed="${UI.rep.sort === k}" data-act="repsort" data-v="${k}">${n}</button>`).join('')}</div>
      <label class="row" style="gap:5px"><input type="checkbox" id="rep-flag" ${UI.rep.flagOnly ? 'checked' : ''}> 주의 사업만</label></div></div>
      <div class="tbl-wrap"><table class="t reptbl"><thead><tr><th>#</th><th>세부사업</th><th>예산현액</th><th>원인행위</th><th>집행액</th><th>잔액</th><th style="min-width:200px">집행률</th><th>비고</th></tr></thead><tbody>
      ${rows.map((r, i) => `<tr><td class="muted">${i + 1}</td><td class="nm2">${esc(r.biz)}</td><td class="num">${won(r.budget)}</td><td class="num sm">${won(r.commit)}</td><td class="num">${won(r.paid)}</td><td class="num remv ${r.remain < 0 ? 'neg' : ''}">${won(r.remain)}</td><td class="pcol"><div class="rate">${barHTML(r.paid, r.gap, r.budget)}<b>${pct1(r.rate)}</b></div></td><td>${r.flags.map(f => `<span class="badge ${f[0] === 'bad' ? 'bad' : f[0] === 'warn' ? 'warn' : 'gray'}">${f[1]}</span>`).join(' ')}</td></tr>`).join('') || '<tr><td colspan="8" class="empty">해당하는 사업이 없습니다</td></tr>'}
      <tr class="tot"><td></td><td>합계</td><td class="num">${won(T.budget)}</td><td class="num sm">${won(T.commit)}</td><td class="num">${won(T.paid)}</td><td class="num remv">${won(T.remain)}</td><td class="pcol"><b>${pct1(T.rate)}</b></td><td></td></tr></tbody></table></div></div>
    ${dailyT ? `<div class="card"><h2>일상경비 <span class="muted" style="font-weight:400;font-size:12px">사무관리비·공공운영비·업무추진비·여비 (프로그램 입력 기준${S.lines.some(l => l.hidden) ? ' · 숨긴 항목 제외' : ''})</span></h2>${GROUPS.map(g => { const Ls = visLines().filter(l => l.group === g); if (!Ls.length) return ''; const gt = groupTotals(Ls); return repBarRow(g, { paid: gt.spent, gap: gt.sch, budget: gt.budget, rate: gt.rate, remain: gt.remain }); }).join('')}</div>` : ''}
    ${ts.length ? `<div class="card"><h2>공공요금 <span class="muted" style="font-weight:400;font-size:12px">올해 지출 합계와 고지서 처리 현황</span></h2><div class="reput">${ts.map(t => { const sec = S.util[t.k]; return `<div class="ru"><b>${t.nm}</b><span class="rv">${won(catTotal(sec))}원</span><span class="muted">${sec.items.filter(i => !i.inactive).length}곳 · ${t.unpaid ? `미납 ${t.unpaid}건 ${won(t.amt)}원` : '미납 없음'}${t.missing ? ` · 미수령 ${t.missing}건` : ''}</span></div>`; }).join('')}</div></div>` : ''}
    <div class="muted rep-foot">※ 집행액은 통계목별 조회의 집행누계액(실집행액), 원인행위는 계약·결의 등으로 확정된 금액입니다. 이월 예산은 제외하고 당해예산만 집계했습니다. 이 보고서는 프로그램이 자동 집계한 참고 자료입니다.</div></div>`;
}
$('#p-report').addEventListener('click', e => {
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act;
  if (a === 'repsort') { UI.rep.sort = b.dataset.v; renderReport(); }
  else if (a === 'repprint') { $$('.tab-panel').forEach(p => p.classList.toggle('printing', p.id === 'p-report')); window.print(); }
  else if (a === 'reppick') pickFiles('.xlsx,.xlsm');
  else if (a === 'reptsv') {
    const D = repData(); if (!D) return; const rows = repSorted(D.rows);
    const tsv = [['세부사업', '예산현액', '원인행위', '집행액', '잔액', '집행률(%)'], ...rows.map(r => [r.biz, r.budget, r.commit, r.paid, r.remain, r.rate.toFixed(1)]), ['합계', D.T.budget, D.T.commit, D.T.paid, D.T.remain, D.T.rate.toFixed(1)]].map(r => r.join('\t')).join('\n');
    copyText(tsv, '사업별 표를 복사했습니다 (엑셀에 붙여넣기)');
  }
});
$('#p-report').addEventListener('change', e => { if (e.target.id === 'rep-flag') { UI.rep.flagOnly = e.target.checked; renderReport(); } });
