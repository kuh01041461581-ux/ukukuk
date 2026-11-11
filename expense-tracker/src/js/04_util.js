/* ===== 공공요금 집행현황 탭 ===== */
const ST_LABEL = ['☐ 미수령', '☑ 수령', '– 청구없음'];
const ST_TIP = ['아직 고지서를 받지 못함', '고지서를 받음', '이번 달은 청구 없음(2천원 미만 합산 등)'];
function manKey(n) { return n >= 1e8 ? (n / 1e8).toFixed(1) + '억' : n >= 1e4 ? Math.round(n / 1e4).toLocaleString('ko-KR') + '만' : String(Math.round(n)); }

function visSeries() { const v = UI.util.series; return CATS.filter(([k]) => v[k] && S.util[k]); }
function monthlyChart() {
  const vis = visSeries();
  if (!vis.length) return '<div class="empty">위의 체크박스에서 보고 싶은 요금을 선택하세요</div>';
  const data = MON.map((_, m) => vis.map(([k]) => monthTotal(S.util[k], m)));
  const maxV = Math.max(1, ...data.map(a => Math.max(...a)));
  const mag = Math.pow(10, Math.floor(Math.log10(maxV))); let nice = mag; for (const f of [1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 10]) { if (maxV <= f * mag) { nice = f * mag; break; } }
  const W = 900, H = 250, L = 46, R = 10, Tp = 18, B = 26, iw = W - L - R, ih = H - Tp - B;
  const slot = iw / 12, grp = slot * 0.8, bw = grp / vis.length, showVal = vis.length === 1;
  let g = '';
  for (let i = 0; i <= 4; i++) { const y = Tp + ih - ih * i / 4; g += `<line class="gd" x1="${L}" x2="${W - R}" y1="${y}" y2="${y}"/><text x="${L - 6}" y="${y + 4}" text-anchor="end">${manKey(nice * i / 4)}</text>`; }
  data.forEach((a, m) => {
    const gx = L + slot * m + (slot - grp) / 2;
    a.forEach((v, i) => {
      const k = vis[i][0], nm = vis[i][1]; const h = v / nice * ih; const x = gx + i * bw + 1, w = Math.max(2, bw - 2);
      if (v) {
        const dx = Math.min(8, w * 0.32), dy = dx * 0.7, fw = w - dx, y = Tp + ih - h, c = CATCOLOR[k];
        g += `<g class="bar3d" data-tip="${MON[m]}분 ${nm}\n${won(v)}원">` +
          `<rect x="${x}" y="${y}" width="${fw}" height="${Math.max(1, h)}" rx="1.5" fill="${c}"/><rect x="${x}" y="${y}" width="${fw}" height="${Math.max(1, h)}" rx="1.5" fill="url(#gFront)"/>` +
          `<polygon points="${x + fw},${y} ${x + fw + dx},${y - dy} ${x + fw + dx},${y + h - dy} ${x + fw},${y + h}" fill="${c}"/><polygon points="${x + fw},${y} ${x + fw + dx},${y - dy} ${x + fw + dx},${y + h - dy} ${x + fw},${y + h}" fill="#0a1228" fill-opacity=".30"/>` +
          `<polygon points="${x},${y} ${x + dx},${y - dy} ${x + fw + dx},${y - dy} ${x + fw},${y}" fill="${c}"/><polygon points="${x},${y} ${x + dx},${y - dy} ${x + fw + dx},${y - dy} ${x + fw},${y}" fill="#fff" fill-opacity=".42"/></g>`;
        if (showVal) g += `<text x="${x + w / 2}" y="${y - dy - 5}" text-anchor="middle" style="fill:var(--text);font-weight:600">${manKey(v)}</text>`;
      }
    });
    g += `<text x="${L + slot * (m + 0.5)}" y="${H - 8}" text-anchor="middle">${m + 1}월</text>`;
  });
  const defs = '<defs><linearGradient id="gFront" x1="0" x2="1" y1="0" y2="0"><stop offset="0" stop-color="#fff" stop-opacity=".34"/><stop offset=".45" stop-color="#fff" stop-opacity=".06"/><stop offset="1" stop-color="#0a1228" stop-opacity=".22"/></linearGradient></defs>';
  return `<svg viewBox="0 0 ${W} ${H}" width="100%" role="img" aria-label="월별 공공요금 지출 막대그래프(${vis.map(x => x[1]).join('·')})">${defs}${g}<line class="ax" x1="${L}" x2="${W - R}" y1="${Tp + ih}" y2="${Tp + ih}"/></svg>`;
}

