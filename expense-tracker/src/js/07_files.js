/* ===== 파일 열기: 탐색기 선택 + 끌어놓기 (엑셀·PDF 공통) ===== */
function pickFiles(accept) {
  const p = $('#pick'); p.accept = accept; p.multiple = true; p.value = '';
  p.onchange = () => { const fs = [...p.files]; p.value = ''; if (fs.length) handleFiles(fs); };
  p.click();
}
function fileToBase64(file) {
  return new Promise((res, rej) => {
    const r = new FileReader(); r.onerror = () => rej(new Error('파일을 읽지 못했습니다')); r.onload = () => res(String(r.result).split(',')[1] || ''); r.readAsDataURL(file);
  });
}
async function handleFiles(files) {
  const pdfs = files.filter(f => /\.pdf$/i.test(f.name)), xls = files.filter(f => /\.xls[xm]$/i.test(f.name));
  const other = files.length - pdfs.length - xls.length;
  if (other) toast('엑셀(.xlsx)과 PDF만 읽을 수 있습니다 — 건너뛴 파일 ' + other + '개');
  for (const f of xls) await importExcelFile(f);
  if (pdfs.length) {
    showTab('import');
    for (const f of pdfs) await handlePdf(f);     // 여러 PDF는 종류별로 각각 미리보기에 쌓임
  }
}
async function importExcelFile(file) {
  if (!IS_EXE) { toast('엑셀 읽기는 exe 프로그램에서만 됩니다'); return; }
  try {
    toast('엑셀을 읽는 중… ' + file.name);
    const r = await api().import_xlsx_data(file.name, await fileToBase64(file), S.year);
    if (r.error) { alert(r.error); return; }
    const parts = [];
    if (r.lines) {
      if (S.lines.length && !confirm(`'${file.name}'의 일상경비(${r.lines.length}개 항목)로 지금 입력된 일상경비를 바꿀까요?\n(지출액·예정·비고가 엑셀 값으로 바뀝니다)`)) { /* 건너뜀 */ }
      else { S.lines = r.lines; S.memo = r.memo || ''; if (r.asOf) { S.asOf = r.asOf; $('#asof').value = S.asOf; } parts.push(`일상경비 ${r.lines.length}개 항목`); }
    }
    if (r.util) {
      const had = Object.values(S.util).some(x => x.items && x.items.length);
      if (had && !confirm(`'${file.name}'의 공공요금으로 지금 입력된 공공요금(번호·월별 금액·수령 체크)을 바꿀까요?`)) { /* 건너뜀 */ }
      else { S.util = r.util; UI.util.month = null; parts.push('공공요금(전기·상하수도·통신)'); }
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
