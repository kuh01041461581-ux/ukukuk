// Cloudflare Worker 진입점: 정적 화면(public/) + /api/* 는 Durable Object 한 곳에서 처리합니다.
// Durable Object는 요청을 한 줄로 처리하므로 여러 명이 동시에 출석해도 기록이 겹쳐 사라지지 않습니다.
import { createCore, emptyDb } from './core.js';

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
        const entries = { config };
        for (const date of dates) entries['rec:' + date] = records[date];
        ctx.storage.put(entries).catch((e) => console.error('storage.put failed', e));
      };
      this.core = createCore({ db, persist, adminPassword: env.ADMIN_PASSWORD });
    });
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
    return env.ASSETS.fetch(new Request(url, request));
  },
};
