/* ===== 전체 사업 진척 (통계목별 조회 엑셀의 모든 세부사업) — 설정에서 켠 때만 보임 ===== */
UI.biz = { q: '', code: '', sort: 'name', open: {}, hideZero: true };
function bizRows() {
  const B = S.biz; if (!B) return [];
  const q = normQ(UI.biz.q); const map = new Map();
  B.entries.forEach(e => {
    if (UI.biz.code && e.code !== UI.biz.code) return;
    let r = map.get(e.biz); if (!r) { r = { biz: e.biz, budget: 0, paid: 0, commit: 0, items: [] }; map.set(e.biz, r); }
    r.budget += e.budget || 0; r.paid += e.paid || 0; r.commit += e.commit || 0; r.items.push(e);
  });
  let rows = [...map.values()];
  if (UI.biz.hideZero) rows = rows.filter(r => r.budget > 0 || r.paid > 0);
  if (q) rows = rows.filter(r => normQ(r.biz).includes(q) || r.items.some(e => normQ(e.name).includes(q) || e.code.includes(q)));
  rows.forEach(r => { r.remain = r.budget - r.paid; r.rate = r.budget > 0 ? r.paid / r.budget * 100 : 0; });
  const by = { name: (a, b) => a.biz.localeCompare(b.biz, 'ko'), remain: (a, b) => b.remain - a.remain, rate: (a, b) => b.rate - a.rate, budget: (a, b) => b.budget - a.budget };
  return rows.sort(by[UI.biz.sort] || by.name);
}
function renderBiz() {
  const P = $('#p-biz'); const B = S.biz;
  if (!B) { P.innerHTML = `<div class="card"><h2>전체 사업 진척</h2><div class="empty">통계목별 조회 엑셀을 불러오면 모든 사업의 예산·집행·잔액이 여기에 나타납니다.<br><br><button class="btn primary" data-act="bizpick">통계목별 조회 엑셀 선택</button></div></div>`; return; }
  const rows = bizRows(); const T = { budget: sum(rows, r => r.budget), paid: sum(rows, r => r.paid) }; T.remain = T.budget - T.paid; T.rate = T.budget > 0 ? T.paid / T.budget * 100 : 0;
  const codes = [...new Map(B.entries.map(e => [e.code, e.name])).entries()].sort((a, b) => a[0].localeCompare(b[0]));
  const trs = rows.map(r => {
    const open = UI.biz.open[r.biz]; const cls = r.budget > 0 && r.remain / r.budget <= 0.05 ? 'rem-low' : '';
    let h = `<tr class="clk ${cls}" data-act="bizopen" data-k="${esc(r.biz)}"><td class="nm2"><span class="caret">${open ? '▾' : '▸'}</span> ${esc(r.biz)}<span class="hint">${r.items.length}개 목</span></td><td class="num remv ${r.remain < 0 ? 'neg' : ''}">${won(r.remain)}</td><td class="num sm">${won(r.budget)}</td><td class="num sm">${won(r.paid)}</td><td class="pcol"><div class="rate">${barHTML(r.paid, 0, r.budget, { noMark: true })}<b>${pct1(r.rate)}</b></div></td></tr>`;
    if (open) h += r.items.filter(e => !UI.biz.hideZero || e.budget > 0 || e.paid > 0).map(e => `<tr class="sub"><td class="nm2" style="padding-left:34px">${esc(e.name)}<span class="hint">${esc(e.code)}</span></td><td class="num">${won((e.budget || 0) - (e.paid || 0))}</td><td class="num sm">${won(e.budget)}</td><td class="num sm">${won(e.paid)}</td><td class="pcol">${e.budget > 0 ? pct1(e.paid / e.budget * 100) : ''}</td></tr>`).join('');
    return h;
  }).join('');
  P.innerHTML = `<div class="kpis"><div class="kpi bigrem"><div class="k">선택 사업 남은 예산</div><div class="v ${T.remain < 0 ? 'neg' : ''}">${won(T.remain)}<small>원</small></div><div class="s">예산 ${won(T.budget)}원 · 집행 ${won(T.paid)}원 · 집행률 <b>${pct1(T.rate)}</b></div></div></div>
  <div class="card" style="margin-top:12px"><div class="row noprint" style="gap:10px;flex-wrap:wrap;margin-bottom:8px">
    <input id="b-q" class="cellin" style="text-align:left;width:220px" placeholder="사업명·목 검색" value="${esc(UI.biz.q)}">
    <select id="b-code" class="cellin" style="width:auto">${'<option value="">모든 통계목</option>' + codes.map(([c, n]) => `<option value="${c}" ${UI.biz.code === c ? 'selected' : ''}>${esc(c + ' ' + n)}</option>`).join('')}</select>
    <div class="pills" role="group" aria-label="정렬">${[['name', '사업명순'], ['remain', '잔액 많은 순'], ['rate', '집행률 높은 순'], ['budget', '예산 큰 순']].map(([k, n]) => `<button class="pill sm" aria-pressed="${UI.biz.sort === k}" data-act="bizsort" data-v="${k}">${n}</button>`).join('')}</div>
    <label class="row" style="gap:5px"><input type="checkbox" id="b-zero" ${UI.biz.hideZero ? 'checked' : ''}> 예산·집행 0원 숨김</label>
    <span class="muted" style="margin-left:auto">${esc(B.file)} · 기준 ${esc(B.asOf)} · 사업을 누르면 목별로 펼쳐집니다</span></div>
    <div class="tbl-wrap"><table class="t nowt"><thead><tr><th>세부사업</th><th>남은 잔액</th><th>예산현액</th><th>실집행액</th><th>집행률</th></tr></thead><tbody>${trs || '<tr><td colspan=5 class="empty">조건에 맞는 사업이 없습니다</td></tr>'}</tbody></table></div></div>`;
  const q = $('#b-q'); if (q && UI.biz._f) { q.focus(); q.setSelectionRange(q.value.length, q.value.length); UI.biz._f = false; }
}
$('#p-biz').addEventListener('click', e => {
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act;
  if (a === 'bizopen') { UI.biz.open[b.dataset.k] = !UI.biz.open[b.dataset.k]; UI._noKeep = false; renderBiz(); }
  else if (a === 'bizsort') { UI.biz.sort = b.dataset.v; renderBiz(); }
  else if (a === 'bizpick') pickFiles('.xlsx,.xlsm');
});
$('#p-biz').addEventListener('input', e => { if (e.target.id === 'b-q') { UI.biz.q = e.target.value; UI.biz._f = true; renderBiz(); } });
$('#p-biz').addEventListener('change', e => { if (e.target.id === 'b-code') { UI.biz.code = e.target.value; renderBiz(); } else if (e.target.id === 'b-zero') { UI.biz.hideZero = e.target.checked; renderBiz(); } });
