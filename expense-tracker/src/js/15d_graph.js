/* ===== 그래프 보기: 예산 지도(트리맵) + 사업별 막대 — 큰 사업이 얼마나 쓰였는지 한눈에 ===== */
function squarify(items, W, H) {
  const total = items.reduce((a, b) => a + b.v, 0) || 1; const out = [];
  let rest = items.map(i => ({ ...i, a: i.v / total * W * H })); let x = 0, y = 0, w = W, h = H;
  const worst = (row, side) => { const s = row.reduce((a, b) => a + b.a, 0), mx = Math.max(...row.map(r => r.a)), mn = Math.min(...row.map(r => r.a)); return Math.max(side * side * mx / (s * s), s * s / (side * side * mn)); };
  while (rest.length) {
    const side = Math.min(w, h); const row = [rest[0]]; let i = 1;
    while (i < rest.length && worst([...row, rest[i]], side) <= worst(row, side)) { row.push(rest[i]); i++; }
    const s = row.reduce((a, b) => a + b.a, 0);
    if (w >= h) { const cw = s / h; let cy = y; row.forEach(r => { const rh = r.a / cw; out.push({ ...r, x, y: cy, w: cw, h: rh }); cy += rh; }); x += cw; w -= cw; }
    else { const rh = s / w; let cx = x; row.forEach(r => { const rw = r.a / rh; out.push({ ...r, x: cx, y, w: rw, h: rh }); cx += rw; }); y += rh; h -= rh; }
    rest = rest.slice(row.length);
  }
  return out;
}
/* 집행률 구간 → 색 (한 가지 파랑의 밝기: 많이 쓸수록 진함). 상태는 기호로 따로 표시 */
const TM_BUCKETS = [['0%', '#d6dbe4', '#222'], ['1~24%', '#cfe1f8', '#16335a'], ['25~49%', '#9fc3f1', '#10294a'], ['50~74%', '#5f9be6', '#fff'], ['75~94%', '#2a78d6', '#fff'], ['95%~', '#14509a', '#fff']];
const tmBucket = r => !(r.rate > 0) ? 0 : r.rate < 25 ? 1 : r.rate < 50 ? 2 : r.rate < 75 ? 3 : r.rate < 95 ? 4 : 5;

function renderGraphView(D) {
  const { T, el, th, nats } = D; const rows = [...D.rows].filter(r => r.budget > 0).sort((a, b) => b.budget - a.budget);
  const tiles = squarify(rows.map(r => ({ v: r.budget, r })), 160, 64);
  const tile = t => {
    const r = t.r, s = offState(r, el, th), bk = TM_BUCKETS[tmBucket(r)];
    const big = t.w > 20 && t.h > 14, mid = t.w > 9 && t.h > 6, sym = s[0] === 'ok' ? '' : s[1];
    const tip = `${r.biz}\n예산 ${fuw(r.budget)} (전체의 ${(r.budget / T.budget * 100).toFixed(1)}%)\n집행 ${fuw(r.paid)} · 집행률 ${pct1(r.rate)}\n집행잔액 ${fuw(r.remain)}\n상태: ${s[2]}`;
    return `<div class="tm ${s[0]}" style="left:${(t.x / 160 * 100).toFixed(3)}%;top:${(t.y / 64 * 100).toFixed(3)}%;width:${(t.w / 160 * 100).toFixed(3)}%;height:${(t.h / 64 * 100).toFixed(3)}%;background:${bk[1]};color:${bk[2]}" data-tip="${esc(tip)}"><span class="tmi">${mid ? `<span class="tn">${esc(r.biz)}</span>` : ''}<b class="tr${big ? ' big' : ''}">${sym ? sym + ' ' : ''}${Math.round(r.rate)}%</b></span></div>`;
  };
  const maxB = rows.length ? rows[0].budget : 1;
  const bl = r => { const s = offState(r, el, th), lag = s[0] === 'warn', over = s[0] === 'bad'; const share = Math.max(3, r.budget / maxB * 100);
    return `<div class="bl"><span class="bln">${esc(r.biz)}</span><div class="blt"><div style="width:${share.toFixed(2)}%">${easyBar(r, el, false, over ? 'bad' : '', !lag)}</div></div><span class="blv"><b>${Math.round(r.rate)}%</b> <span class="ost ${s[0]}">${s[1]} ${s[2]}</span><small>잔액 ${fu(r.remain)}</small></span></div>`; };
  const showAll = UI.rep.graphAll, list = showAll ? rows : rows.slice(0, 15);
  return `<div class="gv">
    <div class="ez-tiles"><div class="ez-tile"><span>예산현액</span><b>${fuw(T.budget)}</b></div><div class="ez-tile"><span>지출액 (집행률 ${pct1(T.rate)})</span><b>${fuw(T.paid)}</b></div><div class="ez-tile"><span>집행잔액</span><b>${fuw(T.remain)}</b></div><div class="ez-tile ${T.rate < el - th.low ? 'w' : ''}"><span>연 경과율 ${pct1(el)} 대비</span><b>${dlt(T.rate - el)}</b></div></div>
    <div class="card"><h2>예산 지도 <span class="muted" style="font-weight:400;font-size:12px">칸 크기 = 예산 · 칸 색 = 집행률 (진할수록 많이 씀) · ▲ 느림 · ■ 초과 · 마우스를 올리면 금액</span></h2>
      <div class="tm-legend">${TM_BUCKETS.map(b => `<span><i style="background:${b[1]}"></i>${b[0]}</span>`).join('')}<span class="tm-lg-sym">▲ 쓰는 속도가 느림 / 소진임박 · ■ 예산 초과</span></div>
      <div class="tmwrap">${tiles.map(tile).join('')}</div></div>
    <div class="card"><div class="row" style="justify-content:space-between;margin-bottom:6px"><h2 style="margin:0">사업별 집행 <span class="muted" style="font-weight:400;font-size:12px">막대 길이 = 예산 크기 · 파랑 = 집행 · 연한 주황 = 지금쯤 써야 하는데 부족 · 세로선 = 연 경과율${showAll ? '' : ` · 예산 큰 순 ${list.length}/${rows.length}개`}</span></h2>${rows.length > 15 ? `<button class="btn sm noprint" data-act="repgraphall">${showAll ? '접기' : `전체 ${rows.length}개 보기`}</button>` : ''}</div>
      ${list.map(bl).join('')}</div>
    <div class="card"><h2>성질별 (통계목 대분류)</h2>${nats.map(n => repBarRow(n.name, n, true)).join('')}</div>
    ${repTrend()}
    <div class="muted rep-foot">※ 예산 지도는 예산현액이 있는 사업만 표시합니다. 집행률 = 지출액 ÷ 예산현액, 단위: ${ul()}.</div></div>`;
}
