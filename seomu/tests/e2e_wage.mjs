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
const tx0 = await fr.evaluate(() => { const c = __wage.calc(__wage.state().workers[0]); return [c.ded.incomeTax, c.ded.localTax, c.net]; });
ok(JSON.stringify(tx0) === '[12020,1200,1575650]', '소득세 자동(간이세액표) 12,020 · 지방 1,200 · 실지급 1,575,650: ' + tx0);
const tx1 = await fr.evaluate(() => __wage.calc(__wage.state().workers[1]).ded.incomeTax);
ok(tx1 === 0, '주말 근로자 소득세 0');
await fr.click('tr.fx:nth-of-type(1) [data-tog]');
await fr.waitForSelector('tr.dt:not([hidden]) [data-b="why"] li');
const why = await fr.$eval('tr.dt:not([hidden]) [data-b="why"]', e => e.textContent);
ok(/간이세액표 1,650,000~1,660,000원 구간/.test(why) && /60,140원/.test(why), '계산 근거에 간이세액표 구간·주휴 단가 표시');
// 달력 날짜 눌러 휴무 처리
await fr.click('tr.dt:not([hidden]) button[data-d="2026-10-14"]');
const d2 = await fr.evaluate(() => { const c = __wage.calc(__wage.state().workers[0]); return [c.days, c.joo, c.wolcha]; });
ok(JSON.stringify(d2) === '[17,3,0]', '10/14 휴무 → 17일·주휴3·월차0');
await fr.click('tr.dt:not([hidden]) button[data-d="2026-10-14"]');
// 휴관 날짜: 설정 달력에서 10/14 를 휴관으로 → 근무일 17, 주휴·월차는 그대로(결근 아님) → 다시 눌러 해제
await fr.click('#closedCal [data-cd="2026-10-14"]');
const cd = await fr.evaluate(() => { const c = __wage.calc(__wage.state().workers[0]); return [c.days, c.joo, c.wolcha, /휴관/.test(__wage.dailyHtml(__wage.state().workers[0]).split('10/14')[1].slice(0, 400))]; });
ok(JSON.stringify(cd) === '[17,4,1,true]', '휴관 날짜 10/14 → 17일 · 주휴 4 · 월차 1 · 일일점검부에 휴관: ' + cd);
await fr.click('#closedCal [data-cd="2026-10-14"]');
// 직접 수정: 표의 소득세 숫자를 눌러 고치기 → 지방소득세·실지급 따라감, 되돌리기
await fr.click('tr.fx:nth-of-type(1) td[data-b="d_income"]');
await fr.fill('tr.dt:not([hidden]) [data-o="tax"]', '15000');
await fr.fill('tr.dt:not([hidden]) [data-o="dayRate"]', '80000');
const ov = await fr.evaluate(() => { const c = __wage.calc(__wage.state().workers[0]); return [c.ded.incomeTax, c.ded.localTax, c.basePay, c.edited.sort().join(',')]; });
ok(JSON.stringify(ov) === '[15000,1500,1440000,"dayRate,tax"]', '직접 수정: 소득세 15,000 → 지방 1,500 · 단가 80,000 → 기본급 1,440,000: ' + ov);
ok(await fr.$eval('tr.fx:nth-of-type(1) td[data-b="d_income"]', e => e.classList.contains('ed')), '고친 칸 표시(✎)');
await fr.click('tr.dt:not([hidden]) [data-reset]');
const rv = await fr.evaluate(() => { const c = __wage.calc(__wage.state().workers[0]); return [c.ded.incomeTax, c.basePay, c.edited.length]; });
ok(JSON.stringify(rv) === '[12020,1353240,0]', '자동 계산으로 되돌리기: ' + rv);
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
