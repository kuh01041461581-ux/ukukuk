/* ===== Ctrl+F 빠른 찾기: 모든 요금 종류에서 번호·장소로 찾고, 바로 수령·납부 체크 + 금액 입력 ===== */
const FIND = { q: '', month: 'auto', open: false };

function findHits(q) {
  const out = [];
  for (const [k, nm] of CATS) {
    const sec = S.util[k]; if (!sec) continue;
    for (const it of sec.items) if (matchItem(it, q, nm)) out.push({ kind: 'util', k, nm, sec, it });
  }
  for (const [k, nm] of CATS) { const sec = S.util[k]; if (sec) (sec.forwards || []).forEach(f => { if (matchFwd(f, q, nm)) out.push({ kind: 'fwd', k, nm, f }); }); }
  const terms = String(q || '').split(/\s+/).map(normQ).filter(Boolean);
  const helper = !!(S.opt && S.opt.helper);
  if (!helper) for (const l of visLines()) { const hay = normQ([l.label, l.biz, l.group, l.note].join(' ')); if (terms.length && terms.every(t => hay.includes(t))) out.push({ kind: 'line', l }); }
  if (terms.length && !helper) { let n = 0; for (const t of (S.tx || [])) { if (n >= 15) break; const l = txLineOf(t); const hay = normQ([t.memo, t.vendor, t.biz, t.name, l ? l.label : '', String(t.amt)].join(' ')); if (terms.every(x => hay.includes(x))) { out.push({ kind: 'tx', t, l }); n++; } } }
  return out;
}
function findMonth(h) { return FIND.month === 'auto' ? defaultMonth(h.sec) : +FIND.month; }

