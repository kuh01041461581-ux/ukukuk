/* ===== 일상경비 집행현황 탭 ===== */
function openDlg(html, onMount) {
  const d = $('#dlg'); d.innerHTML = html;
  d.onclose = null;
  if (!d.open) d.showModal();
  onMount && onMount(d);
  return d;
}
function closeDlg() { const d = $('#dlg'); if (d.open) d.close(); }
$('#dlg').addEventListener('click', e => { if (e.target.id === 'dlg') closeDlg(); });

function barHTML(paid, sch, budget, opt = {}) {
  if (!(budget > 0)) return '<div class="bar' + (opt.lg ? ' lg' : '') + '"></div>';
  const scale = Math.max(1, (paid + sch) / budget);
  const pw = Math.max(0, paid) / (budget * scale) * 100, sw = Math.max(0, sch) / (budget * scale) * 100;
  const over = paid > budget;
  const mk = opt.noMark ? '' : `<i class="mark" style="left:${(elapsed() / scale).toFixed(2)}%"></i>`;
  const tip = `지출 ${won(paid)}원 (${pct1(pct(paid, budget))})` + (sch ? `\n지급예정 ${won(sch)}원` : '') +
    `\n잔액 ${won(budget - paid)}원` + (sch ? `\n예정 반영 잔액 ${won(budget - paid - sch)}원 · 집행률 ${pct1(pct(paid + sch, budget))}` : '');
  return `<div class="bar${opt.lg ? ' lg' : ''}" data-tip="${esc(tip)}" role="img" aria-label="${esc(tip.replace(/\n/g, ', '))}">` +
    `<span class="seg paid${over ? ' over' : ''}" style="width:${pw.toFixed(2)}%"></span>` +
    (sch > 0 ? `<span class="seg sch" style="width:${sw.toFixed(2)}%"></span>` : '') + mk + '</div>';
}
function statusBadges(l) {
  const c = calc(l); let h = '';
  if (l.budget > 0 && l.spent > l.budget) h += '<span class="badge bad">초과</span>';
  else if (l.budget > 0 && c.rateAll >= 99.5) h += '<span class="badge good">소진</span>';
  else if (l.budget > 0 && elapsed() - c.rateAll > 20) h += '<span class="badge warn" data-tip="기준일 경과율보다 집행률이 20%p 이상 낮습니다(불용 우려)">집행 저조</span>';
  return h;
}

/* 지금 확인할 것: 중요도 순 */
function dailyAlerts() {
  const el = elapsed(), out = [];
  for (const l of S.lines) {
    if (!(l.budget > 0)) continue; const c = calc(l); const remAfter = c.remainAfter, sch = c.sch;
    if (l.spent + sch > l.budget) out.push({ sev: 3, tag: '초과', title: l.label + ' (' + l.group + ')', detail: `예정까지 반영하면 <b class="neg">${won(l.spent + sch - l.budget)}원 초과</b> · 예산 ${won(l.budget)}원`, act: 'goline', id: l.id, w: l.spent + sch - l.budget });
    else if (remAfter / l.budget <= 0.05) out.push({ sev: 2, tag: '거의 소진', title: l.label + ' (' + l.group + ')', detail: `잔액 <b>${won(remAfter)}원</b> (${pct1(pct(remAfter, l.budget))}) — 집행률 ${pct1(c.rateAll)}`, act: 'goline', id: l.id, w: 1e9 - remAfter });
    else if (el - c.rateAll > 20) out.push({ sev: 1, tag: '집행 저조', title: l.label + ' (' + l.group + ')', detail: `집행률 ${pct1(c.rateAll)} · 경과율 ${pct1(el)} — 잔액 <b>${won(remAfter)}원</b> 불용 우려`, act: 'goline', id: l.id, w: remAfter });
    if (sch > 0) out.push({ sev: 1, tag: '지급예정', title: l.label + ' (' + l.group + ')', detail: `<b>${won(sch)}원</b> — ${esc((l.scheduled || []).map(x => x.text).filter(Boolean).join(', ') || '내용 미입력')}`, act: 'goline', id: l.id, w: sch });
  }
  for (const t of todoSummary()) {
    if (t.unpaid) out.push({ sev: 2, tag: '미납', title: t.nm + ' 미납 ' + t.unpaid + '건', detail: `고지서는 받았지만 납부 전 · 합계 <b>${won(t.amt)}원</b>`, act: 'goutil', cat: t.k, w: t.amt });
    if (t.missing) out.push({ sev: 1, tag: '미수령', title: t.nm + ' 고지서 미수령 ' + t.missing + '건', detail: '지난 달까지 아직 받지 못한 고지서', act: 'goutil', cat: t.k, w: t.missing });
  }
  return out.sort((a, b) => b.sev - a.sev || b.w - a.w).slice(0, 30);
}
function goLine(id) {
  if (UI.dsub !== 'table') { UI.dsub = 'table'; renderDaily(); }
  const tr = document.querySelector(`tr[data-rowid="${id}"]`); if (!tr) return;
  tr.scrollIntoView({ behavior: 'smooth', block: 'center' }); tr.classList.add('flash'); setTimeout(() => tr.classList.remove('flash'), 1800);
}

