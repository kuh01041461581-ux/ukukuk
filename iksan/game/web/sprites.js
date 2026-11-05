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
// 단지 필지(3x3): 종류별 바닥·울타리·진입로. fence 마스크: 1=+x 2=-x 4=+y 8=-y 방향이 단지 바깥이면 울타리. drive=진입로 칸(도로 쪽 dir)
export function lotSprite(kind, fence, role, dir, key) {
  const k = 'o' + kind + fence + role + (dir ? dir.join('') : '') + key; if (cache.has(k)) return cache.get(k);
  const base = withGround(() => {}, 80, ['#cfc9b8', '#c2bca9'], false);
  const sp = overlay(base, (du, dv, x, r) => {
    const f = (fence & 1 && du > 0.42) || (fence & 2 && du < -0.42) || (fence & 4 && dv > 0.42) || (fence & 8 && dv < -0.42);
    if (f) { const post = (Math.floor((du + dv + 4) * 10) % 4) === 0; if (kind === 'res') return hash(x, r, 70) < 0.5 ? '#3f8a3c' : '#56a850'; if (kind === 'cul') return post ? '#6a4a2a' : '#b08a54'; return post ? '#5a5f68' : (r % 2 ? '#9aa0a8' : '#b8bec6'); }
    if (role === 'drive' && dir) { const along = dir[0] !== 0 ? Math.abs(dv) < 0.13 && du * dir[0] > -0.1 : Math.abs(du) < 0.13 && dv * dir[1] > -0.1; if (along) return Math.abs((dir[0] !== 0 ? dv : du)) > 0.1 ? '#d8d4c8' : (hash(x, r, 71) < 0.2 ? '#4b5058' : '#585d65'); }
    if (kind === 'res') { if (Math.abs(du) < 0.4 && Math.abs(dv) < 0.4) return hash(x, r, 73) < 0.1 ? '#6aae52' : (hash(x, r, 79) < 0.02 ? '#f4e45a' : '#7cc060'); return null; }
    if (kind === 'com') { if (Math.abs(du) < 0.38 && Math.abs(dv) < 0.38) { if (Math.floor((dv + 1) * 9) % 3 === 0) return '#e8e8e0'; return hash(x, r, 74) < 0.2 ? '#4b5058' : '#585d65'; } return null; }
    if (kind === 'ind') { if (Math.abs(du) < 0.4 && Math.abs(dv) < 0.4) return ((Math.abs(du) > 0.34 || Math.abs(dv) > 0.34) ? '#e0b830' : (hash(x, r, 75) < 0.18 ? '#a8a49a' : '#bcb8ac')); return null; }
    if (kind === 'cul') { if (Math.abs(du) < 0.4 && Math.abs(dv) < 0.4) return hash(x, r, 76) < 0.35 ? '#b8aa8a' : '#c8bc9c'; return null; }
    if (kind === 'empty') { if (Math.abs(du) < 0.42 && Math.abs(dv) < 0.42) return hash(x, r, 77) < 0.25 ? '#a8946c' : '#b8a47c'; return null; }
    return null; });
  const g = sp.getContext('2d');
  if (role === 'yard') { if (kind === 'res' && hash(fence, key.length, 78) < 0.8) tree(g, CX + Math.round((hash(fence, 1, 79) - 0.5) * 20), CY - 2, 7 + fence, 0.8); if (kind === 'ind') { box(g, CX - 8, CY - 2, 5, 2, 5, '#c0402f', { roof: '#d85a44' }); box(g, CX + 8, CY + 2, 4, 2, 4, '#3a6ab0', { roof: '#5a86c8' }); }
    if (kind === 'com') { box(g, CX - 6, CY, 5, 2, 3, '#d94a4a', { roof: '#f06a6a' }); } if (kind === 'empty') { RC(g, CX - 10, CY - 6, 1, 6, '#e8c040'); RC(g, CX + 10, CY - 2, 1, 6, '#e8c040'); } }
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
  B.zone_dummy = null;
  B.station_old = withGround(g => { box(g, CX, CY - 4, 24, 11, 16, '#cfc3a8', { roof: '#8a5a4a', win: [7, 9, 4, 4], winCol: '#b8d0e0', band: 0 }); box(g, CX, CY - 12, 7, 3, 12, '#c4b898', { roof: '#7a4a3a', win: [4, 5, 2, 3] }); RC(g, CX - 5, CY - 36, 10, 5, '#2a3a5a'); RC(g, CX - 4, CY - 35, 8, 3, '#fff'); box(g, CX - 18, CY + 8, 6, 2, 8, '#9aa0a8', { roof: '#c0c6ce' }); });
  // 미륵사지 석탑(사진 기준): 낮고 넓은 이중 기단 + 큼직한 1층(기둥·감실·양끝 어두운 출입구) + 두꺼운 층급 지붕돌, 3층 위는 무너져 계단식 석재 더미가 왼쪽으로 비스듬히 솟은 모습
  const STONE = '#cfcabd', STONE_D = '#a9a497';
  const pil = (g, cx, cy, w, d, h, n, doors) => { const yTE = cy - h; for (let k = 0; k <= n; k++) { const dx = Math.round(-w + 2 + k * (2 * w - 4) / n), kk = d * (1 - Math.abs(dx) / w);
      for (let y = Math.round(yTE + kk) + 1; y < Math.round(cy + kk) - 1; y++) { P(g, cx + dx, y, dx < 0 ? '#e2ded2' : '#bdb8aa'); P(g, cx + dx + 1, y, dx < 0 ? '#9a9588' : '#7e796c'); P(g, cx + dx - 1, y, shade(dx < 0 ? STONE : '#a29d90', 0.92)); } }
    for (let k = 0; k < n; k++) { const dx = Math.round(-w + 2 + (k + 0.5) * (2 * w - 4) / n), kk = d * (1 - Math.abs(dx) / w), door = doors && (k === 0 || k === n - 1);
      for (let y = Math.round(yTE + kk) + 3; y < Math.round(cy + kk) - 2; y++) for (let q = -2; q <= 2; q++) if (Math.abs(dx + q) < w - 1) P(g, cx + dx + q, y, door ? (y < Math.round(yTE + kk) + 6 ? '#3a362e' : '#27241e') : shade(dx < 0 ? STONE : '#a29d90', 0.8)); } };
  const eaves = (g, cx, cy, w, d, tone, layers = 2) => { for (let l = 0; l < layers; l++) box(g, cx, cy - l * 2, w + 2 + l * 2, Math.round((w + 2 + l * 2) / 2.15), 2, shade(tone, 0.9 - l * 0.04), { roof: shade(tone, 1.04), out: shade(tone, 0.4), band: 0 });
    for (const sx of [-1, 1]) { const ex = cx + sx * (w + 2 + (layers - 1) * 2); RC(g, ex - (sx > 0 ? 1 : 0), cy - layers * 2 - 2, 2, 2, shade(tone, 1.1)); } };
  // 석재 더미 기둥 그리기(무너진 상부): 가로 줄눈(층)과 어긋난 세로 줄눈으로 돌 쌓임 표현
  const rubble = (g, x, yBase, h, tone, seed) => { for (let y = 0; y < h; y++) { const course = Math.floor(y / 4), jx = (x + (course % 2) * 3 + course * 5) % 7 === 0; let col = hash(x, y, seed) < 0.18 ? shade(tone, 0.9) : tone; if (y % 4 === 0) col = shade(col, 0.82); else if (jx) col = shade(col, 0.86); else if (y % 4 === 3) col = shade(col, 1.05); P(g, x, yBase - y, col); } };
  B.restore_mireuk = withGround(g => {
    // 서탑(국보 11호): 기단 일부가 남은 낮고 넓은 이중 기단 + 1~3층 온전 + 4~6층은 한쪽(왼쪽)이 무너져 석재가 가파르게 쌓이고 오른쪽은 층급 지붕돌이 계단처럼 남음
    box(g, CX, CY - 2, 30, 14, 3, '#b3ae9f', { roof: '#c5c0b1', out: '#5e5a4c', band: 0 }); box(g, CX, CY - 5, 27, 12, 4, '#bfbaab', { roof: '#d2cdbf', out: '#5e5a4c' });
    for (const sx of [-1, 1]) for (let st = 0; st < 4; st++) box(g, CX + sx * (24 - st), CY + 8 - st * 2, 5 - st, 2, 1, '#aaa598', { roof: '#c8c3b5', out: '#5e5a4c' });
    let y = CY - 9; const W = [24, 20, 17], H = [14, 10, 9];
    for (let i = 0; i < 3; i++) { const w = W[i], d = Math.round(w / 2.15), h = H[i]; box(g, CX, y, w, d, h, STONE, { roof: shade(STONE, 1.1), out: '#5a564a' }); pil(g, CX, y, w, d, h, i === 0 ? 6 : 4, i === 0); y -= h; eaves(g, CX, y + 1, w + 3, d + 1, '#c4bfb1', i === 0 ? 3 : 2); y -= 4; }
    const base = y + 3, ST = '#bdb7a6';
    for (let dx = -17; dx <= 17; dx++) {                                                   // 상부(4~6층 잔존부) 윤곽: 왼쪽은 가파르게 솟고, 오른쪽은 3단 계단
      let h; if (dx < -4) h = 10 + Math.floor(((dx + 17) * 30 / 13) / 3) * 3; else if (dx <= 2) h = 40; else if (dx <= 8) h = 31; else if (dx <= 13) h = 22; else h = 13;
      rubble(g, CX + dx, base, h, dx < -4 ? shade(ST, 1.06) : (dx < 3 ? ST : shade(ST, 0.9)), 7);
      P(g, CX + dx, base - h, shade(ST, 0.5)); P(g, CX + dx, base - h + 1, shade(ST, 1.18)); }
    for (const [x0, x1, hh] of [[3, 8, 31], [9, 13, 22], [14, 17, 13]]) { RC(g, CX + x0 - 1, base - hh - 1, x1 - x0 + 3, 2, '#d2ccba'); RC(g, CX + x0 - 1, base - hh + 1, x1 - x0 + 3, 1, '#6e6a5a'); }   // 남은 지붕돌(처마) 층
    for (let k = 0; k < 12; k++) { const dx = -17 + Math.floor(hash(k, 4, 9) * 12), hh = 6 + Math.floor(hash(k, 5, 9) * 8); RC(g, CX + dx - 1, base - 10 - hh, 3, 2, shade(ST, 0.8)); }   // 흩어진 돌
  }, 103);
  // 동탑: 1992년 복원된 9층 석탑(새 석재라 밝고 말끔)
  B.tower_east = (() => { const c = mk(), g = c.getContext('2d'); g.drawImage(ground(['#cbc5b4', '#bdb7a5'], 130, { dither: 0.2 }), 0, 0); g.fillStyle = 'rgba(25,25,45,0.22)'; for (let dy = -5; dy <= 6; dy++) { const w = Math.round(16 * (1 - Math.abs(dy) / 8)); g.fillRect(CX + 8 - w, CY + 2 + dy, 2 * w, 1); }
    const NEW = '#e8e4d8';
    box(g, CX, CY - 2, 21, 9, 3, '#c9c4b4', { roof: '#dad6c8', out: '#6e6a5a', band: 0 }); box(g, CX, CY - 5, 19, 8, 3, '#d4cfc0', { roof: '#e4e0d4', out: '#6e6a5a' });
    let y = CY - 8; const W = [16, 15, 14, 13, 12, 11, 10, 9, 8];
    for (let i = 0; i < 9; i++) { const w = W[i], d = Math.round(w / 2.15), h = i === 0 ? 8 : 6; box(g, CX, y, w, d, h, NEW, { roof: shade(NEW, 1.08), out: '#7a766a' }); pil(g, CX, y, w, d, h, i < 3 ? 4 : 3, i === 0); y -= h; eaves(g, CX, y + 1, w + 2, d + 1, '#dcd8cb', 2); y -= 3; }
    RC(g, CX - 1, y - 8, 2, 9, '#c4bfae'); RC(g, CX - 3, y - 5, 6, 1, '#c4bfae'); RC(g, CX - 1, y - 12, 2, 3, '#e0c050'); return c; })();
  // 왕궁리 오층석탑(사진 기준): 낮은 기단 위 키 큰 1층 몸돌, 층마다 어두운 얇은 지붕돌(3단 옥개받침, 처마 끝 살짝 반전), 위로 갈수록 급격히 줄고 작은 상륜
  B.restore_wanggung = withGround(g => {
    box(g, CX, CY - 2, 26, 12, 5, '#a89f8c', { roof: '#c0b8a6', out: '#5a5446', band: 0 });
    let y = CY - 7; const W = [15, 12, 10, 8, 7], H = [20, 8, 7, 6, 5], ROOF = '#6a6254';
    for (let i = 0; i < 5; i++) { const w = W[i], d = Math.round(w / 2.15), h = H[i]; box(g, CX, y, w, d, h, '#bba98c', { roof: '#cbbb9e', out: '#4f4638', band: 0 });
      if (i === 0) for (let k = 1; k < 4; k++) { const dx = -w + Math.round(k * 2 * w / 4); const kk = d * (1 - Math.abs(dx) / w); for (let yy = Math.round(y - h + kk) + 2; yy < Math.round(y + kk) - 1; yy++) P(g, CX + dx, yy, dx < 0 ? '#8e7e64' : '#6a5c46'); }
      y -= h; for (let st = 0; st < 3; st++) box(g, CX, y + 4 - st, w + 1 + st, Math.round((w + 1 + st) / 2.15), 1, '#8a8068', { roof: '#a49a82', out: '#4f4638' });   // 옥개받침 3단
      box(g, CX, y + 1, w + 8, Math.round((w + 8) / 2.15), 2, ROOF, { roof: shade(ROOF, 1.25), out: '#2e2a22', band: 0 }); for (const sx of [-1, 1]) RC(g, CX + sx * (w + 8) - (sx > 0 ? 1 : 0), y - 3, 2, 2, shade(ROOF, 1.2)); y -= 3; }
    box(g, CX, y + 1, 4, 2, 2, '#9a9078', { roof: '#b0a68e' }); y -= 3; for (let dy = -3; dy <= 0; dy++) { const w = Math.round(3 * Math.sqrt(1 - (dy / 3.5) ** 2)); RC(g, CX - w, y + dy, 2 * w, 1, '#b8ae96'); } RC(g, CX - 1, y - 9, 2, 8, '#9a9078'); RC(g, CX - 3, y - 6, 6, 1, '#9a9078'); RC(g, CX - 1, y - 11, 2, 2, '#d8b84a');
  }, 104);
  B.station_dev = withGround(g => { box(g, CX, CY - 4, 26, 12, 14, '#e8e4d6', { roof: '#5a6a80', win: [6, 8, 4, 5], winCol: '#9fd0f0', band: 0, topFn: (dx, dy, c) => (dy % 5 === 0 ? shade(c, 1.08) : null) }); box(g, CX + 12, CY - 14, 7, 3, 38, '#d4c9ae', { roof: '#5a6a80', win: [4, 7, 2, 4], winCol: '#9fd0f0' });
    for (let a = -4; a <= 4; a++) for (let b = 0; b < 8; b++) if (Math.hypot(a, b - 3.5) < 4.6) P(g, CX + 12 + a, CY - 68 + b, '#f4f0e0'); RC(g, CX + 12, CY - 65, 1, 3, '#333'); RC(g, CX + 12, CY - 65, 2, 1, '#333'); box(g, CX - 14, CY + 6, 10, 3, 7, '#9aa8b8', { roof: '#c4d0dc' }); }, 105);
  B.food_cluster = withGround(g => { box(g, CX, CY - 4, 24, 11, 20, '#eef2ee', { roof: '#c4d8c8', win: [7, 9, 4, 4], winCol: '#8ec7a0', band: 10 }); box(g, CX - 16, CY - 8, 6, 3, 30, '#9fd0a8', { roof: '#c8e6cc', band: 6 }); for (let a = -2; a <= 3; a++) RC(g, CX + 14 + a * 2, CY - 42, 2, 6, '#e84a4a'); RC(g, CX + 14, CY - 36, 1, 16, '#ccc'); box(g, CX + 4, CY + 8, 8, 3, 8, '#cfe0d2', { roof: '#e8f2ea' }); }, 107);
  // ---- 관광·산업 시설(춘포역·보석·석산·교도소 세트장·쌍릉·캠핑장·마트) ----
  const mound = (g, x, y, r, col) => { for (let dy = -r; dy <= 0; dy++) for (let dx = -r * 2; dx <= r * 2; dx++) { const q = (dx / (r * 2)) ** 2 + (dy / r) ** 2; if (q <= 1) P(g, x + dx, y + dy, q > 0.85 ? shade(col, 0.7) : (dx * -0.5 + dy * -1 > 0 ? shade(col, 1.12) : col)); } };
  const tent = (g, x, y, col) => { for (let i = 0; i < 8; i++) { RC(g, x - i, y - 8 + i, 2 * i + 1, 1, i > 6 ? shade(col, 0.6) : (i % 2 ? col : shade(col, 1.12))); } RC(g, x - 1, y - 2, 3, 2, '#2a2a30'); };
  B.chunpo_old = withGround(g => { box(g, CX, CY - 3, 18, 8, 10, '#a98558', { roof: '#6f7378', win: [6, 7, 3, 4], winCol: '#f0e8d0', band: 0 }); for (let dx = -20; dx <= 20; dx++) { const k = 10 * (1 - Math.abs(dx) / 20); RC(g, CX + dx, Math.round(CY - 3 - 10 - k - 6), 1, 5, dx < 0 ? '#5f6368' : '#484b50'); } RC(g, CX - 5, CY - 26, 10, 5, '#f4f0e0'); RC(g, CX - 4, CY - 25, 8, 3, '#3a2a1a'); box(g, CX - 16, CY + 6, 6, 2, 3, '#bfb4a0', { roof: '#d2c8b4' }); }, 110);
  B.chunpo_dev = withGround(g => { box(g, CX, CY - 3, 20, 9, 12, '#b78f5e', { roof: '#555a60', win: [6, 7, 3, 5], winCol: '#fff3d0', band: 0 }); for (let dx = -23; dx <= 23; dx++) { const k = 11 * (1 - Math.abs(dx) / 23); RC(g, CX + dx, Math.round(CY - 3 - 12 - k - 7), 1, 6, dx < 0 ? '#4c5056' : '#3a3d42'); } box(g, CX - 20, CY + 6, 8, 3, 8, '#c9a07a', { roof: '#7a5a3e', win: [4, 5, 2, 3] }); RC(g, CX + 16, CY - 34, 1, 14, '#888'); for (let a = 0; a < 5; a++) for (let b = 0; b < 3; b++) P(g, CX + 17 + a, CY - 34 + b, '#e04040'); for (let x = -18; x <= 18; x += 4) RC(g, CX + x, CY + 10 + Math.round(Math.abs(x) * 0.5), 2, 1, '#e8c840'); }, 111);
  const gem = (g, x, y, col) => { for (let i = 0; i < 6; i++) { RC(g, x - i, y - 6 + i, 2 * i + 1, 1, i < 2 ? shade(col, 1.3) : col); } for (let i = 0; i < 6; i++) RC(g, x - 5 + i, y + i, 11 - 2 * i, 1, shade(col, 0.75 - i * 0.04)); };
  B.jewel_museum = withGround(g => { box(g, CX, CY - 4, 22, 10, 16, '#e8e4f0', { roof: '#9a8ac8', win: [7, 9, 4, 5], winCol: '#a8d8f0', band: 0 }); gem(g, CX, CY - 38, '#c070e8'); box(g, CX - 16, CY + 6, 7, 3, 6, '#cfc8e6', { roof: '#b0a4d8' }); }, 112);
  B.jewel_theme = withGround(g => { box(g, CX - 6, CY - 4, 20, 9, 16, '#e8e4f0', { roof: '#9a8ac8', win: [7, 9, 4, 5], winCol: '#a8d8f0' }); gem(g, CX - 6, CY - 38, '#c070e8'); mound(g, CX + 18, CY + 4, 5, '#5aa850'); RC(g, CX + 15, CY - 6, 6, 4, '#4a9040'); RC(g, CX + 19, CY - 10, 3, 4, '#4a9040'); RC(g, CX + 21, CY - 11, 2, 1, '#e8f0e0'); tent(g, CX - 22, CY + 8, '#e87ac8'); tent(g, CX + 4, CY + 10, '#4aa8e8'); }, 113);
  B.quarry = withGround(g => { for (let k = 0; k < 5; k++) box(g, CX - 14 + k * 3, CY - 2 - k * 7 + 6, 16 - k * 2, 7 - k, 7, k % 2 ? '#a7a69f' : '#b8b7b0', { roof: '#d0cfc8', out: '#4e4d48', band: 3, topFn: (dx, dy, c) => ((dx * 7 + dy * 3) % 5 === 0 ? shade(c, 0.9) : null) }); box(g, CX + 14, CY + 6, 5, 2, 4, '#c8c2b0', { roof: '#e0dac8' }); RC(g, CX + 8, CY - 10, 1, 18, '#555'); RC(g, CX + 8, CY - 10, 10, 1, '#555'); RC(g, CX + 17, CY - 9, 1, 8, '#444'); }, 114, ['#b8b4a8', '#aaa698']);
  B.quarry_theme = withGround(g => { for (let k = 0; k < 4; k++) box(g, CX - 14 + k * 3, CY - 2 - k * 6 + 8, 15 - k * 2, 6 - k, 6, '#b2b1aa', { roof: '#cbcac3', out: '#4e4d48', band: 3 }); g.fillStyle = '#4a8fd8'; for (let y = -3; y <= 3; y++) { const w = Math.round(12 * (1 - Math.abs(y) / 4)); g.fillRect(CX + 10 - w, CY + 6 + y, 2 * w, 1); } box(g, CX + 14, CY - 4, 8, 4, 10, '#e8f0f2', { roof: '#9ac0d0', win: [5, 6, 3, 3], winCol: '#a8d8f0' }); tree(g, CX - 22, CY + 6, 3, 0.8); }, 115, ['#b8b4a8', '#aaa698']);
  B.prison_set = withGround(g => { box(g, CX, CY - 2, 24, 11, 9, '#a8a8a0', { roof: '#c0c0b8', win: [10, 9, 2, 3], winCol: '#2a2a30', band: 0 }); for (const sx of [-20, 20]) box(g, CX + sx, CY + (sx < 0 ? 0 : 3), 4, 2, 20, '#8c8c86', { roof: '#6a6a66', win: [4, 6, 2, 3], winCol: '#ffe070' }); for (let x = -22; x <= 22; x += 3) RC(g, CX + x, CY - 14 + Math.round(Math.abs(x) * 0.45), 2, 1, '#c8c8c0'); RC(g, CX - 4, CY + 4, 8, 4, '#4a3a2a'); }, 116);
  B.prison_theme = withGround(g => { box(g, CX, CY - 2, 24, 11, 9, '#b4b2a4', { roof: '#c8c6b8', win: [10, 9, 2, 3], winCol: '#2a2a30' }); for (const sx of [-20, 20]) box(g, CX + sx, CY + (sx < 0 ? 0 : 3), 4, 2, 20, '#9a9890', { roof: '#c04a3a', win: [4, 6, 2, 3], winCol: '#ffe070' }); for (let i = 0; i < 6; i++) { RC(g, CX - 12 + i * 5, CY + 8, 1, 7, '#888'); for (let b = 0; b < 3; b++) for (let a = 0; a < 4; a++) P(g, CX - 11 + i * 5 + a, CY + 8 + b, ['#e04040', '#f0c030', '#40a0e0'][i % 3]); } }, 117);
  B.tomb = withGround(g => { mound(g, CX - 10, CY + 1, 7, '#5ea854'); mound(g, CX + 11, CY + 4, 7, '#68b55c'); for (let x = -22; x <= 22; x += 3) RC(g, CX + x, CY - 8 + Math.round(Math.abs(x) * 0.5), 2, 2, '#b8b2a0'); RC(g, CX - 10, CY - 2, 3, 3, '#8a8470'); }, 118, ['#bfc89a', '#b0ba8c']);
  B.tomb_dev = withGround(g => { mound(g, CX - 12, CY + 1, 7, '#5ea854'); mound(g, CX + 9, CY + 4, 7, '#68b55c'); hanok(g, CX + 2, CY - 10, 8, 4, 6, '#ece0cb', '#4b5566'); for (let x = -22; x <= 22; x += 3) RC(g, CX + x, CY + 10 + Math.round(Math.abs(x) * 0.5), 2, 1, '#d8c8a0'); }, 119, ['#bfc89a', '#b0ba8c']);
  B.camping = withGround(g => { tent(g, CX - 12, CY, '#e87a3a'); tent(g, CX + 8, CY - 6, '#3a8ae8'); tent(g, CX + 14, CY + 6, '#e8c43a'); RC(g, CX - 2, CY + 5, 3, 3, '#7a4a2a'); for (let k = 0; k < 4; k++) P(g, CX - 1 + (k % 2), CY + 2 - k, k > 1 ? '#f0e040' : '#e04820'); tree(g, CX - 20, CY - 6, 5, 0.8); tree(g, CX + 20, CY - 10, 6, 0.8); }, 120, ['#a8d080', '#98c070']);
  B.costco = withGround(g => { box(g, CX, CY - 4, 26, 12, 16, '#e9edf0', { roof: '#c8ced4', band: 0, topFn: (dx, dy, c) => (dy % 4 === 0 ? shade(c, 0.95) : null) }); RC(g, CX - 18, CY - 24, 36, 5, '#d8302a'); RC(g, CX - 16, CY - 23, 32, 3, '#f8e8e8'); RC(g, CX - 6, CY - 33, 12, 6, '#2a4a9a'); RC(g, CX - 5, CY - 32, 10, 4, '#e83a3a'); for (let i = 0; i < 4; i++) RC(g, CX - 18 + i * 9, CY + 9, 7, 3, '#4a4f58'); }, 121);
  B.jewel_complex = withGround(g => { box(g, CX - 8, CY - 2, 14, 6, 12, '#d8cce8', { roof: '#9a8ac8', win: [6, 8, 3, 4], winCol: '#ffe9a0' }); box(g, CX + 12, CY + 3, 10, 5, 9, '#c8bce0', { roof: '#8a7ab8', win: [6, 7, 3, 3], winCol: '#ffe9a0' }); gem(g, CX - 8, CY - 28, '#e060a8'); gem(g, CX + 12, CY - 14, '#60a8e8'); }, 122);
  B.ruin_mart = null; B.ruin_jewelcx = null; B.ruin_camping = null;
  // ---- 구역 지정형 단지(레벨 1~3) ----
  B.zone_res1 = B.house;
  B.zone_res2 = withGround(g => { box(g, CX - 9, CY - 2, 11, 5, 26, '#d6d2ca', { roof: '#8a98ae', win: [5, 7, 3, 4], winCol: '#ffe9a0' }); box(g, CX + 12, CY + 2, 9, 4, 20, '#e0d6c4', { roof: '#b5543e', win: [5, 7, 3, 4] }); });
  B.zone_res3 = B.apartment;
  B.zone_com1 = withGround(g => { box(g, CX, CY - 4, 14, 6, 10, '#f0d494', { roof: '#e08a4c', win: [6, 8, 3, 4], winCol: '#fff2c0' }); for (let x = -13; x <= 13; x++) if (x % 6 < 3) RC(g, CX + x, CY - 6 + Math.round(Math.abs(x) * 6 / 14), 1, 3, '#d94a4a'); });
  B.zone_com2 = B.shop;
  B.zone_com3 = withGround(g => { box(g, CX - 6, CY - 2, 14, 7, 24, '#f4d68c', { roof: '#c8693f', win: [6, 8, 3, 4], winCol: '#fff2c0' }); box(g, CX + 12, CY + 2, 9, 4, 40, '#b9bfd0', { roof: '#d6dae3', win: [4, 7, 3, 4], winCol: '#a8d0f0' }); RC(g, CX - 14, CY - 28, 12, 3, '#2a3a5a'); RC(g, CX - 13, CY - 27, 10, 1, '#ffe070'); });
  B.zone_ind1 = withGround(g => { box(g, CX, CY - 4, 18, 8, 12, '#a9b2ba', { roof: '#c6ced4', band: 5, win: [9, 9, 3, 2], winCol: '#ffd070' }); box(g, CX + 14, CY + 4, 4, 2, 6, '#c0402f', { roof: '#d85a44' }); });
  B.zone_ind2 = withGround(g => { box(g, CX - 4, CY - 4, 20, 9, 18, '#9aa5af', { roof: '#bcc6ce', band: 6, win: [8, 9, 4, 3], winCol: '#ffd070' }); box(g, CX + 14, CY - 10, 3, 2, 30, '#8a6a58', { roof: '#6a4a3a', band: 5 }); for (let i = 0; i < 6; i++) RC(g, CX + 13 + i % 2, CY - 44 - i * 2, 5, 3, i < 3 ? '#f0f0f0' : '#d0d0d0'); });
  B.zone_ind3 = B.factory;
  return S;
}
