/* ══════════════════════════════════════════════════════════════
   ui-refresh.js — [2026-07 리팩토링] 신규 추가 파일
   ------------------------------------------------------------------
   좌측 아이콘 레일(.rail)과 상단 고정 "다음/출력" 버튼
   (#step-header-next-btn)을 기존 버튼에 클릭을 그대로 전달(forward)
   하거나 기존 상태를 읽어와 표시만 동기화하는 "얇은 UI 연결 층"이다.
   index.js / general-labels.js는 한 줄도 수정하지 않았으며,
   이 파일이 없어도 기존 3단계 마법사(데이터/라벨편집/출력)는
   완전히 정상 동작한다 — 이 파일은 새 UI(레일·고정 CTA)를
   기존 기능에 "배선"만 할 뿐이다.
   ══════════════════════════════════════════════════════════════ */
(function () {
  const $ = (id) => document.getElementById(id);
  const forward = (id) => { const el = $(id); if (el) el.click(); };

  /* ── 현재 활성 스텝 판별 (기존 .step-item.active 클래스를 그대로 신뢰) ── */
  function getCurrentStep() {
    const active = document.querySelector(".step-item.active");
    return active?.dataset.step || "data";
  }

  /* ══════════════════════════════════════════════════════════════
     좌측 레일 — 클릭 전달 + 활성 상태 동기화
     ══════════════════════════════════════════════════════════════ */
  document.querySelectorAll(".rail-item[data-rail-step]").forEach((btn) => {
    btn.addEventListener("click", () => forward("step-tab-" + btn.dataset.railStep));
  });
  /* [버그 수정] 기존 fav-btn 클릭 핸들러(index.js)는 문서 전체 클릭을 감시하다
     열려있는 패널 바깥을 클릭하면 닫는데, 그 "바깥 클릭" 판정이 fav-btn 자기
     자신인지만 확인한다. 레일 버튼을 누르면 forward()가 fav-btn.click()을
     "시뮬레이션"하지만, 레일 버튼 자체의 실제 클릭 이벤트는 멈추지 않고 계속
     bubble되어 document까지 올라가 버려 "열자마자 바로 닫히는" 것처럼 보였다.
     레일 버튼 쪽에서 전파를 멈추면 해결된다. */
  $("rail-fav-btn")?.addEventListener("click", (e) => { e.stopPropagation(); forward("fav-btn"); });
  $("rail-guide-btn")?.addEventListener("click", () => forward("guide-btn"));
  /* [신규] 라벨편집 탭 인스펙터의 "다음: 출력" 버튼 — 상단 단계 탭(출력)에 클릭 전달 */
  $("insp-goto-output")?.addEventListener("click", () => forward("step-tab-print"));

  /* ★RAIL-DONE: 지나온 단계에 은은한 체크 표시를 얹는다 — 일반라벨 레일에
     이미 있는 .gl-rail-done과 완전히 같은 시각 언어(클래스·CSS 재사용,
     새 스타일 없음)를 주소라벨 레일에도 동일하게 적용한다. */
  const ADDR_STEP_ORDER = ["data", "edit", "print"];
  function syncRail(step) {
    document.querySelectorAll(".rail-item[data-rail-step]").forEach((btn) => {
      btn.classList.toggle("active", btn.dataset.railStep === step);
      btn.classList.toggle("gl-rail-done", ADDR_STEP_ORDER.indexOf(btn.dataset.railStep) < ADDR_STEP_ORDER.indexOf(step));
    });
  }

  /* ══════════════════════════════════════════════════════════════
     상단 고정 "다음 / 출력" 버튼 — 스텝에 따라 대상·문구만 바꿔
     기존 버튼(#step-data-next / #step-edit-next / #pstep-print)에
     클릭을 그대로 전달한다.
     ══════════════════════════════════════════════════════════════ */
  const NEXT_ICON = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14M13 6l6 6-6 6"/></svg>';
  const PRINT_ICON = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 01-2-2v-5a2 2 0 012-2h16a2 2 0 012 2v5a2 2 0 01-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>';

  function syncHeaderNext(step) {
    const btn = $("step-header-next-btn");
    const prevBtn = $("step-header-prev-btn");
    if (!btn) return;
    btn.hidden = false;

    if (step === "data") {
      const src = $("step-data-next");
      btn.innerHTML = `다음: 라벨 편집 ${NEXT_ICON}`;
      btn.disabled = !!src?.disabled;
      btn.onclick = () => forward("step-data-next");
      if (prevBtn) prevBtn.hidden = true; /* 첫 단계라 이전 없음 */
    } else if (step === "edit") {
      btn.innerHTML = `다음: 출력 ${NEXT_ICON}`;
      btn.disabled = false;
      btn.onclick = () => forward("step-edit-next");
      if (prevBtn) { prevBtn.hidden = false; prevBtn.onclick = () => forward("step-tab-data"); }
    } else if (step === "print") {
      btn.innerHTML = `${PRINT_ICON} 출력 시작`;
      btn.disabled = false;
      btn.onclick = () => forward("pstep-print");
      if (prevBtn) { prevBtn.hidden = false; prevBtn.onclick = () => forward("step-tab-edit"); }
    } else {
      btn.hidden = true;
      if (prevBtn) prevBtn.hidden = true;
    }
  }

  function syncAll() {
    const step = getCurrentStep();
    syncRail(step);
    syncHeaderNext(step);
  }

  /* 스텝을 바꾸는 모든 기존 트리거에 "동기화만" 추가로 걸어준다
     (기존 클릭 핸들러는 그대로 두고, 리스너를 하나 더 얹는 것뿐이라 안전) */
  ["step-tab-data", "step-tab-edit", "step-tab-print",
   "step-data-next", "step-edit-next", "step-print-back",
   "mode-tab-address", "mode-tab-general"]
    .forEach((id) => $(id)?.addEventListener("click", () => setTimeout(syncAll, 0)));

  /* 데이터가 바뀔 때(엑셀 로드/추가/삭제 등)마다 refreshDataStepView가
     이미 호출되고 있으므로(= index.js가 자체적으로 훅 해 둔 지점),
     그 함수를 감싸 라벨 카운트 배지·고정 버튼 비활성 상태까지 함께 갱신한다. */
  if (typeof window.refreshDataStepView === "function") {
    const _origRefreshDataStepView = window.refreshDataStepView;
    window.refreshDataStepView = function (...args) {
      const ret = _origRefreshDataStepView.apply(this, args);
      const cntEl = $("sb-label-count");
      const railCnt = $("rail-label-count");
      if (cntEl && railCnt) railCnt.textContent = (parseInt(cntEl.textContent, 10) || 0).toString();
      if (getCurrentStep() === "data") syncHeaderNext("data");
      return ret;
    };
  }

  /* 최초 진입 시 동기화 — index.js가 requestAnimationFrame으로 초기
     스텝(데이터/라벨편집)을 비동기 결정하므로, 그 다음 프레임에 맞춰 동기화한다 */
  requestAnimationFrame(() => requestAnimationFrame(syncAll));
})();

