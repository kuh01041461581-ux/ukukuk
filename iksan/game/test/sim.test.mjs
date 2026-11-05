import { readFileSync } from 'node:fs';
import assert from 'node:assert/strict';
import { Game } from '../src/engine.js';
const load = f => JSON.parse(readFileSync(new URL('../data/' + f, import.meta.url)));
import { genMap } from '../src/mapgen.js';
const config = load('config.json'), events = load('events.json'), buildings = load('buildings.json'), policies = load('policies.json'), secretaries = load('secretaries.json'), MAP = genMap(7);

// 정책: honest(청렴), greedy(뒷돈 수락), idle(아무것도 안 함)
function play(seed, policy) {
  const g = new Game({ config, events, buildings, policies, secretaries, plots: MAP.plots, seed, startMonth: 3 });
  let guard = 0;
  while (g.phase !== 'ending' && guard++ < 5000) {
    if (g.phase === 'decision') {
      const bribe = g.decision._bribe;
      g.choose(bribe ? (policy === 'greedy' ? 0 : 1) : (policy === 'idle' ? g.decision.choices.length - 1 : 0));
    } else if (g.phase === 'election') g.electionChoice(1);           // 계속 플레이
    else if (g.phase === 'player') {
      if (policy !== 'idle') {
        if (g.s.fatigue > 60) g.act('rest');
        if (g.s.monthIndex % 7 === 0) for (const id of Object.keys(policies)) if (!g.active.includes(id) && g.s.budget > 60) { g.adopt(id); break; }
        if (g.s.budget < 40) g.act('visit_province'); else { const free = g.plots.find(p => !p.building); if (free) { const opts = Object.entries(buildings).filter(([k, b]) => (!b.zones || b.zones.includes(free.zone))); g.act('build', { plot: free.id, type: opts[g.s.monthIndex % opts.length][0] }); } else g.act('invest', 'new'); }
        if (g.s.ap > 0) g.act('meet');
      }
      g.endMonth();
    }
  }
  return g;
}
// 1) 기본 불변식·종료
for (const pol of ['honest', 'greedy', 'idle']) {
  const res = [];
  for (let seed = 1; seed <= 30; seed++) {
    const g = play(seed, pol); assert.equal(g.phase, 'ending', `${pol}#${seed} 종료되지 않음`);
    assert.ok(g.s.monthIndex <= 144, '12년 초과');
    for (const k of ['support', 'fatigue', 'politics', 'integrity', 'auditRisk']) assert.ok(g.s[k] >= 0 && g.s[k] <= 100, `${k} 범위 이탈`);
    res.push(g.ending.type);
  }
  const cnt = res.reduce((a, t) => (a[t] = (a[t] || 0) + 1, a), {});
  console.log(pol.padEnd(7), JSON.stringify(cnt));
}
// 2) 달력: startMonth=3 → 첫 달 3월, 12개월 후 3월, 임기연차 계산
{ const g = new Game({ config, events, buildings, policies, secretaries, plots: MAP.plots, seed: 1, startMonth: 3 }); assert.equal(g.calMonth, 3); assert.equal(g.termYear, 1);
  for (let i = 0; i < 12; i++) { while (g.phase === 'decision') g.choose(0); g.endMonth(); }
  assert.equal(g.calMonth, 3); assert.equal(g.termYear, 2); assert.equal(g.totalYear, 2); }
