/* ===== 공식 양식: 지방자치단체 결산·집행현황표 형식 (단위 표기 · 예산현액/지출원인행위액/지출액/집행잔액/집행률 · 계 행 · 상태 · 사유)
   + 보고서 관행: 결론(요약)을 맨 앞에, 단위는 표 머리에, 음수는 괄호, 감소는 △ ===== */
const UNITS = [['원', 1, 0], ['천원', 1e3, 0], ['백만원', 1e6, 1], ['억원', 1e8, 2]];
const unitOf = () => UNITS.find(u => u[0] === ((S.opt && S.opt.repUnit) || '원')) || UNITS[0];
const ul = () => unitOf()[0];
const fu = n => { if (n == null || isNaN(n)) return ''; const [, d, dec] = unitOf(); return (n / d).toLocaleString('ko-KR', { minimumFractionDigits: dec, maximumFractionDigits: dec }); };
const fuw = n => fu(n) + ul();
const fuP = n => { const s = fu(Math.abs(n)); return n < 0 && s.replace(/[0.,]/g, '') !== '' ? `(${s})` : s; };   // 공식 양식: 음수는 괄호
const dlt = (v, d = 1) => `${v < 0 ? '△' : v > 0 ? '+' : ''}${Math.abs(v).toFixed(d)}%p`;

