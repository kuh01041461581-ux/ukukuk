/* ===== 예산서·집행액 PDF 불러오기 ===== */
const CAND = /^(20101|20102|202\d\d|203\d\d)$/;
function candGroup(code) { return code === '20101' ? '사무관리비' : code === '20102' ? '공공운영비' : code.startsWith('202') ? '여비' : '업무추진비'; }

async function handlePdf(file) {
  const st = $('#imp-status'); const set = t => { if (st) st.textContent = t; };
  try {
    set('PDF를 읽는 중… (큰 파일은 10~20초 걸릴 수 있습니다)');
    const lines = await pdfToLines(file, (p, n) => set(`PDF를 읽는 중… ${p}/${n}쪽`));
    let kind = detectDoc(lines);
    if (kind === 'ledger') { set('통계목별 조회 PDF는 쓰지 않습니다 — 실집행액이 들어 있는 통계목별 조회 "엑셀"을 올려 주세요.'); toast('통계목별 조회는 엑셀로 불러오세요'); return; }
    if (!kind) kind = 'budget';
    if (kind === 'budget') {
      const doc = parseBudgetDoc(lines); doc.file = file.name; UI.imp = UI.imp || {}; UI.imp.budget = doc;
      set(`합본예산서로 인식: 편성목 ${doc.entries.length}건을 찾았습니다.`);
    }
    renderImport(true);
  } catch (e) { console.error(e); set('⚠ ' + (e.message || e)); toast('PDF를 읽지 못했습니다'); }
}

function renderImport(keepStatus) {
  const P = $('#p-import'); const imp = UI.imp || {}; const prev = keepStatus ? ($('#imp-status') || {}).textContent : '';
  let h = `<div class="card"><h2>파일 불러오기 (PDF · 엑셀)</h2>
    <p class="sub" style="margin:0 0 10px">합본예산서(PDF), 통계목별 조회 엑셀(실집행액), 일상경비·공공요금 엑셀을 올리면 자동으로 종류를 알아보고 채워 줍니다. 여러 파일을 한꺼번에 끌어 놓아도 됩니다. 파일은 이 컴퓨터 안에서만 읽으며 어디로도 전송되지 않습니다.</p>
    <ul class="help" style="margin:0 0 12px;padding-left:18px"><li><b>합본예산서</b>(세출예산사업명세서) → <b>총예산액</b>과 공공요금 연 예산 자동 입력</li><li><b>통계목별 조회 엑셀</b>(e-호조 내려받기) → <b>실집행액</b>(지출액)과 원인행위·결의액 자동 입력</li></ul>
    <div class="drop" id="drop"><div style="font-size:15px;font-weight:600">PDF·엑셀 파일을 이 창 아무 곳에나 끌어 놓거나</div><div style="margin:8px 0"><button class="btn primary" data-act="pick">파일 선택 (탐색기 열기)</button></div><div id="imp-status" class="muted">${esc(prev || '')}</div></div></div>`;
  if (imp.budget) h += budgetPreview(imp.budget);
  if (imp.ledger) h += ledgerPreview(imp.ledger);
  P.innerHTML = h;
}

