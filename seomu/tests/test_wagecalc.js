// node seomu/tests/test_wagecalc.js — 기간제 인부임 계산·고지서 매칭 시험 (가짜 데이터, 숫자는 2026.10 지급명세서와 같게 맞춤)
// XLSX 는 npm 'xlsx@0.18.5' (NODE_PATH 로 지정)
const assert = require('assert'), fs = require('fs'), path = require('path'), vm = require('vm');
const XLSX = require('xlsx');
const here = __dirname;
global.SEOMU_TAXTABLE = vm.runInNewContext(fs.readFileSync(path.join(here, '../shared/taxtable.js'), 'utf8') + ';SEOMU_TAXTABLE');
global.SEOMU_HOLIDAYS = vm.runInNewContext(fs.readFileSync(path.join(here, '../shared/holidays.js'), 'utf8') + ';SEOMU_HOLIDAYS');
const W = require('../shared/wagecalc.js');
const P = require('../shared/personname.js');

const S = { ym: '2026-10', hourlyByYear: { 2026: 10740 }, hoursPerDay: 7, closedDow: [1], excludeHoliday: false };
const sheetRows = (wb, n) => XLSX.utils.sheet_to_json(wb.Sheets[n], { header: 1, defval: '' });

// 1) 명부 읽기: 과 이름 줄은 걸러지고 주민번호 뒷자리는 남지 않는다
const rwb = XLSX.readFile(path.join(here, 'fixtures/roster.xlsx'));
const roster = W.parseRoster(sheetRows(rwb, rwb.SheetNames[0]), v => P.seomuExtractPerson(v).name);
assert.deepStrictEqual(roster.workers.map(w => w.name), ['홍평일', '김주말', '이중복']);
assert.deepStrictEqual(roster.skipped, ['문화유산과']);
const [a, b, c] = roster.workers;
assert.strictEqual(a.birth6, '500910'); assert.strictEqual(a.birth8, '1950-09-10');
assert.strictEqual(b.birth6, '990916'); assert.strictEqual(b.birth8, '1999-09-16');   // 엑셀이 숫자로 저장해도 읽음
assert.strictEqual(a.kind, 'weekday'); assert.strictEqual(b.kind, 'weekend');
assert.strictEqual(a.bank, '전북은행'); assert.strictEqual(a.account, '1021-01-0000001');
assert.ok(!JSON.stringify(roster.workers).includes('1234567'), '주민번호 뒷자리가 남아 있음');

// 2) 고지서 읽기·매칭
const iwb = XLSX.readFile(path.join(here, 'fixtures/insurance.xlsx'));
const sheets = {}; iwb.SheetNames.forEach(n => sheets[n] = sheetRows(iwb, n));
const parsed = W.parseInsurance(sheets);
assert.deepStrictEqual(Object.keys(parsed.found).sort(), ['accident', 'employ', 'health', 'pension']);
assert.strictEqual(parsed.ym, '2026-10');
a.ins = W.matchInsurance(a, parsed); b.ins = W.matchInsurance(b, parsed); c.ins = W.matchInsurance(c, parsed);
assert.deepStrictEqual([a.ins.health, a.ins.pension, a.ins.employ, a.ins.accident], [[65070, 65070], [0, 0], [0, 13600], 14120]);        // 최병학 칸과 같은 값
assert.deepStrictEqual([b.ins.health, b.ins.pension, b.ins.employ, b.ins.accident], [[24400, 24400], [28500, 28500], [5400, 10500], 5290]); // 김향은 칸과 같은 값
assert.strictEqual(b.ins.status, 'ok');                       // 이름만 같은 다른 사람(880101)은 섞이지 않음
assert.deepStrictEqual(c.ins.health, [15000, 15000]);         // 같은 사람 두 줄은 합산
assert.ok(c.ins.dup);
assert.strictEqual(W.matchInsurance({ name: '없는사람', birth6: '700101' }, parsed).found, false);
assert.strictEqual(W.matchInsurance({ name: '김주말', birth6: '770707' }, parsed).status, 'birthdiff');
assert.strictEqual(a.hire, ''); a.hire = a.ins.hire; assert.strictEqual(a.hire, '2026-01-06');

