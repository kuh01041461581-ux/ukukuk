/* ===== 고지서 분류 탭 (공익요원용): 번호를 치면 우리 계 / 다른 계 전달을 판정하고 Enter로 처리 ===== */
function sortMonthOf(sec) { return FIND.month === 'auto' ? defaultMonth(sec) : +FIND.month; }
function sortPending() {
  const own = [], fwd = [];
  for (const [k, nm] of CATS) {
    const sec = S.util[k]; if (!sec) continue; const m = sortMonthOf(sec); S.fwdDone = S.fwdDone || {};
    for (const it of sec.items) {
      if (it.inactive) continue; const st = stateOf(it, m);
      const q = it.no2 || it.no1 || it.name;
      if (!it.forward) { own.push({ k, nm, it, m, st, q }); }
      else { const done = !!S.fwdDone[k + '|' + it.id + '|' + m]; fwd.push({ k, nm, name: it.name, to: it.forward, m, done, q, st }); }
    }
    (sec.forwards || []).forEach(f => { if (!f.id) f.id = uid('F'); const done = !!S.fwdDone[k + '|' + f.id + '|' + m]; fwd.push({ k, nm, name: f.name, to: f.to || '-', m, done, q: String(f.numbers || f.name).split(/\s*\/\s*/)[0], st: null }); });
  }
  return { own, fwd };
}
function sortStatsHTML(P) {
  const ownN = P.own.filter(x => x.st === 1 || x.st === 3).length, ownT = P.own.filter(x => x.st !== 2).length;
  const fwdN = P.fwd.filter(x => x.done).length;
  const t = (cls, big, lbl) => `<div class="sstat ${cls}"><b>${big}</b><span>${lbl}</span></div>`;
  return t('own', `${ownN} / ${ownT}`, '우리 계 수령') + t('fwd', `${fwdN} / ${P.fwd.length}`, '다른 계 전달') + t('', String((FIND.log || []).length), '이번에 처리한 건수');
}
function sortListsHTML(P) {
  const ownLeft = P.own.filter(x => x.st === 0);
  const fwdLeft = P.fwd.filter(x => !x.done);
  const groups = {}; fwdLeft.forEach(x => { (groups[x.to] = groups[x.to] || []).push(x); });
  const row = (x, nm) => `<button class="srow" data-sq="${esc(x.q)}"><span class="badge" style="background:color-mix(in srgb,${CATCOLOR[x.k]} 18%,transparent);color:var(--text)">${x.nm}</span><b>${esc(String(nm).replace(/\n/g, ' '))}</b><span class="muted">${MON[x.m]}분</span></button>`;
  return `<div class="slist own"><h3>우리 계 · 아직 수령 안 한 것 <span class="muted">${ownLeft.length}건</span></h3>${ownLeft.map(x => row(x, x.it.name)).join('') || '<div class="muted" style="padding:8px">모두 수령했습니다 ✓</div>'}</div>
    <div class="slist fwd"><h3>다른 계 · 아직 전달 안 한 것 <span class="muted">${fwdLeft.length}건</span></h3>${Object.keys(groups).sort().map(to => `<div class="sgrp">→ ${esc(to)} <span class="muted">${groups[to].length}건</span></div>` + groups[to].map(x => row(x, x.name)).join('')).join('') || '<div class="muted" style="padding:8px">모두 전달했습니다 ✓</div>'}</div>`;
}
function redrawSort() {
  const P = sortPending();
  const a = $('#sstat'), b = $('#sres'), c = $('#slists'); if (!a) return;
  a.innerHTML = sortStatsHTML(P); const ban = $('#sban'); if (ban) ban.innerHTML = sortBanner();
  b.innerHTML = $('#sq').value.trim() ? verdictCard(FIND.q, true) : vdLogHTML() || '<div class="empty">고지서의 번호(뒤 4자리만 쳐도 됩니다)나 이름 일부를 입력하세요</div>'; c.innerHTML = sortListsHTML(P);
}
function renderSort() {
  const P = $('#p-sort');
  P.innerHTML = `<div class="sorttop"><div class="sbar"><label class="muted">확인할 월 <select id="smon" class="fsel"><option value="auto">자동(처리 필요한 달)</option>${MON.map((x, i) => `<option value="${i}">${x}분</option>`).join('')}</select></label><div id="sstat" class="sstats"></div></div>
    <input id="sq" type="search" class="sq" placeholder="고지서 번호(뒤 4자리) 또는 이름을 입력하고 Enter" autocomplete="off" value="${esc(FIND.q)}">
    <div class="hint" style="margin:6px 2px">Enter: 판정대로 처리하고 다음 고지서 · Esc: 지우기 · 카드의 <b>월</b>이 고지서와 같은지 꼭 확인하세요</div></div>
    <div id="sban"></div><div id="sres" class="sres"></div><div id="slists" class="slists"></div>`;
  $('#smon').value = FIND.month; redrawSort(); setTimeout(() => { const i = $('#sq'); if (i) { i.focus(); i.select(); } }, 0);
}
/* verdictAct 가 끝난 뒤 이 탭을 다시 그리게 하는 훅 */
function sortAfterAct() { const i = $('#sq'); if (i) { i.value = ''; redrawSort(); i.focus(); } }
$('#p-sort').addEventListener('input', e => { if (e.target.id === 'sq') { FIND.q = e.target.value; redrawSort(); } });
$('#p-sort').addEventListener('change', e => { if (e.target.id === 'smon') { FIND.month = e.target.value; redrawSort(); } });
$('#p-sort').addEventListener('keydown', e => {
  if (e.target.id !== 'sq') return;
  if (e.key === 'Enter' && !e.isComposing) { e.preventDefault(); verdictAct(); }
  else if (e.key === 'Escape') { FIND.q = ''; e.target.value = ''; redrawSort(); }
});
$('#p-sort').addEventListener('click', e => {
  const r = e.target.closest('[data-sq]'); if (r) { FIND.q = r.dataset.sq; const i = $('#sq'); i.value = FIND.q; redrawSort(); i.focus(); return; }
  const b = e.target.closest('[data-fo]'); if (!b) return; const a = b.dataset.fo;
  if (a === 'vact') { verdictAct(); return; }
  if (a === 'undo') { undoLog(+b.dataset.i); return; }
  if (a === 'vmonth') { setFindMonth(b.dataset.v); return; }
  if (a === 'classify') { classifyDialog(); return; }
  const q = FIND.q;
  if (a === 'addfwd') { UI.util.cat = UI.util.cat || 'elec'; UI.util.sub = 'fwd'; showTab('util'); fwdDialog(null, UI.util.cat); setTimeout(() => { const n = $('#fnu'); if (n && /\d/.test(q)) n.value = q; }, 50); }
  else if (a === 'additem') { UI.util.sub = 'pay'; showTab('util'); itemDialog(null); setTimeout(() => { const n = $('#n2'); if (n && /\d/.test(q)) n.value = q; }, 50); }
});

