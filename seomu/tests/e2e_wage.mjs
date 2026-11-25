// node seomu/tests/e2e_wage.mjs — 기간제 인부임 도구를 실제 브라우저에서 끝까지 시험 (가짜 명부·고지서)
// 환경변수: PW_PATH(playwright 경로), OUT(결과 파일 폴더), DIST(시험할 html)
import { createRequire } from 'module';
import path from 'path'; import fs from 'fs'; import { fileURLToPath } from 'url';
const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PW_PATH || 'playwright');
const here = path.dirname(fileURLToPath(import.meta.url));
const dist = process.env.DIST || path.resolve(here, '../dist/서무종합프로그램.html');
const out = process.env.OUT || path.join(here, 'out'); fs.mkdirSync(out, { recursive: true });
const fx = n => path.join(here, 'fixtures', n);
let fails = 0; const ok = (c, m) => { console.log((c ? 'PASS ' : 'FAIL ') + m); if (!c) fails++; };

const b = await chromium.launch(); const ctx = await b.newContext({ viewport: { width: 1280, height: 900 }, acceptDownloads: true }); const pg = await ctx.newPage();
pg.on('pageerror', e => { console.log('PAGEERR', e.message); fails++; });
pg.on('dialog', d => { console.log('DIALOG', d.message()); d.accept(); });
await pg.goto('file://' + dist + '#wage');
const fr = await (await pg.locator('#frame-wage').elementHandle({ timeout: 30000 })).contentFrame();
await fr.waitForSelector('#f1', { state: 'attached', timeout: 60000 });
await fr.fill('#ym', '2026-10');
await fr.setInputFiles('#f1', fx('roster.xlsx'));
await fr.waitForSelector('#rosterT tbody tr', { timeout: 30000 });
ok((await fr.$$eval('#rosterT tbody tr', r => r.length)) === 3, '명부 3명 인식(과 이름 줄은 제외)');
await fr.setInputFiles('#f2', fx('insurance.xlsx'));
await fr.waitForSelector('#insT tbody tr', { timeout: 30000 });
const st = await fr.evaluate(() => { const s = __wage.state(); return s.workers.map(w => ({ n: w.name, ins: w.ins && w.ins.status, h: w.ins && w.ins.health, hire: w.hire, c: (({ days, joo, wolcha, gross, net }) => ({ days, joo, wolcha, gross, net }))(__wage.calc(w)) })); });
console.log(JSON.stringify(st));
ok(st[0].ins === 'ok' && st[1].ins === 'ok', '고지서에서 두 명 모두 찾음');
ok(st[0].c.days === 18 && st[0].c.joo === 4 && st[0].c.wolcha === 1 && st[0].c.gross === 1653940, '평일 근로자 18일·주휴4·월차1 = 1,653,940');
ok(st[1].c.days === 9 && st[1].c.gross === 676620, '주말 근로자 9일 = 676,620');
// 소득세 입력
await fr.fill('.wk:nth-child(1) [data-o="tax"]', '12020');
await pg.waitForTimeout(300);
const net1 = await fr.evaluate(() => __wage.calc(__wage.state().workers[0]).net);
ok(net1 === 1575650, '소득세 12,020 입력 → 실지급 1,575,650');
// 달력 날짜 눌러 휴무 처리
await fr.click('.wk:nth-child(1) button[data-d="2026-10-14"]');
const d2 = await fr.evaluate(() => { const c = __wage.calc(__wage.state().workers[0]); return [c.days, c.joo, c.wolcha]; });
ok(JSON.stringify(d2) === '[17,3,0]', '10/14 휴무 → 17일·주휴3·월차0');
await fr.click('.wk:nth-child(1) button[data-d="2026-10-14"]');
// 계좌(공단) 입력 — 접힌 칸을 펼친 뒤
await fr.evaluate(() => document.querySelectorAll('details.fold').forEach(d => d.open = true));
await fr.fill('[data-a="health"][data-f="bank"]', '농협'); await fr.fill('[data-a="health"][data-f="no"]', '000-0000-0000-00');
await pg.screenshot({ path: path.join(out, 'wage_page.png'), fullPage: true });
// 엑셀 만들기
const dl = pg.waitForEvent('download', { timeout: 60000 });
await fr.click('#btnXlsx');
const d = await dl; const xl = path.join(out, 'wage_out.xlsx'); await d.saveAs(xl);
ok(fs.statSync(xl).size > 5000, '엑셀 내려받기: ' + d.suggestedFilename());
// 이호조 거래처입력
await fr.fill('[data-a="incomeTax"][data-f="bank"]','농협'); await fr.fill('[data-a="incomeTax"][data-f="no"]','79000000000001');
const ih = await fr.evaluate(async () => { const r = await __wage.buildIhojo(); let s=''; const u=new Uint8Array(r.buf); for (let i=0;i<u.length;i++) s+=String.fromCharCode(u[i]); return {rows:r.rows, warn:r.warn, b64:btoa(s)}; });
fs.writeFileSync(path.join(out, 'ihojo_out.xlsx'), Buffer.from(ih.b64, 'base64'));
ok(ih.rows === 9, '이호조 줄 수 9 (근로자3+보험4+소득세+지방소득세): ' + ih.rows);
// 한글(HWPX)
for (const m of ['daily', 'claim']) {
  const hx = await fr.evaluate(async (m) => { const u = await __wage.buildHwpx(m); let s = ''; for (let i = 0; i < u.length; i++) s += String.fromCharCode(u[i]); return btoa(s); }, m);
  fs.writeFileSync(path.join(out, 'wage_' + m + '.hwpx'), Buffer.from(hx, 'base64'));
  ok(Buffer.from(hx, 'base64').subarray(0, 2).toString() === 'PK', 'HWPX(' + m + ') 만들어짐');
}
// 일일점검부·청구서 미리보기
await fr.click('#btnPvDaily'); await pg.waitForTimeout(300);
ok((await fr.$$eval('.previewbox .paper.daily', e => e.length)) === 3, '일일점검부 3장(근로자별)');
const dailyHtml = await fr.evaluate(() => __wage.dailyHtml(__wage.state().workers[0]));
ok(/평일근무일수/.test(dailyHtml) && /휴관/.test(dailyHtml) && /10\/1\(목\)/.test(dailyHtml) && /10\/31\(토\)/.test(dailyHtml), '일일점검부: 날짜·요일·휴관 표시');
ok((dailyHtml.match(/class="work"/g) || []).length === 18, '일일점검부: 근무일 18칸에 도장 사선');
{ const el = await fr.$('.previewbox .paper.daily'); if (el) await el.screenshot({ path: path.join(out, 'doc_daily.png') }); }
await fr.click('#btnPvClaim'); await pg.waitForTimeout(300);
{ const el = await fr.$('.previewbox .paper.claim'); if (el) await el.screenshot({ path: path.join(out, 'doc_claim.png') }); }
const claim = await fr.evaluate(() => __wage.claimHtml(__wage.state().workers[0]));
ok(/1,653,940/.test(claim) && /기본급 1,353,240원/.test(claim) && /주휴수당 240,560원/.test(claim) && /월차수당 60,140원/.test(claim) && /10\.1\. ~ 10\.31\.\(평일\)/.test(claim), '청구서: 총액·기본급·주휴·월차·사역기간');
await b.close();
console.log(fails ? 'E2E FAIL ' + fails : 'E2E OK');
process.exit(fails ? 1 : 0);