// 3) 계산 — 2026.10 샘플: 평일(화~금) 18일 + 주휴 4주 + 월차 1, 주말 9일
// 소득세는 간이세액표로 자동 산정: 월급여 1,653,940원 · 가족 1명 → 12,020원, 지방소득세 1,200원 (사용자 샘플과 같음)
const st0 = W.simpleTax(1653940, 1); assert.strictEqual(st0.tax, 12020); assert.deepStrictEqual(st0.row, [1650, 1660]);
assert.strictEqual(W.simpleTax(676620, 1).tax, 0);                 // 주말 근로자: 소득세 0
assert.strictEqual(W.simpleTax(1653940, 2).tax, 7520);             // 가족 2명 칸
assert.strictEqual(W.simpleTax(500000, 1).tax, 0);                 // 770천원 미만
assert.strictEqual(W.simpleTax(1650000, 1).tax, 12020); assert.strictEqual(W.simpleTax(1649999, 1).tax, 11810);   // 구간 경계
assert.strictEqual(W.simpleTax(10000000, 1).known, false);         // 표 밖 → 직접 입력 안내
assert.strictEqual(W.calcWorker(a, S).ded.incomeTax, 12020);       // 입력 없이도 자동
assert.strictEqual(W.calcWorker(Object.assign({}, a, { kids: 1 }), S).ded.incomeTax, W.simpleTax(1653940, 2).tax);   // 8~20세 자녀 1명 = 가족 +1
a.tax = 12020;
const ca = W.calcWorker(a, S), cb = W.calcWorker(b, S);
assert.strictEqual(ca.dayRate, 75180);
assert.strictEqual(ca.days, 18); assert.strictEqual(ca.joo, 4); assert.strictEqual(ca.wolcha, 1);
assert.strictEqual(ca.juUnit, 60140);
assert.strictEqual(ca.basePay, 1353240); assert.strictEqual(ca.juPay, 240560); assert.strictEqual(ca.wolchaPay, 60140);
assert.strictEqual(ca.gross, 1653940);
assert.strictEqual(ca.ded.localTax, 1200);
assert.strictEqual(ca.net, 1575650);
assert.strictEqual(ca.erTotal, 65070 + 13600 + 14120 + 0);
assert.strictEqual(cb.days, 9); assert.strictEqual(cb.joo, 0); assert.strictEqual(cb.wolcha, 0);
assert.strictEqual(cb.gross, 676620); assert.strictEqual(cb.net, 618320);
assert.strictEqual(cb.erTotal, 24400 + 28500 + 10500 + 5290);
assert.deepStrictEqual(ca.worked.slice(0, 3), ['2026-10-01', '2026-10-02', '2026-10-06']);   // 10/5(월)은 휴관
assert.deepStrictEqual(cb.worked, ['2026-10-03', '2026-10-04', '2026-10-10', '2026-10-11', '2026-10-17', '2026-10-18', '2026-10-24', '2026-10-25', '2026-10-31']);
const two = W.calcAll([a, b], S);
assert.strictEqual(two.totals.gross, 2330560); assert.strictEqual(two.totals.er, 161480); assert.strictEqual(two.totals.total, 2492040);

// 4) 규칙 바꿔 보기
const half = Object.assign({}, a, { off: ['2026-10-14'] });                // 하루 빠지면 그 주 주휴·월차 없음
const ch = W.calcWorker(half, S); assert.strictEqual(ch.days, 17); assert.strictEqual(ch.joo, 3); assert.strictEqual(ch.wolcha, 0);
const old = Object.assign({}, a, { hire: '2025-01-01' });                   // 근속 1년 넘으면 월차 없음
assert.strictEqual(W.calcWorker(old, S).wolcha, 0);
const holi = W.calcWorker(a, Object.assign({}, S, { excludeHoliday: true })); // 공휴일(10/9 한글날) 제외
assert.strictEqual(holi.days, 17);
assert.strictEqual(W.calcWorker(a, Object.assign({}, S, { closedDow: [] })).days, 22); // 휴관 없음 → 평일 월~금 22일 (10/5 월 포함)
assert.strictEqual(W.calcWorker(a, Object.assign({}, S, { hourlyByYear: { 2026: 10000 } })).dayRate, 70000);
assert.strictEqual(W.calcWorker(Object.assign({}, a, { extraHours: 2 }), S).extraPay, 21480);

