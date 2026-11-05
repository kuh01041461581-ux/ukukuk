// 익산타이쿤 코어 엔진 (UI 와 무관, 브라우저/Node 공용). 데이터는 JSON 으로 분리.
export function mulberry32(a) { return function () { a |= 0; a = (a + 0x6D2B79F5) | 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const ZONE_KEY = { oldLevel: 'old', newLevel: 'new', baekjeLevel: 'baekje', indLevel: 'ind', farmLevel: 'farm' };

export class Game {
  constructor({ config, events, seed = 1, startMonth = 1 }) {
    this.cfg = config; this.ev = events; this.rng = mulberry32(seed);
    const s = config.start;
    this.s = { budget: s.budget, population: s.population, support: s.support, fatigue: s.fatigue, politics: s.politics,
               integrity: s.integrity, auditRisk: s.auditRisk, audits: s.audits, monthIndex: 0, startMonth, term: 1, ap: config.actionsPerMonth,
               hospital: false, giftsTaken: 0, lastBribeMonth: -99, grants: 0 };
    this.zones = config.zones.map(z => ({ ...z }));
    this.phase = 'player'; this.decision = null; this.ending = null; this.queue = []; this.log = [];
    this.history = [];            // 월별 요약(그래프/엔딩용)
    this._seen = {};
    this._enqueueMonthEvents();
    this._next();
  }
  // ---------- 달력 ----------
  get calMonth() { return (this.s.startMonth - 1 + this.s.monthIndex) % 12 + 1; }
  get termYear() { return Math.floor((this.s.monthIndex % this.cfg.term.months) / 12) + 1; }
  get totalYear() { return Math.floor(this.s.monthIndex / 12) + 1; }
  get monthsLeftInTerm() { return this.cfg.term.months - (this.s.monthIndex % this.cfg.term.months); }
  zone(id) { return this.zones.find(z => z.id === id); }
  avgLevel() { return this.zones.reduce((a, z) => a + z.level, 0) / this.zones.length; }
  say(m) { this.log.push({ m: this.s.monthIndex, text: m }); }

  // ---------- 플레이어 커맨드 ----------
  act(cmd, arg) {
    if (this.phase !== 'player') return { ok: false, msg: '지금은 행동할 수 없습니다.' };
    const s = this.s, C = this.cfg;
    const need = ({ rest: 1, invest: 1, meet: 1, visit_province: 1, visit_national: 2 })[cmd];
    if (need == null) return { ok: false, msg: '알 수 없는 커맨드' };
    if (s.hospital) return { ok: false, msg: '과로로 입원 중입니다. 이번 달은 행동할 수 없습니다.' };
    if (s.ap < need) return { ok: false, msg: '행동력이 부족합니다.' };
    let msg = '';
    switch (cmd) {
      case 'rest': s.fatigue = clamp(s.fatigue - C.fatigue.restRecover, 0, 100); msg = '휴식을 취했습니다. 피로가 풀립니다.'; break;
      case 'meet': s.fatigue = clamp(s.fatigue + 5, 0, 100); s.support = clamp(s.support + 3, 0, 100); msg = '시민과 간담회를 열었습니다. 지지도 +3'; break;
      case 'invest': {
        const z = this.zone(arg); if (!z) return { ok: false, msg: '구역을 선택하세요.' };
        if (s.budget < 10) return { ok: false, msg: '예산이 부족합니다. (필요 10)' };
        s.budget -= 10; s.fatigue = clamp(s.fatigue + 6, 0, 100); z.level = clamp(z.level + 5, 0, 100); s.support = clamp(s.support + 1, 0, 100);
        msg = `${z.name}에 투자했습니다. 발전도 ${z.level}`; break; }
      case 'visit_province': {
        const p = 0.45 + s.politics / 250 + s.support / 500; s.fatigue = clamp(s.fatigue + 8, 0, 100);
        if (this.rng() < p) { const g = Math.round(8 + this.rng() * 8); s.budget += g; s.grants += g; s.politics = clamp(s.politics + 2, 0, 100); msg = `도청에서 정부지원 ${g}억을 확보했습니다.`; }
        else { s.politics = clamp(s.politics + 1, 0, 100); msg = '도청 방문, 이번에는 성과가 없었습니다.'; }
        break; }
      case 'visit_national': {
        const p = 0.30 + s.politics / 200 + s.support / 400; s.fatigue = clamp(s.fatigue + 15, 0, 100);
        if (this.rng() < p) { const g = Math.round(20 + this.rng() * 20); s.budget += g; s.grants += g; s.politics = clamp(s.politics + 3, 0, 100); msg = `국회·중앙부처에서 정부지원 ${g}억을 확보했습니다!`; }
        else { s.politics = clamp(s.politics + 1, 0, 100); msg = '국회 방문, 예산 확보에 실패했습니다.'; }
        break; }
    }
    s.ap -= need; this.say(msg); this._checkOverwork();
    return { ok: true, msg };
  }

  // ---------- 월 진행 ----------
  endMonth() {
    if (this.phase !== 'player') return;
    const s = this.s, C = this.cfg;
    // 1. 수입·지출
    const tax = Math.round(s.population * C.income.taxPerManPop * 10) / 10;
    const gov = C.income.baseSupportGov, upkeep = Math.round(this.zones.reduce((a, z) => a + z.level, 0) * C.income.upkeepPerZoneLevel * 10) / 10;
    s.budget = Math.round((s.budget + tax + gov - upkeep) * 10) / 10;
    // 2. 인구·지지도 변화
    s.population = Math.round((s.population + (this.avgLevel() - 30) / 400) * 100) / 100;
    s.support = clamp(s.support + (25 + this.avgLevel() * 0.5 - s.support) * 0.15, 0, 100);   // 평균 발전도가 목표 지지도를 정함
    if (s.budget < 0) { s.support = clamp(s.support - 3, 0, 100); s.budget = Math.max(s.budget, -50); this.say('예산 적자! 지지도가 하락합니다.'); }
    // 3. 피로·입원
    s.fatigue = clamp(s.fatigue - C.fatigue.monthlyRecover, 0, 100);
    s.hospital = false;
    if (s.fatigue >= C.fatigue.overwork) { s.hospital = true; s.fatigue = 50; s.support = clamp(s.support - 2, 0, 100); this.say('과로로 쓰러져 입원했습니다.'); }
    // 4. 감사
    s.auditRisk = clamp(s.auditRisk - C.audit.riskDecayPerMonth, 0, 100);
    if (s.auditRisk >= C.audit.riskThreshold) {
      s.audits++; s.support = clamp(s.support - 6, 0, 100); s.integrity = clamp(s.integrity - 5, 0, 100); s.budget -= 10; s.auditRisk = 30;
      this.say(`감사원 감사 적발! (누적 ${s.audits}회)`);
      if (s.audits >= C.audit.impeachAudits) { this.history.push(this._snap()); return this._end('impeach'); }
    } else if (s.auditRisk === 0) s.integrity = clamp(s.integrity + C.audit.integrityGainOnClean, 0, 100);
    this.history.push(this._snap());
    // 5. 날짜 진행
    s.monthIndex++; s.ap = C.actionsPerMonth;
    if (s.monthIndex % C.term.months === 0) { this.phase = 'election'; return this._election(); }
    this._enqueueMonthEvents(); this._next();
  }
  _snap() { const s = this.s; return { m: s.monthIndex, budget: s.budget, pop: s.population, support: Math.round(s.support), integrity: Math.round(s.integrity) }; }
  _checkOverwork() { /* 입원은 월말 처리 */ }

  // ---------- 이벤트 ----------
  _enqueueMonthEvents() {
    const cm = this.calMonth;
    for (const e of this.ev.calendar) { if (e.needsVerification || e.month == null || e.month !== cm || !e.choices.length) continue; this.queue.push(e); }
    if (this.rng() < 0.6) { const pool = this.ev.random; const tot = pool.reduce((a, e) => a + e.weight, 0); let r = this.rng() * tot; for (const e of pool) { if ((r -= e.weight) <= 0) { this.queue.push(e); break; } } }
    // 뒷돈 제안: 최근 제안과 간격, 예산이 어려울수록·정치력이 높을수록 확률↑
    const s = this.s, since = s.monthIndex - s.lastBribeMonth;
    const p = 0.20 + (s.budget < 60 ? 0.15 : 0) + (s.politics > 50 ? 0.05 : 0);
    if (since >= 3 && this.rng() < p) { const b = this.ev.bribe; this.queue.push({ ...b[Math.floor(this.rng() * b.length)], _bribe: true }); s.lastBribeMonth = s.monthIndex; }
  }
  _next() {
    if (this.queue.length) { this.decision = this.queue.shift(); this.phase = 'decision'; } else { this.decision = null; this.phase = 'player'; }
  }
  choose(i) {
    if (this.phase !== 'decision') return;
    const ch = this.decision.choices[i]; if (!ch) return;
    this._apply(ch.fx || {});
    if (ch.corrupt) { this.s.auditRisk = clamp(this.s.auditRisk + ch.corrupt, 0, 100); this.s.integrity = clamp(this.s.integrity - ch.corrupt / 2, 0, 100); this.s.giftsTaken++; }
    if (ch.chance && this.rng() < ch.chance.p) { this._apply(ch.chance.fx); this.say(ch.chance.note || '불운이 겹쳤습니다.'); }
    this.say(`[${this.decision.title}] ${ch.label}`);
    this._next();
  }
  _apply(fx) {
    const s = this.s;
    for (const [k, v] of Object.entries(fx)) {
      if (ZONE_KEY[k]) { const z = this.zone(ZONE_KEY[k]); z.level = clamp(z.level + v, 0, 100); }
      else if (k === 'budget') s.budget += v;
      else if (k === 'population') s.population = Math.round((s.population + v) * 100) / 100;
      else s[k] = clamp(s[k] + v, 0, 100);
    }
  }

  // ---------- 선거·연임·엔딩 ----------
  _election() {
    const s = this.s, w = this.cfg.election.weights, n = this.cfg.election.noise;
    const share = w.support * s.support + w.politics * s.politics + w.integrity * s.integrity + (this.rng() - 0.5) * 2 * n;
    this.lastShare = Math.round(share * 10) / 10;
    if (share >= this.cfg.election.winThreshold) {
      if (s.term >= this.cfg.term.maxTerms) { this.say(`선거 승리 (득표율 ${this.lastShare}%) — 마지막 임기를 마무리했습니다.`); return this._end('full'); }
      this.phase = 'election'; this.decision = { title: '선거 결과', text: `득표율 ${this.lastShare}% — 당선! (${s.term}임기 종료, 연임 ${s.term}회)`,
        choices: [{ label: '엔딩 보기', _end: true }, { label: '계속 플레이 (다음 임기)', _end: false }] };
    } else this._end('defeat');
  }
  electionChoice(i) {
    if (this.phase !== 'election' || !this.decision) return;
    const c = this.decision.choices[i]; if (!c) return;
    if (c._end) return this._end('retire');
    this.s.term++; this.s.audits = Math.max(0, this.s.audits - 1); this.s.auditRisk = Math.floor(this.s.auditRisk / 2);
    this.say(`${this.s.term}번째 임기를 시작합니다.`);
    this.decision = null; this.phase = 'player'; this._enqueueMonthEvents(); this._next();
  }
  _end(type) {
    const s = this.s, score = s.population * 2 + this.avgLevel() + s.support + s.integrity / 2 + Math.max(0, s.budget) / 10 + s.term * 5;
    const grade = type === 'impeach' ? 'F' : score > 260 ? 'S' : score > 210 ? 'A' : score > 160 ? 'B' : score > 110 ? 'C' : 'D';
    const text = { impeach: '감사 누적으로 탄핵되었습니다.', defeat: '재선에 실패했습니다. 시민의 선택은 냉정했습니다.', retire: '시민의 박수 속에 명예롭게 퇴임합니다.', full: '12년의 시정을 모두 마쳤습니다. 익산의 역사가 되었습니다.' }[type];
    this.ending = { type, grade, score: Math.round(score), text, years: this.totalYear, term: s.term, gifts: s.giftsTaken, population: s.population, support: Math.round(s.support) };
    this.phase = 'ending'; this.decision = null;
  }
}
