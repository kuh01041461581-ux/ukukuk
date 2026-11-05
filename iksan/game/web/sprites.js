// 절차적 도트 스프라이트(자체 제작, 저작권 문제 없음). 타일 32x16 아이소, 스프라이트 캔버스 32x64 (타일 중심 = (16,48)).
export const TW = 32, TH = 16, SW = 32, SH = 64, CX = 16, CY = 48;
const hash = (x, y, s = 0) => { let h = (x * 374761393 + y * 668265263 + s * 1274126177) | 0; h = (h ^ (h >>> 13)) * 1274126177 | 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };
const mk = () => { const c = document.createElement('canvas'); c.width = SW; c.height = SH; return c; };
const P = (g, x, y, col) => { g.fillStyle = col; g.fillRect(x | 0, y | 0, 1, 1); };
const shade = (hex, f) => { const n = parseInt(hex.slice(1), 16); const cl = v => Math.max(0, Math.min(255, Math.round(v))); return '#' + ((1 << 24) | (cl(n >> 16) * 65536) | (cl((n >> 8) & 255) * 256) | cl(n & 255)).toString(16).slice(1); };
const hw = r => (r < 8 ? r + 1 : TH - r) * 2;                       // 타일 행 r 의 반폭
// 타일 바닥. 윗변 밝게/아랫변 어둡게(입체감) + 디더링 + 사용자 함수
function ground(pal, seed, o = {}) {
  const c = mk(), g = c.getContext('2d');
  for (let r = 0; r < TH; r++) { const w = hw(r); for (let x = CX - w; x < CX + w; x++) {
    let col = hash(x, r, seed) < (o.dither ?? 0.3) ? pal[1] : pal[0];
    const edge = Math.min(CX + w - 1 - x, x - (CX - w)); const lowerHalf = r >= 8;
    if (edge <= 1) col = shade(col, lowerHalf ? (x < CX ? 0.82 : 0.7) : 1.12);       // 가장자리 하이라이트/그림자
    if (o.fn) col = o.fn(x, r, col, edge) || col;
    P(g, x, CY - 8 + r, col); } }
  return c;
}
// 아이소 프리즘(상자). 윗면 밝게/왼면 중간/오른면 어둡게 + 윤곽선 + 창문
function box(g, cx, cy, w, d, h, base, o = {}) {
  const top = o.roof || shade(base, 1.2), L = shade(base, 0.95), R = shade(base, 0.7), out = o.out || shade(base, 0.45);
  for (let dx = -w; dx <= w; dx++) {
    const k = d * (1 - Math.abs(dx) / w), yTop = Math.round(cy - h - k), yTopEnd = Math.round(cy - h + k), yBot = Math.round(cy + k);
    for (let y = yTop; y <= yBot; y++) {
      let col;
      if (y <= yTopEnd) { col = top; if (o.topFn) col = o.topFn(dx, y - yTop, col) || col; }
      else { col = dx < 0 ? L : R;
        if (o.win) { const wy = (y - yTopEnd) % o.win[1], wx = (dx + 64) % o.win[0]; if (wy === 1 && wx === 1 && y > yTopEnd + 1 && y < yBot - 1) col = o.winCol || '#ffe9a0'; else if (wy === 2 && wx === 1 && y > yTopEnd + 1 && y < yBot - 1) col = shade(o.winCol || '#ffe9a0', 0.7); }
        if (o.band && (y - yTopEnd) % o.band === 0) col = shade(col, 0.85); }
      if (y === yTop || y === yBot || Math.abs(dx) === w) col = out;
      if (y === yTopEnd && Math.abs(dx) < w) col = shade(top, 0.85);
      P(g, cx + dx, y, col);
    }
  }
}
function tree(g, x, y, s = 0) { const gr = ['#2e7d3a', '#3f9a48', '#58b15a']; for (let i = 0; i < 7; i++) for (let j = -Math.min(i, 3); j <= Math.min(i, 3); j++) { const v = hash(x + j, i, s); P(g, x + j, y - 9 + i, v < 0.3 ? gr[0] : v < 0.7 ? gr[1] : gr[2]); } for (let j = -3; j <= 3; j++) P(g, x + j, y - 2, '#256a31'); P(g, x, y - 1, '#6b4a2a'); P(g, x, y, '#5a3d22'); }
function withGround(draw, gseed = 80, pal = ['#cfc9b8', '#c2bca9']) { const c = mk(), g = c.getContext('2d'); g.drawImage(ground(pal, gseed, { dither: 0.25, fn: (x, r, col, e) => (e <= 1 ? shade(col, 0.95) : null) }), 0, 0); draw(g); return c; }
// 지붕 있는 한옥풍
function hanok(g, cx, cy, w, d, h, wall, roofCol) {
  box(g, cx, cy, w - 1, d - 1, h, wall, { roof: shade(wall, 1.05), win: [4, 4], winCol: '#6a4a30' });
  for (let dx = -w - 2; dx <= w + 2; dx++) { const k = (d + 2) * (1 - Math.abs(dx) / (w + 2)), y0 = Math.round(cy - h - k - 4 + (Math.abs(dx) > w - 2 ? 2 : 0)); for (let t = 0; t < 4; t++) P(g, cx + dx, y0 + t, t === 0 ? shade(roofCol, 1.25) : t === 3 ? shade(roofCol, 0.6) : (dx < 0 ? roofCol : shade(roofCol, 0.8))); }
}
// 석탑 단(층) 하나
function stoneTier(g, cx, cy, w, d, h, col) { box(g, cx, cy, w, d, h, col, { roof: shade(col, 1.12), band: 3, out: shade(col, 0.5) }); }
export function makeSprites() {
  const S = { b: {} };
  S.grass = [0, 1, 2].map(i => ground(['#78bd58', '#69ab4c'], i, { dither: 0.22, fn: (x, r, col) => (hash(x, r, 5 + i) < 0.05 ? '#8bd06a' : hash(x, r, 8 + i) < 0.04 ? '#4f9040' : null) }));
  S.city = [0, 1].map(i => ground(['#cbc5b4', '#bdb7a5'], 10 + i, { dither: 0.2, fn: (x, r, col, e) => (((x + r) % 8 === 0 || (x - r + 64) % 8 === 0) ? shade(col, 0.93) : null) }));
  S.road = [0, 1].map(i => ground(['#666b73', '#585d65'], 20 + i, { dither: 0.2, fn: (x, r, col, e) => (e <= 2 ? '#8a8f95' : (r === 7 || r === 8) && (x % 8 < 4) ? '#e8d878' : null) }));
  S.water = [0, 1].map(i => ground(['#3f86d4', '#3374c2'], 30 + i, { dither: 0.3, fn: (x, r, col, e) => (hash((x + i * 2) >> 1, r, 40) < 0.1 ? '#a8d4f8' : e <= 1 ? '#6aa8e8' : null) }));
  S.field = [0, 1].map(i => ground(['#b6c458', '#a3b247'], 50 + i, { dither: 0.12, fn: (x, r) => ((x + r * 2 + i) % 5 === 0 ? '#86a03a' : null) }));
  S.mountain = [0, 1, 2].map(i => { const c = mk(), g = c.getContext('2d'); g.drawImage(ground(['#6fa658', '#5f9549'], 60 + i, { dither: 0.3 }), 0, 0);
    const H = 16 + i * 5; for (let dx = -13; dx <= 13; dx++) { const t = Math.abs(dx) / 13, prof = H * (1 - t * t) * (0.85 + 0.3 * hash(dx, i, 61)), top = Math.round(CY - 3 - prof);
      for (let y = top; y < CY - 2 + (1 - t) * 3; y++) { const rel = (y - top) / Math.max(1, prof); let col = dx < -1 ? '#5f8a49' : '#44703a'; if (rel < 0.25 && prof > 12) col = dx < -1 ? '#a6a79a' : '#838579'; if (hash(dx, y, 62) < 0.25) col = shade(col, 0.88); if (y === top) col = shade(col, 0.55); P(g, CX + dx, y, col); } }
    for (let k = 0; k < 5; k++) { const tx = CX - 8 + Math.floor(hash(k, i, 63) * 16), ty = CY - 2 - Math.floor(hash(k, i, 64) * 6); P(g, tx, ty, '#2c5a2e'); P(g, tx, ty - 1, '#3d7a40'); } return c; });
  S.marker = ground(['#ffe27a', '#ffd04a'], 90, { dither: 0.2, fn: (x, r, col, e) => (e <= 2 ? '#e8962a' : null) });
  S.empty = withGround(g => { const e = ground(['#c1ba9e', '#b2ab90'], 91, { dither: 0.3, fn: (x, r, col, e2) => (e2 <= 1 ? '#8e8772' : null) }); g.drawImage(e, 0, 0); });
  // 장식(배경) 건물/나무 — 플레이어 부지가 아님
  S.deco = [
    withGround(g => box(g, CX, CY - 2, 8, 4, 7, '#e4d6bc', { roof: '#b5543e', win: [4, 4] })),
    withGround(g => box(g, CX, CY - 2, 9, 4, 12, '#d9d4cc', { roof: '#7d8aa0', win: [3, 4] })),
    withGround(g => box(g, CX, CY - 2, 9, 4, 20, '#bfc4d0', { roof: '#d6dae3', win: [3, 3] })),
    withGround(g => { box(g, CX, CY - 2, 8, 4, 6, '#f0cc86', { roof: '#c8693f', win: [4, 5] }); }),
  ];
  S.tree = [0, 1, 2].map(i => { const c = mk(), g = c.getContext('2d'); g.drawImage(S.grass[i], 0, 0); tree(g, CX - 5 + i * 4, CY - 1, 1 + i); tree(g, CX + 5 - i * 2, CY + 1, 4 + i); if (i === 1) tree(g, CX, CY - 3, 9); return c; });
  const B = S.b;
  B.house = withGround(g => { hanok(g, CX, CY - 2, 9, 4, 5, '#efe3cf', '#4a4f5a'); });
  B.apartment = withGround(g => { box(g, CX, CY - 2, 6, 3, 30, '#c2c6d2', { roof: '#e0e3ea', win: [3, 4] }); box(g, CX + 7, CY - 1, 5, 2, 22, '#b2b6c4', { roof: '#d4d8e2', win: [3, 4] }); });
  B.shop = withGround(g => { box(g, CX, CY - 2, 11, 5, 10, '#f4d68c', { roof: '#e08a4c', win: [4, 5], winCol: '#fff2c0' }); for (let x = -10; x <= 10; x++) { const y = CY - 1 + ((Math.abs(x) * 5 / 11) | 0); if (x % 4 < 2) { P(g, CX + x, y - 6, '#d94a4a'); P(g, CX + x, y - 5, '#f26a6a'); } } });
  B.factory = withGround(g => { box(g, CX, CY - 2, 11, 5, 11, '#9aa5af', { roof: '#bcc6ce', win: [5, 5], winCol: '#ffd070', band: 4 }); box(g, CX + 7, CY - 9, 2, 1, 22, '#8a6a58', { roof: '#6a4a3a', out: '#3c2a22' }); for (let i = 0; i < 7; i++) { P(g, CX + 6 + (i % 3), CY - 34 - i, i < 3 ? '#f0f0f0' : '#d0d0d0'); P(g, CX + 7 + (i % 2), CY - 33 - i, '#e0e0e0'); } });
  B.farm = withGround(g => { for (let y = -4; y <= 4; y++) for (let x = -12; x <= 12; x++) if (Math.abs(x) / 12 + Math.abs(y) / 5 < 1) P(g, CX + x, CY + y, (x + y * 2) % 4 === 0 ? '#8aa83a' : '#b6d058'); box(g, CX - 3, CY - 3, 6, 3, 8, '#c0402f', { roof: '#e8e0d0', band: 3 }); box(g, CX + 8, CY - 1, 2, 1, 6, '#d8c070', { roof: '#e8d890' }); });
  B.heritage = withGround(g => { hanok(g, CX, CY - 2, 11, 5, 6, '#e9dcc4', '#3d4d60'); });
  B.tourist = withGround(g => { hanok(g, CX - 4, CY - 1, 8, 4, 5, '#ece0cb', '#4b5566'); hanok(g, CX + 7, CY + 1, 6, 3, 4, '#e6d8bf', '#4b5566'); });
  B.park = withGround(g => { tree(g, CX - 7, CY - 1, 1); tree(g, CX + 6, CY - 2, 2); tree(g, CX, CY + 2, 3); P(g, CX - 2, CY - 4, '#d94a4a'); P(g, CX + 2, CY - 5, '#f0c030'); P(g, CX + 1, CY + 5, '#e87ac8'); }, 81, ['#8fcf6a', '#7cc058']);
  B.school = withGround(g => { box(g, CX, CY - 2, 11, 5, 15, '#f4f1e6', { roof: '#c3d1e4', win: [4, 4], winCol: '#8fb4e0', band: 5 }); for (let i = 0; i < 9; i++) P(g, CX + 8, CY - 24 + i, '#888'); for (let a = 0; a < 3; a++) for (let b = 0; b < 3; b++) P(g, CX + 9 + a, CY - 24 + b, '#e04040'); });
  // ---- 랜드마크: 유적터(처음) → 복원 후 상징 건물
  const ruin = (gseed, stones) => withGround(g => { const e = ground(['#b09a76', '#a28b68'], gseed, { dither: 0.35, fn: (x, r, col, e2) => (e2 <= 1 ? '#7e6a4c' : null) }); g.drawImage(e, 0, 0);
    for (let k = 0; k < stones; k++) { const sx = CX - 10 + Math.floor(hash(k, gseed, 1) * 20), sy = CY - 4 + Math.floor(hash(k, gseed, 2) * 8); box(g, sx, sy, 2, 1, 1 + (k % 2), '#c8c2b0', { roof: '#e0dac8', out: '#6a6454' }); }
    for (let x = -10; x <= 10; x += 2) { P(g, CX + x, CY - 6 + ((Math.abs(x) * 5 / 11) | 0), '#e8e4d8'); } }, gseed);
  B.ruin_mireuk = ruin(100, 8); B.ruin_wanggung = ruin(101, 6); B.ruin_station = withGround(g => { for (let x = -12; x <= 12; x++) { P(g, CX + x, CY - 1 + ((Math.abs(x) * 5 / 12) | 0) * 0, '#555'); } box(g, CX - 6, CY - 3, 3, 1, 2, '#999', { roof: '#bbb' }); }, 102, ['#d6d0c0', '#c8c2b0']);
  B.restore_mireuk = withGround(g => { // 미륵사지 석탑: 넓은 기단 + 6층 + 상륜
    stoneTier(g, CX, CY - 1, 12, 5, 3, '#b8b3a2'); let y = CY - 4; const ws = [9, 8, 7, 6, 5, 4];
    ws.forEach((w, i) => { stoneTier(g, CX, y, w, Math.max(2, Math.round(w / 2.2)), 4, '#cfcab8'); y -= 5; box(g, CX, y + 4, w + 1, Math.max(2, Math.round(w / 2.2)), 1, '#9a957f', { roof: '#b5b09b' }); y -= 0; });
    for (let i = 0; i < 5; i++) P(g, CX, y - i, i === 4 ? '#e0c050' : '#b0aa90'); }, 103);
  B.restore_wanggung = withGround(g => { // 왕궁리 오층석탑: 기단 + 5층(좁고 높음)
    stoneTier(g, CX, CY - 1, 9, 4, 3, '#bdb8a6'); let y = CY - 4; const ws = [7, 6, 5, 4, 3];
    ws.forEach((w, i) => { stoneTier(g, CX, y, w, Math.max(2, Math.round(w / 2.2)), 5, '#d8d3c0'); y -= 6; box(g, CX, y + 5, w + 1, Math.max(2, Math.round(w / 2.2)), 1, '#a39e88', { roof: '#bdb8a2' }); });
    for (let i = 0; i < 5; i++) P(g, CX, y - i, i > 2 ? '#e0c050' : '#b4ae94'); }, 104);
  B.station_dev = withGround(g => { box(g, CX, CY - 2, 13, 6, 9, '#d8cdb4', { roof: '#7a5848', win: [4, 4], winCol: '#a8c8e8', band: 0 }); box(g, CX, CY - 8, 4, 2, 24, '#c9bda0', { roof: '#7a5848', win: [3, 5] }); for (let a = -2; a <= 2; a++) for (let b = 0; b < 3; b++) if (Math.abs(a) < 2 || b === 1) P(g, CX + a, CY - 31 + b, '#f4f0e0'); P(g, CX, CY - 30, '#333'); P(g, CX, CY - 29, '#333'); }, 105);
  return S;
}
