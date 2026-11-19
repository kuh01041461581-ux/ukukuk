// Cloudflare Worker 진입점: 정적 화면(public/) + /api/* 는 Durable Object 한 곳에서 처리합니다.
// Durable Object는 요청을 한 줄로 처리하므로 여러 명이 동시에 출석해도 기록이 겹쳐 사라지지 않습니다.
import { createCore, emptyDb } from './core.js';

// 다음 한국 시간 00:05 (UTC 기준 밀리초)
function nextKstMidnight() {
  const KST = 9 * 3600e3, DAY = 86400e3, now = Date.now();
  return Math.floor((now + KST) / DAY) * DAY + DAY + 5 * 60e3 - KST;
}

export class AttendanceDO {
  constructor(ctx, env) {
    this.ctx = ctx;
    this.ready = ctx.blockConcurrencyWhile(async () => {
      // 저장 구조: 'config'(단체·구성원·강의·설정 등) + 'rec:YYYY-MM-DD'(날짜별 출석 기록)
      const cfg = (await ctx.storage.get('config')) || emptyDb();
      const db = { ...cfg, records: {} };
      for (const [k, v] of await ctx.storage.list({ prefix: 'rec:' })) db.records[k.slice(4)] = v;
      const persist = (d, dates) => {
        const { records, ...config } = d;
        const entries = { config }, gone = [];
        for (const date of dates) { if (records[date] === undefined) gone.push('rec:' + date); else entries['rec:' + date] = records[date]; }
        ctx.storage.put(entries).catch((e) => console.error('storage.put failed', e));
        if (gone.length) ctx.storage.delete(gone).catch((e) => console.error('storage.delete failed', e));
      };
      // 자동 백업: 매일 한 번 명단·강의·설정(작은 데이터)을 14일치 보관. 출석 기록은 날짜별로 따로 저장되어 있음
      const backups = {
        list: async () => [...(await ctx.storage.list({ prefix: 'bak:' })).keys()].map((k) => k.slice(4)).sort().reverse(),
        get: async (date) => ctx.storage.get('bak:' + String(date).replace(/[^\d-]/g, '')),
      };
      this.backups = backups;
      this.core = createCore({ db, persist, adminPassword: env.ADMIN_PASSWORD, backups });
      this.core.tick();                                  // 오늘 열리는 강의 출석 자동 개설
      await this.backupToday();
      if ((await ctx.storage.getAlarm()) == null) await ctx.storage.setAlarm(nextKstMidnight());
    });
  }
  async backupToday() {
    const day = new Date(Date.now() + 9 * 3600e3).toISOString().slice(0, 10);
    if (!(await this.ctx.storage.get('bak:' + day))) await this.ctx.storage.put('bak:' + day, this.core.snapshot());
    const all = [...(await this.ctx.storage.list({ prefix: 'bak:' })).keys()].sort();
    if (all.length > 14) await this.ctx.storage.delete(all.slice(0, all.length - 14));
  }
  async alarm() { // 매일 한국 시간 자정 직후: 그날 열리는 강의의 출석을 자동으로 개설하고 다음 알람 예약
    await this.ready;
    this.core.tick();
    await this.backupToday();
    await this.ctx.storage.setAlarm(nextKstMidnight());
  }
  async fetch(request) {
    await this.ready;
    return (await this.core.handle(request)) || new Response('Not found', { status: 404 });
  }
}

const ROUTES = [['/c/', '/checkin.html', true], ['/m/', '/member.html', true], ['/admin', '/admin.html'], ['/', '/index.html']];

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname.startsWith('/api/')) {
      return env.ATTENDANCE.get(env.ATTENDANCE.idFromName('main')).fetch(request);
    }
    if (!['GET', 'HEAD'].includes(request.method)) return new Response('Method Not Allowed', { status: 405 });
    const hit = ROUTES.find(([p, , prefix]) => (prefix ? url.pathname.startsWith(p) : url.pathname === p));
    if (hit) url.pathname = hit[1];
    const res = await env.ASSETS.fetch(new Request(url, request));
    if (!hit) return res;
    const page = new Response(res.body, res);               // 화면(HTML)에서 위치 기능을 사용할 수 있음을 명시
    page.headers.set('Permissions-Policy', 'geolocation=(self)');
    return page;
  },
};