/* 비고를 '용도' 조각으로 나눠 한눈에: 쉼표·슬래시로 구분, 금액은 굵게 */
function noteChips(text, extraSched) {
  const parts = String(text || '').split(/\s*(?:,\s+|\s\/\s|\n|，|;\s+)\s*/).map(x => x.trim()).filter(Boolean);
  const fmt = t => esc(t).replace(/(\d[\d,]*\s*(?:만원|천원|원))/g, '<b class="amt">$1</b>');
  const chips = parts.map(t => `<span class="nchip">${fmt(t)}</span>`);
  (extraSched || []).forEach(x => chips.push(`<span class="nchip sch">예정 · ${esc(x.text || '')} <b class="amt">${won(x.amount)}원</b></span>`));
  return chips.join('') || '<span class="muted">비고 없음</span>';
}
function goNote(id) {
  UI.dsub = 'table'; UI._noKeep = true; renderDaily();
  const el = document.querySelector(`textarea[data-f=note][data-id="${id}"]`); if (!el) return;
  el.scrollIntoView({ block: 'center' }); el.focus();
  const tr = el.closest('tr'); tr && tr.classList.add('flash'); setTimeout(() => tr && tr.classList.remove('flash'), 1800);
}
/* 용도(비고) 탭: 어디에 쓰는 돈인지 + 잔액 */
function dailyNoteTab() {
  const rows = GROUPS.map(g => {
    const Ls = S.lines.filter(l => l.group === g); if (!Ls.length) return '';
    return `<tr class="grp"><td colspan="3">${g}</td></tr>` + Ls.map(l => { const c = calc(l);
      return `<tr class="clk ${remCls(l)}" data-act="gonote" data-id="${l.id}"><td class="nm2"><b>${esc(l.label)}</b>${(l.group === '업무추진비' || l.group === '여비') ? `<span class="hint">${esc(l.biz)}</span>` : ''}</td>
        <td class="num"><span class="remv2 ${c.remain < 0 ? 'neg' : ''}">${won(c.remain)}</span><span class="hint">예산 ${won(l.budget)} · 지출 ${won(l.spent)}</span></td>
        <td><div class="nchips">${noteChips(l.note, l.scheduled)}</div></td></tr>`; }).join('');
  }).join('');
  return `<div class="card" style="margin-top:12px"><div class="row" style="justify-content:space-between;margin-bottom:6px"><h2 style="margin:0">용도(비고) 한눈에 <span class="muted" style="font-weight:400;font-size:12px">줄을 누르면 비고를 고칠 수 있어요</span></h2></div>
    <div class="tbl-wrap fit fit-note"><table class="t notet"><thead><tr><th style="width:230px">사업명</th><th style="width:170px">잔액</th><th>비고 · 어떤 항목에 쓰는 돈인지</th></tr></thead><tbody>${rows}</tbody></table></div></div>`;
}
function welcomeHTML() {
  return `<div class="card" style="text-align:center;padding:40px 20px"><h2 style="font-size:18px">처음 오셨네요 — 파일을 끌어 놓아 시작하세요</h2>
    <p class="sub">기존에 쓰던 <b>엑셀</b>(일상경비 집행현황 · 공공요금 · 통계목별 조회=실집행액)과 <b>합본예산서 PDF</b>를 이 창에 끌어 놓으면 종류를 자동으로 알아보고 채워 줍니다.</p>
    <div class="drop" style="max-width:560px;margin:14px auto"><div style="font-size:15px;font-weight:600">파일을 이 창 아무 곳에나 끌어 놓거나</div>
      <div class="row" style="justify-content:center;margin:10px 0"><button class="btn primary" data-act="pickany">파일 선택 (탐색기 열기)</button><button class="btn" data-act="addline">＋ 직접 행 추가</button></div></div>
    <p class="muted" style="margin:0">일상경비 엑셀은 '사무관리비' 탭, 공공요금 엑셀은 '${esc(S.year)}' 탭이 있으면 됩니다. 여러 파일을 한꺼번에 놓아도 됩니다.</p></div>`;
}
/* 현황 탭: 잔액을 숫자로 한눈에 */
function remCls(l) { const c = calc(l); if (l.budget <= 0) return ''; if (c.remainAfter < 0) return 'rem-bad'; if (c.remainAfter / l.budget <= 0.05) return 'rem-low'; return ''; }
function dailyNow(T, el, diff, alerts) {
  const rows = GROUPS.map(g => {
    const Ls = S.lines.filter(l => l.group === g); if (!Ls.length) return ''; const gt = groupTotals(Ls);
    return `<tr class="grp"><td>${g}</td><td class="num">${won(gt.budget)}</td><td class="num">${won(gt.spent)}</td><td class="num remv ${gt.remain < 0 ? 'neg' : ''}">${won(gt.remain)}</td><td class="pcol">${pct1(gt.rate)}</td></tr>` +
      Ls.map(l => { const c = calc(l); return `<tr class="clk ${remCls(l)}" data-act="goline" data-id="${l.id}"><td class="nm2">${esc(l.label)}${statusBadges(l)}${c.sch ? `<span class="hint">예정 ${won(c.sch)} → 예정 반영 잔액 ${won(c.remainAfter)}</span>` : ''}</td><td class="num">${won(l.budget)}</td><td class="num">${won(l.spent)}</td><td class="num remv">${won(c.remain)}</td><td class="pcol"><div class="rate">${barHTML(l.spent, c.sch, l.budget)}<b>${pct1(c.rate)}</b></div></td></tr>`; }).join('');
  }).join('');
  return `<div class="now">
    <div class="now-l"><div class="tbl-wrap fit fit-now"><table class="t nowt"><thead><tr><th>사업명</th><th>예산</th><th>지출</th><th>잔액</th><th>집행률</th></tr></thead><tbody>${rows}
      <tr class="tot"><td>전체 합계</td><td class="num">${won(T.budget)}</td><td class="num">${won(T.spent)}</td><td class="num remv">${won(T.remain)}</td><td class="pcol">${pct1(T.rate)}</td></tr></tbody></table></div></div>
    <div class="now-r">
      <div class="kpi bigrem"><div class="k">전체 남은 예산 (잔액)</div><div class="v ${T.remain < 0 ? 'neg' : ''}">${won(T.remain)}<small>원</small></div>
        <div class="s">지급예정 ${won(T.sch)}원 반영 후 <b>${won(T.remainAfter)}원</b></div>
        <div class="row" style="gap:8px;margin-top:8px"><span>집행률 <b>${pct1(T.rate)}</b></span><span class="pace ${diff < -10 ? 'slow' : diff > 5 ? 'fast' : 'ok'}">경과율 ${pct1(el)} 대비 ${diff >= 0 ? '▲' : '▼'} ${Math.abs(diff).toFixed(1)}%p</span></div></div>
      <div class="card alerts" style="margin-top:12px"><h2>지금 확인할 것 <span class="muted" style="font-weight:400;font-size:12px">${alerts.length ? alerts.length + '건' : ''}</span></h2>
        ${alerts.length ? alerts.slice(0, UI.alertsAll ? 99 : 4).map(al => `<div class="alert sev${al.sev}"><span class="badge ${al.sev >= 3 ? 'bad' : al.sev === 2 ? 'warn' : 'gray'}">${al.tag}</span><div class="al-main"><b>${esc(al.title)}</b><span class="al-d">${al.detail}</span></div><button class="btn sm" data-act="${al.act}" data-id="${al.id || ''}" data-cat="${al.cat || ''}">보기</button></div>`).join('') + (alerts.length > 4 ? `<button class="btn sm noprint" style="margin-top:4px" data-act="alertsall">${UI.alertsAll ? '접기' : '더보기 (' + (alerts.length - 5) + '건)'}</button>` : '') : '<div class="empty" style="padding:10px">✓ 지금 확인할 항목이 없습니다</div>'}</div>
      <div class="card" style="margin-top:12px;padding:12px 16px"><h2 style="margin-bottom:6px">지급예정·메모</h2><textarea class="cellin" data-f="gmemo" rows="1" style="width:100%;text-align:left;border-color:var(--edge-dark)" placeholder="예: 남중마트 미납 759,690원, 지성솔루션 토너 490,000원">${esc(S.memo)}</textarea></div>
    </div></div>`;
}
/* 그래프 탭 */
function dailyChartTab(T, el) {
  const byG = g => S.lines.filter(l => l.group === g);
  let h = `<div class="gcards" style="margin-top:0">${GROUPS.map(g => {
    const Ls = byG(g); if (!Ls.length) return ''; const gt = groupTotals(Ls); const gd = gt.rate - el;
    return `<div class="kpi gcard"><div class="row" style="justify-content:space-between"><b>${g}</b><span class="pace ${gd < -10 ? 'slow' : gd > 5 ? 'fast' : 'ok'}" data-tip="기준일 경과율(${pct1(el)})과의 차이">${gd >= 0 ? '▲' : '▼'} ${Math.abs(gd).toFixed(0)}%p</span></div>
      <div class="g-v">${pct1(gt.rate)}</div>${barHTML(gt.spent, gt.sch, gt.budget)}
      <div class="g-s"><span>지출 ${won(gt.spent)}</span><span>예산 ${won(gt.budget)}</span></div>
      <div class="g-r ${gt.remain < 0 ? 'neg' : ''}">잔액 <b>${won(gt.remain)}원</b>${gt.sch ? `<span class="muted"> · 예정 반영 ${won(gt.remainAfter)}</span>` : ''}</div></div>`;
  }).join('')}</div>
  <div class="card">
    <div class="row" style="justify-content:space-between;margin-bottom:6px"><h2 style="margin:0">항목별 집행률</h2>
      <div class="pills noprint" role="group" aria-label="정렬">${[['group', '구분별'], ['low', '집행률 낮은 순'], ['rem', '잔액 적은 순']].map(([k, n]) => `<button class="pill" aria-pressed="${UI.dailySort === k}" data-act="sortd" data-v="${k}">${n}</button>`).join('')}</div></div>
    <div class="legend"><span><i class="lg-paid"></i>지출액</span><span><i class="lg-sch"></i>지급예정</span><span><i class="lg-rem"></i>잔액</span><span><i class="lg-mark"></i>기준일 경과율 ${pct1(el)}</span></div>`;
  const chartRow = (l, tag) => { const c = calc(l); const bad = l.budget > 0 && c.rateAll >= 100, low = !bad && l.budget > 0 && el - c.rateAll > 20;
    return `<div class="chart-row clickable ${bad ? 'is-bad' : low ? 'is-low' : ''}" data-act="goline" data-id="${l.id}"><div class="nm" title="${esc(l.label)}">${tag ? `<span class="gtag">${esc(l.group)}</span>` : ''}${esc(l.label)}${statusBadges(l)}</div>${barHTML(l.spent, c.sch, l.budget)}
      <div class="st"><b class="pct ${bad ? 'neg' : ''}">${pct1(c.rate)}</b><span class="rem">잔액 ${won(c.remain)}</span></div></div>`; };
  if (UI.dailySort === 'group') {
    for (const g of GROUPS) { const Ls = byG(g); if (!Ls.length) continue; const gt = groupTotals(Ls);
      h += `<div class="chart-group"><span>${g}</span><span class="muted">지출 ${won(gt.spent)} / 예산 ${won(gt.budget)}원 · <b>${pct1(gt.rate)}</b></span></div>` + Ls.map(l => chartRow(l, false)).join(''); }
  } else {
    const key = UI.dailySort === 'low' ? l => calc(l).rateAll : l => calc(l).remainAfter;
    h += [...S.lines].sort((x, y) => key(x) - key(y)).map(l => chartRow(l, true)).join('');
  }
  return h + '</div>';
}
function renderDaily() {
  const P = $('#p-daily'); const snap = scrollSnap(P);
  if (!S.lines.length) { P.innerHTML = welcomeHTML(); return; }
  const T = groupTotals(S.lines), el = elapsed(), diff = T.rate - el;
  UI.dailySort = UI.dailySort || 'group'; UI.dsub = UI.dsub || 'now'; UI.alertsAll = !!UI.alertsAll;
  const SUBS = [['now', '현황 · 잔액'], ['note', '용도(비고)'], ['chart', '그래프'], ['table', '입력 표']];
  let h = `<div class="row noprint subbar">
    <div class="pills subtabs" role="tablist">${SUBS.map(([k, n]) => `<button class="pill" role="tab" aria-pressed="${UI.dsub === k}" data-act="dsub" data-v="${k}">${n}</button>`).join('')}</div>
    <span class="spacer"></span><span class="muted">${esc(S.year)}년 · ${esc(S.asOf)} 기준</span>
    <button class="btn sm" data-act="addline">＋ 행 추가</button><button class="btn sm" data-act="copytsv" data-tip="엑셀·한글에 붙여넣을 수 있게 표를 복사합니다">표 복사</button><button class="btn sm" data-act="print">인쇄</button>
  </div>`;
  if (UI.dsub === 'now') h += dailyNow(T, el, diff, dailyAlerts());
  else if (UI.dsub === 'note') h += dailyNoteTab();
  else if (UI.dsub === 'chart') h += dailyChartTab(T, el);
  else h += dailyTable();
  P.innerHTML = h; $$('textarea.cellin', P).forEach(autoGrow); scrollRestore(P, snap); restoreFocus();
}