/* ══════════════════════════════════════════════════════════════
   ★GL-RAIL: 일반라벨 좌측 아이콘 레일 — 주소라벨 레일(위 블록)과 완전히
   같은 원리의 "얇은 배선"이다. 실제 동작은 이미 있는 #gl-step-tab-*
   (규격/디자인/확인·인쇄 전환)·#gl-clear-btn(전체 초기화)·#guide-btn
   (사용법 모달)에 그대로 있고, 여긴 클릭만 그 버튼들로 전달한다.
   #gl-step-tab-design/#gl-step-tab-review는 규격을 고르기 전엔
   disabled인데, forward()가 실제 <button>.click()을 호출하므로 disabled
   버튼은 자연히 무시된다 — general-labels.js의 기존 가드를 그대로
   물려받는 셈이라 별도 가드 코드가 필요 없다. general-labels.js·
   index.js는 이 블록에서도 한 줄도 건드리지 않는다. ══════════════════ */
(function () {
  const $ = (id) => document.getElementById(id);
  const forward = (id) => { const el = $(id); if (el) el.click(); };
  const GL_STEP_ORDER = ["spec", "design", "review"];
  /* [주의] NEXT_ICON/PRINT_ICON은 위쪽의 "다른"(별개) IIFE 안에 선언돼
     있어 여기서는 안 보인다 — 같은 값을 이 블록 안에도 로컬로 둔다. */
  const NEXT_ICON = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14M13 6l6 6-6 6"/></svg>';
  const PRINT_ICON = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 01-2-2v-5a2 2 0 012-2h16a2 2 0 012 2v5a2 2 0 01-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>';

  document.querySelectorAll(".rail-item[data-gl-rail-step]").forEach((btn) => {
    btn.addEventListener("click", () => forward("gl-step-tab-" + btn.dataset.glRailStep));
  });
  $("gl-rail-new-btn")?.addEventListener("click", () => forward("gl-clear-btn"));
  $("gl-rail-guide-btn")?.addEventListener("click", () => forward("guide-btn"));

  /* 현재 활성 스텝 — #gl-step-header 안의 .step-item.active를 그대로 신뢰
     (general-labels.js의 setGlWizardStep이 이미 관리하는 상태라 중복 계산 없음) */
  function getCurrentGlStep() {
    const active = document.querySelector("#gl-step-header .step-item.active");
    return active?.dataset.glStep || "spec";
  }

  function syncGlRailCount() {
    /* [신규] 칸 수 배지 — Step1의 "칸 수"·Step2 인스펙터의 "가로×세로=칸수"
       텍스트 중 지금 존재하는 걸 그대로 읽어와 숫자만 뽑아 표시한다.
       별도 계산 로직을 새로 만들지 않고 이미 있는 표시를 재사용한다. */
    const src = $("gl-insp-count") || $("gl-spec-sel-count");
    const cnt = $("gl-rail-cell-count");
    if (!cnt) return;
    const m = src && src.textContent && src.textContent.match(/(\d+)\s*칸/);
    cnt.textContent = m ? m[1] : "0";
  }

  /* [신규] 상단 고정 "이전/다음" 버튼 — 주소라벨 syncHeaderNext와 완전히
     같은 패턴. 실제 이동은 이미 있는 버튼(#gl-step-spec-next,
     #gl-step-tab-*)에 클릭을 그대로 전달하므로, 규격 미선택 시 다음
     버튼이 막히는 기존 가드도 그대로 물려받는다. */
  function syncGlHeaderNext(step) {
    const btn = $("gl-step-header-next-btn");
    const prevBtn = $("gl-step-header-prev-btn");
    if (!btn) return;
    btn.hidden = false;

    if (step === "spec") {
      const src = $("gl-step-spec-next");
      btn.innerHTML = `다음: 라벨 디자인 ${NEXT_ICON}`;
      btn.disabled = !!src?.disabled;
      btn.onclick = () => forward("gl-step-spec-next");
      if (prevBtn) prevBtn.hidden = true; /* 첫 단계라 이전 없음 */
    } else if (step === "design") {
      btn.innerHTML = `다음: 확인 및 인쇄 ${NEXT_ICON}`;
      btn.disabled = false;
      btn.onclick = () => forward("gl-step-tab-review");
      if (prevBtn) { prevBtn.hidden = false; prevBtn.onclick = () => forward("gl-step-tab-spec"); }
    } else if (step === "review") {
      btn.innerHTML = `${PRINT_ICON} 인쇄 시작`;
      btn.disabled = false;
      btn.onclick = () => forward("gl-pstep-print-btn");
      if (prevBtn) { prevBtn.hidden = false; prevBtn.onclick = () => forward("gl-step-tab-design"); }
    } else {
      btn.hidden = true;
      if (prevBtn) prevBtn.hidden = true;
    }
  }

  function syncGlRail() {
    const step = getCurrentGlStep();
    document.querySelectorAll(".rail-item[data-gl-rail-step]").forEach((btn) => {
      const s = btn.dataset.glRailStep;
      btn.classList.toggle("active", s === step);
      btn.classList.toggle("gl-rail-done", GL_STEP_ORDER.indexOf(s) < GL_STEP_ORDER.indexOf(step));
    });
    syncGlRailCount();
    syncGlHeaderNext(step);
  }

  /* [신규] 규격을 고르기 전/후로 #gl-step-tab-design·review의 disabled가
     바뀌는데, 그 상태를 레일 버튼에도 그대로 비춰준다(안 그러면 레일에서는
     눌러도 반응이 없는 이유를 알 수 없다) — general-labels.js를 건드리지
     않고 MutationObserver로 "가만히 지켜만" 본다. */
  function syncGlRailDisabled() {
    ["design", "review"].forEach((step) => {
      const src = $("gl-step-tab-" + step);
      const btn = document.querySelector('.rail-item[data-gl-rail-step="' + step + '"]');
      if (!src || !btn) return;
      if (!btn.dataset.glOrigTitle) btn.dataset.glOrigTitle = btn.title; /* 최초 1회만 원래 안내문구 저장 */
      btn.classList.toggle("gl-rail-disabled", !!src.disabled);
      btn.title = src.disabled ? "먼저 라벨 규격을 선택하세요" : btn.dataset.glOrigTitle;
    });
  }
  const glGatingObserver = new MutationObserver(syncGlRailDisabled);
  ["gl-step-tab-design", "gl-step-tab-review"].forEach((id) => {
    const el = $(id);
    if (el) glGatingObserver.observe(el, { attributes: true, attributeFilter: ["disabled"] });
  });

  /* 칸 수 텍스트가 바뀔 때마다(규격 카드를 고를 때 등) 배지도 함께 갱신 */
  const glCountObserver = new MutationObserver(syncGlRailCount);
  ["gl-insp-count", "gl-spec-sel-count"].forEach((id) => {
    const el = $(id);
    if (el) glCountObserver.observe(el, { characterData: true, childList: true, subtree: true });
  });

  ["gl-step-tab-spec", "gl-step-tab-design", "gl-step-tab-review",
   "gl-step-spec-next", "gl-clear-btn", "mode-tab-address", "mode-tab-general"]
    .forEach((id) => $(id)?.addEventListener("click", () => setTimeout(syncGlRail, 0)));

  syncGlRailDisabled();
  requestAnimationFrame(() => requestAnimationFrame(syncGlRail));
})();

