// 절차적 도트 스프라이트 v2 — 고해상도(타일 64x32 아이소, 캔버스 64x128, 타일 중심 (32,96)). 모든 그림은 자체 제작.
export const TW = 64, TH = 32, SW = 64, SH = 128, CX = 32, CY = 96;
const hash = (x, y, s = 0) => { let h = (x * 374761393 + y * 668265263 + s * 1274126177) | 0; h = (h ^ (h >>> 13)) * 1274126177 | 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };
const mk = () => { const c = document.createElement('canvas'); c.width = SW; c.height = SH; return c; };
const P = (g, x, y, col) => { g.fillStyle = col; g.fillRect(x | 0, y | 0, 1, 1); };
const RC = (g, x, y, w, h, col) => { g.fillStyle = col; g.fillRect(x | 0, y | 0, w | 0, h | 0); };
const shade = (hex, f) => { const n = parseInt(hex.slice(1), 16); const cl = v => Math.max(0, Math.min(255, Math.round(v))); return '#' + ((1 << 24) | (cl(n >> 16) * 65536) | (cl((n >> 8) & 255) * 256) | cl(n & 255)).toString(16).slice(1); };
const hw = r => (r < 16 ? r + 1 : TH - r) * 2;
// ---------- 타일 바닥 ----------
function ground(pal, seed, o = {}) {
  const c = mk(), g = c.getContext('2d');
  for (let r = 0; r < TH; r++) { const w = hw(r); for (let x = CX - w; x < CX + w; x++) {
    let col = hash(x, r, seed) < (o.dither ?? 0.3) ? pal[1] : pal[0];
    const edge = Math.min(CX + w - 1 - x, x - (CX - w)), lower = r >= 16;
    if (edge <= 1) col = shade(col, lower ? (x < CX ? 0.82 : 0.68) : 1.14); else if (edge <= 2 && lower) col = shade(col, x < CX ? 0.93 : 0.86);
    if (o.fn) col = o.fn(x, r, col, edge) || col;
    P(g, x, CY - 16 + r, col); } }
  return c;
}
function withGround(draw, gseed = 80, pal = ['#cfc9b8', '#c2bca9'], shadow = true) {
  const c = mk(), g = c.getContext('2d');
  g.drawImage(ground(pal, gseed, { dither: 0.22, fn: (x, r, col, e) => (((x + r) % 16 === 0 || (x - r + 128) % 16 === 0) ? shade(col, 0.95) : null) }), 0, 0);
  if (shadow) { g.fillStyle = 'rgba(25,25,45,0.24)'; for (let dy = -7; dy <= 8; dy++) { const w = Math.round(23 * (1 - Math.abs(dy) / 10)); g.fillRect(CX + 6 - w, CY + 2 + dy, 2 * w, 1); } }
  draw(g); return c;
}
// ---------- 아이소 프리즘 ----------
function box(g, cx, cy, w, d, h, base, o = {}) {
  const top = o.roof || shade(base, 1.2), out = o.out || shade(base, 0.42);
  for (let dx = -w; dx <= w; dx++) {
    const k = d * (1 - Math.abs(dx) / w), yTop = Math.round(cy - h - k), yTopEnd = Math.round(cy - h + k), yBot = Math.round(cy + k);
    for (let y = yTop; y <= yBot; y++) {
      let col;
      if (y <= yTopEnd) { col = top; if (o.topFn) col = o.topFn(dx, y - yTop, col) || col; else if (hash(dx, y, 3) < 0.12) col = shade(col, 0.95); }
      else {
        const t = (y - yTopEnd) / Math.max(1, yBot - yTopEnd), side = dx < 0 ? 0.96 : 0.7; col = shade(base, side * (1.04 - 0.16 * t));
        if (hash(dx, y, 4) < 0.06) col = shade(col, 0.94);
        if (o.win) { const [wx, wy, ww = 2, wh = 3] = o.win, px = (dx + 256) % wx, py = (y - yTopEnd) % wy;
          if (y > yTopEnd + 3 && y < yBot - 3 && Math.abs(dx) < w - 2 && px < ww && py < wh) col = (py === 0 || px === ww - 1 ? shade(o.winCol || '#ffe9a0', 0.72) : (hash(Math.floor((dx + 256) / wx), Math.floor((y - yTopEnd) / wy), 8) < 0.7 ? (o.winCol || '#ffe9a0') : shade(o.winCol || '#ffe9a0', 0.55))); }
        if (o.band && (y - yTopEnd) % o.band === 0) col = shade(col, 0.82);
        if (y > yBot - 2) col = shade(col, 0.75);                           // 바닥 접지 그림자
      }
      if (y === yTop || y === yBot || Math.abs(dx) === w) col = out;
      else if (y === yTopEnd && Math.abs(dx) < w) col = shade(top, 0.82);   // 지붕 처마선
      else if (dx === 0 && y > yTopEnd) col = shade(col, 1.12);             // 앞 모서리 하이라이트
      P(g, cx + dx, y, col);
    }
  }
}
function tree(g, x, y, s = 0, big = 1) {
  const R0 = Math.round(7 * big); RC(g, x - 1, y - 4, 2, 5, '#5a3d22');
  for (let dy = -R0; dy <= R0; dy++) for (let dx = -R0; dx <= R0; dx++) { const dd = Math.hypot(dx, dy * 1.1); if (dd > R0) continue; const lit = (dx * -0.6 + dy * -0.8) / R0; const v = hash(x + dx, y + dy, s);
    let col = lit > 0.35 ? '#6fc25a' : lit > -0.1 ? '#4aa04a' : '#2f7a3c'; if (v < 0.18) col = shade(col, 0.88); else if (v > 0.9) col = shade(col, 1.15); if (dd > R0 - 1.2) col = shade(col, 0.72); P(g, x + dx, y - 10 - Math.round(R0 * 0.6) + dy, col); }
}
// 한옥(기와지붕)
function hanok(g, cx, cy, w, d, h, wall, roofCol, o = {}) {
  box(g, cx, cy, w - 3, d - 2, h, wall, { roof: shade(wall, 1.05), win: [6, 7, 3, 5], winCol: '#7a5a3a', out: shade(wall, 0.5) });
  for (let dx = -w - 4; dx <= w + 4; dx++) {
    const k = (d + 4) * (1 - Math.abs(dx) / (w + 4)), y0 = Math.round(cy - h - k - 8 + (Math.abs(dx) > w - 3 ? Math.round((Math.abs(dx) - (w - 3)) * 0.5) * -1 : 0));
    for (let t = 0; t < 8; t++) { let col = dx < 0 ? roofCol : shade(roofCol, 0.78); if (t === 0) col = shade(roofCol, 1.3); if (t === 7) col = shade(roofCol, 0.5); if (t > 0 && t < 7 && (dx + t * 2) % 5 === 0) col = shade(col, 0.88); P(g, cx + dx, y0 + t, col); }
  }
  RC(g, cx - 1, cy - h - d - 14, 3, 2, shade(roofCol, 1.2));                        // 용마루 장식
}
function stoneTier(g, cx, cy, w, d, h, col) { box(g, cx, cy, w, d, h, col, { roof: shade(col, 1.14), band: 4, out: shade(col, 0.5), topFn: (dx, dy, c) => ((dx + dy) % 6 === 0 ? shade(c, 0.9) : null) }); }
function eave(g, cx, cy, w, d, col) { box(g, cx, cy, w + 3, Math.max(2, d + 1), 2, shade(col, 0.82), { roof: shade(col, 1.0), out: shade(col, 0.4) }); }
// ---------- 도로·철도 자동 타일링 ----------
const cache = new Map();
function overlay(base, fn) {
  const c = mk(), g = c.getContext('2d'); g.drawImage(base, 0, 0);
  for (let r = 0; r < TH; r++) { const w = hw(r); for (let x = CX - w; x < CX + w; x++) {
    const px = x - CX + 0.5, py = r - 16 + 0.5, du = (px / 32 + py / 16) / 2, dv = (py / 16 - px / 32) / 2, col = fn(du, dv, x, r); if (col) P(g, x, CY - 16 + r, col); } }
  return c;
}
const arm = (m, du, dv, W) => { const bx = (m & 3) === 0 ? 3 : (m & 3), by = (m & 12);
  return (Math.abs(du) < W && Math.abs(dv) < W) || (Math.abs(dv) < W && ((bx & 1 && du >= 0) || (bx & 2 && du <= 0))) || (Math.abs(du) < W && ((by & 4 && dv >= 0) || (by & 8 && dv <= 0))); };
