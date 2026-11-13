// 사용법 안내서용 화면 캡처 (실제 화면에 빨간 박스·번호를 입힘)
// 사용: ADMIN_PASSWORD=guide1234 DATA_FILE=/tmp/guide.json PORT=3111 node server.js  (다른 터미널에서)
//       node tools/capture-guides.cjs <출력폴더> <지도타일.png 경로(선택)>
// 필요: playwright, 저장소의 public/vendor/xlsx.full.min.js
const { chromium, devices } = require('playwright');
const fs = require('fs'), path = require('path');
const OUT = process.argv[2] || './shots', TILE = process.argv[3];
const B = 'http://localhost:3111', PW = 'guide1234', A = { 'x-admin': PW, 'content-type': 'application/json' };
fs.mkdirSync(OUT, { recursive: true });
const api = async (u, o = {}) => { const r = await fetch(B + u, { ...o, headers: { ...A, ...(o.headers || {}) } }); return r.json(); };
// 저장소가 ESM(type: module)이라 UMD 파일은 직접 require 할 수 없어 함수로 감싸 불러옴
const XLSX = (() => { const mod = { exports: {} }; new Function('module', 'exports', 'require', fs.readFileSync(path.join(__dirname, '../public/vendor/xlsx.full.min.js'), 'utf8'))(mod, mod.exports, require); return mod.exports; })();

// ── 빨간 박스 + 번호 ──
async function mark(page, items) {
  for (const it of items) {
    const h = await it.loc.first().elementHandle(); if (!h) throw new Error('mark target not found: ' + it.n);
    await h.scrollIntoViewIfNeeded().catch(() => {});
  }
  for (const it of items) {
    const h = await it.loc.first().elementHandle();
    await page.evaluate(([el, n, pad]) => {
      const fixed = (() => { for (let e = el; e; e = e.parentElement) if (getComputedStyle(e).position === 'fixed') return true; return false; })();
      const r = el.getBoundingClientRect(), sx = fixed ? 0 : scrollX, sy = fixed ? 0 : scrollY, pos = fixed ? 'fixed' : 'absolute';
      const d = document.createElement('div'); d.className = '__mark';
      d.style.cssText = `position:${pos};left:${r.left + sx - pad}px;top:${r.top + sy - pad}px;width:${r.width + pad * 2}px;height:${r.height + pad * 2}px;border:4px solid #e01b1b;border-radius:10px;pointer-events:none;z-index:99998;box-sizing:border-box`;
      document.body.appendChild(d);
      const b = document.createElement('div'); b.className = '__mark'; b.textContent = n;
      b.style.cssText = `position:${pos};left:${Math.max(2, r.left + sx - pad - 6)}px;top:${Math.max(2, r.top + sy - pad - 16)}px;width:30px;height:30px;border-radius:50%;background:#e01b1b;color:#fff;font:700 17px/30px system-ui,sans-serif;text-align:center;z-index:99999`;
      document.body.appendChild(b);
    }, [h, it.n, it.pad ?? 5]);
  }
}
async function snap(page, name, items = [], opt = {}) {
  await page.waitForTimeout(opt.wait ?? 250);
  if (items.length) await mark(page, items);
  let clip = opt.clip;
  if (opt.clipSel) clip = await page.evaluate((sel) => { const r = document.querySelector(sel).getBoundingClientRect(); return { x: Math.max(0, r.left + scrollX - 8), y: Math.max(0, r.top + scrollY - 8), width: r.width + 16, height: r.height + 16 }; }, opt.clipSel);
  if (opt.fit) clip = await page.evaluate(([x0, x1, pt, pb]) => {
    const ms = [...document.querySelectorAll('.__mark')]; let t = 1e9, b = 0;
    ms.forEach((m) => { const r = m.getBoundingClientRect(); t = Math.min(t, r.top + scrollY); b = Math.max(b, r.bottom + scrollY); });
    const y = Math.max(0, t - pt), h = Math.min(document.documentElement.scrollHeight, b + pb) - y;
    return { x: x0, y, width: x1 - x0, height: h };
  }, [opt.x0 ?? 230, 1366, opt.padT ?? 60, opt.padB ?? 30]);
  await page.screenshot({ path: path.join(OUT, name + '.png'), clip, fullPage: !!(opt.full || opt.fit) });
  await page.evaluate(() => document.querySelectorAll('.__mark').forEach((e) => e.remove()));
  console.log('  ✓', name);
}

