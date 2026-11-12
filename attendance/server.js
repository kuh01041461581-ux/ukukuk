// 로컬 실행용 Node 서버 (개발·시험·자체 서버 운영용). Cloudflare 배포는 src/worker.js 를 사용합니다.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createCore, emptyDb, seedDb } from './src/core.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const PORT = process.env.PORT || 3000;
const DATA_FILE = process.env.DATA_FILE || path.join(here, 'data.json');
const PUBLIC = path.join(here, 'public');
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'admin1234';
if (!process.env.ADMIN_PASSWORD) console.warn('⚠ ADMIN_PASSWORD 미설정: 기본 비밀번호(admin1234)를 사용합니다. 실제 운영 전에 반드시 바꾸세요.');

let db, fresh = false;
if (fs.existsSync(DATA_FILE)) db = JSON.parse(fs.readFileSync(DATA_FILE, 'utf8'));
else { db = emptyDb(); seedDb(db); fresh = true; }

const persist = (d) => {
  const tmp = DATA_FILE + '.tmp';
  fs.writeFileSync(tmp, JSON.stringify(d, null, 1));
  fs.renameSync(tmp, DATA_FILE);
};
const core = createCore({ db, persist, adminPassword: ADMIN_PASSWORD });
if (fresh) persist(db);

const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json' };
const ROUTES = [['/c/', '/checkin.html', true], ['/m/', '/member.html', true], ['/admin', '/admin.html'], ['/', '/index.html']];

http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, 'http://localhost');
    const headers = new Headers(req.headers);
    headers.set('cf-connecting-ip', req.socket.remoteAddress || 'local');
    let bodyBuf;
    if (!['GET', 'HEAD'].includes(req.method)) { const c = []; for await (const x of req) c.push(x); bodyBuf = Buffer.concat(c); }
    const r = await core.handle(new Request(url, { method: req.method, headers, body: bodyBuf }));
    if (r) {
      res.writeHead(r.status, Object.fromEntries(r.headers));
      return res.end(Buffer.from(await r.arrayBuffer()));
    }
    const hit = ROUTES.find(([p, , prefix]) => (prefix ? url.pathname.startsWith(p) : url.pathname === p));
    const f = path.join(PUBLIC, path.normalize(hit ? hit[1] : url.pathname));
    if (!f.startsWith(PUBLIC) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.writeHead(404); return res.end('Not found'); }
    res.writeHead(200, { 'Content-Type': MIME[path.extname(f)] || 'application/octet-stream' });
    fs.createReadStream(f).pipe(res);
  } catch (e) { res.writeHead(500); res.end(String(e.message || e)); }
}).listen(PORT, () => console.log(`출석체크 서버: http://localhost:${PORT}  (관리자: /admin)`));
