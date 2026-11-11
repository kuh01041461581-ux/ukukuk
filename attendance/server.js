// 국가무형유산 출석체크 서버 (외부 패키지 없음, Node 18+)
const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = process.env.PORT || 3000;
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'admin1234';
const DATA_FILE = process.env.DATA_FILE || path.join(__dirname, 'data.json');
const PUBLIC = path.join(__dirname, 'public');

var db = { groups: [], members: [], lectures: [], records: {}, sessions: {}, bindings: {}, settings: { openBefore: 30, lateAfter: 10, closeAfter: 30 }, venue: { name: '', lat: null, lng: null, radius: 150 } };
var crypto = require('crypto');
function newQr() { return crypto.randomBytes(9).toString('base64url'); }
if (fs.existsSync(DATA_FILE)) {
  db = Object.assign(db, JSON.parse(fs.readFileSync(DATA_FILE, 'utf8')));
  db.settings = Object.assign({ openBefore: 30, lateAfter: 10, closeAfter: 30 }, db.settings);
} else seed();
let migrated = false;
for (const mm of db.members) if (!mm.qr) { mm.qr = newQr(); migrated = true; }

function save() {
  const tmp = DATA_FILE + '.tmp';
  fs.writeFileSync(tmp, JSON.stringify(db, null, 1));
  fs.renameSync(tmp, DATA_FILE);
}
function uid(p) { return p + Math.random().toString(36).slice(2, 8); }

function seed() {
  const names = ['A단체', 'B단체', 'C단체', 'D단체', 'E단체', 'F단체'];
  names.forEach((n, i) => {
    const g = { id: 'g' + (i + 1), name: n, pin: String(1111 * (i + 1)) };
    db.groups.push(g);
    for (let k = 1; k <= 3; k++) db.members.push({ id: uid('m'), groupId: g.id, name: `${n} 구성원${k}` });
  });
  const days = [1, 2, 3, 4, 5];
  let n = 1;
  for (const d of days) for (const [s, e] of [['09:00', '10:30'], ['10:40', '12:10'], ['13:30', '15:00'], ['15:10', '16:40']])
    db.lectures.push({ id: 'l' + n, name: `강의 ${n++}`, weekday: d, start: s, end: e });
  save();
}

if (migrated) save();
const json = (res, code, obj) => {
  res.writeHead(code, { 'Content-Type': 'application/json; charset=utf-8' });
  res.end(JSON.stringify(obj));
};
const body = (req) => new Promise((ok, no) => {
  let s = '';
  req.on('data', (c) => { s += c; if (s.length > 5e6) req.destroy(); });
  req.on('end', () => { try { ok(s ? JSON.parse(s) : {}); } catch (e) { no(e); } });
});
const isAdmin = (req) => req.headers['x-admin'] === ADMIN_PASSWORD;
const dateOk = (d) => /^\d{4}-\d{2}-\d{2}$/.test(d || '');
const wd = (d) => new Date(d + 'T00:00:00Z').getUTCDay();
const STATUS = ['P', 'L', 'A']; // 출석, 지각, 결석

function dist(a, b, c, d) { // meters
  const R = 6371000, r = Math.PI / 180, dLat = (c - a) * r, dLng = (d - b) * r;
  const x = Math.sin(dLat / 2) ** 2 + Math.cos(a * r) * Math.cos(c * r) * Math.sin(dLng / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(x));
}
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
  for (const l of db.lectures.filter((x) => x.weekday === wd(date))) {
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
  save();
  return { status };
}
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

function groupAuth(req, groupId) {
  const g = db.groups.find((x) => x.id === groupId);
  return g && req.headers['x-pin'] === g.pin ? g : null;
}

