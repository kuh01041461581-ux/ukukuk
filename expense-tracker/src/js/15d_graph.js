/* ===== 그래프 조각: 예산 지도(트리맵) + 사업별 막대 — 대시보드와 그래프 보기에서 함께 사용 ===== */
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
const tmBucket = r => !(r.rate > 0) ? 0 : r.rate < 25 ? 1 : r.rate < 50 ? 2 : r.rate < 75 ? 3 : r.rate < 95 ? 4 : 5;
function tmInk(hex) {   // 칸 색의 밝기에 따라 글자를 흰색/거의 검정 중 대비가 더 큰 쪽으로
  const c = hex.slice(1).match(/../g).map(h => parseInt(h, 16) / 255).map(v => v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4)), L = 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
  return 1.05 / (L + 0.05) >= (L + 0.05) / 0.0556 ? '#fff' : '#0f1720';
}

function gvTreemapCard(D, aspect) {
  const { T, el, th } = D; const rows = [...D.rows].filter(r => r.budget > 0).sort((a, b) => b.budget - a.budget);
  const ASP = aspect || 2.5, H0 = 64, W0 = H0 * ASP;
  const tiles = squarify(rows.map(r => ({ v: r.budget, r })), W0, H0);
  const tile = t => {
    const r = t.r, s = offState(r, el, th), bk = tmB()[tmBucket(r)], ink = tmInk(bk[1]);
    const big = t.w > W0 * 0.125 && t.h > 14, mid = t.w > W0 * 0.056 && t.h > 6, flag = s[0] === 'warn' ? '▲ 느림' : s[0] === 'bad' ? (s[2] === '초과' ? '■ 초과' : '▲ 소진') : s[2] === '미집행' ? '○ 미집행' : '';
    const tip = `${r.biz}\n예산 ${fuw(r.budget)} (전체의 ${(r.budget / T.budget * 100).toFixed(1)}%)\n실집행 ${fuw(r.paid)} · 집행률 ${pct1(r.rate)}\n집행잔액 ${fuw(r.remain)}\n상태: ${s[2]}`;
    return `<div class="tm" tabindex="0" style="left:${(t.x / W0 * 100).toFixed(3)}%;top:${(t.y / H0 * 100).toFixed(3)}%;width:${(t.w / W0 * 100).toFixed(3)}%;height:${(t.h / H0 * 100).toFixed(3)}%;background:${bk[1]};color:${ink}" data-tip="${esc(tip)}" aria-label="${esc(r.biz)} 집행률 ${Math.round(r.rate)}% ${s[2]}"><span class="tmi">${mid ? `<span class="tn">${esc(r.biz)}</span>` : ''}<b class="tr${big ? ' big' : ''}">${Math.round(r.rate)}%</b>${flag && mid ? `<span class="tflag ${s[0]}">${flag}</span>` : ''}</span></div>`;
  };
  return `<div class="card"><h2>예산 지도 <span class="muted" style="font-weight:400;font-size:12px">칸 크기 = 예산 · 칸 색 = 집행률(진할수록 많이 씀) · 마우스를 올리면 금액</span></h2>
    <div class="tm-legend">${tmB().map(b => `<span><i style="background:${b[1]}"></i>${b[0]}</span>`).join('')}<span class="tm-lg-sym"><span class="tflag warn">▲ 느림</span> <span class="tflag bad">▲ 소진 · ■ 초과</span> <span class="tflag gray">○ 미집행</span></span></div>
    <div class="tmwrap" style="aspect-ratio:${ASP}/1">${tiles.map(tile).join('')}</div></div>`;
}
/* 사업별 막대 카드: opt.rows(없으면 예산 큰 순 전체), opt.limit, opt.title, opt.note, opt.empty, opt.allBtn */
function gvBarsCard(D, opt = {}) {
  const { el, th } = D; const all = [...D.rows].filter(r => r.budget > 0).sort((a, b) => b.budget - a.budget); const rows = opt.rows || all;
  const maxB = all.length ? all[0].budget : 1;
  const bl = r => { const s = offState(r, el, th), lag = s[0] === 'warn', over = s[0] === 'bad'; const share = Math.max(3, r.budget / maxB * 100);
    return `<div class="bl"><span class="bln">${esc(r.biz)}</span><div class="blt"><div style="width:${share.toFixed(2)}%">${easyBar(r, el, false, over ? 'bad' : '', !lag)}</div></div><span class="blv"><b>${Math.round(r.rate)}%</b> <span class="ost ${s[0]}">${s[1]} ${s[2]}</span><small>잔액 ${fu(r.remain)}</small></span></div>`; };
  const list = rows.slice(0, opt.limit || 999);
  return `<div class="card"><div class="row" style="justify-content:space-between;margin-bottom:6px"><h2 style="margin:0">${opt.title || '사업별 집행'} <span class="muted" style="font-weight:400;font-size:12px">${opt.note || '막대 길이 = 예산 크기 · 파랑 = 집행 · 연한 주황 = 지금쯤 써야 하는데 부족 · 세로선 = 연 경과율'}${opt.limit && rows.length > opt.limit ? ` · ${rows.length}개 중 ${opt.limit}개` : ''}</span></h2>${opt.allBtn || ''}</div>
    ${list.map(bl).join('') || `<div class="empty" style="padding:16px">${opt.empty || '해당하는 사업이 없습니다'}</div>`}</div>`;
}

function renderGraphView(D) {
  const { T, el, th, nats } = D; const rows = [...D.rows].filter(r => r.budget > 0).sort((a, b) => b.budget - a.budget);
  const showAll = UI.rep.graphAll;
  return `<div class="gv">
    <div class="ez-tiles"><div class="ez-tile"><span>예산현액</span><b>${fuw(T.budget)}</b></div><div class="ez-tile"><span>실집행액 (집행률 ${pct1(T.rate)})</span><b>${fuw(T.paid)}</b></div><div class="ez-tile"><span>집행잔액</span><b>${fuw(T.remain)}</b></div><div class="ez-tile ${T.rate < el - th.low ? 'w' : ''}"><span>연 경과율 ${pct1(el)} 대비</span><b>${dlt(T.rate - el)}</b></div></div>
    ${gvTreemapCard(D, 160 / 64)}
    ${gvBarsCard(D, { limit: showAll ? 999 : 15, allBtn: rows.length > 15 ? `<button class="btn sm noprint" data-act="repgraphall">${showAll ? '접기' : `전체 ${rows.length}개 보기`}</button>` : '' })}
    <div class="card"><h2>성질별 (통계목 대분류)</h2>${nats.map(n => repBarRow(n.name, n, true)).join('')}</div>
    ${repTrend()}
    <div class="muted rep-foot">※ 예산 지도는 예산현액이 있는 사업만 표시합니다. 집행률 = 실집행액 ÷ 예산현액, 단위: ${ul()}.</div></div>`;
}