// 5) 납부서 글자 읽기
const txt = ' 세입세출외현금납부서 금12,020원(금일만이천이십원) 정리구분 보관금-소득세 건 명 2026년 구춘포역사 기간제근로자 10월분 인부임(홍평일:1,653,940원) '
  + ' 세입세출외현금납부서 금1,200원(금일천이백원) 정리구분 보관금-지방소득세 건 명 인부임(홍평일:1,653,940원)';
assert.deepStrictEqual(W.parsePayNotice(txt), [{ kind: 'income', amount: 12020, name: '홍평일', gross: 1653940 }, { kind: 'local', amount: 1200, name: '홍평일', gross: 1653940 }]);
assert.deepStrictEqual(W.parseRrn('5009101234567'), { ok: true, birth6: '500910', birth8: '1950-09-10', y: 1950, m: 9, d: 10, hadBack: true });
assert.strictEqual(W.parseRrn('0501013123456').birth8, '2005-01-01');
assert.strictEqual(W.parseRrn(50101).birth6, '050101');
assert.strictEqual(W.parseRrn('1950-09-10').birth6, '500910');
// 스캔 읽기 규칙(글자 부분)
const SR = require('../shared/scanread.js');
assert.strictEqual(SR.fixBank('카카오뱀크'), '카카오뱅크'); assert.strictEqual(SR.fixBank('전북은헹'), '전북은행');
assert.strictEqual(SR.bestName('죄병학', ['최병학', '김향은']), '최병학'); assert.strictEqual(SR.bestName('ㅣ', ['최병학']), null);
const cl = SR.parseClaim('청 구 서\n 1,653,940 1021-01-6139189 7\n총 1,653,940원\n기본급 1,353,240원\n10.1. ~ 10.31.(평얄   18   75,180원\n(주휴수당 240,560원)\n(월차수당 60,140원)\n2026. 10.');
assert.deepStrictEqual([cl.isClaim, cl.gross, cl.account, cl.days, cl.dayRate, cl.base, cl.ju, cl.wolcha, cl.kind, cl.ym], [true, 1653940, '1021-01-6139189', 18, 75180, 1353240, 240560, 60140, 'weekday', '2026-10']);
assert.deepStrictEqual(SR.parseDailyTitle('익산 구 춘포역사 주말\n기간제근로자 일일점검부\n(2026년 10월)'), { kind: 'weekend', ym: '2026-10', isDaily: true });
assert.strictEqual(SR.monthGrid('2026-10')[0][4], '2026-10-01');      // 일요일 시작: 10/1(목) = 다섯째 칸
assert.strictEqual(SR.monthGrid('2026-10', 1)[0][3], '2026-10-01');   // 월요일 시작(예전 양식): 넷째 칸
assert.strictEqual(SR.kindOfDates(['2026-10-03', '2026-10-04']), 'weekend');
// 휴관 날짜: 근무일에서 빠지지만 결근이 아니라 주휴·월차는 그대로
const cl0 = W.calcWorker(Object.assign({}, a, { tax: null }), Object.assign({}, S, { closedDates: ['2026-10-14'] }));
assert.deepStrictEqual([cl0.days, cl0.joo, cl0.wolcha], [17, 4, 1]);
// 직접 수정: 단가·주휴수당·지방소득세를 고치면 합계가 따라가고 무엇을 고쳤는지 남는다
const ed = W.calcWorker(Object.assign({}, a, { tax: null, ovr: { dayRate: 80000, juPay: 200000, localTax: 1000 } }), S);
assert.strictEqual(ed.basePay, 18 * 80000); assert.strictEqual(ed.juPay, 200000); assert.strictEqual(ed.gross, 18 * 80000 + 200000 + 60140);
assert.strictEqual(ed.ded.localTax, 1000); assert.deepStrictEqual(ed.edited.sort(), ['dayRate', 'juPay', 'localTax']);
assert.strictEqual(ed.ded.incomeTax, W.simpleTax(ed.gross, 1).tax);   // 소득세는 바뀐 산출액으로 다시 자동 산정
console.log('wagecalc OK');
