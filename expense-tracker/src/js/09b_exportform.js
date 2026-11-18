/* ===== 지급 폼 만들기: 수령 체크한 고지서를 내 거래처입력 양식 그대로 엑셀로 출력 =====
   - 양식은 사용자가 가진 거래처입력 엑셀을 한 번 등록(지출 폼 불러오기 때 자동 등록 / [양식만 저장])
   - 항목별 거래처명·사업자번호·은행·예금주·계좌번호는 그 폼에서 배워 두었다가 그대로 채움 */
const HK = h => String(h || '').trim().split(/[\s(]/)[0];
const FM_SKIP = /^(공급가액|부가세액|공제액|지출액)$/;
const EX = { m: 0, off: {} };

function isTplForm(rows) { const h = ((rows && rows[0] && rows[0].header) || []).map(HK); return h.includes('거래처명') && h.includes('공급가액'); }

/* 지출 폼의 줄에서 거래처 정보를 배워 항목에 저장 */
function pfLearn(list, b64) {
  let n = 0;
  list.forEach(x => {
    const r = x.r; if (!r || !isTplForm([r])) return;
    const fm = {}; r.header.forEach((h, c) => { const k = HK(h); if (!k || FM_SKIP.test(k)) return; const v = r.cells[c]; if (v) fm[k] = v; });
    if (fm['거래처명']) { x.it.fm = fm; n++; }
  });
  if (n && b64) S.formTpl = b64;
  return n;
}
function fmFallback(sec, it) {
  const same = sec.items.find(i => i.fm && it.carrier && i.carrier === it.carrier) || sec.items.find(i => i.fm);
  if (!same) return null;
  const f = { ...same.fm }; delete f['예금주명']; delete f['계좌번호']; delete f['입금명세']; delete f['지로번호'];
  f['예금주명'] = String(it.name || '').replace(/\n/g, ' '); return f;
}
function exportRows(sec, items, m) {
  return items.map(it => {
    const base = it.fm || fmFallback(sec, it) || {};
    return { learned: !!it.fm, it, row: { ...base, '계좌번호': base['계좌번호'] && it.fm ? base['계좌번호'] : it.no2, '공급가액': it.amt[m] } };
  });
}
function exportCands(m) {
  return CATS.map(([k, nm]) => ({ k, nm, sec: S.util[k] })).filter(x => x.sec).map(x => ({ ...x, list: x.sec.items.filter(i => !i.inactive && stateOf(i, m) === 1 && i.amt[m] > 0), noAmt: x.sec.items.filter(i => !i.inactive && stateOf(i, m) === 1 && !(i.amt[m] > 0)) }));
}
function openExportForm() {
  const sel = UI.util && UI.util.month; EX.m = sel != null ? sel : (S.util[UI.util.cat] ? defaultMonth(S.util[UI.util.cat]) : 0); EX.off = {};
  drawExportForm();
}
function drawExportForm() {
  const m = EX.m, cs = exportCands(m);
  if (!S.formTpl) {
    openDlg(`<h3>지급 폼 만들기</h3><p class="sub">수령 체크한 고지서를 <b>내 거래처입력 엑셀 양식 그대로</b> 출력합니다. 먼저 <b>양식을 한 번만 등록</b>해 주세요.</p>
      <p>평소 쓰시는 거래처입력 엑셀(전기·상하수도·통신 중 아무거나, 이미 작성한 것도 됩니다)을 선택하면 양식과 거래처 정보(거래처명·사업자번호·은행·예금주·계좌번호)를 기억합니다. 항목에 없는 계좌는 비워 두니 한 번 더 확인하세요.</p>
      <div class="row" style="justify-content:flex-end;margin-top:12px"><button class="btn" data-exa="close">닫기</button><button class="btn primary" data-exa="register">양식 등록 (엑셀 선택)</button></div>`);
    return;
  }
  const rowsHTML = cs.map(c => c.list.length || c.noAmt.length ? `<tr class="grp"><td colspan="5"><b>${c.nm}</b> <span class="muted">수령·미납 ${c.list.length}건 · ${won(sum(c.list, i => i.amt[m]))}원</span></td></tr>` +
    c.list.map(i => `<tr><td><label class="chk"><input type="checkbox" data-ex="${c.k}|${i.id}" ${EX.off[c.k + '|' + i.id] ? '' : 'checked'}><i></i></label></td><td class="nm2">${esc(String(i.name).replace(/\n/g, ' '))}</td><td class="muted">${esc(i.no2 || i.no1)}</td><td class="num">${won(i.amt[m])}</td><td>${i.fm ? '<span class="badge good">양식 정보</span>' : '<span class="badge warn" data-tip="이 항목은 지출 폼에서 거래처 정보를 배운 적이 없어 같은 종류의 정보로 채웁니다. 열어서 확인하세요">추정</span>'}</td></tr>`).join('') +
    (c.noAmt.length ? `<tr><td></td><td colspan="4" class="muted">⚠ 수령했지만 금액이 없는 ${c.noAmt.length}건은 제외됩니다: ${esc(c.noAmt.map(i => String(i.name).replace(/\n/g, ' ').slice(0, 14)).join(', '))}</td></tr>` : '') : '').join('');
  const picked = cs.flatMap(c => c.list.filter(i => !EX.off[c.k + '|' + i.id]).map(i => ({ c, i })));
  openDlg(`<div class="pfwrap"><div class="row" style="justify-content:space-between"><h3 style="margin:0">지급 폼 만들기 <span class="muted" style="font-weight:400;font-size:12px">수령 체크한 고지서 → 거래처입력 양식 엑셀</span></h3><button class="btn sm" data-exa="close">닫기</button></div>
    <div class="row" style="margin:8px 0;gap:8px"><label class="fld" style="margin:0">월<select id="ex-m" class="fsel">${MON.map((n, i) => `<option value="${i}" ${i === m ? 'selected' : ''}>${n}분</option>`).join('')}</select></label>
      <span class="muted">${S.formTpl ? '양식 등록됨' : ''} · 종류별(전기/상하수도/통신)로 파일이 하나씩 만들어집니다</span><button class="btn sm" data-exa="register" style="margin-left:auto">양식 다시 등록</button></div>
    ${picked.length || rowsHTML ? `<div class="tbl-wrap" style="max-height:50vh;overflow:auto"><table class="t"><thead><tr><th>포함</th><th>항목</th><th>번호</th><th>금액(원)</th><th>거래처 정보</th></tr></thead><tbody>${rowsHTML}</tbody></table></div>` : '<div class="empty">이 달에 수령 체크됐고 아직 납부 전인 고지서가 없습니다.</div>'}
    <div class="row" style="justify-content:space-between;margin-top:12px"><span class="muted">선택 ${picked.length}건 · ${won(sum(picked, p => p.i.amt[m]))}원</span><span class="row"><button class="btn" data-exa="close">닫기</button><button class="btn primary" data-exa="make" ${picked.length ? '' : 'disabled'}>엑셀 만들기</button></span></div></div>`,
    d => { d.classList.add('pfbox'); d.onclose = () => d.classList.remove('pfbox'); });
}
async function makeExportForm() {
  const m = EX.m; const cs = exportCands(m); let made = 0, last = '';
  for (const c of cs) {
    const items = c.list.filter(i => !EX.off[c.k + '|' + i.id]); if (!items.length) continue;
    const rows = exportRows(c.sec, items, m).map(x => x.row);
    const name = `${c.nm}_거래처입력_${S.year}-${String(m + 1).padStart(2, '0')}.xlsx`;
    try { const r = await api().export_payform(S.formTpl, rows, name); if (r && r.ok) { made++; last = r.path; } else { alert('엑셀을 만들지 못했습니다: ' + ((r && r.error) || '')); return; } }
    catch (e) { alert('엑셀을 만들지 못했습니다: ' + (e.message || e)); return; }
  }
  closeDlg(); toast(made ? `${made}개 파일을 만들었습니다 (내보내기 폴더): ${last}` : '만들 항목이 없습니다');
}
$('#dlg').addEventListener('change', e => {
  const t = e.target; if (!$('#ex-m') && !t.dataset.ex) return;
  if (t.id === 'ex-m') { EX.m = +t.value; EX.off = {}; drawExportForm(); }
  else if (t.dataset.ex) { EX.off[t.dataset.ex] = !t.checked; drawExportForm(); }
});
$('#dlg').addEventListener('click', e => {
  const b = e.target.closest('[data-exa]'); if (!b) return; const a = b.dataset.exa;
  if (a === 'close') closeDlg();
  else if (a === 'register') { closeDlg(); UI._learnOnly = true; pickFiles('.xlsx,.xlsm', 'form'); }
  else if (a === 'make') makeExportForm();
});
