/* ============================================================
   기간제 인부임 계산 (화면 없이 계산만 — Node 에서도 시험한다)
   규칙은 사용자가 준 2026.10 지급명세서에서 그대로 읽어 냈다.
     일급        = 최저시급 × 1일 소정근로시간(7)                     75,180 = 10,740 × 7
     주휴수당    = 주 소정근로시간/40 × 8 × 시급 (10원 미만 절사)       28h/주 → 60,140
                   · 주 15시간 이상일 때만, 그 주의 소정근로일이 모두 이 달 안에 있고 개근한 주만 센다
     월차수당    = 주휴 대상자가 한 달 소정근로일을 모두 채우면 1일(단가는 주휴와 같음), 근속 1년 미만일 때만
     지방소득세  = 소득세의 10% (10원 미만 절사)                       12,020 → 1,200
   주 15시간 미만(예: 주말 이틀 14h)은 주휴·월차 없음.
   ============================================================ */
var WAGE = (function () {
  'use strict';
  var P2 = function (n) { return String(n).padStart(2, '0'); };
  var floor10 = function (n) { return Math.floor(n / 10) * 10; };
  var DOW_KO = ['일', '월', '화', '수', '목', '금', '토'];
  var holidays = (typeof SEOMU_HOLIDAYS !== 'undefined') ? SEOMU_HOLIDAYS : {};

  function parseYm(ym) { var m = /^(\d{4})-(\d{1,2})$/.exec(String(ym || '')); return m ? { y: +m[1], m: +m[2] } : null; }
  function iso(y, m, d) { return y + '-' + P2(m) + '-' + P2(d); }
  function isoOf(dt) { return iso(dt.getFullYear(), dt.getMonth() + 1, dt.getDate()); }
  function daysInMonth(y, m) { return new Date(y, m, 0).getDate(); }
  function monthDates(ym) {
    var p = parseYm(ym), out = [];
    for (var d = 1; d <= daysInMonth(p.y, p.m); d++) out.push({ iso: iso(p.y, p.m, d), d: d, dow: new Date(p.y, p.m - 1, d).getDay() });
    return out;
  }
  function holidayName(isoStr) { return holidays[isoStr] || ''; }

  /* ---------- 주민번호 / 생년월일 ----------
     뒷자리는 읽자마자 버린다: 앞 6자리와 성별 숫자 1개(세기 판단용)만 남긴다. */
  function parseRrn(raw) {
    var s = String(raw == null ? '' : raw).replace(/[^0-9]/g, '');
    var res = { ok: false, birth6: '', birth8: '', y: 0, m: 0, d: 0, hadBack: false };
    if (!s) return res;
    var yy, mm, dd, cent = 0;
    if (s.length >= 12) {                       // 주민번호 전체 (엑셀이 앞 0을 지운 12자리 포함)
      s = s.padStart(13, '0');
      var g = +s[6];
      cent = (g === 1 || g === 2 || g === 5 || g === 6) ? 1900 : (g === 3 || g === 4 || g === 7 || g === 8) ? 2000 : (g === 9 || g === 0) ? 1800 : 0;
      yy = +s.slice(0, 2); mm = +s.slice(2, 4); dd = +s.slice(4, 6); res.hadBack = true;
    } else if (s.length === 7) {                // 앞6 + 성별
      var g2 = +s[6]; cent = (g2 === 1 || g2 === 2 || g2 === 5 || g2 === 6) ? 1900 : (g2 === 3 || g2 === 4 || g2 === 7 || g2 === 8) ? 2000 : 1800;
      yy = +s.slice(0, 2); mm = +s.slice(2, 4); dd = +s.slice(4, 6); res.hadBack = true;
    } else if (s.length === 8) {                // YYYYMMDD
      yy = +s.slice(2, 4); cent = +s.slice(0, 2) * 100; mm = +s.slice(4, 6); dd = +s.slice(6, 8);
    } else if (s.length === 6 || s.length === 5) { // yymmdd (엑셀이 앞 0을 지운 5자리 포함)
      s = s.padStart(6, '0'); yy = +s.slice(0, 2); mm = +s.slice(2, 4); dd = +s.slice(4, 6);
      cent = yy > (new Date().getFullYear() % 100) ? 1900 : 2000;
    } else return res;
    if (!(mm >= 1 && mm <= 12 && dd >= 1 && dd <= 31)) return res;
    res.ok = true; res.y = cent + yy; res.m = mm; res.d = dd;
    res.birth6 = P2(yy) + P2(mm) + P2(dd); res.birth8 = res.y + '-' + P2(mm) + '-' + P2(dd);
    return res;
  }
  /* 고지서의 생년월일 칸(990916, 19990916, 1999-09-16, 엑셀 날짜) → 'yymmdd' */
  function birth6Of(v) {
    if (v instanceof Date) return P2(v.getFullYear() % 100) + P2(v.getMonth() + 1) + P2(v.getDate());
    var s = String(v == null ? '' : v).replace(/[^0-9]/g, '');
    if (s.length === 8) return s.slice(2);
    if (s.length === 5) return '0' + s;
    if (s.length === 6) return s;
    return '';
  }
  function dateOnly(v) {   // 취득일자·고용일 → 'YYYY-MM-DD'
    if (v instanceof Date) return isoOf(v);
    var s = String(v == null ? '' : v).replace(/[^0-9]/g, '');
    if (s.length === 8 && s !== '99991231') return s.slice(0, 4) + '-' + s.slice(4, 6) + '-' + s.slice(6, 8);
    return '';
  }

  /* ---------- 근무일 ---------- */
  function scheduledDow(w, S) {
    var closed = S.closedDow || [];
    var base = w.kind === 'weekend' ? [0, 6] : w.kind === 'custom' ? (w.dow || []) : [1, 2, 3, 4, 5];
    return base.filter(function (x) { return closed.indexOf(x) < 0; });
  }
  function isOffHoliday(isoStr, S) { return !!S.excludeHoliday && !!holidayName(isoStr); }

  function workDates(w, S) {
    var dows = scheduledDow(w, S), off = w.off || [], on = w.on || [], out = [];
    var start = (w.hire && String(w.hire).slice(0, 7) === S.ym) ? w.hire : '';   // 이 달에 입사했으면 그 전 날짜는 근무일이 아님
    monthDates(S.ym).forEach(function (d) {
      var worked = dows.indexOf(d.dow) >= 0 && !isOffHoliday(d.iso, S) && (!start || d.iso >= start);
      if (off.indexOf(d.iso) >= 0) worked = false;
      if (on.indexOf(d.iso) >= 0) worked = true;
      if (worked) out.push(d.iso);
    });
    return out;
  }
  /* 이번 달의 소정근로일(개근 판단 기준) */
  function scheduledDatesInMonth(w, S) {
    var dows = scheduledDow(w, S);
    return monthDates(S.ym).filter(function (d) { return dows.indexOf(d.dow) >= 0 && !isOffHoliday(d.iso, S); }).map(function (d) { return d.iso; });
  }
  /* 주휴수당 받는 주 수: 그 주의 소정근로일이 전부 이 달 안에 있고 전부 근무한 주 */
  function countJoo(w, S, worked) {
    var dows = scheduledDow(w, S); if (!dows.length) return 0;
    var p = parseYm(S.ym), first = new Date(p.y, p.m - 1, 1), last = new Date(p.y, p.m - 1, daysInMonth(p.y, p.m));
    var ws = new Date(first); ws.setDate(ws.getDate() - ((ws.getDay() + 6) % 7));   // 그 주 월요일
    var ym = S.ym, n = 0, workedSet = {}; worked.forEach(function (x) { workedSet[x] = 1; });
    for (; ws <= last; ws.setDate(ws.getDate() + 7)) {
      var sched = [];
      for (var k = 0; k < 7; k++) {
        var d = new Date(ws.getFullYear(), ws.getMonth(), ws.getDate() + k), s = isoOf(d);
        if (dows.indexOf(d.getDay()) >= 0 && !isOffHoliday(s, S)) sched.push(s);
      }
      if (!sched.length) continue;
      if (sched.every(function (s) { return s.slice(0, 7) === ym && workedSet[s]; })) n++;
    }
    return n;
  }
  function yearsBetween(fromIso, toIso) {
    var a = new Date(fromIso), b = new Date(toIso); var y = b.getFullYear() - a.getFullYear();
    if (b.getMonth() < a.getMonth() || (b.getMonth() === a.getMonth() && b.getDate() < a.getDate())) y--;
    return y;
  }

  /* ---------- 소득세: 근로소득 간이세액표(소득세법 시행령 별표 2) ----------
     월급여액(비과세 제외, 4대보험 공제 전 금액) 천원 구간 × 공제대상가족 수(본인 포함, 8~20세 자녀는 1명당 1명 더)로 찾는다.
     샘플로 확인: 월급여 1,653,940원 → 1,650~1,660천원 구간, 가족 1명 = 12,020원 (최병학 칸과 같음) */
  function simpleTax(gross, fam) {
    var T = (typeof SEOMU_TAXTABLE !== 'undefined') ? SEOMU_TAXTABLE : null;
    fam = Math.max(1, Math.floor(+fam || 1));
    var r = { tax: 0, row: null, fam: fam, known: true };
    if (!T) { r.known = false; return r; }
    var k = gross / 1000;
    if (k < T.rows[0][0]) return r;                                       // 770천원 미만 = 0원
    var last = T.rows[T.rows.length - 1];
    if (k >= last[1]) { r.known = false; r.row = [last[1], null]; return r; } // 10,000천원 이상은 표 밖 → 직접 입력
    var lo = 0, hi = T.rows.length - 1;
    while (lo < hi) { var mid = (lo + hi + 1) >> 1; if (T.rows[mid][0] <= k) lo = mid; else hi = mid - 1; }
    var row = T.rows[lo]; r.row = [row[0], row[1]];
    if (fam <= 11) r.tax = row[1 + fam];
    else r.tax = Math.max(0, row[12] - (row[11] - row[12]) * (fam - 11));    // 11명 초과: 별표 2 제4호
    return r;
  }

  /* ---------- 근로자 한 명 계산 ---------- */
  function hourlyOf(S) { var p = parseYm(S.ym); return +((S.hourlyByYear || {})[p.y]) || +S.hourly || 0; }

  function calcWorker(w, S) {
    var hourly = hourlyOf(S), hpd = +S.hoursPerDay || 7;
    var dayRate = Math.round(hourly * hpd);
    var worked = workDates(w, S), sched = scheduledDatesInMonth(w, S);
    var wd = scheduledDow(w, S).length, weeklyHours = wd * hpd;
    var juEligible = weeklyHours >= 15;
    var juUnit = juEligible ? floor10(Math.min(weeklyHours, 40) / 40 * 8 * hourly) : 0;
    var joo = juEligible ? countJoo(w, S, worked) : 0;
    var full = sched.length > 0 && sched.every(function (s) { return worked.indexOf(s) >= 0; });
    var p = parseYm(S.ym), monthEnd = iso(p.y, p.m, daysInMonth(p.y, p.m));
    var firstYear = !(w.hire) || yearsBetween(w.hire, monthEnd) < 1;
    var wolcha = (juEligible && full && firstYear) ? 1 : 0;
    var ovr = w.ovr || {};
    var days = ovr.days != null && ovr.days !== '' ? +ovr.days : worked.length;
    if (ovr.joo != null && ovr.joo !== '') joo = +ovr.joo;
    if (ovr.wolcha != null && ovr.wolcha !== '') wolcha = +ovr.wolcha;
    var extraHours = +w.extraHours || 0;
    var basePay = days * dayRate, juPay = joo * juUnit, wolchaPay = wolcha * juUnit, extraPay = Math.round(extraHours * hourly);
    var gross = basePay + juPay + wolchaPay + extraPay;
    var ins = w.ins || { found: false, health: [0, 0], pension: [0, 0], employ: [0, 0], accident: 0 };
    var st = simpleTax(gross, (+w.fam || 1) + (+w.kids || 0));
    var manual = (w.tax != null && w.tax !== '');
    var taxPct = (S.taxPct != null && S.taxPct !== '') ? +S.taxPct : 100;          // 간이세액표의 80/100/120% 중 선택(원천징수 선택)
    var taxAuto = floor10(st.tax * taxPct / 100);
    var incomeTax = manual ? (+w.tax || 0) : taxAuto, localTax = floor10(incomeTax * 0.1);
    var ded = { health: ins.health[0], pension: ins.pension[0], employ: ins.employ[0], incomeTax: incomeTax, localTax: localTax };
    var dedTotal = ded.health + ded.pension + ded.employ + ded.incomeTax + ded.localTax;
    var er = { health: ins.health[1], pension: ins.pension[1], employ: ins.employ[1], accident: ins.accident };
    var erTotal = er.health + er.pension + er.employ + er.accident;
    return {
      full: full, firstYear: firstYear, juRaw: Math.min(weeklyHours, 40) / 40 * 8 * hourly,
      hourly: hourly, hoursPerDay: hpd, dayRate: dayRate, worked: worked, days: days, weeklyDays: wd, weeklyHours: weeklyHours,
      juEligible: juEligible, juUnit: juUnit, joo: joo, wolcha: wolcha, extraHours: extraHours,
      basePay: basePay, juPay: juPay, wolchaPay: wolchaPay, extraPay: extraPay, gross: gross,
      ded: ded, dedTotal: dedTotal, net: gross - dedTotal, er: er, erTotal: erTotal, total: gross + erTotal,
      special: joo + wolcha > 0 || extraHours > 0,
      tax: { auto: taxAuto, manual: manual, row: st.row, fam: st.fam, known: st.known, pct: taxPct, base: gross }
    };
  }
  function calcAll(workers, S) {
    var rows = workers.map(function (w) { return { w: w, c: calcWorker(w, S) }; });
    var t = { gross: 0, ded: 0, net: 0, er: 0, total: 0, days: 0, joo: 0, wolcha: 0 };
    rows.forEach(function (r) { t.gross += r.c.gross; t.ded += r.c.dedTotal; t.net += r.c.net; t.er += r.c.erTotal; t.total += r.c.total; t.days += r.c.days; t.joo += r.c.joo; t.wolcha += r.c.wolcha; });
    return { rows: rows, totals: t };
  }

  /* ---------- 4대보험 고지서 ---------- */
  var norm = function (v) { return String(v == null ? '' : v).replace(/[\s\r\n]+/g, ''); };
  var num = function (v) { if (typeof v === 'number') return v; var n = parseFloat(String(v == null ? '' : v).replace(/[^0-9.\-]/g, '')); return isNaN(n) ? 0 : n; };

  function findHeaderRow(rows) {
    for (var i = 0; i < Math.min(rows.length, 40); i++) {
      var r = rows[i] || [];
      for (var j = 0; j < r.length; j++) { var t = norm(r[j]); if (t === '성명' || t === '근로자명' || t === '이름') return i; }
    }
    return -1;
  }
  function colOf(rows, hdr, re, from, to) {
    for (var i = Math.max(0, from != null ? from : hdr - 1); i <= Math.min(rows.length - 1, to != null ? to : hdr + 2); i++) {
      var r = rows[i] || [];
      for (var j = 0; j < r.length; j++) if (re.test(norm(r[j]))) return j;
    }
    return -1;
  }
  function detectKind(sheetName, rows) {
    var n = norm(sheetName);
    if (/건강/.test(n)) return 'health';
    if (/국민연금/.test(n)) return 'pension';
    if (/고용/.test(n)) return 'employ';
    if (/산재/.test(n)) return 'accident';
    var title = '';
    for (var i = 0; i < Math.min(rows.length, 5); i++) (rows[i] || []).forEach(function (c) { if (typeof c === 'string') title += c; });
    if (/건강/.test(title)) return 'health';
    if (/국민연금/.test(title)) return 'pension';
    if (/고용/.test(title)) return 'employ';
    if (/산재/.test(title)) return 'accident';
    return '';
  }
  function titleYm(rows) {
    for (var i = 0; i < Math.min(rows.length, 6); i++) for (var j = 0; j < (rows[i] || []).length; j++) {
      var m = /(20\d\d)\s*[.\-년]\s*(\d{1,2})/.exec(String(rows[i][j] == null ? '' : rows[i][j])); if (m) return m[1] + '-' + P2(+m[2]);
    }
    return '';
  }
  /* sheets: { 시트이름: 2차원 배열 }  →  { people: {키: {...}}, found: {health:true,...}, ym } */
  function parseInsurance(sheets) {
    var people = {}, found = {}, ym = '', notes = [];
    function get(name, b6) {
      var k = name + '|' + b6;
      return people[k] || (people[k] = { name: name, birth6: b6, dept: '', hire: '', health: [0, 0], pension: [0, 0], employ: [0, 0], accident: 0, rows: 0 });
    }
    Object.keys(sheets).forEach(function (sn) {
      var rows = sheets[sn] || [], kind = detectKind(sn, rows);
      if (!kind) return;
      var hdr = findHeaderRow(rows); if (hdr < 0) { notes.push(sn.trim() + ': 성명 머리글을 못 찾음'); return; }
      ym = ym || titleYm(rows);
      var cName = colOf(rows, hdr, /^(성명|근로자명|이름)$/, hdr, hdr), cBirth = colOf(rows, hdr, /^생년월일$/), cDept = colOf(rows, hdr, /^부서명$/);
      var cHire = colOf(rows, hdr, /^(취득일자|고용일)$/);
      var cols = {};
      if (kind === 'health') {
        cols.emp = colOf(rows, hdr, /근로자총부담금/); cols.er = colOf(rows, hdr, /사업장부담금/);
        if (cols.emp < 0) cols.emp = cols.er = colOf(rows, hdr, /고지보험료계/);
      } else if (kind === 'pension') {
        cols.emp = colOf(rows, hdr, /본인기여금/); cols.er = colOf(rows, hdr, /사용자부담금/);
      } else if (kind === 'employ') {
        var g = colOf(rows, hdr, /보험료합계/, hdr, hdr + 1);
        if (g >= 0) {
          var sub = rows[hdr + 1] || [];
          for (var k = g; k < g + 3; k++) { var s = norm(sub[k]); if (/근로자/.test(s)) cols.emp = k; else if (/사업주/.test(s)) (cols.er = cols.er == null ? [k] : cols.er.concat(k)); }
          if (cols.emp == null) cols.emp = g;
          if (cols.er == null) cols.er = [g + 1, g + 2];
        }
      } else if (kind === 'accident') {
        cols.er = colOf(rows, hdr, /보험료합계/, hdr, hdr + 1);
      }
      if (cName < 0 || (kind !== 'accident' && cols.emp < 0 && cols.er < 0)) { notes.push(sn.trim() + ': 금액 열을 못 찾음'); return; }
      found[kind] = true;
      for (var i = hdr + 1; i < rows.length; i++) {
        var r = rows[i] || [], nm = String(r[cName] == null ? '' : r[cName]).trim();
        if (!nm || !/[가-힣]/.test(nm) || /^(합계|소계|계)$/.test(norm(nm))) continue;
        var b6 = cBirth >= 0 ? birth6Of(r[cBirth]) : '';
        var p = get(nm, b6); p.rows++; p.seen = p.seen || {}; if (p.seen[kind]) p.dup = true; p.seen[kind] = 1;
        if (cDept >= 0 && r[cDept]) p.dept = String(r[cDept]).trim();
        if (cHire >= 0) { var h = dateOnly(r[cHire]); if (h && (!p.hire || h < p.hire)) p.hire = h; }
        if (kind === 'health') { p.health[0] += num(r[cols.emp]); p.health[1] += num(r[cols.er]); }
        else if (kind === 'pension') { p.pension[0] += num(r[cols.emp]); p.pension[1] += num(r[cols.er]); }
        else if (kind === 'employ') { p.employ[0] += num(r[cols.emp]); p.employ[1] += cols.er.reduce(function (a, c) { return a + num(r[c]); }, 0); }
        else if (kind === 'accident') { p.accident += num(r[cols.er]); }
      }
    });
    return { people: people, found: found, ym: ym, notes: notes };
  }
  /* 근로자 ↔ 고지서 사람 찾기: 이름 + 생년월일(6자리). 생년월일이 없으면 이름이 하나뿐일 때만 */
  function matchInsurance(w, parsed) {
    var keys = Object.keys(parsed.people), exact = [], sameName = [];
    keys.forEach(function (k) {
      var p = parsed.people[k]; if (p.name !== w.name) return;
      if (w.birth6 && p.birth6 === w.birth6) exact.push(p); else sameName.push(p);
    });
    var hit = null, status = 'none', msg = '';
    if (exact.length) { hit = exact; status = 'ok'; }
    else if (!w.birth6 && sameName.length === 1) { hit = sameName; status = 'nameonly'; msg = '생년월일 없이 이름만으로 찾음'; }
    else if (sameName.length) { status = 'birthdiff'; msg = '이름은 같지만 생년월일이 달라 넣지 않음(' + sameName.map(function (p) { return p.birth6 || '-'; }).join(', ') + ')'; }
    else { msg = '고지서에 없음'; }
    var ins = { found: !!hit, health: [0, 0], pension: [0, 0], employ: [0, 0], accident: 0, hire: '', sheets: {}, status: status, msg: msg };
    if (hit) hit.forEach(function (p) {
      ins.health[0] += p.health[0]; ins.health[1] += p.health[1]; ins.pension[0] += p.pension[0]; ins.pension[1] += p.pension[1];
      ins.employ[0] += p.employ[0]; ins.employ[1] += p.employ[1]; ins.accident += p.accident;
      if (p.hire && (!ins.hire || p.hire < ins.hire)) ins.hire = p.hire; ins.dept = p.dept; ins.rows = (ins.rows || 0) + p.rows;
    });
    ins.dup = !!hit && hit.some(function (p) { return p.dup; });
    return ins;
  }

  /* ---------- 근로자 명부 엑셀 ---------- */
  var ROSTER_HEAD = {
    name: /^(성명|이름|근로자명|근로자|성함)$/, rrn: /^(주민등록번호|주민번호|주민등록|생년월일|생년|주민\(생년월일\))$/,
    bank: /^(은행|은행명|금융기관)$/, acct: /^(계좌번호|계좌|계좌\(번호\)|입금계좌)$/, kind: /^(구분|근무구분|근무형태|근무|평일주말)$/,
    hire: /^(입사일|고용일|취득일|입사일자|근무시작일)$/, tax: /^(소득세|근로소득세)$/, note: /^(비고)$/,
    fam: /^(공제대상가족|공제대상가족수|부양가족|부양가족수|가족수|공제가족)$/, kids: /^(자녀|자녀수|8세이상20세이하자녀|8~20세자녀)$/
  };
  var BANKS = ['국민', '신한', '우리', '하나', '농협', 'NH', '기업', 'IBK', 'SC', '제일', '씨티', '카카오', '케이', '토스', '새마을', '신협', '우체국', '수협', '산림', '부산', '대구', '경남', '광주', '전북', '제주', 'KDB', '산업'];
  function splitBankAccount(s) {
    s = String(s == null ? '' : s).trim(); if (!s) return { bank: '', acct: '' };
    var m = /^(.*?[가-힣A-Za-z]+)[\s:：]*([0-9][0-9\-\s]{5,})$/.exec(s);
    if (m) return { bank: m[1].trim(), acct: m[2].replace(/\s+/g, '') };
    return { bank: '', acct: s };
  }
  function parseRoster(rows, personFilter) {
    var hdr = -1, map = {};
    for (var i = 0; i < Math.min(rows.length, 30) && hdr < 0; i++) {
      var m = {}, cnt = 0;
      (rows[i] || []).forEach(function (c, j) { var t = norm(c); Object.keys(ROSTER_HEAD).forEach(function (k) { if (m[k] == null && ROSTER_HEAD[k].test(t)) { m[k] = j; cnt++; } }); });
      if (m.name != null && cnt >= 2) { hdr = i; map = m; }
    }
    var start = hdr + 1;
    if (hdr < 0) { map = { name: 0, rrn: 1, bank: 2, acct: 3 }; start = (rows[0] && /성명|이름/.test(norm(rows[0][0]))) ? 1 : 0; }
    var out = [], skipped = [];
    for (var r = start; r < rows.length; r++) {
      var row = rows[r] || [], raw = row[map.name];
      if (raw == null || String(raw).trim() === '') continue;
      var name = personFilter ? personFilter(raw) : String(raw).trim();
      if (!name) { var t = String(raw).replace(/\s+/g, ' ').trim(); if (skipped.indexOf(t) < 0) skipped.push(t); continue; }
      var rr = parseRrn(map.rrn != null ? row[map.rrn] : '');
      var bank = map.bank != null ? String(row[map.bank] == null ? '' : row[map.bank]).trim() : '';
      var acct = map.acct != null ? String(row[map.acct] == null ? '' : row[map.acct]).trim() : '';
      if (!bank && acct) { var sp = splitBankAccount(acct); if (sp.bank) { bank = sp.bank; acct = sp.acct; } }
      var kindTxt = map.kind != null ? String(row[map.kind] || '') : '';
      var kind = /주말|토|일/.test(kindTxt) && !/평일/.test(kindTxt) ? 'weekend' : 'weekday';
      var hire = map.hire != null ? dateOnly(row[map.hire]) : '';
      var tax = map.tax != null && row[map.tax] !== '' && row[map.tax] != null ? num(row[map.tax]) : null;
      var fam = map.fam != null && row[map.fam] !== '' ? Math.max(1, num(row[map.fam])) : 1, kids = map.kids != null && row[map.kids] !== '' ? Math.max(0, num(row[map.kids])) : 0;
      out.push({ name: name, birth6: rr.birth6, birth8: rr.birth8, rrnOk: rr.ok, bank: bank, account: acct, kind: kind, hire: hire, tax: tax, fam: fam, kids: kids, note: map.note != null ? String(row[map.note] || '') : '' });
    }
    return { workers: out, skipped: skipped };
  }

  /* ---------- 납부서 PDF 글자 → 소득세 / 지방소득세 ---------- */
  function parsePayNotice(text) {
    var out = [], parts = String(text).split(/세입세출외현금납부서/);
    parts.forEach(function (pg) {
      var kind = /보관금\s*-\s*지방소득세/.test(pg) ? 'local' : /보관금\s*-\s*소득세/.test(pg) ? 'income' : '';
      if (!kind) return;
      var amt = /금\s*([0-9][0-9,]*)\s*원\s*[\(（]/.exec(pg), who = /인부임\s*[\(（]\s*([가-힣]{2,4})\s*[:：]\s*([0-9][0-9,]*)\s*원/.exec(pg);
      if (!amt || !who) return;
      out.push({ kind: kind, amount: +amt[1].replace(/,/g, ''), name: who[1], gross: +who[2].replace(/,/g, '') });
    });
    return out;
  }

  var api = {
    floor10: floor10, DOW_KO: DOW_KO, parseYm: parseYm, monthDates: monthDates, holidayName: holidayName, isoOf: isoOf, iso: iso,
    parseRrn: parseRrn, birth6Of: birth6Of, scheduledDow: scheduledDow, workDates: workDates, scheduledDatesInMonth: scheduledDatesInMonth,
    calcWorker: calcWorker, calcAll: calcAll, simpleTax: simpleTax, hourlyOf: hourlyOf, parseInsurance: parseInsurance, matchInsurance: matchInsurance,
    parseRoster: parseRoster, parsePayNotice: parsePayNotice, splitBankAccount: splitBankAccount
  };
  if (typeof module !== 'undefined') module.exports = api;
  return api;
})();