(async () => {
  const kst = new Date(Date.now() + 9 * 3600e3), today = kst.toISOString().slice(0, 10), wd = kst.getUTCDay();
  const addMin = (m) => new Date(Date.now() + 9 * 3600e3 + m * 60e3).toISOString().slice(11, 16);
  // ── 예시 데이터 (모두 가상) ──
  const names = [['김영자', '이순희', '박정남', '최말순'], ['정복동', '한옥분', '오금순', '윤두식'], ['강봉수', '조말례', '서판석', '임정자'], ['문옥례', '배갑순', '신만복', '유정애'], ['권순이', '황금자', '안병철', '송미자'], ['홍길순', '노연옥', '장팔복', '고순덕']];
  const gnames = ['판소리보존회', '강릉단오제보존회', '전통목공회', '한지장보존회', '자수공방', '옹기장보존회'];
  const groups = gnames.map((n, i) => ({ name: n, pin: String(1111 * (i + 1)), members: names[i].map((m, k) => `${m} ${String(1000 + i * 111 + k * 7).padStart(4, '0')}`) }));
  await api('/api/admin/config', { method: 'PUT', body: JSON.stringify({ groups, venue: { name: '전통문화회관', lat: 37.5665, lng: 126.978, radius: 150 }, settings: { useCode: false, openBefore: 30, lateAfter: 10, closeAfter: 30 }, lectures: [] }) });
  let cfg = await api('/api/admin/config'); const G = Object.fromEntries(cfg.groups.map((g) => [g.name, g.id]));
  const lec = [
    { name: '판소리 기초', weekday: wd, start: addMin(-3), end: addMin(87), targets: [G['판소리보존회'], G['강릉단오제보존회']] },
    { name: '전통 목공 실습', weekday: wd, start: addMin(8), end: addMin(98), targets: [G['전통목공회'], G['한지장보존회']] },
    { name: '자수 입문', weekday: (wd + 2) % 7, start: '10:00', end: '11:30', targets: [G['자수공방']] },
    { name: '옹기 빚기', weekday: (wd + 4) % 7, start: '14:00', end: '16:00', targets: [], off: [] },
  ];
  await api('/api/admin/config', { method: 'PUT', body: JSON.stringify({ lectures: lec }) });
  cfg = await api('/api/admin/config'); const L = Object.fromEntries(cfg.lectures.map((l) => [l.name, l]));
  const links = await api('/api/admin/links');
  // 출석 기록 (판소리 기초: 판소리 4명 중 3출석·1지각 / 강릉 4명 중 3출석·1결석)
  const mem = Object.fromEntries(links.flatMap((g) => g.members.map((m) => [m.name, m])));
  const mk = (arr) => Object.fromEntries(arr.map(([n, s]) => [mem[n].id, s]));
  await api('/api/admin/manual', { method: 'PUT', body: JSON.stringify({ date: today, lectureId: L['판소리 기초'].id, marks: mk([['김영자', 'P'], ['이순희', 'P'], ['박정남', 'L'], ['최말순', 'P'], ['정복동', 'P'], ['한옥분', 'P'], ['오금순', 'A'], ['윤두식', 'P']]), notes: { [mem['박정남'].id]: '버스 지연' } }) });
  // 과거 기록 몇 건 (통계용)
  for (let d = 1; d <= 3; d++) {
    const ds = new Date(Date.now() + 9 * 3600e3 - d * 7 * 86400e3).toISOString().slice(0, 10);
    const lv = { ...L['판소리 기초'] }; await api('/api/admin/session', { method: 'POST', body: JSON.stringify({ date: ds, lectureId: lv.id }) });
    await api('/api/admin/manual', { method: 'PUT', body: JSON.stringify({ date: ds, lectureId: lv.id, marks: mk([['김영자', 'P'], ['이순희', d === 2 ? 'A' : 'P'], ['박정남', 'P'], ['최말순', d === 1 ? 'L' : 'P'], ['정복동', 'P'], ['한옥분', d === 3 ? 'A' : 'P'], ['오금순', d > 1 ? 'A' : 'P'], ['윤두식', 'P']]) }) });
  }
  const ses1 = await api('/api/admin/session', { method: 'POST', body: JSON.stringify({ date: today, lectureId: L['판소리 기초'].id }) });
  const url1 = '/c/' + L['판소리 기초'].token, url2 = '/c/' + L['전통 목공 실습'].token, urlOther = '/c/' + L['자수 입문'].token;

  const br = await chromium.launch();
  const route = async (ctx) => { if (TILE) await ctx.route('**/tile.openstreetmap.org/**', (r) => r.fulfill({ path: TILE, contentType: 'image/png' })); };
  const phone = async (geo = { latitude: 37.5666, longitude: 126.9781, accuracy: 15 }, extra = {}) => {
    const c = await br.newContext({ ...devices['iPhone 13'], geolocation: geo, permissions: ['geolocation'], ...extra }); return [c, await c.newPage()];
  };

  // ════════ 출석자 화면 ════════
  console.log('출석자 화면');
  const [c1, p1] = await phone();
  await p1.goto(B + url1); await p1.waitForSelector('[data-gid]'); await p1.waitForFunction(() => document.body.innerText.includes('위치 확인됨'));
  await snap(p1, 'a1_first', [{ loc: p1.locator('.card').first(), n: 1 }, { loc: p1.locator('.box').first(), n: 2 }], { full: true , clipSel: 'main' });
  await p1.click('[data-gid]:has-text("판소리보존회")'); await p1.click('.mem:has-text("김영자")'); await p1.fill('#ph', '1000');
  await snap(p1, 'a2_filled', [{ loc: p1.locator('[data-gid].sel'), n: 1 }, { loc: p1.locator('.mem.sel'), n: 2 }, { loc: p1.locator('#ph'), n: 3 }, { loc: p1.locator('#go'), n: 4 }], { full: true , clipSel: '#body' });
  await p1.click('#go'); await p1.waitForSelector('.big');
  await snap(p1, 'a3_done', [{ loc: p1.locator('.big'), n: 1 }, { loc: p1.locator('.card.mut'), n: 2 }], { full: true , clipSel: 'main' });
  // 다음부터: 다른 강의 링크 → 이름 선택 없이 자동 출석
  await p1.goto(B + url2); await p1.waitForSelector('.big', { timeout: 15000 });
  await snap(p1, 'a4_auto', [{ loc: p1.locator('.big'), n: 1 }], { full: true , clipSel: 'main' });
  await p1.goto(B + url1); await p1.waitForSelector('.who');
  await snap(p1, 'a4b_recognized', [{ loc: p1.locator('.who').first().locator('xpath=ancestor::div[contains(@class,"card")]'), n: 1 }, { loc: p1.locator('#go'), n: 2 }], { full: true , clipSel: 'main' });
  // 위치 권한 꺼짐
  const c5 = await br.newContext({ ...devices['iPhone 13'] }); await c5.addInitScript(() => { navigator.geolocation.getCurrentPosition = (ok, no) => setTimeout(() => no({ code: 1 }), 50); });
  const p5 = await c5.newPage(); await p5.goto(B + url1); await p5.waitForSelector('#retry');
  await snap(p5, 'a5_denied', [{ loc: p5.locator('.box').first(), n: 1 }, { loc: p5.locator('#retry'), n: 2 }], { full: true , clipSel: '#body' });
  // 교육장에서 멀리
  const [c6, p6] = await phone({ latitude: 37.60, longitude: 126.98, accuracy: 15 });
  await p6.goto(B + url1); await p6.waitForSelector('[data-gid]'); await p6.click('[data-gid]:has-text("강릉단오제보존회")'); await p6.click('.mem:has-text("정복동")'); await p6.fill('#ph', '1111'); await p6.click('#go'); await p6.waitForSelector('.box.err');
  await snap(p6, 'a6_far', [{ loc: p6.locator('.box.err'), n: 1 }], { full: true , clipSel: '#body' });
  // 이미 등록된 이름을 다른 휴대폰에서
  const [c7, p7] = await phone(); await p7.goto(B + url1); await p7.waitForSelector('[data-gid]'); await p7.waitForFunction(() => document.body.innerText.includes('위치 확인됨'));
  await p7.click('[data-gid]:has-text("판소리보존회")'); await p7.click('.mem:has-text("김영자")'); await p7.fill('#ph', '1000'); await p7.click('#go'); await p7.waitForSelector('#req');
  await snap(p7, 'a7_other_phone', [{ loc: p7.locator('.box.err'), n: 1 }, { loc: p7.locator('#req'), n: 2 }], { full: true , clipSel: '#body' });
  await p7.click('#req'); await p7.waitForSelector('.big'); // 관리자 승인 화면용 요청 생성
  // 오늘은 강의가 없는 날
  const [c8, p8] = await phone(); await p8.goto(B + urlOther); await p8.waitForSelector('.who');
  await snap(p8, 'a8_notToday', [{ loc: p8.locator('.card').first(), n: 1 }], { full: true , clipSel: 'main' });

  // ════════ 관리자 PC ════════
  console.log('관리자 PC');
  const pc = await br.newContext({ viewport: { width: 1366, height: 860 }, geolocation: { latitude: 37.5665, longitude: 126.978 }, permissions: ['geolocation'], acceptDownloads: true }); await route(pc);
  const p = await pc.newPage(); p.on('dialog', (d) => d.accept());
  await p.goto(B + '/admin');
  await snap(p, 'p0_login', [{ loc: p.locator('#pw'), n: 1 }, { loc: p.locator('#go'), n: 2 }], { clip: { x: 380, y: 100, width: 600, height: 330 } });
  await p.fill('#pw', PW); await p.click('#go'); await p.waitForSelector('.only-pc table');
  await snap(p, 'p1_overview', [{ loc: p.locator('nav [data-tab=ov]'), n: 1 }, { loc: p.locator('#date'), n: 2 }, { loc: p.locator('.only-pc table tr').nth(1).locator('td').nth(1), n: 3 }, { loc: p.locator('.only-pc [data-shl]').first(), n: 4 }, { loc: p.locator('.only-pc [data-tg]').first(), n: 5 }], { fit: true, x0: 0, padT: 40 });
  await p.screenshot({ path: path.join(OUT, 'p1b_nav.png'), clip: { x: 0, y: 0, width: 240, height: 340 } });
  // 설정
  await p.click('nav [data-tab=set]'); await p.waitForSelector('#vmap.leaflet-container'); await p.waitForTimeout(900);
  await snap(p, 'p2_settings', [{ loc: p.locator('#sob').locator('xpath=..'), n: 1 }, { loc: p.locator('#suc').locator('xpath=ancestor::label'), n: 2 }, { loc: p.locator('#vsearch'), n: 3 }, { loc: p.locator('#here'), n: 4 }, { loc: p.locator('#vmap'), n: 5 }, { loc: p.locator('#vr'), n: 6 }], { fit: true });
  // 명부 가져오기: 예시 파일 업로드 → 미리보기
  const rows = [['단체', '이름', '전화번호'], ['판소리보존회', '홍길동', '010-1234-5678'], ['판소리보존회', '김영자', '1000'], ['전통목공회', '이순희', '010-2222-3333'], ['전통목공회', '박정남', ''], ['신규공방', '최말순', '010-4444-0123']];
  const wb = XLSX.utils.book_new(); XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(rows), '명부');
  const xl = path.join(OUT, 'sample_roster.xlsx'); fs.writeFileSync(xl, XLSX.write(wb, { type: 'buffer', bookType: 'xlsx' }));
  await p.setInputFiles('#xf', xl); await p.waitForSelector('#xapply'); await p.locator('#xf').scrollIntoViewIfNeeded();
  await p.evaluate(() => document.getElementById('xf').closest('.card').scrollIntoView({ block: 'start' }));
  await snap(p, 'p3_roster', [{ loc: p.locator('#xf'), n: 1 }, { loc: p.locator('#xtpl'), n: 2 }, { loc: p.locator('#xprev table'), n: 3 }, { loc: p.locator('#xapply'), n: 4 }], { fit: true });
  await p.evaluate(() => { document.getElementById('xprev').innerHTML = ''; });
  // 단체·구성원
  await p.evaluate(() => document.getElementById('gs').closest('.card').scrollIntoView({ block: 'start' }));
  await snap(p, 'p4_groups', [{ loc: p.locator('#gs input[data-k=pin]').first(), n: 1 }, { loc: p.locator('#gs textarea').first(), n: 2 }, { loc: p.locator('#gs [data-del]').first(), n: 3 }], { fit: true });
  // 개인 링크
  await p.evaluate(() => document.getElementById('links').closest('.card').scrollIntoView({ block: 'start' }));
  await p.waitForSelector('#links .lk');
  await snap(p, 'p5_links', [{ loc: p.locator('#lkall'), n: 1 }, { loc: p.locator('#links .lk [data-lks]').first(), n: 2 }, { loc: p.locator('#links .lk [data-lkm]').first(), n: 3 }, { loc: p.locator('#links .lk [data-lkr]').first(), n: 4 }], { fit: true });
  // 강의 개설 폼
  await p.click('nav [data-tab=lec]'); await p.waitForSelector('#nl-name');
  await p.fill('#nl-name', '한지 공예'); await p.click('#nl-days label:nth-child(3)'); await p.click('#nl-tg label:has-text("한지장보존회")');
  await p.fill('#nl-start', '10시 반'); await p.dispatchEvent('#nl-start', 'change'); await p.click('[data-dur="90"]');
  await snap(p, 'p6_lec_form', [{ loc: p.locator('#nl-name'), n: 1 }, { loc: p.locator('.seg2'), n: 2 }, { loc: p.locator('#nl-days'), n: 3 }, { loc: p.locator('#nl-tg'), n: 4 }, { loc: p.locator('#nl-start').locator('xpath=ancestor::div[contains(@class,"trow")]'), n: 5 }, { loc: p.locator('#nl-startq'), n: 6 }, { loc: p.locator('#nl-add'), n: 7 }], { fit: true });
  await p.click('#nl-add'); await p.waitForSelector('#nl-done .box2'); await p.waitForTimeout(600);
  await p.evaluate(() => document.getElementById('nl-done').scrollIntoView({ block: 'start' })); await p.evaluate(() => scrollBy(0, -40));
  await snap(p, 'p7_lec_cards', [{ loc: p.locator('#nl-done .box2'), n: 1 }, { loc: p.locator('#lecState'), n: 2 }, { loc: p.locator('.lcard').first().locator('.trow').first(), n: 3 }, { loc: p.locator('.lcard').first().locator('.days'), n: 4 }, { loc: p.locator('.lcard').first().locator('[data-offadd]'), n: 5 }, { loc: p.locator('.lcard').first().locator('[data-lsh]'), n: 6 }], { fit: true });
  // 수동 출석
  await p.click('nav [data-tab=man]'); await p.waitForFunction(() => document.querySelectorAll('#ml option').length > 0);
  await p.selectOption('#ml', L['판소리 기초'].id); await p.click('#mgo'); await p.waitForSelector('#msave');
  await p.evaluate(() => { const kids = [...document.getElementById('mout').children]; const hs = kids.filter((e) => e.querySelector && e.querySelector('[data-all]')); const i2 = kids.indexOf(hs[1]); const last = kids.length - 1; for (let i = i2; i < last; i++) kids[i].style.display = 'none'; });
  await snap(p, 'p8_manual', [{ loc: p.locator('#md'), n: 1 }, { loc: p.locator('#ml'), n: 2 }, { loc: p.locator('#mgo'), n: 3 }, { loc: p.locator('[data-all]').first(), n: 4 }, { loc: p.locator('.mrow').nth(2).locator('.seg'), n: 5 }, { loc: p.locator('#msave'), n: 6 }], { fit: true });
  // 기기 변경 요청 배너 (7번 화면에서 만든 요청)
  await p.click('nav [data-tab=ov]'); await p.waitForSelector('[data-rqa]').catch(() => {});
  await p.waitForTimeout(500);
  if (await p.locator('[data-rqa]').count()) await snap(p, 'p9_requests', [{ loc: p.locator('#ov .card').first(), n: 1 }, { loc: p.locator('[data-rqa]').first(), n: 2 }, { loc: p.locator('[data-rqd]').first(), n: 3 }], { fit: true });
  else console.log('  (기기 변경 요청 배너 없음)');
  // 통계
  await p.click('nav [data-tab=stat]'); await p.waitForSelector('#sout table');
  await p.fill('#sf', new Date(Date.now() + 9 * 3600e3 - 40 * 86400e3).toISOString().slice(0, 10)); await p.click('#sgo'); await p.waitForTimeout(500);
  await p.evaluate(() => { const t = document.querySelectorAll('#sout table')[2]; [...t.querySelectorAll('tr')].slice(7).forEach((r) => (r.style.display = 'none')); });
  await snap(p, 'p10_stats', [{ loc: p.locator('#sf').locator('xpath=ancestor::div[contains(@class,"row")]'), n: 1 }, { loc: p.locator('#csv'), n: 2 }, { loc: p.locator('#sout table').first(), n: 3 }, { loc: p.locator('#sout table').nth(2), n: 4 }], { fit: true });

  // ════════ 관리자 휴대폰 ════════
  console.log('관리자 휴대폰');
  const mc = await br.newContext({ ...devices['iPhone 13'] }); const m = await mc.newPage(); m.on('dialog', (d) => d.accept());
  await m.goto(B + '/admin'); await m.fill('#pw', PW); await m.click('#go'); await m.waitForSelector('.only-mob .lec');
  await snap(m, 'm1_overview', [{ loc: m.locator('nav'), n: 1 }, { loc: m.locator('.only-mob .lec').first().locator('[data-shl]'), n: 2 }, { loc: m.locator('.only-mob .lec').first().locator('.gc').first(), n: 3 }]);
  await m.click('nav [data-tab=lec]'); await m.waitForSelector('#nl-name');
  await m.fill('#nl-name', '옹기 빚기'); await m.click('#nl-days label:nth-child(6)'); await m.click('[data-qs="14:00"]'); await m.click('[data-dur="120"]');
  await snap(m, 'm2_lec_form', [{ loc: m.locator('#nl-name'), n: 1 }, { loc: m.locator('.seg2'), n: 2 }, { loc: m.locator('#nl-days'), n: 3 }, { loc: m.locator('#nl-startq'), n: 4 }]);
  await m.click('nav [data-tab=man]'); await m.waitForFunction(() => document.querySelectorAll('#ml option').length > 0); await m.selectOption('#ml', L['판소리 기초'].id); await m.click('#mgo'); await m.waitForSelector('#msave');
  await snap(m, 'm3_manual', [{ loc: m.locator('#ml'), n: 1 }, { loc: m.locator('.mrow').first().locator('.seg'), n: 2 }, { loc: m.locator('[data-all]').first(), n: 3 }]);
  await br.close();
  console.log('끝');
})().catch((e) => { console.error(e); process.exit(1); });
