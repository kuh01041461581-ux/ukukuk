/* ===== 그래프 스타일: 색감·모서리·막대 모양을 골라서 앱 전체 그래프에 적용 (모두 평면·무광 — 유리/네온/그라데이션 없음) ===== */
const CHART_THEMES = {
  blue:   { name: '파랑 · 기본', note: '차분한 파랑, 모서리 약간 둥글게', fill: '#2a78d6', fillD: '#3987e5', track: '#e6edf8', trackD: '#2a3345', short: 'rgba(217,143,0,.20)', ink: '#7a5000', inkD: '#f0c060', r: '4px', tr: '4px', ramp: ['#e1e5ec', '#dce9fb', '#bad4f6', '#8fb9ef', '#6199e4', '#3a78cc'] },
  green:  { name: '초록 · 안정', note: '눈이 편한 청록 계열, 부드러운 모서리', fill: '#14866a', fillD: '#2fb58a', track: '#e1f0ea', trackD: '#25352f', short: 'rgba(217,143,0,.22)', ink: '#6b4500', inkD: '#f0c060', r: '6px', tr: '6px', ramp: ['#e1e7e4', '#d7efe4', '#aedcc9', '#7ec6a9', '#43a583', '#1a7d63'] },
  navy:   { name: '남색 · 진중', note: '진한 남색, 각진 모서리 (보고서 느낌)', fill: '#1f3f75', fillD: '#7f9fe0', track: '#e2e7f0', trackD: '#2a3142', short: 'rgba(217,143,0,.24)', ink: '#6b4500', inkD: '#f0c060', r: '2px', tr: '2px', ramp: ['#e1e5ec', '#dde4f1', '#b6c4e2', '#869dcb', '#5675ae', '#2c4a85'] },
  indigo: { name: '보라 · 부드럽게', note: '인디고, 알약처럼 둥근 막대', fill: '#5a56d6', fillD: '#8f8cf2', track: '#ebebf9', trackD: '#2d2d45', short: 'rgba(217,143,0,.20)', ink: '#6b4500', inkD: '#f0c060', r: '999px', tr: '10px', ramp: ['#e4e4ee', '#e7e6fa', '#cac8f4', '#a7a4ec', '#827ee2', '#5a56d6'] },
};
const TM_LABELS = ['0%', '1~24%', '25~49%', '50~74%', '75~94%', '95%~'];
function chartThemeKey() { const k = S && S.opt && S.opt.chartTheme; return CHART_THEMES[k] ? k : 'blue'; }
const tmRamp = () => CHART_THEMES[chartThemeKey()].ramp;
const tmB = () => TM_LABELS.map((l, i) => [l, tmRamp()[i]]);

/* 선택한 스타일을 CSS 변수로 적용 (어두운 화면이면 어두운 단계) */
function applyChartTheme() {
  const t = CHART_THEMES[chartThemeKey()], dk = typeof isDarkUI === 'function' && isDarkUI(), st = document.documentElement.style;
  st.setProperty('--cs-fill', dk ? t.fillD : t.fill); st.setProperty('--cs-track', dk ? t.trackD : t.track);
  st.setProperty('--cs-short', t.short); st.setProperty('--cs-short-ink', dk ? t.inkD : t.ink); st.setProperty('--cs-r', t.r); st.setProperty('--cs-tr', t.tr);
}
/* 카드마다 그 스타일의 변수를 직접 달아 실제 모양 그대로 미리보기 */
function chartThemeCardHTML() {
  const cur = chartThemeKey(), el = 85;
  return `<div class="card"><h2>그래프 스타일 <span class="muted" style="font-weight:400;font-size:12px">모든 그래프(막대·예산 지도·분포·추이)에 한 번에 적용됩니다</span></h2>
    <div class="ctgrid">${Object.entries(CHART_THEMES).map(([k, t]) => `<button class="ctcard ${k === cur ? 'on' : ''}" data-ct="${k}" aria-pressed="${k === cur}" style="--cs-fill:${t.fill};--cs-track:${t.track};--cs-short:${t.short};--cs-short-ink:${t.ink};--cs-r:${t.r};--cs-tr:${t.tr}">
      <div class="ctname">${t.name}${k === cur ? ' <span class="badge good">사용 중</span>' : ''}</div><div class="muted" style="font-size:12px;margin-bottom:8px">${t.note}</div>
      ${easyBar({ rate: 52, gap: 0, budget: 100 }, el, false, '', false)}
      <div class="ctramp">${t.ramp.map(c => `<i style="background:${c};border-radius:${t.tr}"></i>`).join('')}</div></button>`).join('')}</div></div>`;
}
document.addEventListener('click', e => {
  const b = e.target.closest('[data-ct]'); if (!b || !b.closest('#p-settings')) return;
  S.opt = S.opt || {}; S.opt.chartTheme = b.dataset.ct; save(); applyChartTheme(); renderSettings(); toast('그래프 스타일: ' + CHART_THEMES[S.opt.chartTheme].name);
});
