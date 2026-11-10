// 국가무형유산 출석체크 서버 (외부 패키지 없음, Node 18+)
const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = process.env.PORT || 3000;
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'admin1234';
const DATA_FILE = process.env.DATA_FILE || path.join(__dirname, 'data.json');
const PUBLIC = path.join(__dirname, 'public');

var db = { groups: [], members: [], lectures: [], records: {}, sessions: {}, venue: { name: '', lat: null, lng: null, radius: 150 } };
if (fs.existsSync(DATA_FILE)) { db = Object.assign(db, JSON.parse(fs.readFileSync(DATA_FILE, 'utf8'))); }
else seed();

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
          id: x.id, name: x.name, done: !!(((db.records[se.date] || {})[se.lectureId] || {})[g.id] || { marks: {} }).marks[x.id] })) })),
    });
  }
  if ((mm = p.match(/^\/api\/s\/([\w-]+)\/checkin$/)) && m === 'POST') {
    const se = sessionOf(mm[1]); if (!se) return json(res, 404, { error: '유효하지 않은 링크입니다' });
    if (!se.open) return json(res, 403, { error: '출석이 마감되었습니다' });
    const b = await body(req);
    const mem = db.members.find((x) => x.id === b.memberId); if (!mem) return json(res, 404, { error: '이름을 선택해 주세요' });
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
    const l = db.lectures.find((x) => x.id === se.lectureId);
    const k = kstNow(), nowMin = k.getUTCHours() * 60 + k.getUTCMinutes();
    const [h, mi] = l.start.split(':').map(Number);
    const late = k.toISOString().slice(0, 10) === se.date && nowMin > h * 60 + mi + 10;
    rec.marks[mem.id] = late ? 'L' : 'P';
    rec.meta[mem.id] = { at: new Date().toISOString(), device: dev, dist: d == null ? null : Math.round(d), self: true };
    rec.at = rec.meta[mem.id].at;
    save();
    return json(res, 200, { ok: true, status: rec.marks[mem.id] });
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
        groups: db.groups.map((g) => ({ ...g, members: db.members.filter((x) => x.groupId === g.id).map((x) => x.name) })),
        lectures: db.lectures, venue: db.venue,
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
        for (const name of [...new Set((g.members || []).map((s) => s.trim()).filter(Boolean))]) {
          const ex = old.find((x) => x.name === name);
          members.push({ id: ex ? ex.id : uid('m'), groupId: id, name });
        }
      }
      for (const l of b.lectures || []) {
        if (!l.name) continue;
        lectures.push({
          id: l.id && db.lectures.some((x) => x.id === l.id) ? l.id : uid('l'),
          name: l.name.trim(), weekday: +l.weekday, start: l.start || '09:00', end: l.end || '10:00',
        });
      }
      const v = b.venue || {};
      db.venue = { name: String(v.name || ''), lat: v.lat === '' || v.lat == null ? null : +v.lat, lng: v.lng === '' || v.lng == null ? null : +v.lng, radius: +v.radius > 0 ? +v.radius : 150 };
      db.groups = groups; db.members = members; db.lectures = lectures; save();
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
    let f = url.pathname.startsWith('/c/') ? '/checkin.html' : url.pathname === '/' ? '/index.html' : url.pathname === '/admin' ? '/admin.html' : url.pathname;
    f = path.join(PUBLIC, path.normalize(f));
    if (!f.startsWith(PUBLIC) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.writeHead(404); return res.end('Not found'); }
    res.writeHead(200, { 'Content-Type': MIME[path.extname(f)] || 'application/octet-stream' });
    fs.createReadStream(f).pipe(res);
  } catch (e) { json(res, 500, { error: String(e.message || e) }); }
}).listen(PORT, () => console.log(`출석체크 서버: http://localhost:${PORT}  (관리자: /admin)`));