function renderUtil() {
  const P = $('#p-util'); const u = UI.util; const sec = S.util[u.cat]; if (!sec) { P.innerHTML = '<div class="empty">공공요금 데이터가 없습니다</div>'; return; }
  if (u.month == null) u.month = defaultMonth(sec);
  const m = u.month, dm = defaultMonth(sec);
  const grand = sum(CATS, ([k]) => S.util[k] ? catTotal(S.util[k]) : 0);
  let h = `<div class="cat-cards">`;
  for (const [k, nm] of CATS) {
    const s = S.util[k]; if (!s) continue; const tot = catTotal(s), bud = S.utilBudget[k];
    h += `<div class="cat-card" style="--c:${CATCOLOR[k]}"><div class="row" style="justify-content:space-between"><b>${nm}</b><span class="muted">${s.items.filter(i => !i.inactive).length}곳</span></div>
      <div class="big">${won(tot)}<span style="font-size:13px;font-weight:400"> 원</span></div>
      <div class="row" style="margin:6px 0"><span class="muted">연 예산</span><input class="cellin" style="width:130px;border-color:var(--line)" data-u="budget" data-cat="${k}" data-nav="ub-${k}" value="${bud ? won(bud) : ''}" placeholder="미입력" inputmode="numeric"><span class="muted">원</span></div>
      ${bud ? `${barHTML(tot, 0, bud)}<div class="row" style="justify-content:space-between;margin-top:4px"><b>${pct1(pct(tot, bud))}</b><span class="muted">잔액 ${won(bud - tot)}원</span></div>` : '<div class="hint">예산을 입력하면 집행률 막대가 나옵니다 ([예산서 불러오기]에서 자동 계산 가능)</div>'}
      </div>`;
  }
  h += `</div>
  <div class="card"><h2>월별 공공요금 지출 <span class="muted" style="font-weight:400;font-size:12px">체크한 요금만 막대로 보입니다 · 선택 합계 ${won(sum(visSeries(), ([k]) => catTotal(S.util[k])))}원</span></h2>
    <div class="legend noprint" role="group" aria-label="표시할 요금">${CATS.map(([k, nm]) => S.util[k] ? `<label class="serchk" style="--c:${CATCOLOR[k]}"><input type="checkbox" data-u="series" data-cat="${k}" ${u.series[k] ? 'checked' : ''}><i></i>${nm}</label>` : '').join('')}
      <button class="btn sm" data-act="series-all">3개 모두</button></div>
    ${monthlyChart()}
    <details class="fold"><summary>표로 보기</summary><div class="tbl-wrap"><table class="t"><thead><tr><th>구분</th>${MON.map(x => `<th>${x}</th>`).join('')}<th>합계</th></tr></thead><tbody>
    ${CATS.map(([k, nm]) => { const s = S.util[k]; if (!s) return ''; return `<tr><td>${nm}</td>${MON.map((_, i) => `<td class="num">${won(monthTotal(s, i))}</td>`).join('')}<td class="num"><b>${won(catTotal(s))}</b></td></tr>`; }).join('')}
    <tr class="tot"><td>합계</td>${MON.map((_, i) => `<td class="num">${won(sum(CATS, ([k]) => S.util[k] ? monthTotal(S.util[k], i) : 0))}</td>`).join('')}<td class="num">${won(grand)}</td></tr></tbody></table></div></details>
  </div>

  <div class="card">
    <div class="row" style="justify-content:space-between">
      <div class="pills" role="group" aria-label="요금 종류">${CATS.map(([k, nm]) => `<button class="pill" aria-pressed="${k === u.cat}" data-act="cat" data-v="${k}">${nm}</button>`).join('')}</div>
      <div class="pills" role="group" aria-label="보기 방식"><button class="pill" aria-pressed="${u.mode === 'month'}" data-act="mode" data-v="month">월별 입력</button><button class="pill" aria-pressed="${u.mode === 'year'}" data-act="mode" data-v="year">연간 표</button></div>
    </div>
    ${(sec.tips || []).length ? `<p class="muted" style="margin:8px 0 0">※ ${esc(sec.tips.join(' · '))}</p>` : ''}
    <div class="row noprint" style="margin-top:10px">
      <input type="search" data-u="q" data-nav="q" value="${esc(u.q)}" placeholder="항목·번호 검색" style="border:1px solid var(--line);border-radius:8px;padding:6px 10px;background:var(--surface-2);min-width:200px">
      <label class="row" style="gap:4px"><input type="checkbox" data-u="inactive" ${u.showInactive ? 'checked' : ''}> 미사용·사용자납부 포함</label>
      <span class="spacer"></span>
      <button class="btn sm" data-act="additem">＋ 항목 추가</button><button class="btn sm" data-act="tsv">표 복사</button><button class="btn sm" data-act="printu">인쇄</button>
    </div>
    <div id="u-body" style="margin-top:12px">${u.mode === 'month' ? monthView(sec, m, dm) : yearView(sec, dm)}</div>
  </div>`;
  if ((sec.forwards || []).length) {
    h += `<div class="card"><details class="fold"><summary>${sec.title} — 타 계·부서 전달처 안내표 (${sec.forwards.length}건)</summary>
      <div class="tbl-wrap" style="margin-top:8px"><table class="t"><thead><tr><th>관리 항목</th><th>번호</th><th>전달처</th><th>비고</th></tr></thead><tbody>
      ${sec.forwards.map(f => `<tr><td>${esc(f.name)}</td><td class="nolist">${esc(f.numbers)}</td><td>${esc(f.to)}</td><td>${esc(f.memo)}</td></tr>`).join('')}</tbody></table></div></details></div>`;
  }
  P.innerHTML = h; restoreFocus();
  const q = $('[data-nav="q"]', P); if (q && document.activeElement === q) { q.setSelectionRange(q.value.length, q.value.length); }
}