const junction = m => (((m & 1) ? 1 : 0) + ((m & 2) ? 1 : 0) + ((m & 4) ? 1 : 0) + ((m & 8) ? 1 : 0)) >= 3;
export function roadSprite(base, mask, key) {
  const k = 'r' + key + mask; if (cache.has(k)) return cache.get(k); const W = 0.29, CW2 = 0.40, jn = junction(mask), m = mask;
  const sp = overlay(base, (du, dv, x, r) => {
    if (!arm(m, du, dv, CW2)) return null;
    if (!arm(m, du, dv, W + 0.025)) return hash(x, r, 35) < 0.3 ? '#a49f92' : '#b9b5a8';                      // 보도
    if (!arm(m, du, dv, W)) return '#d4d0c4';                                                                 // 연석(밝은 선)
    let col = hash(x, r, 33) < 0.2 ? '#4b5058' : '#585d65'; if (hash(x, r, 36) < 0.015) col = '#6a6f77';     // 아스팔트 질감
    const bx = (m & 3) === 0 ? 3 : (m & 3);
    const edgeLine = !jn && ((Math.abs(Math.abs(dv) - (W - 0.04)) < 0.012 && bx) || (Math.abs(Math.abs(du) - (W - 0.04)) < 0.012 && (m & 12)));
    if (edgeLine) col = '#c9c9c0';
    const alongX = Math.abs(dv) < 0.035 && ((bx & 1 && du > 0.06) || (bx & 2 && du < -0.06)) && !(Math.abs(du) < W + 0.05 && jn);
    const alongY = Math.abs(du) < 0.035 && (((m & 4) && dv > 0.06) || ((m & 8) && dv < -0.06)) && !(Math.abs(dv) < W + 0.05 && jn);
    if ((alongX && Math.floor((du + 1) * 12) % 2 === 0) || (alongY && Math.floor((dv + 1) * 12) % 2 === 0)) col = '#ecdc6e';
    if (jn) { const zx = Math.abs(du) > W + 0.01 && Math.abs(du) < W + 0.11 && Math.abs(dv) < W - 0.02 && (m & (du > 0 ? 1 : 2)), zy = Math.abs(dv) > W + 0.01 && Math.abs(dv) < W + 0.11 && Math.abs(du) < W - 0.02 && (m & (dv > 0 ? 4 : 8));
      if (zx && Math.floor((dv + 1) * 20) % 2 === 0) col = '#ececec'; if (zy && Math.floor((du + 1) * 20) % 2 === 0) col = '#ececec'; }
    return col; });
  cache.set(k, sp); return sp;
}
export function railSprite(base, mask, key, platform = false) {
  const k = 'l' + key + mask + (platform ? 'p' : ''); if (cache.has(k)) return cache.get(k); const m = mask;
  const hx = (m & 3) !== 0 || (m & 12) === 0, vy = (m & 12) !== 0;
  const paint = (a, b, x, r, plat) => {
    if (plat && b > 0.24 && b < 0.48) return (b < 0.285) ? '#ecc43c' : (hash(x, r, 5) < 0.18 ? '#b6b1a4' : '#cdc8ba');
    if (Math.abs(b) > 0.2) return null;
    if (Math.abs(Math.abs(b) - 0.085) < 0.016) return ((x + r) % 4 < 2) ? '#9aa0a8' : '#dfe3e7';
    if (Math.floor((a + 2) * 12) % 2 === 0 && Math.abs(b) < 0.175) return '#4d3a28';
    return hash(x, r, 34) < 0.4 ? '#756c5e' : '#8c8375'; };
  const sp = overlay(base, (du, dv, x, r) => (hx ? paint(du, dv, x, r, platform) : null) || (vy ? paint(dv, du, x, r, false) : null));
  cache.set(k, sp); return sp;
}
export function crossSprite(base, roadMask, railHoriz, key) {
  const k = 'x' + key + roadMask + railHoriz; if (cache.has(k)) return cache.get(k);
  const road = roadSprite(base, roadMask, key + 'c');
  const sp = overlay(road, (du, dv, x, r) => { const a = railHoriz ? du : dv, b = railHoriz ? dv : du; if (Math.abs(b) > 0.2) return null;
    if (Math.abs(Math.abs(b) - 0.085) < 0.016) return '#dfe3e7'; if (Math.floor((a + 2) * 12) % 2 === 0 && Math.abs(b) < 0.175) return '#6a5640'; return '#6e6a62'; });
  const g = sp.getContext('2d'); for (const [ax, ay] of [[-16, -8], [16, 4]]) { RC(g, CX + ax, CY + ay - 8, 2, 8, '#d94a4a'); RC(g, CX + ax, CY + ay - 8, 2, 2, '#f0f0f0'); RC(g, CX + ax - 2, CY + ay - 10, 6, 2, '#f0d040'); }
  cache.set(k, sp); return sp;
}
export function makeSprites() {
  const S = { b: {} };
  S.grass = [0, 1, 2].map(i => ground(['#78bd58', '#69ab4c'], i, { dither: 0.22, fn: (x, r) => (hash(x, r, 5 + i) < 0.04 ? '#92d572' : hash(x, r, 8 + i) < 0.035 ? '#4a8c3e' : (hash(x >> 1, r >> 1, 12 + i) < 0.025 ? '#f4e45a' : null)) }));
  S.city = [0, 1].map(i => ground(['#cbc5b4', '#bdb7a5'], 10 + i, { dither: 0.2, fn: (x, r) => (((x + r) % 16 === 0 || (x - r + 128) % 16 === 0) ? '#b0aa98' : null) }));
  S.water = [0, 1].map(i => ground(['#3f86d4', '#3374c2'], 30 + i, { dither: 0.3, fn: (x, r, col, e) => (hash((x + i * 3) >> 1, r >> 1, 40) < 0.07 ? '#b6dcfa' : e <= 1 ? '#74b0ec' : (r % 6 === i * 2 && hash(x >> 2, r, 41) < 0.4 ? '#5c9be0' : null)) }));
  S.field = [0, 1].map(i => ground(['#b6c458', '#a3b247'], 50 + i, { dither: 0.12, fn: (x, r) => (((x + r * 2 + i * 2) % 8) < 2 ? '#86a03a' : null) }));
  S.mountain = [0, 1, 2].map(i => { const c = mk(), g = c.getContext('2d'); g.drawImage(ground(['#6fa658', '#5f9549'], 60 + i, { dither: 0.3 }), 0, 0);
    const H = 34 + i * 10; for (let dx = -26; dx <= 26; dx++) { const t = Math.abs(dx) / 26, ridge = 0.8 + 0.35 * hash(dx >> 1, i, 61) + 0.1 * Math.sin(dx / 3), prof = H * (1 - t * t) * ridge, top = Math.round(CY - 6 - prof);
      for (let y = top; y < CY - 4 + (1 - t) * 6; y++) { const rel = (y - top) / Math.max(1, prof); let col = dx < -2 ? '#5f8a49' : '#44703a'; if (rel < 0.22 && prof > 22) col = dx < -2 ? '#b2b3a6' : '#8a8c80'; else if (rel < 0.5 && hash(dx, y, 65) < 0.3) col = shade(col, 1.12); if (hash(dx, y, 62) < 0.22) col = shade(col, 0.86); if (y === top) col = shade(col, 0.5); P(g, CX + dx, y, col); } }
    for (let k = 0; k < 11; k++) { const tx = CX - 18 + Math.floor(hash(k, i, 63) * 36), ty = CY - 4 - Math.floor(hash(k, i, 64) * 14); RC(g, tx, ty - 3, 2, 3, '#2c5a2e'); RC(g, tx, ty - 5, 2, 2, '#3d7a40'); } return c; });
  S.marker = ground(['#ffe27a', '#ffd04a'], 90, { dither: 0.2, fn: (x, r, col, e) => (e <= 3 ? '#e8962a' : null) });
  S.empty = withGround(g => { g.drawImage(ground(['#c1ba9e', '#b2ab90'], 91, { dither: 0.3, fn: (x, r, col, e2) => (e2 <= 2 ? '#8e8772' : null) }), 0, 0); }, 80, ['#cfc9b8', '#c2bca9'], false);
  // 장식(배경) 건물/나무
  S.deco = [
    withGround(g => { box(g, CX, CY - 4, 16, 8, 14, '#e4d6bc', { roof: '#b5543e', win: [6, 7, 3, 4] }); RC(g, CX + 8, CY - 30, 3, 6, '#8a6a58'); }),
    withGround(g => box(g, CX, CY - 4, 18, 9, 26, '#d9d4cc', { roof: '#7d8aa0', win: [5, 7, 3, 4] })),
    withGround(g => { box(g, CX, CY - 4, 18, 9, 44, '#bfc4d0', { roof: '#d6dae3', win: [5, 6, 3, 3], band: 0 }); RC(g, CX - 2, CY - 56, 4, 8, '#9aa0ac'); }),
    withGround(g => { box(g, CX, CY - 4, 16, 8, 12, '#f0cc86', { roof: '#c8693f', win: [6, 8, 3, 4] }); for (let x = -15; x <= 15; x++) if (x % 6 < 3) RC(g, CX + x, CY - 3 + Math.round(Math.abs(x) * 0.5), 1, 3, x % 12 < 6 ? '#d94a4a' : '#f5f0e0'); }),
  ];
  S.tree = [0, 1, 2].map(i => { const c = mk(), g = c.getContext('2d'); g.drawImage(S.grass[i], 0, 0); g.fillStyle = 'rgba(20,40,20,0.2)'; g.fillRect(CX - 14 + i * 6, CY + 2, 18, 3); tree(g, CX - 10 + i * 8, CY - 2, 1 + i, 1); tree(g, CX + 10 - i * 4, CY + 3, 4 + i, 0.9); if (i === 1) tree(g, CX, CY - 6, 9, 0.8); return c; });
  const B = S.b;
  B.house = withGround(g => { hanok(g, CX, CY - 4, 18, 8, 9, '#efe3cf', '#4a4f5a'); RC(g, CX - 20, CY + 2, 10, 2, '#9c8a6e'); });
  B.apartment = withGround(g => { box(g, CX - 5, CY - 2, 12, 6, 64, '#c2c6d2', { roof: '#e0e3ea', win: [5, 8, 3, 4], winCol: '#a8d0f0', band: 0, topFn: (dx, dy, c) => (dy % 4 === 0 ? shade(c, 0.95) : null) }); box(g, CX + 14, CY + 2, 10, 5, 42, '#b2b6c4', { roof: '#d4d8e2', win: [5, 8, 3, 4], winCol: '#a8d0f0' }); });
  B.shop = withGround(g => { box(g, CX, CY - 4, 22, 10, 20, '#f4d68c', { roof: '#e08a4c', win: [7, 9, 4, 4], winCol: '#fff2c0' }); for (let x = -21; x <= 21; x++) { const y = CY - 2 + Math.round(Math.abs(x) * 10 / 22); if (x % 8 < 4) { RC(g, CX + x, y - 11, 1, 5, '#d94a4a'); RC(g, CX + x, y - 6, 1, 1, '#a02828'); } else RC(g, CX + x, y - 11, 1, 5, '#f4f0e0'); } RC(g, CX - 5, CY - 36, 10, 4, '#2a3a5a'); RC(g, CX - 4, CY - 35, 8, 2, '#ffe070'); });
  B.factory = withGround(g => { box(g, CX, CY - 4, 22, 10, 22, '#9aa5af', { roof: '#bcc6ce', win: [8, 9, 4, 3], winCol: '#ffd070', band: 6, topFn: (dx, dy, c) => ((dx + 64) % 10 < 3 ? shade(c, 0.9) : null) }); box(g, CX + 14, CY - 18, 4, 2, 44, '#8a6a58', { roof: '#6a4a3a', out: '#3c2a22', band: 5 }); box(g, CX - 14, CY + 4, 6, 3, 16, '#c6ccd2', { roof: '#e4e8ec' });
    for (let i = 0; i < 12; i++) { RC(g, CX + 12 + Math.round(Math.sin(i / 2)) * 2 + i % 2, CY - 70 - i * 2, 6 - (i >> 2), 3, i < 5 ? '#f2f2f2' : '#d6d6d6'); } });
  B.farm = withGround(g => { for (let y = -8; y <= 8; y++) for (let x = -24; x <= 24; x++) if (Math.abs(x) / 24 + Math.abs(y) / 10 < 1) P(g, CX + x, CY + y, (x + y * 2) % 6 < 2 ? '#8aa83a' : '#b6d058'); box(g, CX - 6, CY - 6, 11, 5, 16, '#c0402f', { roof: '#e8e0d0', band: 5, win: [8, 8, 3, 3], winCol: '#ffd070' }); box(g, CX + 16, CY - 2, 4, 2, 14, '#d8c070', { roof: '#e8d890', band: 3 }); });
  B.heritage = withGround(g => { hanok(g, CX, CY - 4, 22, 10, 12, '#e9dcc4', '#3d4d60'); RC(g, CX - 26, CY + 4, 14, 3, '#b5a98a'); });
  B.tourist = withGround(g => { hanok(g, CX - 8, CY - 2, 15, 7, 9, '#ece0cb', '#4b5566'); hanok(g, CX + 14, CY + 2, 12, 6, 7, '#e6d8bf', '#4b5566'); });
  B.park = withGround(g => { g.fillStyle = '#4a8fd8'; for (let y = -3; y <= 3; y++) { const w = Math.round(9 * (1 - Math.abs(y) / 4)); g.fillRect(CX + 6 - w, CY + 5 + y, 2 * w, 1); } tree(g, CX - 14, CY - 2, 1); tree(g, CX + 14, CY - 6, 2); tree(g, CX - 2, CY + 4, 3, 0.8); for (const [dx, dy, c] of [[-4, -8, '#d94a4a'], [4, -10, '#f0c030'], [0, 8, '#e87ac8'], [-8, 4, '#fff']]) RC(g, CX + dx, CY + dy, 2, 2, c); RC(g, CX - 20, CY + 8, 12, 2, '#a89878'); }, 81, ['#8fcf6a', '#7cc058']);
  B.school = withGround(g => { box(g, CX, CY - 4, 22, 10, 30, '#f4f1e6', { roof: '#c3d1e4', win: [7, 9, 4, 4], winCol: '#8fb4e0', band: 10 }); box(g, CX - 14, CY + 4, 8, 4, 14, '#e6e2d4', { roof: '#c3d1e4' }); RC(g, CX + 17, CY - 52, 1, 18, '#888'); for (let a = 0; a < 6; a++) for (let b = 0; b < 4; b++) P(g, CX + 18 + a, CY - 52 + b, a < 3 ? '#e04040' : '#fff'); });
  const ruin = (seed, n) => withGround(g => { g.drawImage(ground(['#b09a76', '#a28b68'], seed, { dither: 0.35, fn: (x, r, col, e2) => (e2 <= 2 ? '#7e6a4c' : (((x + r) % 14 === 0) ? '#968260' : null)) }), 0, 0);
    for (let k = 0; k < n; k++) { const sx = CX - 22 + Math.floor(hash(k, seed, 1) * 44), sy = CY - 8 + Math.floor(hash(k, seed, 2) * 16); box(g, sx, sy, 4, 2, 2 + (k % 3), '#c8c2b0', { roof: '#e0dac8', out: '#6a6454' }); }
    for (let x = -22; x <= 22; x += 3) RC(g, CX + x, CY - 12 + Math.round(Math.abs(x) * 0.5), 2, 1, '#e8e4d8'); RC(g, CX - 14, CY - 16, 1, 6, '#6a5a3a'); RC(g, CX + 14, CY - 6, 1, 6, '#6a5a3a'); }, seed);
  B.ruin_mireuk = ruin(100, 14); B.ruin_wanggung = ruin(101, 10);
  B.ruin_food = withGround(g => { for (let x = -24; x <= 24; x += 2) RC(g, CX + x, CY - 4 + Math.round(Math.abs(x) * 0.5), 1, 1, '#c9a23a'); box(g, CX - 14, CY - 4, 6, 3, 4, '#9aa5af', { roof: '#bcc6ce' }); box(g, CX + 14, CY - 2, 4, 2, 10, '#e0c060', { roof: '#f0d878' }); }, 106, ['#d2cdbd', '#c4bfae']);
  // 기존 익산역(낡은 역사): 낮고 긴 맞배지붕 + 승강장 지붕
  B.station_old = withGround(g => { box(g, CX, CY - 4, 24, 11, 16, '#cfc3a8', { roof: '#8a5a4a', win: [7, 9, 4, 4], winCol: '#b8d0e0', band: 0 }); box(g, CX, CY - 12, 7, 3, 12, '#c4b898', { roof: '#7a4a3a', win: [4, 5, 2, 3] }); RC(g, CX - 5, CY - 36, 10, 5, '#2a3a5a'); RC(g, CX - 4, CY - 35, 8, 3, '#fff'); box(g, CX - 18, CY + 8, 6, 2, 8, '#9aa0a8', { roof: '#c0c6ce' }); });
  B.restore_mireuk = withGround(g => { stoneTier(g, CX, CY - 2, 24, 10, 6, '#b8b3a2'); let y = CY - 8; const ws = [18, 16, 14, 12, 10, 8];
    ws.forEach((w, i) => { stoneTier(g, CX, y, w, Math.max(3, Math.round(w / 2.2)), 8, '#cfcab8'); y -= 9; eave(g, CX, y + 8, w, Math.max(3, Math.round(w / 2.2)), '#a8a38c'); y -= 2; });
    RC(g, CX - 1, y - 8, 2, 8, '#b0aa90'); RC(g, CX - 3, y - 6, 6, 1, '#b0aa90'); RC(g, CX - 1, y - 12, 2, 4, '#e0c050'); }, 103);
  B.restore_wanggung = withGround(g => { stoneTier(g, CX, CY - 2, 18, 8, 6, '#bdb8a6'); let y = CY - 8; const ws = [14, 12, 10, 8, 6];
    ws.forEach((w, i) => { stoneTier(g, CX, y, w, Math.max(3, Math.round(w / 2.2)), 10, '#d8d3c0'); y -= 11; eave(g, CX, y + 10, w, Math.max(3, Math.round(w / 2.2)), '#aaa590'); y -= 2; });
    RC(g, CX - 1, y - 10, 2, 10, '#b4ae94'); RC(g, CX - 2, y - 14, 4, 4, '#e0c050'); }, 104);
  B.station_dev = withGround(g => { box(g, CX, CY - 4, 26, 12, 14, '#e8e4d6', { roof: '#5a6a80', win: [6, 8, 4, 5], winCol: '#9fd0f0', band: 0, topFn: (dx, dy, c) => (dy % 5 === 0 ? shade(c, 1.08) : null) }); box(g, CX + 12, CY - 14, 7, 3, 38, '#d4c9ae', { roof: '#5a6a80', win: [4, 7, 2, 4], winCol: '#9fd0f0' });
    for (let a = -4; a <= 4; a++) for (let b = 0; b < 8; b++) if (Math.hypot(a, b - 3.5) < 4.6) P(g, CX + 12 + a, CY - 68 + b, '#f4f0e0'); RC(g, CX + 12, CY - 65, 1, 3, '#333'); RC(g, CX + 12, CY - 65, 2, 1, '#333'); box(g, CX - 14, CY + 6, 10, 3, 7, '#9aa8b8', { roof: '#c4d0dc' }); }, 105);
  B.food_cluster = withGround(g => { box(g, CX, CY - 4, 24, 11, 20, '#eef2ee', { roof: '#c4d8c8', win: [7, 9, 4, 4], winCol: '#8ec7a0', band: 10 }); box(g, CX - 16, CY - 8, 6, 3, 30, '#9fd0a8', { roof: '#c8e6cc', band: 6 }); for (let a = -2; a <= 3; a++) RC(g, CX + 14 + a * 2, CY - 42, 2, 6, '#e84a4a'); RC(g, CX + 14, CY - 36, 1, 16, '#ccc'); box(g, CX + 4, CY + 8, 8, 3, 8, '#cfe0d2', { roof: '#e8f2ea' }); }, 107);
  return S;
}
