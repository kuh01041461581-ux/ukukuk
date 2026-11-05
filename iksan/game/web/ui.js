import { Game } from '../src/engine.js';
import { genMap } from '../src/mapgen.js';
import { MapView } from './map.js';
import { playScene, drawPortrait, HAS_SCENE } from './cutscenes.js';
const [config, events, buildings, policies, secretaries] = await Promise.all(['config', 'events', 'buildings', 'policies', 'secretaries'].map(f => fetch(`../data/${f}.json`).then(r => r.json())));
const MAPDATA = genMap(7);
const app = document.getElementById('app');
const params = new URLSearchParams(location.search);
let stage = params.get('skip') ? 'game' : 'title', profile = { gender: 'm', name: '시장', secretary: 'admin' }, g = null, mv = null, selPlot = null, stopScene = null, sceneKey = null;
const meter = (v, c) => `<div class="meter"><i style="width:${Math.max(0, Math.min(100, v))}%;${c ? 'background:' + c : ''}"></i></div>`;
const who = n => (n === '시장' ? '시장' : n + ' 시장');
const f1 = v => (Math.round(v * 10) / 10).toString();
const newGame = () => { g = new Game({ config, events, buildings, plots: MAPDATA.plots, policies, secretaries, secretary: profile.secretary, seed: Number(params.get('seed')) || (Date.now() & 0xffff), startMonth: new Date().getMonth() + 1, player: profile }); mv = null; selPlot = null; sceneKey = null; };
if (stage === 'game') newGame();

function plotPanel() {
  const p = selPlot && g.plots.find(q => q.id === selPlot); if (!p) return '<div class="zone"><small>지도에서 부지를 눌러 건설하세요. 드래그로 이동. 유적터·역 앞 부지는 복원·개발 사업을 할 수 있습니다.</small></div>';
  const z = g.zone(p.zone);
  if (p.building) return `<div class="zone"><b>${p.name ? p.built : z.name}</b> — ${buildings[p.building].name} 운영 중</div>`;
  const list = Object.entries(buildings).filter(([k, b]) => (b.landmark ? b.landmark === p.landmark : (!p.landmark && (!b.zones || b.zones.includes(p.zone)))));
  return `<div class="zone"><b>${p.name || z.name + ' · 빈 부지'}</b>` + list.map(([k, b]) => `<button data-b="${k}" style="width:100%;margin-top:4px;text-align:left">${b.name} — 예산 ${b.cost} · 인구 +${b.pop} · 월 수익 ${(b.tax - b.upkeep).toFixed(1)}${b.support ? ' · 지지도 ' + (b.support > 0 ? '+' : '') + b.support : ''}</button>`).join('') + '</div>';
}
function bindBuild(root) { root.querySelectorAll('[data-b]').forEach(b => b.onclick = () => { const r = g.act('build', { plot: selPlot, type: b.dataset.b }); if (!r.ok) alert(r.msg); render(); }); }
function scene(canvasId, id) { if (stopScene) { stopScene(); stopScene = null; } const cv = document.getElementById(canvasId); if (cv && HAS_SCENE(id)) stopScene = playScene(cv, id, { female: profile.gender === 'f', secretary: true }); }

