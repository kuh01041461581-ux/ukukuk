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
  else if (pays.length) openPayForm(pays.flatMap(f => f.form.map(r => ({ ...r, file: f.file }))), pays.map(f => f.file).join(', '));
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
    const r = await api().import_xlsx_data(file.name, await fileToBase64(file), S.year, (intent === 'form' || intent === 'tx') ? 'form' : 'auto');
    if (r.error) { alert(r.error); return; }
    if (r.form) return { form: r.form, file: file.name };
    if (r.ledger) {
      const d = new Date(file.lastModified || Date.now());
      const asOf = d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
      if (!(await checkDept(r.ledger.dept))) return;
      S.biz = { asOf, file: file.name, entries: r.ledger.entries }; save();   // 전체 사업 진척용(설정에서 켜면 보임)
      if (!S.lines.length) {   // 새 장부: 일상경비 항목을 자동으로 만든다
        const made = linesFromEntries(r.ledger.entries, asOf);
        if (made.length) { S.lines = made; S.asOf = asOf; $('#asof').value = asOf; save(); UI.dsub = 'now'; showTab('daily'); toast(`${S.dept || ''} 일상경비 ${made.length}개 항목을 만들었습니다 (예산·실집행액 포함)`); return; }
      }
      UI.imp = UI.imp || {}; UI.imp.ledger = { kind: 'ledger', entries: r.ledger.entries, asOf, file: file.name, src: 'xlsx', skipped: r.ledger.skipped };
      showTab('import'); toast('통계목별 조회 엑셀(실집행액)을 읽었습니다 — 아래에서 적용할 항목을 확인하세요'); return;
    }
    const parts = [];
    if (r.lines) {
      if (S.lines.length && !confirm(`'${file.name}'의 일상경비(${r.lines.length}개 항목)로 지금 입력된 일상경비를 바꿀까요?\n(지출액·예정·비고가 엑셀 값으로 바뀝니다)`)) { /* 건너뜀 */ }
      else { S.lines = r.lines; S.memo = r.memo || ''; if (r.asOf) { S.asOf = r.asOf; $('#asof').value = S.asOf; } parts.push(`일상경비 ${r.lines.length}개 항목`); }
    }
    if (r.util) {
      const had = Object.values(S.util).some(x => x.items && x.items.length);
      if (had && !confirm(`'${file.name}'의 공공요금으로 지금 입력된 공공요금(번호·월별 금액·수령 체크)을 바꿀까요?`)) { /* 건너뜀 */ }
      else { S.util = r.util; for (const k in S.util) ensurePaid(S.util[k]); UI.util.month = null; parts.push('공공요금(전기·상하수도·통신)'); }
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
