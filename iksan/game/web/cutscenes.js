// 이벤트 컷신(16:9 도트, 192x108 논리 해상도 → 정수 배율). 절차적 장면 + 캐릭터 초상. 모든 그림은 자체 제작.
export const CW = 192, CH = 108;
const hash = (x, y, s = 0) => { let h = (x * 374761393 + y * 668265263 + s * 1274126177) | 0; h = (h ^ (h >>> 13)) * 1274126177 | 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };
const R = (g, x, y, w, h, c) => { g.fillStyle = c; g.fillRect(Math.round(x), Math.round(y), Math.round(w), Math.round(h)); };
const mix = (a, b, t) => { const p = h => [h >> 16, (h >> 8) & 255, h & 255]; const A = p(parseInt(a.slice(1), 16)), B = p(parseInt(b.slice(1), 16)); return '#' + A.map((v, i) => Math.round(v + (B[i] - v) * t).toString(16).padStart(2, '0')).join(''); };
function sky(g, top, bot, bands = 14) { for (let i = 0; i < bands; i++) { R(g, 0, i * (CH * 0.62 / bands), CW, CH * 0.62 / bands + 1, mix(top, bot, i / (bands - 1))); } for (let k = 0; k < 40; k++) { const x = hash(k, 1) * CW, y = hash(k, 2) * CH * 0.4; if (top < '#4') R(g, x, y, 1, 1, '#ffffff'); } }
function hills(g, y, col, seed, amp = 8) { for (let x = 0; x < CW; x++) { const h = y - amp * (0.5 + 0.5 * Math.sin(x / 17 + seed)) - 3 * Math.sin(x / 5 + seed * 2); R(g, x, h, 1, CH - h, col); } }
function skyline(g, y, col, seed) { for (let x = 0; x < CW;) { const w = 6 + Math.floor(hash(x, seed) * 9), h = 8 + Math.floor(hash(x, seed + 1) * 26); R(g, x, y - h, w, h + 60, col); for (let wy = y - h + 3; wy < y - 2; wy += 4) for (let wx = x + 2; wx < x + w - 1; wx += 3) if (hash(wx, wy, seed) < 0.45) R(g, wx, wy, 1, 2, '#ffe9a0'); x += w + 1; } }
function pagoda(g, cx, base, tiers, w0, col) { let y = base; R(g, cx - w0 - 3, y - 3, 2 * w0 + 6, 3, mix(col, '#000000', 0.2)); y -= 3; for (let i = 0; i < tiers; i++) { const w = w0 - i * (w0 / (tiers + 1)); R(g, cx - w, y - 6, 2 * w, 6, col); R(g, cx - w - 2, y - 7, 2 * w + 4, 2, mix(col, '#ffffff', 0.18)); R(g, cx - w, y - 5, 2 * w, 1, mix(col, '#000000', 0.15)); y -= 8; } R(g, cx, y - 8, 1, 9, '#e0c050'); R(g, cx - 1, y - 4, 3, 1, '#e0c050'); }
function water(g, y, t, col1 = '#2a6db8', col2 = '#4a8fd8') { for (let r = y; r < CH; r++) for (let x = 0; x < CW; x++) R(g, x, r, 1, 1, hash(x + ((t / 8) | 0), r, 7) < 0.1 ? '#a8d4f8' : (r % 4 < 2 ? col1 : col2)); }
// ---- 캐릭터(상반신) : 남/여 시장, 비서
export function drawPerson(g, cx, by, o = {}) {
  const s = o.s || 1, f = !!o.female, skin = o.skin || '#f0c8a0', hair = o.hair || (f ? '#2a1a14' : '#1c1c24'), suit = o.suit || (f ? '#3a4a78' : '#2b3550'), shirt = '#f4f4f4';
  const r = (x, y, w, h, c) => R(g, cx + x * s, by + y * s, w * s, h * s, c);
  const sh = shade => mix(suit, '#000000', shade);
  r(-12, -14, 24, 14, suit); r(-12, -14, 4, 14, sh(0.22)); r(8, -14, 4, 14, sh(0.3));            // 어깨/몸통
  r(-4, -16, 8, 6, shirt); if (f) { r(-3, -10, 6, 1, '#e8a0b8'); r(-1, -16, 2, 4, '#c04070'); } else { r(-1, -15, 2, 8, '#a02030'); }
  r(-3, -20, 6, 5, skin); r(-3, -17, 6, 1, mix(skin, '#000000', 0.12));                              // 목
  for (let yy = -34; yy < -19; yy++) { const t = (yy + 27) / 8, w = Math.round(7 * Math.sqrt(Math.max(0, 1 - t * t))); r(-w, yy, 2 * w, 1, skin); }  // 얼굴
  r(-6, -32, 12, 4, hair); r(-7, -30, 2, 6, hair); r(5, -30, 2, 6, hair);                           // 머리
  if (f) { r(-8, -30, 3, 17, hair); r(5, -30, 3, 17, hair); r(-7, -33, 14, 3, hair); } else { r(-7, -33, 14, 2, hair); }
  r(-4, -26, 2, 2, '#ffffff'); r(2, -26, 2, 2, '#ffffff'); r(-3, -26, 1, 2, '#222'); r(3, -26, 1, 2, '#222');   // 눈
  r(-2, -22, 4, 1, o.mood === 'sad' ? '#a05050' : '#c06060'); if (o.mood !== 'sad') r(-3, -23, 1, 1, '#c06060'), r(2, -23, 1, 1, '#c06060');
  if (!f) r(-8, -29, 1, 2, skin); if (o.badge) r(5, -10, 3, 3, '#ffd24a');
}
const SCENES = {
  flood(g, t) { sky(g, '#3a4658', '#6a7888'); hills(g, 64, '#4a5a4a', 1); water(g, 72, t, '#2a5d98', '#3a76b8'); skyline(g, 74, '#2c3445', 3); for (let i = 0; i < 90; i++) { const x = (hash(i, 5) * CW + t / 6) % CW, y = (hash(i, 6) * CH + t / 2) % CH; R(g, x, y, 1, 3, '#b8d4f0'); } R(g, 140, 50, 3, 24, '#ddd'); R(g, 134, 40, 16, 10, '#d94a4a'); R(g, 137, 44, 10, 2, '#fff'); },
  snow(g, t) { sky(g, '#9ab0c8', '#dde8f0'); hills(g, 66, '#e6eef4', 2); skyline(g, 78, '#9aa8b8', 4); R(g, 0, 78, CW, 30, '#f2f6fa'); for (let i = 0; i < 70; i++) { const x = (hash(i, 7) * CW + Math.sin(t / 400 + i) * 4) % CW, y = (hash(i, 8) * CH + t / 14) % CH; R(g, x, y, 1, 1, '#fff'); R(g, x + 1, y, 1, 1, '#dce8f4'); } },
  harvest(g, t) { sky(g, '#f0a860', '#fde6a8'); R(g, 150, 14, 16, 16, '#fff2b0'); hills(g, 60, '#7a9a4a', 3); for (let y = 62; y < CH; y += 4) for (let x = 0; x < CW; x++) R(g, x, y, 1, 3, hash(x >> 2, y, 9) < 0.5 ? '#e8c040' : '#d0a830'); },
  fire(g, t) { sky(g, '#1c1424', '#5a2a30'); skyline(g, 78, '#1a1420', 5); for (let i = 0; i < 40; i++) { const x = 70 + hash(i, 1) * 50, hh = 8 + hash(i, (t / 120) | 0) * 30; R(g, x, 78 - hh, 3, hh, i % 3 ? '#ff7a20' : '#ffd040'); } R(g, 0, 90, CW, 18, '#2a2030'); },
  factory(g, t) { sky(g, '#6ea0d8', '#cfe4f4'); hills(g, 66, '#5a8a58', 6); R(g, 60, 52, 70, 26, '#8a96a2'); R(g, 60, 48, 70, 4, '#aab6c2'); R(g, 140, 30, 8, 48, '#8a6a58'); for (let i = 0; i < 8; i++) R(g, 141 + Math.sin(t / 300 + i) * 3, 28 - i * 4, 7 - (i >> 1), 4, '#e8e8e8'); for (let x = 64; x < 126; x += 8) R(g, x, 58, 4, 5, '#ffd070'); R(g, 0, 78, CW, 30, '#6a7078'); },
  baekje(g, t) { sky(g, '#e8956a', '#fbd7a0'); R(g, 28, 24, 18, 18, '#fff0b0'); hills(g, 72, '#5a7a50', 7, 10); pagoda(g, 118, 82, 6, 20, '#a8a290'); R(g, 0, 82, CW, 26, '#8aa860'); for (let i = 0; i < 6; i++) tree(g, 20 + i * 28, 92 + (i % 2) * 6); },
  town(g, t) { sky(g, '#7fb0e8', '#d8ecf8'); skyline(g, 82, '#a9b4c8', 8); skyline(g, 92, '#7e8aa4', 9); R(g, 0, 92, CW, 16, '#6a7078'); for (let i = 0; i < 5; i++) { const x = (i * 40 + t / 30) % CW; R(g, x, 96, 3, 6, ['#d94a4a', '#4a8ad9', '#e8c040'][i % 3]); R(g, x, 94, 3, 2, '#f0c8a0'); } },
  bribe(g, t) { R(g, 0, 0, CW, CH, '#1a1620'); R(g, 0, 70, CW, 38, '#3a2a22'); R(g, 20, 66, 100, 6, '#5a4030'); R(g, 70, 56, 22, 12, '#f0e8d0'); R(g, 70, 56, 22, 3, '#d8cdb0'); R(g, 78, 60, 6, 1, '#999'); R(g, 150, 10, 28, 40, '#2a3548'); R(g, 154, 14, 20, 28, mix('#2a3548', '#ffe9a0', 0.15)); },
  election_win(g, t) { sky(g, '#2a2f6a', '#c06a8a'); skyline(g, 80, '#2a2a48', 10); for (let i = 0; i < 60; i++) { const x = hash(i, 11) * CW, y = (hash(i, 12) * CH + t / 18) % CH; R(g, x, y, 2, 2, ['#ffd24a', '#ff6a8a', '#6ad0ff'][i % 3]); } R(g, 40, 84, 112, 24, '#40305a'); },
  ending_full(g, t) { sky(g, '#f09a6a', '#fcd9a0'); R(g, 150, 22, 20, 20, '#fff0b0'); hills(g, 70, '#5a7a50', 11, 9); pagoda(g, 70, 86, 6, 18, '#b0aa98'); R(g, 0, 86, CW, 22, '#7ea05a'); },
  ending_retire(g, t) { SCENES.ending_full(g, t); },
  ending_defeat(g, t) { sky(g, '#38404f', '#6a7280'); skyline(g, 82, '#2a303c', 12); R(g, 0, 82, CW, 26, '#3a3f48'); for (let i = 0; i < 40; i++) R(g, (hash(i, 13) * CW + t / 40) % CW, (hash(i, 14) * CH + t / 6) % CH, 1, 2, '#9ab0c8'); },
  ending_impeach(g, t) { R(g, 0, 0, CW, CH, '#12101a'); for (let i = 0; i < 12; i++) { const x = hash(i, 15) * CW, y = hash(i, 16) * 60; R(g, x, y, 10, 14, '#e8e4d8'); R(g, x + 2, y + 3, 6, 1, '#999'); R(g, x + 2, y + 6, 6, 1, '#999'); } R(g, 60, 70, 72, 30, '#2a2438'); R(g, 80, 60, 32, 10, '#c9a040'); },
};
function tree(g, x, y) { for (let i = 0; i < 9; i++) R(g, x - Math.min(i, 4), y - 14 + i, 2 * Math.min(i, 4) + 1, 1, hash(x, i) < 0.4 ? '#2e7d3a' : '#4aa050'); R(g, x, y - 5, 1, 5, '#5a3d22'); }
export const HAS_SCENE = id => !!SCENES[id];
// 캔버스에 장면 재생(애니메이션). 반환: 정지 함수. who: { mayor:{female,name}, secretary:true }
export function playScene(canvas, id, who = {}) {
  const k = Math.max(2, Math.floor(canvas.clientWidth * (window.devicePixelRatio || 1) / CW));
  canvas.width = CW * k; canvas.height = CH * k;
  const off = document.createElement('canvas'); off.width = CW; off.height = CH; const og = off.getContext('2d'); const g = canvas.getContext('2d'); g.imageSmoothingEnabled = false;
  let on = true, t0 = performance.now(); const fn = SCENES[id] || SCENES.town;
  const loop = () => { if (!on) return; const t = performance.now() - t0; og.clearRect(0, 0, CW, CH); fn(og, t);
    const mood = /impeach|defeat|bribe|fire|flood/.test(id) ? 'sad' : 'ok';
    drawPerson(og, 28, CH - 2, { s: 1.2, female: who.female, mood, badge: true });
    if (who.secretary) drawPerson(og, 164, CH - 2, { s: 1.1, female: !who.female, suit: '#5a3a4a', hair: '#3a2a1a', mood });
    // 비네트/스캔 느낌
    for (let y = 0; y < CH; y += 2) { og.fillStyle = 'rgba(0,0,0,0.05)'; og.fillRect(0, y, CW, 1); }
    g.drawImage(off, 0, 0, CW * k, CH * k); requestAnimationFrame(loop); };
  loop(); return () => { on = false; };
}
// 시장 선택/타이틀용 초상 (캔버스에 직접)
export function drawPortrait(canvas, female) { const k = 4, W = 48, H = 56; canvas.width = W * k; canvas.height = H * k; const g = canvas.getContext('2d'); g.imageSmoothingEnabled = false; const o = document.createElement('canvas'); o.width = W; o.height = H; const og = o.getContext('2d');
  R(og, 0, 0, W, H, mix('#4a6aa8', '#b8d0f0', 0.0)); for (let i = 0; i < 8; i++) R(og, 0, i * 7, W, 7, mix('#4a6aa8', '#c8dcf4', i / 8)); R(og, 0, 44, W, 12, '#7a9a60'); pagoda(og, 38, 46, 4, 8, '#b0aa98'); drawPerson(og, 22, H, { s: 1.35, female, badge: true }); g.drawImage(o, 0, 0, W * k, H * k); }