function budgetPreview(doc) {
  const used = new Set(); const ub = utilBudgetFromBudget(doc);
  let rows = '';
  S.lines.forEach(l => {
    const e = findEntry(doc, l); if (e) used.add(e);
    const diff = e ? e.amount - l.budget : 0;
    rows += `<tr><td><input type="checkbox" data-bl="${l.id}" ${e && diff !== 0 ? 'checked' : ''} ${e ? '' : 'disabled'} aria-label="${esc(l.label)} 적용"></td><td>${esc(l.group)}</td><td>${esc(l.label)}<span class="hint">${esc(l.biz)} · ${esc(l.code)}</span></td>
      <td class="num">${won(l.budget)}</td><td class="num">${e ? won(e.amount) : '-'}</td><td class="num ${diff < 0 ? 'neg' : ''}">${e ? (diff ? (diff > 0 ? '+' : '') + won(diff) : '변동 없음') : ''}</td>
      <td>${e ? (diff ? '<span class="badge warn">변경</span>' : '<span class="badge good">일치</span>') : '<span class="badge bad">예산서에서 못 찾음</span>'}</td></tr>`;
  });
  const cands = doc.entries.filter(e => CAND.test(e.code) && !used.has(e));
  let crow = cands.map((e, i) => `<tr><td><input type="checkbox" data-bc="${i}"></td><td>${candGroup(e.code)}</td><td>${esc(e.biz)}<span class="hint">${esc(e.code)} ${esc(e.name)}</span></td><td class="num">${won(e.amount)}</td></tr>`).join('');
  return `<div class="card" id="bprev"><h2>① 합본예산서 결과 <span class="muted" style="font-weight:400;font-size:12px">${esc(doc.file)}${doc.title ? ' · ' + esc(doc.title) : ''}</span></h2>
    <h3>현재 관리 중인 항목</h3><div class="tbl-wrap"><table class="t"><thead><tr><th>적용</th><th>구분</th><th>사업명</th><th>현재 예산</th><th>예산서 금액</th><th>증감</th><th>상태</th></tr></thead><tbody>${rows}</tbody></table></div>
    <h3>공공요금 연 예산 (공공운영비 '문화유적지 보존관리' 산출내역에서 계산)</h3>
    ${ub.found ? `<div class="row">${CATS.map(([k, nm]) => `<label class="row" style="gap:6px"><input type="checkbox" data-bu="${k}" ${ub[k] ? 'checked' : ''}> ${nm} <b>${won(ub[k])}원</b> <span class="muted">(현재 ${S.utilBudget[k] ? won(S.utilBudget[k]) : '미입력'})</span></label>`).join('')}</div>` : '<p class="muted">산출내역을 찾지 못했습니다. 공공요금 탭에서 직접 입력하세요.</p>'}
    <details class="fold" style="margin-top:10px"><summary>관리하지 않던 일상경비 후보 ${cands.length}건 (필요하면 체크해서 추가)</summary>
    <div class="tbl-wrap" style="margin-top:6px"><table class="t"><thead><tr><th>추가</th><th>구분</th><th>세부사업 / 편성목</th><th>예산액</th></tr></thead><tbody>${crow || '<tr><td colspan=4 class="empty">없음</td></tr>'}</tbody></table></div></details>
    <div class="row" style="justify-content:flex-end;margin-top:12px"><button class="btn" data-act="clearb">닫기</button><button class="btn primary" data-act="applyb">선택한 항목 적용</button></div></div>`;
}

