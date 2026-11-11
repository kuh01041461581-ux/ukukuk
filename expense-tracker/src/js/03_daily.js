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

function welcomeHTML() {
  return `<div class="card" style="text-align:center;padding:40px 20px"><h2 style="font-size:18px">처음 오셨네요 — 파일을 끌어 놓아 시작하세요</h2>
    <p class="sub">기존에 쓰던 <b>엑셀</b>(일상경비 집행현황 · 공공요금 · 통계목별 조회=실집행액)과 <b>합본예산서 PDF</b>를 이 창에 끌어 놓으면 종류를 자동으로 알아보고 채워 줍니다.</p>
    <div class="drop" style="max-width:560px;margin:14px auto"><div style="font-size:15px;font-weight:600">파일을 이 창 아무 곳에나 끌어 놓거나</div>
      <div class="row" style="justify-content:center;margin:10px 0"><button class="btn primary" data-act="pickany">파일 선택 (탐색기 열기)</button><button class="btn" data-act="addline">＋ 직접 행 추가</button></div></div>
    <p class="muted" style="margin:0">일상경비 엑셀은 '사무관리비' 탭, 공공요금 엑셀은 '${esc(S.year)}' 탭이 있으면 됩니다. 여러 파일을 한꺼번에 놓아도 됩니다.</p></div>`;
}
function renderDaily() {
  const P = $('#p-daily');
  if (!S.lines.length) { P.innerHTML = welcomeHTML(); return; } const all = S.lines; const T = groupTotals(all); const el = elapsed();
  const byG = g => all.filter(l => l.group === g);
  const kpi = (k, v, s) => `<div class="kpi"><div class="k">${k}</div><div class="v">${v}</div><div class="s">${s || ''}</div></div>`;
  let h = `
  <div class="row noprint" style="margin-top:16px">
    <button class="btn" data-act="addline">＋ 행 추가</button>
    <button class="btn" data-act="copytsv" data-tip="엑셀·한글에 붙여넣을 수 있게 표를 복사합니다">표 복사</button>
    <button class="btn" data-act="print">인쇄 / PDF 저장</button>
    <span class="spacer"></span>
    <span class="muted">${esc(S.year)}년 · ${esc(S.asOf)} 기준 · 경과율 ${pct1(el)}</span>
  </div>
  <div class="kpis">
    ${kpi('총 예산액', won(T.budget) + '원', `${all.length}개 항목`)}
    ${kpi('지출액', won(T.spent) + '원', `집행률 <b>${pct1(T.rate)}</b>`)}
    ${kpi('지급예정', won(T.sch) + '원', `예정 포함 집행률 ${pct1(T.rateAll)}`)}
    ${kpi('잔액', `<span class="${T.remain < 0 ? 'neg' : ''}">${won(T.remain)}원</span>`, `예정 반영 잔액 ${won(T.remainAfter)}원`)}
  </div>

  <div class="card">
    <h2>집행실적 한눈에</h2>
    <div class="legend"><span><i class="lg-paid"></i>지출액</span><span><i class="lg-sch"></i>지급예정</span><span><i class="lg-rem"></i>잔액</span><span><i class="lg-mark"></i>기준일 경과율 ${pct1(el)}</span></div>`;
  for (const g of GROUPS) {
    const L = byG(g); if (!L.length) continue; const gt = groupTotals(L);
    h += `<div class="chart-group"><span>${g}</span><span class="muted">${won(gt.spent)} / ${won(gt.budget)}원</span></div>
      <div class="chart-row grp"><div class="nm">${g} 합계</div>${barHTML(gt.spent, gt.sch, gt.budget, { lg: true })}
      <div class="st"><b>${pct1(gt.rate)}</b>${gt.sch ? ` (예정 포함 ${pct1(gt.rateAll)})` : ''}<br>잔액 ${won(gt.remain)}</div></div>`;
    for (const l of L) {
      const c = calc(l);
      h += `<div class="chart-row"><div class="nm" title="${esc(l.label)}">${esc(l.label)}${statusBadges(l)}</div>${barHTML(l.spent, c.sch, l.budget)}
        <div class="st"><b>${pct1(c.rate)}</b> · 잔액 ${won(c.remain)}</div></div>`;
    }
  }
  h += `</div>

  <div class="card">
    <h2>지급예정·메모 <span class="muted" style="font-weight:400;font-size:12px">(전체 공통 메모 — 행별 예정 금액은 표의 '지급예정' 칸에서 입력)</span></h2>
    <textarea class="cellin" data-f="gmemo" rows="2" style="width:100%;text-align:left;border-color:var(--line)" placeholder="예: 남중마트 미납 759,690원, 지성솔루션 토너 490,000원">${esc(S.memo)}</textarea>
  </div>

  <div class="card" id="daily-table">
    <h2>${esc(S.year)} 일상경비 집행현황 <span class="muted" style="font-weight:400;font-size:12px">${esc(S.asOf)} 기준 · 지출액·예산·비고 칸을 눌러 바로 수정 (Enter = 다음 칸)</span></h2>
    <div class="tbl-wrap scroll-y"><table class="t">
    <thead><tr><th>연번</th><th>사업명</th><th>총예산액</th><th>분기별(1/4)</th><th>지출액</th><th>지급예정</th><th>잔액</th><th>집행률</th><th>비고</th><th class="noprint"></th></tr></thead><tbody>`;
  for (const g of GROUPS) {
    const L = byG(g); if (!L.length) continue; const gt = groupTotals(L);
    h += `<tr class="grp"><td colspan="2">${g}</td><td class="num">${won(gt.budget)}</td><td class="num">${won(gt.budget / 4)}</td><td class="num">${won(gt.spent)}</td><td class="num">${won(gt.sch)}</td><td class="num ${gt.remain < 0 ? 'neg' : ''}">${won(gt.remain)}</td>
      <td><div class="rate">${barHTML(gt.spent, gt.sch, gt.budget)}<b>${pct1(gt.rate)}</b></div></td><td></td><td class="noprint"></td></tr>`;
    L.forEach((l, i) => {
      const c = calc(l); const e = l.ehojo; const unpaid = e ? Math.max(0, (e.commit || 0) - (e.paid || 0)) : 0;
      const sub = (l.group === '업무추진비' || l.group === '여비') ? `<span class="hint">${esc(l.biz)}</span>` : '';
      h += `<tr>
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
        <td><textarea class="cellin" data-f="note" data-id="${l.id}" rows="1">${esc(l.note)}</textarea>${(l.scheduled || []).length ? `<span class="hint">예정: ${esc(l.scheduled.map(x => x.text + ' ' + won(x.amount)).join(', '))}</span>` : ''}</td>
        <td class="noprint"><button class="btn sm" data-act="editline" data-id="${l.id}" aria-label="행 편집">⚙</button></td></tr>`;
    });
  }
  h += `<tr class="tot"><td colspan="2">합계</td><td class="num">${won(T.budget)}</td><td class="num">${won(T.budget / 4)}</td><td class="num">${won(T.spent)}</td><td class="num">${won(T.sch)}</td><td class="num">${won(T.remain)}</td>
    <td><div class="rate">${barHTML(T.spent, T.sch, T.budget)}<b>${pct1(T.rate)}</b></div></td><td></td><td class="noprint"></td></tr>
  </tbody></table></div></div>`;
  P.innerHTML = h; $$('textarea.cellin', P).forEach(autoGrow); restoreFocus();
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
