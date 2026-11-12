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
  for (const l of S.lines) { const hay = normQ([l.label, l.biz, l.group, l.note].join(' ')); if (terms.length && terms.every(t => hay.includes(t))) out.push({ kind: 'line', l }); }
  return out;
}
function findMonth(h) { return FIND.month === 'auto' ? defaultMonth(h.sec) : +FIND.month; }

function findRows() {
  const q = FIND.q.trim();
  if (!q) return '<div class="empty">계약번호·납부번호·장소·계좌 일부를 입력하세요. 예) <b>2392</b> · <b>숭림사</b> · <b>790183</b> · <b>함벽정 전기</b></div>';
  const hits = findHits(q); if (!hits.length) return '<div class="empty">찾는 항목이 없습니다</div>';
  const list = hits.slice(0, 40);
  return list.map((h, idx) => {
    if (h.kind === 'fwd') return `<div class="frow fwdrow"><div class="fmain"><span class="badge" style="background:color-mix(in srgb,${CATCOLOR[h.k]} 18%,transparent);color:var(--text)">${h.nm}</span> <span class="badge warn">다른 계 전달</span> <b class="nolist">${esc(h.f.name.replace(/\n/g, ' '))}</b><div class="fwdnums">${fwdNums(h.f.numbers)}</div>${h.f.memo ? `<span class="hint">${esc(h.f.memo)}</span>` : ''}</div><div class="fact"><span class="fwdtag lg">→ ${esc(h.f.to || '-')}</span></div></div>`;
    if (h.kind === 'line') return `<div class="frow"><div class="fmain"><span class="badge gray">일상경비</span> <b>${esc(h.l.label)}</b> <span class="hint" style="display:inline">${esc(h.l.group)} · ${esc(h.l.biz)} · 잔액 ${won(calc(h.l).remain)}원</span><div class="nchips sm">${noteChips(h.l.note, h.l.scheduled)}</div></div><div class="fact"><button class="btn sm" data-fo="line" data-id="${h.l.id}">표에서 보기</button></div></div>`;
    const m = findMonth(h), it = h.it, st = stateOf(it, m), at = `data-k="${h.k}" data-id="${it.id}" data-m="${m}"`;
    return `<div class="frow st${st} ${it.inactive ? 'inactive' : ''}">
      <div class="fmain"><span class="badge" style="background:color-mix(in srgb,${CATCOLOR[h.k]} 18%,transparent);color:var(--text)">${h.nm}</span> <b class="nolist">${esc(it.name.replace(/\n/g, ' '))}</b>${it.inactive ? ' <span class="badge gray">미사용</span>' : ''}${it.forward ? ` <span class="fwdtag">→ ${esc(it.forward)} 전달</span>` : ''}
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
  if (focus === 'search') { const i = $('#fq'); i.focus(); i.select(); }
  else if (focus) { const el = box.querySelector(`[data-nav="${focus}"]`); if (el) { el.focus(); el.select(); } }
}
function openFind() {
  const d = $('#dlg'); if (d.open && $('#fq')) { $('#fq').focus(); $('#fq').select(); return; }
  openDlg(`<div class="findwrap"><div class="row" style="gap:8px;margin-bottom:8px"><h3 style="margin:0;flex:1">빠르게 찾기 <span class="muted" style="font-weight:400;font-size:12px">모든 요금 종류 · 일상경비</span></h3>
      <label class="muted" style="font-size:12px">확인할 월 <select id="fmon" class="fsel"><option value="auto">자동(처리 필요한 달)</option>${MON.map((x, i) => `<option value="${i}">${x}분</option>`).join('')}</select></label>
      <button class="btn sm" data-fo="close" aria-label="닫기">✕</button></div>
    <input id="fq" type="search" class="fq" placeholder="계약번호 · 납부번호 · 계좌 · 장소 · 전달처 (예: 2392, 숭림사, 790183)" autocomplete="off" value="${esc(FIND.q)}">
    <div class="hint" style="margin:6px 2px 8px">Enter: 첫 결과의 금액 칸으로 · 금액 입력 후 Enter: 저장하고 다시 검색창으로 · 띄어쓰기로 여러 낱말 · 번호의 - 는 무시 · Esc: 닫기</div>
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
    if (e.target.id === 'fq') { e.preventDefault(); const f = $('#fres .fin'); if (f) { f.focus(); f.select(); } return; }
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
  if (a === 'line') { closeDlg(); showTab('daily'); const el = document.querySelector(`input[data-f=spent][data-id="${b.dataset.id}"]`); if (el) { el.scrollIntoView({ block: 'center' }); el.focus(); const tr = el.closest('tr'); tr && tr.classList.add('flash'); setTimeout(() => tr && tr.classList.remove('flash'), 1800); } return; }
  if (a === 'util') {
    const u = UI.util; u.cat = b.dataset.k; u.sub = 'pay'; u.filter = 'all'; u.q = FIND.q; u.month = +b.dataset.m; closeDlg(); showTab('util');
  }
});