function visibleItems(sec) {
  const u = UI.util; const q = u.q.trim().replace(/\s/g, '').toLowerCase();
  return sec.items.filter(i => (u.showInactive || !i.inactive) && (!q || (i.name + i.no1 + i.no2 + i.memo + (i.forward || '')).replace(/\s/g, '').toLowerCase().includes(q)));
}
function numLines(sec, it) {
  const c = (lbl, v) => v ? `<span>${lbl} <b data-copy="${esc(v)}" data-tip="누르면 복사">${esc(v)}</b></span>` : '';
  return `<div class="numline">${it.carrier ? `<span class="badge gray">${esc(it.carrier)}</span>` : ''}${c(sec.no1Label, it.no1)}${c(sec.no2Label, it.no2)}</div>`;
}
function itemCell(sec, it) {
  return `<div class="nolist"><b>${esc(it.name)}</b></div>${numLines(sec, it)}
    ${it.forward ? `<span class="badge warn">전달처: ${esc(it.forward)}</span>` : ''}${it.inactive ? '<span class="badge gray">미사용/사용자 납부</span>' : ''}
    ${it.memo ? `<span class="hint">${esc(it.memo)}</span>` : ''}`;
}

function monthView(sec, m, dm) {
  const u = UI.util; const items = visibleItems(sec); const act = sec.items.filter(i => !i.inactive);
  const cnt = s => act.filter(i => (i.rcv[m] || 0) === s).length;
  const rows = items.filter(i => u.filter !== 'miss' || (!i.inactive && (i.rcv[m] || 0) === 0));
  const tot = monthTotal(sec, m), prev = m > 0 ? monthTotal(sec, m - 1) : null, pyT = sum(sec.items, i => i.py ? i.py[m] : 0);
  let h = `<div class="pills mo-pills" role="group" aria-label="월 선택">${MON.map((nm, i) => { const mc = missingCount(sec, i); return `<button class="pill" aria-pressed="${i === m}" data-act="month" data-v="${i}">${nm}분${i <= dm && mc ? `<span class="n" data-tip="미수령 ${mc}건">${mc}</span>` : ''}</button>`; }).join('')}</div>
    <div class="row" style="margin:12px 0"><b style="font-size:15px">${MON[m]}분</b><span class="badge good">수령 ${cnt(1)}</span><span class="badge warn">미수령 ${cnt(0)}</span><span class="badge gray">청구없음 ${cnt(2)}</span>
    <span>입력 합계 <b>${won(tot)}원</b></span>${prev != null ? `<span class="muted">전월 ${won(prev)}원</span>` : ''}${pyT ? `<span class="muted">전년 같은 달 ${won(pyT)}원</span>` : ''}
    <span class="spacer"></span><div class="pills"><button class="pill" aria-pressed="${u.filter === 'all'}" data-act="filter" data-v="all">전체</button><button class="pill" aria-pressed="${u.filter === 'miss'}" data-act="filter" data-v="miss">미수령만</button></div></div>`;
  if (!rows.length) return h + '<div class="empty">표시할 항목이 없습니다</div>';
  h += `<div class="tbl-wrap scroll-y"><table class="t"><thead><tr><th>고지서</th><th>관리 항목 · 번호</th><th>${MON[m]}분 금액(원)</th><th>전월</th><th>전년 같은 달</th></tr></thead><tbody>`;
  for (const it of rows) {
    const s = it.rcv[m] || 0, a = it.amt[m], p = it.py ? it.py[m] : null, pv = m > 0 ? it.amt[m - 1] : null;
    const diff = a && p ? (a - p) / p * 100 : null;
    h += `<tr class="${it.inactive ? 'inactive' : ''}"><td style="text-align:center"><button class="st-btn s${s}" data-act="cycle" data-id="${it.id}" data-m="${m}" data-tip="${ST_TIP[s]} (누르면 상태 전환)">${ST_LABEL[s]}</button></td>
      <td class="name">${itemCell(sec, it)}<button class="btn sm noprint" data-act="edititem" data-id="${it.id}">편집</button></td>
      <td style="width:150px"><input class="cellin" data-u="amt" data-id="${it.id}" data-m="${m}" data-nav="a-${it.id}" value="${a == null ? '' : won(a)}" inputmode="numeric" aria-label="${esc(it.name)} ${MON[m]}분 금액"></td>
      <td class="num muted">${pv ? won(pv) : ''}</td>
      <td class="num muted">${p ? won(p) : ''}${diff != null && Math.abs(diff) >= 20 ? `<span class="hint ${diff > 0 ? 'diff-up' : 'diff-dn'}">${diff > 0 ? '▲' : '▼'} ${Math.abs(diff).toFixed(0)}%</span>` : ''}</td></tr>`;
  }
  return h + `<tr class="tot"><td></td><td>합계 (표시 항목)</td><td class="num">${won(sum(rows, i => i.amt[m]))}</td><td class="num">${prev != null ? won(sum(rows, i => i.amt[m - 1])) : ''}</td><td class="num">${won(sum(rows, i => i.py ? i.py[m] : 0))}</td></tr></tbody></table></div>`;
}