/* ══════════════════════════════════════════════════════════════
   ★GL-STATUSBAR: 일반라벨 전용 하단 상태표시줄 — 주소라벨의 .statusbar와
   같은 시각 언어를 재사용한다(위 ★GL-RAIL과 같은 원리). 여기서도 새로
   계산하는 값은 없다 — 전부 화면에 이미 떠 있는 텍스트를 그대로 옮겨온다:
   · 규격 이름: #gl-template-display 그대로
   · 칸 수/입력·빈칸: ★GL-RAIL의 syncGlRailCount와 같은 방식으로 "N칸"
     텍스트에서 숫자만 뽑고, 확인·인쇄 단계에서만 보이는 입력/빈칸 배지
     (#gl-review-stat-*)가 있으면 그것도 같이 붙인다.
   · 선택된 칸: #gl-edit-toolbar가 떠 있을 때 그 안의 "N번 칸 편집 중"을
   · 실행취소: general-labels.js가 최소한으로 노출해 둔
     window._glUndoStackLength()/window._glUndo (일반라벨은 실행취소
     스택이 모듈 내부 변수라 DOM에 값이 안 보이므로, 이 두 개만 예외적으로
     최소 노출을 받아 썼다 — 자세한 이유는 general-labels.js의 해당 줄 주석)
   ══════════════════════════════════════════════════════════════ */
