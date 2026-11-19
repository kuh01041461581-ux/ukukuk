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
function easyWaffle(T) {
  const spent = Math.min(100, Math.round(T.rate)), gap = Math.max(0, Math.min(100 - spent, Math.round(T.budget > 0 ? T.gap / T.budget * 100 : 0)));
  let h = ''; for (let i = 0; i < 100; i++) h += `<i class="${i < spent ? 'a' : i < spent + gap ? 'b' : 'c'}"></i>`;
  return `<div class="waffle" role="img" aria-label="네모 100칸 중 ${spent}칸 사용">${h}</div>`;
}
function renderEasy(D) {
  const { T, el, th } = D; const st = easyState(T, el, th);
  const spentN = Math.min(100, Math.round(T.rate)), gapN = Math.max(0, Math.min(100 - spentN, Math.round(T.budget > 0 ? T.gap / T.budget * 100 : 0))), leftN = 100 - spentN - gapN;
  const sorted = [...D.rows].filter(r => r.budget > 0 || r.paid > 0);
  const rank = r => { const s = easyState(r, el, th); return { bad: 0, warn: 1, gray: 2, ok: 3 }[s.cls]; };
  sorted.sort((a, b) => rank(a) - rank(b) || b.budget - a.budget);
  const showAll = UI.rep.easyAll, list = showAll ? sorted : sorted.slice(0, 8);
  const row = r => { const s = easyState(r, el, th); return `<div class="ez ${s.cls}"><div class="ezn">${esc(r.biz)}</div>${easyBar(r, el)}<div class="ezp"><b>${Math.round(r.rate)}%</b><span class="ezs ${s.cls}"><i>${s.sym}</i> ${s.word}</span></div><div class="ezd">쓴 돈 <b>${kor(r.paid)}</b> · 남은 돈 <b>${kor(r.remain)}</b> <span class="muted">(전체 ${kor(r.budget)})</span></div></div>`; };
  return `<div class="easy">
    <div class="ez-hero ${st.cls}">
      <div class="ez-q">올해 쓸 수 있는 돈</div><div class="ez-big">${kor(T.budget)}</div>
      <div class="ez-line">그중 <b>${Math.round(T.rate)}%</b>를 썼어요 · 남은 돈은 <b>${kor(T.remain)}</b></div>
      ${easyBar(T, el, true)}
      <div class="ez-scale"><span>0</span><span class="ez-now" style="left:${Math.min(100, el).toFixed(1)}%">▲ 오늘까지 지난 기간 ${Math.round(el)}%</span><span>100%</span></div>
      <div class="ez-verdict"><span class="ezs ${st.cls} lg"><i>${st.sym}</i> ${st.word}</span><span class="ez-why">${st.cls === 'warn' ? `지금쯤 ${Math.round(el)}%는 썼어야 하는데 ${Math.round(T.rate)}%만 썼어요` : st.cls === 'ok' ? `오늘까지 지난 기간(${Math.round(el)}%)에 맞게 쓰고 있어요` : st.cls === 'bad' ? '예산을 거의 다 썼거나 넘었어요. 확인이 필요해요' : ''}</span></div>
    </div>
    <div class="ez-two"><div class="card ez-wf"><h2>돈 100칸으로 보기</h2>${easyWaffle(T)}<div class="ez-leg"><div><i class="a"></i> 쓴 돈 <b>${spentN}칸</b></div>${gapN ? `<div><i class="b"></i> 쓰기로 약속한 돈 <b>${gapN}칸</b></div>` : ''}<div><i class="c"></i> 남은 돈 <b>${leftN}칸</b></div></div><div class="muted" style="font-size:12px;margin-top:6px">네모 100칸이 올해 쓸 수 있는 돈 전체예요.</div></div>
      <div class="card ez-sum"><h2>한마디로</h2><ul class="ez-ul"><li><b>쓴 돈</b> ${kor(T.paid)}</li><li><b>남은 돈</b> ${kor(T.remain)}</li>${T.gap > 0 ? `<li><b>쓰기로 약속만 한 돈</b> ${kor(T.gap)} <span class="muted">(계약·결의는 했고 아직 안 나간 돈)</span></li>` : ''}<li><b>사업</b> ${sorted.length}개 중 <b>주의가 필요한 사업 ${sorted.filter(r => ['bad', 'warn'].includes(easyState(r, el, th).cls)).length}개</b></li></ul>
      ${D.prev && T.dlt != null ? `<div class="ez-chg">지난번 자료(${esc(D.prev.asOf)})보다 <b>${T.dlt >= 0 ? '+' : '−'}${Math.abs(T.dlt).toFixed(1)}%p</b> 더 썼어요</div>` : ''}</div></div>
    <div class="card"><div class="row" style="justify-content:space-between;margin-bottom:6px"><h2 style="margin:0">사업별로 볼까요? <span class="muted" style="font-weight:400;font-size:13px">신경 써야 할 사업이 위에 있어요${showAll ? '' : ` · ${sorted.length}개 중 ${list.length}개`}</span></h2>${sorted.length > 8 ? `<button class="btn sm noprint" data-act="repeasyall">${showAll ? '접기' : '전부 보기'}</button>` : ''}</div>
      <div class="ez-legend"><span>■ 파란 막대 = 쓴 돈</span><span>세로선 = 오늘까지 지난 기간</span><span class="ezs ok"><i>●</i> 잘 쓰고 있어요</span><span class="ezs warn"><i>▲</i> 느려요</span><span class="ezs bad"><i>▲</i> 거의 다 썼어요</span></div>
      ${list.map(row).join('')}</div>
    <div class="muted rep-foot">※ 쉬운 말 풀이: <b>올해 쓸 수 있는 돈</b>=예산현액, <b>쓴 돈</b>=집행액, <b>남은 돈</b>=잔액, <b>쓰기로 약속한 돈</b>=원인행위 중 아직 집행 전인 금액, <b>오늘까지 지난 기간</b>=연 경과율. 정확한 숫자와 표는 [요약]·[상세]에서 볼 수 있어요.</div></div>`;
}