function offState(r, el, th) {
  if (!(r.budget > 0)) return ['gray', '–', '-'];
  if (r.remain < 0) return ['bad', '■', '초과'];
  if (r.paid <= 0) return ['gray', '○', '미집행'];
  if (r.rate >= th.hot) return ['bad', '▲', '소진임박'];
  if (r.rate < el - th.low) return ['warn', '▲', '저조'];
  return ['ok', '●', '정상'];
}
function renderOfficial(D) {
  const { T, el, th, nats } = D;
  const rows = [...D.rows].filter(r => r.budget > 0 || r.paid > 0).sort((a, b) => b.budget - a.budget);
  const sh = v => T.budget > 0 ? (v / T.budget * 100).toFixed(1) : '0.0';
  const low = rows.filter(r => offState(r, el, th)[2] === '저조'), zero = rows.filter(r => offState(r, el, th)[2] === '미집행'), hot = rows.filter(r => ['소진임박', '초과'].includes(offState(r, el, th)[2]));
  const diff = T.rate - el;
  const bullets = [
    `예산현액 <b>${fuw(T.budget)}</b> 중 <b>${fuw(T.paid)}</b> 집행(집행률 <b>${pct1(T.rate)}</b>), 집행잔액 <b>${fuw(T.remain)}</b>`,
    `연 경과율(${pct1(el)}) 대비 <b>${dlt(diff)}</b>${T.dlt != null && D.prev ? `, 직전 자료(${esc(D.prev.asOf)}) 대비 집행률 ${dlt(T.dlt)}` : ''}`,
    T.gap > 0 ? `지출원인행위 후 미집행액 ${fuw(T.gap)} (원인행위 포함 집행률 ${pct1(T.rateAll)})` : '',
    (low.length || zero.length || hot.length) ? `주의 사업: 집행 저조 <b>${low.length}</b>개${low.length ? '(' + low.slice(0, 3).map(r => esc(r.biz)).join(', ') + (low.length > 3 ? ' 등' : '') + ')' : ''}, 미집행 <b>${zero.length}</b>개, 소진임박·초과 <b>${hot.length}</b>개` : '주의가 필요한 사업 없음',
  ].filter(Boolean);
  const stCell = r => { const s = offState(r, el, th); return `<span class="ost ${s[0]}">${s[1]} ${s[2]}</span>`; };
  const note = r => (S.repNote && S.repNote[r.biz]) || '';
  const noteCell = r => { const v = note(r), s = offState(r, el, th)[0]; if (!v && (s === 'ok')) return ''; return `<div class="rnote"><input class="cellin rni" data-rn="${esc(r.biz)}" value="${esc(v)}" placeholder="${s !== 'ok' ? '사유·계획' : ''}" aria-label="${esc(r.biz)} 사유">${v ? `<span class="rpn">${esc(v)}</span>` : ''}</div>`; };
  const need = rows.filter(r => ['저조', '미집행', '소진임박', '초과'].includes(offState(r, el, th)[2]));
  return `<div class="of">
    <div class="of-meta"><span>기준일: <b>${esc(S.asOf)}</b></span><span>자료: ${esc(D.B.file)}</span><span class="of-unit">(단위: <b>${ul()}</b>, %)</span></div>
    <div class="of-sec">1. 총괄</div>
    <ul class="of-sum">${bullets.map(b => `<li>${b}</li>`).join('')}</ul>
    <table class="t of-tbl"><thead><tr><th>구분</th><th>예산현액</th><th>지출원인행위액</th><th>지출액</th><th>집행잔액</th><th>집행률(%)</th></tr></thead><tbody>
      <tr class="of-tot"><td>계</td><td class="num">${fuP(T.budget)}</td><td class="num">${fuP(T.commit)}</td><td class="num">${fuP(T.paid)}</td><td class="num">${fuP(T.remain)}</td><td class="num">${T.rate.toFixed(1)}</td></tr>
      ${nats.map(n => `<tr><td>${esc(n.name)}</td><td class="num">${fuP(n.budget)}</td><td class="num">${fuP(n.commit)}</td><td class="num">${fuP(n.paid)}</td><td class="num">${fuP(n.remain)}</td><td class="num">${n.rate.toFixed(1)}</td></tr>`).join('')}</tbody></table>
    <div class="of-sec">2. 사업별 집행 현황 <span class="of-unit">(단위: ${ul()}, %)</span></div>
    <table class="t of-tbl"><thead><tr><th>순번</th><th>세부사업</th><th>예산현액</th><th>구성비(%)</th><th>지출원인행위액</th><th>지출액</th><th>집행잔액</th><th>집행률(%)</th><th>상태</th><th style="min-width:200px">비고 (사유·계획)</th></tr></thead><tbody>
      <tr class="of-tot"><td colspan="2">계 (${rows.length}개 사업)</td><td class="num">${fuP(T.budget)}</td><td class="num">100.0</td><td class="num">${fuP(T.commit)}</td><td class="num">${fuP(T.paid)}</td><td class="num">${fuP(T.remain)}</td><td class="num">${T.rate.toFixed(1)}</td><td></td><td></td></tr>
      ${rows.map((r, i) => `<tr><td class="c">${i + 1}</td><td class="nm2">${esc(r.biz)}</td><td class="num">${fuP(r.budget)}</td><td class="num">${sh(r.budget)}</td><td class="num">${fuP(r.commit)}</td><td class="num">${fuP(r.paid)}</td><td class="num ${r.remain < 0 ? 'neg' : ''}">${fuP(r.remain)}</td><td class="num"><b>${r.rate.toFixed(1)}</b>${r.dlt != null && Math.abs(r.dlt) >= 0.05 ? `<span class="of-d">${r.dlt < 0 ? '△' : '+'}${Math.abs(r.dlt).toFixed(1)}</span>` : ''}</td><td>${stCell(r)}</td><td>${noteCell(r)}</td></tr>`).join('')}</tbody></table>
    ${need.length ? `<div class="of-sec">3. 주요 사항 (집행 저조·미집행·소진임박 사업)<span class="noprint muted" style="font-size:12px;font-weight:400;margin-left:10px">사유·계획은 위 표의 비고에 입력하면 여기에 함께 표시됩니다</span></div><ul class="of-sum">${need.slice(0, 12).map(r => { const s = offState(r, el, th); const nt = note(r); return `<li><b>${esc(r.biz)}</b> — 집행률 ${r.rate.toFixed(1)}%(경과율 대비 ${dlt(r.rate - el)}), 집행잔액 ${fuw(r.remain)} <span class="ost ${s[0]}">${s[1]} ${s[2]}</span>${nt ? `<div class="of-why">▷ ${esc(nt)}</div>` : ''}</li>`; }).join('')}${need.length > 12 ? `<li class="muted">외 ${need.length - 12}개 사업</li>` : ''}</ul>` : ''}
    <div class="of-foot">※ 집행률 = 지출액 ÷ 예산현액, 집행잔액 = 예산현액 − 지출액. 지출액은 통계목별 조회의 집행누계액(당해예산), 지출원인행위액은 계약·결의 등으로 확정된 금액입니다. 이월·불용은 반영하지 않았고, ( )는 음수(예산 초과), △는 감소, 단위 미만은 반올림하여 합계가 일치하지 않을 수 있습니다. 「저조」는 집행률이 경과율보다 ${th.low}%p 이상 낮은 경우, 「소진임박」은 집행률 ${th.hot}% 이상인 경우입니다.</div></div>`;
}