function yearView(sec, dm) {
  const rows = visibleItems(sec);
  let h = `<p class="muted" style="margin:0 0 8px">칸 앞의 작은 네모가 고지서 상태입니다 — <span class="ycell"><span class="dot s0" style="display:inline-block"></span></span> 미수령 · <span class="ycell"><span class="dot s1" style="display:inline-block"></span></span> 수령 · <span class="ycell"><span class="dot s2" style="display:inline-block"></span></span> 청구없음 (누르면 전환)</p>
  <div class="tbl-wrap scroll-y"><table class="t"><thead><tr><th class="sticky-l" style="min-width:230px">관리 항목</th><th>’${String(S.year - 1).slice(2)}.12</th>${MON.map((x, i) => `<th>${x}분</th>`).join('')}<th>합계</th></tr></thead><tbody>
  <tr class="tot"><td class="sticky-l" style="background:var(--accent-soft)">합계 (전체)</td><td class="num">${won(sum(sec.items, i => i.dec))}</td>${MON.map((_, i) => `<td class="num">${won(monthTotal(sec, i))}</td>`).join('')}<td class="num">${won(catTotal(sec))}</td></tr>`;
  for (const it of rows) {
    h += `<tr class="${it.inactive ? 'inactive' : ''}"><td class="sticky-l name"><div class="nolist"><b>${esc(it.name)}</b></div>${numLines(sec, it)}${it.memo ? `<span class="hint">${esc(it.memo)}</span>` : ''}<button class="btn sm noprint" data-act="edititem" data-id="${it.id}">편집</button></td>
      <td class="num muted">${it.dec ? won(it.dec) : ''}</td>
      ${MON.map((_, i) => `<td><div class="ycell"><button class="dot s${it.rcv[i] || 0}" data-act="cycle" data-id="${it.id}" data-m="${i}" data-tip="${MON[i]}분 ${ST_TIP[it.rcv[i] || 0]}" aria-label="${MON[i]} 고지서 상태"></button><input class="cellin" data-u="amt" data-id="${it.id}" data-m="${i}" data-nav="y${i}-${it.id}" value="${it.amt[i] == null ? '' : won(it.amt[i])}" inputmode="numeric"></div></td>`).join('')}
      <td class="num"><b>${won(itemTotal(it))}</b></td></tr>`;
  }
  return h + '</tbody></table></div>';
}

