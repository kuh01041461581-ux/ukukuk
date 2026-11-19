/* ===== 숫자 맞춰보기: 프로그램에 입력한 일상경비 ↔ e-호조(통계목별 조회) ===== */
function reconcile() {
  if (!S.biz || !S.lines.length) return null;
  const doc = { entries: S.biz.entries };
  const rows = S.lines.map(l => { const e = findEntry(doc, l); return { l, e, diff: e ? (l.spent || 0) - (e.paid || 0) : 0 }; });
  const matched = rows.filter(r => r.e), diffs = matched.filter(r => Math.abs(r.diff) >= 1), missing = rows.filter(r => !r.e);
  const allB = sum(S.biz.entries, e => e.budget || 0), allP = sum(S.biz.entries, e => e.paid || 0);
  return { rows, matched, diffs, missing, appSum: sum(matched, r => r.l.spent || 0), ledgerSum: sum(matched, r => r.e.paid || 0), allB, allP };
}
function reconNotice() {   // [요약] 위쪽의 한 줄 알림 (문제가 있을 때만)
  const rc = reconcile(); if (!rc || (!rc.diffs.length && !rc.missing.length)) return '';
  return `<div class="recon-note noprint"><span><b>숫자 확인</b> — ${rc.diffs.length ? `일상경비 입력값이 e-호조와 다른 곳 <b>${rc.diffs.length}곳</b>` : ''}${rc.diffs.length && rc.missing.length ? ' · ' : ''}${rc.missing.length ? `e-호조에서 못 찾은 항목 <b>${rc.missing.length}개</b>` : ''}</span><button class="btn sm" data-act="repview" data-v="full">자세히</button></div>`;
}
function reconCardHTML(T) {
  const rc = reconcile(); if (!S.biz) return '';
  const same = Math.abs(rc ? rc.allP - T.paid : 0) < 1 && Math.abs(rc ? rc.allB - T.budget : 0) < 1;
  let h = `<div class="card recon"><h2>숫자 맞춰보기 <span class="muted" style="font-weight:400;font-size:12px">프로그램 ↔ e-호조(통계목별 조회)</span></h2>
    <div class="recon-row"><span class="ok-sym">${same ? '✓' : '!'}</span><span>불러온 파일(${esc(S.biz.file)}, ${S.biz.entries.length}건)의 예산·집행 합계가 이 보고서의 합계와 <b>${same ? '같습니다' : '다릅니다'}</b></span></div>`;
  if (!rc) return h + `<div class="recon-row"><span class="ok-sym">–</span><span class="muted">일상경비 항목이 없어 대조할 것이 없습니다.</span></div></div>`;
  const ok = !rc.diffs.length;
  h += `<div class="recon-row"><span class="ok-sym ${ok ? '' : 'warn'}">${ok ? '✓' : '!'}</span><span>일상경비 ${rc.rows.length}개 항목 중 ${rc.matched.length}개를 e-호조에서 찾았고, 지출 합계는 프로그램 <b>${won(rc.appSum)}원</b> / e-호조 <b>${won(rc.ledgerSum)}원</b> ${ok ? '— <b>모두 같습니다</b>' : `— 차이 <b class="neg">${won(rc.appSum - rc.ledgerSum)}원</b> (<b>${rc.diffs.length}곳</b> 다름)`}</span></div>`;
  if (rc.diffs.length) h += `<div class="tbl-wrap" style="margin-top:6px"><table class="t"><thead><tr><th>일상경비 항목</th><th>프로그램 지출</th><th>e-호조 집행</th><th>차이</th><th class="noprint"></th></tr></thead><tbody>${rc.diffs.map(r => `<tr><td class="nm2">${esc(r.l.label)}<span class="hint">${esc(r.l.group)} · ${esc(r.l.code)}</span></td><td class="num">${won(r.l.spent)}</td><td class="num">${won(r.e.paid)}</td><td class="num ${r.diff < 0 ? 'neg' : ''}">${r.diff > 0 ? '+' : ''}${won(r.diff)}</td><td class="noprint"><button class="btn sm" data-act="reconfix" data-id="${r.l.id}">e-호조 값으로</button></td></tr>`).join('')}</tbody></table></div>
    <div class="row noprint" style="justify-content:flex-end;margin-top:8px"><span class="muted" style="font-size:12px;margin-right:8px">차이는 프로그램에서 직접 고친 값이거나, e-호조 자료가 더 새롭거나 오래된 경우에 생깁니다. 어느 쪽이 맞는지 확인한 뒤 맞추세요.</span><button class="btn primary sm" data-act="reconfixall">차이 나는 ${rc.diffs.length}곳 모두 e-호조 값으로 맞추기</button></div>`;
  if (rc.missing.length) h += `<div class="recon-row" style="margin-top:8px"><span class="ok-sym warn">!</span><span>e-호조에서 <b>못 찾은 일상경비 항목 ${rc.missing.length}개</b>: ${rc.missing.slice(0, 6).map(r => esc(r.l.label)).join(', ')}${rc.missing.length > 6 ? ' 외' : ''} <span class="muted">— 사업명·편성목 코드가 다르거나 이번 조회 파일에 없는 항목입니다. 비교에서 제외했습니다.</span></span></div>`;
  return h + '</div>';
}
function reconApply(ids) {
  const rc = reconcile(); if (!rc) return 0; let n = 0;
  rc.diffs.filter(r => !ids || ids.includes(r.l.id)).forEach(r => { r.l.spent = r.e.paid || 0; r.l.ehojo = { commit: r.e.commit, resolved: r.e.resolved, paid: r.e.paid, asOf: S.biz.asOf }; n++; });
  if (n) save(); return n;
}