function dailyTable() {
  const all = S.lines, T = groupTotals(all), byG = g => all.filter(l => l.group === g);
  let h = `<div class="card" id="daily-table">
    <h2>${esc(S.year)} 일상경비 집행현황 <span class="muted" style="font-weight:400;font-size:12px">${esc(S.asOf)} 기준 · 지출액·예산·비고 칸을 눌러 바로 수정 (Enter = 다음 칸)</span></h2>
    <div class="tbl-wrap fit fit-d"><table class="t">
    <thead><tr><th>연번</th><th>사업명</th><th>총예산액</th><th>분기별(1/4)</th><th>지출액</th><th>지급예정</th><th>잔액</th><th>집행률</th><th>비고</th><th class="noprint"></th></tr></thead><tbody>`;
  for (const g of GROUPS) {
    const L = byG(g); if (!L.length) continue; const gt = groupTotals(L);
    h += `<tr class="grp"><td colspan="2">${g}</td><td class="num">${won(gt.budget)}</td><td class="num">${won(gt.budget / 4)}</td><td class="num">${won(gt.spent)}</td><td class="num">${won(gt.sch)}</td><td class="num ${gt.remain < 0 ? 'neg' : ''}">${won(gt.remain)}</td>
      <td><div class="rate">${barHTML(gt.spent, gt.sch, gt.budget)}<b>${pct1(gt.rate)}</b></div></td><td></td><td class="noprint"></td></tr>`;
    L.forEach((l, i) => {
      const c = calc(l); const e = l.ehojo; const unpaid = e ? Math.max(0, (e.commit || 0) - (e.paid || 0)) : 0;
      const sub = (l.group === '업무추진비' || l.group === '여비') ? `<span class="hint">${esc(l.biz)}</span>` : '';
      h += `<tr data-rowid="${l.id}">
        <td style="text-align:center">${i + 1}</td>
        <td class="name">${esc(l.label)}${statusBadges(l)}${sub}</td>
        <td><input class="cellin" data-f="budget" data-id="${l.id}" data-nav="b-${l.id}" value="${won(l.budget)}" inputmode="numeric"></td>
        <td class="num">${won(l.budget / 4)}</td>
        <td><input class="cellin" data-f="spent" data-id="${l.id}" data-nav="s-${l.id}" value="${won(l.spent)}" inputmode="numeric" aria-label="${esc(l.label)} 지출액">
            ${e ? `<span class="hint num" data-tip="e-호조 ${esc(e.asOf || '')} 기준\n원인행위 ${won(e.commit)}\n결의 ${won(e.resolved)}\n지출 ${won(e.paid)}">e-호조 ${esc((e.asOf || '').slice(5))}</span>` : ''}</td>
        <td class="num"><div class="sch-cell">${c.sch ? won(c.sch) : '<span class="muted">-</span>'}<button class="btn sm noprint" data-act="sch" data-id="${l.id}">${(l.scheduled || []).length ? '✎' : '＋'}</button></div>
            ${unpaid ? `<span class="hint" data-tip="원인행위(계약)는 했지만 아직 지출되지 않은 금액입니다">미지출 계약 ${won(unpaid)}</span>` : ''}</td>
        <td class="num ${c.remain < 0 ? 'neg' : ''}">${won(c.remain)}${c.sch ? `<span class="hint">예정 반영 ${won(c.remainAfter)}</span>` : ''}</td>
        <td><div class="rate">${barHTML(l.spent, c.sch, l.budget)}<b>${pct1(c.rate)}</b></div></td>
        <td class="notecol"><div class="nchips sm">${noteChips(l.note, [])}</div><textarea class="cellin" data-f="note" data-id="${l.id}" rows="1" placeholder="비고 (어떤 항목에 쓰는지)">${esc(l.note)}</textarea>${(l.scheduled || []).length ? `<span class="hint">예정: ${esc(l.scheduled.map(x => x.text + ' ' + won(x.amount)).join(', '))}</span>` : ''}</td>
        <td class="noprint"><button class="btn sm" data-act="editline" data-id="${l.id}" aria-label="행 편집">⚙</button></td></tr>`;
    });
  }
  h += `<tr class="tot"><td colspan="2">합계</td><td class="num">${won(T.budget)}</td><td class="num">${won(T.budget / 4)}</td><td class="num">${won(T.spent)}</td><td class="num">${won(T.sch)}</td><td class="num">${won(T.remain)}</td>
    <td><div class="rate">${barHTML(T.spent, T.sch, T.budget)}<b>${pct1(T.rate)}</b></div></td><td></td><td class="noprint"></td></tr>
  </tbody></table></div></div>`;
  return h;
}

