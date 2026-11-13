// 국가무형유산 출석체크 — 플랫폼 공통 로직 (Node 로컬 서버 / Cloudflare Worker 공용)
// Web 표준 Request/Response만 사용합니다.

const DEFAULT_SETTINGS = { openBefore: 30, lateAfter: 10, closeAfter: 30, useCode: false }; // useCode: 현장 코드도 함께 입력받기(기본 꺼짐)

export function emptyDb() {
  return { groups: [], members: [], lectures: [], records: {}, sessions: {}, bindings: {}, deviceRequests: [],
    settings: { ...DEFAULT_SETTINGS }, venue: { name: '', lat: null, lng: null, radius: 150 } };
}

function uid(p) { return p + Math.random().toString(36).slice(2, 8); }
function newQr() {
  const a = new Uint8Array(9); crypto.getRandomValues(a);
  return btoa(String.fromCharCode(...a)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

// 로컬 개발·시험용 샘플 데이터
export function seedDb(db) {
  ['A단체', 'B단체', 'C단체', 'D단체', 'E단체', 'F단체'].forEach((n, i) => {
    const g = { id: 'g' + (i + 1), name: n, pin: String(1111 * (i + 1)) };
    db.groups.push(g);
    for (let k = 1; k <= 3; k++) db.members.push({ id: uid('m'), groupId: g.id, name: `${n} 구성원${k}` });
  });
  let n = 1;
  for (const d of [1, 2, 3, 4, 5]) for (const [s, e] of [['09:00', '10:30'], ['10:40', '12:10'], ['13:30', '15:00'], ['15:10', '16:40']])
    db.lectures.push({ id: 'l' + n, name: `강의 ${n++}`, weekday: d, start: s, end: e });
}

// 저장된 데이터를 현재 형식으로 보정. 바뀐 게 있으면 true
export function normalizeDb(db) {
  let changed = false;
  const base = emptyDb();
  for (const k of Object.keys(base)) if (db[k] === undefined) { db[k] = base[k]; changed = true; }
  db.settings = { ...DEFAULT_SETTINGS, ...db.settings };
  for (const m of db.members) if (!m.qr) { m.qr = newQr(); changed = true; }
  return changed;
}

/**
 * @param {{db: object, persist: (db: object, dates: string[]) => void, adminPassword?: string}} opts
 * @returns {{ handle: (req: Request) => Promise<Response|null> }}  /api/* 가 아니면 null
 */
export function createCore({ db, persist, adminPassword }) {
  const dirty = new Set();
  function save() { const d = [...dirty]; dirty.clear(); persist(db, d); }
  if (normalizeDb(db)) save();

  const json = (code, obj) => new Response(JSON.stringify(obj), { status: code, headers: { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' } });
  async function body(req) {
    const t = await req.text();
    if (t.length > 2e6) throw new Error('요청이 너무 큽니다');
    return t ? JSON.parse(t) : {};
  }
  const same = (a, b) => { // 길이가 달라도 일정한 시간으로 비교
    a = String(a ?? ''); b = String(b ?? ''); let r = a.length ^ b.length;
    for (let i = 0; i < Math.max(a.length, b.length); i++) r |= (a.charCodeAt(i) || 0) ^ (b.charCodeAt(i) || 0);
    return r === 0;
  };
  const isAdmin = (req) => !!adminPassword && same(req.headers.get('x-admin'), adminPassword);
  const ip = (req) => req.headers.get('cf-connecting-ip') || 'local';
  // 실패 횟수 제한: 10분 안에 10번 틀리면 10분간 차단 (메모리 보관)
  const fails = new Map();
  const blocked = (k) => { const f = fails.get(k); if (!f) return false; if (Date.now() - f.t > 600e3) { fails.delete(k); return false; } return f.n >= 10; };
  const strike = (k) => { const f = fails.get(k); if (!f || Date.now() - f.t > 600e3) fails.set(k, { n: 1, t: Date.now() }); else f.n++; if (fails.size > 5000) fails.clear(); };
  const dateOk = (d) => /^\d{4}-\d{2}-\d{2}$/.test(d || '');
  const wd = (d) => new Date(d + 'T00:00:00Z').getUTCDay();
  const STATUS = ['P', 'L', 'A']; // 출석, 지각, 결석
  const NO_VENUE = '교육 장소가 아직 설정되지 않아 출석할 수 없습니다. 담당자에게 문의하세요.';

function dist(a, b, c, d) { // meters
  const R = 6371000, r = Math.PI / 180, dLat = (c - a) * r, dLng = (d - b) * r;
  const x = Math.sin(dLat / 2) ** 2 + Math.cos(a * r) * Math.cos(c * r) * Math.sin(dLng / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(x));
}
const lecturesOn = (date) => db.lectures.filter((l) => l.weekday === wd(date)
  && (!l.startDate || date >= l.startDate) && (!l.endDate || date <= l.endDate) && !(l.off || []).includes(date));
const kstNow = () => new Date(Date.now() + 9 * 3600e3);
function sessionOf(token) { return db.sessions[token] && { token, ...db.sessions[token] }; }


// ---- 출석 인정 시간 ----
function windowOf(date, l) {
  const S = db.settings, k = kstNow(), today = k.toISOString().slice(0, 10);
  if (date < today) return { err: '지난 교육입니다' };
  if (date > today) return { err: '아직 출석 시간이 아닙니다' };
  const [h, mi] = l.start.split(':').map(Number);
  const diff = k.getUTCHours() * 60 + k.getUTCMinutes() - (h * 60 + mi);
  if (diff < -S.openBefore) return { err: `출석은 시작 ${S.openBefore}분 전(${l.start} 기준)부터 가능합니다` };
  if (diff > S.closeAfter) return { err: `출석 시간이 지났습니다 (시작 ${S.closeAfter}분 후 마감)` };
  return { status: diff > S.lateAfter ? 'L' : 'P', diff };
}
function currentLecture() { // 지금 출석 가능한 강의(시작 시각이 가장 가까운 것)
  const date = kstNow().toISOString().slice(0, 10);
  let best = null;
  for (const l of lecturesOn(date)) {
    const w = windowOf(date, l); if (w.err) continue;
    if (!best || Math.abs(w.diff) < best.d) best = { l, d: Math.abs(w.diff), w };
  }
  return best && { date, lecture: best.l, window: best.w };
}
function mark(date, l, mem, status, meta) {
  const rec = ((db.records[date] ||= {})[l.id] ||= {})[mem.groupId] ||= { marks: {}, meta: {} };
  rec.meta ||= {};
  if (rec.marks[mem.id] && rec.meta[mem.id]) return { already: true, status: rec.marks[mem.id] };
  rec.marks[mem.id] = status;
  rec.meta[mem.id] = { at: new Date().toISOString(), ...meta };
  rec.at = rec.meta[mem.id].at;
  dirty.add(date);
  save();
  return { status };
}
const memberOfDevice = (dev) => { // 이 휴대폰에 등록된 구성원 (한 휴대폰 = 한 사람)
  if (!dev) return null; const e = Object.entries(db.bindings || {}).find(([, v]) => v === dev);
  return e ? db.members.find((x) => x.id === e[0]) || null : null; };
const memberByQr = (q) => { const t = String(q || '').trim().split('/').pop().split('?')[0]; return db.members.find((m) => m.qr === t); };

// ---- 출석률 통계 ----
function stats(from, to) {
  const held = []; // 실시된 교육 = 링크를 만들었거나 출석 기록이 있는 (날짜, 강의)
  const keys = new Set();
  for (const v of Object.values(db.sessions)) keys.add(v.date + '|' + v.lectureId);
  for (const [d, byL] of Object.entries(db.records)) for (const lid of Object.keys(byL)) keys.add(d + '|' + lid);
  for (const k of keys) { const [d, lid] = k.split('|'); if (d >= from && d <= to && db.lectures.some((x) => x.id === lid)) held.push({ d, lid }); }
  const mem = db.members.map((m) => ({ id: m.id, name: m.name, groupId: m.groupId, P: 0, L: 0, A: 0 }));
  const byId = Object.fromEntries(mem.map((m) => [m.id, m]));
  const lec = Object.fromEntries(db.lectures.map((l) => [l.id, { id: l.id, name: l.name, weekday: l.weekday, start: l.start, held: 0, P: 0, L: 0, A: 0 }]));
  for (const { d, lid } of held) {
    lec[lid].held++;
    const rec = (db.records[d] || {})[lid] || {};
    for (const m of mem) {
      const s = ((rec[m.groupId] || {}).marks || {})[m.id];
      const k = s === 'P' ? 'P' : s === 'L' ? 'L' : 'A';
      m[k]++; lec[lid][k]++;
    }
  }
  const rate = (o, n) => (n ? Math.round(((o.P + o.L) / n) * 1000) / 10 : null);
  const groups = db.groups.map((g) => {
    const ms = mem.filter((m) => m.groupId === g.id), t = { P: 0, L: 0, A: 0 };
    ms.forEach((m) => { t.P += m.P; t.L += m.L; t.A += m.A; });
    return { id: g.id, name: g.name, members: ms.length, ...t, rate: rate(t, t.P + t.L + t.A) };
  });
  return {
    from, to, heldCount: held.length, groups,
    members: mem.map((m) => ({ ...m, held: held.length, rate: rate(m, held.length) })),
    lectures: Object.values(lec).filter((x) => x.held).map((x) => ({ ...x, rate: rate(x, x.held * db.members.length) })),
  };
}

const pinBlocked = (req, groupId) => blocked('pin:' + groupId + ':' + ip(req));
function groupAuth(req, groupId) {
  const g = db.groups.find((x) => x.id === groupId); if (!g) return null;
  const k = 'pin:' + g.id + ':' + ip(req);
  if (blocked(k)) return null;
  if (same(req.headers.get('x-pin'), g.pin)) return g;
  strike(k); return null;
}

function overview(date) {
  const lectures = lecturesOn(date)
    .sort((a, b) => a.start.localeCompare(b.start));
  const day = db.records[date] || {};
  return lectures.map((l) => {
    const sess = Object.entries(db.sessions).find(([, v]) => v.date === date && v.lectureId === l.id);
    return {
    ...l,
    session: sess ? { token: sess[0], code: sess[1].code, open: sess[1].open } : null,
    groups: db.groups.map((g) => {
      const ms = db.members.filter((m) => m.groupId === g.id);
      const rec = (day[l.id] || {})[g.id];
      const marks = rec ? rec.marks : {};
      const c = { P: 0, L: 0, A: 0, U: 0 };
      const absent = [];
      ms.forEach((m) => {
        const s = marks[m.id];
        if (s) { c[s]++; if (s !== 'P') absent.push({ name: m.name, status: s }); } else c.U++;
      });
      return { groupId: g.id, name: g.name, total: ms.length, checked: !!rec, ...c, issues: absent };
    }),
  }; });
}

function csv(from, to) {
  const rows = [['날짜', '요일', '강의', '시작', '종료', '단체', '이름', '상태']];
  const label = { P: '출석', L: '지각', A: '결석' };
  for (const date of Object.keys(db.records).sort()) {
    if (date < from || date > to) continue;
    for (const [lid, byG] of Object.entries(db.records[date])) {
      const l = db.lectures.find((x) => x.id === lid); if (!l) continue;
      for (const [gid, rec] of Object.entries(byG)) {
        const g = db.groups.find((x) => x.id === gid); if (!g) continue;
        for (const [mid, s] of Object.entries(rec.marks)) {
          const m = db.members.find((x) => x.id === mid); if (!m) continue;
          rows.push([date, '일월화수목금토'[wd(date)], l.name, l.start, l.end, g.name, m.name, label[s]]);
        }
      }
    }
  }
  const q = (v) => `"${String(v).replace(/"/g, '""')}"`;
  return '﻿' + rows.map((r) => r.map(q).join(',')).join('\r\n');
}

async function api(req, url) {
  const p = url.pathname, q = url.searchParams, m = req.method;

  if (p === '/api/groups' && m === 'GET') return json(200, db.groups.map(({ id, name }) => ({ id, name })));


  let mm;
  if ((mm = p.match(/^\/api\/s\/([\w-]+)$/)) && m === 'GET') {
    const se = sessionOf(mm[1]); if (!se) return json(404, { error: '유효하지 않은 링크입니다' });
    const l = db.lectures.find((x) => x.id === se.lectureId);
    const me = memberOfDevice(String(q.get('deviceId') || '').slice(0, 64));
    const meGroup = me && db.groups.find((g) => g.id === me.groupId);
    const done = me && !!((((db.records[se.date] || {})[se.lectureId] || {})[me.groupId] || {}).marks || {})[me.id];
    return json(200, {
      open: se.open, useCode: !!db.settings.useCode, date: se.date, lecture: l && { name: l.name, start: l.start, end: l.end },
      venue: db.venue.lat == null ? null : { name: db.venue.name },
      me: me ? { id: me.id, name: me.name, group: meGroup && meGroup.name, done: !!done } : null,
      // 등록 전 휴대폰에만 단체·이름 목록을 보여줍니다
      groups: me ? [] : db.groups.map((g) => ({ id: g.id, name: g.name,
        members: db.members.filter((x) => x.groupId === g.id).map((x) => ({ id: x.id, name: x.name })) })),
    });
  }
  if ((mm = p.match(/^\/api\/s\/([\w-]+)\/checkin$/)) && m === 'POST') {
    const se = sessionOf(mm[1]); if (!se) return json(404, { error: '유효하지 않은 링크입니다' });
    if (!se.open) return json(403, { error: '출석이 마감되었습니다' });
    const lec0 = db.lectures.find((x) => x.id === se.lectureId);
    const w = windowOf(se.date, lec0); if (w.err) return json(403, { error: w.err });
    const b = await body(req);
    const dev = String(b.deviceId || '').slice(0, 64);
    if (dev.length < 8) return json(400, { error: '이 브라우저에서 기기 정보를 저장할 수 없습니다. 사생활 보호(시크릿) 모드를 끄고 다시 열어 주세요.' });
    db.bindings ||= {};
    // ① 본인 확인: 이미 등록된 휴대폰이면 그 사람으로만 출석, 처음이면 단체·이름·전화번호 뒤 4자리로 등록
    let mem = memberOfDevice(dev), registering = false;
    if (mem) {
      if (b.memberId && b.memberId !== mem.id) return json(403, { error: `이 휴대폰은 ${mem.name}님으로 등록되어 있어 다른 분 이름으로는 출석할 수 없습니다.` });
    } else {
      mem = db.members.find((x) => x.id === b.memberId); if (!mem) return json(404, { error: '단체와 본인 이름을 선택해 주세요' });
      if (!mem.phone4) return json(403, { error: '명부에 전화번호가 없어 등록할 수 없습니다. 담당자에게 문의하세요.' });
      const pk = 'ph:' + mem.id + ':' + ip(req);
      if (blocked(pk)) return json(429, { error: '시도가 너무 많습니다. 10분 뒤에 다시 시도하세요' });
      if (!same(String(b.phone4 || ''), mem.phone4)) { strike(pk); return json(403, { error: '전화번호 뒤 4자리가 맞지 않습니다', needPhone: true }); }
      if (db.bindings[mem.id] && db.bindings[mem.id] !== dev)
        return json(403, { error: `${mem.name}님은 이미 다른 휴대폰에 등록되어 있습니다. 휴대폰을 바꾸셨다면 아래에서 기기 변경을 요청하세요.`, canRequest: true, memberId: mem.id });
      registering = true;
    }
    // ② 현장 코드(선택) · 위치
    if (db.settings.useCode && String(b.code || '') !== se.code) return json(403, { error: '현장 코드가 맞지 않습니다' });
    const v = db.venue; let d = null;
    if (v.lat != null) {
      const lat = +b.lat, lng = +b.lng, acc = +b.accuracy || 0;
      if (!isFinite(lat) || !isFinite(lng)) return json(403, { error: '위치 정보가 필요합니다. 위치 권한을 허용해 주세요' });
      if (acc > 300) return json(403, { error: `위치가 부정확합니다(±${Math.round(acc)}m). 실외/창가에서 다시 시도해 주세요` });
      d = dist(lat, lng, v.lat, v.lng);
      if (d > v.radius) return json(403, { error: `교육장에서 ${Math.round(d)}m 떨어져 있습니다 (허용 ${v.radius}m)` });
    } else if (!db.settings.useCode) return json(403, { error: NO_VENUE });
    // ③ 기록
    if (registering) db.bindings[mem.id] = dev; // 위치까지 통과한 뒤에만 이 휴대폰을 본인 전용으로 등록
    const rec = ((db.records[se.date] ||= {})[se.lectureId] ||= {})[mem.groupId] ||= { marks: {}, meta: {} };
    rec.meta ||= {};
    if (rec.marks[mem.id] && rec.meta[mem.id]) { if (registering) save(); return json(200, { ok: true, already: true, status: rec.marks[mem.id], name: mem.name, registered: registering }); }
    rec.marks[mem.id] = w.status;
    rec.meta[mem.id] = { at: new Date().toISOString(), device: dev, dist: d == null ? null : Math.round(d), self: true };
    rec.at = rec.meta[mem.id].at;
    dirty.add(se.date);
    save();
    return json(200, { ok: true, status: rec.marks[mem.id], name: mem.name, registered: registering });
  }
  // 휴대폰을 바꾼 구성원이 담당자에게 기기 변경을 요청 (전화번호 뒤 4자리 확인 후 접수)
  if ((mm = p.match(/^\/api\/s\/([\w-]+)\/device-request$/)) && m === 'POST') {
    if (!sessionOf(mm[1])) return json(404, { error: '유효하지 않은 링크입니다' });
    const b = await body(req), dev = String(b.deviceId || '').slice(0, 64);
    const mem = db.members.find((x) => x.id === b.memberId);
    if (!mem || dev.length < 8) return json(400, { error: '요청 정보가 올바르지 않습니다' });
    const pk = 'ph:' + mem.id + ':' + ip(req);
    if (blocked(pk)) return json(429, { error: '시도가 너무 많습니다. 10분 뒤에 다시 시도하세요' });
    if (!mem.phone4 || !same(String(b.phone4 || ''), mem.phone4)) { strike(pk); return json(403, { error: '전화번호 뒤 4자리가 맞지 않습니다' }); }
    db.deviceRequests = (db.deviceRequests || []).filter((r) => r.memberId !== mem.id);
    db.deviceRequests.push({ id: uid('r'), memberId: mem.id, deviceId: dev, at: new Date().toISOString() });
    save();
    return json(200, { ok: true });
  }


  // ---- 개인 링크(/m/토큰): 구성원 본인 전용, 열면 위치 확인 후 자동 출석 (첫 기기에 고정) ----
  if ((mm = p.match(/^\/api\/m\/([\w-]+)$/)) && m === 'GET') {
    const mem = memberByQr(mm[1]); if (!mem) return json(404, { error: '유효하지 않은 링크입니다' });
    const g = db.groups.find((x) => x.id === mem.groupId), now = currentLecture();
    const st = stats('0000-00-00', '9999-99-99').members.find((x) => x.id === mem.id);
    const hist = [];
    for (const d of Object.keys(db.records).sort().reverse()) for (const [lid, byG] of Object.entries(db.records[d])) {
      const l = db.lectures.find((x) => x.id === lid); const s = ((byG[mem.groupId] || {}).marks || {})[mem.id];
      if (l) hist.push({ date: d, lecture: l.name, status: s || 'A' });
    }
    return json(200, { name: mem.name, group: g && g.name, stats: st, history: hist.slice(0, 15),
      now: now && { lecture: now.lecture.name, start: now.lecture.start, date: now.date, venue: db.venue.lat != null } });
  }
  if ((mm = p.match(/^\/api\/m\/([\w-]+)\/checkin$/)) && m === 'POST') {
    const mem = memberByQr(mm[1]); if (!mem) return json(404, { error: '유효하지 않은 링크입니다' });
    const now = currentLecture(); if (!now) return json(403, { error: `지금은 출석 가능한 강의가 없습니다 (시작 ${db.settings.openBefore}분 전 ~ ${db.settings.closeAfter}분 후)` });
    const b = await body(req), v = db.venue; let d = null;
    if (v.lat != null) {
      const lat = +b.lat, lng = +b.lng, acc = +b.accuracy || 0;
      if (!isFinite(lat) || !isFinite(lng)) return json(403, { error: '위치 정보가 필요합니다. 위치 권한을 허용해 주세요' });
      if (acc > 300) return json(403, { error: `위치가 부정확합니다(±${Math.round(acc)}m). 다시 시도해 주세요` });
      d = dist(lat, lng, v.lat, v.lng);
      if (d > v.radius) return json(403, { error: `교육장에서 ${Math.round(d)}m 떨어져 있습니다 (허용 ${v.radius}m)` });
    } else return json(403, { error: NO_VENUE });
    const dev = String(b.deviceId || '').slice(0, 64);
    db.bindings ||= {};
    const other = memberOfDevice(dev);
    if (other && other.id !== mem.id) return json(403, { error: `이 휴대폰은 ${other.name}님으로 등록되어 있어 다른 분의 링크로는 출석할 수 없습니다.` });
    if (db.bindings[mem.id] && dev && db.bindings[mem.id] !== dev)
      return json(403, { error: '이 휴대폰은 등록된 기기가 아닙니다. 휴대폰을 바꾸셨다면 담당자에게 기기 초기화를 요청하세요' });
    if (dev) db.bindings[mem.id] = dev;
    const r = mark(now.date, now.lecture, mem, now.window.status, { via: 'phone', dist: d == null ? null : Math.round(d), device: String(b.deviceId || '').slice(0, 64) });
    return json(200, { ok: true, lecture: now.lecture.name, ...r });
  }

  if (p === '/api/day' && m === 'GET') { // 단체 대표자: 해당 날짜 강의 + 우리 단체 출석 현황
    if (pinBlocked(req, q.get('groupId'))) return json(429, { error: '시도가 너무 많습니다. 10분 뒤에 다시 시도하세요' });
    const g = groupAuth(req, q.get('groupId')); if (!g) return json(401, { error: 'PIN이 맞지 않습니다' });
    const date = q.get('date'); if (!dateOk(date)) return json(400, { error: '날짜 오류' });
    const members = db.members.filter((x) => x.groupId === g.id).map(({ id, name }) => ({ id, name }));
    const day = db.records[date] || {};
    const lectures = lecturesOn(date).sort((a, b) => a.start.localeCompare(b.start))
      .map((l) => ({ ...l, marks: ((day[l.id] || {})[g.id] || {}).marks || null }));
    return json(200, { group: { id: g.id, name: g.name }, members, lectures });
  }

  if (p === '/api/attendance' && m === 'PUT') {
    const b = await body(req);
    if (pinBlocked(req, b.groupId)) return json(429, { error: '시도가 너무 많습니다. 10분 뒤에 다시 시도하세요' });
    const g = groupAuth(req, b.groupId); if (!g) return json(401, { error: 'PIN이 맞지 않습니다' });
    if (!dateOk(b.date)) return json(400, { error: '날짜 오류' });
    const l = db.lectures.find((x) => x.id === b.lectureId); if (!l) return json(404, { error: '강의 없음' });
    const ids = new Set(db.members.filter((x) => x.groupId === g.id).map((x) => x.id));
    const marks = {};
    for (const [mid, s] of Object.entries(b.marks || {})) if (ids.has(mid) && STATUS.includes(s)) marks[mid] = s;
    ((db.records[b.date] ||= {})[l.id] ||= {})[g.id] = { marks, at: new Date().toISOString() };
    dirty.add(b.date);
    save();
    return json(200, { ok: true });
  }

  if (p.startsWith('/api/admin/')) {
    if (!adminPassword) return json(503, { error: '서버에 ADMIN_PASSWORD가 설정되지 않았습니다' });
    if (blocked('admin:' + ip(req))) return json(429, { error: '시도가 너무 많습니다. 10분 뒤에 다시 시도하세요' });
    if (!isAdmin(req)) { strike('admin:' + ip(req)); return json(401, { error: '관리자 비밀번호가 맞지 않습니다' }); }
    if (p === '/api/admin/overview' && m === 'GET') {
      const date = q.get('date'); if (!dateOk(date)) return json(400, { error: '날짜 오류' });
      return json(200, { date, lectures: overview(date) });
    }
    if (p === '/api/admin/export' && m === 'GET') {
      return new Response(csv(q.get('from') || '0000-00-00', q.get('to') || '9999-99-99'), { status: 200,
        headers: { 'Content-Type': 'text/csv; charset=utf-8', 'Content-Disposition': 'attachment; filename="attendance.csv"' } });
    }
    if (p === '/api/admin/stats' && m === 'GET') return json(200, stats(q.get('from') || '0000-00-00', q.get('to') || '9999-99-99'));
    if (p === '/api/admin/links' && m === 'GET') { // 구성원별 개인 출석 링크(토큰)
      return json(200, db.groups.map((g) => ({ id: g.id, name: g.name,
        members: db.members.filter((x) => x.groupId === g.id).map((x) => ({ id: x.id, name: x.name, token: x.qr, bound: !!(db.bindings || {})[x.id] })) })));
    }
    if (p === '/api/admin/regen' && m === 'POST') { // 링크 유출·분실 시 새 링크 발급 (이전 링크·등록 휴대폰 무효)
      const b = await body(req), mem = db.members.find((x) => x.id === b.memberId);
      if (!mem) return json(404, { error: '구성원을 찾을 수 없습니다' });
      mem.qr = newQr(); if (db.bindings) delete db.bindings[mem.id]; save();
      return json(200, { ok: true, token: mem.qr });
    }
    if (p === '/api/admin/manual' && m === 'GET') {
      const date = q.get('date'), l = db.lectures.find((x) => x.id === q.get('lectureId'));
      if (!dateOk(date) || !l) return json(400, { error: '날짜/강의를 선택하세요' });
      const rec = (db.records[date] || {})[l.id] || {};
      return json(200, { lecture: { id: l.id, name: l.name, start: l.start, end: l.end },
        groups: db.groups.map((g) => ({ id: g.id, name: g.name, members: db.members.filter((x) => x.groupId === g.id).map((x) => {
          const r = rec[g.id] || {}, mt = (r.meta || {})[x.id];
          return { id: x.id, name: x.name, status: (r.marks || {})[x.id] || null, via: mt ? mt.via || (mt.self ? 'phone' : '') : ((r.marks || {})[x.id] ? 'group' : ''), note: mt && mt.note || '', bound: !!(db.bindings || {})[x.id] };
        }) })) });
    }
    if (p === '/api/admin/manual' && m === 'PUT') { // 담당자 수동 입력·정정 (시간 제한 없음)
      const b = await body(req), l = db.lectures.find((x) => x.id === b.lectureId);
      if (!dateOk(b.date) || !l) return json(400, { error: '날짜/강의를 선택하세요' });
      let n = 0;
      for (const [mid, v] of Object.entries(b.marks || {})) {
        const mem = db.members.find((x) => x.id === mid); if (!mem) continue;
        const rec = ((db.records[b.date] ||= {})[l.id] ||= {})[mem.groupId] ||= { marks: {}, meta: {} };
        rec.meta ||= {};
        if (STATUS.includes(v)) { rec.marks[mid] = v; rec.meta[mid] = { at: new Date().toISOString(), via: 'admin', note: String((b.notes || {})[mid] || '').slice(0, 100) }; n++; }
        else if (v === null || v === '') { delete rec.marks[mid]; delete rec.meta[mid]; n++; }
      }
      dirty.add(b.date);
      save();
      return json(200, { ok: true, changed: n });
    }
    if (p === '/api/admin/device-requests' && m === 'GET') {
      return json(200, (db.deviceRequests || []).map((r) => { const mem = db.members.find((x) => x.id === r.memberId);
        return mem ? { id: r.id, name: mem.name, group: (db.groups.find((g) => g.id === mem.groupId) || {}).name, at: r.at } : null; }).filter(Boolean));
    }
    if (p === '/api/admin/device-requests' && m === 'POST') {
      const b = await body(req), r = (db.deviceRequests || []).find((x) => x.id === b.id);
      if (!r) return json(404, { error: '요청을 찾을 수 없습니다' });
      if (b.approve) { // 새 휴대폰으로 교체: 그 휴대폰의 기존 등록·이전 휴대폰 등록 모두 정리
        for (const [id, v] of Object.entries(db.bindings || {})) if (v === r.deviceId) delete db.bindings[id];
        db.bindings[r.memberId] = r.deviceId;
      }
      db.deviceRequests = db.deviceRequests.filter((x) => x.id !== r.id); save();
      return json(200, { ok: true });
    }
    if (p === '/api/admin/reset-device' && m === 'POST') {
      const b = await body(req); if (db.bindings) delete db.bindings[b.memberId]; save();
      return json(200, { ok: true });
    }
    if (p === '/api/admin/session' && m === 'POST') {
      const b = await body(req);
      if (!dateOk(b.date) || !db.lectures.some((x) => x.id === b.lectureId)) return json(400, { error: '입력 오류' });
      let e = Object.entries(db.sessions).find(([, v]) => v.date === b.date && v.lectureId === b.lectureId);
      if (!e) {
        const t = newQr();
        db.sessions[t] = { date: b.date, lectureId: b.lectureId, code: String(Math.floor(1000 + Math.random() * 9000)), open: true };
        e = [t, db.sessions[t]];
      }
      if (typeof b.open === 'boolean') e[1].open = b.open;
      if (b.newCode) e[1].code = String(Math.floor(1000 + Math.random() * 9000));
      save();
      return json(200, { token: e[0], code: e[1].code, open: e[1].open });
    }
    if (p === '/api/admin/config' && m === 'GET') {
      return json(200, {
        groups: db.groups.map((g) => ({ ...g, members: db.members.filter((x) => x.groupId === g.id).map((x) => x.phone4 ? `${x.name} ${x.phone4}` : x.name) })),
        lectures: db.lectures, venue: db.venue, settings: db.settings,
      });
    }
    if (p === '/api/admin/config' && m === 'PUT') {
      const b = await body(req);
      const groups = [], members = [], lectures = [];
      for (const g of b.groups || []) {
        if (!g.name || !g.pin) continue;
        const id = g.id && db.groups.some((x) => x.id === g.id) ? g.id : uid('g');
        groups.push({ id, name: g.name.trim(), pin: String(g.pin).trim() });
        const old = db.members.filter((x) => x.groupId === id);
        const seen = new Set();
        for (const line of (g.members || []).map((s) => s.trim()).filter(Boolean)) {
          const mt = line.match(/^(.*\S)\s+(\d{4})$/);
          const name = mt ? mt[1] : line, phone4 = mt ? mt[2] : '';
          if (seen.has(name)) continue; seen.add(name);
          const ex = old.find((x) => x.name === name);
          members.push({ id: ex ? ex.id : uid('m'), groupId: id, name, phone4, qr: ex && ex.qr ? ex.qr : newQr() });
        }
      }
      for (const l of b.lectures || []) {
        if (!l.name) continue;
        const sd = dateOk(l.startDate) ? l.startDate : '', ed = dateOk(l.endDate) ? l.endDate : '';
        const off = [...new Set((Array.isArray(l.off) ? l.off : String(l.off || '').split(/[\s,]+/)).filter(dateOk))].sort();
        lectures.push({
          id: l.id && db.lectures.some((x) => x.id === l.id) ? l.id : uid('l'),
          name: l.name.trim(), weekday: sd && sd === ed ? wd(sd) : +l.weekday, // 하루짜리 강의는 날짜의 요일로 자동 지정
          start: l.start || '09:00', end: l.end || '10:00', startDate: sd, endDate: ed, off,
        });
      }
      const st = b.settings || {}, num = (x, d) => (isFinite(+x) && x !== '' && x !== null ? Math.max(0, Math.round(+x)) : d);
      db.settings = { openBefore: num(st.openBefore, 30), lateAfter: num(st.lateAfter, 10), closeAfter: num(st.closeAfter, 30), useCode: st.useCode === true || st.useCode === 'true' };
      const v = b.venue || {};
      db.venue = { name: String(v.name || ''), lat: v.lat === '' || v.lat == null ? null : +v.lat, lng: v.lng === '' || v.lng == null ? null : +v.lng, radius: +v.radius > 0 ? +v.radius : 150 };
      db.groups = groups; db.members = members; db.lectures = lectures;
      db.bindings = Object.fromEntries(Object.entries(db.bindings || {}).filter(([id]) => members.some((x) => x.id === id)));
      db.deviceRequests = (db.deviceRequests || []).filter((r) => members.some((x) => x.id === r.memberId));
      save();
      return json(200, { ok: true });
    }
    return json(404, { error: 'not found' });
  }
  json(404, { error: 'not found' });
}

  async function handle(request) {
    const url = new URL(request.url);
    if (!url.pathname.startsWith('/api/')) return null;
    try { return await api(request, url); }
    catch (e) { return json(500, { error: String((e && e.message) || e) }); }
  }
  return { handle };
}
