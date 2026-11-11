'use strict';
/* ===== 공통 도구 ===== */
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const won = n => (n == null || isNaN(n)) ? '' : Math.round(n).toLocaleString('ko-KR');
const pct = (a, b) => b ? (a / b * 100) : 0;
const pct1 = v => (Math.round(v * 10) / 10).toFixed(1) + '%';
const sum = (arr, f) => arr.reduce((s, x) => s + (f ? f(x) : x || 0), 0);
const MON = ['1월', '2월', '3월', '4월', '5월', '6월', '7월', '8월', '9월', '10월', '11월', '12월'];

/* 입력칸: '1,234' '12000+3400' 같은 간단한 계산식 허용 */
function parseNum(s) {
  if (s == null) return null;
  s = String(s).replace(/[,원\s]/g, '');
  if (s === '') return null;
  if (!/^[\d.+\-*/()]+$/.test(s)) return NaN;
  try { const v = Function('"use strict";return (' + s + ')')(); return isFinite(v) ? Math.round(v) : NaN; } catch (e) { return NaN; }
}

function toast(msg) {
  const t = $('#toast'); t.textContent = msg; t.classList.add('on');
  clearTimeout(toast._t); toast._t = setTimeout(() => t.classList.remove('on'), 2200);
}
const IS_EXE = !!window.__EXE__;
const api = () => window.pywebview && window.pywebview.api;
function waitApi() {
  return new Promise((res, rej) => { let n = 0; const t = setInterval(() => { if (api() && api().load_state) { clearInterval(t); res(); } else if (++n > 200) { clearInterval(t); rej(new Error('프로그램 내부 연결을 찾지 못했습니다')); } }, 50); });
}
async function download(name, text, type = 'application/json') {
  if (IS_EXE) { try { const r = await api().save_text(name, text); if (r && r.ok) toast('저장했습니다: ' + r.path); else if (r && r.error) toast('저장 실패: ' + r.error); } catch (e) { toast('저장 실패'); } return; }
  const blob = new Blob([text], { type: type + ';charset=utf-8' });
  const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = name;
  document.body.appendChild(a); a.click(); setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500);
}
function copyText(text, okMsg) {
  const done = () => toast(okMsg || '복사했습니다');
  if (navigator.clipboard && window.isSecureContext !== false) navigator.clipboard.writeText(text).then(done, fallback); else fallback();
  function fallback() {
    const ta = document.createElement('textarea'); ta.value = text; document.body.appendChild(ta); ta.select();
    try { document.execCommand('copy'); done(); } catch (e) { toast('복사하지 못했습니다'); } ta.remove();
  }
}

/* ===== 상태 / 저장 ===== */
const KEY = 'ilsang-gyeongbi-v1';
let S = null;                       // 전체 데이터
const UI = { tab: 'daily', util: { cat: 'elec', mode: 'month', month: null, filter: 'all', q: '', showInactive: false, series: { elec: true, water: true, tel: true } }, imp: null };

function deepCopy(o) { return JSON.parse(JSON.stringify(o)); }
function emptyState() { return { version: 1, year: new Date().getFullYear(), asOf: today(), memo: '', lines: [], util: { elec: emptySec('elec'), water: emptySec('water'), tel: emptySec('tel') }, utilBudget: { elec: null, water: null, tel: null } }; }
function emptySec(k) { const d = { elec: ['전기요금', '계약번호', '납부번호(지로·가상계좌)'], water: ['상하수도요금', '수전번호', '납부번호(지로·가상계좌)'], tel: ['통신요금(U-안전관리시스템)', '고객번호', '납부계좌(농협)'] }[k]; return { key: k, title: d[0], no1Label: d[1], no2Label: d[2], items: [], forwards: [], tips: [], budget: null }; }
function loadState() {
  try { const raw = localStorage.getItem(KEY); if (raw) { const d = JSON.parse(raw); if (d && d.lines && d.util) return d; } } catch (e) { }
  return window.__SEED__ ? deepCopy(window.__SEED__) : emptyState();
}
function today() { const d = new Date(); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
let saveTimer = null;
function save() {
  clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    const ok = () => { const n = new Date(); $('#sv').textContent = '자동 저장됨 ' + String(n.getHours()).padStart(2, '0') + ':' + String(n.getMinutes()).padStart(2, '0'); };
    if (IS_EXE) { api().save_state(JSON.stringify(S)).then(r => { if (r && r.ok) ok(); else $('#sv').textContent = '⚠ 저장 실패: ' + ((r && r.error) || ''); }, () => { $('#sv').textContent = '⚠ 저장 실패'; }); return; }
    try { localStorage.setItem(KEY, JSON.stringify(S)); ok(); }
    catch (e) { $('#sv').textContent = '⚠ 브라우저에 저장하지 못했습니다 — [설정]에서 백업 파일을 저장하세요'; }
  }, 150);
}
function uid(p) { return p + Math.random().toString(36).slice(2, 8); }