/* 입력 처리 */
$('#p-daily').addEventListener('change', e => {
  const t = e.target, f = t.dataset.f; if (!f) return;
  if (f === 'gmemo') { S.memo = t.value; save(); return; }
  const l = S.lines.find(x => x.id === t.dataset.id); if (!l) return;
  if (f === 'note') { l.note = t.value; save(); autoGrow(t); return; }
  const v = parseNum(t.value);
  if (v === null || Number.isNaN(v)) { if (t.value.trim() !== '') toast('숫자나 계산식(예: 1,000,000+250,000)만 입력할 수 있습니다'); renderDaily(); return; }
  l[f] = v; save(); renderDaily();
});
$('#p-daily').addEventListener('keydown', e => { if (e.target.matches('input.cellin')) enterNext(e, e.target.dataset.nav.slice(0, 2)); });
$('#p-daily').addEventListener('input', e => { if (e.target.matches('textarea.cellin')) autoGrow(e.target); });
$('#p-daily').addEventListener('focusin', e => { if (e.target.matches('input.cellin')) { const v = parseNum(e.target.value); e.target.value = v == null || isNaN(v) ? '' : String(v); e.target.select(); } });
$('#p-daily').addEventListener('click', e => {
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act;
  if (a === 'sch') schDialog(b.dataset.id);
  else if (a === 'editline') lineDialog(b.dataset.id);
  else if (a === 'addline') lineDialog(null);
  else if (a === 'pickany') pickFiles('.xlsx,.xlsm,.pdf');
  else if (a === 'dsub') { UI.dsub = b.dataset.v; UI._noKeep = true; renderDaily(); window.scrollTo(0, 0); }
  else if (a === 'alertsall') { UI.alertsAll = !UI.alertsAll; renderDaily(); }
  else if (a === 'sortd') { UI.dailySort = b.dataset.v; UI._noKeep = true; renderDaily(); }
  else if (a === 'goline') goLine(b.dataset.id);
  else if (a === 'gonote') goNote(b.dataset.id);
  else if (a === 'goutil') { const u = UI.util; u.cat = b.dataset.cat; u.sub = 'pay'; u.filter = 'todo'; u.q = ''; u.month = defaultMonth(S.util[u.cat]); showTab('util'); }
  else if (a === 'copytsv') copyText(dailyTSV(), '표를 복사했습니다 — 엑셀·한글에 붙여넣으세요');
  else if (a === 'print') { $$('.tab-panel').forEach(p => p.classList.toggle('printing', p.id === 'p-daily')); window.print(); }
});