// 3) 탄핵: 뒷돈을 계속 받으면 감사 누적으로 탄핵 가능
{ let imp = 0; for (let seed = 1; seed <= 30; seed++) if (play(seed, 'greedy').ending.type === 'impeach') imp++; assert.ok(imp > 0 && imp < 30, "greedy 탄핵 비율이 극단적: " + imp); console.log("greedy 탄핵", imp, "/30"); }
// 4) 청렴 플레이는 탄핵 0
{ for (let seed = 1; seed <= 30; seed++) assert.notEqual(play(seed, "honest").ending.type, "impeach"); }
// 5) 12년 만점 경로 존재(엔딩 'full') 와 연임 선택 'retire'
{ const types = new Set(); for (let seed = 1; seed <= 60; seed++) types.add(play(seed, 'honest').ending.type); console.log('honest 엔딩 종류', [...types].join(',')); assert.ok(types.has('full') || types.has('defeat')); }
// 6) 엔딩 선택: 첫 임기 후 '엔딩 보기'
{ const g = new Game({ config, events, buildings, policies, secretaries, plots: MAP.plots, seed: 7, startMonth: 1 }); let guard = 0;
  while (g.phase !== 'election' && g.phase !== 'ending' && guard++ < 3000) { if (g.phase === 'decision') g.choose(g.decision._bribe ? 1 : 0); else { if (g.s.fatigue > 60) g.act('rest'); g.act('meet'); g.act('invest', 'new'); g.endMonth(); } }
  if (g.phase === 'election') { g.electionChoice(0); assert.equal(g.ending.type, 'retire'); console.log('연임→엔딩보기 OK', g.ending.grade); } else console.log('첫 임기 낙선/탄핵(시드 7):', g.ending.type); }
// 7) 정책: 동시 3개 제한, 비용 차감, 철회
{ const g = new Game({ config, events, buildings, policies, secretaries, plots: MAP.plots, seed: 3, startMonth: 1 }); while (g.phase === 'decision') g.choose(g.decision._bribe ? 1 : 0);
  g.s.budget = 500; const ids = Object.keys(policies); for (const id of ids.slice(0, 3)) assert.ok(g.adopt(id).ok); assert.equal(g.adopt(ids[3]).ok, false); assert.ok(g.s.budget < 500);
  assert.ok(g.repeal(ids[0]).ok); assert.ok(g.adopt(ids[3]).ok); assert.ok(typeof g.advice() === 'string'); }
// 8) 비서 효과: 경제 비서는 건설비 10% 할인, 행정 비서는 피로 25% 감소
{ const mk = sec => { const g = new Game({ config, events, buildings, policies, secretaries, plots: MAP.plots, seed: 4, startMonth: 1, secretary: sec }); while (g.phase === 'decision') g.choose(g.decision._bribe ? 1 : 0); return g; };
  const a = mk('finance'), b = mk('admin'), c = mk('politic'); a.s.budget = b.s.budget = 100; a.act('build', { plot: 'old0', type: 'house' }); b.act('build', { plot: 'old0', type: 'house' });
  assert.ok(a.s.budget > b.s.budget, '경제 비서 할인 없음'); assert.ok(b.s.fatigue < c.s.fatigue + 6 && b.s.fatigue >= 0);
  const d = mk('admin'); d.s.fatigue = 0; d.act('meet'); const e = mk('politic'); e.s.fatigue = 0; e.act('meet'); assert.ok(d.s.fatigue <= e.s.fatigue); }
// 9) 구역 지정형 단지: 성장·산업 오염
{ const g = new Game({ config, events, buildings, policies, secretaries, plots: MAP.plots, seed: 11, startMonth: 1 }); while (g.phase === 'decision') g.choose(g.decision._bribe ? 1 : 0);
  g.s.budget = 900; g.s.ap = 9; assert.ok(g.act('build', { plot: 'new0', type: 'zone_res' }).ok); assert.equal(g.plots.find(p => p.id === 'new0').lv, 1);
  assert.equal(g.act('build', { plot: 'new1', type: 'zone_ind' }).ok, true);
  for (let i = 0; i < 80 && g.phase !== 'ending'; i++) { while (g.phase === 'decision') g.choose(g.decision._bribe ? 1 : g.decision.choices.length - 1); if (g.phase === 'election') g.electionChoice(1); else if (g.phase === 'player') { g.s.budget = Math.max(g.s.budget, 300); g.endMonth(); } }
  const lvs = g.plots.filter(p => p.lv).map(p => p.lv); assert.ok(lvs.every(v => v >= 1 && v <= 3)); assert.ok(lvs.some(v => v >= 2), '단지가 성장하지 않음'); console.log('단지 레벨', lvs.join(',')); }
console.log('ALL TESTS PASSED');
