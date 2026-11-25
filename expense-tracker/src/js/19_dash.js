/* ===== 대시보드: ① 일상경비 첫 화면 상단 카드 ② 과 요약 보고서(대시보드) ===== */

/* ① 일상경비 첫 화면: 큰 숫자 4칸 (남은 예산 · 집행률 · 구분별 잔액 · 확인할 것) */
function dashStrip(T, el, diff, alerts) {
  const th = thr(), over = T.budget > 0 && (T.rate >= th.hot || T.spent + T.sch > T.budget), lag = T.budget > 0 && !over && T.rate < el - th.low;
  const verdict = over ? ['bad', '▲', '거의 다 썼어요'] : lag ? ['warn', '▲', '쓰는 속도가 느려요'] : ['ok', '●', '잘 쓰고 있어요'];
  const top = alerts[0];
  const grp = GROUPS.map(g => { const Ls = visLines().filter(l => l.group === g); if (!Ls.length) return ''; const gt = groupTotals(Ls);
    return `<div class="dg"><span class="dgn">${g}</span><b class="dgv ${gt.remain < 0 ? 'neg' : ''}">${kor(gt.remain)}</b><span class="dgb">${barHTML(gt.spent, gt.sch, gt.budget, { noMark: false })}</span><span class="dgp">${Math.round(gt.rate)}%</span></div>`; }).join('');
  return `<div class="dstrip">
    <div class="card dk hero ${verdict[0]}"><div class="dk-l">전체 남은 예산${hiddenNote()}</div><div class="dk-big ${T.remain < 0 ? 'neg' : ''}">${won(T.remain)}<small>원</small></div>
      <div class="dk-s">${kor(T.remain)} · 지급예정 ${won(T.sch)}원 반영 후 <b>${won(T.remainAfter)}원</b></div></div>
    <div class="card dk"><div class="dk-l">집행률</div><div class="dk-big2">${pct1(T.rate)}</div>${easyBar({ rate: T.rate, gap: T.sch, budget: T.budget }, el, false, over ? 'bad' : '', !lag)}
      <div class="dk-s"><span class="ezs ${verdict[0]}"><i>${verdict[1]}</i> ${verdict[2]}</span> 경과율 ${pct1(el)} 대비 <b>${diff >= 0 ? '▲' : '▼'} ${Math.abs(diff).toFixed(1)}%p</b></div></div>
    <div class="card dk"><div class="dk-l">구분별 남은 돈</div>${grp || '<div class="muted">항목 없음</div>'}</div>
    <div class="card dk ${top ? 'alertc' : ''}"><div class="dk-l">지금 확인할 것</div><div class="dk-big2">${alerts.length}<small>건</small></div>${top ? `<div class="dk-s"><span class="badge ${top.sev >= 3 ? 'bad' : top.sev === 2 ? 'warn' : 'gray'}">${top.tag}</span> <b>${esc(top.title)}</b></div><button class="btn sm noprint" data-act="${top.act}" data-id="${top.id || ''}" data-cat="${top.cat || ''}" style="margin-top:6px">보기</button>` : '<div class="dk-s">✓ 확인할 항목이 없습니다</div>'}</div></div>`;
}

/* ② 과 요약 보고서 — 대시보드: 위에서 아래로 "결론 → 그림 → 근거" */
function renderDash(D) {
  const { T, el, th, nats, rows } = D; const st = easyState(T, el, th), diff = T.rate - el;
  const slow = [...rows].filter(r => r.budget > 0 && ['warn', 'gray'].includes(easyState(r, el, th).cls)).sort((a, b) => a.rate - b.rate || b.budget - a.budget);
  return `<div class="dash2">
    ${reconNotice()}
    <div class="d2-top">
      <div class="card d2-hero ${st.cls}"><div class="d2-q">올해 예산 (예산현액)</div><div class="d2-big">${fu(T.budget)}<small>${ul()}</small></div>
        <div class="d2-line">실집행액 <b>${fuw(T.paid)}</b> (<b>${pct1(T.rate)}</b>) · 남은 예산 <b>${fuw(T.remain)}</b></div>
        ${easyBar(T, el, true, st.cls)}
        <div class="ez-scale"><span>0</span><span class="ez-now" style="left:${Math.min(100, el).toFixed(1)}%">▲ 연 경과율 ${Math.round(el)}%</span><span>100%</span></div>
        <div class="ez-verdict"><span class="ezs ${st.cls} lg"><i>${st.sym}</i> ${st.word}</span><span class="ez-why">경과율 대비 <b>${dlt(diff)}</b>${T.dlt != null && D.prev ? ` · 직전 자료(${esc(D.prev.asOf)}) 대비 ${dlt(T.dlt)}` : ''}${T.gap > 0 ? ` · 계약·결의 후 미집행 ${fuw(T.gap)}` : ''}</span></div></div>
      <div class="card repsum"><h2>요약</h2><ul>${repSummaryText(D)}</ul></div></div>
    <div class="d2-two">${gvTreemapCard(D, 1.55)}${gvBarsCard(D, { rows: slow, limit: 7, title: '쓰는 속도가 느린 사업', note: '쓴 비율이 낮은 순', empty: '✓ 쓰는 속도가 느린 사업이 없어요' })}</div>
    <div class="d2-two"><div class="card"><h2>성질별 (통계목 대분류)</h2>${nats.map(n => repBarRow(n.name, n, true)).join('')}</div>${repTrend() || `<div class="card"><h2>집행 추이</h2><div class="muted" style="padding:10px 0">통계목별 조회를 한 번 더 불러오면(다른 날짜) 추이 그래프가 나타납니다.</div></div>`}</div>
    <div class="row noprint" style="gap:8px;justify-content:flex-end;margin:4px 0 16px"><button class="btn" data-act="repview" data-v="full">상세 표 보기</button><button class="btn primary" data-act="godoc">제출용 보고서 출력 →</button></div></div>`;
}