function overview(date) {
  const lectures = db.lectures.filter((l) => l.weekday === wd(date))
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

async function api(req, res, url) {
  const p = url.pathname, q = url.searchParams, m = req.method;

  if (p === '/api/groups' && m === 'GET') return json(res, 200, db.groups.map(({ id, name }) => ({ id, name })));


  let mm;
  if ((mm = p.match(/^\/api\/s\/([\w-]+)$/)) && m === 'GET') {
    const se = sessionOf(mm[1]); if (!se) return json(res, 404, { error: '유효하지 않은 링크입니다' });
    const l = db.lectures.find((x) => x.id === se.lectureId);
    return json(res, 200, {
      open: se.open, date: se.date, lecture: l && { name: l.name, start: l.start, end: l.end },
      venue: db.venue.lat == null ? null : { name: db.venue.name },
      groups: db.groups.map((g) => ({ id: g.id, name: g.name,
        members: db.members.filter((x) => x.groupId === g.id).map((x) => ({
          id: x.id, name: x.name, needPhone: !!x.phone4, done: !!(((db.records[se.date] || {})[se.lectureId] || {})[g.id] || { marks: {} }).marks[x.id] })) })),
    });
  }
  if ((mm = p.match(/^\/api\/s\/([\w-]+)\/checkin$/)) && m === 'POST') {
    const se = sessionOf(mm[1]); if (!se) return json(res, 404, { error: '유효하지 않은 링크입니다' });
    if (!se.open) return json(res, 403, { error: '출석이 마감되었습니다' });
    const lec0 = db.lectures.find((x) => x.id === se.lectureId);
    const w = windowOf(se.date, lec0); if (w.err) return json(res, 403, { error: w.err });
    const b = await body(req);
    const mem = db.members.find((x) => x.id === b.memberId); if (!mem) return json(res, 404, { error: '이름을 선택해 주세요' });
    const dev0 = String(b.deviceId || '').slice(0, 64);
    db.bindings ||= {};
    const bound = dev0 && db.bindings[mem.id] === dev0;
    if (!bound && mem.phone4 && String(b.phone4 || '') !== mem.phone4) return json(res, 403, { error: '본인 확인 번호(전화번호 뒤 4자리)가 맞지 않습니다', needPhone: true });
    if (String(b.code || '') !== se.code) return json(res, 403, { error: '현장 코드가 맞지 않습니다' });
    const v = db.venue; let d = null;
    if (v.lat != null) {
      const lat = +b.lat, lng = +b.lng, acc = +b.accuracy || 0;
      if (!isFinite(lat) || !isFinite(lng)) return json(res, 403, { error: '위치 정보가 필요합니다. 위치 권한을 허용해 주세요' });
      if (acc > 300) return json(res, 403, { error: `위치가 부정확합니다(±${Math.round(acc)}m). 실외/창가에서 다시 시도해 주세요` });
      d = dist(lat, lng, v.lat, v.lng);
      if (d > v.radius) return json(res, 403, { error: `교육장에서 ${Math.round(d)}m 떨어져 있습니다 (허용 ${v.radius}m)` });
    }
    const rec = ((db.records[se.date] ||= {})[se.lectureId] ||= {})[mem.groupId] ||= { marks: {}, meta: {} };
    rec.meta ||= {};
    const dev = String(b.deviceId || '').slice(0, 64);
    if (dev && !(rec.meta[mem.id] && rec.meta[mem.id].device === dev)) {
      for (const g of Object.values(db.records[se.date][se.lectureId]))
        for (const [id, mt] of Object.entries(g.meta || {}))
          if (mt.device === dev && id !== mem.id) return json(res, 403, { error: '이 휴대폰으로는 이미 다른 분이 출석했습니다' });
    }
    if (rec.marks[mem.id] && rec.meta[mem.id]) return json(res, 200, { ok: true, already: true, status: rec.marks[mem.id] });
    rec.marks[mem.id] = w.status;
    rec.meta[mem.id] = { at: new Date().toISOString(), device: dev, dist: d == null ? null : Math.round(d), self: true };
    rec.at = rec.meta[mem.id].at;
    if (dev) db.bindings[mem.id] = dev; // 첫 출석 성공 시 이 휴대폰을 본인 기기로 등록
    save();
    return json(res, 200, { ok: true, status: rec.marks[mem.id], memberId: mem.id });
  }


  // ---- 개인 링크(/m/토큰): NFC 스티커·QR 명찰로 열면 자동 출석 ----
  if ((mm = p.match(/^\/api\/m\/([\w-]+)$/)) && m === 'GET') {
    const mem = memberByQr(mm[1]); if (!mem) return json(res, 404, { error: '유효하지 않은 링크입니다' });
    const g = db.groups.find((x) => x.id === mem.groupId), now = currentLecture();
    const st = stats('0000-00-00', '9999-99-99').members.find((x) => x.id === mem.id);
    const hist = [];
    for (const d of Object.keys(db.records).sort().reverse()) for (const [lid, byG] of Object.entries(db.records[d])) {
      const l = db.lectures.find((x) => x.id === lid); const s = ((byG[mem.groupId] || {}).marks || {})[mem.id];
      if (l) hist.push({ date: d, lecture: l.name, status: s || 'A' });
    }
    return json(res, 200, { name: mem.name, group: g && g.name, stats: st, history: hist.slice(0, 15),
      now: now && { lecture: now.lecture.name, start: now.lecture.start, date: now.date, venue: db.venue.lat != null } });
  }
  if ((mm = p.match(/^\/api\/m\/([\w-]+)\/checkin$/)) && m === 'POST') {
    const mem = memberByQr(mm[1]); if (!mem) return json(res, 404, { error: '유효하지 않은 링크입니다' });
    const now = currentLecture(); if (!now) return json(res, 403, { error: `지금은 출석 가능한 강의가 없습니다 (시작 ${db.settings.openBefore}분 전 ~ ${db.settings.closeAfter}분 후)` });
    const b = await body(req), v = db.venue; let d = null;
    if (v.lat != null) {
      const lat = +b.lat, lng = +b.lng, acc = +b.accuracy || 0;
      if (!isFinite(lat) || !isFinite(lng)) return json(res, 403, { error: '위치 정보가 필요합니다. 위치 권한을 허용해 주세요' });
      if (acc > 300) return json(res, 403, { error: `위치가 부정확합니다(±${Math.round(acc)}m). 다시 시도해 주세요` });
      d = dist(lat, lng, v.lat, v.lng);
      if (d > v.radius) return json(res, 403, { error: `교육장에서 ${Math.round(d)}m 떨어져 있습니다 (허용 ${v.radius}m)` });
    }
    const r = mark(now.date, now.lecture, mem, now.window.status, { via: 'phone', dist: d == null ? null : Math.round(d), device: String(b.deviceId || '').slice(0, 64) });
    return json(res, 200, { ok: true, lecture: now.lecture.name, ...r });
  }

  if (p === '/api/day' && m === 'GET') { // 단체 대표자: 해당 날짜 강의 + 우리 단체 출석 현황
    const g = groupAuth(req, q.get('groupId')); if (!g) return json(res, 401, { error: 'PIN이 맞지 않습니다' });
    const date = q.get('date'); if (!dateOk(date)) return json(res, 400, { error: '날짜 오류' });
    const members = db.members.filter((x) => x.groupId === g.id).map(({ id, name }) => ({ id, name }));
    const day = db.records[date] || {};
    const lectures = db.lectures.filter((l) => l.weekday === wd(date)).sort((a, b) => a.start.localeCompare(b.start))
      .map((l) => ({ ...l, marks: ((day[l.id] || {})[g.id] || {}).marks || null }));
    return json(res, 200, { group: { id: g.id, name: g.name }, members, lectures });
  }

  if (p === '/api/attendance' && m === 'PUT') {
    const b = await body(req);
    const g = groupAuth(req, b.groupId); if (!g) return json(res, 401, { error: 'PIN이 맞지 않습니다' });
    if (!dateOk(b.date)) return json(res, 400, { error: '날짜 오류' });
    const l = db.lectures.find((x) => x.id === b.lectureId); if (!l) return json(res, 404, { error: '강의 없음' });
    const ids = new Set(db.members.filter((x) => x.groupId === g.id).map((x) => x.id));
    const marks = {};
    for (const [mid, s] of Object.entries(b.marks || {})) if (ids.has(mid) && STATUS.includes(s)) marks[mid] = s;
    ((db.records[b.date] ||= {})[l.id] ||= {})[g.id] = { marks, at: new Date().toISOString() };
    save();
    return json(res, 200, { ok: true });
  }

  if (p.startsWith('/api/admin/')) {
    if (!isAdmin(req)) return json(res, 401, { error: '관리자 비밀번호가 맞지 않습니다' });
    if (p === '/api/admin/overview' && m === 'GET') {
      const date = q.get('date'); if (!dateOk(date)) return json(res, 400, { error: '날짜 오류' });
      return json(res, 200, { date, lectures: overview(date) });
    }
    if (p === '/api/admin/export' && m === 'GET') {
      res.writeHead(200, { 'Content-Type': 'text/csv; charset=utf-8', 'Content-Disposition': 'attachment; filename="attendance.csv"' });
      return res.end(csv(q.get('from') || '0000-00-00', q.get('to') || '9999-99-99'));
    }
    if (p === '/api/admin/stats' && m === 'GET') return json(res, 200, stats(q.get('from') || '0000-00-00', q.get('to') || '9999-99-99'));
    if (p === '/api/admin/cards' && m === 'GET') {
      return json(res, 200, db.groups.map((g) => ({ id: g.id, name: g.name,
        members: db.members.filter((x) => x.groupId === g.id).map((x) => ({ name: x.name, qr: x.qr })) })));
    }
    if (p === '/api/admin/kiosk' && m === 'GET') { // 입구 태블릿: 지금 강의 + 오늘 강의 + 출석 현황
      const now = currentLecture(), date = kstNow().toISOString().slice(0, 10);
      const lid = q.get('lectureId') || (now && now.lecture.id);
      const todays = db.lectures.filter((x) => x.weekday === wd(date)).sort((a, b) => a.start.localeCompare(b.start));
      const lec = db.lectures.find((x) => x.id === lid);
      const rec = (lec && (db.records[date] || {})[lec.id]) || {};
      return json(res, 200, { date, auto: !q.get('lectureId'), lectureId: lid || null, lectures: todays.map(({ id, name, start, end }) => ({ id, name, start, end })),
        groups: db.groups.map((g) => ({ id: g.id, name: g.name, members: db.members.filter((x) => x.groupId === g.id).map((x) => ({ id: x.id, name: x.name, status: ((rec[g.id] || {}).marks || {})[x.id] || null })) })) });
    }
    if (p === '/api/admin/scan' && m === 'POST') { // 태블릿 카메라로 명찰 QR 인식 또는 이름 직접 선택
      const b = await body(req);
      const mem = b.memberId ? db.members.find((x) => x.id === b.memberId) : memberByQr(b.qr);
      if (!mem) return json(res, 404, { error: '등록되지 않은 QR입니다' });
      const date = kstNow().toISOString().slice(0, 10);
      const l = b.lectureId ? db.lectures.find((x) => x.id === b.lectureId) : (currentLecture() || {}).lecture;
      if (!l) return json(res, 403, { error: '지금 출석 가능한 강의가 없습니다' });
      const w = windowOf(date, l); if (w.err) return json(res, 403, { error: w.err });
      const r = mark(date, l, mem, w.status, { via: b.memberId ? 'kiosk-manual' : 'kiosk' });
      return json(res, 200, { ok: true, name: mem.name, group: (db.groups.find((x) => x.id === mem.groupId) || {}).name, lecture: l.name, ...r });
    }
    if (p === '/api/admin/session' && m === 'POST') {
      const b = await body(req);
      if (!dateOk(b.date) || !db.lectures.some((x) => x.id === b.lectureId)) return json(res, 400, { error: '입력 오류' });
      let e = Object.entries(db.sessions).find(([, v]) => v.date === b.date && v.lectureId === b.lectureId);
      if (!e) {
        const t = require('crypto').randomBytes(9).toString('base64url');
        db.sessions[t] = { date: b.date, lectureId: b.lectureId, code: String(Math.floor(1000 + Math.random() * 9000)), open: true };
        e = [t, db.sessions[t]];
      }
      if (typeof b.open === 'boolean') e[1].open = b.open;
      if (b.newCode) e[1].code = String(Math.floor(1000 + Math.random() * 9000));
      save();
      return json(res, 200, { token: e[0], code: e[1].code, open: e[1].open });
    }
    if (p === '/api/admin/config' && m === 'GET') {
      return json(res, 200, {
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
        lectures.push({
          id: l.id && db.lectures.some((x) => x.id === l.id) ? l.id : uid('l'),
          name: l.name.trim(), weekday: +l.weekday, start: l.start || '09:00', end: l.end || '10:00',
        });
      }
      const st = b.settings || {}, num = (x, d) => (isFinite(+x) && x !== '' && x !== null ? Math.max(0, Math.round(+x)) : d);
      db.settings = { openBefore: num(st.openBefore, 30), lateAfter: num(st.lateAfter, 10), closeAfter: num(st.closeAfter, 30) };
      const v = b.venue || {};
      db.venue = { name: String(v.name || ''), lat: v.lat === '' || v.lat == null ? null : +v.lat, lng: v.lng === '' || v.lng == null ? null : +v.lng, radius: +v.radius > 0 ? +v.radius : 150 };
      db.groups = groups; db.members = members; db.lectures = lectures;
      db.bindings = Object.fromEntries(Object.entries(db.bindings || {}).filter(([id]) => members.some((x) => x.id === id)));
      save();
      return json(res, 200, { ok: true });
    }
    return json(res, 404, { error: 'not found' });
  }
  json(res, 404, { error: 'not found' });
}

const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json' };
http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://x');
  try {
    if (url.pathname.startsWith('/api/')) return await api(req, res, url);
    let f = url.pathname.startsWith('/c/') ? '/checkin.html' : url.pathname.startsWith('/m/') ? '/member.html' : url.pathname === '/kiosk' ? '/kiosk.html' : url.pathname === '/cards' ? '/cards.html' : url.pathname === '/' ? '/index.html' : url.pathname === '/admin' ? '/admin.html' : url.pathname;
    f = path.join(PUBLIC, path.normalize(f));
    if (!f.startsWith(PUBLIC) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.writeHead(404); return res.end('Not found'); }
    res.writeHead(200, { 'Content-Type': MIME[path.extname(f)] || 'application/octet-stream' });
    fs.createReadStream(f).pipe(res);
  } catch (e) { json(res, 500, { error: String(e.message || e) }); }
}).listen(PORT, () => console.log(`출석체크 서버: http://localhost:${PORT}  (관리자: /admin)`));