function findRows() {
  const q = FIND.q.trim();
  if (!q) return vdLogHTML() + '<div class="empty">계약번호·납부번호·장소·계좌 일부를 입력하세요. 예) <b>2392</b> · <b>숭림사</b> · <b>790183</b> · <b>함벽정 전기</b></div>';
  const hits = findHits(q); const card = verdictCard(q); if (!hits.length) return card + '<div class="empty">찾는 항목이 없습니다</div>';
  const list = hits.slice(0, 40);
  return card + list.map((h, idx) => {
    if (h.kind === 'tx') return `<div class="frow"><div class="fmain"><span class="badge gray">지출 내역</span> <b>${h.t.d}</b> <b>${won(h.t.amt)}원</b> ${esc(h.t.memo)}<span class="hint">${esc(h.t.vendor || '')} ${h.l ? '· ' + esc(h.l.label) : ''}</span></div></div>`;
    if (h.kind === 'fwd') return `<div class="frow fwdrow"><div class="fmain"><span class="badge" style="background:color-mix(in srgb,${CATCOLOR[h.k]} 18%,transparent);color:var(--text)">${h.nm}</span> <span class="badge warn">다른 계 전달</span> <b class="nolist">${esc(h.f.name.replace(/\n/g, ' '))}</b><div class="fwdnums">${fwdNums(h.f.numbers)}</div>${h.f.memo ? `<span class="hint">${esc(h.f.memo)}</span>` : ''}</div><div class="fact"><span class="fwdtag lg">→ ${esc(h.f.to || '-')}</span></div></div>`;
    if (h.kind === 'line') return `<div class="frow"><div class="fmain"><span class="badge gray">일상경비</span> <b>${esc(h.l.label)}</b> <span class="hint" style="display:inline">${esc(h.l.group)} · ${esc(h.l.biz)} · 잔액 ${won(calc(h.l).remain)}원</span><div class="nchips sm">${noteChips(h.l.note, h.l.scheduled)}</div></div><div class="fact"><button class="btn sm" data-fo="line" data-id="${h.l.id}">표에서 보기</button></div></div>`;
    const m = findMonth(h), it = h.it, st = stateOf(it, m), at = `data-k="${h.k}" data-id="${it.id}" data-m="${m}"`;
    return `<div class="frow st${st} ${it.inactive ? 'inactive' : ''}">
      <div class="fmain"><span class="badge" style="background:color-mix(in srgb,${CATCOLOR[h.k]} 18%,transparent);color:var(--text)">${h.nm}</span> <b class="nolist">${esc(it.name.replace(/\n/g, ' '))}</b>${it.inactive ? ' <span class="badge gray">미사용</span>' : ''} ${vdBadge(it)}
        ${numLines(h.sec, it, true)}${it.memo ? `<span class="hint">${esc(it.memo)}</span>` : ''}</div>
      <div class="fact"><b class="fm">${MON[m]}분</b>
        ${chk('수령', `data-fu="rcv" ${at}`, st === 1 || st === 3)}${chk('납부', `data-fu="paid" ${at}`, st === 3, 'pay')}
        <input class="cellin fin" data-fu="amt" ${at} data-nav="fa-${idx}" value="${it.amt[m] == null ? '' : won(it.amt[m])}" inputmode="numeric" placeholder="금액" aria-label="${esc(it.name)} ${MON[m]}분 금액">
        <span class="stlabel s${st}">${{ 0: '미수령', 1: '수령 · 미납', 3: '납부 완료', 2: '청구없음' }[st]}</span>
        <button class="btn sm" data-fo="util" data-k="${h.k}" data-id="${it.id}" data-m="${m}" data-tip="목록에서 보기">목록</button></div></div>`;
  }).join('') + (hits.length > 40 ? `<div class="empty">${hits.length - 40}건 더 있습니다 — 검색어를 더 구체적으로 입력하세요</div>` : '');
}
function redrawFind(focus) {
  const box = $('#fres'); if (!box) return; box.innerHTML = findRows();
  const hn = $('#fhint'); if (hn) hn.textContent = findMode() === 'rcv' ? '번호 뒤 4자리만 쳐도 됩니다 · Enter: 판정대로 처리하고 다음 고지서 · Esc: 닫기' : 'Enter: 첫 결과의 금액 칸으로 · 금액 입력 후 Enter: 저장 · Esc: 닫기';
  $$('[data-fo=mode]').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.v === findMode())));
  if (focus === 'search') { const i = $('#fq'); i.focus(); i.select(); }
  else if (focus) { const el = box.querySelector(`[data-nav="${focus}"]`); if (el) { el.focus(); el.select(); } }
}
function openFind() {
  const d = $('#dlg'); if (d.open && $('#fq')) { $('#fq').focus(); $('#fq').select(); return; }
  openDlg(`<div class="findwrap"><div class="row" style="gap:8px;margin-bottom:8px"><h3 style="margin:0;flex:1">빠르게 찾기 <span class="muted" style="font-weight:400;font-size:12px">모든 요금 종류 · 일상경비</span></h3>
      <label class="muted" style="font-size:12px">확인할 월 <select id="fmon" class="fsel"><option value="auto">자동(처리 필요한 달)</option>${MON.map((x, i) => `<option value="${i}">${x}분</option>`).join('')}</select></label>
      <button class="btn sm" data-fo="close" aria-label="닫기">✕</button></div>
    <input id="fq" type="search" class="fq" placeholder="계약번호 · 납부번호 · 계좌 · 장소 · 전달처 (예: 2392, 숭림사, 790183)" autocomplete="off" value="${esc(FIND.q)}">
    <div class="hint" id="fhint" style="margin:6px 2px 8px"></div>
    <div id="fres" class="fres"></div></div>`, d => {
    $('#fmon', d).value = FIND.month; FIND.open = true; d.classList.add('findbox'); d.onclose = () => { d.classList.remove('findbox'); FIND.open = false; if (UI.tab === 'util') renderUtil(); else if (UI.tab === 'daily') renderDaily(); };
    redrawFind('search');
  });
}
document.addEventListener('keydown', e => {
  if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === 'f') { e.preventDefault(); openFind(); }
});
/* 창 안 이벤트 */
$('#dlg').addEventListener('input', e => { if (e.target.id === 'fq') { FIND.q = e.target.value; redrawFind(); } });
$('#dlg').addEventListener('change', e => {
  const t = e.target;
  if (t.id === 'fmon') { FIND.month = t.value; redrawFind(); return; }
  const fu = t.dataset.fu; if (!fu) return;
  const sec = S.util[t.dataset.k]; const it = sec && sec.items.find(x => x.id === t.dataset.id); if (!it) return; const m = +t.dataset.m;
  if (fu === 'rcv') setRcv(it, m, t.checked ? 1 : 0);
  else if (fu === 'paid') setPaid(it, m, t.checked);
  else if (fu === 'amt') { const v = parseNum(t.value); if (Number.isNaN(v)) { toast('숫자나 계산식만 입력할 수 있습니다'); redrawFind(); return; } setAmt(it, m, v); }
  save(); redrawFind();
});
$('#dlg').addEventListener('keydown', e => {
  if (!$('#fq')) return;
  if (e.key === 'Escape') { e.preventDefault(); closeDlg(); return; }
  if (e.key === 'Enter' && !e.isComposing) {
    if (e.target.id === 'fq') { e.preventDefault(); if (findMode() === 'rcv') { verdictAct(); return; } const f = $('#fres .fin'); if (f) { f.focus(); f.select(); } return; }
    if (e.target.classList.contains('fin')) { e.preventDefault(); e.target.blur(); redrawFind('search'); return; }
  }
  if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && (e.target.classList.contains('fin') || e.target.id === 'fq')) {
    const ins = $$('#fres .fin'); if (!ins.length) return;
    let i = ins.indexOf(e.target); i = e.target.id === 'fq' ? (e.key === 'ArrowDown' ? 0 : ins.length - 1) : i + (e.key === 'ArrowDown' ? 1 : -1);
    if (i < 0) { $('#fq').focus(); $('#fq').select(); } else if (ins[i]) { ins[i].focus(); ins[i].select(); } e.preventDefault();
  }
});
$('#dlg').addEventListener('focusin', e => { if (e.target.classList && e.target.classList.contains('fin')) { const v = parseNum(e.target.value); e.target.value = v == null || isNaN(v) ? '' : String(v); e.target.select(); } });
$('#dlg').addEventListener('click', e => {
  const b = e.target.closest('[data-fo]'); if (!b) return; const a = b.dataset.fo;
  if (a === 'close') { closeDlg(); return; }
  if (a === 'mode') { S.opt = S.opt || {}; S.opt.findMode = b.dataset.v; save(); redrawFind('search'); return; }
  if (a === 'vact') { verdictAct(); return; }
  if (a === 'undo') { undoLog(+b.dataset.i); return; }
  if (a === 'vmonth') { setFindMonth(b.dataset.v); return; }
  if (a === 'addfwd') { const q = FIND.q; closeDlg(); UI.util.cat = UI.util.cat || 'elec'; UI.util.sub = 'fwd'; showTab('util'); fwdDialog(null, UI.util.cat); setTimeout(() => { const n = $('#fnu'); if (n && /\d/.test(q)) n.value = q; }, 50); return; }
  if (a === 'additem') { const q = FIND.q; closeDlg(); UI.util.sub = 'pay'; showTab('util'); itemDialog(null); setTimeout(() => { const n = $('#n2'); if (n && /\d/.test(q)) n.value = q; }, 50); return; }
  if (a === 'line') { closeDlg(); showTab('daily'); const el = document.querySelector(`input[data-f=spent][data-id="${b.dataset.id}"]`); if (el) { el.scrollIntoView({ block: 'center' }); el.focus(); const tr = el.closest('tr'); tr && tr.classList.add('flash'); setTimeout(() => tr && tr.classList.remove('flash'), 1800); } return; }
  if (a === 'util') {
    const u = UI.util; u.cat = b.dataset.k; u.sub = 'pay'; u.filter = 'all'; u.q = FIND.q; u.month = +b.dataset.m; closeDlg(); showTab('util');
  }
});

