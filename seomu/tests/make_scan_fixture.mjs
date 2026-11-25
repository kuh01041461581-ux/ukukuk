// 가짜 '도장 받은 스캔 PDF' 만들기: 앱이 만든 일일점검부·청구서(가짜 근로자) → 그림으로 바꿔 빨간 도장을 찍고 → 그림 PDF
//   PW_PATH=... node seomu/tests/make_scan_fixture.mjs   → seomu/tests/fixtures/scan_signed.pdf
import { createRequire } from 'module'; import path from 'path'; import fs from 'fs'; import { execFileSync } from 'child_process'; import { fileURLToPath } from 'url';
const require = createRequire(import.meta.url); const { chromium } = require(process.env.PW_PATH || 'playwright');
const here = path.dirname(fileURLToPath(import.meta.url)), fx = n => path.join(here, 'fixtures', n);
const tmp = fs.mkdtempSync(path.join(process.env.TMPDIR || '/tmp', 'scanfx-'));
const b = await chromium.launch(); const pg = await b.newPage();
await pg.goto('file://' + path.resolve(here, '../dist/서무종합프로그램.html') + '#wage');
const fr = await (await pg.locator('#frame-wage').elementHandle()).contentFrame();
await fr.waitForSelector('#f1', { state: 'attached', timeout: 60000 }); await fr.fill('#ym', '2026-10');
await fr.setInputFiles('#f1', fx('roster.xlsx')); await fr.waitForSelector('#rosterT tbody tr');
const doc = await fr.evaluate(() => {
  const ws = __wage.state().workers.slice(0, 2), css = document.getElementById('wage-css').outerHTML;
  const html = ws.map(w => __wage.dailyHtml(w)).join('') + ws.map(w => __wage.claimHtml(w)).join('');
  return { html: '<!doctype html><meta charset="utf-8">' + css + '<style>body{margin:0}.paper{margin:0}</style>' + html, worked: ws.map(w => __wage.calc(w).worked) };
});
const p2 = await b.newPage(); await p2.setContent(doc.html); await p2.pdf({ path: path.join(tmp, 'clean.pdf'), format: 'A4', printBackground: true });
await b.close();
// 홍평일은 10/14 도장을 일부러 뺀다(도장 없는 날을 찾아내는지 시험)
doc.worked[0] = doc.worked[0].filter(d => d !== '2026-10-14');
fs.writeFileSync(path.join(tmp, 'worked.json'), JSON.stringify(doc.worked));
execFileSync('python3', ['-I', path.join(here, 'stamp_fixture.py'), tmp, fx('scan_signed.pdf')], { stdio: 'inherit' });
console.log('ok', fx('scan_signed.pdf'));