function dailyTSV() {
  const rows = [['구분', '연번', '사업명', '총예산액', '분기별', '지출액', '지급예정', '잔액', '집행률', '비고']];
  for (const g of GROUPS) {
    const L = S.lines.filter(l => l.group === g); if (!L.length) continue; const gt = groupTotals(L);
    rows.push([g, '', '', gt.budget, Math.round(gt.budget / 4), gt.spent, gt.sch, gt.remain, pct1(gt.rate), '']);
    L.forEach((l, i) => { const c = calc(l); rows.push([g, i + 1, l.label, l.budget, Math.round(l.budget / 4), l.spent, c.sch, c.remain, pct1(c.rate), l.note.replace(/\s+/g, ' ')]); });
  }
  const T = groupTotals(S.lines); rows.push(['합계', '', '', T.budget, Math.round(T.budget / 4), T.spent, T.sch, T.remain, pct1(T.rate), '']);
  return rows.map(r => r.join('\t')).join('\n');
}

/* 지급예정 입력창 */
function schDialog(id) {
  const l = S.lines.find(x => x.id === id); const list = deepCopy(l.scheduled || []);
  const e = l.ehojo; const unpaid = e ? Math.max(0, (e.commit || 0) - (e.paid || 0)) : 0;
  const draw = () => {
    openDlg(`<h3>${esc(l.label)} <span class="muted" style="font-size:12px">(${esc(l.group)})</span> — 지급예정</h3>
      <p class="muted" style="margin:0 0 10px">아직 지출하지 않았지만 곧 지급할 금액을 적어 두세요. 잔액·집행률에 '예정 반영'으로 따로 표시됩니다.</p>
      ${unpaid ? `<p class="note-box">e-호조에 원인행위(계약)는 되어 있으나 아직 지출되지 않은 금액: <b>${won(unpaid)}원</b> <button class="btn sm" data-d="fillunpaid">예정으로 추가</button></p>` : ''}
      <div id="sl">${list.map((x, i) => `<div class="sch-row"><input data-i="${i}" data-k="text" value="${esc(x.text)}" placeholder="내용 (예: 남중마트 청소용품)"><input data-i="${i}" data-k="amount" class="num" value="${won(x.amount)}" inputmode="numeric" placeholder="금액">
        <button class="btn sm" data-d="paid" data-i="${i}" data-tip="지출액에 더하고 예정에서 뺍니다">지출로 반영</button><button class="btn sm danger" data-d="del" data-i="${i}" aria-label="삭제">✕</button></div>`).join('') || '<div class="empty">등록된 지급예정이 없습니다</div>'}</div>
      <div class="row" style="justify-content:space-between;margin-top:10px"><button class="btn" data-d="add">＋ 항목 추가</button><span>합계 <b>${won(sum(list, x => x.amount))}원</b></span></div>
      <div class="row" style="justify-content:flex-end;margin-top:14px"><button class="btn" data-d="cancel">닫기</button><button class="btn primary" data-d="save">저장</button></div>`, d => {
      d.onclick = ev => {
        const b = ev.target.closest('[data-d]'); if (!b) return; const k = b.dataset.d, i = +b.dataset.i; sync(d);
        if (k === 'add') { list.push({ id: uid('S'), text: '', amount: 0 }); draw(); }
        else if (k === 'del') { list.splice(i, 1); draw(); }
        else if (k === 'paid') { l.spent += list[i].amount || 0; toast(`지출액에 ${won(list[i].amount)}원을 더했습니다`); list.splice(i, 1); l.scheduled = list; save(); closeDlg(); renderDaily(); }
        else if (k === 'fillunpaid') { list.push({ id: uid('S'), text: 'e-호조 미지출 계약분', amount: unpaid }); draw(); }
        else if (k === 'cancel') closeDlg();
        else if (k === 'save') { l.scheduled = list.filter(x => x.text.trim() || x.amount); save(); closeDlg(); renderDaily(); }
      };
    });
  };
  const sync = d => $$('#sl input', d).forEach(inp => { const it = list[+inp.dataset.i]; if (!it) return; if (inp.dataset.k === 'text') it.text = inp.value; else { const v = parseNum(inp.value); it.amount = v && !isNaN(v) ? v : 0; } });
  draw();
}

