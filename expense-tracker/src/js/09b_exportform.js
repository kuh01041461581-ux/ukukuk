/* ===== 지급 폼 만들기: 수령 체크한 고지서를 거래처입력 양식 그대로 엑셀로 출력 =====
   - 양식: 기본 거래처입력 양식(PAYFORM_BLANK)을 내장. 쓰시던 폼을 불러오면 그 양식을 대신 사용
   - 계좌번호: 항목에 이미 들어 있는 납부번호(계좌)를 그대로 사용
   - 거래처명·사업자번호·예금주 등: 쓰시던 폼에서 배우거나(여러 개 가능), 종류별 [거래처 기본정보]에 직접 입력 */
const HK = h => String(h || '').trim().split(/[\s(]/)[0];
const FM_SKIP = /^(공급가액|부가세액|공제액|지출액)$/;
const PAYDEF_KEYS = ['거래처명', '거래처구분', '사업자등록번호', '입금유형', '은행', '예금주명'];
const PAYDEF_DEFAULT = { '거래처구분': '10:법인사업자', '입금유형': '10:계좌이체', '은행': '011:농협은행' };
const EX = { m: 0, off: {} };

function isTplForm(rows) { const h = ((rows && rows[0] && rows[0].header) || []).map(HK); return h.includes('거래처명') && h.includes('공급가액'); }
function tplB64() { return S.formTpl || (typeof PAYFORM_BLANK !== 'undefined' ? PAYFORM_BLANK : ''); }

/* 지출 폼의 줄에서 거래처 정보를 배워 항목에 저장하고, 종류별 기본정보(가장 흔한 값)도 갱신 */
function pfLearn(list, b64) {
  let n = 0; const byK = {};
  list.forEach(x => {
    const r = x.r; if (!r || !isTplForm([r])) return;
    const fm = {}; r.header.forEach((h, c) => { const k = HK(h); if (!k || FM_SKIP.test(k)) return; const v = r.cells[c]; if (v) fm[k] = v; });
    if (fm['거래처명']) { x.it.fm = fm; n++; if (x.k) (byK[x.k] = byK[x.k] || []).push(fm); }
  });
  S.payDef = S.payDef || {};
  for (const k in byK) {
    const d = {};
    PAYDEF_KEYS.forEach(f => { const cnt = {}; byK[k].forEach(fm => { if (fm[f]) cnt[fm[f]] = (cnt[fm[f]] || 0) + 1; }); const top = Object.entries(cnt).sort((a, b) => b[1] - a[1])[0]; if (top) d[f] = top[0]; });
    S.payDef[k] = d;
  }
  if (n && b64) S.formTpl = b64;
  return n;
}
/* 항목이 폼에서 직접 배운 정보가 없을 때: 같은 종류(·같은 통신사)에서 배운 값 → 종류별 기본정보 순으로 채움 */
function fmFallback(sec, it, k) {
  const same = sec.items.find(i => i.fm && it.carrier && i.carrier === it.carrier) || sec.items.find(i => i.fm);
  let f = null;
  if (same) { f = { ...same.fm }; delete f['예금주명']; delete f['계좌번호']; delete f['입금명세']; delete f['지로번호']; }
  else if (S.payDef && S.payDef[k] && S.payDef[k]['거래처명']) { f = { ...PAYDEF_DEFAULT, ...S.payDef[k] }; }
  if (!f) return null;
  if (!f['예금주명']) f['예금주명'] = String(it.name || '').replace(/\n/g, ' ');
  return f;
}
function exportInfo(sec, it, k) { return it.fm ? 'learned' : (fmFallback(sec, it, k) ? 'def' : 'none'); }
function exportRows(sec, items, m, k) {
  return items.map(it => {
    const base = it.fm || fmFallback(sec, it, k) || {};
    return { it, row: { ...base, '계좌번호': (it.fm && it.fm['계좌번호']) || it.no2, '공급가액': it.amt[m] } };
  });
}
function exportCands(m) {
  return CATS.map(([k, nm]) => ({ k, nm, sec: S.util[k] })).filter(x => x.sec).map(x => ({ ...x, list: x.sec.items.filter(i => !i.inactive && stateOf(i, m) === 1) }));
}
function openExportForm() {
  const sel = UI.util && UI.util.month; EX.m = sel != null ? sel : (S.util[UI.util.cat] ? defaultMonth(S.util[UI.util.cat]) : 0); EX.off = {}; EX.markPaid = false;
  drawExportForm();
}
function drawExportForm() {
  const m = EX.m, cs = exportCands(m);
  const info = { learned: '<span class="badge good" data-tip="쓰시던 폼에서 배운 이 항목의 거래처 정보를 씁니다">폼 정보</span>', def: '<span class="badge gray" data-tip="이 종류의 기본정보로 채웁니다 (계좌번호는 항목에 입력된 납부번호)">기본정보</span>', none: '<span class="badge bad" data-tip="거래처명·사업자번호를 모릅니다. [거래처 기본정보]를 입력하거나 쓰시던 폼을 불러오세요">정보 없음</span>' };
  const anyLearned = cs.some(c => c.sec.items.some(i => i.fm)) || Object.keys(S.payDef || {}).length > 0;
  const rowsHTML = cs.map(c => c.list.length ? `<tr class="grp"><td colspan="5"><b>${c.nm}</b> <span class="muted">수령·미납 ${c.list.length}건 · 금액 입력 ${c.list.filter(i => i.amt[m] > 0).length}건 · ${won(sum(c.list, i => i.amt[m]))}원</span> <button class="btn sm" data-exa="def" data-k="${c.k}" style="margin-left:8px">거래처 기본정보</button></td></tr>` +
    c.list.map(i => `<tr class="${i.amt[m] > 0 ? '' : 'noamt'}"><td><label class="chk"><input type="checkbox" data-ex="${c.k}|${i.id}" ${EX.off[c.k + '|' + i.id] || !(i.amt[m] > 0) ? '' : 'checked'} ${i.amt[m] > 0 ? '' : 'disabled'}><i></i></label></td><td class="nm2">${esc(String(i.name).replace(/\n/g, ' '))}</td><td class="muted">${esc(i.no2 || i.no1)}</td><td><input class="cellin" style="width:120px;border-color:var(--edge-dark)" data-examt="${c.k}|${i.id}" value="${i.amt[m] > 0 ? won(i.amt[m]) : ''}" placeholder="금액 입력" inputmode="numeric" aria-label="${esc(i.name)} 금액"></td><td>${info[exportInfo(c.sec, i, c.k)]}</td></tr>`).join('') : '').join('');
  const picked = cs.flatMap(c => c.list.filter(i => !EX.off[c.k + '|' + i.id] && i.amt[m] > 0).map(i => ({ c, i })));
  openDlg(`<div class="pfwrap"><div class="row" style="justify-content:space-between"><h3 style="margin:0">지급 폼 만들기 <span class="muted" style="font-weight:400;font-size:12px">수령 체크한 고지서 → 거래처입력 양식 엑셀</span></h3><button class="btn sm" data-exa="close">닫기</button></div>
    <div class="row" style="margin:8px 0;gap:8px"><label class="fld" style="margin:0">월<select id="ex-m" class="fsel">${MON.map((n, i) => `<option value="${i}" ${i === m ? 'selected' : ''}>${n}분</option>`).join('')}</select></label>
      <span class="muted">금액이 비어 있으면 여기서 바로 입력 · 종류별(전기/상하수도/통신)로 파일이 하나씩 만들어집니다</span><button class="btn sm" data-exa="register" style="margin-left:auto" data-tip="쓰시던 거래처입력 엑셀(여러 개 가능)을 불러오면 양식과 항목별 거래처 정보를 배웁니다">쓰시던 폼 불러오기</button></div>
    ${anyLearned ? '' : '<div class="note-box" style="margin:0 0 8px">처음이라면: 쓰시던 <b>거래처입력 엑셀을 이 창에 끌어 놓거나</b> [쓰시던 폼 불러오기]를 누르세요(전기·상하수도·통신 폼을 한꺼번에 여러 개 가능). 또는 종류별 [거래처 기본정보]에 거래처명·사업자번호를 직접 입력하세요. 계좌번호는 항목에 이미 들어 있는 납부번호를 씁니다.</div>'}
    ${rowsHTML ? `<div class="tbl-wrap" style="max-height:48vh;overflow:auto"><table class="t"><thead><tr><th>포함</th><th>항목</th><th>계좌·납부번호</th><th>금액(원)</th><th>거래처 정보</th></tr></thead><tbody>${rowsHTML}</tbody></table></div>` : '<div class="empty">이 달에 수령 체크됐고 아직 납부 전인 고지서가 없습니다.</div>'}
    <label class="row" style="gap:6px;margin-top:8px"><input type="checkbox" id="ex-paid" ${EX.markPaid ? 'checked' : ''}> 엑셀을 만든 뒤 선택한 항목을 <b>납부 완료</b>로 표시 <span class="muted">(지출 결의까지 끝난 뒤에 쓰세요)</span></label>
    <div class="row" style="justify-content:space-between;margin-top:8px"><span class="muted">선택 ${picked.length}건 · ${won(sum(picked, p => p.i.amt[m]))}원</span><span class="row"><button class="btn" data-exa="close">닫기</button><button class="btn primary" data-exa="make" ${picked.length ? '' : 'disabled'}>엑셀 만들기</button></span></div></div>`,
    d => { d.classList.add('pfbox'); UI._learnOnly = true; d.onclose = () => { d.classList.remove('pfbox'); UI._learnOnly = false; }; });
}
/* 종류별 거래처 기본정보 입력 */
function payDefDialog(k) {
  const cur = { ...PAYDEF_DEFAULT, ...((S.payDef && S.payDef[k]) || {}) }; const nm = (CATS.find(c => c[0] === k) || [k, k])[1];
  const f = (id, lbl, key, ph) => `<label class="fld">${lbl}<input id="${id}" value="${esc(cur[key] || '')}" placeholder="${esc(ph || '')}"></label>`;
  openDlg(`<h3>거래처 기본정보 — ${esc(nm)}</h3><p class="sub" style="margin:0 0 8px">지급 폼의 거래처 열에 들어갈 값입니다. 쓰시던 폼을 불러오면 자동으로 채워집니다. <b>계좌번호는 항목에 입력된 납부번호</b>를 씁니다.</p>
    ${f('pd1', '거래처명 (필수)', '거래처명', '예: 한국전력공사')}${f('pd2', '사업자등록번호', '사업자등록번호', '')}${f('pd3', '거래처구분', '거래처구분', '10:법인사업자')}${f('pd4', '입금유형', '입금유형', '10:계좌이체')}${f('pd5', '은행', '은행', '011:농협은행')}${f('pd6', '예금주명 (비우면 항목 이름)', '예금주명', '')}
    <div class="row" style="justify-content:flex-end;margin-top:10px"><button class="btn" data-pd="cancel">취소</button><button class="btn primary" data-pd="save">저장</button></div>`, d => {
    d.onclick = ev => {
      const b = ev.target.closest('[data-pd]'); if (!b) return;
      if (b.dataset.pd === 'cancel') { drawExportForm(); return; }
      const v = id => $('#' + id, d).value.trim(); if (!v('pd1')) { toast('거래처명을 입력하세요'); return; }
      S.payDef = S.payDef || {}; S.payDef[k] = { '거래처명': v('pd1'), '사업자등록번호': v('pd2'), '거래처구분': v('pd3'), '입금유형': v('pd4'), '은행': v('pd5'), '예금주명': v('pd6') };
      Object.keys(S.payDef[k]).forEach(x => { if (!S.payDef[k][x]) delete S.payDef[k][x]; });
      save(); drawExportForm(); toast('거래처 기본정보를 저장했습니다');
    };
  });
}
async function makeExportForm() {
  const m = EX.m; const cs = exportCands(m); let made = 0, last = ''; const made_items = [];
  const jobs = cs.map(c => ({ c, items: c.list.filter(i => !EX.off[c.k + '|' + i.id] && i.amt[m] > 0) })).filter(j => j.items.length);
  const bad = jobs.filter(j => j.items.some(i => exportInfo(j.c.sec, i, j.c.k) === 'none'));
  if (bad.length) { alert(`${bad.map(j => j.c.nm).join(', ')}: 거래처명·사업자번호 정보가 없는 항목이 있습니다.\n종류별 [거래처 기본정보]를 입력하거나 [쓰시던 폼 불러오기]로 배운 뒤 다시 만들어 주세요.`); return; }
  for (const { c, items } of jobs) {
    made_items.push(...items);
    const rows = exportRows(c.sec, items, m, c.k).map(x => x.row);
    const name = `${c.nm}_거래처입력_${S.year}-${String(m + 1).padStart(2, '0')}.xlsx`;
    try { const r = await api().export_payform(tplB64(), rows, name); if (r && r.ok) { made++; last = r.path; } else { alert('엑셀을 만들지 못했습니다: ' + ((r && r.error) || '')); return; } }
    catch (e) { alert('엑셀을 만들지 못했습니다: ' + (e.message || e)); return; }
  }
  if (made && EX.markPaid) { made_items.forEach(i => setPaid(i, m, true)); }
  save(); closeDlg(); if (UI.tab === 'util') renderUtil();
  toast(made ? `${made}개 파일을 만들었습니다 (내보내기 폴더)${EX.markPaid ? ' · 납부 완료로 표시했습니다' : ''}: ${last}` : '만들 항목이 없습니다');
}
$('#dlg').addEventListener('change', e => {
  const t = e.target; if (!$('#ex-m')) return;
  if (t.id === 'ex-m') { EX.m = +t.value; EX.off = {}; drawExportForm(); }
  else if (t.id === 'ex-paid') { EX.markPaid = t.checked; }
  else if (t.dataset.examt) {
    const [k, id] = t.dataset.examt.split('|'); const it = (S.util[k] ? S.util[k].items : []).find(x => x.id === id); if (!it) return;
    const v = parseNum(t.value); if (Number.isNaN(v)) { toast('숫자나 계산식만 입력할 수 있습니다'); drawExportForm(); return; }
    setAmt(it, EX.m, v); save(); drawExportForm();
  }
  else if (t.dataset.ex) { EX.off[t.dataset.ex] = !t.checked; drawExportForm(); }
});
$('#dlg').addEventListener('click', e => {
  const b = e.target.closest('[data-exa]'); if (!b) return; const a = b.dataset.exa;
  if (a === 'close') closeDlg();
  else if (a === 'register') { closeDlg(); UI._learnOnly = true; pickFiles('.xlsx,.xlsm', 'form'); }
  else if (a === 'def') payDefDialog(b.dataset.k);
  else if (a === 'make') makeExportForm();
});