/* 이벤트 */
const PU = $('#p-util');
PU.addEventListener('change', e => {
  const t = e.target, u = t.dataset.u; if (!u) return;
  if (u === 'inactive') { UI.util.showInactive = t.checked; renderUtil(); return; }
  if (u === 'series') { UI.util.series[t.dataset.cat] = t.checked; if (!visSeries().length) { UI.util.series[t.dataset.cat] = true; toast('최소 한 개는 선택해야 합니다'); } renderUtil(); return; }
  if (u === 'q') return;
  const v = parseNum(t.value);
  if (u === 'budget') { S.utilBudget[t.dataset.cat] = v && !isNaN(v) ? v : null; save(); renderUtil(); return; }
  const sec = S.util[UI.util.cat]; const it = sec.items.find(x => x.id === t.dataset.id); if (!it) return; const m = +t.dataset.m;
  if (Number.isNaN(v)) { toast('숫자나 계산식만 입력할 수 있습니다'); renderUtil(); return; }
  setAmt(it, m, v); save(); renderUtil();
});
PU.addEventListener('input', e => {
  if (e.target.dataset.u === 'q') { UI.util.q = e.target.value; pendingFocus = 'q'; renderUtil(); }
});
PU.addEventListener('keydown', e => { if (e.target.matches('input.cellin')) { const n = e.target.dataset.nav; enterNext(e, n.startsWith('y') ? n.split('-')[0] + '-' : n.slice(0, 2)); } });
PU.addEventListener('focusin', e => { if (e.target.matches('input.cellin')) { const v = parseNum(e.target.value); e.target.value = v == null || isNaN(v) ? '' : String(v); e.target.select(); } });
PU.addEventListener('click', e => {
  const cp = e.target.closest('[data-copy]'); if (cp) { copyText(cp.dataset.copy, '복사했습니다: ' + cp.dataset.copy); return; }
  const b = e.target.closest('[data-act]'); if (!b) return; const a = b.dataset.act, u = UI.util;
  if (a === 'series-all') { CATS.forEach(([k]) => UI.util.series[k] = true); renderUtil(); return; }
  if (a === 'cat') { u.cat = b.dataset.v; u.month = null; renderUtil(); }
  else if (a === 'mode') { u.mode = b.dataset.v; renderUtil(); }
  else if (a === 'month') { u.month = +b.dataset.v; renderUtil(); }
  else if (a === 'filter') { u.filter = b.dataset.v; renderUtil(); }
  else if (a === 'cycle') { const it = S.util[u.cat].items.find(x => x.id === b.dataset.id), m = +b.dataset.m; it.rcv[m] = ((it.rcv[m] || 0) + 1) % 3; save(); renderUtil(); }
  else if (a === 'additem') itemDialog(null);
  else if (a === 'edititem') itemDialog(b.dataset.id);
  else if (a === 'tsv') copyText(utilTSV(S.util[u.cat]), '표를 복사했습니다 — 엑셀에 붙여넣으세요');
  else if (a === 'printu') { $$('.tab-panel').forEach(p => p.classList.toggle('printing', p.id === 'p-util')); window.print(); }
});
function utilTSV(sec) {
  const rows = [['관리 항목', sec.no1Label, sec.no2Label, ...MON.map(x => x + '분'), '합계']];
  for (const it of sec.items) rows.push([it.name.replace(/\n/g, ' '), it.no1, it.no2, ...it.amt.map(v => v == null ? '' : v), itemTotal(it)]);
  rows.push(['합계', '', '', ...MON.map((_, i) => monthTotal(sec, i)), catTotal(sec)]);
  return rows.map(r => r.join('\t')).join('\n');
}

