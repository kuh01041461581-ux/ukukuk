/* ===== 첫 실행 안내(처음 켰을 때 한 번) + 고지서 분류 사용 안내 ===== */
function hasAnyData() { return S.lines.length > 0 || Object.values(S.util).some(x => x.items && x.items.length) || !!S.biz; }
function maybeOnboard() {
  if (!S.opt) S.opt = {};
  if (S.opt.onboarded || hasAnyData() || S.opt.helper || $('#dlg').open) return;
  const step = (n, t, d) => `<div class="ob-step"><span class="ob-n">${n}</span><div><b>${t}</b><div class="muted">${d}</div></div></div>`;
  openDlg(`<h3 style="font-size:22px;margin:0 0 4px">처음 오셨네요! 이렇게 시작하세요</h3><p class="sub" style="margin:0 0 12px">세 단계면 끝나요. 나중에 [사용법] 탭에서 그림으로 다시 볼 수 있어요.</p>
    ${step(1, 'e-호조에서 엑셀을 받아요', '「통계목별 조회」에서 <b>실집행액을 체크</b>한 뒤, 표시 <b>위쪽의 파란색 아이콘 버튼</b>으로 내려받아요. (아래쪽 출력 버튼은 결의액만 나와요)')}
    ${step(2, '[파일 불러오기]에 끌어 놓아요', '받은 엑셀 파일을 창에 끌어 놓으면 일상경비 항목이 자동으로 만들어져요. 쓰시던 일상경비·공공요금 엑셀도 같은 방법이에요.')}
    ${step(3, '숫자를 확인해요', '[일상경비 집행현황]에서 사업별 남은 돈을, [과 요약 보고서]에서 한눈에 보는 쉬운 보기를, 고지서가 오면 [고지서 분류]를 쓰세요.')}
    <div class="row" style="justify-content:flex-end;margin-top:14px;gap:8px"><button class="btn" data-ob="later">다음에 볼게요</button><button class="btn" data-ob="guide">사용법 보기</button><button class="btn primary" data-ob="import">파일 불러오기로 가기</button></div>`, d => {
    d.onclick = ev => { const b = ev.target.closest('[data-ob]'); if (!b) return; const a = b.dataset.ob; closeDlg(); if (a === 'import') showTab('import'); else if (a === 'guide') showTab('guide'); };
    d.onclose = () => { S.opt.onboarded = true; save(); };
  });
}
