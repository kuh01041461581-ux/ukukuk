// 익산 개략 지도(타일) 생성기. 위치는 *개략*이며 공식 지도 확보 후 data 로 교체 예정.
import { mulberry32 } from './engine.js';
export const MAP_W = 38, MAP_H = 28;
// 권역 사각형 [x0,y0,x1,y1] (타일 좌표)
// 방위(검색 확인): 금마·여산 북동, 왕궁 동·남동(완주 접경), 삼기·낭산 북, 함열 북서, 황등·함라 서(군산 접경), 도심 중앙, 만경강 남쪽(김제)
export const ZONE_RECT = { baekje: [26, 1, 37, 8], ind: [8, 1, 22, 8], old: [14, 12, 25, 19], new: [26, 13, 37, 20], farm: [4, 22, 31, 27] };
export function genMap(seed = 7) {
  const rng = mulberry32(seed);
  const t = Array.from({ length: MAP_H }, () => Array(MAP_W).fill('grass'));
  // 산: 북쪽(미륵산)·북서(함라산)
  for (let y = 0; y < 2; y++) for (let x = 0; x < MAP_W; x++) if (rng() < 0.6) t[y][x] = 'mountain';       // 북쪽 능선(금강 방향)
  for (let y = 2; y < 10; y++) for (let x = 30; x < MAP_W; x++) if (rng() < 0.12) t[y][x] = 'mountain';   // 미륵산·여산 일대 산세
  // 강: 만경강(남쪽, 굽이침), 금강 일부(북서→서)
  for (let x = 0; x < MAP_W; x++) { const y = 21 + Math.round(Math.sin(x / 4) * 0.8); if (y >= 20 && y < MAP_H) { t[y][x] = 'water'; if (x % 3 === 0) t[y + 1][x] = 'water'; } }
  for (let y = 5; y < 22; y++) { const x = Math.round(1 + Math.sin(y / 3)); if (x >= 0) t[y][x] = 'water'; }
  // 농지: 남·서 일부
  for (let y = 22; y < MAP_H; y++) for (let x = 0; x < MAP_W; x++) if (t[y][x] === 'grass' && rng() < 0.7) t[y][x] = 'field';
  // 권역 바닥(도시 포장) 및 도로 격자
  const plots = [];
  for (const [id, [x0, y0, x1, y1]] of Object.entries(ZONE_RECT)) {
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      if (t[y][x] === 'water') continue;
      const road = ((x - x0) % 4 === 0) || ((y - y0) % 4 === 0);
      t[y][x] = (id === 'farm' || id === 'baekje') ? (road ? 'road' : (id === 'farm' && rng() < 0.6 ? 'field' : 'grass')) : (road ? 'road' : 'city');
    }
    // 부지: 도로로 둘러싸인 칸 중앙 (2,2) 간격
    let n = 0;
    for (let y = y0 + 1; y < y1 && n < 6; y += 4) for (let x = x0 + 1; x < x1 && n < 6; x += 4) { if (t[y][x] === 'water') continue; plots.push({ id: `${id}${n}`, zone: id, x: x + 1, y: y + 1, building: null }); n++; }
  }
  // 랜드마크: 처음엔 '유적터/역 부지'로 비어 있고, 복원·개발하면 상징 건물이 나타남
  const LM = { baekje0: { landmark: 'mireuk', name: '미륵사지 유적터', built: '미륵사지 석탑' }, baekje5: { landmark: 'wanggung', name: '왕궁리 유적터', built: '왕궁리 오층석탑' }, baekje2: { landmark: 'food', name: '국가식품클러스터 부지(왕궁)', built: '국가식품클러스터' }, old1: { landmark: 'station', name: '익산역(기존 역사)', built: '익산역' } };
  for (const p of plots) if (LM[p.id]) Object.assign(p, LM[p.id]);
  // 단지(lot): 부지 중심의 3x3 블록을 하나의 필지로 묶고, 가장 가까운 도로 쪽 가운데 칸을 진입로(drive)로 둔다
  const lots = {};
  for (const p of plots) { if (p.id === 'old1') continue; const key = (x, y) => x + ',' + y;
    let dir = null; for (const [dx, dy] of [[0, -2], [0, 2], [-2, 0], [2, 0]]) if (t[p.y + dy] && t[p.y + dy][p.x + dx] === 'road') { dir = [dx / 2, dy / 2]; break; }
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const x = p.x + dx, y = p.y + dy; if (t[y][x] === 'road' || t[y][x] === 'water') continue; t[y][x] = 'lot';
      lots[key(x, y)] = { id: p.id, role: (dx === 0 && dy === 0) ? 'center' : (dir && dx === dir[0] && dy === dir[1]) ? 'drive' : 'yard', dir, dx, dy }; } }
  // 철도: 호남선(동서, y=10) + 장항선(북쪽 x=24) — 도로와 떨어진 전용 선로(완충 행 y=9·11)이고, 도로와는 건널목에서만 만난다.
  // 익산역: 분기점 (24,10), 승강장 (22~26,10), 기존 역사 부지 (23,9)
  const RY = 10, RX = 24;
  for (let x = 0; x < MAP_W; x++) t[RY][x] = 'rail';
  for (let y = 0; y < RY; y++) if (t[y][RX] !== 'water') t[y][RX] = 'rail';
  for (const x of [18, 30]) { for (let y = 9; y <= 12; y++) if (t[y][x] !== 'city') t[y][x] = 'road'; t[RY][x] = 'cross'; }          // 도로 연결 + 건널목
  for (let x = 22; x <= 26; x++) t[RY][x] = 'platform';
  for (let y = 9; y <= 11; y++) for (let x = 0; x < MAP_W; x++) if (y !== 10 && t[y][x] === 'mountain') t[y][x] = 'grass';
  const st = plots.find(p => p.id === 'old1'); if (st) { st.x = 23; st.y = 9; st.building = 'station_old'; }
  // 권역 간 연결 도로(남북): 도심 ↔ 남쪽 농업 권역
  for (let y = 20; y < 22; y++) if (t[y][18] !== 'water' && t[y][18] !== 'city') t[y][18] = 'road';
  return { w: MAP_W, h: MAP_H, tiles: t, plots, lots };
}