(function () {
  const $ = (id) => document.getElementById(id);
  const sb = $("gl-statusbar");
  if (!sb) return;

  function syncGlStatusbarTemplate() {
    const disp = $("gl-template-display");
    const nameEl = $("gl-sb-template-name");
    if (!disp || !nameEl) return;
    const txt = disp.textContent.trim();
    nameEl.textContent = txt || "규격을 선택하세요";
  }

  function syncGlStatusbarCount() {
    const countInfoEl = $("gl-sb-count-info");
    const sepEl = $("gl-sb-count-sep");
    if (!countInfoEl || !sepEl) return;
    const src = $("gl-insp-count") || $("gl-spec-sel-count");
    const m = src && src.textContent && src.textContent.match(/(\d+)\s*칸/);
    if (!m) { countInfoEl.style.display = "none"; sepEl.style.display = "none"; return; }
    const total = m[1];
    /* 확인·인쇄 단계에서만 존재하는 입력/빈칸 배지가 있으면 같이 붙인다 */
    const filledEl = $("gl-review-stat-filled");
    const emptyEl  = $("gl-review-stat-empty");
    const filled = filledEl && filledEl.textContent.trim();
    const empty  = emptyEl && emptyEl.textContent.trim();
    countInfoEl.textContent = (filled && filled !== "-" && empty && empty !== "-")
      ? `${total}칸 · 입력됨 ${filled} · 비어있음 ${empty}`
      : `${total}칸`;
    countInfoEl.style.display = "";
    sepEl.style.display = "";
  }

  function syncGlStatusbarSelection() {
    const toolbar = $("gl-edit-toolbar");
    const idxEl = $("gl-edit-toolbar-idx");
    const selEl = $("gl-sb-sel-info");
    const sepEl = $("gl-sb-sel-sep");
    if (!toolbar || !idxEl || !selEl || !sepEl) return;
    const visible = toolbar.style.display !== "none" && toolbar.style.display !== "";
    if (!visible) { selEl.style.display = "none"; sepEl.style.display = "none"; return; }
    selEl.textContent = `✎ ${idxEl.textContent}번 칸 편집 중`;
    selEl.style.display = "";
    sepEl.style.display = "";
  }

  function syncGlStatusbarUndo() {
    const infoEl = $("gl-sb-undo-info");
    const sepEl = $("gl-sb-undo-sep");
    if (infoEl && sepEl) {
      const len = typeof window._glUndoStackLength === "function" ? window._glUndoStackLength() : 0;
      if (!len) { infoEl.style.display = "none"; sepEl.style.display = "none"; }
      else {
        infoEl.textContent = `↩ 실행취소 ${len}단계`;
        infoEl.style.display = "";
        sepEl.style.display = "";
      }
    }
    /* [신규] 다시 실행(Redo) 뱃지 — 실행취소와 같은 방식으로 동작 */
    const rInfoEl = $("gl-sb-redo-info");
    const rSepEl = $("gl-sb-redo-sep");
    if (rInfoEl && rSepEl) {
      const rlen = typeof window._glRedoStackLength === "function" ? window._glRedoStackLength() : 0;
      if (!rlen) { rInfoEl.style.display = "none"; rSepEl.style.display = "none"; }
      else {
        rInfoEl.textContent = `↪ 다시실행 ${rlen}단계`;
        rInfoEl.style.display = "";
        rSepEl.style.display = "";
      }
    }
  }

  function syncGlStatusbarView() {
    const activeBtn = document.querySelector("#gl-view-mode-seg .gl-view-btn.active");
    const viewEl = $("gl-sb-viewmode");
    if (viewEl) viewEl.textContent = activeBtn ? activeBtn.textContent.trim() : "";
    const zoomSrc = $("gl-zoom-label");
    const zoomEl = $("gl-sb-zoom-label");
    if (zoomEl && zoomSrc) zoomEl.textContent = zoomSrc.textContent;
  }

  function syncGlStatusbar() {
    syncGlStatusbarTemplate();
    syncGlStatusbarCount();
    syncGlStatusbarSelection();
    syncGlStatusbarUndo();
    syncGlStatusbarView();
  }

  $("gl-sb-undo-info")?.addEventListener("click", () => window._glUndo?.());
  $("gl-sb-redo-info")?.addEventListener("click", () => window._glRedo?.()); /* [신규] */

  /* ★GL-RAIL과 같은 트리거(스텝 전환·규격 선택·전체 초기화·모드 전환)에
     얹혀 같이 갱신한다. */
  ["gl-step-tab-spec", "gl-step-tab-design", "gl-step-tab-review",
   "gl-step-spec-next", "gl-clear-btn", "mode-tab-address", "mode-tab-general",
   "gl-view-mode-seg"]
    .forEach((id) => $(id)?.addEventListener("click", () => setTimeout(syncGlStatusbar, 0)));

  const glSbCountObserver = new MutationObserver(syncGlStatusbar);
  ["gl-insp-count", "gl-spec-sel-count", "gl-template-display",
   "gl-review-stat-filled", "gl-review-stat-empty"].forEach((id) => {
    const el = $(id);
    if (el) glSbCountObserver.observe(el, { characterData: true, childList: true, subtree: true });
  });
  /* 칸을 선택/해제할 때 #gl-edit-toolbar의 style.display가 바뀐다 */
  const glSbSelObserver = new MutationObserver(syncGlStatusbar);
  const editToolbarEl = $("gl-edit-toolbar");
  if (editToolbarEl) glSbSelObserver.observe(editToolbarEl, { attributes: true, attributeFilter: ["style"] });

  /* 실행취소 스택은 DOM에 값이 없는 내부 변수라(general-labels.js 참고)
     타이핑·드래그처럼 버튼 클릭이 아닌 동작으로도 계속 바뀐다 — 위
     이벤트/옵저버로 다 잡기 어려워 가볍게 주기적으로만 같이 맞춰준다.
     [수정] 이 배지는 일반라벨 전용인데 인터벌 자체는 무조건 계속
     돌고 있었다(실제 부하는 미미하지만 불필요) — 일반라벨 탭일 때만
     동작하도록 가드를 추가한다. */
  setInterval(() => {
    if (document.body.classList.contains("mode-general")) syncGlStatusbarUndo();
  }, 1000);

  requestAnimationFrame(() => requestAnimationFrame(syncGlStatusbar));
})();