function ledgerPreview(doc) {
  const x = true; let rows = '';
  S.lines.forEach(l => {
    const e = findEntry(doc, l); const diff = e ? e.paid - l.spent : 0; const bd = e && e.budget !== l.budget;
    rows += `<tr><td><input type="checkbox" data-ll="${l.id}" ${e && diff !== 0 && x ? 'checked' : ''} ${e ? '' : 'disabled'} aria-label="${esc(l.label)} 적용"></td><td>${esc(l.group)}</td><td>${esc(l.label)}<span class="hint">${esc(l.biz)} · ${esc(l.code)}</span></td>
      <td class="num">${won(l.spent)}</td><td class="num">${e ? won(e.paid) : '-'}</td><td class="num ${diff < 0 ? 'neg' : ''}">${e ? (diff ? (diff > 0 ? '+' : '') + won(diff) : '변동 없음') : ''}</td>
      <td class="num">${e ? won(e.commit) : ''}</td><td class="num">${e ? won(e.resolved) : ''}</td>
      <td>${e ? (bd ? `<span class="badge warn" data-tip="예산현액 ${won(e.budget)}원 / 현재 입력 ${won(l.budget)}원">예산 다름</span>` : '') : '<span class="badge bad">못 찾음</span>'}</td></tr>`;
  });
  return `<div class="card" id="lprev"><h2>② 집행액(통계목별 조회 ${x ? '엑셀' : 'PDF'}) 결과 <span class="muted" style="font-weight:400;font-size:12px">${esc(doc.file)} · 기준일 ${esc(doc.asOf || '?')}${x && doc.skipped ? ` · 이월 ${doc.skipped}행 제외(당해예산만)` : ''}</span></h2>
    ${x ? '<p class="sub" style="margin:0 0 8px">지출액 = 엑셀의 <b>실집행액(집행누계액)</b>입니다. 원인행위(계약)는 했지만 아직 집행되지 않은 금액은 지급예정 입력창에서 한 번에 추가할 수 있습니다.</p>'
      : '<p class="note-box" style="margin:0 0 8px">⚠ PDF에는 <b>실집행액</b>이 없어 <b>지급명령액</b>(실집행액보다 큽니다)만 나옵니다. 그래서 지출액 체크는 꺼 두었습니다. 지출액은 <b>통계목별 조회 엑셀</b>로 불러오세요. (원인행위·결의액 참고용으로만 쓰려면 체크 후 적용)</p>'}
    <div class="tbl-wrap"><table class="t"><thead><tr><th>적용</th><th>구분</th><th>사업명</th><th>현재 지출</th><th>${x ? '실집행액' : '지급명령액(참고)'}</th><th>증감</th><th>원인행위</th><th>결의액</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>
    <div class="row" style="justify-content:space-between;margin-top:12px"><label class="row" style="gap:6px"><input type="checkbox" id="l-bud"> 예산이 다른 항목은 조회된 예산현액으로도 바꾸기</label>
    <span class="row"><button class="btn" data-act="clearl">닫기</button><button class="btn primary" data-act="applyl">선택한 항목 적용</button></span></div></div>`;
}

const PI = $('#p-import');
PI.addEventListener('click', e => {
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act; const imp = UI.imp || {};
  if (a === 'pick') pickFiles('.pdf,.xlsx,.xlsm');
  else if (a === 'clearb') { delete imp.budget; renderImport(); }
  else if (a === 'clearl') { delete imp.ledger; renderImport(); }
  else if (a === 'applyb') {
    const doc = imp.budget; let n = 0;
    $$('[data-bl]:checked', PI).forEach(c => { const l = S.lines.find(x => x.id === c.dataset.bl); const en = findEntry(doc, l); if (en) { l.budget = en.amount; n++; } });
    $$('[data-bu]:checked', PI).forEach(c => { S.utilBudget[c.dataset.bu] = utilBudgetFromBudget(doc)[c.dataset.bu]; n++; });
    const used = new Set(S.lines.map(l => findEntry(doc, l)).filter(Boolean)); const cands = doc.entries.filter(e => CAND.test(e.code) && !used.has(e));
    $$('[data-bc]:checked', PI).forEach(c => { const e = cands[+c.dataset.bc]; const g = candGroup(e.code); S.lines.push({ id: uid('L'), group: g, label: (g === '사무관리비' || g === '공공운영비') ? e.biz : e.name, biz: e.biz, code: e.code, budget: e.amount, spent: 0, note: '', scheduled: [], ehojo: null }); n++; });
    S.lines.sort((x, y) => GROUPS.indexOf(x.group) - GROUPS.indexOf(y.group));
    save(); delete imp.budget; renderImport(); toast(`${n}건을 적용했습니다`);
  } else if (a === 'applyl') {
    const doc = imp.ledger; let n = 0; const withBud = $('#l-bud', PI).checked;
    $$('[data-ll]:checked', PI).forEach(c => {
      const l = S.lines.find(x => x.id === c.dataset.ll); const en = findEntry(doc, l); if (!en) return;
      l.spent = en.paid; l.ehojo = { commit: en.commit, resolved: en.resolved, paid: en.paid, asOf: doc.asOf }; if (withBud && en.budget !== l.budget) l.budget = en.budget; n++;
    });
    if (doc.asOf) { S.asOf = doc.asOf; $('#asof').value = doc.asOf; }
    save(); delete imp.ledger; renderImport(); toast(`지출액 ${n}건을 적용했습니다 (기준일 ${S.asOf})`);
  }
});
