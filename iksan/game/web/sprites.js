// 절차적 도트 스프라이트(저작권 문제 없는 자체 제작). 타일 32x16 아이소, 스프라이트 캔버스 32x64 (타일 중심 = (16,48)).
export const TW = 32, TH = 16, SW = 32, SH = 64, CX = 16, CY = 48;
const hash = (x, y, s = 0) => { let h = (x * 374761393 + y * 668265263 + s * 1274126177) | 0; h = (h ^ (h >>> 13)) * 1274126177 | 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };
const mk = () => { const c = document.createElement('canvas'); c.width = SW; c.height = SH; return c; };
const P = (g, x, y, col) => { g.fillStyle = col; g.fillRect(x, y, 1, 1); };
const shade = (hex, f) => { const n = parseInt(hex.slice(1), 16); const r = Math.max(0, Math.min(255, Math.round((n >> 16) * f))), gg = Math.max(0, Math.min(255, Math.round(((n >> 8) & 255) * f))), b = Math.max(0, Math.min(255, Math.round((n & 255) * f))); return '#' + ((1 << 24) | (r << 16) | (gg << 8) | b).toString(16).slice(1); };
// 타일 바닥(다이아몬드). pal: [밝은색, 어두운색], 노이즈로 디더링
function ground(pal, seed, opts = {}) {
  const c = mk(), g = c.getContext('2d');
  for (let r = 0; r < TH; r++) { const hw = (r < 8 ? r + 1 : TH - r) * 2; for (let x = CX - hw; x < CX + hw; x++) {
    let col = hash(x, r, seed) < (opts.dither ?? 0.3) ? pal[1] : pal[0];
    if (opts.edge && (Math.abs(x - CX) >= hw - 1)) col = shade(pal[1], 0.8);
    if (opts.fn) col = opts.fn(x, r, col) || col;
    P(g, x, CY - 8 + r, col); } }
  return c;
}
// 상자(아이소 프리즘). 위=밝게, 왼=중간, 오른=어둡게
function box(g, cx, cy, hw, hd, h, base, o = {}) {
  const top = o.roof || shade(base, 1.18), L = shade(base, 0.92), R = shade(base, 0.7);
  for (let dx = -hw; dx <= hw; dx++) {
    const k = hd * (1 - Math.abs(dx) / hw), yTop = Math.round(cy - h - k), yTopEnd = Math.round(cy - h + k), yBot = Math.round(cy + k);
    for (let y = yTop; y <= yBot; y++) {
      let col;
      if (y <= yTopEnd) col = top; else { col = dx < 0 ? L : R;
        if (o.win && y > yTopEnd + 2 && y < yBot - 2) { const wy = (y - yTopEnd) % o.win[1], wx = Math.abs(dx) % o.win[0]; if (wy === 1 && wx === 1) col = o.winCol || '#ffe9a0'; } }
      if (y === yTop || Math.abs(dx) === hw) col = shade(col, 0.78);
      P(g, cx + dx, y, col);
    }
  }
}
function tree(g, x, y, s = 0) { for (let i = 0; i < 6; i++) for (let j = -i; j <= i; j++) P(g, x + j, y - 8 + i, hash(x + j, i, s) < 0.35 ? '#2f7a3a' : '#3f9a48'); P(g, x, y, '#6b4a2a'); P(g, x, y - 1, '#6b4a2a'); }
export function makeSprites() {
  const S = {};
  S.grass = [0, 1].map(i => ground(['#7cc05a', '#6fb04f'], i, { dither: 0.25 }));
  S.city = [0, 1].map(i => ground(['#c9c3b2', '#bab49f'], 10 + i, { dither: 0.3, edge: false }));
  S.road = ground(['#6b7078', '#5d6269'], 20, { dither: 0.2, fn: (x, r) => (r === 7 && (x % 6 < 3) ? '#d9d27a' : null) });
  S.water = [0, 1].map(i => ground(['#4a8fd8', '#3b7cc6'], 30 + i, { dither: 0.3, fn: (x, r) => (hash(x >> 1, r, 40 + i) < 0.12 ? '#9ccaf5' : null) }));
  S.field = ground(['#b9c75a', '#a4b44a'], 50, { dither: 0.15, fn: (x, r) => ((x + r) % 4 === 0 ? '#8fa83f' : null) });
  S.mountain = [0, 1].map(i => { const c = mk(), g = c.getContext('2d'); g.drawImage(ground(['#7a9a5a', '#6d8c50'], 60 + i, { dither: 0.3 }), 0, 0);
    const h = 20 + i * 6; for (let dx = -12; dx <= 12; dx++) { const t = Math.abs(dx) / 12, top = Math.round(CY - h * (1 - t) - 2); for (let y = top; y < CY + 2 - Math.round(t * 5); y++) { let col = dx < 0 ? '#8a8f7a' : '#6a6f60'; if (y - top < 4 + (1 - t) * 3) col = dx < 0 ? '#e9efe0' : '#cfd6c6'; if (hash(dx, y, 70) < 0.12) col = shade(col, 0.88); P(g, CX + dx, y, col); } } return c; });
  const withGround = (draw, gseed = 80) => { const c = mk(), g = c.getContext('2d'); g.drawImage(ground(['#cfc9b8', '#c2bca9'], gseed, { dither: 0.25 }), 0, 0); draw(g); return c; };
  S.marker = ground(['#ffe27a', '#ffd04a'], 90, { dither: 0.2, fn: (x, r, col) => { const hw = (r < 8 ? r + 1 : TH - r) * 2; return (Math.abs(x - CX) >= hw - 2 || r < 1 || r > 14) ? '#e8a020' : col; } });
  S.empty = withGround(g => { const e = ground(['#b4ad98', '#a39c87'], 91, { dither: 0.3, fn: (x, r) => { const hw = (r < 8 ? r + 1 : TH - r) * 2; return Math.abs(x - CX) >= hw - 2 ? '#8e8772' : null; } }); g.drawImage(e, 0, 0); });
  S.b = {
    house: withGround(g => { box(g, CX, CY - 2, 9, 4, 8, '#e8dcc4', { roof: '#c8553d', win: [4, 4] }); box(g, CX + 5, CY - 8, 2, 1, 4, '#9a8e7a', { roof: '#7a6e5a' }); }),
    apartment: withGround(g => { box(g, CX, CY - 2, 10, 5, 30, '#b9bdc9', { roof: '#d8dbe4', win: [3, 4] }); }),
    shop: withGround(g => { box(g, CX, CY - 2, 11, 5, 9, '#f0c070', { roof: '#e0884a', win: [4, 5] }); for (let x = -10; x <= 10; x++) if (x % 4 < 2) P(g, CX + x, CY - 1 + (x < 0 ? -x : x) / 4 | 0, '#d94a4a'); }),
    factory: withGround(g => { box(g, CX, CY - 2, 11, 5, 12, '#98a3ad', { roof: '#b6c0c8', win: [5, 5], winCol: '#ffd070' }); box(g, CX + 7, CY - 10, 2, 1, 22, '#8a6a58', { roof: '#6a4a3a' }); for (let i = 0; i < 6; i++) P(g, CX + 7 + (i % 2), CY - 36 - i, i < 3 ? '#e8e8e8' : '#cfcfcf'); }),
    farm: withGround(g => { for (let y = -3; y <= 3; y++) for (let x = -11; x <= 11; x++) if (Math.abs(x) / 11 + Math.abs(y) / 5 < 1) P(g, CX + x, CY + y, (x + y) % 3 === 0 ? '#9ab43c' : '#b8cf5a'); box(g, CX - 3, CY - 3, 6, 3, 8, '#c0402f', { roof: '#e6e0d0' }); }),
    heritage: withGround(g => { for (let t = 0; t < 4; t++) box(g, CX, CY - 2 - t * 8, 9 - t * 2, 4 - (t >> 1), 7, '#c9c2ad', { roof: '#e3dccb' }); for (let t = 0; t < 4; t++) box(g, CX, CY - 2 - t * 8 - 7, 10 - t * 2, 4, 1, '#8a8470', { roof: '#6f6a58' }); }),
    tourist: withGround(g => { box(g, CX, CY - 2, 11, 5, 6, '#e8dcc4', { roof: '#e8dcc4' }); for (let x = -13; x <= 13; x++) { const y = CY - 12 - Math.round((1 - Math.abs(x) / 13) * 4) + (Math.abs(x) > 10 ? -2 : 0); for (let k = 0; k < 4; k++) P(g, CX + x, y + k, k === 0 ? '#2e3b4a' : '#3d4d60'); } }),
    park: withGround(g => { tree(g, CX - 6, CY - 1, 1); tree(g, CX + 6, CY - 2, 2); tree(g, CX, CY + 2, 3); }, 81),
    school: withGround(g => { box(g, CX, CY - 2, 11, 5, 14, '#f2efe4', { roof: '#c7d4e6', win: [4, 4], winCol: '#8fb4e0' }); for (let i = 0; i < 8; i++) P(g, CX + 8, CY - 22 + i, '#888'); P(g, CX + 9, CY - 22, '#e04040'); P(g, CX + 10, CY - 22, '#e04040'); P(g, CX + 9, CY - 21, '#e04040'); P(g, CX + 10, CY - 21, '#e04040'); }),
  };
  return S;
}