/* ══════════════════════════════════════════════════════════════
   [신규] 인스펙터 아코디언 — 자주 안 쓰는 섹션(라벨 배치 미리보기,
   미니맵) 접기/펼치기. data-collapsible 속성이 붙은 .insp-section만
   대상이며, 상태는 localStorage에 저장돼 다음 방문에도 유지된다.
   (스타일은 style.css의 #inspector .insp-section[data-collapsible] 참고)
   [수정] 일반 라벨 탭 좌측 사이드바의 "텍스트/이미지/배치" 그룹도
   같은 방식(라벨 클릭 → 접힘, localStorage에 상태 저장)으로 접을 수
   있게 확장했다 — "여기서 바꾸면 전체 칸에 적용된다"는 걸 몰라 헷갈린다는
   피드백에 맞춰, 자주 안 쓸 땐 접어서 사이드바를 짧게 볼 수 있다.
   (스타일은 general-labels.css의 .gl-sidebar .gl-sb-collapsible 참고)
   ══════════════════════════════════════════════════════════════ */
(function () {
  const KEY_PREFIX = "inspCollapsed:";
  document.querySelectorAll("#inspector .insp-section[data-collapsible], .gl-sidebar .gl-sb-collapsible[data-collapsible]").forEach((section) => {
    const key = KEY_PREFIX + section.dataset.collapsible;
    const title = section.querySelector(".insp-title, .sb-ctrl-label");
    if (!title) return;
    if (localStorage.getItem(key) === "1") section.classList.add("collapsed");
    title.addEventListener("click", (e) => {
      if (e.target.closest(".sb-scope-badge")) return; /* 배지의 title 툴팁만 보려는 클릭은 접기와 무관하게 둠 */
      section.classList.toggle("collapsed");
      localStorage.setItem(key, section.classList.contains("collapsed") ? "1" : "0");
    });
  });
})();

