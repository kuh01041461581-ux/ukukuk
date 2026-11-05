import { Game } from '../src/engine.js';
import { genMap } from '../src/mapgen.js';
import { MapView } from './map.js';
const [config, events, buildings] = await Promise.all(['config', 'events', 'buildings'].map(f => fetch(`../data/${f}.json`).then(r => r.json())));
const MAPDATA = genMap(7);
let mv = null, selPlot = null;
const app = document.getElementById('app');
const seed = Number(new URLSearchParams(location.search).get('seed')) || (Date.now() & 0xffff);
let g = new Game({ config, events, buildings, plots: MAPDATA.plots, seed, startMonth: new Date().getMonth() + 1 });   // 실제 달력의 월로 시작
const meter = (v, c) => `<div class="meter"><i style="width:${Math.max(0, Math.min(100, v))}%;${c ? 'background:' + c : ''}"></i></div>`;
const f1 = v => (Math.round(v * 10) / 10).toString();
function plotPanel() {
  const p = selPlot && g.plots.find(q => q.id === selPlot); if (!p) return '<div class="zone"><small>지도에서 부지(노란 칸)를 눌러 건설하세요. 드래그로 이동, 휠로 확대.</small></div>';
  const z = g.zone(p.zone);
  if (p.building) return `<div class="zone"><b>${z.name}</b> — ${buildings[p.building].name} 운영 중</div>`;
  return `<div class="zone"><b>${z.name} · 빈 부지</b>` + Object.entries(buildings).filter(([k, b]) => !b.zones || b.zones.includes(p.zone))
    .map(([k, b]) => `<button data-b="${k}" style="width:100%;margin-top:4px;text-align:left">${b.name} — 예산 ${b.cost} · 인구 +${b.pop} · 월 수익 ${(b.tax - b.upkeep).toFixed(1)}${b.support ? ' · 지지도 ' + (b.support > 0 ? '+' : '') + b.support : ''}</button>`).join('') + '</div>';
}
function render() {
  const s = g.s;
  let h = `<h1>익산타이쿤</h1><div class="date"><span>임기 ${g.s.term}기 ${g.termYear}년 ${g.calMonth}월</span><span>남은 ${g.monthsLeftInTerm}개월 · 행동 ${s.ap}</span></div>
  <div class="bar">
   <div class="stat">예산<b>${f1(s.budget)}억</b></div><div class="stat">인구<b>${f1(s.population)}만</b></div>
   <div class="stat">지지도<b>${Math.round(s.support)}</b>${meter(s.support)}</div><div class="stat">피로<b>${Math.round(s.fatigue)}</b>${meter(s.fatigue, 'var(--bad)')}</div>
   <div class="stat">정치력<b>${Math.round(s.politics)}</b>${meter(s.politics, '#6ab0ff')}</div><div class="stat">청렴도<b>${Math.round(s.integrity)}</b>${meter(s.integrity)}</div>
   <div class="stat">감사위험<b>${Math.round(s.auditRisk)}</b>${meter(s.auditRisk, 'var(--bad)')}</div><div class="stat">감사<b>${s.audits}/${config.audit.impeachAudits}</b></div></div>`;
  if (g.phase === 'ending') {
    const e = g.ending; h += `<div class="card end"><h2>엔딩 — ${e.text}</h2><div class="grade">${e.grade}</div>
      <p>재임 ${e.years}년 · ${e.term}기 · 인구 ${f1(e.population)}만 · 지지도 ${e.support} · 뒷돈 ${e.gifts}회</p><button id="again">새 게임</button></div>`;
  } else if (g.decision) {
    h += `<div class="card"><h2>${g.decision.title}</h2><div>${g.decision.text}</div>` +
      g.decision.choices.map((c, i) => `<button data-c="${i}">${c.label}</button>`).join('') + `</div>`;
  } else {
    h += `<div class="cmds"><button data-a="rest">휴식</button><button data-a="meet">시민 간담회</button>
      <button data-a="visit_province">도청 방문</button><button data-a="visit_national">국회 방문 (행동 2)</button></div>
      <canvas id="map"></canvas><div id="plotpanel">${plotPanel()}</div>
      <div class="zones">` + g.zones.map(z => `<div class="zone"><b>${z.name}</b> <small>발전도 ${Math.round(z.level)}</small>${meter(z.level)}</div>`).join('') +
      `</div><button id="end" style="width:100%">이번 달 마무리 ▶</button>`;
  }
  h += `<div id="log">${g.log.slice(-8).reverse().map(l => `· ${l.text}`).join('<br>')}</div>`;
  app.innerHTML = h;
  app.querySelectorAll('[data-a]').forEach(b => b.onclick = () => { g.act(b.dataset.a); render(); });
  app.querySelectorAll('[data-b]').forEach(b => b.onclick = () => { const r = g.act('build', { plot: selPlot, type: b.dataset.b }); if (!r.ok) alert(r.msg); render(); });
  const cv = document.getElementById('map'); if (cv) { mv = new MapView(cv, g, MAPDATA, p => { selPlot = p && p.id; const pp = document.getElementById('plotpanel'); pp.innerHTML = plotPanel(); pp.querySelectorAll('[data-b]').forEach(b => b.onclick = () => { const r = g.act('build', { plot: selPlot, type: b.dataset.b }); if (!r.ok) alert(r.msg); render(); }); }); if (selPlot) mv.sel = selPlot; }
  app.querySelectorAll('[data-c]').forEach(b => b.onclick = () => { g.phase === 'election' ? g.electionChoice(+b.dataset.c) : g.choose(+b.dataset.c); render(); });
  const e = document.getElementById('end'); if (e) e.onclick = () => { g.endMonth(); render(); };
  const a = document.getElementById('again'); if (a) a.onclick = () => { g = new Game({ config, events, buildings, plots: MAPDATA.plots, seed: Date.now() & 0xffff, startMonth: new Date().getMonth() + 1 }); mv = null; selPlot = null; render(); };
}
render();
