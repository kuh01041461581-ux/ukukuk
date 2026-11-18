/* ===== 파일 열기: 탐색기 선택 + 끌어놓기 (엑셀·PDF 공통) ===== */
function pickFiles(accept, intent) {
  const p = $('#pick'); p.accept = accept; p.multiple = true; p.value = '';
  p.onchange = () => { const fs = [...p.files]; p.value = ''; if (fs.length) handleFiles(fs, intent); };
  p.click();
}
function fileToBase64(file) {
  return new Promise((res, rej) => {
    const r = new FileReader(); r.onerror = () => rej(new Error('파일을 읽지 못했습니다')); r.onload = () => res(String(r.result).split(',')[1] || ''); r.readAsDataURL(file);
  });
}
async function handleFiles(files, intent) {
  const pdfs = files.filter(f => /\.pdf$/i.test(f.name)), xls = files.filter(f => /\.xls[xm]$/i.test(f.name));
  const other = files.length - pdfs.length - xls.length;
  if (other) toast('엑셀(.xlsx)과 PDF만 읽을 수 있습니다 — 건너뛴 파일 ' + other + '개');
  const forms = [];
  for (const f of xls) { const res = await importExcelFile(f, intent); if (res && res.form) forms.push(res); }
  const isTx = f => intent === 'tx' || (intent !== 'form' && looksLikeTx(f.form));
  const txs = forms.filter(isTx), pays = forms.filter(f => !isTx(f));
  if (txs.length) openTxImport(txs.flatMap(f => f.form.map(r => ({ ...r, file: f.file }))), txs.map(f => f.file).join(', '));
  else if (pays.length) { const tp = pays.find(f => isTplForm(f.form)); openPayForm(pays.flatMap(f => f.form.map(r => ({ ...r, file: f.file }))), pays.map(f => f.file).join(', '), tp && tp.b64); }
  if (pdfs.length) {
    showTab('import');
    for (const f of pdfs) await handlePdf(f);     // 여러 PDF는 종류별로 각각 미리보기에 쌓임
  }
}
function looksLikeTx(rows) {
  const h = ((rows[0] && rows[0].header) || []).map(hnorm).join('|');
  return /세부사업|통계목/.test(h) && TX_RE.amt.test(h) && TX_RE.date.test(h);
}
async function importExcelFile(file, intent) {
  if (!IS_EXE) { toast('엑셀 읽기는 exe 프로그램에서만 됩니다'); return; }
  try {
    toast('엑셀을 읽는 중… ' + file.name);
    const b64 = await fileToBase64(file);
    let opts = null;
    if (intent !== 'form' && intent !== 'tx') {   // 탭이 여러 개인 엑셀: 어느 탭을 읽을지 고르게 함
      try {
        const ins = await api().inspect_xlsx(file.name, b64, S.year);
        if (ins && !ins.error && ins.sheets.length > 1 && ins.sheets.some(x => x.kinds.length)) { opts = await pickTabs(file.name, ins); if (!opts) return; }
      } catch (e) { /* 검사에 실패하면 예전처럼 자동으로 읽음 */ }
    }
    const r = await api().import_xlsx_data(file.name, b64, S.year, (intent === 'form' || intent === 'tx') ? 'form' : 'auto', opts || undefined);
    if (r.error) { alert(r.error); return; }
    if (r.form) return { form: r.form, file: file.name, b64 };
    if (r.ledger) {
      const d = new Date(file.lastModified || Date.now());
      const asOf = d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
      if (!(await checkDept(r.ledger.dept))) return;
      S.biz = { asOf, file: file.name, entries: r.ledger.entries }; save();   // 전체 사업 진척용(설정에서 켜면 보임)
      if (!S.lines.length) {   // 새 장부: 일상경비 항목을 자동으로 만든다
        const made = linesFromEntries(r.ledger.entries, asOf);
        if (made.length) { S.lines = made; S.asOf = asOf; $('#asof').value = asOf; save(); UI.dsub = 'now'; showTab('daily'); toast(`${S.dept || ''} 일상경비 ${made.length}개 항목을 만들었습니다 — 안 쓰는 항목은 숨겨 주세요`); setTimeout(listDialog, 300); return; }
      }
      UI.imp = UI.imp || {}; UI.imp.ledger = { kind: 'ledger', entries: r.ledger.entries, asOf, file: file.name, src: 'xlsx', skipped: r.ledger.skipped };
      showTab('import'); toast('통계목별 조회 엑셀(실집행액)을 읽었습니다 — 아래에서 적용할 항목을 확인하세요'); return;
    }
    const parts = [];
    if (r.lines) {
      if (S.lines.length && !confirm(`'${file.name}'의 일상경비(${r.lines.length}개 항목)로 지금 입력된 일상경비를 바꿀까요?\n(지출액·예정·비고가 엑셀 값으로 바뀝니다)`)) { /* 건너뜀 */ }
      else { await safetyBackup('가져오기전'); S.lines = r.lines; S.memo = r.memo || ''; if (r.asOf) { S.asOf = r.asOf; $('#asof').value = S.asOf; } parts.push(`일상경비 ${r.lines.length}개 항목`); }
    }
    if (r.util) {
      const had = Object.values(S.util).some(x => x.items && x.items.length);
      if (had && !confirm(`'${file.name}'의 공공요금으로 지금 입력된 공공요금(번호·월별 금액·수령 체크)을 바꿀까요?`)) { /* 건너뜀 */ }
      else { await safetyBackup('가져오기전'); S.util = r.util; for (const k in S.util) ensurePaid(S.util[k]); UI.util.month = null; parts.push('공공요금(전기·상하수도·통신)'); }
    }
    if (parts.length) { save(); showTab(UI.tab === 'import' || UI.tab === 'settings' ? 'daily' : UI.tab); toast(parts.join(' · ') + '을 가져왔습니다'); }
  } catch (e) { alert('엑셀을 읽지 못했습니다: ' + (e.message || e)); }
}

