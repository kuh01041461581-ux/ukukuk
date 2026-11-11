/* ===== PDF 읽기 (pdf.js) → 줄 단위 텍스트 ===== */
async function pdfToLines(file, onProgress) {
  if (typeof pdfjsLib === 'undefined') throw new Error('PDF 읽기 모듈을 불러오지 못했습니다.');
  if (!pdfToLines._w) {
    pdfToLines._w = URL.createObjectURL(new Blob([$('#pdf-worker').textContent], { type: 'text/javascript' }));
    pdfjsLib.GlobalWorkerOptions.workerSrc = pdfToLines._w;
  }
  const data = new Uint8Array(await file.arrayBuffer());
  const doc = await pdfjsLib.getDocument({ data, isEvalSupported: false }).promise;
  const out = [];
  for (let p = 1; p <= doc.numPages; p++) {
    onProgress && onProgress(p, doc.numPages);
    const page = await doc.getPage(p);
    const tc = await page.getTextContent();
    const items = tc.items.filter(i => i.str !== '').map(i => ({ x: i.transform[4], y: i.transform[5], w: i.width, s: i.str }));
    items.sort((a, b) => b.y - a.y || a.x - b.x);
    const lines = [];
    for (const it of items) {
      const L = lines[lines.length - 1];
      if (L && Math.abs(L.y - it.y) <= 2.2) L.items.push(it); else lines.push({ y: it.y, items: [it] });
    }
    for (const L of lines) {
      L.items.sort((a, b) => a.x - b.x);
      let text = '', prevEnd = null, x0 = null;
      for (const it of L.items) {
        if (x0 === null && it.s.trim() !== '') x0 = it.x;
        if (prevEnd !== null && it.x - prevEnd > 1.5 && !/\s$/.test(text) && !/^\s/.test(it.s)) text += ' ';
        text += it.s; prevEnd = it.x + it.w;
      }
      out.push({ page: p, x0: x0 === null ? 0 : x0, text: text.replace(/\s+/g, ' ').trim() });
    }
  }
  return out;
}