/* 기준일 경과율 */
function elapsed() {
  const d = new Date((S.asOf || today()) + 'T00:00:00'); if (isNaN(d)) return 0;
  const y = d.getFullYear(), start = new Date(y, 0, 1), end = new Date(y + 1, 0, 1);
  return ((d - start) / 86400000 + 1) / ((end - start) / 86400000) * 100;
}

/* ===== 일상경비 계산 ===== */
const GROUPS = ['사무관리비', '공공운영비', '업무추진비', '여비'];
function calc(l) {
  const sch = sum(l.scheduled || [], x => x.amount);
  const remain = l.budget - l.spent;
  return { sch, remain, remainAfter: remain - sch, rate: pct(l.spent, l.budget), rateAll: pct(l.spent + sch, l.budget) };
}
function groupTotals(lines) {
  const budget = sum(lines, l => l.budget), spent = sum(lines, l => l.spent), sch = sum(lines, l => calc(l).sch);
  return { budget, spent, sch, remain: budget - spent, remainAfter: budget - spent - sch, rate: pct(spent, budget), rateAll: pct(spent + sch, budget) };
}

/* ===== 공공요금 계산 ===== */
const CATS = [['elec', '전기요금'], ['water', '상하수도요금'], ['tel', '통신요금']];
const CATCOLOR = { elec: 'var(--c-elec)', water: 'var(--c-water)', tel: 'var(--c-tel)' };
function itemTotal(it) { return sum(it.amt || []); }
function monthTotal(sec, m, incInactive = true) { return sum(sec.items.filter(i => incInactive || !i.inactive), i => i.amt[m]); }
function catTotal(sec) { let t = 0; for (let m = 0; m < 12; m++) t += monthTotal(sec, m); return t; }
function missingCount(sec, m) { return sec.items.filter(i => !i.inactive && (i.rcv[m] || 0) === 0).length; }
function activeCount(sec) { return sec.items.filter(i => !i.inactive).length; }
function setAmt(it, m, v) {
  it.amt[m] = v;
  if (v && v > 0) it.rcv[m] = 1; else if (v == null && it.rcv[m] === 1) it.rcv[m] = 0;
}
/* 선택할 기본 월: 활성 항목에 미수령이 있는 가장 빠른 달 */
function defaultMonth(sec) {
  for (let m = 0; m < 12; m++) if (missingCount(sec, m) > 0) return m;
  return 11;
}

/* 말풍선 */
document.addEventListener('mouseover', e => {
  const t = e.target.closest('[data-tip]'); const tip = $('#tip');
  if (!t) { tip.style.display = 'none'; return; }
  tip.textContent = t.dataset.tip; tip.style.display = 'block';
});
document.addEventListener('mousemove', e => {
  const tip = $('#tip'); if (tip.style.display === 'none') return;
  const w = tip.offsetWidth, h = tip.offsetHeight;
  tip.style.left = Math.min(e.clientX + 14, innerWidth - w - 8) + 'px';
  tip.style.top = (e.clientY + 18 + h > innerHeight ? e.clientY - h - 12 : e.clientY + 18) + 'px';
});

/* 다음 입력칸으로 포커스 이동 (렌더 후 복원) */
let pendingFocus = null;
function restoreFocus() {
  if (!pendingFocus) return; const el = document.querySelector('[data-nav="' + pendingFocus + '"]'); pendingFocus = null;
  if (el) { el.focus(); if (el.select) el.select(); }
}
function enterNext(e, attr) {
  if (e.key !== 'Enter' || e.isComposing) return;
  const inputs = $$('[data-nav^="' + attr + '"]'); const i = inputs.indexOf(e.target);
  if (i >= 0 && inputs[i + 1]) pendingFocus = inputs[i + 1].dataset.nav;
  e.target.blur();
}
function autoGrow(ta) { ta.style.height = 'auto'; ta.style.height = ta.scrollHeight + 'px'; }
