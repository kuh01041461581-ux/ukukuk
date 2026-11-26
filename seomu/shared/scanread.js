/* ============================================================
   서명된 일일점검부 · 청구서 스캔 PDF 읽기 (화면 없이 계산만 — Node 에서도 시험)
   · 일일점검부: 표(8열 × 12행: 일자줄/확인줄 6주)를 선으로 찾고, 확인 칸마다 빨간 인주 비율로 도장 여부를 판단한다.
     칸 선이 연한 스캔도 있어 어두움 기준을 올려 가며 찾고, 못 찾은 가로선은 "일자줄·확인줄" 높이 비율로 채운다.
   · 청구서: OCR 글자에서 청구금액·계좌·사역일수·단가·기본급·주휴·월차를 뽑는다(성명·은행 칸은 스캔 OCR 이 잘 못 읽어
     근로자 짝은 계좌·금액·평일/주말로 맞춘다).
   입력 이미지: gray(Uint8Array, 0~255), red(Uint8Array, 1=빨간 인주), W, H
   ============================================================ */
var SCANREAD = (function () {
  'use strict';
  function grp(a, gap) {
    gap = gap || 4; var out = [];
    a.forEach(function (x) { var g = out[out.length - 1]; if (g && x - g[g.length - 1] <= gap) g.push(x); else out.push([x]); });
    return out.map(function (g) { return Math.round(g.reduce(function (s, v) { return s + v; }, 0) / g.length); });
  }
  function rowFrac(G, W, y, x0, x1, th) { var n = 0; for (var x = x0; x < x1; x++) if (G[y * W + x] < th) n++; return n / Math.max(1, x1 - x0); }
  function colFrac(G, W, x, y0, y1, th) { var n = 0; for (var y = y0; y < y1; y++) if (G[y * W + x] < th) n++; return n / Math.max(1, y1 - y0); }

  /* 일일점검부 달력 표 찾기 → {xs:[9], ys:[13], filled:채운 선 수} 또는 null */
  var ROW_RATIO = [74, 97, 73, 96, 73, 97, 73, 98, 73, 91, 73, 96];   // 사용자 양식에서 잰 행 높이 비율
  function detectCalendar(G, W, H) {
    var ths = [205, 220, 235];
    for (var ti = 0; ti < ths.length; ti++) {
      var th = ths[ti], long = [];
      for (var y = 0; y < H; y++) if (rowFrac(G, W, y, 0, W, th) > 0.45) long.push(y);
      var hl = grp(long); if (hl.length < 2) continue;
      // 달력 상자: 마지막 긴 선을 바닥으로 두고, 위쪽 후보 선을 내려가며 세로선이 정확히 9개(8칸) 나오는 첫 범위
      var y0 = -1, y1 = hl[hl.length - 1], xs = null;
      for (var hi = 0; hi < hl.length - 1 && !xs; hi++) {
        if (y1 - hl[hi] < H * 0.35) break;
        var vc = []; for (var x = 0; x < W; x++) if (colFrac(G, W, x, hl[hi], y1, th) > 0.6) vc.push(x);
        var cand = grp(vc); if (cand.length !== 9) continue;
        // 맨 윗줄 칸(이 선 ~ 다음 선)에도 세로선 9개가 다 이어져야 진짜 달력의 위 테두리(제목 상자 선이 아님)
        var nxt = hl[hi + 1], okc = cand.filter(function (cx) { var b = 0; for (var dx = -2; dx <= 2; dx++) b = Math.max(b, colFrac(G, W, cx + dx, hl[hi] + 2, nxt - 2, th + 20)); return b > 0.7; }).length;
        if (okc >= 8) { xs = cand; y0 = hl[hi]; }
      }
      if (!xs) continue;
      // 안쪽 가로선: 기준을 올려 가며 찾는다
      var found = [];
      for (var t2 = th; t2 <= 240; t2 += 10) {
        var rs = []; for (var yy = y0; yy <= y1; yy++) if (rowFrac(G, W, yy, xs[0], xs[8], t2) > 0.6) rs.push(yy);
        found = grp(rs); if (found.length >= 13) break;
      }
      // 비율로 기대 위치를 만들고 가까운 실제 선에 붙인다
      var tot = ROW_RATIO.reduce(function (a, b) { return a + b; }, 0), acc = 0, ys = [y0], filled = 0;
      for (var k = 0; k < 12; k++) {
        acc += ROW_RATIO[k]; var e = Math.round(y0 + (y1 - y0) * acc / tot);
        if (k === 11) { ys.push(y1); break; }
        var best = null; found.forEach(function (f) { if (Math.abs(f - e) <= 18 && (best == null || Math.abs(f - e) < Math.abs(best - e))) best = f; });
        if (best == null) filled++;
        ys.push(best == null ? e : best);
      }
      return { xs: xs, ys: ys, filled: filled };
    }
    return null;
  }
  /* 확인 칸(6주 × 7요일)의 빨간 인주 비율 */
  function stampFractions(R, W, grid) {
    var out = [];
    for (var wi = 0; wi < 6; wi++) {
      var ya = grid.ys[wi * 2 + 1], yb = grid.ys[wi * 2 + 2], row = [];
      for (var ci = 1; ci <= 7; ci++) {
        var xa = grid.xs[ci] + 4, xb = grid.xs[ci + 1] - 4, n = 0, all = 0;
        for (var y = ya + 4; y < yb - 4; y++) for (var x = xa; x < xb; x++) { all++; if (R[y * W + x]) n++; }
        row.push(all ? n / all : 0);
      }
      out.push(row);
    }
    return out;
  }
  /* 그 달 달력: 6주 × 7칸(월~일) → 'YYYY-MM-DD' 또는 null */
  /* weekStart: 0 = 일요일부터(일월화수목금토, 기본) · 1 = 월요일부터(예전 양식) */
  function monthGrid(ym, weekStart) {
    var m = /^(\d{4})-(\d{1,2})$/.exec(ym), y = +m[1], mo = +m[2], first = new Date(y, mo - 1, 1), ws = weekStart === 1 ? 1 : 0;
    var start = new Date(first); start.setDate(1 - ((first.getDay() - ws + 7) % 7));
    var P = function (n) { return String(n).padStart(2, '0'); }, out = [];
    for (var wi = 0; wi < 6; wi++) {
      var row = [];
      for (var ci = 0; ci < 7; ci++) {
        var d = new Date(start.getFullYear(), start.getMonth(), start.getDate() + wi * 7 + ci);
        row.push(d.getMonth() === mo - 1 ? d.getFullYear() + '-' + P(d.getMonth() + 1) + '-' + P(d.getDate()) : null);
      }
      out.push(row);
    }
    return out;
  }

  /* 스캔한 일일점검부가 일요일 시작인지 월요일 시작인지: 첫 주 '일자' 줄에서 글자가 없는 앞칸 수로 판단 */
  function detectWeekStart(G, W, grid, ym) {
    var m = /^(\d{4})-(\d{1,2})$/.exec(ym), dow = new Date(+m[1], +m[2] - 1, 1).getDay();
    var ya = grid.ys[0] + 4, yb = grid.ys[1] - 4, lead = 0;
    for (var ci = 1; ci <= 7; ci++) {
      var xa = grid.xs[ci] + 5, xb = grid.xs[ci + 1] - 5, n = 0, all = 0;
      for (var y = ya; y < yb; y++) for (var x = xa; x < xb; x++) { all++; if (G[y * W + x] < 140) n++; }
      if (all && n / all < 0.004) lead++; else break;
    }
    if (lead === (dow + 6) % 7) return 1;
    return 0;
  }
  var STAMP_ON = 0.04, STAMP_WEAK = 0.02;
  /* 도장 비율표 → 출근일 목록 · 흐린 칸 · 달력 밖 도장 */
  function stampedDates(frac, ym, weekStart) {
    var g = monthGrid(ym, weekStart), dates = [], weak = [], outside = 0;
    for (var wi = 0; wi < 6; wi++) for (var ci = 0; ci < 7; ci++) {
      var f = frac[wi][ci], iso = g[wi][ci];
      if (f >= STAMP_ON) { if (iso) dates.push(iso); else outside++; if (iso && f < 0.08) weak.push(iso); }
    }
    return { dates: dates, weak: weak, outside: outside };
  }
  function kindOfDates(dates) {
    if (!dates.length) return '';
    var we = dates.filter(function (d) { var w = new Date(d).getDay(); return w === 0 || w === 6; }).length;
    return we === dates.length ? 'weekend' : we === 0 ? 'weekday' : 'custom';
  }
  /* 일일점검부 제목 OCR → 평일/주말, 연월 */
  function parseDailyTitle(t) {
    t = String(t || '');
    var kind = /평\s*일/.test(t) ? 'weekday' : /주\s*말/.test(t) ? 'weekend' : '';
    var m = /(20\d\d)\s*년\s*(\d{1,2})\s*월/.exec(t);
    return { kind: kind, ym: m ? m[1] + '-' + String(+m[2]).padStart(2, '0') : '', isDaily: /일일\s*점검부|점검부/.test(t) };
  }
  var N = function (s) { return s == null ? null : +String(s).replace(/[^\d]/g, ''); };
  /* 청구서 OCR 글자 → 값 */
  function parseClaim(t) {
    t = String(t || '');
    var r = { isClaim: /청\s*구\s*서|청구합니다/.test(t), gross: null, account: '', days: null, dayRate: null, base: null, ju: null, wolcha: null, kind: '', ym: '' };
    var m = /총\s*([\d,]{5,})\s*원/.exec(t); if (m) r.gross = N(m[1]);
    m = /기\s*본\s*급\s*([\d,]{5,})/.exec(t); if (m) r.base = N(m[1]);
    m = /주\s*휴\s*수\s*당\s*([\d,]{4,})/.exec(t); if (m) r.ju = N(m[1]);
    m = /월\s*차\s*수\s*당\s*([\d,]{4,})/.exec(t); if (m) r.wolcha = N(m[1]);
    m = /(\d{1,2})\s+(\d{2,3},\d{3})\s*원/.exec(t); if (m) { r.days = +m[1]; r.dayRate = N(m[2]); }
    // 계좌: 숫자 10자리 이상이 '-' 로 이어진 것 (날짜·금액 제외)
    var acc = (t.match(/\d{2,6}(?:-\d{1,8}){1,4}/g) || []).filter(function (x) { return x.replace(/\D/g, '').length >= 10; });
    if (acc.length) r.account = acc[0];
    m = /\d{1,2}\.\s*\d{1,2}\.\s*\(\s*([평주])/.exec(t); if (m) r.kind = m[1] === '평' ? 'weekday' : 'weekend';
    if (!r.kind) { if (/\(\s*평/.test(t)) r.kind = 'weekday'; else if (/\(\s*주\s*[말맘]/.test(t)) r.kind = 'weekend'; }
    if (r.gross == null) { var am = t.match(/\d{1,3}(?:,\d{3})+/g); if (am) r.gross = N(am[0]); }
    m = /(20\d\d)\s*\.\s*(\d{1,2})\s*\.\s*$/m.exec(t); if (m) r.ym = m[1] + '-' + String(+m[2]).padStart(2, '0');
    return r;
  }
  function digitDiff(a, b) {   // 숫자만 비교해 다른 자리 수(길이가 다르면 큰 값)
    a = String(a || '').replace(/\D/g, ''); b = String(b || '').replace(/\D/g, '');
    if (!a || !b) return 99; if (a.length !== b.length) return 99;
    var n = 0; for (var i = 0; i < a.length; i++) if (a[i] !== b[i]) n++; return n;
  }
  /* 청구서 ↔ 근로자: 계좌(1자리까지 오차 허용) > 청구금액 > 평일/주말 */
  function matchClaim(claim, cands) {   // cands: [{id, account, kind, gross}]
    var best = null, why = '';
    cands.forEach(function (c) {
      var s = 0, w = [];
      var dd = digitDiff(claim.account, c.account);
      if (dd === 0) { s += 100; w.push('계좌 일치'); } else if (dd === 1) { s += 60; w.push('계좌 1자리 다름'); }
      if (claim.gross != null && claim.gross === c.gross) { s += 40; w.push('금액 일치'); }
      if (claim.kind && claim.kind === c.kind) { s += 10; w.push(claim.kind === 'weekday' ? '평일' : '주말'); }
      if (s > 0 && (!best || s > best.s)) { best = { s: s, id: c.id }; why = w.join(' · '); }
    });
    return best ? { id: best.id, score: best.s, why: why } : null;
  }
  /* 일일점검부 ↔ 근로자: 평일/주말이 같은 사람 중 계산한 근무일과 가장 많이 겹치는 사람 */
  function matchDaily(kind, dates, cands) {   // cands: [{id, kind, worked:[iso]}]
    var best = null;
    cands.forEach(function (c) {
      var set = {}; c.worked.forEach(function (d) { set[d] = 1; });
      var inter = dates.filter(function (d) { return set[d]; }).length, uni = dates.length + c.worked.length - inter;
      var s = (uni ? inter / uni : 0) + (kind && c.kind === kind ? 1 : 0);
      if (!best || s > best.s) best = { s: s, id: c.id, j: uni ? inter / uni : 0 };
    });
    return best;
  }

  /* 표 한 덩어리의 세로선(칸 경계) — 표 높이의 80% 이상 이어진 선 */
  function vlines(G, W, y0, y1, th) { var vc = []; for (var x = 0; x < W; x++) if (colFrac(G, W, x, y0 + 3, y1 - 3, th || 220) > 0.8) vc.push(x); return grp(vc); }
  /* 일일점검부 위쪽 요약표(평일근무일수 | 주말근무일수 | 비고)의 '비고'(이름) 칸 */
  function summaryNameCell(G, W, H, gridTop) {
    var a = Math.max(0, gridTop - 280), ys = [];
    for (var y = a; y < gridTop - 8; y++) if (rowFrac(G, W, y, 0, W, 220) > 0.2) ys.push(y);
    var hl = grp(ys); if (hl.length < 3) return null;
    var r0 = hl[hl.length - 3], r1 = hl[hl.length - 2], r2 = hl[hl.length - 1];
    var vs = vlines(G, W, r0, r2); if (vs.length < 4) return null;
    return { x0: vs[vs.length - 2], x1: vs[vs.length - 1], y0: r1, y1: r2 };
  }
  /* 청구서 '인적사항' 표(성명 | 청구금액 | 은행 | 계좌번호 | 청구인)의 둘째 줄 칸들 */
  function claimCells(G, W, H) {
    var tries = [[220, 0.35], [205, 0.45], [235, 0.3]];
    for (var t = 0; t < tries.length; t++) {
      var ys = []; for (var y = 0; y < H; y++) if (rowFrac(G, W, y, 0, W, tries[t][0]) > tries[t][1]) ys.push(y);
      var hl = grp(ys);
      for (var i = 0; i + 2 < hl.length; i++) {
        if (hl[i + 1] - hl[i] > 120 || hl[i + 2] - hl[i + 1] > 120) continue;
        var vs = vlines(G, W, hl[i], hl[i + 2], tries[t][0]);
        if (vs.length >= 6) {
          var y0 = hl[i + 1], y1 = hl[i + 2], c = function (k) { return { x0: vs[k], x1: vs[k + 1], y0: y0, y1: y1 }; };
          return { name: c(0), gross: c(1), bank: c(2), account: c(3) };
        }
      }
    }
    return null;
  }
  /* OCR 이름 → 명부 이름 (같은 자리 글자가 2자 이상 같으면) */
  function bestName(txt, names) {
    var t = String(txt || '').replace(/[^가-힣]/g, ''); if (t.length < 2) return null;
    var best = null;
    names.forEach(function (n) {
      var sc = 0; for (var i = 0; i < Math.min(n.length, t.length); i++) if (n[i] === t[i]) sc++;
      if (n === t) sc += 10;
      if (sc >= Math.min(2, n.length) && (!best || sc > best.sc)) best = { sc: sc, name: n };
    });
    return best ? best.name : null;
  }
  function cleanAccount(t) { var m = String(t || '').replace(/[^\d\-]/g, '').match(/\d{2,6}(?:-\d{1,8}){1,4}/); return m ? m[0] : ''; }

  var BANKS = ['국민은행', '신한은행', '우리은행', '하나은행', '농협은행', '농협', '기업은행', '카카오뱅크', '토스뱅크', '케이뱅크', '전북은행', '광주은행', '부산은행', '경남은행', '대구은행', 'iM뱅크', '제주은행', '새마을금고', '신협', '우체국', '수협은행', 'SC제일은행', '씨티은행', '산업은행', '산림조합'];
  /* OCR 은행 이름 → 알려진 은행 이름(같은 자리 글자가 70% 이상 같으면) */
  function fixBank(t) {
    t = String(t || '').replace(/[^가-힣A-Za-z]/g, ''); if (t.length < 2) return t;
    var best = null;
    BANKS.forEach(function (b) {
      if (b.length !== t.length) return; var n = 0; for (var i = 0; i < b.length; i++) if (b[i] === t[i]) n++;
      if (n / b.length >= 0.7 && (!best || n > best.n)) best = { n: n, b: b };
    });
    return best ? best.b : t;
  }
  var api = { detectWeekStart: detectWeekStart, fixBank: fixBank, summaryNameCell: summaryNameCell, claimCells: claimCells, bestName: bestName, cleanAccount: cleanAccount, vlines: vlines, grp: grp, detectCalendar: detectCalendar, stampFractions: stampFractions, monthGrid: monthGrid, stampedDates: stampedDates, kindOfDates: kindOfDates,
    parseDailyTitle: parseDailyTitle, parseClaim: parseClaim, digitDiff: digitDiff, matchClaim: matchClaim, matchDaily: matchDaily, STAMP_ON: STAMP_ON };
  if (typeof module !== 'undefined') module.exports = api;
  return api;
})();