/* 행 추가/수정 */
function lineDialog(id) {
  const isNew = !id; const l = isNew ? { id: uid('L'), group: '사무관리비', label: '', biz: '', code: '20101', budget: 0, spent: 0, note: '', scheduled: [], ehojo: null } : S.lines.find(x => x.id === id);
  openDlg(`<h3>${isNew ? '행 추가' : '행 편집'}</h3>
    <label class="fld">구분<select id="lg">${GROUPS.map(g => `<option ${g === l.group ? 'selected' : ''}>${g}</option>`).join('')}</select></label>
    <label class="fld">표시 이름 (표·그래프에 보이는 사업명)<input id="ll" value="${esc(l.label)}"></label>
    <label class="fld">예산서의 세부사업명 (예산서 불러오기 때 이 이름으로 찾습니다)<input id="lb" value="${esc(l.biz)}"></label>
    <label class="fld">편성목 코드 5자리 (사무관리비 20101 · 공공운영비 20102 · 시책추진 20303 · 부서운영 20304 · 국내여비 20201)<input id="lc" value="${esc(l.code)}" maxlength="5"></label>
    <label class="fld">총예산액(원)<input id="lm" value="${won(l.budget)}" inputmode="numeric"></label>
    <div class="row" style="justify-content:space-between;margin-top:14px"><span>${isNew ? '' : '<button class="btn danger" data-d="del">이 행 삭제</button>'}</span>
    <span class="row"><button class="btn" data-d="cancel">취소</button><button class="btn primary" data-d="save">저장</button></span></div>`, d => {
    d.onclick = ev => {
      const b = ev.target.closest('[data-d]'); if (!b) return; const k = b.dataset.d;
      if (k === 'cancel') closeDlg();
      else if (k === 'del') { if (confirm(`'${l.label}' 행을 삭제할까요? (지출·예정·비고도 함께 삭제됩니다)`)) { S.lines = S.lines.filter(x => x.id !== l.id); save(); closeDlg(); renderDaily(); } }
      else if (k === 'save') {
        l.group = $('#lg', d).value; l.label = $('#ll', d).value.trim() || '(이름 없음)'; l.biz = $('#lb', d).value.trim() || l.label; l.code = $('#lc', d).value.trim();
        const m = parseNum($('#lm', d).value); l.budget = m && !isNaN(m) ? m : 0;
        if (isNew) S.lines.push(l);
        S.lines.sort((a, b) => GROUPS.indexOf(a.group) - GROUPS.indexOf(b.group));  // 안정 정렬: 구분별로 모음
        save(); closeDlg(); renderDaily();
      }
    };
  });
}