function renderTitle() {
  app.innerHTML = `<h1 style="text-align:center;font-size:24px;margin:14px 0 4px;color:var(--acc)">익산타이쿤</h1><canvas id="sc" class="scene"></canvas><div class="card"><button id="start">새 게임 시작</button><small style="color:var(--dim)">실제 달력의 월로 시작합니다. 임기 4년 · 연임 최대 2회(12년)</small></div>`;
  scene('sc', 'baekje'); document.getElementById('start').onclick = () => { stage = 'select'; render(); };
}
function renderSelect() {
  app.innerHTML = `<h1>시장 선택</h1><div class="pick"><div data-g="m" class="${profile.gender === 'm' ? 'on' : ''}"><canvas id="pm"></canvas><b>남성 시장</b></div><div data-g="f" class="${profile.gender === 'f' ? 'on' : ''}"><canvas id="pf"></canvas><b>여성 시장</b></div></div>
    <div class="card"><h2>시장님 존함 (최대 6자)</h2><input id="nm" maxlength="6" value="${profile.name}" style="width:100%;font:inherit;padding:8px;background:#0b0f16;color:var(--tx);border:2px solid var(--line)"><h2 style="margin-top:10px">비서 선택</h2>${Object.entries(secretaries).map(([k, v]) => `<button data-s="${k}" style="text-align:left;${profile.secretary === k ? 'border-color:var(--acc)' : ''}"><b>${v.name}</b><br><small>${v.desc}</small></button>`).join('')}<button id="go">취임하기</button></div>`;
  drawPortrait(document.getElementById('pm'), false); drawPortrait(document.getElementById('pf'), true);
  app.querySelectorAll('[data-g]').forEach(d => d.onclick = () => { profile.name = document.getElementById('nm').value || '시장'; profile.gender = d.dataset.g; renderSelect(); });
  app.querySelectorAll('[data-s]').forEach(d => d.onclick = () => { profile.name = document.getElementById('nm').value || '시장'; profile.secretary = d.dataset.s; renderSelect(); });
  document.getElementById('go').onclick = () => { profile.name = document.getElementById('nm').value.trim() || '시장'; stage = 'game'; newGame(); render(); };
}
function render() {
  if (stage === 'title') return renderTitle();
  if (stage === 'select') return renderSelect();
  const s = g.s, sk = g.scene ? g.scene + '|' + s.monthIndex + '|' + g.phase : null;
  let h = `<h1>익산타이쿤 <small style="color:var(--dim)">${profile.gender === 'f' ? '여' : '남'}성 · ${who(profile.name)}</small></h1><div class="date"><span>임기 ${s.term}기 ${g.termYear}년 ${g.calMonth}월</span><span>남은 ${g.monthsLeftInTerm}개월 · 행동 ${s.ap}</span></div>
  <div class="bar">
   <div class="stat">예산<b>${f1(s.budget)}억</b></div><div class="stat">인구<b>${f1(s.population)}만</b></div>
   <div class="stat">지지도<b>${Math.round(s.support)}</b>${meter(s.support)}</div><div class="stat">피로<b>${Math.round(s.fatigue)}</b>${meter(s.fatigue, 'var(--bad)')}</div>
   <div class="stat">정치력<b>${Math.round(s.politics)}</b>${meter(s.politics, '#6ab0ff')}</div><div class="stat">청렴도<b>${Math.round(s.integrity)}</b>${meter(s.integrity)}</div>
   <div class="stat">감사위험<b>${Math.round(s.auditRisk)}</b>${meter(s.auditRisk, 'var(--bad)')}</div><div class="stat">감사<b>${s.audits}/${config.audit.impeachAudits}</b></div></div>`;
  const showScene = g.scene && HAS_SCENE(g.scene);
  if (g.phase === 'ending') {
    const e = g.ending; h += `${showScene ? '<canvas id="sc" class="scene"></canvas>' : ''}<div class="card end"><h2>${who(e.name)} — ${e.text}</h2><div class="grade">${e.grade}</div>
      <p>재임 ${e.years}년 · ${e.term}기 · 인구 ${f1(e.population)}만 · 지지도 ${e.support} · 뒷돈 ${e.gifts}회</p><button id="again">새 게임</button></div>`;
  } else if (g.decision) {
    h += `${showScene ? '<canvas id="sc" class="scene"></canvas>' : ''}<div class="card"><h2>${g.decision.title}</h2><div>${g.decision.text}</div>` + g.decision.choices.map((c, i) => `<button data-c="${i}">${c.label}</button>`).join('') + `</div>`;
  } else {
    h += `<div class="cmds"><button data-a="rest">휴식</button><button data-a="meet">시민 간담회</button><button data-a="visit_province">도청 방문</button><button data-a="visit_national">국회 방문 (행동 2)</button></div>
      <div class="zone"><b>${g.sec.name}</b><br><small>“${g.advice()}”</small></div><canvas id="map"></canvas><div id="plotpanel">${plotPanel()}</div><div class="zones">` + g.zones.map(z => `<div class="zone"><b>${z.name}</b> <small>발전도 ${Math.round(z.level)}</small>${meter(z.level)}</div>`).join('') + `</div><details class="zone"><summary><b>정책 (${g.active.length}/3 시행 중)</b></summary>` + Object.entries(policies).map(([k, p]) => g.active.includes(k) ? `<button data-rp="${k}" style="width:100%;margin-top:4px;text-align:left;border-color:var(--ok)">✔ ${p.name} — 철회</button>` : `<button data-ap="${k}" style="width:100%;margin-top:4px;text-align:left">${p.name} — 예산 ${p.cost} · 월 유지 ${p.upkeep}<br><small>${p.desc}</small></button>`).join('') + `</details><button id="end" style="width:100%">이번 달 마무리 ▶</button>`;
  }
  h += `<div id="log">${g.log.slice(-8).reverse().map(l => `· ${l.text}`).join('<br>')}</div>`;
  app.innerHTML = h;
  if (showScene) { if (sk !== sceneKey) sceneKey = sk; scene('sc', g.scene); } else if (stopScene) { stopScene(); stopScene = null; }
  app.querySelectorAll('[data-a]').forEach(b => b.onclick = () => { g.act(b.dataset.a); render(); });
  app.querySelectorAll('[data-c]').forEach(b => b.onclick = () => { g.phase === 'election' ? g.electionChoice(+b.dataset.c) : g.choose(+b.dataset.c); render(); });
  const e = document.getElementById('end'); if (e) e.onclick = () => { g.endMonth(); render(); };
  const a = document.getElementById('again'); if (a) a.onclick = () => { stage = 'title'; render(); };
  app.querySelectorAll('[data-ap]').forEach(b => b.onclick = () => { const r = g.adopt(b.dataset.ap); if (!r.ok) alert(r.msg); render(); });
  app.querySelectorAll('[data-rp]').forEach(b => b.onclick = () => { g.repeal(b.dataset.rp); render(); });
  bindBuild(app);
  const cv = document.getElementById('map');
  if (cv) { mv = new MapView(cv, g, MAPDATA, p => { selPlot = p && p.id; const pp = document.getElementById('plotpanel'); pp.innerHTML = plotPanel(); bindBuild(pp); }); if (selPlot) mv.sel = selPlot; }
}
render();
window.__game = () => g; window.__render = render;