function itemDialog(id) {
  const sec = S.util[UI.util.cat]; const isNew = !id;
  const it = isNew ? { id: uid(UI.util.cat), name: '', no1: '', no2: '', carrier: '', memo: '', inactive: false, dec: null, amt: Array(12).fill(null), rcv: Array(12).fill(0), py: null, forward: '' } : sec.items.find(x => x.id === id);
  openDlg(`<h3>${isNew ? '항목 추가' : '항목 편집'} — ${esc(sec.title)}</h3>
    <label class="fld">관리 항목 (장소·용도)<textarea id="in" rows="2">${esc(it.name)}</textarea></label>
    <label class="fld">${esc(sec.no1Label)}<input id="n1" value="${esc(it.no1)}"></label>
    <label class="fld">${esc(sec.no2Label)}<input id="n2" value="${esc(it.no2)}"></label>
    ${UI.util.cat === 'tel' ? `<label class="fld">통신사<input id="ca" value="${esc(it.carrier)}"></label>` : ''}
    <label class="fld">전달처 (고지서를 넘겨줄 계·부서)<input id="fw" value="${esc(it.forward || '')}"></label>
    <label class="fld">메모<textarea id="me" rows="2">${esc(it.memo)}</textarea></label>
    <label class="row" style="gap:6px;margin-bottom:8px"><input type="checkbox" id="ia" ${it.inactive ? 'checked' : ''}> 미사용 / 사용자 납부 (수령 확인·미수령 집계에서 제외)</label>
    <div class="row" style="justify-content:space-between;margin-top:10px"><span>${isNew ? '' : '<button class="btn danger" data-d="del">삭제</button>'}</span>
    <span class="row"><button class="btn" data-d="cancel">취소</button><button class="btn primary" data-d="save">저장</button></span></div>`, d => {
    d.onclick = ev => {
      const b = ev.target.closest('[data-d]'); if (!b) return; const k = b.dataset.d;
      if (k === 'cancel') closeDlg();
      else if (k === 'del') { if (confirm('이 항목을 삭제할까요? (월별 금액도 함께 삭제됩니다)')) { sec.items = sec.items.filter(x => x.id !== it.id); save(); closeDlg(); renderUtil(); } }
      else if (k === 'save') {
        it.name = $('#in', d).value.trim() || '(이름 없음)'; it.no1 = $('#n1', d).value.trim(); it.no2 = $('#n2', d).value.trim(); it.forward = $('#fw', d).value.trim(); it.memo = $('#me', d).value.trim();
        if ($('#ca', d)) it.carrier = $('#ca', d).value.trim();
        const was = it.inactive; it.inactive = $('#ia', d).checked;
        if (it.inactive && !was) it.rcv = it.rcv.map(x => x === 0 ? 2 : x);
        if (!it.inactive && was) it.rcv = it.rcv.map((x, i) => x === 2 && !it.amt[i] ? 0 : x);
        if (isNew) sec.items.push(it); save(); closeDlg(); renderUtil();
      }
    };
  });
}