/* ===== 판정 카드: 고지서 번호를 치면 "우리 계 직접 지급 / 다른 계 전달 / 목록에 없음"을 크게 보여 줌 ===== */
function findMode() { return 'amt'; }
function vdOf(h) { if (h.kind === 'fwd') return { v: 'fwd', to: h.f.to || '-' }; return h.it.forward ? { v: 'fwd', to: h.it.forward } : { v: 'own' }; }
function vdBadge(it) { return it.forward ? `<span class="vd fwd">→ ${esc(it.forward)} 전달</span>` : '<span class="vd own">우리 계</span>'; }
function vdHits(q) { return findHits(q).filter(h => h.kind === 'util' || h.kind === 'fwd'); }
function vdMonth(h) { return h.kind === 'util' ? findMonth(h) : (FIND.month === 'auto' ? defaultMonth(S.util[h.k]) : +FIND.month); }
function fwdKey(h, m) { if (h.kind === 'fwd') { if (!h.f.id) h.f.id = uid('F'); return h.k + '|' + h.f.id + '|' + m; } return h.k + '|' + h.it.id + '|' + m; }
function vdLabel(h) { const d = vdOf(h); return d.v === 'own' ? '수령 체크' : (h.kind === 'util' ? '수령 + 전달함' : '전달함'); }
function verdictCard(q, key) {
  if (!q.trim()) return '';
  const hs = vdHits(q);
  if (!hs.length) return `<div class="verdict none"><div class="vt">목록에 없음</div><div class="vs">새 납부처일 수 있습니다. 번호를 다시 확인하거나 아래 버튼으로 등록하세요.</div><div class="row" style="margin-top:8px"><button class="btn" data-fo="addfwd">다른 계 전달처로 추가</button><button class="btn" data-fo="additem">우리 계 항목으로 추가</button></div></div>`;
  if (hs.length > 1) {
    const own = hs.filter(h => vdOf(h).v === 'own').length;
    return `<div class="verdict multi"><div class="vt">${hs.length}건이 일치합니다</div><div class="vs">우리 계 ${own}건 · 다른 계 ${hs.length - own}건 — 번호를 더 입력하면 한 건으로 좁혀집니다 (Enter 처리는 한 건일 때만)</div></div>`;
  }
  const h = hs[0], d = vdOf(h), m = vdMonth(h); let name, nums, state = '', done = false;
  if (h.kind === 'util') {
    const st = stateOf(h.it, m); name = String(h.it.name).replace(/\n/g, ' '); nums = numLines(h.sec, h.it, true);
    done = st === 1 || st === 3 || (d.v === 'fwd' && S.fwdDone && S.fwdDone[fwdKey(h, m)]);
    state = { 0: '아직 수령 처리 안 됨', 1: '이미 수령 처리됨', 3: '이미 납부까지 끝남', 2: '이 달은 청구 없음으로 표시됨' }[st];
    if (d.v === 'fwd' && st >= 1 && !(S.fwdDone && S.fwdDone[fwdKey(h, m)])) done = false;
  } else { name = String(h.f.name).replace(/\n/g, ' '); nums = `<div class="fwdnums">${fwdNums(h.f.numbers)}</div>`; done = !!(S.fwdDone && S.fwdDone[fwdKey(h, m)]); state = done ? '전달 처리됨' : '아직 전달 처리 안 됨'; }
  const title = d.v === 'own' ? '우리 계 직접 지급' : `→ ${esc(d.to)} 전달`;
  let warn = '';
  if (FIND.month === 'auto' && !done) {   // 다른 항목은 더 뒤 달까지 진행됐는데 이 항목만 앞선 달로 잡히는 경우 경고
    const sec = S.util[h.k]; const last = sec ? Math.max(-1, ...sec.items.map(lastFilledOf)) : -1;
    if (last > m) warn = `<div class="vwarn">⚠ <b>${MON[m]}분</b>으로 처리됩니다. 이 종류는 다른 항목이 ${MON[last]}분까지 진행돼 있습니다. 지금 받은 고지서가 ${MON[last]}분이면 <button class="btn sm" data-fo="vmonth" data-v="${last}">${MON[last]}분으로 바꾸기</button></div>`;
  }
  return `<div class="verdict ${d.v}"><div class="vtrow"><div class="vt">${title}</div><div class="vmonth" data-tip="이 월로 처리됩니다. 다르면 위쪽 '확인할 월'을 바꾸세요">${MON[m]}분</div></div>
    <div class="vs"><span class="badge" style="background:color-mix(in srgb,${CATCOLOR[h.k]} 18%,transparent);color:var(--text)">${h.nm}</span> <b>${esc(name)}</b> · ${state}</div>${nums}${warn}
    <div class="row" style="margin-top:10px">${done ? '<span class="vdone">✓ 처리 완료</span>' : `<button class="btn primary vbtn" data-fo="vact">${vdLabel(h)}${key ? ' <kbd>Enter</kbd>' : ''}</button>`}</div></div>`;
}
function setFindMonth(v) {
  FIND.month = String(v); const a = $('#smon'); if (a) a.value = FIND.month; const b = $('#fmon'); if (b) b.value = FIND.month;
  if ($('#fq')) redrawFind(); if (UI.tab === 'sort' && typeof redrawSort === 'function') { redrawSort(); const i = $('#sq'); if (i) i.focus(); }
}
function undoLog(i) {
  const x = (FIND.log || [])[i]; if (!x) return;
  if (x.kind === 'util') { const it = (S.util[x.k] ? S.util[x.k].items : []).find(y => y.id === x.id); if (it && x.prevRcv != null) setRcv(it, x.m, x.prevRcv); }
  if (x.key && !x.hadKey && S.fwdDone) delete S.fwdDone[x.key];
  FIND.log.splice(i, 1); save(); toast('되돌렸습니다');
  if ($('#fq')) redrawFind(); if (UI.tab === 'sort' && typeof redrawSort === 'function') redrawSort();
}
function verdictAct() {
  const q = FIND.q.trim(); if (!q) return; const hs = vdHits(q);
  if (hs.length !== 1) { toast(hs.length ? `${hs.length}건이 일치합니다 — 번호를 더 입력하세요` : '목록에 없는 번호입니다'); return; }
  const h = hs[0], d = vdOf(h), m = vdMonth(h); S.fwdDone = S.fwdDone || {};
  const prevRcv = h.kind === 'util' ? h.it.rcv[m] : null, fkey = d.v === 'fwd' ? fwdKey(h, m) : null, hadKey = !!(fkey && S.fwdDone[fkey]);
  if (h.kind === 'util') {
    const st = stateOf(h.it, m);
    if (st === 2) { toast('이 달은 청구 없음으로 표시돼 있습니다'); return; }
    if (st >= 1 && (d.v === 'own' || S.fwdDone[fkey])) { toast(`이미 처리된 고지서입니다 (${MON[m]}분)`); return; }
    if (st === 0) setRcv(h.it, m, 1);
  } else if (S.fwdDone[fkey]) { toast(`이미 전달 처리된 고지서입니다 (${MON[m]}분)`); return; }
  if (d.v === 'fwd') S.fwdDone[fwdKey(h, m)] = true;
  const name = String(h.kind === 'util' ? h.it.name : h.f.name).replace(/\n/g, ' ');
  FIND.log = FIND.log || []; FIND.log.unshift({ t: new Date(), name, v: d.v, to: d.to, nm: h.nm, m, kind: h.kind, k: h.k, id: h.kind === 'util' ? h.it.id : null, prevRcv, key: fkey, hadKey }); FIND.log = FIND.log.slice(0, 8);
  save(); FIND.q = '';
  if ($('#fq')) { $('#fq').value = ''; redrawFind('search'); } else sortAfterAct();
}
function vdLogHTML() {
  const L = FIND.log || []; if (!L.length) return '';
  return `<div class="vlog"><div class="muted" style="font-size:12px;margin:4px 2px">방금 처리한 것 (이번 창에서)</div>${L.map((x, i) => `<div class="vl ${x.v}"><b>${x.v === 'own' ? '우리 계' : '→ ' + esc(x.to) + ' 전달'}</b> <span>${x.nm} · ${esc(x.name)} · ${MON[x.m]}분</span><em>${String(x.t.getHours()).padStart(2, '0')}:${String(x.t.getMinutes()).padStart(2, '0')}</em><button class="btn sm" data-fo="undo" data-i="${i}">되돌리기</button></div>`).join('')}</div>`;
}
