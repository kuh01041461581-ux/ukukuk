import { makeSprites, roadSprite, railSprite, crossSprite, TW, TH, CX, CY } from './sprites.js';
// 아이소메트릭 맵 렌더러: 드래그로 이동, 탭으로 부지 선택. 도트는 정수 배율로 확대.
export class MapView {
  constructor(canvas, game, mapData, onSelect) {
    this.cv = canvas; this.g = game; this.m = mapData; this.onSelect = onSelect; this.S = makeSprites(); this.sel = null;
    this.zoom = 3; this.ox = 0; this.oy = 0; this.t0 = performance.now();
    this._bind(); this.fit(); this.loop();
  }
  fit() { const r = this.cv.getBoundingClientRect(); const dpr = window.devicePixelRatio || 1; this.cv.width = Math.round(r.width * dpr); this.cv.height = Math.round(r.height * dpr); this.dpr = dpr;
    this.zoom = Math.max(2, Math.round(1.5 * dpr)); this.center(this.g.plots.find(p => p.landmark === 'mireuk') || this.m.plots[0]); }
  w2s(x, y) { return [(x - y) * (TW / 2), (x + y) * (TH / 2)]; }
  center(p) { const [sx, sy] = this.w2s(p.x, p.y); this.ox = this.cv.width / 2 - sx * this.zoom; this.oy = this.cv.height / 2 - sy * this.zoom; }
  _bind() {
    let down = null, moved = false;
    const pos = e => { const r = this.cv.getBoundingClientRect(); return [(e.clientX - r.left) * this.dpr, (e.clientY - r.top) * this.dpr]; };
    this.cv.addEventListener('pointerdown', e => { down = pos(e); moved = false; this.cv.setPointerCapture(e.pointerId); this._last = down; });
    this.cv.addEventListener('pointermove', e => { if (!down) return; const p = pos(e); if (Math.hypot(p[0] - down[0], p[1] - down[1]) > 8 * this.dpr) moved = true; if (moved) { this.ox += p[0] - this._last[0]; this.oy += p[1] - this._last[1]; } this._last = p; });
    this.cv.addEventListener('pointerup', e => { if (down && !moved) this.tap(...pos(e)); down = null; });
    this.cv.addEventListener('wheel', e => { e.preventDefault(); this.zoom = Math.max(1, Math.min(8, this.zoom + (e.deltaY < 0 ? 1 : -1))); }, { passive: false });
  }
  tap(px, py) {
    let best = null, bd = 1e9;
    for (const p of this.g.plots) { const [sx, sy] = this.w2s(p.x, p.y); const x = this.ox + sx * this.zoom, y = this.oy + (sy + 4) * this.zoom; const d = Math.hypot(px - x, py - y); if (d < bd) { bd = d; best = p; } }
    if (best && bd < 22 * this.zoom) { this.sel = best.id; this.onSelect(best); } else { this.sel = null; this.onSelect(null); }
  }
  // 도로·철도 자동 타일링: 이웃 연결 마스크로 스프라이트 선택
  linear(x, y, t) {
    const T = (xx, yy) => (xx >= 0 && yy >= 0 && xx < this.m.w && yy < this.m.h) ? this.m.tiles[yy][xx] : null;
    const roadish = n => n === 'road' || n === 'cross', railish = n => n === 'rail' || n === 'cross' || n === 'platform';
    const mk = f => (f(T(x + 1, y)) ? 1 : 0) | (f(T(x - 1, y)) ? 2 : 0) | (f(T(x, y + 1)) ? 4 : 0) | (f(T(x, y - 1)) ? 8 : 0);
    let city = false; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if (T(x + dx, y + dy) === 'city') city = true;
    const base = city ? this.S.city[0] : this.S.grass[(x * 5 + y * 3) % 3], key = city ? 'c' : 'g' + ((x * 5 + y * 3) % 3);
    this.lin = this.lin || new Map(); const ck = x + ',' + y; if (this.lin.has(ck)) return this.lin.get(ck);
    let sp;
    if (t === 'road') sp = roadSprite(base, mk(roadish), key);
    else if (t === 'rail') sp = railSprite(base, mk(railish), key);
    else if (t === 'platform') sp = railSprite(base, mk(railish), key, true);
    else { const railH = railish(T(x + 1, y)) || railish(T(x - 1, y)); sp = crossSprite(base, mk(roadish) & (railH ? 12 : 3) || (railH ? 12 : 3), railH, key); }
    this.lin.set(ck, sp); return sp;
  }
  loop() { this.draw(); requestAnimationFrame(() => this.loop()); }
  draw() {
    const c = this.cv, g = c.getContext('2d'); g.imageSmoothingEnabled = false; g.fillStyle = '#1a2a3a'; g.fillRect(0, 0, c.width, c.height);
    const z = this.zoom, S = this.S, m = this.m, frame = ((performance.now() - this.t0) / 600 | 0) % 2;
    const plotAt = new Map(this.g.plots.map(p => [p.x + ',' + p.y, p]));
    const order = []; for (let s = 0; s < m.w + m.h - 1; s++) for (let x = 0; x < m.w; x++) { const y = s - x; if (y >= 0 && y < m.h) order.push([x, y]); }
    for (const [x, y] of order) {
      const [sx, sy] = this.w2s(x, y), dx = Math.round(this.ox + (sx - CX) * z), dy = Math.round(this.oy + (sy - CY + 8) * z);
      if (dx < -40 * z || dy < -70 * z || dx > c.width || dy > c.height + 20 * z) continue;
      const t = m.tiles[y][x], v = (x * 7 + y * 13) & 1; let spr;
      switch (t) { case 'grass': spr = S.grass[(x * 5 + y * 3) % 3]; break; case 'city': spr = S.city[v]; break; case 'road': case 'rail': case 'cross': case 'platform': spr = this.linear(x, y, t); break; case 'water': spr = S.water[frame]; break; case 'field': spr = S.field[v]; break; case 'mountain': spr = S.mountain[(x * 3 + y * 7) % 3]; break; default: spr = S.grass[0]; }
      g.drawImage(spr, dx, dy, 32 * z, 64 * z);
      const p = plotAt.get(x + ',' + y);
      if (!p) { const hv = ((x * 73856093) ^ (y * 19349663)) >>> 0; const r = (hv % 100) / 100;
        if (t === 'city' && r < 0.55) g.drawImage(S.deco[hv % 4], dx, dy, 32 * z, 64 * z);
        else if (t === 'grass' && r < (this.m.tiles[y][x] === 'grass' ? 0.2 : 0)) g.drawImage(S.tree[hv % 3], dx, dy, 32 * z, 64 * z); }
      if (p) { const b = p.building ? S.b[p.building] : (p.landmark ? S.b['ruin_' + (p.landmark === 'station' ? 'station' : p.landmark)] : (this.sel === p.id ? S.marker : S.empty)); if (this.sel === p.id) g.drawImage(S.marker, dx, dy, 32 * z, 64 * z); g.drawImage(b, dx, dy, 32 * z, 64 * z); }
    }
  }
}
