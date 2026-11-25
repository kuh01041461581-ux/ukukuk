// node seomu/tests/e2e_scan.mjs — 도장 받은 일일점검부·청구서 스캔 PDF 인식 시험 (가짜 스캔: tests/make_scan_fixture.mjs 로 만듦)
import { createRequire } from 'module'; import path from 'path'; import { fileURLToPath } from 'url';
const require = createRequire(import.meta.url); const { chromium } = require(process.env.PW_PATH || 'playwright');
const here = path.dirname(fileURLToPath(import.meta.url)), fx = n => path.join(here, 'fixtures', n);
let fails = 0; const ok = (c, m) => { console.log((c ? 'PASS ' : 'FAIL ') + m); if (!c) fails++; };
const b = await chromium.launch(); const pg = await (await b.newContext({ viewport: { width: 1280, height: 900 } })).newPage();
pg.on('pageerror', e => { console.log('PAGEERR', e.message); fails++; }); pg.on('dialog', d => d.accept());
await pg.goto('file://' + path.resolve(here, '../dist/서무종합프로그램.html') + '#wage');
const fr = await (await pg.locator('#frame-wage').elementHandle()).contentFrame();
await fr.waitForSelector('#f1', { state: 'attached', timeout: 60000 }); await fr.fill('#ym', '2026-10');
await fr.setInputFiles('#f1', fx('roster.xlsx')); await fr.waitForSelector('#rosterT tbody tr');
await fr.setInputFiles('#f2', fx('insurance.xlsx')); await fr.waitForSelector('#insT tbody tr');
await fr.setInputFiles('#f4', fx('scan_signed.pdf'));
await fr.waitForSelector('.scan-tbl', { timeout: 300000 });
const sc = await fr.evaluate(() => __wage.scans().map(x => ({ t: x.type, kind: x.kind || (x.claim && x.claim.kind), n: x.stamps && x.stamps.dates.length, miss: x.stamps && !x.stamps.dates.includes('2026-10-14'),
  name: x.type === 'daily' ? x.name : x.claim && x.claim.name, who: x.wid && __wage.state().workers.find(w => w.id === x.wid).name, gross: x.claim && x.claim.gross, acct: x.claim && x.claim.account, bank: x.claim && x.claim.bank })));
console.log(JSON.stringify(sc));
ok(sc.length === 4 && sc[0].t === 'daily' && sc[1].t === 'daily' && sc[2].t === 'claim' && sc[3].t === 'claim', '4쪽: 일일점검부 2 · 청구서 2');
ok(sc[0].kind === 'weekday' && sc[0].n === 17 && sc[0].miss, '평일 일일점검부: 도장 17칸(10/14 없음)');
ok(sc[1].kind === 'weekend' && sc[1].n === 9, '주말 일일점검부: 도장 9칸');
ok(sc[0].who === '홍평일' && sc[1].who === '김주말', '일일점검부 → 근로자 짝(비고 칸 이름)');
ok(sc[2].who === '홍평일' && sc[2].gross === 1653940 && sc[2].acct === '1021-01-0000001', '청구서(평일): 이름·청구금액·계좌');
ok(sc[3].who === '김주말' && sc[3].gross === 676620, '청구서(주말): 이름·청구금액');
const diff = await fr.$eval('.scan-tbl tbody tr:nth-child(1)', e => e.textContent);
ok(/근무일인데 도장 없음: 10\/14/.test(diff), '도장 없는 날(10/14)을 찾아 표시');
await fr.click('#scanApplyAll'); await pg.waitForTimeout(400);
const c = await fr.evaluate(() => { const c = __wage.calc(__wage.state().workers[0]); return [c.days, c.joo, c.wolcha]; });
ok(JSON.stringify(c) === '[17,3,0]', '도장대로 맞춤 → 17일 · 주휴 3주 · 월차 0: ' + c);
await b.close(); console.log(fails ? 'SCAN FAIL ' + fails : 'SCAN OK'); process.exit(fails ? 1 : 0);