/* 줄 끝의 숫자 덩어리를 떼어내 {label, tokens} 로 */
const NUMISH = /^[()△\-,\d.%]+$/;
function splitLine(text) {
  const t = text.split(' ').filter(Boolean); const nums = [];
  while (t.length && (NUMISH.test(t[t.length - 1]) || t[t.length - 1] === 'N' || t[t.length - 1] === 'Y')) nums.unshift(t.pop());
  return { label: t.join(' '), nums };
}
function amt(tok) {
  if (tok == null) return null; const neg = /△|^\(?-/.test(tok);
  const d = tok.replace(/[^\d]/g, ''); if (d === '') return null; return (neg ? -1 : 1) * parseInt(d, 10);
}
const firstAmt = nums => { const t = nums.find(x => /\d/.test(x)); return t == null ? null : amt(t); };
const normName = s => String(s || '').replace(/[\s·ㆍ・]/g, '');

function detectDoc(lines) {
  const head = lines.slice(0, 30).map(l => l.text.replace(/\s/g, '')).join('|');
  if (head.includes('세출예산사업명세서')) return 'budget';
  if (head.includes('통계목별조회')) return 'ledger';
  if (head.includes('세출예산') && head.includes('사업명세서')) return 'budget';
  return null;
}

/* ===== 합본예산서(세출예산사업명세서, 천원) ===== */
function parseBudgetDoc(lines) {
  const SKIP = /^(부서|정책|단위)\s*:|부서ㆍ정책|경정액|^예산액|전년도예산액|^- ?\d+ ?-$/;
  const entries = new Map(); const order = [];
  // 편성목(3자리 코드) 줄의 들여쓰기 = xMok
  let xMok = null;
  for (const l of lines) { if (/^\d{3}\s+\S/.test(l.text) && splitLine(l.text).nums.length) { xMok = l.x0; break; } }
  if (xMok == null) throw new Error('편성목(201 일반운영비 등) 줄을 찾지 못했습니다. 세출예산사업명세서 PDF가 맞는지 확인하세요.');
  const stack = []; let cur = null, mok = null, block = 'base', lastParent = '', lastDetail = null, prevHeader = null;
  for (const l of lines) {
    const t = l.text; if (!t || SKIP.test(t)) continue;
    if (/^\(?[국균도시]\s/.test(t) || /^[국균도시]$/.test(t)) continue;        // 재원(국·도·시) 줄
    const { label, nums } = splitLine(t);
    if (!label) continue;
    if (l.x0 < 50) continue;                                                      // 부서·제목
    const first = label.charAt(0);
    if (first === '○' || (first === 'o' && l.x0 >= xMok + 12)) {                  // 산출내역
      if (!cur) continue; const lvl = first === '○' ? 1 : 2; const text = label.slice(1).trim();
      if (lvl === 1) lastParent = text;
      lastDetail = { lvl, text, parent: lvl === 2 ? lastParent : '', amt: firstAmt(nums), blk: block };
      cur.raw.push(lastDetail); prevHeader = null; continue;
    }
    if (l.x0 > xMok + 12 && cur && lastDetail) {                                  // 산출내역 줄바꿈·산출기초
      if (/\d+원\*|\*\d/.test(label)) { /* 산출기초 */ } else lastDetail.text = (lastDetail.text + (/\s$/.test(lastDetail.text) ? '' : '') + label).trim();
      if (nums.length && lastDetail.amt == null) lastDetail.amt = firstAmt(nums);
      if (nums.length && lastDetail.amt != null && /\d+원\*|\*\d/.test(label)) lastDetail.amt = firstAmt(nums);
      continue;
    }
    if (/^(본예산|추경)/.test(label)) { block = label.startsWith('본예산') ? 'base' : 'chg'; lastParent = ''; lastDetail = null; continue; }
    let m;
    if ((m = label.match(/^(\d{3})\s+(.+)$/)) && nums.length) { mok = { code: m[1], name: m[2] }; prevHeader = null; lastDetail = null; continue; }
    if ((m = label.match(/^(\d{2})\s+(.+)$/)) && nums.length && mok && stack.length) {
      const biz = stack[stack.length - 1];
      const code = mok.code + m[1]; const key = biz.name + '|' + code;
      cur = { biz: biz.name, unit: stack.length > 1 ? stack[stack.length - 2].name : '', mok: mok.name, code, name: m[2], amount: (firstAmt(nums) || 0) * 1000, raw: [] };
      entries.set(key, cur); order.push(key); block = 'base'; lastParent = ''; lastDetail = null; prevHeader = null; continue;
    }
    // 정책·단위·세부사업 머리줄
    if (nums.length && l.x0 < xMok - 3) {
      while (stack.length && stack[stack.length - 1].x0 >= l.x0 - 0.5) stack.pop();
      const node = { x0: l.x0, name: label }; stack.push(node); prevHeader = node; cur = null; mok = null; lastDetail = null; continue;
    }
    if (!nums.length && prevHeader && Math.abs(l.x0 - prevHeader.x0) < 1.5) {    // 긴 세부사업명 줄바꿈
      prevHeader.name = (prevHeader.name + label).replace(/\s+/g, ' '); continue;
    }
  }
  // 산출내역 확정 (추경 값이 본예산 값을 덮어씀)
  const result = [];
  for (const key of order) {
    const e = entries.get(key); const map = new Map();
    for (const d of e.raw) { const k = d.parent + '/' + normName(d.text); map.set(k, { ...d }); }
    const details = [...map.values()];
    const parents = new Set(details.filter(d => d.lvl === 2).map(d => d.parent));
    e.leaves = details.filter(d => d.lvl === 2 || (d.lvl === 1 && !parents.has(d.text))).filter(d => d.amt != null)
      .map(d => ({ text: d.text, parent: d.parent, amount: d.amt * 1000 }));
    delete e.raw; result.push(e);
  }
  const title = lines.slice(0, 6).map(l => l.text).find(t => /추경|본예산|예산/.test(t) && /20\d\d/.test(t)) || '';
  return { kind: 'budget', entries: result, title };
}

/* 공공운영비 산출내역에서 전기·상하수도·통신 예산 추정 */
function utilBudgetFromBudget(doc, biz = '문화유적지 보존관리') {
  const out = { elec: 0, water: 0, tel: 0, found: false };
  const e = doc.entries.find(x => normName(x.biz) === normName(biz) && x.code === '20102'); if (!e) return out;
  out.found = true;
  for (const d of e.leaves) {
    if (/전기요금/.test(d.text)) out.elec += d.amount;
    else if (/상하수도/.test(d.text)) out.water += d.amount;
    else if (/통신요금|인터넷\s*사용료/.test(d.text)) out.tel += d.amount;
  }
  return out;
}

/* ===== 통계목별 조회(원 단위, 집행액) ===== */
function parseLedgerDoc(lines) {
  let xMok = null;
  for (const l of lines) { if (/^\d{3}\s+\S/.test(l.text) && splitLine(l.text).nums.length) { xMok = l.x0; break; } }
  if (xMok == null) throw new Error('편성목 줄을 찾지 못했습니다. 통계목별 조회 PDF가 맞는지 확인하세요.');
  const stack = []; let mok = null; const entries = [];
  let asOf = '';
  for (const l of lines) {
    const t = l.text; const mm = t.match(/~\s*(\d{4}-\d{2}-\d{2})/); if (mm) asOf = mm[1];
    const { label, nums } = splitLine(t); if (!label || !nums.length) continue;
    const ints = nums.filter(x => /^\d{1,3}(,\d{3})*$/.test(x)).map(amt);
    if (ints.length < 13) continue;
    let m;
    if ((m = label.match(/^(\d{3})\s+(.+)$/))) { mok = { code: m[1], name: m[2] }; continue; }
    if ((m = label.match(/^(\d{5})\s+(.+)$/)) && stack.length) {
      entries.push({ biz: stack[stack.length - 1].name, code: m[1], name: m[2], budget: ints[1], commit: ints[6], resolved: ints[10], paid: ints[12] });
      continue;
    }
    if (l.x0 < xMok - 3) { while (stack.length && stack[stack.length - 1].x0 >= l.x0 - 0.5) stack.pop(); stack.push({ x0: l.x0, name: label }); mok = null; }
  }
  return { kind: 'ledger', entries, asOf };
}

function findEntry(doc, l) {
  const nb = normName(l.biz);
  return doc.entries.find(e => normName(e.biz) === nb && e.code === l.code) ||
    doc.entries.find(e => e.code === l.code && (normName(e.biz).includes(nb) || nb.includes(normName(e.biz))) && nb.length > 3);
}
