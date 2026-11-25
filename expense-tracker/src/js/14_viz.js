/* ===== 한눈에 보는 시각화: 공공요금 월별 처리 현황(히트맵) · 전체 사업 진척 요약(집행률 분포 도넛 + 잔액 TOP) ===== */

/* 월별 처리 현황: 종류 × 월. 칸마다 납부 완료 건수/전체, 색 + 기호로 상태 구분 */
function procHeatmap() {
  const rows = CATS.filter(([k]) => S.util[k]).map(([k, nm]) => {
    const its = S.util[k].items.filter(i => !i.inactive);
    const cells = MON.map((_, m) => {
      let paid = 0, unp = 0, miss = 0;
      its.forEach(i => { const st = stateOf(i, m); if (st === 3) paid++; else if (st === 1) unp++; else if (st === 0) miss++; });
      const tot = paid + unp + miss; let cls = 'none', sym = '';
      if (tot === 0) { cls = 'none'; sym = '–'; }
      else if (paid + unp === 0) { cls = 'wait'; sym = ''; }          // 아직 아무것도 안 들어온 달(앞으로 올 달)
      else if (miss > 0) { cls = 'miss'; sym = '!'; }                  // 일부 미수령
      else if (unp > 0) { cls = 'unp'; sym = '●'; }                    // 전부 수령, 납부 대기
      else { cls = 'ok'; sym = '✓'; }
      return { m, paid, unp, miss, tot, cls, sym };
    });
    return { k, nm, cells };
  });
  const cur = (() => { const d = new Date(S.asOf || Date.now()); return d.getFullYear() === S.year ? d.getMonth() : -1; })();
  return `<div class="card"><h2>월별 처리 현황 <span class="muted" style="font-weight:400;font-size:12px">칸을 누르면 그 달의 납부 확인으로 이동 · 숫자는 납부 완료 / 전체</span></h2>
    <div class="legend noprint" style="margin-bottom:8px"><span class="hmk ok">✓ 모두 납부</span><span class="hmk unp">● 수령 완료·납부 대기</span><span class="hmk miss">! 미수령 있음</span><span class="hmk wait">아직 안 온 달</span></div>
    <div class="hm"><div class="hmrow hmhead"><span class="hmlab"></span>${MON.map((n, m) => `<span class="${m === cur ? 'cur' : ''}">${n}</span>`).join('')}</div>
    ${rows.map(r => `<div class="hmrow"><span class="hmlab"><i style="background:${CATCOLOR[r.k]}"></i>${r.nm}</span>${r.cells.map(c => `<button class="hmc ${c.cls}" data-act="hm" data-k="${r.k}" data-m="${c.m}" data-tip="${r.nm} ${MON[c.m]}분\n납부 ${c.paid} · 수령·미납 ${c.unp} · 미수령 ${c.miss}">${c.tot ? (c.cls === 'ok' ? `<b>${c.sym}</b>` : `<b>${c.sym}</b>${c.paid}/${c.tot}`) : '–'}</button>`).join('')}</div>`).join('')}</div></div>`;
}

/* 전체 사업 진척 요약 */
function bizVizHTML(rows, T) {
  const b = [
    { name: '미집행 (0%)', test: r => r.paid <= 0, color: '#b9c0cc' },
    { name: '1~49%', test: r => r.rate > 0 && r.rate < 50, color: '#bad4f6' },
    { name: '50~89%', test: r => r.rate >= 50 && r.rate < 90, color: '#6199e4' },
    { name: '90% 이상', test: r => r.rate >= 90, color: '#3a78cc' },
  ];
  const items = b.map(x => ({ name: x.name, v: rows.filter(r => r.budget > 0 && x.test(r)).length, color: x.color }));
  const nTot = sum(items, x => x.v) || 1;
  const top = [...rows].filter(r => r.remain > 0).sort((a, c) => c.remain - a.remain).slice(0, 8); const mx = top.length ? top[0].remain : 1;
  return `<div class="bizviz"><div class="card donutc"><h2 style="margin:0 0 6px">집행률 분포 <span class="muted" style="font-weight:400;font-size:12px">사업 ${rows.filter(r => r.budget > 0).length}개</span></h2>
      <div class="donutwrap">${donutSVG(items, pct1(T.rate), '전체 집행률')}<div class="dleg">${items.map(x => `<div class="dl"><i style="background:${x.color}"></i><span class="dn">${x.name}</span><b class="dp">${x.v}개</b><span class="dv">${pct1(x.v / nTot * 100)}</span></div>`).join('')}</div></div></div>
    <div class="card"><h2 style="margin:0 0 8px">남은 잔액이 큰 사업 TOP ${top.length}</h2>
      ${top.map(r => `<div class="hb" data-act="bizopen" data-k="${esc(r.biz)}" data-tip="${esc(r.biz)}\n잔액 ${won(r.remain)}원 · 집행률 ${pct1(r.rate)}"><span class="hbn">${esc(r.biz)}</span><span class="hbt"><i style="width:${Math.max(2, r.remain / mx * 100).toFixed(1)}%"></i></span><b class="hbv">${won(r.remain)}</b></div>`).join('') || '<div class="muted">잔액이 남은 사업이 없습니다</div>'}</div></div>`;
}
