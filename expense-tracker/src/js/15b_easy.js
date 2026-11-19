/* ===== 쉬운 보기: 누구나 한눈에 — 네모 100칸, 큰 막대, 쉬운 말, 신호(기호+글자) ===== */
/* 큰 돈을 읽기 쉽게: 343,300,000 → 3억 4,330만 원 */
function kor(n) {
  n = Math.round(n || 0); const neg = n < 0; n = Math.abs(n);
  const eok = Math.floor(n / 1e8), man = Math.floor((n % 1e8) / 1e4), rest = n % 1e4; const out = [];
  if (eok) out.push(eok.toLocaleString('ko-KR') + '억'); if (man) out.push(man.toLocaleString('ko-KR') + '만');
  if (!eok && !man) out.push(rest.toLocaleString('ko-KR')); else if (!eok && man < 10 && rest) out.push(rest.toLocaleString('ko-KR'));
  return (neg ? '−' : '') + out.join(' ') + ' 원';
}
/* 상태를 쉬운 말과 기호로 (색만으로 구분하지 않음) */
function easyState(r, el, th) {
  if (!(r.budget > 0)) return { cls: 'gray', sym: '–', word: '예산 없음' };
  if (r.remain < 0) return { cls: 'bad', sym: '■', word: '예산을 넘었어요' };
  if (r.paid <= 0) return { cls: 'gray', sym: '○', word: '아직 안 썼어요' };
  if (r.rate >= th.hot) return { cls: 'bad', sym: '▲', word: '거의 다 썼어요' };
  if (r.rate < el - th.low) return { cls: 'warn', sym: '▲', word: '쓰는 속도가 느려요' };
  return { cls: 'ok', sym: '●', word: '잘 쓰고 있어요' };
}
function easyBar(r, el, big) {
  const w = v => Math.max(0, Math.min(100, v)).toFixed(2);
  const gapW = r.budget > 0 ? Math.min(100 - Math.min(100, r.rate), r.gap / r.budget * 100) : 0;
  return `<div class="ezbar${big ? ' big' : ''}" role="img" aria-label="쓴 비율 ${pct1(r.rate)}"><i class="f" style="width:${w(r.rate)}%"></i><i class="g" style="left:${w(r.rate)}%;width:${w(gapW)}%"></i><b class="mk" style="left:${w(el)}%"></b></div>`;
}
function renderEasy(D) {
  const { T, el, th } = D; const st = easyState(T, el, th);
  const sorted = [...D.rows].filter(r => r.budget > 0 || r.paid > 0).sort((a, b) => (a.budget > 0 ? a.rate : 999) - (b.budget > 0 ? b.rate : 999) || b.budget - a.budget);
  const slow = sorted.filter(r => ['warn', 'gray'].includes(easyState(r, el, th).cls) && r.budget > 0);
  const hotN = sorted.filter(r => easyState(r, el, th).cls === 'bad').length;
  const showAll = UI.rep.easyAll, base = showAll ? sorted : slow, list = base.slice(0, showAll ? 999 : 10);
  const row = r => { const s = easyState(r, el, th); return `<div class="ez ${s.cls}"><div class="ezn">${esc(r.biz)}</div>${easyBar(r, el)}<div class="ezp"><b>${Math.round(r.rate)}%</b><span class="ezs ${s.cls}"><i>${s.sym}</i> ${s.word}</span></div><div class="ezd">쓴 돈 <b>${kor(r.paid)}</b> · 남은 돈 <b>${kor(r.remain)}</b> <span class="muted">(전체 ${kor(r.budget)})</span></div></div>`; };
  return `<div class="easy">
    <div class="ez-hero ${st.cls}">
      <div class="ez-q">올해 쓸 수 있는 돈</div><div class="ez-big">${kor(T.budget)}</div>
      <div class="ez-line">그중 <b>${Math.round(T.rate)}%</b>를 썼어요 · 남은 돈은 <b>${kor(T.remain)}</b></div>
      ${easyBar(T, el, true)}
      <div class="ez-scale"><span>0</span><span class="ez-now" style="left:${Math.min(100, el).toFixed(1)}%">▲ 오늘까지 지난 기간 ${Math.round(el)}%</span><span>100%</span></div>
      <div class="ez-verdict"><span class="ezs ${st.cls} lg"><i>${st.sym}</i> ${st.word}</span><span class="ez-why">${st.cls === 'warn' ? `지금쯤 ${Math.round(el)}%는 썼어야 하는데 ${Math.round(T.rate)}%만 썼어요` : st.cls === 'ok' ? `오늘까지 지난 기간(${Math.round(el)}%)에 맞게 쓰고 있어요` : st.cls === 'bad' ? '예산을 거의 다 썼거나 넘었어요. 확인이 필요해요' : ''}</span></div>
    </div>
    <div class="ez-tiles"><div class="ez-tile"><span>쓴 돈</span><b>${kor(T.paid)}</b></div><div class="ez-tile"><span>남은 돈</span><b>${kor(T.remain)}</b></div>${T.gap > 0 ? `<div class="ez-tile"><span>쓰기로 약속만 한 돈</span><b>${kor(T.gap)}</b></div>` : ''}<div class="ez-tile ${slow.length ? 'w' : ''}"><span>쓰는 속도가 느린 사업</span><b>${slow.length}개</b></div>${hotN ? `<div class="ez-tile b"><span>거의 다 쓴 사업</span><b>${hotN}개</b></div>` : ''}</div>
    ${D.prev && T.dlt != null ? `<div class="ez-chg">지난번 자료(${esc(D.prev.asOf)})보다 <b>${T.dlt >= 0 ? '+' : '−'}${Math.abs(T.dlt).toFixed(1)}%p</b> 더 썼어요</div>` : ''}
    <div class="card"><div class="row" style="justify-content:space-between;margin-bottom:6px"><h2 style="margin:0">${showAll ? '전체 사업' : '쓰는 속도가 느린 사업'} <span class="muted" style="font-weight:400;font-size:13px">쓴 비율이 낮은 사업부터 보여요${showAll ? '' : ` · ${slow.length}개${slow.length > 10 ? ' 중 10개' : ''}`}</span></h2><button class="btn sm noprint" data-act="repeasyall">${showAll ? '느린 사업만 보기' : `전체 ${sorted.length}개 사업 보기`}</button></div>
      <div class="ez-legend"><span>■ 파란 막대 = 쓴 돈</span><span>세로선 = 오늘까지 지난 기간</span><span class="ezs ok"><i>●</i> 잘 쓰고 있어요</span><span class="ezs warn"><i>▲</i> 느려요</span><span class="ezs bad"><i>▲</i> 거의 다 썼어요</span></div>
      ${list.map(row).join('') || '<div class="empty" style="padding:16px;font-size:18px">✓ 쓰는 속도가 느린 사업이 없어요</div>'}</div>
    <div class="muted rep-foot">※ 쉬운 말 풀이: <b>올해 쓸 수 있는 돈</b>=예산현액, <b>쓴 돈</b>=집행액, <b>남은 돈</b>=잔액, <b>쓰기로 약속한 돈</b>=원인행위 중 아직 집행 전인 금액, <b>오늘까지 지난 기간</b>=연 경과율, <b>느린 사업</b>=쓴 비율이 오늘까지 지난 기간보다 많이 낮거나 아직 안 쓴 사업. 정확한 숫자와 표는 [요약]·[상세]에서 볼 수 있어요.</div></div>`;
}