/* 창 아무 곳에나 끌어놓기 */
(function () {
  const ov = document.createElement('div');
  ov.id = 'dropov'; ov.hidden = true; ov.innerHTML = '<div><b>여기에 놓으세요</b><span>엑셀(.xlsx) · PDF 파일</span></div>';
  document.body.appendChild(ov);
  let depth = 0;
  const hasFiles = e => e.dataTransfer && [...(e.dataTransfer.types || [])].includes('Files');
  window.addEventListener('dragenter', e => { if (!hasFiles(e)) return; e.preventDefault(); depth++; ov.hidden = false; });
  window.addEventListener('dragover', e => { if (hasFiles(e)) { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; } });
  window.addEventListener('dragleave', e => { if (!hasFiles(e)) return; depth = Math.max(0, depth - 1); if (!depth) ov.hidden = true; });
  window.addEventListener('drop', e => {
    e.preventDefault(); depth = 0; ov.hidden = true;
    const fs = [...(e.dataTransfer ? e.dataTransfer.files : [])]; if (fs.length) handleFiles(fs);
  });
})();

/* 덮어쓰기 직전 자동 백업 (백업 폴더에 따로 한 벌 남김) */
async function safetyBackup(label) {
  if (!IS_EXE) return;
  try { await api().save_state(JSON.stringify(S)); await api().snapshot(label); toast('덮어쓰기 전 상태를 백업해 두었습니다'); } catch (e) { /* 백업 실패는 진행을 막지 않음 */ }
}
/* 엑셀 탭 고르기: 역할별로 어느 탭을 읽을지 지정 */
function pickTabs(fileName, ins) {
  return new Promise(resolve => {
    const kindOf = n => (ins.sheets.find(x => x.name === n) || { kinds: [] }).kinds;
    const has = k => ins.sheets.some(x => x.kinds.includes(k));
    const sel = (id, kind, def) => `<select id="${id}" class="fsel" style="min-width:230px"><option value="">(읽지 않음)</option>${ins.sheets.map(x => `<option value="${esc(x.name)}" ${x.name === def ? 'selected' : ''}>${esc(x.name)}${x.kinds.includes(kind) ? '  ★ 읽을 수 있음' : ''}</option>`).join('')}</select>`;
    const row = (lbl, hint, html) => `<tr><td style="padding:8px 10px"><b>${lbl}</b><div class="muted" style="font-size:12px">${hint}</div></td><td style="padding:8px 10px">${html}</td></tr>`;
    let done = false;
    openDlg(`<h3>읽을 탭 고르기 <span class="muted" style="font-weight:400;font-size:12px">${esc(fileName)}</span></h3>
      <p class="sub" style="margin:0 0 8px">이 엑셀에는 탭이 ${ins.sheets.length}개 있습니다. 용도별로 어느 탭을 읽을지 정해 주세요. ★은 그 용도로 읽을 수 있는 탭이고, 미리 가장 알맞은 탭을 골라 두었습니다.</p>
      <table class="t" style="width:100%"><tbody>
      ${has('util') ? row('공공요금 탭', '전기·상하수도·통신 표가 있는 탭 (연도 이름 탭)', sel('tp-util', 'util', ins.default.util)) + row('전년도 비교 탭', '“전년 같은 달”을 채울 탭 (선택)', sel('tp-prev', 'util', ins.default.prev)) : ''}
      ${has('daily') ? row('일상경비 탭', '사무관리비·공공운영비… 집행현황 표', sel('tp-daily', 'daily', ins.default.daily)) : ''}
      ${has('ledger') ? row('통계목별 조회 탭', '실집행액(집행누계액)이 있는 표', sel('tp-ledger', 'ledger', ins.default.ledger)) : ''}
      </tbody></table>
      <div class="row" style="justify-content:flex-end;margin-top:12px"><button class="btn" data-tp="cancel">취소</button><button class="btn primary" data-tp="ok">이 탭으로 불러오기</button></div>`, d => {
      const fin = v => { if (done) return; done = true; closeDlg(); resolve(v); };
      d.onclose = () => { if (!done) { done = true; resolve(null); } };
      d.onclick = ev => {
        const b = ev.target.closest('[data-tp]'); if (!b) return;
        if (b.dataset.tp === 'cancel') { fin(null); return; }
        const g = id => { const e = $('#' + id, d); return e ? e.value : ''; };
        const o = { util: g('tp-util'), prev: g('tp-prev'), daily: g('tp-daily'), ledger: g('tp-ledger') };
        if (!o.util && !o.daily && !o.ledger) { toast('읽을 탭을 하나 이상 선택하세요'); return; }
        fin(o);
      };
    });
  });
}