/* ══════════════════════════════════════════════════════════════
   [신규] 온보딩 모달 — 첫 방문 시 1회, 3단계 마법사 구조를 안내한다.
   기존 shortcut-modal의 열기/닫기 패턴(display:flex → rAF로 .active
   추가, 닫을 땐 .active 제거 후 transitionend에서 display:none)을
   그대로 따라 해 시각적으로 어색함이 없도록 했다.
   ══════════════════════════════════════════════════════════════ */
(function () {
  const modal = document.getElementById("onboarding-modal");
  if (!modal) return;
  const SEEN_KEY = "onboardingSeen";

  function open() {
    modal.style.display = "flex";
    requestAnimationFrame(() => modal.classList.add("active"));
  }
  function close(markSeen) {
    modal.classList.remove("active");
    modal.addEventListener("transitionend", () => {
      if (!modal.classList.contains("active")) modal.style.display = "none";
    }, { once: true });
    if (markSeen) localStorage.setItem(SEEN_KEY, "1");
  }

  document.getElementById("onboarding-start")?.addEventListener("click", () => close(true));
  document.getElementById("onboarding-close")?.addEventListener("click", () => close(true));
  modal.addEventListener("click", (e) => { if (e.target === modal) close(true); });
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && modal.classList.contains("active")) close(true);
  });

  /* "사용법" 모달 안의 "다시 보기" 버튼 — 언제든 재확인 가능 */
  document.getElementById("reopen-onboarding-btn")?.addEventListener("click", () => {
    document.getElementById("shortcut-close")?.click(); /* 사용법 모달을 먼저 닫고 */
    setTimeout(open, 160);
  });

  /* 최초 방문 시에만 자동으로 띄운다 (일반라벨 모드로 처음 들어온 경우는
     제외 — 3단계 마법사는 주소라벨 전용 개념이라 헷갈릴 수 있음) */
  if (localStorage.getItem(SEEN_KEY) !== "1" && !document.body.classList.contains("mode-general")) {
    setTimeout(open, 500); /* 로딩/복원 애니메이션과 겹치지 않도록 약간 지연 */
  }
})();