/* ===== 분류 점검: 모든 공공요금 항목을 '우리 계 / 다른 계(전달처)'로 지정·확인 ===== */
function sortBanner() {
  let fw = 0, own = 0; CATS.forEach(([k]) => (S.util[k] ? S.util[k].items : []).forEach(it => { if (it.inactive) return; if (it.forward) fw++; else own++; }));
  const lst = sum(CATS, ([k]) => S.util[k] ? (S.util[k].forwards || []).length : 0); const warn = fw === 0 && lst > 0;
  return `<div class="sbanner ${warn ? 'warn' : ''}"><span>${warn ? '⚠ 전달처가 지정된 항목이 없습니다. 다른 계에서 내는 항목이 있다면 지정해야 판정이 맞습니다. ' : ''}분류 현황: <b>우리 계 ${own}개</b> · <b>다른 계 전달 ${fw}개</b> (+ 전달처 안내 목록 ${lst}건)</span><button class="btn sm" data-fo="classify">분류 점검</button></div>`;
}
function classifyDialog() {
  const tos = [...new Set(CATS.flatMap(([k]) => S.util[k] ? [...(S.util[k].forwards || []).map(f => f.to), ...S.util[k].items.map(i => i.forward)] : []).filter(Boolean))].sort();
  const pend = {}; CATS.forEach(([k]) => (S.util[k] ? S.util[k].items : []).forEach(it => { pend[k + '|' + it.id] = it.forward || ''; }));
  const optHTML = cur => `<option value="">우리 계 (직접 지급)</option>${tos.map(t => `<option value="${esc(t)}" ${t === cur ? 'selected' : ''}>→ ${esc(t)}</option>`).join('')}<option value="__new">＋ 새 전달처 입력…</option>`;
  const rowsHTML = q => CATS.map(([k, nm]) => {
    const its = (S.util[k] ? S.util[k].items : []).filter(it => !it.inactive && (!q || normQ([it.name, it.no1, it.no2, it.carrier].join(' ')).includes(q))); if (!its.length) return '';
    return `<tr class="grp"><td colspan="3"><b>${nm}</b> <span class="muted">${its.length}개</span></td></tr>` + its.map(it => { const key = k + '|' + it.id; const cur = pend[key];
      return `<tr class="${cur ? 'fwdrowc' : ''}"><td class="nm2">${esc(String(it.name).replace(/\n/g, ' '))}</td><td class="muted">${esc(it.no2 || it.no1)}</td><td><select class="fsel" data-cl="${key}" style="min-width:210px">${optHTML(cur).replace(`value="${esc(cur)}" `, `value="${esc(cur)}" `)}</select></td></tr>`; }).join('');
  }).join('') || '<tr><td colspan="3" class="empty">없음</td></tr>';
  openDlg(`<h3>분류 점검 <span class="muted" style="font-weight:400;font-size:12px">우리 계 직접 지급 / 다른 계 전달</span></h3>
    <p class="sub" style="margin:0 0 8px">항목마다 <b>누가 내는지</b>를 지정합니다. 「→ ○○계」로 지정한 항목은 고지서 분류에서 주황(전달)으로 판정되고, 그 외는 초록(우리 계)입니다.</p>
    <div class="row" style="gap:8px;margin-bottom:8px"><input id="clq" class="cellin" style="text-align:left;width:220px" placeholder="이름·번호 검색"><span class="muted" id="clcnt"></span></div>
    <div class="tbl-wrap" style="max-height:54vh;overflow:auto"><table class="t"><thead><tr><th>항목</th><th>번호</th><th>누가 내나</th></tr></thead><tbody id="clbody">${rowsHTML('')}</tbody></table></div>
    <div class="row" style="justify-content:flex-end;margin-top:10px"><button class="btn" data-d="cancel">취소</button><button class="btn primary" data-d="save">적용</button></div>`, d => {
    const cnt = () => { $('#clcnt', d).textContent = `다른 계 전달 ${Object.values(pend).filter(Boolean).length}개 / 전체 ${Object.keys(pend).length}개`; };
    const redraw = () => { $('#clbody', d).innerHTML = rowsHTML(normQ($('#clq', d).value)); cnt(); };
    cnt(); $('#clq', d).oninput = redraw;
    d.onchange = e => {
      const t = e.target; const key = t.dataset.cl; if (!key) return;
      if (t.value === '__new') { const nm = (prompt('전달처(계·부서) 이름') || '').trim(); if (nm) { if (!tos.includes(nm)) tos.push(nm); pend[key] = nm; } redraw(); return; }
      pend[key] = t.value; cnt(); const tr = t.closest('tr'); if (tr) tr.classList.toggle('fwdrowc', !!t.value);
    };
    d.onclick = ev => {
      const b = ev.target.closest('[data-d]'); if (!b) return;
      if (b.dataset.d === 'cancel') closeDlg();
      else if (b.dataset.d === 'save') {
        CATS.forEach(([k]) => (S.util[k] ? S.util[k].items : []).forEach(it => { const v = pend[k + '|' + it.id]; if (v !== undefined) it.forward = v; }));
        save(); closeDlg(); if (UI.tab === 'sort') redrawSort(); toast('분류를 저장했습니다');
      }
    };
  });
}
