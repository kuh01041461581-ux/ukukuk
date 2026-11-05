// 익산타이쿤 코어 엔진 (UI 와 무관, 브라우저/Node 공용). 데이터는 JSON 으로 분리.
export function mulberry32(a) { return function () { a |= 0; a = (a + 0x6D2B79F5) | 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const ZONE_KEY = { oldLevel: 'old', newLevel: 'new', baekjeLevel: 'baekje', indLevel: 'ind', farmLevel: 'farm' };

export class Game {
  constructor({ config, events, buildings = {}, plots = [], policies = {}, secretaries = {}, seed = 1, startMonth = 1, player = { gender: 'm', name: '시장' }, secretary = 'admin' }) {
    this.cfg = config; this.ev = events; this.bd = buildings; this.pol = policies; this.secs = secretaries; this.secId = secretaries[secretary] ? secretary : Object.keys(secretaries)[0] || null; this.active = []; this.plots = plots.map(p => ({ ...p })); this.rng = mulberry32(seed); this.player = { gender: player.gender === 'f' ? 'f' : 'm', name: (player.name || '시장').slice(0, 6) }; this.scene = null;
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
  get sec() { return this.secs[this.secId] || {}; }
  /** 정책 채택(예산 일회 비용, 최대 3개 동시) / 철회 */
  adopt(id) {
    const p = this.pol[id]; if (!p) return { ok: false, msg: '없는 정책' };
    if (this.active.includes(id)) return { ok: false, msg: '이미 시행 중입니다.' };
    if (this.active.length >= 3) return { ok: false, msg: '정책은 동시에 3개까지 시행할 수 있습니다.' };
    const cost = Math.round(p.cost * (this.sec.costMul || 1) * 10) / 10;
    if (this.s.budget < cost) return { ok: false, msg: `예산이 부족합니다. (필요 ${cost})` };
    this.s.budget -= cost; this.active.push(id); if (p.once) this._apply(p.once);
    const msg = `정책 시행: ${p.name}`; this.say(msg); return { ok: true, msg };
  }
  repeal(id) { const i = this.active.indexOf(id); if (i < 0) return { ok: false, msg: '시행 중이 아닙니다.' }; this.active.splice(i, 1); this.s.support = clamp(this.s.support - 1, 0, 100); return { ok: true, msg: '정책을 철회했습니다. 지지도 -1' }; }
  /** 비서 조언(상황별 한 줄) */
  advice() {
    const s = this.s;
    if (s.budget < 30) return '예산이 바닥입니다. 도청·국회 방문으로 정부지원을 확보하시지요.';
    if (s.fatigue > 70) return '많이 지치셨습니다. 이번 달은 쉬셔야 합니다.';
    if (s.auditRisk > 40) return '감사 위험이 높습니다. 청렴 서약을 고려하십시오.';
    if (s.support < 45) return '지지도가 낮습니다. 시민 간담회나 공원·학교 건설을 권합니다.';
    if (this.monthsLeftInTerm <= 6) return '선거가 얼마 남지 않았습니다. 지지도 관리에 집중하십시오.';
    if (this.plots.some(p => p.landmark && !p.building)) return '랜드마크 부지가 비어 있습니다. 복원·개발하면 관광과 지지도에 큰 도움이 됩니다.';
    return '순조롭습니다. 빈 부지에 건물을 지어 세수를 늘려 보십시오.';
  }
  zone(id) { return this.zones.find(z => z.id === id); }
  avgLevel() { return this.zones.reduce((a, z) => a + z.level, 0) / this.zones.length; }
  say(m) { this.log.push({ m: this.s.monthIndex, text: m }); }

  // ---------- 플레이어 커맨드 ----------
  act(cmd, arg) {
    if (this.phase !== 'player') return { ok: false, msg: '지금은 행동할 수 없습니다.' };
    const s = this.s, C = this.cfg;
    const need = ({ rest: 1, invest: 1, build: 1, meet: 1, visit_province: 1, visit_national: 2 })[cmd];
    if (need == null) return { ok: false, msg: '알 수 없는 커맨드' };
    if (s.hospital) return { ok: false, msg: '과로로 입원 중입니다. 이번 달은 행동할 수 없습니다.' };
    if (s.ap < need) return { ok: false, msg: '행동력이 부족합니다.' };
    let msg = ''; const fm = this.sec.fatigueMul || 1, fg = n => Math.round(n * fm);
    switch (cmd) {
      case 'rest': s.fatigue = clamp(s.fatigue - C.fatigue.restRecover - (this.sec.restBonus || 0), 0, 100); msg = '휴식을 취했습니다. 피로가 풀립니다.'; break;
      case 'meet': s.fatigue = clamp(s.fatigue + fg(5), 0, 100); s.support = clamp(s.support + 3, 0, 100); msg = '시민과 간담회를 열었습니다. 지지도 +3'; break;
      case 'invest': {
        const z = this.zone(arg); if (!z) return { ok: false, msg: '구역을 선택하세요.' };
        if (s.budget < 10) return { ok: false, msg: '예산이 부족합니다. (필요 10)' };
        s.budget -= 10; s.fatigue = clamp(s.fatigue + 6, 0, 100); z.level = clamp(z.level + 5, 0, 100); s.support = clamp(s.support + 1, 0, 100);
        msg = `${z.name}에 투자했습니다. 발전도 ${z.level}`; break; }
      case 'build': {                                   // arg: { plot, type }
        const p = this.plots.find(q => q.id === arg.plot), b = this.bd[arg.type];
        if (!p || !b) return { ok: false, msg: '부지/건물을 선택하세요.' };
        if (p.building && b.upgradeFrom !== p.building) return { ok: false, msg: '이미 건물이 있는 부지입니다.' };
        if (!p.building && b.upgradeFrom) return { ok: false, msg: '기존 시설이 있어야 개축할 수 있습니다.' };
        if (b.landmark ? b.landmark !== p.landmark : !!p.landmark) return { ok: false, msg: p.landmark ? `${p.name}에는 복원·개발 사업만 할 수 있습니다.` : `${b.name}은(는) 해당 랜드마크 부지에만 가능합니다.` };
        if (b.zones && !b.zones.includes(p.zone)) return { ok: false, msg: `${b.name}은(는) 이 권역에 지을 수 없습니다.` };
        const cost = Math.round(b.cost * (this.sec.costMul || 1) * 10) / 10;
        if (s.budget < cost) return { ok: false, msg: `예산이 부족합니다. (필요 ${cost})` };
        s.budget -= cost; s.fatigue = clamp(s.fatigue + fg(6), 0, 100); p.building = arg.type; if (b.zoning) p.lv = 1; else delete p.lv;
        s.population = Math.round((s.population + b.pop) * 100) / 100; s.support = clamp(s.support + b.support, 0, 100);
        const z = this.zone(p.zone); z.level = clamp(z.level + b.level, 0, 100);
        msg = `${b.name} 건설 (${z.name}). 발전도 ${z.level}`; break; }
      case 'visit_province': {
        const p = 0.45 + s.politics / 250 + s.support / 500 + (this.sec.grantBonus || 0); s.fatigue = clamp(s.fatigue + fg(8), 0, 100);
        if (this.rng() < p) { const g = Math.round(8 + this.rng() * 8); s.budget += g; s.grants += g; s.politics = clamp(s.politics + 2, 0, 100); msg = `도청에서 정부지원 ${g}억을 확보했습니다.`; }
        else { s.politics = clamp(s.politics + 1, 0, 100); msg = '도청 방문, 이번에는 성과가 없었습니다.'; }
        break; }
      case 'visit_national': {
        const p = 0.30 + s.politics / 200 + s.support / 400 + (this.sec.grantBonus || 0); s.fatigue = clamp(s.fatigue + fg(15), 0, 100);
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
    const tax = Math.round(s.population * C.income.taxPerManPop * 10) / 10 * (this.sec.taxMul || 1);
    const gov = C.income.baseSupportGov, upkeep = Math.round(this.zones.reduce((a, z) => a + z.level, 0) * C.income.upkeepPerZoneLevel * 10) / 10;
    const tx = p => { const b = this.bd[p.building]; return b.zoning ? b.taxLv[(p.lv || 1) - 1] : b.tax; }, up = p => { const b = this.bd[p.building]; return b.zoning ? b.upLv[(p.lv || 1) - 1] : b.upkeep; };
    const bTax = this.plots.reduce((a, p) => a + (p.building ? tx(p) : 0), 0), bUp = this.plots.reduce((a, p) => a + (p.building ? up(p) : 0), 0);
    this._zoningTick();
    let pTax = 0, pUp = 0;
    for (const id of this.active) { const p = this.pol[id]; pUp += p.upkeep || 0; for (const [k, v] of Object.entries(p.monthly || {})) { if (k === 'tax') pTax += v; else this._apply({ [k]: v }); } }
    s.budget = Math.round((s.budget + tax + gov + bTax + pTax - upkeep - bUp - pUp) * 10) / 10;
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
  /** 구역 지정형 단지: 수요(지지도·예산)·주변(산업 오염/공원)에 따라 월마다 성장, 산업단지는 인근 주거 지지도를 깎는다 */
  _zoningTick() {
    const Z = this.cfg.zoning; if (!Z) return; const s = this.s, dist = (a, b) => Math.abs(a.x - b.x) + Math.abs(a.y - b.y);
    const zp = this.plots.filter(p => p.building && this.bd[p.building].zoning);
    let pollution = 0, parkBoost = 0;
    for (const p of zp) {
      const kind = this.bd[p.building].zoning; let chance = Z.baseGrow + (s.support > 55 ? Z.supportBonus : 0) + (s.budget > 50 ? Z.budgetBonus : 0) - (s.support < 35 ? 0.12 : 0);
      if (kind === 'res') { const ind = zp.filter(q => this.bd[q.building].zoning === 'ind' && dist(p, q) <= Z.pollutionRange); if (ind.length) { chance -= Z.nearIndPenalty; pollution += ind.reduce((a, q) => a + (q.lv || 1), 0) * Z.pollutionPerLv; }
        if (this.plots.some(q => q.building === 'park' && dist(p, q) <= Z.parkBonusRange)) { chance += Z.parkBonus; parkBoost += 0.08; } }
      if ((p.lv || 1) < Z.maxLevel && this.rng() < chance) { p.lv = (p.lv || 1) + 1; s.population = Math.round((s.population + this.bd[p.building].pop) * 100) / 100; this.zone(p.zone).level = clamp(this.zone(p.zone).level + 1.5, 0, 100); this.say(`${this.zone(p.zone).name} ${this.bd[p.building].name.replace(' 지정', '')}가 Lv${p.lv}로 성장했습니다.`); }
    }
    s.support = clamp(s.support - Math.min(pollution, 4) + Math.min(parkBoost, 1), 0, 100);
  }
  _snap() { const s = this.s; return { m: s.monthIndex, budget: s.budget, pop: s.population, support: Math.round(s.support), integrity: Math.round(s.integrity) }; }
  _checkOverwork() { /* 입원은 월말 처리 */ }

  // ---------- 이벤트 ----------
  _enqueueMonthEvents() {
    const cm = this.calMonth;
    for (const e of this.ev.calendar) { if (e.needsVerification || e.month == null || e.month !== cm || !e.choices.length) continue; this.queue.push(e); }
    if (this.rng() < 0.6) { const pool = this.ev.random.filter(x => !x.needs || this.plots.some(p => p.building === x.needs)); const tot = pool.reduce((a, e) => a + e.weight, 0); let r = this.rng() * tot; for (const e of pool) { if ((r -= e.weight) <= 0) { this.queue.push(e); break; } } }
    // 뒷돈 제안: 최근 제안과 간격, 예산이 어려울수록·정치력이 높을수록 확률↑
    const s = this.s, since = s.monthIndex - s.lastBribeMonth;
    const p = 0.20 + (s.budget < 60 ? 0.15 : 0) + (s.politics > 50 ? 0.05 : 0);
    if (since >= 3 && this.rng() < p) { const b = this.ev.bribe; this.queue.push({ ...b[Math.floor(this.rng() * b.length)], _bribe: true }); s.lastBribeMonth = s.monthIndex; }
  }
  _next() {
    if (this.queue.length) { this.decision = this.queue.shift(); if (this.decision.advice && this.decision.advice[this.secId]) this.decision = { ...this.decision, secLine: this.decision.advice[this.secId] }; if (this.decision._bribe && this.sec.warnBribe && !this.decision._warned) this.decision = { ...this.decision, _warned: true, text: this.decision.text + ' ⚠ 정무 비서: "받으시면 감사 위험이 크게 오릅니다."' }; this.phase = 'decision'; this.scene = this.decision.scene || null; } else { this.decision = null; this.phase = 'player'; this.scene = null; }
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
      this.scene = 'election_win'; this.phase = 'election'; this.decision = { title: '선거 결과', text: `득표율 ${this.lastShare}% — 당선! (${s.term}임기 종료, 연임 ${s.term}회)`,
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
    this.ending = { type, name: this.player.name, gender: this.player.gender, grade, score: Math.round(score), text, years: this.totalYear, term: s.term, gifts: s.giftsTaken, population: s.population, support: Math.round(s.support) };
    this.phase = 'ending'; this.decision = null; this.scene = { impeach: 'ending_impeach', defeat: 'ending_defeat', retire: 'ending_retire', full: 'ending_full' }[type];
  }
}
