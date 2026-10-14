/* ══════════════════════════════════════════════════════════════
   일반라벨 모드 — general-labels.js  (v2 — 브랜드 확장 + 주소라벨 미리보기)
   ⚠️ 기존 index.js는 단 한 줄도 수정하지 않습니다.
   ══════════════════════════════════════════════════════════════ */
(function () {
  "use strict";

  const CFG = window.GENERAL_TEMPLATE_CONFIGS;
  if (!CFG) { console.error("[일반라벨] GENERAL_TEMPLATE_CONFIGS 가 로드되지 않았습니다."); return; }

  /* [신규] "최근 사용" 규격 — 매번 같은 라벨지를 쓰는 사람이 많으므로,
     Step 1 규격 목록 맨 위에 최근 선택한 규격을 고정해 반복 작업을 줄인다.
     주소라벨 탭(index.js)의 getRecent/saveRecent와 같은 관례(localStorage +
     최신이 맨 앞)를 따르되, 규격 데이터 자체가 서로 다른 네임스페이스
     (GENERAL_TEMPLATE_CONFIGS)라 키 충돌을 피하기 위해 별도 localStorage
     키(glRecentTemplates)를 쓴다. */
  const GL_RECENT_TPL_KEY = "glRecentTemplates";
  const GL_MAX_RECENT_TPL = 5;
  function getGlRecentTemplates() {
    try { return JSON.parse(localStorage.getItem(GL_RECENT_TPL_KEY) || "[]"); } catch { return []; }
  }
  function saveGlRecentTemplate(key) {
    if (!key || !CFG[key]) return;
    let list = getGlRecentTemplates().filter(k => k !== key);
    list.unshift(key);
    if (list.length > GL_MAX_RECENT_TPL) list = list.slice(0, GL_MAX_RECENT_TPL);
    try { localStorage.setItem(GL_RECENT_TPL_KEY, JSON.stringify(list)); } catch {}
  }

  /* ══════════════════════════════════════════════════════════════
     [신규] 표 즐겨찾기(재사용 양식) — "자주 쓰는 양식 저장해서 재사용"
     ──────────────────────────────────────────────────────────────
     칸 구조·병합·열너비/행높이·테두리 설정과 "지금 입력된 글자"까지
     통째로 이름 붙여 localStorage에 저장해 둔다. 라벨 규격이 달라져도
     표 자체는 규격과 무관한 데이터라 어떤 칸에든 그대로 불러올 수
     있다. getGlRecentTemplates()와 같은 패턴(localStorage + JSON)을
     그대로 따른다. ════════════════════════════════════════════ */
  const GL_TABLE_FAV_KEY = "glTableFavorites";
  const GL_MAX_TABLE_FAV = 30;
  function getGlTableFavorites() {
    try { return JSON.parse(localStorage.getItem(GL_TABLE_FAV_KEY) || "[]"); } catch { return []; }
  }
  function saveGlTableFavorites(list) {
    try { localStorage.setItem(GL_TABLE_FAV_KEY, JSON.stringify(list)); } catch {}
  }
  function addGlTableFavorite(name, tbl) {
    const list = getGlTableFavorites();
    list.unshift({ id: "tf" + Date.now() + Math.random().toString(36).slice(2, 6), name, savedAt: Date.now(), table: glTblDeepClone(tbl) });
    if (list.length > GL_MAX_TABLE_FAV) list.length = GL_MAX_TABLE_FAV;
    saveGlTableFavorites(list);
    return list;
  }
  function deleteGlTableFavorite(id) {
    const list = getGlTableFavorites().filter(f => f.id !== id);
    saveGlTableFavorites(list);
    return list;
  }

  /* ── 제조사 표시 순서 (드롭다운 optgroup 정렬용) ─────────────── */
  const MAKER_ORDER = [
    "Formtec (폼텍)", "애니라벨 (프린텍)", "아이라벨 (iLabel)", "iLIFE",
    "오피스디포", "아이라벨 LQ", "삼성레이블", "동방라벨",
    "아펙스 (Apex)", "Avery (에이버리)", "업계 표준 (범용)",
  ];

  /* ── 상태 ──────────────────────────────────────────────────── */
  let currentTplKey  = null;
  let glInitialized  = false;   /* 캔버스/미리보기를 최소 1회 빌드했는지 여부 */
  let contentType    = "text";
  let imageDataUrl   = null;   /* 실제로 칸(라벨)에 반영된 이미지 — '전체 칸에 적용'을 눌러야 채워짐 */
  let stagedImageDataUrl = null; /* 신규: 방금 업로드/드롭했지만 아직 칸에 반영되지 않은 대기 이미지 */
  let cellImages     = {};   /* 신규: 칸별로 전체 이미지와 다른 이미지를 지정 — { idx: dataUrl }
                                  전역 이미지(imageDataUrl)보다 우선하며, 그 칸에만 적용된다.
                                  상단 편집 툴바의 "이 칸 이미지 교체"로 채워진다. */
  let selectedIdx    = null; /* 신규: 캔버스에서 클릭해 선택한 칸 인덱스 — 상단 편집 툴바가 이 칸을 대상으로 동작한다 */
  /* [신규] 여러 칸 한꺼번에 선택 — Shift+클릭(범위 선택)/Ctrl(또는 Cmd)+클릭(개별 토글).
     2칸 이상일 때만 "다중 선택 모드"로 간주하며(1칸 이하는 기존 단일 선택과 동일하게
     동작), 우클릭 메뉴가 일괄 작업 메뉴로 바뀌는 기준도 이 Set의 크기다. */
  let multiSelectedIdxs = new Set();
  let multiSelectAnchor = null; /* Shift+클릭 범위의 고정 시작점 — 마지막 클릭이 아니라 "처음 클릭한 칸"을 기준으로 범위를 다시 계산해야 자연스럽다(탐색기·표계산 프로그램과 동일한 관례) */
  let glViewMode     = "scroll"; /* [변경] 기본값을 "fit"→"scroll"로 변경 — 규격에 칸 수가 많으면 "한 페이지" 축소 보기에서 글자가 너무 작아 식별이 어렵다는 피드백에 따라, 처음 들어왔을 때부터 실제 인쇄 크기로 보면서 스크롤하도록 함. "한 페이지" 보기는 버튼으로 언제든 전환 가능. */
  let glZoomLevel    = 1.0; /* 신규: "스크롤" 보기에서 쓰는 수동 확대/축소 배율 — 주소라벨 탭 zoomLevel과 동일한 개념 */
  let imageFit       = "contain";
  let contentLayout  = "below";  /* 텍스트+이미지 모드에서 텍스트 위치: below(기본,기존 동작)|above|side|overlay */
  let cellTexts      = {};
  let cellFontSizes  = {};   /* 신규: 칸별 글자 크기 개별 오버라이드 — { idx: multiplier } */
  let cellImgTransforms = {}; /* 신규: 칸별 이미지 위치·확대·자르기 개별 오버라이드
                                  — { idx: {scaleX, scaleY, x, y, croppedSrc?, crop?} }
                                  x/y는 드래그로 옮긴 px 오프셋. scaleX/scaleY는 9개 핸들로 조절한
                                  가로/세로 배율(각 1=원래 크기) — 모서리(대각선) 핸들은 비율을
                                  유지한 채 둘을 함께 키우고, 변(상/하/좌/우) 핸들은 그 방향의
                                  배율만 따로 바꾼다.
                                  croppedSrc/crop은 자르기 모달로 잘라낸 결과(있으면 원본 대신 사용).
                                  오버라이드가 없는 칸은 기존처럼 전역 '이미지 배율/위치' 설정을 그대로 따른다. */
  const IMG_SCALE_MIN = 0.4, IMG_SCALE_MAX = 3; /* 칸별 핸들 확대/축소 범위 — 상단 '이미지 배율'(40~100%)과 하한을 맞춤 */
  let commonText     = "";
  let glFontSizeMultiplier = 1.0;
  const CELL_FONT_STEP = 0.1, CELL_FONT_MIN = 0.2, CELL_FONT_MAX = 9.4; /* ★FONTSIZE-RANGE: 8~18pt → 4~96pt로 확대. gl-font-size-select 옵션 범위와 동일 매핑(pt = mult*10+2) */

  /* [신규] 텍스트 속성 확장(전역) — 색상/굵기/기울임/밑줄/줄간격/자간.
     기본값은 이전 하드코딩 렌더링과 완전히 동일(검정, 보통 굵기, 줄간격 125%,
     자간 없음)해서 이 기능을 쓰지 않으면 결과물이 전혀 달라지지 않는다. */
  const DEFAULT_GL_TEXT_COLOR = "#000000";
  let textColor      = DEFAULT_GL_TEXT_COLOR;
  let textBold       = false;
  let textItalic     = false;
  let textUnderline  = false;
  let lineHeight     = 1.25;
  let letterSpacing  = 0; /* px, 0이면 "자간 없음"(기본) */

  /* [신규] 칸별 개별 오버라이드 맵 — cellTexts/cellFontSizes/cellImages/
     cellImgTransforms와 같은 패턴. 칸을 더블클릭해 여는 "칸 개별 편집"
     모달에서만 채워지며, 값이 없는 칸은 항상 위 전역 설정을 그대로 따른다. */
  let cellFontFamilies = {}; /* idx: 폰트 문자열 */
  let cellColors        = {}; /* idx: '#rrggbb' */
  let cellStyles         = {}; /* idx: {bold, italic, underline} */
  let cellAligns          = {}; /* idx: {h:'left'|'center'|'right', v:'top'|'middle'|'bottom'} */
  /* [신규] 칸별 콘텐츠 타입 오버라이드 — 'text'|'image'|'both' 중 하나가
     지정되면 왼쪽 사이드바의 전역 "출력 내용"(텍스트/이미지/텍스트+이미지)
     대신 이 칸에서만 그 값을 따른다. 값이 없으면(대부분의 칸) 항상 전역
     설정을 그대로 따른다 — cellFontSizes 등과 완전히 같은 설계. */
  let cellContentTypes = {};
  const CONTENT_TYPE_LABEL = { text: "텍스트", image: "이미지", both: "텍스트+이미지" };
  /* 어떤 칸이 실제로 어떤 콘텐츠 타입을 써야 하는지 한곳에서 계산 —
     buildCell·칸 개별 편집 패널의 탭 전환·확대 미리보기가 모두 이 함수
     하나만 참조해 항상 같은 결론을 내리게 한다. */
  function glEffContentType(idx) {
    return (idx != null && cellContentTypes[idx]) || contentType;
  }
  /* [신규] 칸별 "텍스트+이미지 배치" 오버라이드 — cellContentTypes와
     완전히 같은 패턴. 사이드바의 전역 contentLayout(below/above/side/
     overlay)과 다르게 이 칸 하나만 배치를 바꾸고 싶을 때 쓴다. 값이
     없는 칸은 항상 전역 설정을 그대로 따른다. */
  let cellContentLayouts = {};
  const LAYOUT_LABEL = { below: "아래", above: "위", side: "옆(우)", "side-left": "옆(좌)", overlay: "겹침" };
  function glEffContentLayout(idx) {
    return (idx != null && cellContentLayouts[idx]) || contentLayout;
  }
  /* [신규] 이 칸의 "실제 적용되는" 글꼴·크기·색상·굵기/기울임/밑줄을 한
     곳에서 계산 — 칸 개별 편집 패널과 상단 편집 툴바(캔버스에서 칸을
     선택했을 때 뜨는 빠른 서식 컨트롤)가 모두 이 함수 하나만 참조해
     항상 같은 값을 보여준다(glEffContentType/Layout과 같은 설계). */
  function glEffTextFormat(idx) {
    return {
      fam: (idx != null && cellFontFamilies[idx]) || els.fontSelect.value,
      fsz: (idx != null && cellFontSizes[idx] != null) ? cellFontSizes[idx] : glFontSizeMultiplier,
      col: (idx != null && cellColors[idx]) || textColor,
      sty: (idx != null && cellStyles[idx]) || { bold: textBold, italic: textItalic, underline: textUnderline },
    };
  }

  /* [신규] 칸별 "표" — 이 칸의 텍스트를 일반 문단 대신 표(행×열)로 보여준다.
     idx: { rows: [ [셀텍스트, 셀텍스트, ...], ... ] } 형태의 아주 단순한
     2차원 배열. 표가 있는 칸은 cellTexts의 일반 텍스트 대신 이 표를
     렌더링한다("이 칸 개별 편집" 패널의 "표" 도구로만 만들고 고친다 —
     칸을 많이 인쇄하는 라벨 특성상 캔버스에서 직접 표를 조작하게 하면
     자칫 실수로 표가 깨지기 쉬워, 편집은 패널에서만 하도록 범위를
     좁혔다). 폰트·색상·굵기/기울임/밑줄·정렬은 기존 cellTextStyleAttr가
     그대로 부모 div에 적용해 표 셀에도 CSS 상속으로 자연스럽게 먹는다. */
  let cellTables = {};

  /* ══════════════════════════════════════════════════════════════
     [신규] 표 데이터 모델 v2 — 셀 병합(rowSpan/colSpan) 지원
     ──────────────────────────────────────────────────────────────
     예전 모델: { rows: [["글자","글자"], ["글자","글자"]] } — 문자열만.
     새 모델:   { rows: [[cell,cell],[cell,null]], colWidths:[50,50],
                  rowHeights:[null,32], widthPct:100 }
       - cell = { text, rowSpan, colSpan } — 병합의 "주인(왼쪽 위)" 칸.
       - null = 병합에 "덮인" 칸(그 칸을 덮고 있는 주인 칸의 rowSpan/
         colSpan 범위 안에 있다는 뜻) — 실제 HTML로 칠 때는 건너뛴다.
         (HTML <table>의 rowspan/colspan이 동작하는 방식과 완전히 같다.)
       - rowHeights: 행마다 높이(px). null이면 "자동"(글자 양에 맞춤).
       - widthPct: 표 전체가 칸(라벨) 안에서 차지하는 가로 폭(%,
         20~100). null/100이면 칸 너비를 꽉 채움 — [신규] "표 크기 자체"를
         줄이고 싶다는 요청에 대응해 추가했다.
     예전에 저장된 표(문자열 그리드)는 glTblNormalize()가 자동으로
     이 새 모델로 변환해 준다 — 기존 저장물이 계속 그대로 열린다. ════ */
  function glTblNewCell(text) { return { text: text || "", rowSpan: 1, colSpan: 1, bg: null, edge: null }; }
  /* [신규] 칸의 한 변(top/right/bottom/left)에 표 전체 기본값과 다른
     "이 변만의" 종류·굵기·색을 지정한다. 이전에는 noBorder:{top:true,...}
     처럼 "보임/숨김" 불리언만 있었는데, 그러면 그 변의 선을 없애는 것만
     가능하고 "다른 종류·굵기로 바꾸는" 건 할 수 없었다. 이제
     edge.top = {w, s, c} 형태로 굵기(w, px)·종류(s, css border-style)·
     색(c, hex 문자열 | null=글자색 그대로)을 각각 담아, 그 변에 한해서만
     표 기본 테두리 설정을 덮어쓴다. 값이 없으면(null) 표 전체 기본값을
     그대로 따른다. */
  function glTblNormEdgeSide(v) {
    if (!v || typeof v !== "object") return null;
    return { w: (typeof v.w === "number" && v.w >= 0) ? v.w : 1, s: v.s || "solid", c: v.c || null };
  }
  function glTblNormEdge(edge) {
    if (!edge) return null;
    const out = {};
    let any = false;
    ["top", "right", "bottom", "left"].forEach(side => {
      const v = glTblNormEdgeSide(edge[side]);
      if (v) { out[side] = v; any = true; }
    });
    return any ? out : null;
  }
  function glTblNormalize(raw) {
    if (!raw || !raw.rows || !raw.rows.length) return raw;
    const rows = raw.rows.map(row => row.map(cell => {
      if (cell === null) return null;
      if (typeof cell === "string") return glTblNewCell(cell);
      if (cell && typeof cell === "object") {
        /* [하위호환] 예전에 저장된 noBorder:{top:true,...}("숨김" 불리언)는
           그 변에 굵기 0·종류 "없음"인 edge 오버라이드로 그대로 옮겨서,
           예전에 "이 변 숨기기"를 눌러둔 표를 열어도 그대로 안 보이는
           상태가 유지되게 한다. */
        let edge = glTblNormEdge(cell.edge);
        if (!edge && cell.noBorder) {
          const legacy = {};
          let any = false;
          ["top", "right", "bottom", "left"].forEach(side => {
            if (cell.noBorder[side]) { legacy[side] = { w: 0, s: "none", c: null }; any = true; }
          });
          if (any) edge = legacy;
        }
        return { text: cell.text || "", rowSpan: cell.rowSpan || 1, colSpan: cell.colSpan || 1, bg: cell.bg || null, edge };
      }
      return glTblNewCell("");
    }));
    const tbl = {
      rows,
      colWidths: Array.isArray(raw.colWidths) ? raw.colWidths.slice() : null,
      rowHeights: Array.isArray(raw.rowHeights) ? raw.rowHeights.slice() : null,
      widthPct: (typeof raw.widthPct === "number" && raw.widthPct > 0) ? raw.widthPct : null,
      /* [신규] 표 전체 기본 테두리 종류/굵기/색상 — "셀 외곽선의 종류와
         굵기 조절 가능하게" 피드백 대응. 칸별로 특정 변만 다르게 지정하고
         싶으면 위 cell.edge로 그 칸의 해당 변만 덮어쓸 수 있다(아래
         "적용 위치" 버튼들이 이 두 가지를 함께 다룬다). borderColor가
         없으면(null) 글자색(currentColor)을 그대로 따라간다. */
      borderWidth: (typeof raw.borderWidth === "number" && raw.borderWidth >= 0) ? raw.borderWidth : 1,
      borderStyle: raw.borderStyle || "solid",
      borderColor: raw.borderColor || null,
    };
    glTblNormalizeColWidths(tbl);
    glTblNormalizeRowHeights(tbl);
    return tbl;
  }
  function glTblDims(tbl) { return { R: tbl.rows.length, C: tbl.rows[0] ? tbl.rows[0].length : 0 }; }
  function glTblNormalizeColWidths(tbl) {
    const { C } = glTblDims(tbl);
    if (!C) { tbl.colWidths = []; return; }
    if (!tbl.colWidths || tbl.colWidths.length !== C) {
      tbl.colWidths = new Array(C).fill(+(100 / C).toFixed(2));
      return;
    }
    const sum = tbl.colWidths.reduce((a, b) => a + b, 0) || 1;
    tbl.colWidths = tbl.colWidths.map(w => +(w * 100 / sum).toFixed(2));
  }
  /* colWidths(비율, 항상 합 100)와 달리 rowHeights는 각 행이 서로 독립적인
     "절대값(px) 또는 자동"이라 정규화가 훨씬 단순하다 — 행 개수만 맞춘다. */
  function glTblNormalizeRowHeights(tbl) {
    const { R } = glTblDims(tbl);
    if (!tbl.rowHeights || tbl.rowHeights.length !== R) {
      tbl.rowHeights = new Array(R).fill(null);
    }
  }
  /* (r,c) 위치를 실제로 소유한 "주인" 칸을 찾는다 — (r,c) 자신이 주인이면
     그 자리를, null(덮인 칸)이면 왼쪽 위 방향으로 훑어 rowSpan/colSpan
     범위가 (r,c)를 포함하는 주인 칸을 찾아 돌려준다. */
  function glTblMasterAt(tbl, r, c) {
    for (let mr = r; mr >= 0; mr--) {
      for (let mc = c; mc >= 0; mc--) {
        const cell = tbl.rows[mr] && tbl.rows[mr][mc];
        if (cell && mr + cell.rowSpan > r && mc + cell.colSpan > c) return { r: mr, c: mc, cell };
      }
    }
    return null;
  }
  function glTblCloneEdge(edge) {
    if (!edge) return null;
    const out = {};
    ["top", "right", "bottom", "left"].forEach(side => { if (edge[side]) out[side] = Object.assign({}, edge[side]); });
    return out;
  }
  function glTblDeepClone(tbl) {
    return {
      rows: tbl.rows.map(row => row.map(cell => cell ? { text: cell.text, rowSpan: cell.rowSpan, colSpan: cell.colSpan, bg: cell.bg || null, edge: glTblCloneEdge(cell.edge) } : null)),
      colWidths: tbl.colWidths ? tbl.colWidths.slice() : null,
      rowHeights: tbl.rowHeights ? tbl.rowHeights.slice() : null,
      widthPct: tbl.widthPct || null,
      borderWidth: (typeof tbl.borderWidth === "number") ? tbl.borderWidth : 1,
      borderStyle: tbl.borderStyle || "solid",
      borderColor: tbl.borderColor || null,
    };
  }
  /* 행 삽입 — at번째 자리에 새 행을 끼워 넣는다(기존 at행부터는 한 칸씩
     밀림). 삽입 지점이 세로 병합의 "한가운데"를 지나가면(그 병합이 삽입
     지점 위아래를 이미 잇고 있었다면) 표가 깨지지 않도록 그 병합을
     한 칸 늘려서 새 행도 함께 덮게 한다 — 워드/구글독스와 동일한 동작. */
  function glTblInsertRow(tbl, at) {
    const { R, C } = glTblDims(tbl);
    at = Math.max(0, Math.min(at, R));
    const newRow = new Array(C).fill(null).map(() => glTblNewCell(""));
    if (at > 0 && at < R) {
      const extended = new Set();
      for (let c = 0; c < C; c++) {
        const found = glTblMasterAt(tbl, at - 1, c);
        if (found && (found.r + found.cell.rowSpan) > at) {
          if (!extended.has(found.cell)) { found.cell.rowSpan += 1; extended.add(found.cell); }
          newRow[c] = null;
        }
      }
    }
    tbl.rows.splice(at, 0, newRow);
    if (tbl.rowHeights) tbl.rowHeights.splice(at, 0, null);
  }
  /* 행 삭제 — 지운 행의 한가운데를 지나던 세로 병합은 한 칸 줄이고(칸이
     통째로 없어지지 않게), 그 병합이 지운 행에서 "시작"했다면 다음 행으로
     주인 자리를 옮겨 병합이 끊기지 않게 한다. 표는 최소 1행 유지. */
  function glTblDeleteRow(tbl, at) {
    const { R, C } = glTblDims(tbl);
    if (R <= 1) return false;
    const row = tbl.rows[at];
    const shrunk = new Set();
    for (let c = 0; c < C; c++) {
      const cell = row[c];
      if (cell === null) {
        const found = glTblMasterAt(tbl, at, c);
        if (found && found.r < at && !shrunk.has(found.cell)) {
          found.cell.rowSpan = Math.max(1, found.cell.rowSpan - 1);
          shrunk.add(found.cell);
        }
      } else if (cell.rowSpan > 1) {
        const belowRow = tbl.rows[at + 1];
        if (belowRow) belowRow[c] = { text: cell.text, rowSpan: cell.rowSpan - 1, colSpan: cell.colSpan };
      }
    }
    tbl.rows.splice(at, 1);
    if (tbl.rowHeights) tbl.rowHeights.splice(at, 1);
    return true;
  }
  /* 열 삽입/삭제 — 행 삽입/삭제와 완전히 대칭인 로직(가로 병합 기준). */
  function glTblInsertCol(tbl, at) {
    const { R, C } = glTblDims(tbl);
    at = Math.max(0, Math.min(at, C));
    const extended = new Set();
    for (let r = 0; r < R; r++) {
      let cell = glTblNewCell("");
      if (at > 0 && at < C) {
        const found = glTblMasterAt(tbl, r, at - 1);
        if (found && (found.c + found.cell.colSpan) > at) {
          if (!extended.has(found.cell)) { found.cell.colSpan += 1; extended.add(found.cell); }
          cell = null;
        }
      }
      tbl.rows[r].splice(at, 0, cell);
    }
    if (tbl.colWidths) tbl.colWidths.splice(at, 0, +(100 / (tbl.colWidths.length + 1)).toFixed(2));
    glTblNormalizeColWidths(tbl);
  }
  function glTblDeleteCol(tbl, at) {
    const { R, C } = glTblDims(tbl);
    if (C <= 1) return false;
    const shrunk = new Set();
    for (let r = 0; r < R; r++) {
      const cell = tbl.rows[r][at];
      if (cell === null) {
        const found = glTblMasterAt(tbl, r, at);
        if (found && found.c < at && !shrunk.has(found.cell)) {
          found.cell.colSpan = Math.max(1, found.cell.colSpan - 1);
          shrunk.add(found.cell);
        }
      } else if (cell.colSpan > 1) {
        tbl.rows[r][at + 1] = { text: cell.text, rowSpan: cell.rowSpan, colSpan: cell.colSpan - 1 };
      }
      tbl.rows[r].splice(at, 1);
    }
    if (tbl.colWidths) tbl.colWidths.splice(at, 1);
    glTblNormalizeColWidths(tbl);
    return true;
  }
  /* 셀 병합 — 선택한 사각 범위(r1,c1)~(r2,c2) 안의 칸들이 전부 "깔끔하게"
     범위 안에서만 겹치는 경우(범위 밖으로 삐져나가는 기존 병합이 없는
     경우)에만 허용한다 — 안 그러면 표 구조가 뒤틀릴 수 있어서다. 병합된
     칸들의 글자는 줄바꿈으로 이어붙여 내용이 사라지지 않게 한다. */
  function glTblCanMerge(tbl, r1, c1, r2, c2) {
    if (r1 === r2 && c1 === c2) return false;
    for (let r = r1; r <= r2; r++) {
      for (let c = c1; c <= c2; c++) {
        const found = glTblMasterAt(tbl, r, c);
        if (!found) return false;
        if (found.r < r1 || found.c < c1 || (found.r + found.cell.rowSpan - 1) > r2 || (found.c + found.cell.colSpan - 1) > c2) return false;
      }
    }
    return true;
  }
  function glTblMergeRange(tbl, r1, c1, r2, c2) {
    if (r1 > r2) { const t = r1; r1 = r2; r2 = t; }
    if (c1 > c2) { const t = c1; c1 = c2; c2 = t; }
    if (!glTblCanMerge(tbl, r1, c1, r2, c2)) return false;
    const seen = new Set();
    const texts = [];
    const originBg = tbl.rows[r1][c1] && tbl.rows[r1][c1].bg;
    for (let r = r1; r <= r2; r++) {
      for (let c = c1; c <= c2; c++) {
        const cell = tbl.rows[r][c];
        if (cell && !seen.has(cell)) { seen.add(cell); if (cell.text) texts.push(cell.text); }
      }
    }
    const merged = glTblNewCell(texts.join("\n"));
    merged.rowSpan = r2 - r1 + 1;
    merged.colSpan = c2 - c1 + 1;
    merged.bg = originBg || null;
    for (let r = r1; r <= r2; r++) {
      for (let c = c1; c <= c2; c++) tbl.rows[r][c] = (r === r1 && c === c1) ? merged : null;
    }
    return true;
  }
  /* 셀 분할 — 병합됐던 칸을 다시 1×1 칸들로 되돌린다. 원래 글자는 왼쪽
     위(원래 자리)에만 남기고, 나머지는 빈 칸으로 만든다. */
  function glTblSplitCell(tbl, r, c) {
    const cell = tbl.rows[r][c];
    if (!cell || (cell.rowSpan <= 1 && cell.colSpan <= 1)) return false;
    const { rowSpan, colSpan, text, bg, edge } = cell;
    for (let rr = r; rr < r + rowSpan; rr++) {
      for (let cc = c; cc < c + colSpan; cc++) {
        const isOrigin = rr === r && cc === c;
        const nc = glTblNewCell(isOrigin ? text : "");
        if (isOrigin) { nc.bg = bg || null; nc.edge = glTblCloneEdge(edge); }
        tbl.rows[rr][cc] = nc;
      }
    }
    return true;
  }
  /* 표 전체를 <tr><td rowspan colspan>...</td></tr>로 걸어 다니며 콜백을
     호출 — 실제 화면 표시(renderCellTableHtml)와 편집기(renderCellTableEditor)가
     이 함수 하나만 같이 써서 항상 같은 구조로 그려지게 한다. */
  function glTblWalk(tbl, cellFn, rowAttrFn) {
    const { R, C } = glTblDims(tbl);
    let html = "";
    for (let r = 0; r < R; r++) {
      let rowHtml = "";
      for (let c = 0; c < C; c++) {
        const cell = tbl.rows[r][c];
        if (cell === null) continue;
        rowHtml += cellFn(cell, r, c);
      }
      const rowAttrs = rowAttrFn ? rowAttrFn(r) : "";
      html += "<tr" + rowAttrs + ">" + rowHtml + "</tr>";
    }
    return html;
  }
  /* 표 렌더링 — text/both 두 분기가 모두 이 함수 하나만 쓴다.
     t가 없거나 행이 비어 있으면 빈 문자열(표 없음)을 돌려준다.
     [수정] 셀 병합(rowspan/colspan)·열 너비(colgroup)·행 높이(각 <tr>의
     style="height")·표 전체 너비(widthPct)·칸 안 줄바꿈(white-space:
     pre-wrap이 텍스트의 \n을 그대로 줄바꿈으로 그려줌)을 지원하도록 새
     표 모델(glTblNormalize) 기반으로 다시 작성.
     [신규] editable=true면 각 <td>(병합의 "주인" 칸만 — 덮인 칸은 애초에
     렌더링되지 않는다)에 contenteditable="plaintext-only"와 data-r/data-c를
     달아, 캔버스(메인 격자·칸 포커스 확대뷰 모두)에서 표 칸을 클릭해
     바로 타이핑할 수 있게 한다. plaintext-only를 쓰는 이유는 기존
     .gl-cell-text 클론 편집과 같다 — Enter가 <div>/<br> 같은 중첩 태그
     없이 순수 개행 문자만 남겨 white-space:pre-wrap과 잘 맞고, 표의
     행/열/병합 구조를 바꾸는 브라우저 네이티브 편집 명령이 끼어들 여지가
     없다(각 칸이 독립된 최소 단위 편집 영역이라, 표 전체를
     contenteditable로 감쌀 때와 달리 행 삭제·셀 병합 같은 구조적 변경이
     실수로 일어나지 않는다). 인쇄/검토 미리보기로 이 페이지를 복제하는
     glClonePageInto()는 clone 안의 모든 [contenteditable]을 이미
     자동으로 꺼주므로 별도 처리가 필요 없다. */
  function renderCellTableHtml(raw, editable) {
    const t = glTblNormalize(raw);
    if (!t || !t.rows || !t.rows.length) return "";
    const colgroup = "<colgroup>" + t.colWidths.map(w => '<col style="width:' + w + '%">').join("") + "</colgroup>";
    const body = glTblWalk(t, (cell, r, c) => {
      const attrs = (cell.rowSpan > 1 ? ' rowspan="' + cell.rowSpan + '"' : "") + (cell.colSpan > 1 ? ' colspan="' + cell.colSpan + '"' : "");
      const styleParts = [];
      if (cell.bg) styleParts.push("background:" + cell.bg);
      /* [신규] 칸의 특정 변만 표 기본 테두리와 다른 종류·굵기·색을 쓰도록
         지정돼 있으면(cell.edge), 그 변에 한해 인라인 border-<방향>으로
         표 전체 공통 설정(CSS 변수 기반)을 덮어쓴다. 나머지 변은 손대지
         않아 표 기본값을 그대로 따른다. */
      if (cell.edge) {
        ["top", "right", "bottom", "left"].forEach(side => {
          const e = cell.edge[side];
          if (e) styleParts.push("border-" + side + ":" + e.w + "px " + e.s + " " + (e.c || "currentColor"));
        });
      }
      const styleAttr = styleParts.length ? ' style="' + styleParts.join(";") + '"' : "";
      const editAttrs = editable ? ' contenteditable="plaintext-only" spellcheck="false" data-r="' + r + '" data-c="' + c + '"' : "";
      return "<td" + attrs + styleAttr + editAttrs + ">" + escHtml(cell.text) + "</td>";
    }, (r) => {
      const h = t.rowHeights && t.rowHeights[r];
      return h ? ' style="height:' + h + 'px"' : "";
    });
    /* [신규] 테두리 종류/굵기/색상을 CSS 커스텀 프로퍼티로 <table>에 실어
       보낸다 — general-labels.css의 .gl-cell-table td가 이 값을
       var(...)로 읽어 적용한다. borderColor가 없으면 변수를 아예 안 써서
       CSS의 기본값(currentColor, 글자색과 자동으로 맞춰짐)을 그대로 쓴다. */
    const borderVars = "--gl-tbl-bw:" + t.borderWidth + "px;--gl-tbl-bs:" + t.borderStyle + ";" +
      (t.borderColor ? "--gl-tbl-bc:" + t.borderColor + ";" : "");
    const widthStyle = ' style="width:' + (t.widthPct || 100) + '%;margin:0 auto;' + borderVars + '"';
    return '<table class="gl-cell-table"' + widthStyle + '>' + colgroup + "<tbody>" + body + "</tbody></table>";
  }

  /* [신규] 선택 범위 안에서 side(top/right/bottom/left) 방향으로 "바깥
     가장자리"에 걸친 주인 칸들만 골라낸다 — 한글·워드의 테두리 버튼과
     같은 대상 판정. 안쪽에서 서로 맞닿은 칸들끼리의 경계선은 대상이
     아니다(그건 아래 glTblPaintInside*가 담당). */
  function glTblCellsOnSelEdge(tbl, sel, side) {
    const cells = [];
    const seen = new Set();
    for (let r = sel.r1; r <= sel.r2; r++) {
      for (let c = sel.c1; c <= sel.c2; c++) {
        const found = glTblMasterAt(tbl, r, c);
        if (!found || seen.has(found.cell)) continue;
        seen.add(found.cell);
        const top = found.r, left = found.c, bottom = found.r + found.cell.rowSpan - 1, right = found.c + found.cell.colSpan - 1;
        const onEdge =
          (side === "top" && top === sel.r1) ||
          (side === "bottom" && bottom === sel.r2) ||
          (side === "left" && left === sel.c1) ||
          (side === "right" && right === sel.c2);
        if (onEdge) cells.push(found.cell);
      }
    }
    return cells;
  }
  function glTblPaintEdge(cell, side, val) {
    if (val) { if (!cell.edge) cell.edge = {}; cell.edge[side] = val; }
    else if (cell.edge) { delete cell.edge[side]; if (!Object.keys(cell.edge).length) cell.edge = null; }
  }
  /* val=null이면 "표 기본값을 다시 따르게"(칸별 지정 해제), val={w,s,c}면
     그 값으로 칠한다. 아래 4개는 "적용 위치" 버튼 하나하나에 대응한다. */
  function glTblPaintSide(tbl, sel, side, val) {
    const cells = glTblCellsOnSelEdge(tbl, sel, side);
    cells.forEach(cell => glTblPaintEdge(cell, side, val));
    return cells.length > 0;
  }
  function glTblPaintOuter(tbl, sel, val) {
    let any = false;
    ["top", "right", "bottom", "left"].forEach(side => { if (glTblPaintSide(tbl, sel, side, val)) any = true; });
    return any;
  }
  /* 선택 범위 "안쪽"에서 서로 맞닿은 칸들 사이의 경계선 — 위 칸의 아래쪽
     변과 아래 칸의 위쪽 변(또는 왼쪽 칸의 오른쪽 변과 오른쪽 칸의 왼쪽
     변)을 같은 값으로 동시에 칠해, 두 칸이 서로 다른 값을 갖고 있어서
     생기는 충돌(border-collapse가 임의로 승자를 고르는 상황)을 애초에
     만들지 않는다. */
  function glTblPaintInner(tbl, sel, val) {
    let any = false;
    for (let rb = sel.r1; rb < sel.r2; rb++) {
      for (let c = sel.c1; c <= sel.c2; c++) {
        const above = glTblMasterAt(tbl, rb, c), below = glTblMasterAt(tbl, rb + 1, c);
        if (above) { glTblPaintEdge(above.cell, "bottom", val); any = true; }
        if (below) { glTblPaintEdge(below.cell, "top", val); any = true; }
      }
    }
    for (let cb = sel.c1; cb < sel.c2; cb++) {
      for (let r = sel.r1; r <= sel.r2; r++) {
        const left = glTblMasterAt(tbl, r, cb), right = glTblMasterAt(tbl, r, cb + 1);
        if (left) { glTblPaintEdge(left.cell, "right", val); any = true; }
        if (right) { glTblPaintEdge(right.cell, "left", val); any = true; }
      }
    }
    return any;
  }
  function glTblPaintAll(tbl, sel, val) {
    const a = glTblPaintOuter(tbl, sel, val);
    const b = glTblPaintInner(tbl, sel, val);
    return a || b;
  }
  /* "기본값" 버튼 — 선택 범위 안 칸들의 칸별 테두리 지정을 통째로 지워
     표 전체 기본 테두리를 다시 그대로 따르게 한다. */
  function glTblResetEdges(tbl, sel) {
    const seen = new Set();
    let any = false;
    for (let r = sel.r1; r <= sel.r2; r++) {
      for (let c = sel.c1; c <= sel.c2; c++) {
        const found = glTblMasterAt(tbl, r, c);
        if (!found || seen.has(found.cell)) continue;
        seen.add(found.cell);
        if (found.cell.edge) { found.cell.edge = null; any = true; }
      }
    }
    return any;
  }


  /* [신규] 칸 더블클릭 → 개별 편집 모달의 임시 상태 — "저장"을 눌러야
     실제 cellFontFamilies/cellColors/cellStyles/cellAligns에 반영된다. */
  let glCellEditIdx = null;
  let glCellEditPendingAlign = { h: "center", v: "middle" };
  let glCellEditPendingStyle = { bold: false, italic: false, underline: false };
  /* [신규] 되돌리기/다시실행이 개별편집패널이 열려 있는 화면도 최신 상태로
     다시 채울 수 있게 하는 훅. initGlCellFocusPanel()이 실제 함수를
     채워 넣는다(그 스코프 안의 cellFocusTab·openCellFocusPanel 등을 써야
     하는데, glApplySnapshot은 그 함수들이 없는 바깥 스코프에 있어서
     직접 부를 수 없기 때문). */
  let glRefreshCellFocusPanelHook = null;
  /* [버그 수정] 캔버스(메인 격자/확대뷰)에서 표 칸에 타이핑할 때 "칸 개별
     편집" 패널의 표 그리드도 같이 맞춰야 하는데, renderCellTableEditor는
     initGlCellFocusPanel() 안쪽 스코프에만 있어 바깥(캔버스 렌더링 쪽인
     wireCanvasTableEditing)에서 직접 부를 수 없었다 — `typeof
     renderCellTableEditor === "function"` 검사가 스코프 문제로 항상
     실패해(바깥에서는 그 이름 자체가 안 보임) 사이드 패널이 캔버스 입력을
     따라가지 못했다. 이 훅에 실제 함수를 연결해 해결한다. */
  let glRefreshTableEditorGridHook = null;
  /* [신규] 캔버스(메인 격자/확대뷰)에서 표 칸을 클릭(Shift+클릭이면 범위
     확장)했을 때 선택 상태(glTblActive/glTblSel, 사이드 패널·병합/분할/
     테두리/Ctrl+방향키 크기조절이 모두 참조하는 바로 그 상태)를 갱신하는
     훅 — 실제 선택 계산은 initGlCellFocusPanel() 스코프 안에서만 할 수
     있어서(글쓴 함수가 그 안에 있음) 훅으로 연결한다. */
  let glCanvasTableSelectHook = null;

  /* ══════════════════════════════════════════════════════════════
     신규: 되돌리기(Ctrl+Z)
     ──────────────────────────────────────────────────────────────
     칸 텍스트·글자크기·개별 이미지·위치/크기/자르기, 그리고 "전체 칸에
     적용"·"전체 초기화" 같은 실수하기 쉬운 동작을 한 단계 전 상태로
     되돌릴 수 있다. 최근 GL_UNDO_MAX단계까지 기억한다.
     드래그(이미지 이동/크기조절)처럼 연속으로 값이 바뀌는 동작은 매
     순간마다 쌓지 않고 "그 동작을 시작하기 직전" 딱 한 번만 저장해,
     Ctrl+Z 한 번에 드래그 시작 전으로 자연스럽게 돌아가게 한다. */
  let glUndoStack = [];
  /* [신규] 다시 실행(Redo) — 주소라벨 탭엔 이미 있는데 일반라벨엔 없었다.
     실행취소로 스냅샷을 되돌릴 때마다 "되돌리기 직전" 상태를 이 스택에
     쌓아 두고, 다시실행을 누르면 그걸 그대로 복원한다. 새로운 변경을
     하나라도 저장(glPushUndo)하면 그 시점부터의 "미래"는 의미가 없어지므로
     redo 스택은 비운다(표준적인 되돌리기/다시실행 동작과 동일). */
  let glRedoStack = [];
  const GL_UNDO_MAX = 30;
  /* 메모리 최적화: cellImgTransforms는 칸이 바뀔 때마다 항상
     Object.assign({}, ...)로 "새 객체를 통째로 교체"하거나 delete하는
     방식으로만 쓰이고, 기존 객체의 필드를 직접 mutate하는 곳은 없다
     (grep으로 전수 확인). 즉 예전 스냅샷이 들고 있는 내부 객체는 이후에도
     절대 바뀌지 않으므로, 굳이 JSON.stringify/parse로 깊은 복제를 할
     필요가 없다 — 바깥 맵만 얕은 복사(shallow copy)하면 각 칸의 값(구조
     공유, structural sharing)은 안전하게 재사용된다.
     이전에는 자르기(croppedSrc)로 큰 base64 문자열이 들어간 칸이 있으면
     Ctrl+Z 스택을 쌓을 때마다(글자 하나 고칠 때마다!) 그 base64 전체를
     문자열로 직렬화 → 재구성 하면서 매번 새로 복제해, 이미지가 많아질수록
     메모리 사용량이 급격히 늘어났다. 얕은 복사로 바꾸면 값이 바뀌지 않은
     칸의 이미지 데이터는 메모리를 공유하고, 실제로 늘어나는 건 "바뀐
     칸의 개수"에만 비례한다. */
  function glSnapshotState() {
    return {
      cellTexts:         Object.assign({}, cellTexts),
      cellFontSizes:     Object.assign({}, cellFontSizes),
      cellImages:        Object.assign({}, cellImages),
      cellImgTransforms: Object.assign({}, cellImgTransforms),
      cellFontFamilies:  Object.assign({}, cellFontFamilies),
      cellColors:        Object.assign({}, cellColors),
      cellStyles:        Object.assign({}, cellStyles),
      cellAligns:        Object.assign({}, cellAligns),
      cellContentTypes:  Object.assign({}, cellContentTypes),
      cellTables:        Object.assign({}, cellTables),
      commonText:        commonText,
      imageDataUrl:      imageDataUrl,
    };
  }
  function glPushUndo() {
    glUndoStack.push(glSnapshotState());
    if (glUndoStack.length > GL_UNDO_MAX) glUndoStack.shift();
    glRedoStack.length = 0; /* 새 액션 → 다시실행(미래) 스택은 의미 없어지므로 비운다 */
  }
  function glApplySnapshot(snap) {
    cellTexts         = snap.cellTexts;
    cellFontSizes     = snap.cellFontSizes;
    cellImages        = snap.cellImages;
    cellImgTransforms = snap.cellImgTransforms;
    /* [신규] 이 기능이 생기기 전에 쌓인 스냅샷엔 아래 필드가 없을 수 있어 안전하게 폴백 */
    cellFontFamilies  = snap.cellFontFamilies || {};
    cellColors        = snap.cellColors || {};
    cellStyles         = snap.cellStyles || {};
    cellAligns          = snap.cellAligns || {};
    cellContentTypes    = snap.cellContentTypes || {};
    cellTables           = snap.cellTables || {};
    commonText         = snap.commonText;
    imageDataUrl        = snap.imageDataUrl;
    if (els.textarea) els.textarea.value = commonText;
    renderImageDrop();
    rebuildCanvas();
    updateEditToolbar();
    /* [신규] 개별편집패널이 열려 있는 상태에서 Ctrl+Z/다시실행을 하면 그 안의
       확대 미리보기·입력 필드도 같이 최신 값으로 다시 채운다 — 안 그러면
       실제 데이터는 바뀌었는데 패널 화면만 예전 값 그대로 보여서 마치
       "Ctrl+Z가 안 먹는 것"처럼 보인다. */
    if (typeof glRefreshCellFocusPanelHook === "function") glRefreshCellFocusPanelHook();
  }
  function glUndo() {
    if (!glUndoStack.length) { showToast("더 되돌릴 내용이 없습니다.", "info", 1400); return; }
    glRedoStack.push(glSnapshotState()); /* [신규] 되돌리기 직전 상태를 다시실행용으로 보관 */
    if (glRedoStack.length > GL_UNDO_MAX) glRedoStack.shift();
    const prev = glUndoStack.pop();
    glApplySnapshot(prev);
    showToast("↩️ 이전 상태로 되돌렸습니다.", "success", 1300);
  }
  /* [신규] 다시 실행 — glUndo의 반대. 되돌리기로 인해 밀려난 "미래" 상태를 복원한다. */
  function glRedo() {
    if (!glRedoStack.length) { showToast("다시 실행할 내용이 없습니다.", "info", 1400); return; }
    glUndoStack.push(glSnapshotState()); /* 다시실행 직전 상태를 실행취소용으로 보관 */
    if (glUndoStack.length > GL_UNDO_MAX) glUndoStack.shift();
    const next = glRedoStack.pop();
    glApplySnapshot(next);
    showToast("↪️ 다시 실행했습니다.", "success", 1300);
  }
  /* [신규] ui-refresh.js(하단 상태표시줄의 "↩ 실행취소 N단계" 배지)에서
     실행취소 가능 단계 수를 읽고, 클릭 시 실제로 되돌릴 수 있게 최소한만
     노출한다 — window._glOpenCellFocusPanel 등과 같은 기존 패턴. 배열
     자체가 아니라 "지금 길이"를 함수로 노출하는 이유는, 이후 push/pop으로
     계속 바뀌는 값을 매번 다시 읽어야 하기 때문이다(정적 값이면 스택이
     쌓여도 상태표시줄이 갱신되지 않는다). */
  window._glUndoStackLength = () => glUndoStack.length;
  window._glUndo = glUndo;
  window._glRedoStackLength = () => glRedoStack.length; /* [신규] */
  window._glRedo = glRedo; /* [신규] */

  /* [신규/수정] "지금 포커스가 진짜 텍스트 입력 중인 곳에 있는가"를 판정.
     Ctrl+Z 전역 핸들러가 이 함수를 써서, 텍스트류 입력창(브라우저 자체
     되돌리기가 의미 있는 곳)일 때만 우리 되돌리기를 양보한다. <input type="range">
     (밝기·대비·채도·흑백 슬라이더)나 <input type="color">(글자 색상 피커)처럼
     "값은 바뀌지만 타이핑은 아닌" 컨트롤은 여기 해당하지 않는다 — 이런 걸
     마지막으로 만진 뒤에는 그 요소가 포커스를 그대로 갖고 있는 경우가 많아,
     예전 판정 방식으론 Ctrl+Z가 완전히 먹통이 됐었다. */
  const GL_FREE_TEXT_INPUT_TYPES = ["text", "search", "email", "url", "tel", "password", "number", ""];
  function isGlFreeTextEditTarget(el) {
    if (!el) return false;
    if (el.isContentEditable) return true;
    const tag = el.tagName;
    if (tag === "TEXTAREA" || tag === "SELECT") return true;
    if (tag === "INPUT") {
      const t = (el.getAttribute("type") || "text").toLowerCase();
      return GL_FREE_TEXT_INPUT_TYPES.includes(t);
    }
    return false;
  }

  /* 신규: 텍스트 정렬(칸 안에서의 위치) — 기본값(center/middle)은 기존 중앙정렬과 동일 */
  let textAlignH = "center";   /* left | center | right */
  let textAlignV = "middle";   /* top | middle | bottom */
  /* 신규: 글자 방향(세로쓰기) — 기본값(horizontal)은 기존 동작과 완전히 동일.
     "분류번호(옆면)"처럼 칸 폭이 아주 좁아 가로쓰기가 불가능한 규격(예: 16mm)에
     쓰도록 추가한 전역 설정이다. 현재는 칸별 개별 오버라이드 없이 전역 하나만
     적용된다 — 글자 크기(cellFontSizes)처럼 칸별로 다르게 주는 기능은 이후
     필요해지면 같은 패턴(전역 기본값 + { idx: 값 } 오버라이드 맵)으로 확장 가능하다. */
  let textWritingMode = "horizontal"; /* horizontal | vertical */
  /* 신규: 이미지 크기/위치 — 기본값(100%, center/middle)은 기존 동작과 완전히 동일 */
  let imageScale  = 100;       /* 40~100 (%) */
  let imageAlignH = "center";  /* left | center | right */
  let imageAlignV = "middle";  /* top | middle | bottom */

  /* ── DOM 참조 ──────────────────────────────────────────────── */
  let els = {};
  function cacheEls() {
    els = {
      tabAddr:        document.getElementById("mode-tab-address"),
      tabGeneral:     document.getElementById("mode-tab-general"),
      body:           document.getElementById("general-app-body"),
      templateSelect: document.getElementById("gl-template-select"),
      templateDisplay:document.getElementById("gl-template-display"),
      fontSelect:     document.getElementById("gl-font-select"),
      fontDisplay:    document.getElementById("gl-font-display"),
      fontSizeSelect: document.getElementById("gl-font-size-select"),
      fontSizeDown:   document.getElementById("gl-font-size-down"),
      fontSizeUp:     document.getElementById("gl-font-size-up"),
      contentSeg:     document.getElementById("gl-content-seg"),
      textBlock:      document.getElementById("gl-text-block"),
      imageBlock:     document.getElementById("gl-image-block"),
      textarea:       document.getElementById("gl-textarea"),
      imageInput:     document.getElementById("gl-image-input"),
      bulkImageInput: document.getElementById("gl-bulk-image-input"), /* [신규] */
      bulkImageBtn:   document.getElementById("gl-bulk-image-btn"),   /* [신규] */
      bulkImageProgress: document.getElementById("gl-bulk-image-progress"), /* [신규] */
      importDataInput: document.getElementById("gl-import-data-input"), /* [신규] 엑셀·한글·메모장에서 열 데이터 가져오기 */
      importDataBtn:   document.getElementById("gl-import-data-btn"),  /* [신규] */
      imageDrop:      document.getElementById("gl-image-drop"),
      imagePendingHint: document.getElementById("gl-image-pending-hint"),
      imageHintToggle:  document.getElementById("gl-image-hint-toggle"),   /* 신규 */
      imageHintBody:    document.getElementById("gl-image-hint-body"),    /* 신규 */
      fitSeg:         document.getElementById("gl-fit-seg"),
      layoutBlock:    document.getElementById("gl-layout-block"),
      layoutSeg:      document.getElementById("gl-layout-seg"),
      textAlignGrid:  document.getElementById("gl-text-align-grid"),
      writingSeg:     document.getElementById("gl-writing-seg"),   /* 신규: 글자 방향(가로/세로) */
      imgAlignGrid:   document.getElementById("gl-img-align-grid"),
      imgScaleSelect: document.getElementById("gl-img-scale-select"),
      imgScaleDown:   document.getElementById("gl-img-scale-down"),
      imgScaleUp:     document.getElementById("gl-img-scale-up"),
      canvasScroll:   document.getElementById("gl-canvas-scroll"),
      canvasSpec:     document.getElementById("gl-canvas-spec"),
      viewModeSeg:    document.getElementById("gl-view-mode-seg"), /* 신규: 한 페이지/스크롤 보기 모드 전환 */
      glZoomIn:       document.getElementById("gl-zoom-in"),    /* 신규: 캔버스 확대/축소 */
      glZoomOut:      document.getElementById("gl-zoom-out"),
      glZoomLabel:    document.getElementById("gl-zoom-label"),
      inspMaker:      document.getElementById("gl-insp-maker"),
      inspModel:      document.getElementById("gl-insp-model"),
      inspSize:       document.getElementById("gl-insp-size"),
      inspCount:      document.getElementById("gl-insp-count"),
      inspMargin:     document.getElementById("gl-insp-margin"),
      inspSource:     document.getElementById("gl-insp-source"),
      printBtn:       document.getElementById("gl-print-btn"),
      addAllBtn:      document.getElementById("gl-fill-all-btn"),
      clearBtn:       document.getElementById("gl-clear-btn"),
      /* 신규: 칸 선택 편집 툴바 */
      editToolbar:      document.getElementById("gl-edit-toolbar"),
      editToolbarIdx:   document.getElementById("gl-edit-toolbar-idx"),
      /* [신규] "이 칸" 글자 서식 빠른 조절 — 캔버스에서 칸을 선택하면 바로
         이 칸의 글꼴·크기·색상·굵기/기울임/밑줄을 여기서 바꿀 수 있다.
         (좌측 사이드바는 항상 "전체 칸 공통값"만 다루던 것과의 차이점) */
      editFontSelect:     document.getElementById("gl-edit-font-select"),
      editFontSizeSelect: document.getElementById("gl-edit-fontsize-select"),
      editColorInput:     document.getElementById("gl-edit-color-input"),
      editBoldBtn:        document.getElementById("gl-edit-bold"),
      editItalicBtn:      document.getElementById("gl-edit-italic"),
      editUnderlineBtn:   document.getElementById("gl-edit-underline"),
      editTextResetBtn:   document.getElementById("gl-edit-text-reset"),
      editImgReplaceBtn:document.getElementById("gl-edit-img-replace"),
      editImgCropBtn:   document.getElementById("gl-edit-img-crop"),   /* 신규: 이 칸 이미지 자르기 */
      editImgRotateBtn: document.getElementById("gl-edit-img-rotate"), /* 신규: 이 칸 이미지 90도 회전 */
      editImgFlipXBtn:  document.getElementById("gl-edit-img-flip-x"), /* ★FLIP: 이 칸 이미지 좌우 반전 */
      editImgFlipYBtn:  document.getElementById("gl-edit-img-flip-y"), /* ★FLIP: 이 칸 이미지 상하 반전 */
      editImgResetBtn:  document.getElementById("gl-edit-img-reset"), /* 신규: 이 칸 위치·크기 초기화 */
      editImgClearBtn:  document.getElementById("gl-edit-img-clear"),
      editTextClearBtn: document.getElementById("gl-edit-text-clear"),
      /* [신규] 상단 캔버스 선택 툴바의 표 빠른 도구 */
      editTableGroup:   document.getElementById("gl-edit-table-group"),
      editTableAddRowBtn: document.getElementById("gl-edit-table-add-row"),
      editTableAddColBtn: document.getElementById("gl-edit-table-add-col"),
      editTableDeleteBtn: document.getElementById("gl-edit-table-delete"),
      editFillOthersBtn: document.getElementById("gl-edit-fill-others"), /* [신규] 이 칸을 다른 칸에 채우기 */
      editToolbarCloseBtn: document.getElementById("gl-edit-toolbar-close"),
      cellImageInput:   document.getElementById("gl-cell-image-input"),
      /* 신규: 일반 라벨 칸 우클릭 컨텍스트 메뉴(주소 라벨과 동일한 .ctx-menu 스타일) */
      glCtxMenu:        document.getElementById("gl-context-menu"),
      glCtxGlabelCrop:    document.getElementById("gl-ctx-glabel-crop"),     /* 신규 */
      glCtxGlabelReplace: document.getElementById("gl-ctx-glabel-replace"), /* 신규 */
      glCtxGlabelCopy:    document.getElementById("gl-ctx-glabel-copy"),   /* 신규 */
      glCtxCrop:        document.getElementById("gl-ctx-crop"),
      glCtxResetImg:    document.getElementById("gl-ctx-reset-img"),
      glCtxSepA:        document.getElementById("gl-ctx-sep-a"),
      glCtxReplaceCell: document.getElementById("gl-ctx-replace-cell"),
      glCtxClearImg:    document.getElementById("gl-ctx-clear-img"),
      glCtxReplaceAll:  document.getElementById("gl-ctx-replace-all"),
      glCtxSepB:        document.getElementById("gl-ctx-sep-b"),
      glCtxCopy:        document.getElementById("gl-ctx-copy"),
      glCtxPaste:       document.getElementById("gl-ctx-paste"),
      glCtxFillAllText: document.getElementById("gl-ctx-fill-all-text"), /* [신규] */
      glCtxFillEmptyCells: document.getElementById("gl-ctx-fill-empty-cells"), /* [수정] 이 칸 전체를 빈 칸에 채우기 */
      glCtxSepC:        document.getElementById("gl-ctx-sep-c"),
      glCtxGlabelManage: document.getElementById("gl-ctx-glabel-manage"),
      glUndoBtn: document.getElementById("gl-undo-btn"), /* [신규] 눈에 보이는 되돌리기/다시실행 버튼 */
      glSidebarToggleBtn: document.getElementById("gl-sidebar-toggle"), /* [신규] 왼쪽 사이드바 접기 버튼 */
      glInspectorToggleBtn: document.getElementById("gl-inspector-toggle"), /* [신규] 오른쪽 인스펙터 접기 버튼 */
      glRedoBtn: document.getElementById("gl-redo-btn"),
      glCtxClearText:   document.getElementById("gl-ctx-clear-text"),
      glCtxResetCell:   document.getElementById("gl-ctx-reset-cell"),
      /* [신규] 다중 선택(2칸 이상) 일괄 작업 메뉴 그룹 — 여러 칸을 Shift/Ctrl+클릭으로
         선택한 뒤 그 중 하나를 우클릭하면 평소 메뉴 대신 이 그룹만 보인다. */
      glCtxBatchGroup:  document.getElementById("gl-ctx-batch-group"),
      glCtxBatchLabel:  document.getElementById("gl-ctx-batch-label"),
      glCtxBatchClearText: document.getElementById("gl-ctx-batch-clear-text"),
      glCtxBatchClearImg:  document.getElementById("gl-ctx-batch-clear-img"),
      glCtxBatchReset:     document.getElementById("gl-ctx-batch-reset"),
      /* [신규] 텍스트 속성 확장(전역) — 색상/스타일/줄간격/자간 */
      textColorInput:   document.getElementById("gl-text-color-input"),
      textColorReset:   document.getElementById("gl-text-color-reset-btn"),
      boldBtn:          document.getElementById("gl-bold-toggle-btn"),
      italicBtn:        document.getElementById("gl-italic-toggle-btn"),
      underlineBtn:     document.getElementById("gl-underline-toggle-btn"),
      lineHeightSelect: document.getElementById("gl-line-height-select"),
      letterSpacingSelect: document.getElementById("gl-letter-spacing-select"),
      /* [신규] 칸 더블클릭 → 캔버스 안 "칸 포커스" 패널 */
      cellFocusPanel:      document.getElementById("gl-cell-focus-panel"),
      cellFocusStage:      document.querySelector("#gl-cell-focus-panel .gl-cell-focus-stage"),
      cellFocusPreview:    document.getElementById("gl-cell-focus-preview"),
      cellEditTitle:       document.getElementById("gl-cell-edit-title"),
      cellEditCloseBtn:    document.getElementById("gl-cell-edit-close"),
      cellEditTextCol:     document.getElementById("gl-cell-edit-text-col"),
      cellEditImgCol:      document.getElementById("gl-cell-edit-img-col"),
      cellToolRail:        document.getElementById("gl-cell-tool-rail"), /* [신규] 포토샵풍 세로 도구 레일 */
      cellFocusTabs:       document.getElementById("gl-cell-focus-tabs"), /* [신규] 텍스트/이미지 탭 */
      cellFocusTypeSeg:    document.getElementById("gl-cell-focus-type-seg"), /* [신규] 이 칸만의 콘텐츠 타입 오버라이드 */
      cellFocusTypeHint:   document.getElementById("gl-cell-focus-type-hint"),
      cellFocusLayoutBlock: document.getElementById("gl-cell-focus-layout-block"), /* [신규] 이 칸만의 텍스트+이미지 배치 오버라이드 */
      cellFocusLayoutSeg:   document.getElementById("gl-cell-focus-layout-seg"),
      cellFocusLayoutHint:  document.getElementById("gl-cell-focus-layout-hint"),
      cellEditText:        document.getElementById("gl-cell-edit-text"),
      cellEditFont:        document.getElementById("gl-cell-edit-font"),
      cellEditFontSize:    document.getElementById("gl-cell-edit-fontsize"),
      cellEditColor:       document.getElementById("gl-cell-edit-color"),
      cellEditBold:        document.getElementById("gl-cell-edit-bold"),
      cellEditItalic:      document.getElementById("gl-cell-edit-italic"),
      cellEditUnderline:   document.getElementById("gl-cell-edit-underline"),
      cellEditAlignGrid:   document.getElementById("gl-cell-edit-align-grid"),
      cellEditImgReplaceBtn: document.getElementById("gl-cell-edit-img-replace-btn"),
      cellEditImgLoadBtn:    document.getElementById("gl-cell-edit-img-load-btn"), /* [신규] 탭 본문의 큰 "이미지 불러오기" 버튼 */
      cellEditImgClearBtn:   document.getElementById("gl-cell-edit-img-clear-btn"),
      cellEditImgRotateBtn:  document.getElementById("gl-cell-edit-img-rotate-btn"),   /* [신규] */
      cellEditImgFlipXBtn:   document.getElementById("gl-cell-edit-img-flip-x-btn"),   /* ★FLIP */
      cellEditImgFlipYBtn:   document.getElementById("gl-cell-edit-img-flip-y-btn"),   /* ★FLIP */
      cellEditImgCropBtn:    document.getElementById("gl-cell-edit-img-crop-btn"),     /* [신규] */
      cellEditImgPosResetBtn: document.getElementById("gl-cell-edit-img-posreset-btn"),/* [신규] */
      cellEditImgFilterField: document.getElementById("gl-cell-edit-img-filter-field"), /* [신규] 보정 슬라이더 4종 묶음 */
      cellEditImgBrightness: document.getElementById("gl-cell-edit-img-brightness"),
      cellEditImgContrast:   document.getElementById("gl-cell-edit-img-contrast"),
      cellEditImgSaturate:   document.getElementById("gl-cell-edit-img-saturate"),
      cellEditImgGrayscale:  document.getElementById("gl-cell-edit-img-grayscale"),
      cellEditImgOpacity:    document.getElementById("gl-cell-edit-img-opacity"),
      cellEditImgInvert:     document.getElementById("gl-cell-edit-img-invert"),
      cellEditImgFilterResetBtn: document.getElementById("gl-cell-edit-img-filter-reset-btn"),
      cellEditCancelBtn:   document.getElementById("gl-cell-edit-cancel"),
      cellEditResetBtn:    document.getElementById("gl-cell-edit-reset"),
      cellEditSaveBtn:     document.getElementById("gl-cell-edit-save"),
      /* [신규] 이 칸 텍스트를 표(행×열)로 바꾸는 도구 — "칸 개별 편집" 패널 */
      cellEditTextPlainWrap: document.getElementById("gl-cell-edit-text-plain-wrap"),
      cellTableInsertBtn:  document.getElementById("gl-cell-table-insert-btn"),
      cellTableEditorWrap: document.getElementById("gl-cell-table-editor-wrap"),
      cellTableGrid:       document.getElementById("gl-cell-table-grid"),
      cellTableDeleteBtn:  document.getElementById("gl-cell-table-delete-btn"),
      cellTableInsRowAboveBtn: document.getElementById("gl-cell-table-ins-row-above"),
      cellTableInsRowBelowBtn: document.getElementById("gl-cell-table-ins-row-below"),
      cellTableDelRowBtn:      document.getElementById("gl-cell-table-del-row"),
      cellTableInsColLeftBtn:  document.getElementById("gl-cell-table-ins-col-left"),
      cellTableInsColRightBtn: document.getElementById("gl-cell-table-ins-col-right"),
      cellTableDelColBtn:      document.getElementById("gl-cell-table-del-col"),
      cellTableMergeBtn:       document.getElementById("gl-cell-table-merge"),
      cellTableSplitBtn:       document.getElementById("gl-cell-table-split"),
      cellTableWidthRange:     document.getElementById("gl-cell-table-width-range"), /* [신규] 표 전체 너비 */
      cellTableWidthVal:       document.getElementById("gl-cell-table-width-val"),
      /* [신규] 균등 분배 / 배경색 / 테두리 / 즐겨찾기 */
      cellTableDistColBtn:     document.getElementById("gl-cell-table-dist-col"),
      cellTableDistRowBtn:     document.getElementById("gl-cell-table-dist-row"),
      cellTableBgColor:        document.getElementById("gl-cell-table-bg-color"),
      cellTableBgClearBtn:     document.getElementById("gl-cell-table-bg-clear"),
      cellTableBorderStyle:    document.getElementById("gl-cell-table-border-style"),
      cellTableBorderWidth:    document.getElementById("gl-cell-table-border-width"),
      cellTableBorderWidthVal: document.getElementById("gl-cell-table-border-width-val"),
      cellTableBorderColor:    document.getElementById("gl-cell-table-border-color"),
      cellTableBorderColorClearBtn: document.getElementById("gl-cell-table-border-color-clear"),
      /* [수정] "적용 위치" 버튼 — 위/오른쪽/아래/왼쪽(바깥 가장자리),
         바깥 전체/안쪽 전체/모든 선, 기본값(칸별 지정 해제) */
      cellTableBorderTopBtn:    document.getElementById("gl-cell-table-border-top"),
      cellTableBorderRightBtn:  document.getElementById("gl-cell-table-border-right"),
      cellTableBorderBottomBtn: document.getElementById("gl-cell-table-border-bottom"),
      cellTableBorderLeftBtn:   document.getElementById("gl-cell-table-border-left"),
      cellTableBorderOuterBtn:  document.getElementById("gl-cell-table-border-outer"),
      cellTableBorderInnerBtn:  document.getElementById("gl-cell-table-border-inner"),
      cellTableBorderAllBtn:    document.getElementById("gl-cell-table-border-all"),
      cellTableBorderResetBtn:  document.getElementById("gl-cell-table-border-reset"),
      cellTableFavSelect:      document.getElementById("gl-cell-table-fav-select"),
      cellTableFavApplyBtn:    document.getElementById("gl-cell-table-fav-apply"),
      cellTableFavDeleteBtn:   document.getElementById("gl-cell-table-fav-delete"),
      cellTableFavNameInput:   document.getElementById("gl-cell-table-fav-name"),
      cellTableFavSaveBtn:     document.getElementById("gl-cell-table-fav-save"),
      /* 일반라벨 인스펙터 내 라벨 미리보기 */
      glGridPreview:  document.getElementById("gl-grid-preview"),
      glGridInfo:     document.getElementById("gl-grid-info"),
    };
  }

  /* ── 유틸 ──────────────────────────────────────────────────── */
  function escHtml(s) {
    return String(s == null ? "" : s)
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }
  function mm(v) { return v + "mm"; }

  /* [재발 방지] 열(column) 간 가로 간격 안전값 계산
     ──────────────────────────────────────────────────────────────
     주소라벨(index.js)의 getSafeColumnGapMM()과 완전히 동일한 이유로
     추가한 안전장치다. general-labels-data.js에 적어둔 colGap 값을
     그대로 믿고 쓰면, pl(좌우 여백)×2 + labelW×colN + colGap×(colN-1)의
     합이 실수로 210mm(A4 폭)를 넘어가는 규격이 등록됐을 때 그 초과분만큼
     칸이 페이지 밖으로 삐져나온다 — 실제로 주소라벨 쪽에서 이 문제가
     "화면에서는 안 보이다가 인쇄하면 라벨이 밀리는" 형태로 나타난 적이
     있었다(210mm 강제 고정 + overflow:hidden 때문에 화면과 인쇄 결과가
     달라짐). 일반라벨 모드는 지금 등록된 규격 전부 정상이지만, 안전장치
     자체가 없어서 나중에 규격을 추가/수정하다 같은 실수가 나면 조용히
     재발할 수 있다. → "규격에 적힌 간격"과 "실제로 남는 공간" 중 더 작은
     값만 적용해, 어떤 값이 들어와도 210mm를 절대 넘지 않도록 한다. */
  function getSafeColGapMM(c) {
    const colN = c.colN || 1;
    if (colN <= 1) return 0;
    const declared = parseFloat(c.colGap) || 0;
    const pl = parseFloat(c.pl) || 0;
    const w  = parseFloat(c.labelW) || 0;
    const leftover = Math.max(0, (210 - 2 * pl - colN * w) / (colN - 1));
    return Math.min(declared, leftover);
  }
  /* [재발 방지] 하단 여백(pb) 안전값 계산 — getSafeColGapMM()과 같은 이유의
     세로 버전(index.js의 getSafePaddingBottomMM()과 동일). pt(상단 여백)에
     적힌 값을 그대로 믿지 않고, "라벨H × 행수를 다 배치하고 297mm 안에서
     실제로 남는 공간"을 매번 다시 계산해서 쓴다. 이렇게 하면 규격 데이터에
     적힌 pb 값이 잘못돼 있어도(오탈자 등) 페이지 전체 높이는 항상 정확히
     297mm에 맞고, 마지막 행이 페이지 밖으로 밀리거나 잘리는 일이 없다. */
  function getSafePbMM(c) {
    const pt   = parseFloat(c.pt) || 0;
    const rowN = c.rowN || 1;
    const h    = parseFloat(c.labelH) || 0;
    return Math.max(0, 297 - pt - rowN * h);
  }

  /* ── 라벨 규격 드롭다운 렌더 (주소라벨과 동일한 ctrl-select 패턴) ─
     제조사별 optgroup으로 묶고, 칸수가 적은 순으로 정렬한다. ──────── */
  function renderTemplateSelect() {
    const sel = els.templateSelect;
    if (!sel) return;

    const byMaker = {};
    Object.entries(CFG).forEach(([key, v]) => {
      (byMaker[v.maker] = byMaker[v.maker] || []).push([key, v]);
    });
    Object.values(byMaker).forEach(list => list.sort((a, b) => a[1].perPage - b[1].perPage));

    const makerKeys = MAKER_ORDER.filter(m => byMaker[m]).concat(
      Object.keys(byMaker).filter(m => !MAKER_ORDER.includes(m))
    );

    sel.innerHTML = makerKeys.map(maker => {
      const opts = byMaker[maker].map(([key, v]) => {
        const shortLabel = `${v.maker.replace(/\s*\(.*?\)/, "")} ${v.model}`;
        return `<option value="${key}" data-short="${escHtml(shortLabel)}">` +
          `${escHtml(v.model)} (${v.perPage}칸 · ${v.labelW}×${v.labelH}mm)</option>`;
      }).join("");
      return `<optgroup label="${escHtml(maker)}">${opts}</optgroup>`;
    }).join("");

    if (!currentTplKey || !CFG[currentTplKey]) {
      currentTplKey = Object.keys(CFG)[0];
    }
    sel.value = currentTplKey;
    updateTemplateDisplay();
  }

  function updateTemplateDisplay() {
    const sel = els.templateSelect;
    if (!sel || !els.templateDisplay) return;
    const opt = sel.options[sel.selectedIndex];
    els.templateDisplay.textContent = (opt && (opt.dataset.short || opt.textContent)) || "규격 선택";
  }

  /* ── 규격 선택 ─────────────────────────────────────────────── */
  function selectTemplate(key) {
    currentTplKey = key;
    glInitialized = true;
    saveGlRecentTemplate(key); /* [신규] 최근 사용 규격 기록 */
    updateGlStepTabsGating(); /* [신규] 규격이 정해졌으니 다음 단계 탭들을 활성화 */
    cellTexts = {};
    cellFontSizes = {};
    cellImgTransforms = {}; /* 규격이 바뀌면 칸 크기가 달라지므로 개별 이미지 위치/확대도 함께 초기화 */
    cellImages = {};        /* 신규: 칸별 개별 이미지도 함께 초기화(칸 배치·수가 달라지므로) */
    cellFontFamilies = {};  /* 신규: 개별 폰트 오버라이드도 초기화(칸 배치·수가 달라지므로) */
    cellColors = {};        /* 신규 */
    cellStyles = {};         /* 신규 */
    cellAligns = {};          /* 신규 */
    cellContentTypes = {};    /* 신규 */
    cellContentLayouts = {};  /* 신규: 텍스트+이미지 배치 오버라이드도 규격 변경 시 함께 초기화 */
    deselectCell();         /* 신규: 규격이 바뀌면 이전 선택은 의미가 없으므로 해제 */
    if (els.templateSelect) els.templateSelect.value = key;
    updateTemplateDisplay();
    renderInspector();
    renderGlGridPreview();
    rebuildCanvas();
    /* [버그 수정] 개별 편집(칸 포커스) 패널이 열린 채로 옆에서 규격을 바꾸면,
       그 패널은 예전 규격의 칸 픽셀 크기를 그대로 들고 있어 새로 그려진
       캔버스와 크기가 맞지 않고 따로 노는 문제가 있었다(회전·반전 버튼이
       "안 먹는" 것처럼 보이는 원인도 대부분 여기서 비롯됨 — 규격이 바뀌면
       cellImages/cellImgTransforms가 전부 초기화되는데, 패널은 그걸 모른 채
       예전 이미지가 계속 떠 있는 것처럼 보여줬기 때문). 규격이 바뀌면 칸 개수도
       달라질 수 있으므로 편집 중이던 idx를 새 grid 범위 안으로 맞추고,
       openCellFocusPanel을 다시 호출해 새 칸 크기·상태로 패널을 그대로
       열어둔 채 갱신한다(패널이 닫혀 있었다면 glCellEditIdx가 null이라
       아무 일도 하지 않는다). */
    if (glCellEditIdx != null && typeof window._glOpenCellFocusPanel === "function") {
      const c2 = CFG[key];
      const maxIdx = c2 ? c2.perPage - 1 : 0;
      const nextIdx = Math.min(glCellEditIdx, Math.max(0, maxIdx));
      window._glOpenCellFocusPanel(nextIdx);
    }
    /* 신규: 칸 폭이 좁아 세로쓰기가 필요한 규격(정부문서화일 분류번호-옆면 등)을
       선택하면, 전역 '글자 방향'을 조용히 바꾸는 대신(다른 규격에 계속 영향을
       주면 안 되므로) 안내만 띄운다 — 사용자가 직접 '세로'를 선택하게 한다. */
    if (CFG[key] && CFG[key].suggestVertical && textWritingMode !== "vertical" && typeof showToast === "function") {
      showToast("📐 이 규격은 세로쓰기용입니다 — 좌측 '텍스트'의 '글자 방향'에서 '세로'를 선택하세요.", "info", 3200);
    }
  }

  /* ── 인스펙터 갱신 ─────────────────────────────────────────── */
  function renderInspector() {
    const c = CFG[currentTplKey];
    if (!c) return;
    if (els.inspMaker)  els.inspMaker.textContent  = c.maker;
    if (els.inspModel)  els.inspModel.textContent  = c.model;
    if (els.inspSize)   els.inspSize.textContent   = c.labelW + " × " + c.labelH + " mm";
    if (els.inspCount)  els.inspCount.textContent  = c.colN + " × " + c.rowN + " = " + c.perPage + "칸";
    if (els.inspMargin) els.inspMargin.textContent = c.pt.toFixed(2) + " / " + c.pl.toFixed(2) + " mm";
    if (els.inspSource) {
      /* 개선: data.js에 정의돼 있었지만 어디에도 표시되지 않던 source 필드를 노출 */
      els.inspSource.textContent = c.source || "";
      els.inspSource.parentElement.style.display = c.source ? "" : "none";
    }
    if (els.canvasSpec) els.canvasSpec.innerHTML = "<b>" + escHtml(c.model) + "</b> · " + c.colN + "×" + c.rowN + "칸 · " + c.labelW + "×" + c.labelH + "mm";
  }

  /* ── 일반라벨 인스펙터 내 라벨 그리드 미리보기 ─────────────────
     A4(210×297mm) 비율 고정 프레임 안에, 실제 라벨 mm 크기·여백·
     칸 간격 비율 그대로 칸을 절대좌표(%)로 배치한다. ──────────── */
  function renderGlGridPreview(targetEl, infoEl) {
    const c = CFG[currentTplKey];
    const previewEl = targetEl || els.glGridPreview;
    const infoTargetEl = infoEl !== undefined ? infoEl : els.glGridInfo;
    if (!c || !previewEl) return;

    const A4_W = 210, A4_H = 297;
    const pt = c.pt || 0, pl = c.pl || 0;
    const colGap = getSafeColGapMM(c);  /* [재발 방지] rebuildCanvas()와 동일한 안전값 사용 — 미리보기와 실제 인쇄가 항상 일치하도록 */
    const rowGap = c.rowGap != null ? c.rowGap : 0;

    let html = "";
    for (let r = 0; r < c.rowN; r++) {
      for (let cn = 0; cn < c.colN; cn++) {
        const xMm = pl + cn * (c.labelW + colGap);
        const yMm = pt + r * (c.labelH + rowGap);
        const leftPct   = (xMm / A4_W) * 100;
        const topPct    = (yMm / A4_H) * 100;
        const widthPct  = (c.labelW / A4_W) * 100;
        const heightPct = (c.labelH / A4_H) * 100;
        const idx = r * c.colN + cn;
        html += `<div class="gl-gp-cell" title="${idx + 1}번 라벨" ` +
          `style="left:${leftPct}%;top:${topPct}%;width:${widthPct}%;height:${heightPct}%"></div>`;
      }
    }
    previewEl.innerHTML = html;
    if (infoTargetEl) {
      infoTargetEl.textContent = `${c.colN}열 × ${c.rowN}행 = ${c.perPage}칸`;
    }
  }

  /* ── 콘텐츠 타입 세그먼트 ──────────────────────────────────── */
  function renderContentSeg() {
    const types = [
      { key: "text",  label: "텍스트" },
      { key: "image", label: "이미지" },
      { key: "both",  label: "텍스트+<br>이미지" },
    ];
    els.contentSeg.innerHTML = types.map(t =>
      `<button class="gl-content-btn${t.key === contentType ? " active" : ""}" data-type="${t.key}">${t.label}</button>`
    ).join("");
    els.contentSeg.querySelectorAll(".gl-content-btn").forEach(btn => {
      btn.addEventListener("click", () => {
        contentType = btn.dataset.type;
        renderContentSeg();
        updateContentBlockVisibility();
        rebuildCanvas();
        updateEditToolbar(); /* 신규: 이미지/텍스트 버튼 활성화 상태를 새 콘텐츠 타입에 맞게 갱신 */
      });
    });
    updateContentBlockVisibility();
  }
  function updateContentBlockVisibility() {
    els.textBlock.style.display  = (contentType === "text"  || contentType === "both") ? "" : "none";
    els.imageBlock.style.display = (contentType === "image" || contentType === "both") ? "" : "none";
    /* 신규: 텍스트+이미지 모드일 때만 "텍스트 위치" 컨트롤을 노출 */
    if (els.layoutBlock) els.layoutBlock.style.display = (contentType === "both") ? "" : "none";
  }

  /* ── 이미지 업로드 ─────────────────────────────────────────────
     변경: 파일을 고르거나 끌어다 놓아도 곧바로 모든 칸에 채우지 않는다.
     업로드된 이미지는 일단 "대기(staged)" 상태로만 미리보기에 표시되고,
     사이드바 하단의 '전체 칸에 적용' 버튼을 눌러야 비로소 모든 칸에
     실제로 채워진다(applyStagedImageToAll 참고). 단, 이미지가 이미 칸에
     채워져 있는 상태에서 우클릭 메뉴의 "이미지 교체"처럼 명시적으로
     "지금 바로 전체 교체"를 요청한 경우는 forceImmediateApply로 예외 처리한다. ── */
  const SUPPORTED_IMG_EXT = /\.(png|jpe?g|webp|svg)$/i;
  let forceImmediateApply = false;

  /* ══════════════════════════════════════════════════════════════
     신규: 큰 이미지 파일 경고 / 자동 최적화
     ──────────────────────────────────────────────────────────────
     휴대폰 사진처럼 용량이 큰 이미지를 여러 칸에 개별로 넣으면 매 칸마다
     원본 용량 그대로 메모리에 쌓여 인쇄 미리보기가 느려지거나 브라우저가
     버벅일 수 있다. 라벨은 실제 인쇄 크기가 작으므로, 화면에 보이는
     품질 차이 없이 가로·세로 중 긴 변을 GL_IMG_MAX_DIM(px) 이하로 자동
     축소해 저장한다. 이미 충분히 작은 이미지는 그대로 두고(불필요한
     재압축으로 화질이 떨어지는 것을 방지), SVG는 벡터라 대상에서 제외한다. ══ */
  const GL_IMG_MAX_DIM     = 1600;              /* 저장 시 허용하는 최대 가로/세로(px) */
  const GL_IMG_WARN_BYTES  = 3 * 1024 * 1024;   /* 이 크기(3MB)를 넘으면 최적화했다는 안내를 띄움 */

  function optimizeImageDataUrl(file, dataUrl) {
    return new Promise((resolve) => {
      const isSvg = file.type === "image/svg+xml" || /\.svg$/i.test(file.name);
      if (isSvg) { resolve(dataUrl); return; } /* 벡터 이미지는 리사이즈 대상이 아님 */
      const img = new Image();
      img.onload = () => {
        const w = img.naturalWidth, h = img.naturalHeight;
        if (!w || !h || Math.max(w, h) <= GL_IMG_MAX_DIM) { resolve(dataUrl); return; } /* 이미 충분히 작음 */
        const scale = GL_IMG_MAX_DIM / Math.max(w, h);
        const nw = Math.max(1, Math.round(w * scale)), nh = Math.max(1, Math.round(h * scale));
        try {
          const canvas = document.createElement("canvas");
          canvas.width = nw; canvas.height = nh;
          const ctx = canvas.getContext("2d");
          ctx.drawImage(img, 0, 0, nw, nh);
          /* PNG 원본은 투명도를 보존해야 하므로 PNG로, 그 외(사진류)는 용량 절감을 위해 JPEG로 재인코딩 */
          const keepPng = file.type === "image/png" || /\.png$/i.test(file.name);
          resolve(keepPng ? canvas.toDataURL("image/png") : canvas.toDataURL("image/jpeg", 0.86));
        } catch { resolve(dataUrl); } /* 실패 시 원본 그대로 사용(안전한 폴백) */
      };
      img.onerror = () => resolve(dataUrl);
      img.src = dataUrl;
    });
  }
  function warnIfLargeImage(file) {
    if (file.size > GL_IMG_WARN_BYTES) {
      showToast("📦 큰 이미지라 인쇄에 맞게 자동으로 최적화했어요 (원본 " + (file.size / 1024 / 1024).toFixed(1) + "MB).", "info", 2600);
    }
  }

  function handleImageFile(file) {
    if (!file) { forceImmediateApply = false; return; }
    if (!SUPPORTED_IMG_EXT.test(file.name) && !/^image\//.test(file.type)) {
      alert("PNG, JPG, JPEG, WEBP, SVG 형식만 지원합니다.");
      forceImmediateApply = false;
      return;
    }
    const immediate = forceImmediateApply;
    forceImmediateApply = false;
    const reader = new FileReader();
    reader.onload = () => {
      optimizeImageDataUrl(file, reader.result).then((optimized) => {
        warnIfLargeImage(file);
        stagedImageDataUrl = optimized;
        if (immediate) applyStagedImageToAll();
        else renderImageDrop();
      });
    };
    reader.readAsDataURL(file);
  }
  /* 대기 중인 이미지를 실제로 모든 칸에 채운다 — '전체 칸에 적용' 버튼과
     우클릭 "이미지 교체" 메뉴가 이 함수를 사용한다. */
  function applyStagedImageToAll() {
    if (!stagedImageDataUrl) return;
    glPushUndo();
    imageDataUrl = stagedImageDataUrl;
    cellImgTransforms = {}; /* 새 이미지로 전체 교체되면 이전 개별 위치/확대 설정은 의미가 없으므로 초기화 */
    renderImageDrop();
    rebuildCanvas();
  }
  function renderImageDrop() {
    const previewSrc = stagedImageDataUrl || imageDataUrl;
    if (previewSrc) {
      els.imageDrop.classList.add("has-image");
      els.imageDrop.innerHTML = '<img src="' + previewSrc + '" alt="업로드 이미지"/>';
    } else {
      els.imageDrop.classList.remove("has-image");
      els.imageDrop.innerHTML = '이미지를 끌어다 놓거나 클릭해서 업로드<br><span style="color:var(--text-3)">PNG · JPG · JPEG · WEBP · SVG</span>';
    }
    updateImagePendingUI();
  }

  /* [신규] 여러 이미지 한번에 넣기 — 파일을 고른 순서대로 1번 칸, 2번 칸...에
     하나씩 다르게 채운다("전체 칸에 적용"은 항상 같은 이미지 1장을 공통으로
     채우는 것과 다른, 칸마다 다른 사진을 넣는 용도). */
  function readFileAsDataUrl(file) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload  = () => resolve(reader.result);
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
  }
  async function handleBulkImageFiles(fileList) {
    const files = Array.from(fileList || []).filter(f => SUPPORTED_IMG_EXT.test(f.name) || /^image\//.test(f.type));
    if (!files.length) { showToast("이미지 파일을 선택해주세요 (PNG·JPG·JPEG·WEBP).", "warn", 2400); return; }
    const c = CFG[currentTplKey];
    const total = c ? c.perPage : 0;
    if (!total) return;
    const useCount = Math.min(files.length, total);
    if (els.bulkImageProgress) { els.bulkImageProgress.style.display = ""; els.bulkImageProgress.textContent = "이미지 불러오는 중... 0/" + useCount; }
    glPushUndo();
    for (let i = 0; i < useCount; i++) {
      const file = files[i];
      try {
        const dataUrl   = await readFileAsDataUrl(file);
        const optimized = await optimizeImageDataUrl(file, dataUrl);
        cellImages[i] = optimized;
      } catch { /* 이 파일만 건너뛰고 계속 진행 */ }
      if (els.bulkImageProgress) els.bulkImageProgress.textContent = "이미지 불러오는 중... " + (i + 1) + "/" + useCount;
    }
    rebuildCanvas();
    if (els.bulkImageProgress) els.bulkImageProgress.style.display = "none";
    if (contentType === "text") {
      showToast('⚠️ 지금은 "텍스트" 모드라 이미지가 안 보여요 — 위에서 "이미지" 또는 "텍스트+이미지"를 선택해주세요.', "warn", 4400);
    } else if (files.length > total) {
      showToast("📥 칸이 " + total + "개뿐이라 앞의 " + total + "장만 순서대로 채웠어요(나머지 " + (files.length - total) + "장은 사용하지 않았어요).", "info", 3600);
    } else {
      showToast("✅ 이미지 " + useCount + "장을 칸 순서대로 채웠습니다.", "info", 2400);
    }
  }
  /* 대기 중(아직 칸에 반영 안 됨)인 이미지가 있으면 안내 문구를 보이고
     '전체 칸에 적용' 버튼을 강조해, 눌러야 채워진다는 점을 알려준다. */
  function updateImagePendingUI() {
    const pending = !!stagedImageDataUrl && stagedImageDataUrl !== imageDataUrl;
    if (els.imagePendingHint) els.imagePendingHint.style.display = pending ? "" : "none";
    if (els.addAllBtn) els.addAllBtn.classList.toggle("gl-fillall-pending", pending);
  }

  /* ══════════════════════════════════════════════════════════════
     [신규] 데이터 가져오기 — 엑셀(.xlsx/.xls) · 한글(.hwpx) · 메모장(.txt)
     원하는 "열" 하나를 골라 그 값을 1번 칸, 2번 칸… 순서대로 채운다.
     주소 라벨 탭(index.js)의 가져오기와 달리 "이름+주소" 같은 고정된
     역할이 없으므로, 열을 하나만 고르면 되는 훨씬 단순한 자체 매퍼를
     쓴다. 다만 무거운 파싱 로직(hwpx 텍스트 추출·엑셀 병합 셀 펼치기)은
     index.js에 이미 있는 것을 그대로 재사용한다(중복 구현 방지).
     ⚠️ 일반 라벨은 "한 페이지 = c.perPage칸" 고정이라(여러 페이지로
     늘어나지 않음) 값이 칸 수보다 많으면 앞에서부터 칸 수만큼만 쓰고
     나머지는 버린다 — 이미지 여러 장 넣기(handleBulkImageFiles)와 동일한
     원칙. ══════════════════════════════════════════════════════════════ */

  /* 엑셀 열 번호 → A, B, …, Z, AA, AB… 표시용 라벨 */
  function glColLetter(i) {
    let s = "", n = i + 1;
    while (n > 0) { const r = (n - 1) % 26; s = String.fromCharCode(65 + r) + s; n = Math.floor((n - 1) / 26); }
    return s;
  }

  /* 엑셀 파일 → 행렬([[셀,셀,…], …]) — index.js와 동일하게 병합 셀을
     먼저 펼쳐서(fillMergedCellsIntoSheet) 병합된 열도 정상적으로 읽는다.
     주소 라벨의 processFile()과 달리 Worker를 쓰지 않고 메인 스레드에서
     바로 읽는다(일반 라벨 데이터는 보통 대량 메일머지용 엑셀보다 훨씬
     작아 굳이 Worker로 분리할 필요가 적음). */
  async function glExcelFileToMatrix(file) {
    const buf = await file.arrayBuffer();
    const wb = XLSX.read(buf);
    const sheet = wb.Sheets[wb.SheetNames[0]];
    if (typeof fillMergedCellsIntoSheet === "function") fillMergedCellsIntoSheet(sheet);
    return XLSX.utils.sheet_to_json(sheet, { header: 1, defval: "", blankrows: false });
  }

  /* 텍스트(hwpx에서 뽑아낸 것 포함) → 행렬. 탭이 있으면 탭으로, 없고
     쉼표가 모든 줄에 있으면 쉼표로 나눠 "열"을 만든다. 둘 다 없으면
     한 줄 = 값 하나(열 1개)로 취급한다. */
  function glTextToMatrix(text) {
    const lines = String(text).split(/\r\n|\r|\n/).map(l => l.replace(/\s+$/, "")).filter(l => l.trim().length > 0);
    if (!lines.length) return [];
    let delim = null;
    if (lines.some(l => l.includes("\t"))) delim = "\t";
    else if (lines.length > 1 && lines.every(l => l.includes(","))) delim = ",";
    if (delim) return lines.map(l => l.split(delim).map(s => s.trim()));
    return lines.map(l => [l.trim()]);
  }

  /* 파일 확장자를 보고 알맞은 경로로 보낸 뒤, 열 선택 모달을 띄운다. */
  async function glHandleImportFile(file) {
    if (!file) return;
    const c = CFG[currentTplKey];
    if (!c) { showToast("먼저 라벨 규격을 선택해주세요.", "warn", 2400); return; }
    const name = file.name || "";
    let matrix;
    try {
      if (/\.xlsx?$/i.test(name)) matrix = await glExcelFileToMatrix(file);
      else if (/\.txt$/i.test(name)) matrix = glTextToMatrix(await file.text());
      else if (/\.hwpx$/i.test(name)) matrix = glTextToMatrix(await extractHwpxText(file));
      else { await showAlert(".xlsx, .xls, .hwpx, .txt 파일만 지원합니다.", "📂", "지원하지 않는 파일"); return; }
    } catch (err) {
      console.error(err);
      await showAlert("파일을 읽는 중 오류가 발생했습니다.\n" + (err && err.message ? err.message : ""), "❌", "오류");
      return;
    }
    matrix = matrix || [];
    /* ★TABLE-START: 병합된 제목 행이나 빈 줄이 위에 몇 줄 끼어있어도(예:
       1행 제목, 2행 빈 줄, 3행부터 진짜 헤더+데이터) 표가 실제로 시작하는
       행을 구조적으로 추정해 그 앞은 건너뛴다. 확신이 없으면(예: 원래부터
       1행이 표였던 경우) 아무것도 건너뛰지 않아 기존 동작과 동일하다. */
    if (typeof TableImport !== "undefined" && matrix.length > 1) {
      const startRow = TableImport.detectTableStartRow(matrix, 12);
      if (startRow > 0) {
        matrix = matrix.slice(startRow);
        showToast("표 앞의 제목·빈 줄로 보이는 " + startRow + "행은 건너뛰고 가져왔어요.", "info", 2600);
      }
    }
    matrix = matrix.filter(row => (row || []).some(cell => String(cell != null ? cell : "").trim() !== ""));
    if (!matrix.length) { await showAlert("파일에서 가져올 데이터를 찾지 못했습니다.", "📭", "데이터 없음"); return; }
    let values;
    try {
      values = await openGlImportModal(matrix);
    } catch { return; } /* 취소 */
    await glApplyImportedValues(values, name);
  }

  /* 열 선택 + 미리보기 모달. resolve(values) — 최종 선택된 열의 값 배열(빈 값 제외)을 돌려준다. */
  function openGlImportModal(matrix) {
    return new Promise((resolve, reject) => {
      const modal    = document.getElementById("gl-import-modal");
      const colSel   = document.getElementById("gl-import-col-select");
      const headerCb = document.getElementById("gl-import-header-cb");
      const summaryEl= document.getElementById("gl-import-summary");
      const prevEl   = document.getElementById("gl-import-preview-table");
      const confirmBtn = document.getElementById("gl-import-confirm");
      const cancelBtn  = document.getElementById("gl-import-cancel");
      if (!modal || !colSel || !headerCb || !confirmBtn || !cancelBtn) { reject("no-modal"); return; }

      const maxCols = matrix.reduce((m, r) => Math.max(m, (r || []).length), 0);
      const c = CFG[currentTplKey];
      const total = c ? c.perPage : 0;
      headerCb.checked = maxCols > 1; /* 열이 여러 개면 엑셀처럼 첫 행이 제목일 가능성이 높음 */

      function colLabel(i) {
        const h = headerCb.checked ? String((matrix[0] || [])[i] != null ? matrix[0][i] : "").trim() : "";
        return h ? (h + " (" + glColLetter(i) + "열)") : (glColLetter(i) + "열");
      }
      function buildColOptions() {
        let html = "";
        for (let i = 0; i < maxCols; i++) html += '<option value="' + i + '">' + escHtml(colLabel(i)) + '</option>';
        colSel.innerHTML = html;
      }
      function dataRows() { return headerCb.checked ? matrix.slice(1) : matrix.slice(); }
      function selectedValues() {
        const ci = Number(colSel.value) || 0;
        return dataRows().map(r => String((r || [])[ci] != null ? r[ci] : "").trim()).filter(v => v !== "");
      }
      function renderPreview() {
        const vals = selectedValues();
        const rowsHtml = vals.slice(0, 8).map((v, i) =>
          '<tr><td style="width:52px;color:var(--text-3)">' + (i + 1) + '번 칸</td><td title="' + escHtml(v) + '">' + escHtml(v) + '</td></tr>'
        ).join("");
        prevEl.innerHTML = '<div class="mapper-preview"><h4>미리보기 (상위 ' + Math.min(vals.length, 8) + '개 · 총 ' + vals.length + '개)</h4>' +
          '<div class="preview-table-wrapper"><table class="preview-table"><tbody>' + (rowsHtml || '<tr><td style="color:var(--text-3)">가져올 값이 없어요</td></tr>') + '</tbody></table></div></div>';
        let summary = "총 " + vals.length + "개 값을 찾았어요 · 라벨 칸은 " + total + "개예요.";
        if (vals.length > total) summary += " 앞의 " + total + "개만 순서대로 채우고 나머지 " + (vals.length - total) + "개는 사용하지 않아요.";
        summaryEl.textContent = summary;
      }
      function onColChange() { renderPreview(); }
      function onHeaderChange() { buildColOptions(); renderPreview(); }

      buildColOptions();
      renderPreview();
      colSel.addEventListener("change", onColChange);
      headerCb.addEventListener("change", onHeaderChange);

      modal.style.display = "flex";
      requestAnimationFrame(() => modal.classList.add("active"));

      function cleanup() {
        colSel.removeEventListener("change", onColChange);
        headerCb.removeEventListener("change", onHeaderChange);
        modal.classList.remove("active");
        setTimeout(() => { modal.style.display = "none"; }, 200);
        document.removeEventListener("keydown", escH);
        confirmBtn.onclick = null; cancelBtn.onclick = null;
      }
      function escH(e) { if (e.key === "Escape") { cleanup(); reject("cancelled"); } }
      document.addEventListener("keydown", escH);
      confirmBtn.onclick = () => {
        const vals = selectedValues();
        if (!vals.length) { showToast("가져올 값이 없어요 — 다른 열을 선택해주세요.", "warn", 2600); return; }
        cleanup(); resolve(vals);
      };
      cancelBtn.onclick = () => { cleanup(); reject("cancelled"); };
    });
  }

  /* 선택된 값을 실제로 칸(cellTexts)에 순서대로 채운다. */
  async function glApplyImportedValues(values, sourceLabel) {
    const c = CFG[currentTplKey];
    const total = c ? c.perPage : 0;
    if (!total || !values || !values.length) return;
    const useCount = Math.min(values.length, total);

    let hasExisting = false;
    for (let i = 0; i < useCount; i++) { if (cellTexts[i] != null && cellTexts[i] !== "") { hasExisting = true; break; } }
    if (hasExisting) {
      const ok = await showConfirm(
        "가져온 데이터로 " + useCount + "개 칸을 채웁니다.\n이미 글자가 있는 칸은 덮어씌워집니다. 계속할까요?",
        { icon: "📥", title: "칸 채우기", okLabel: "덮어쓰고 채우기", cancelLabel: "취소" }
      );
      if (!ok) return;
    }

    glPushUndo();
    for (let i = 0; i < useCount; i++) cellTexts[i] = values[i];
    rebuildCanvas();

    if (contentType === "image") {
      showToast('⚠️ 지금은 "이미지" 모드라 글자가 안 보여요 — 위에서 "텍스트" 또는 "텍스트+이미지"를 선택해주세요.', "warn", 4400);
    } else if (values.length > total) {
      showToast("📥 칸이 " + total + "개뿐이라 앞의 " + total + "개만 순서대로 채웠어요(나머지 " + (values.length - total) + "개는 사용하지 않았어요).", "info", 3800);
    } else {
      showToast("✅ " + (sourceLabel ? sourceLabel + " — " : "") + useCount + "개 칸을 순서대로 채웠습니다.", "info", 2600);
    }
  }

  /* ── 캔버스(페이지) 구성 ───────────────────────────────────── */
  function rebuildCanvas() {
    const c = CFG[currentTplKey];
    if (!c) return;
    const page = document.createElement("div");
    page.className = "gl-a4-page";
    page.style.gridTemplateColumns = "repeat(" + c.colN + ", " + mm(c.labelW) + ")";
    page.style.gridTemplateRows    = "repeat(" + c.rowN + ", " + mm(c.labelH) + ")";
    page.style.columnGap = mm(getSafeColGapMM(c));    /* [재발 방지] 210mm를 넘지 않도록 안전하게 계산된 값 */
    page.style.rowGap    = "0mm";
    page.style.paddingTop    = mm(c.pt);
    page.style.paddingBottom = mm(getSafePbMM(c));    /* [재발 방지] 규격 데이터를 믿지 않고 항상 297mm에 정확히 맞도록 계산 */
    page.style.paddingLeft   = mm(c.pl);
    page.style.paddingRight  = mm(c.pl);
    for (let i = 0; i < c.perPage; i++) {
      page.appendChild(buildCell(i));
    }
    /* 신규: "한 페이지" 보기 모드에서 페이지를 축소할 때, 축소된 만큼만
       레이아웃 공간을 차지하도록 감싸는 래퍼(applyGlViewMode 참고) */
    const wrap = document.createElement("div");
    wrap.className = "gl-page-fit-wrap";
    wrap.appendChild(page);
    els.canvasScroll.innerHTML = "";
    els.canvasScroll.appendChild(wrap);
    applyGlViewMode();
  }

  /* ── 캔버스 보기 모드: 한 페이지(축소해서 전체 보기) / 스크롤(실제 크기) ──
     "스크롤" 모드는 기존 그대로(실제 인쇄 크기, 넘치면 스크롤)이고,
     "한 페이지" 모드는 페이지 전체가 캔버스 영역에 스크롤 없이 들어오도록
     비율을 유지한 채 transform:scale()로 축소해 가운데 배치한다. */
  const MM2PX = 96 / 25.4;
  function applyGlViewMode() {
    if (!els.canvasScroll) return;
    els.canvasScroll.classList.toggle("gl-fit-mode", glViewMode === "fit");
    const wrap = els.canvasScroll.querySelector(".gl-page-fit-wrap");
    const page = wrap && wrap.querySelector(".gl-a4-page");
    if (!wrap || !page) return;

    if (glViewMode !== "fit") {
      wrap.style.width = "";
      wrap.style.height = "";
      /* [수정] 기존엔 무조건 transform을 지웠는데, "스크롤(실제 크기)" 보기에서
         사용자가 캔버스 크기조절(줌) 버튼으로 설정한 배율(glZoomLevel)이 있다면
         그걸 반영해야 한다. 100%(1.0)면 굳이 transform을 걸지 않아 평소와
         동일하게 둔다 — 인쇄 시엔 @media print의 transform:none !important가
         항상 우선하므로 인쇄 결과에는 영향 없다. */
      page.style.transform = glZoomLevel !== 1 ? `scale(${glZoomLevel})` : "";
      page.style.transformOrigin = "top left";
      return;
    }
    const PAD = 32; /* 캔버스 가장자리와 페이지 사이 최소 여백(px) */
    const availW = els.canvasScroll.clientWidth  - PAD * 2;
    const availH = els.canvasScroll.clientHeight - PAD * 2;
    const pageWpx = 210 * MM2PX, pageHpx = 297 * MM2PX;
    if (availW <= 0 || availH <= 0) return;
    const scale = Math.max(0.05, Math.min(availW / pageWpx, availH / pageHpx));
    page.style.transformOrigin = "top left";
    page.style.transform = "scale(" + scale + ")";
    wrap.style.width  = Math.round(pageWpx * scale) + "px";
    wrap.style.height = Math.round(pageHpx * scale) + "px";
  }

  function setGlViewMode(mode) {
    if (mode !== "fit" && mode !== "scroll") return;
    glViewMode = mode;
    if (els.viewModeSeg) {
      els.viewModeSeg.querySelectorAll(".gl-view-btn").forEach(btn => {
        btn.classList.toggle("active", btn.dataset.mode === mode);
      });
    }
    applyGlViewMode();
  }

  /* 신규: 캔버스 크기조절(확대/축소) — 주소라벨 탭의 zoomLevel/applyZoom()과 같은 개념을
     일반라벨 캔버스에도 이식. "한 페이지" 모드는 이미 자동으로 배율을 계산하므로, 줌
     버튼을 누르면 "스크롤(실제 크기)" 모드로 자동 전환해 수동 배율이 곧바로 눈에 보이게 한다. */
  function stepGlZoom(delta) {
    glZoomLevel = Math.min(2.0, Math.max(0.3, Math.round((glZoomLevel + delta) * 100) / 100));
    if (els.glZoomLabel) els.glZoomLabel.textContent = Math.round(glZoomLevel * 100) + "%";
    if (glViewMode !== "scroll") setGlViewMode("scroll"); /* setGlViewMode 안에서 applyGlViewMode()까지 호출됨 */
    else applyGlViewMode();
  }

  /* [신규] 칸별 텍스트 서식 인라인 style 빌더 — 글자크기(기존)에 더해
     폰트/색상/굵기/기울임/밑줄 오버라이드를 한 번에 합쳐준다. 오버라이드가
     없는 항목은 그냥 생략되어 CSS 변수(전역 설정)가 자연히 적용된다.
     [신규] 두 번째 인자 ov는 "칸 포커스" 패널의 실시간 미리보기 전용 —
     아직 저장되지 않은 폼 입력값을 실제 cellFontSizes 등보다 우선 적용한다. */
  /* [신규] "겹침" 배치 후광(text-shadow) 자동 대비 — 글자색이 밝으면(흰색 등)
     기존의 흰 후광이 글자와 뒤섞여 오히려 안 보이게 되는 문제가 있었다.
     글자색의 상대 휘도(WCAG 근사식)를 계산해, 밝은 글자색이면 검정 계열
     후광을, 어두운 글자색이면(기존과 동일) 흰색 계열 후광을 반환한다. */
  function glHaloShadowFor(hex) {
    let r = 0, g = 0, b = 0;
    const m = /^#?([0-9a-f]{6})$/i.exec(hex || "");
    if (m) {
      const n = parseInt(m[1], 16);
      r = (n >> 16) & 255; g = (n >> 8) & 255; b = n & 255;
    }
    /* 상대 휘도(0~255 채널 기준 간이식) — 0.6 언저리를 기준으로 밝다/어둡다 판단 */
    const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
    if (luminance > 0.6) {
      /* 밝은 글자색 → 검정 계열 후광 */
      return "0 0 3px rgba(0,0,0,.85), 0 0 3px rgba(0,0,0,.85), 0 0 6px rgba(0,0,0,.6)";
    }
    /* 어두운 글자색(기본 검정 포함) → 기존과 동일한 흰색 후광 */
    return "0 0 3px rgba(255,255,255,.85), 0 0 3px rgba(255,255,255,.85), 0 0 6px rgba(255,255,255,.6)";
  }

  function cellTextStyleAttr(idx, ov, isOverlay) {
    ov = ov || {};
    const parts = [];
    const fsz = ov.fontSize != null ? ov.fontSize : cellFontSizes[idx];
    if (fsz != null) parts.push("font-size:" + multToPx(fsz));
    const fam = ov.fontFamily || cellFontFamilies[idx];
    if (fam) parts.push("font-family:" + fam);
    const col = ov.color || cellColors[idx];
    if (col) parts.push("color:" + col);
    const sty = ov.style || cellStyles[idx];
    if (sty) {
      if (sty.bold) parts.push("font-weight:700");
      if (sty.italic) parts.push("font-style:italic");
      if (sty.underline) parts.push("text-decoration:underline");
    }
    /* [버그 수정] "겹침 배치에서 글자색을 흰색/밝은 색으로 하면 흰 후광이
       안 먹혀서 다시 안 보인다" — 색에 맞는 후광을 계산해서 적용한다.
       [주의] 이 칸만의 개별 색 오버라이드(col)가 있을 때만 인라인으로
       굽는다 — 그 경우엔 "완료" 저장 시 항상 다시 그려지므로 최신 색을
       반영해 안전하다. 오버라이드가 없는(전역 색을 그대로 쓰는) 칸은
       여기서 아무것도 넣지 않고, CSS 변수(--gl-halo-shadow, 전역 색이
       바뀔 때마다 rebuildCanvas 없이 즉시 갱신됨)에 맡긴다 — 그렇지
       않으면 전역 색상을 바꿀 때마다 이미 그려진 칸들의 후광이 그 순간
       값에 고정돼버려 실시간 반영이 깨진다(실제로 겪은 회귀). */
    if (isOverlay && col) parts.push("text-shadow:" + glHaloShadowFor(col));
    return parts.length ? (' style="' + parts.join(";") + '"') : "";
  }

  /* [신규] previewOverrides가 주어지면 실제 칸이 아니라 "칸 포커스" 패널의
     확대 미리보기용 사본을 만든다 — 선택/점 표시·contenteditable·칸 내부
     미니 컨트롤(글자크기 −/＋, 이미지 자르기 손잡이 등 실제 칸 조작용 UI)이
     전혀 없는 순수 시각적 렌더링이며, 실제 상태(cellTexts 등)는 건드리지
     않는다. 이렇게 하면 렌더링 로직을 두 번 베껴 쓰지 않고 하나로 유지된다. */
  function buildCell(idx, previewOverrides) {
    const isPreview = !!previewOverrides;
    const ov = previewOverrides || {};
    const cell = document.createElement("div");
    /* 신규: 이 칸에 개별 오버라이드(텍스트·글자크기·이미지·위치/크기/자르기·
       폰트·색상·스타일·정렬)가 하나라도 있으면 항상 보이는 작은 점 표시
       (gl-cell-modified)를 붙인다 — 칸이 많을 때 마우스를 올려보지 않아도
       어떤 칸을 개별로 손댔는지 한눈에 알 수 있게 하기 위함. */
    const isModified = cellTexts[idx] != null || cellFontSizes[idx] != null || !!cellImages[idx] || !!cellImgTransforms[idx]
      || cellFontFamilies[idx] != null || cellColors[idx] != null || cellStyles[idx] != null || cellAligns[idx] != null || cellContentTypes[idx] != null
      || cellTables[idx] != null;
    cell.className = "gl-label-cell" + (idx === selectedIdx && !isPreview ? " gl-cell-selected" : "") + (isModified && !isPreview ? " gl-cell-modified" : "");
    if (isModified && !isPreview) cell.title = "이 칸은 개별로 수정되었습니다 (더블클릭 → 칸 개별 편집, 우클릭 → 이 칸 전체 초기화로 되돌릴 수 있어요)";
    /* [수정] 예전엔 미리보기 사본(clone)에는 data-idx를 붙이지 않았는데,
       그러면 확대 편집 패널 안에서 9점 핸들을 드래그해도 어느 칸을
       고치는 건지 알 수 없어 동작하지 않았다. 항상 붙여도 안전하다 —
       document.querySelector('[data-idx=...]')는 항상 DOM 순서상 먼저
       나오는 실제 캔버스 칸(canvasScroll)을 찾고, 사본은 그보다 뒤에
       있는 #gl-cell-focus-preview 안에서만 쓰인다. */
    cell.dataset.idx = String(idx);
    const text = ov.text != null ? ov.text : (cellTexts[idx] != null ? cellTexts[idx] : commonText);
    /* [신규] 이 칸만의 콘텐츠 타입 오버라이드가 있으면 전역 설정 대신 그것을 따른다.
       미리보기(사본)에서도 똑같이 적용돼야 패널에서 바꾼 결과가 바로 보인다. */
    const effType = glEffContentType(idx);
    /* [신규] 이 칸의 표(있으면) — 확대 미리보기 중이면 아직 저장 전인
       패널의 임시 표 데이터(ov.table)를, 아니면 저장된 cellTables[idx]를 쓴다.
       표가 있으면 일반 텍스트가 비어 있어도(표 칸이 전부 빈칸이어도)
       "텍스트가 있다"로 간주해 빈 칸(#N) 표시 대신 표 틀을 보여준다. */
    const tbl = ov.table !== undefined ? ov.table : cellTables[idx];
    const hasText  = (effType === "text" || effType === "both") && (!!tbl || text.trim());
    /* 변경: 전역 이미지(imageDataUrl)가 없어도 이 칸만의 개별 이미지(cellImages[idx])가
       있으면 이미지가 있는 것으로 간주 — '전체 칸에 적용' 없이도 칸 하나만 이미지를 넣을 수 있다. */
    const hasImage = (effType === "image" || effType === "both") && !!(imageDataUrl || cellImages[idx]);
    /* 신규: 텍스트 정렬(위치) 클래스 — 기본값(gl-ta-center gl-va-middle)은 기존 중앙정렬과 동일.
       [신규] 칸별 정렬 오버라이드(cellAligns), 그리고 미리보기 중이면 폼의 임시 정렬값 순으로 적용. */
    const effAlignH = (ov.align && ov.align.h) || (cellAligns[idx] && cellAligns[idx].h) || textAlignH;
    const effAlignV = (ov.align && ov.align.v) || (cellAligns[idx] && cellAligns[idx].v) || textAlignV;
    let taCls = "gl-ta-" + effAlignH + " gl-va-" + effAlignV;
    /* 신규: 세로쓰기 — 기본값(horizontal)일 땐 클래스가 붙지 않아 기존 렌더링과 완전히 동일 */
    if (textWritingMode === "vertical") taCls += " gl-writing-vertical";
    /* [신규] "확대 미리보기에 커서 놓고 바로 타이핑" — 실제 캔버스 칸은
       기존대로 contenteditable="true"(그리드에서 바로 수정, blur 시 저장)를
       쓰고, 칸 포커스 패널의 확대 미리보기(clone)는 plaintext-only로
       만들어 커서를 놓고 바로 타이핑할 수 있게 한다. 일반 contenteditable
       대신 plaintext-only를 쓰는 이유: Enter를 쳤을 때 <div>/<br> 같은
       중첩 태그 없이 순수 개행 문자만 남아, .gl-cell-text의
       white-space:pre-wrap과 그대로 잘 맞고, 옆 폼 textarea와 값을
       주고받기도 훨씬 단순해진다. */
    const textEditAttr = isPreview ? ' contenteditable="plaintext-only" spellcheck="false"' : ' contenteditable="true"';
    /* 클론에 입력할 때마다 그 값을 옆 폼의 textarea로 실시간 동기화한다.
       (주의) 여기서 refreshCellFocusPreview()를 부르면 이 클론 자체가
       통째로 다시 그려져 커서 위치가 매번 처음으로 튀어버린다 — 그래서
       textarea.value만 조용히 갱신하고, 다시 그리기는 하지 않는다. */
    function wireCloneTextSync(textEl) {
      if (!textEl) return;
      textEl.addEventListener("input", () => {
        if (els.cellEditText) els.cellEditText.value = textEl.innerText.replace(/\n$/, "");
      });
    }
    /* [신규] "표를 직접 캔버스에서 수정" — 메인 격자의 실제 칸이든, 칸
       포커스 패널의 확대뷰든, 화면에 보이는 표의 각 칸(<td>)을 클릭하면
       바로 그 자리에 커서가 놓여 타이핑할 수 있다(구조를 바꾸는 병합·
       분할·테두리·행/열 크기 조절 같은 도구는 실수 방지를 위해 계속
       "칸 개별 편집" 패널의 표 도구에서만 다룬다 — 여기서는 글자
       입력·수정·줄바꿈만).
       [주의] input마다 "지금 타이핑 중인 그 화면 자신"은 절대 다시
       그리지 않는다(그러면 커서가 매번 처음으로 튄다) — 그 대신 데이터
       (cellTables[idx])만 바로 갱신하고, "다른" 화면들(메인 캔버스↔확대
       클론↔옆 패널의 표 그리드)만 필요하면 다시 그려 맞춘다. */
    function wireCanvasTableEditing(tableEl, idxForTable, isPreviewCtx) {
      if (!tableEl) return;
      const tds = Array.from(tableEl.querySelectorAll("td[contenteditable]"));
      if (!tds.length) return;
      let sessionUndo = false;
      tds.forEach(td => {
        td.addEventListener("focus", () => { sessionUndo = false; });
        /* [신규] "셀 선택해서 늘리고 줄이고" — 캔버스에서 표 칸을 클릭하면
           (Shift+클릭이면 마지막 클릭 칸부터 지금 칸까지 범위 확장)
           선택 상태를 갱신한다. 이 선택이 Ctrl+방향키 크기조절·사이드
           패널의 병합/분할/테두리 버튼이 공통으로 참조하는 바로 그
           상태다(mousedown을 쓰는 이유: focus보다 먼저 일어나야 Shift
           범위 확장 시 브라우저의 기본 텍스트 선택 동작과 덜 부딪힌다). */
        td.addEventListener("mousedown", (e) => {
          if (typeof glCanvasTableSelectHook === "function") {
            glCanvasTableSelectHook(idxForTable, Number(td.dataset.r), Number(td.dataset.c), e.shiftKey);
          }
          if (e.shiftKey) return; /* Shift+클릭은 즉시 범위 확정 — 드래그 추적 불필요(사이드 패널과 동일) */
          /* [신규] "드래그로 여러 칸 선택" — 캔버스에서도 사이드 패널과
             똑같이, 시작 칸에서 마우스를 누른 채 다른 칸으로 끌면 그
             순간부터 범위 선택으로 바뀐다. glCanvasTableSelectHook을
             shiftKey=true로 계속 불러 재사용한다 — 훅 내부에서
             "glTblActive부터 지금 칸까지" 범위를 계산하는 로직이 바로
             Shift+클릭 확장과 같은 로직이라, 새로 만들 필요 없이 그대로
             드래그 확장에도 맞는다. */
          const startR = Number(td.dataset.r), startC = Number(td.dataset.c);
          let dragMoved = false;
          function onMove(ev) {
            const overEl = document.elementFromPoint(ev.clientX, ev.clientY);
            const overTd = overEl && overEl.closest(".gl-cell-table td[contenteditable]");
            if (!overTd || !tableEl.contains(overTd)) return;
            const or = Number(overTd.dataset.r), oc = Number(overTd.dataset.c);
            if (or === startR && oc === startC) return; /* 아직 시작 칸 위 — 드래그로 취급하지 않음 */
            if (!dragMoved) {
              dragMoved = true;
              /* 드래그가 실제로 칸 경계를 넘는 순간부터만 브라우저의 기본
                 텍스트 선택(파랗게 글자 긁힘)을 꺼서 하이라이트와 안 겹치게 */
              const sel0 = document.getSelection(); if (sel0) sel0.removeAllRanges();
              tableEl.classList.add("gl-tbl-dragging");
            }
            if (typeof glCanvasTableSelectHook === "function") glCanvasTableSelectHook(idxForTable, or, oc, true);
          }
          function onUp() {
            document.removeEventListener("mousemove", onMove);
            document.removeEventListener("mouseup", onUp);
            tableEl.classList.remove("gl-tbl-dragging");
          }
          document.addEventListener("mousemove", onMove);
          document.addEventListener("mouseup", onUp);
        });
        td.addEventListener("input", () => {
          const raw = cellTables[idxForTable];
          if (!raw) return;
          /* 정규화된 표는 항상 colWidths가 배열이다 — 아직 예전(문자열 그리드)
             형태라면 여기서 한 번 변환해 그대로 cellTables에 저장해 둔다. */
          if (!Array.isArray(raw.colWidths)) cellTables[idxForTable] = glTblNormalize(raw);
          if (!sessionUndo) { glPushUndo(); sessionUndo = true; }
          const r = Number(td.dataset.r), c = Number(td.dataset.c);
          const row = cellTables[idxForTable].rows[r];
          if (row && row[c]) row[c].text = td.textContent.replace(/\n$/, "");
          if (!isPreviewCtx) {
            /* 메인 격자에서 타이핑 중 — 칸 포커스 확대뷰가 열려 있으면 그것만 갱신 */
            refreshCellFocusPreview();
          } else {
            /* 확대뷰에서 타이핑 중 — 메인 격자의 그 칸을 다시 그려 맞춘다 */
            const liveCell = document.querySelector('.gl-label-cell[data-idx="' + idxForTable + '"]');
            if (liveCell && liveCell.parentNode) liveCell.replaceWith(buildCell(idxForTable));
          }
          /* "칸 개별 편집" 패널의 표 그리드가 지금 이 칸을 보여주고 있으면 함께 맞춘다 */
          if (glCellEditIdx === idxForTable && typeof glRefreshTableEditorGridHook === "function") glRefreshTableEditorGridHook();
        });
        td.addEventListener("blur", () => { sessionUndo = false; });
        td.addEventListener("keydown", (e) => {
          if (e.key !== "Tab") return;
          e.preventDefault();
          const pos = tds.indexOf(td);
          const next = tds[pos + (e.shiftKey ? -1 : 1)];
          if (!next) return;
          next.focus();
          const range = document.createRange();
          range.selectNodeContents(next);
          range.collapse(false);
          const sel = window.getSelection();
          if (sel) { sel.removeAllRanges(); sel.addRange(range); }
        });
        td.addEventListener("paste", handleGlCellPaste);
      });
      /* [신규] "표를 캔버스에서 직접 늘리고 줄이고 싶다"는 피드백 —
         "칸 개별 편집" 패널의 표 그리드에 붙이던 것과 똑같은 크기 조절
         오버레이(attachTableResizeOverlay)를 캔버스에 그려진 이 표에도
         그대로 붙인다. 클릭해서 글자를 먼저 입력해야만 나타나는 게
         아니라, 표가 그려지자마자 항상 손잡이가 준비돼 있어 굳이 옆
         패널을 열지 않고도 바로 드래그로 늘리고 줄일 수 있다.
         requestAnimationFrame으로 미루는 이유: 이 함수가 불리는 시점엔
         아직 이 칸(cellEl)이 실제 화면(document)에 붙기 전이라
         getBoundingClientRect()가 전부 0을 돌려준다 — 호출한 쪽
         (rebuildCanvas/replaceWith)이 반환된 엘리먼트를 화면에 넣는 건
         이 함수가 끝난 직후(같은 동기 실행 안)라, 다음 애니메이션
         프레임 시점엔 이미 화면에 붙어 있어 실제 크기를 정확히 잴 수
         있다. */
      requestAnimationFrame(() => {
        if (!tableEl.isConnected) return; /* 그 사이 다시 그려져 이 표는 이미 화면에서 빠졌을 수 있음 */
        attachTableResizeOverlay(tableEl.parentElement, tableEl, idxForTable);
      });
    }
    if (!hasText && !hasImage) {
      cell.classList.add("gl-empty");
      if (!isPreview) {
        cell.textContent = "#" + (idx + 1);
        cell.contentEditable = (effType === "text" || effType === "both") ? "true" : "false";
        cell.addEventListener("blur", onCellEdit);
        cell.addEventListener("focus", onCellFocus);
      } else if (effType === "text" || effType === "both") {
        /* [신규] 빈 칸이라도 확대 미리보기에서는 자리표시(#N) 대신 바로
           타이핑을 시작할 수 있어야 한다 — placeholder만 얹은 편집
           가능한 영역으로 만든다. */
        cell.innerHTML = '<div class="gl-cell-text ' + taCls + '" data-placeholder="#' + (idx + 1) + '"' + textEditAttr + '></div>';
        wireCloneTextSync(cell.querySelector(".gl-cell-text"));
      } else {
        cell.textContent = "#" + (idx + 1);
      }
      return cell;
    }
    if (effType === "text") {
      const fontAttr = cellTextStyleAttr(idx, ov);
      /* [수정] 표가 있으면 표를 그리되, 이제는 각 칸(<td>)에
         contenteditable을 걸어(renderCellTableHtml의 editable=true) 표
         칸을 캔버스에서 바로 클릭해 타이핑할 수 있다. 다만 칸 전체를
         감싸는 .gl-cell-text에는 여전히 contenteditable을 걸지 않는다
         (표 구조를 바꾸는 브라우저 네이티브 편집 명령이 끼어들 여지를
         원천적으로 막기 위해 — 표 내부는 항상 칸 단위로만 편집된다). */
      const bodyHtml = tbl ? renderCellTableHtml(tbl, true) : escHtml(text);
      cell.innerHTML =
        '<div class="gl-cell-text ' + taCls + (tbl ? " gl-cell-text-has-table" : "") + '"' + fontAttr + (tbl ? "" : textEditAttr) + '>' + bodyHtml + '</div>' +
        (isPreview ? "" : buildFszCtrlHtml(idx));
      if (!tbl) {
        if (!isPreview) {
          cell.querySelector(".gl-cell-text").addEventListener("blur", onCellEdit);
          cell.querySelector(".gl-cell-text").addEventListener("paste", handleGlCellPaste);
        } else {
          wireCloneTextSync(cell.querySelector(".gl-cell-text"));
        }
      } else {
        wireCanvasTableEditing(cell.querySelector(".gl-cell-table"), idx, isPreview);
      }
    } else if (effType === "image") {
      /* [수정] 이미지가 확대 편집 패널(.gl-cell-focus-clone)에서 칸 밖으로
         삐져나와 보이던 버그 수정 — .gl-cell-focus-clone은 자유 회전
         손잡이(.gl-img-handle-rot 등)가 칸 위쪽 바깥으로 나가야 해서
         overflow:visible로 되어 있는데, 그러면 같이 자식인 <img>도 칸
         밖으로 확대했을 때 그대로 다 보여버렸다. <img>만 별도의 클리핑
         래퍼(.gl-cell-img-clip, overflow:hidden)로 감싸고, 손잡이류
         (buildImgCtrlHtml)는 이전처럼 칸(.gl-label-cell) 바로 아래
         형제로 남겨둬 회전 손잡이는 계속 밖으로 나갈 수 있게 한다. */
      cell.innerHTML = '<div class="gl-cell-img-clip"><img class="gl-cell-img gl-fit-' + imageFit + '" src="' + cellImgSrc(idx) + '" alt=""' + imgOverrideStyleAttr(idx) + '/></div>' +
        buildImgCtrlHtml(idx);
    } else {
      /* 신규: contentLayout(칸별 오버라이드가 있으면 그것)에 따라
         텍스트/이미지 배치 클래스 부여. overlay 레이아웃일 때만 이미지가
         칸 전체를 덮어야 하므로 기존 이미지 배치(fit) 설정을 함께 적용한다. */
      const effLayout = glEffContentLayout(idx);
      const isOverlay = effLayout === "overlay";
      const imgFitCls = isOverlay ? (" gl-cell-img gl-fit-" + imageFit) : " gl-cell-img";
      /* [신규] "위/아래/옆" 배치도 9점 핸들·자유 회전이 되도록 확장.
         겹침(overlay)은 이미지가 칸 전체를 덮으므로 기존처럼 핸들을
         칸 전체(.gl-label-cell) 기준으로 그린다. 그 외 배치는 이미지가
         칸의 일부(예: 옆 배치는 45%)만 차지하므로, 핸들을 칸 전체가
         아니라 "그 이미지가 차지하는 영역"에만 정확히 맞춰야 한다.
         이를 위해 이미지를 position:relative인 .gl-cell-img-wrap으로
         감싸고, 그 안에 핸들(.gl-img-handles)을 함께 넣는다 — CSS
         position:absolute는 가장 가까운 relative 조상을 기준으로 하므로
         기존 handles/ctrl 스타일은 그대로 재사용된다(general-labels.css
         .gl-cell-img-wrap 규칙 참고). 드래그 계산 자체는 이미
         imgEl.getBoundingClientRect() 기준이라 칸 전체가 아닌 실제
         이미지 크기를 쓰고 있었으므로(startImgDrag/startHandleDrag),
         시각적으로 핸들 위치만 맞추면 됨. */
      const fontAttr = cellTextStyleAttr(idx, ov, isOverlay);
      /* [수정] "겹침" 배치는 image 분기와 똑같이 <img>가 칸 전체를 덮는
         구조라 안전하게 .gl-cell-img-clip으로 감쌀 수 있다(손잡이류는
         감싸지 않아 자유 회전 손잡이가 계속 밖으로 나갈 수 있다).
         "위/아래/옆" 배치(.gl-cell-img-wrap)는 래퍼 자체의 크기가
         <img>의 실제 렌더링 크기로 결정되고 9점 핸들 위치도 그 크기에
         맞춰지므로, 여기서 <img>를 또 다른 래퍼로 감싸 크기 계산을
         건드리는 대신 .gl-cell-img-wrap 자체에 overflow:hidden을 걸어
         해결했다(general-labels.css .gl-cell-img-wrap 규칙 참고) —
         이미지가 커져도 최소한 이 래퍼(=칸 안쪽 영역) 밖으로는 못
         나간다. */
      const imgTag = '<img class="' + imgFitCls.trim() + '" src="' + cellImgSrc(idx) + '" alt=""' + imgOverrideStyleAttr(idx) + '/>';
      const imgTagClipped = '<div class="gl-cell-img-clip">' + imgTag + '</div>';
      const bodyHtml = tbl ? renderCellTableHtml(tbl, true) : escHtml(text);
      cell.innerHTML =
        '<div class="gl-cell-both gl-layout-' + effLayout + '">' +
          (isOverlay ? imgTagClipped : '<div class="gl-cell-img-wrap">' + imgTag + buildImgCtrlHtml(idx) + '</div>') +
          '<div class="gl-cell-text ' + taCls + (tbl ? " gl-cell-text-has-table" : "") + '"' + fontAttr + (tbl ? "" : textEditAttr) + '>' + bodyHtml + '</div>' +
        '</div>' +
        (isPreview ? "" : buildFszCtrlHtml(idx)) +
        (isOverlay ? buildImgCtrlHtml(idx) : "");
      if (!tbl) {
        if (!isPreview) {
          cell.querySelector(".gl-cell-text").addEventListener("blur", onCellEdit);
          cell.querySelector(".gl-cell-text").addEventListener("paste", handleGlCellPaste);
        } else {
          wireCloneTextSync(cell.querySelector(".gl-cell-text"));
        }
      } else {
        wireCanvasTableEditing(cell.querySelector(".gl-cell-table"), idx, isPreview);
      }
    }
    return cell;
  }

  function onCellFocus(e) {
    if (e.target.classList.contains("gl-empty")) e.target.textContent = "";
  }
  /* [버그 수정] 외부(엑셀/워드/웹페이지 등)에서 텍스트를 복사해 붙여넣을 때
     원본 서식(글꼴·글자 크기·색상 등)이 함께 들어와 라벨 양식에 맞게 반영되지
     않던 문제 수정. 서식을 모두 제거하고 순수 텍스트만 삽입해, 글꼴·글자 크기가
     항상 현재 라벨 양식(사이드바 설정)을 그대로 따르도록 함. */
  function handleGlCellPaste(e) {
    e.preventDefault();
    const raw = (e.clipboardData || window.clipboardData).getData("text/plain") || "";
    const text = raw.replace(/\r\n|\r|\n/g, " ").replace(/\s+/g, " ").trim();
    if (!text) return;
    const sel = window.getSelection();
    if (!sel || !sel.rangeCount) return;
    const range = sel.getRangeAt(0);
    range.deleteContents();
    const node = document.createTextNode(text);
    range.insertNode(node);
    range.setStartAfter(node); range.setEndAfter(node);
    sel.removeAllRanges(); sel.addRange(range);
  }
  function onCellEdit(e) {
    const cellEl = e.target.closest(".gl-label-cell");
    if (!cellEl) return;
    const idx = Number(cellEl.dataset.idx);
    const newText = e.target.textContent || "";
    const oldText = cellTexts[idx] != null ? cellTexts[idx] : commonText;
    if (newText !== oldText) glPushUndo(); /* 신규: 실제로 바뀐 경우에만 되돌리기 지점 저장 */
    cellTexts[idx] = newText;
    /* 신규: 방금 편집한 칸 하나만 다시 그려서(전체 재빌드 아님) 빈칸↔텍스트 상태 전환 시
       글자 크기 미니 컨트롤이 즉시 나타나거나 사라지도록 한다. 포커스는 이미 blur된 뒤라
       커서 유지 문제는 없다. */
    const fresh = buildCell(idx);
    if (cellEl.parentNode) cellEl.replaceWith(fresh);
  }

  /* ── 칸별 글자 크기 개별 조절 ───────────────────────────────────
     기본은 전역 글자 크기(glFontSizeMultiplier)를 그대로 따르고,
     cellFontSizes[idx]가 지정된 칸만 그 값으로 오버라이드한다.
     오버라이드가 없는 칸은 inline style을 주지 않아 CSS 변수를
     그대로 상속하므로, 전역 크기를 바꿔도 즉시(재빌드 없이) 반영된다. ── */
  function cellFontMultiplier(idx) {
    return cellFontSizes[idx] != null ? cellFontSizes[idx] : glFontSizeMultiplier;
  }
  function multToPx(mult) { return (12 * mult).toFixed(1) + "px"; }
  function multToPt(mult) { return Math.round(mult * 10) + 2; } /* gl-font-size-select 표시(8~18pt)와 동일 매핑 */

  function buildFszCtrlHtml(idx) {
    const isCustom = cellFontSizes[idx] != null;
    const pt = multToPt(cellFontMultiplier(idx));
    return '<div class="gl-cell-fsz-ctrl' + (isCustom ? " gl-cell-fsz-custom" : "") + '" data-idx="' + idx + '" ' +
      'title="이 칸 글자 크기: ' + pt + 'pt' + (isCustom ? " (개별 지정됨 · ↺로 기본값 복원)" : "") + '">' +
      '<button type="button" class="gl-cell-fsz-btn" data-act="down" tabindex="-1" title="이 칸만 글자 작게" aria-label="이 칸만 글자 작게">−</button>' +
      '<button type="button" class="gl-cell-fsz-btn" data-act="up" tabindex="-1" title="이 칸만 글자 크게" aria-label="이 칸만 글자 크게">＋</button>' +
      '<button type="button" class="gl-cell-fsz-btn gl-cell-fsz-reset" data-act="reset" tabindex="-1" title="기본 크기로" aria-label="이 칸 글자 크기 기본값으로">↺</button>' +
    '</div>';
  }

  /* 델리게이션 클릭 핸들러 — canvasScroll에 1회만 등록되며 rebuildCanvas로
     페이지가 교체돼도(자식이 바뀌어도) 계속 동작한다(이벤트 위임). */
  function onCellFszCtrlClick(e) {
    const btn = e.target.closest(".gl-cell-fsz-btn");
    if (!btn) return;
    e.preventDefault(); /* 편집 중이던 contenteditable의 포커스/커서를 그대로 유지 */
    const ctrl = btn.closest(".gl-cell-fsz-ctrl");
    if (!ctrl) return;
    const idx = Number(ctrl.dataset.idx);
    const cellEl = ctrl.closest(".gl-label-cell");
    const textEl = cellEl && cellEl.querySelector(".gl-cell-text");
    const act = btn.dataset.act;

    if (act === "reset") {
      if (cellFontSizes[idx] != null) glPushUndo();
      delete cellFontSizes[idx];
    } else {
      const cur  = cellFontMultiplier(idx);
      const next = Math.round((cur + (act === "up" ? 1 : -1) * CELL_FONT_STEP) * 10) / 10;
      if (next < CELL_FONT_MIN || next > CELL_FONT_MAX) return;
      glPushUndo();
      cellFontSizes[idx] = next;
    }

    /* 캔버스 전체를 다시 빌드하지 않고 이 칸만 즉시 갱신(입력 흐름 방해 최소화) */
    const isCustom = cellFontSizes[idx] != null;
    const mult = cellFontMultiplier(idx);
    if (textEl) textEl.style.fontSize = isCustom ? multToPx(mult) : "";
    ctrl.classList.toggle("gl-cell-fsz-custom", isCustom);
    ctrl.title = "이 칸 글자 크기: " + multToPt(mult) + "pt" + (isCustom ? " (개별 지정됨 · ↺로 기본값 복원)" : "");
  }

  /* ── 칸별 이미지 위치·확대·자르기 개별 조절 ─────────────────────
     기본은 전역 '이미지 배율/위치' 설정을 그대로 따르고,
     cellImgTransforms[idx]가 지정된 칸만 그 값으로 오버라이드한다.
     오버라이드가 없는 칸은 inline transform을 주지 않아 CSS 변수
     (--gl-img-scale·--gl-img-origin)를 그대로 상속하므로, 전역 설정을
     바꿔도 즉시(재빌드 없이) 반영된다 — cellFontSizes와 동일한 설계.
     croppedSrc가 있으면 원본(imageDataUrl) 대신 잘라낸 이미지를 사용한다. ── */
  function cellImgSrc(idx) {
    const t = cellImgTransforms[idx];
    if (t && t.croppedSrc) return t.croppedSrc;
    /* 신규: 이 칸만의 개별 이미지(상단 편집 툴바 "이 칸 이미지 교체")가 있으면 최우선 사용 */
    if (cellImages[idx]) return cellImages[idx];
    return imageDataUrl;
  }

  /* [신규] 밝기·대비·채도·흑백·불투명도·색상 반전 — CSS filter/opacity로
     구현한다(원본 이미지 데이터는 건드리지 않고 화면/인쇄에 그릴 때만
     적용되므로, 나중에 슬라이더를 다시 만지거나 초기화해도 화질 손실
     없이 언제든 되돌릴 수 있다). 값이 전부 기본치면 빈 문자열을 돌려줘
     굳이 filter:none을 박아넣지 않는다. */
  function cellImgFilterCss(f) {
    if (!f) return "";
    const b = f.brightness != null ? f.brightness : 100;
    const c = f.contrast   != null ? f.contrast   : 100;
    const s = f.saturate   != null ? f.saturate   : 100;
    const g = f.grayscale  != null ? f.grayscale  : 0;
    const inv = f.invert   != null ? f.invert     : 0;
    if (b === 100 && c === 100 && s === 100 && g === 0 && inv === 0) return "";
    return "brightness(" + b + "%) contrast(" + c + "%) saturate(" + s + "%) grayscale(" + g + "%) invert(" + inv + "%)";
  }
  /* [신규] 불투명도는 filter가 아니라 opacity 스타일 속성이라 별도로 관리한다. */
  function cellImgOpacityCss(f) {
    const o = (f && f.opacity != null) ? f.opacity : 100;
    return o === 100 ? "" : (o / 100).toFixed(2);
  }

  function imgOverrideStyleAttr(idx) {
    const t = cellImgTransforms[idx];
    if (!t) return "";
    const rot = t.rotate ? " rotate(" + t.rotate + "deg)" : "";
    /* ★FLIP: 반전은 scaleX/scaleY 자체의 부호를 바꾸지 않고(핸들 드래그·클램프
       로직이 항상 양수를 가정하므로), 화면에 그릴 때만 곱해주는 별도 배수다. */
    const fx = t.flipX ? -1 : 1, fy = t.flipY ? -1 : 1;
    const filterCss = cellImgFilterCss(t.filter);
    const opacityCss = cellImgOpacityCss(t.filter);
    return ' style="transform:translate(' + t.x.toFixed(1) + 'px,' + t.y.toFixed(1) + 'px)' + rot + ' scale(' + (t.scaleX * fx).toFixed(3) + ',' + (t.scaleY * fy).toFixed(3) + ');transform-origin:50% 50%' + (filterCss ? ';filter:' + filterCss : "") + (opacityCss ? ';opacity:' + opacityCss : "") + '"';
  }

  /* 인라인 transform을 한곳에서 갱신 — 드래그·핸들 조절 모두 이 함수를 사용한다.
     scale(scaleX, scaleY)로 가로/세로를 따로 지정한다(둘이 같으면 기존과 동일한 비율 확대).
     rotate는 90도 단위 회전(신규) — 지정 안 돼 있으면(undefined/0) 기존과 동일하게 생략된다.
     ★FLIP: flipX/flipY가 있으면 화면에 그릴 때만 부호를 뒤집는다(저장된 scaleX/scaleY는
     항상 양수로 유지 — 9-핸들 드래그·clampImgScale·clampImgOffset이 모두 양수를 가정하고
     계산하므로, 그 계산들을 건드리지 않고 반전만 얹기 위해 렌더링 단계에서만 곱한다). */
  function applyCellTransform(imgEl, t) {
    const rot = t.rotate ? " rotate(" + t.rotate + "deg)" : "";
    const fx = t.flipX ? -1 : 1, fy = t.flipY ? -1 : 1;
    imgEl.style.transform = "translate(" + t.x.toFixed(1) + "px," + t.y.toFixed(1) + "px)" + rot + " scale(" + (t.scaleX * fx).toFixed(3) + "," + (t.scaleY * fy).toFixed(3) + ")";
    imgEl.style.transformOrigin = "50% 50%";
    imgEl.style.filter = cellImgFilterCss(t.filter); /* [신규] 빈 문자열이면 filter 속성 자체가 사라져 기존과 동일 */
    imgEl.style.opacity = cellImgOpacityCss(t.filter); /* [신규] 불투명도 — 빈 문자열이면 opacity 속성이 사라져 기본값(불투명) */
  }

  /* [신규] 밝기·대비·채도·흑백·반전·불투명도 슬라이더 하나가 바뀔 때 호출한다.
     실제 캔버스 칸과(열려 있다면) 칸 포커스 패널의 확대 미리보기 양쪽에
     동시에 적용해 둘이 항상 같은 상태를 보여주게 한다. */
  function setCellImgFilter(idx, key, value) {
    const cur = cellImgTransforms[idx] || { scaleX: 1, scaleY: 1, x: 0, y: 0 };
    const nextFilter = Object.assign({ brightness: 100, contrast: 100, saturate: 100, grayscale: 0, invert: 0, opacity: 100 }, cur.filter, { [key]: value });
    cellImgTransforms[idx] = Object.assign({}, cur, { filter: nextFilter });
    const realCellEl = els.canvasScroll && els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
    const realImgEl = realCellEl && realCellEl.querySelector(".gl-cell-img");
    if (realImgEl) { realImgEl.style.filter = cellImgFilterCss(nextFilter); realImgEl.style.opacity = cellImgOpacityCss(nextFilter); }
    if (glCellEditIdx === idx) {
      const cloneImgEl = els.cellFocusPreview && els.cellFocusPreview.querySelector(".gl-cell-img");
      if (cloneImgEl) { cloneImgEl.style.filter = cellImgFilterCss(nextFilter); cloneImgEl.style.opacity = cellImgOpacityCss(nextFilter); }
    }
    if (realCellEl) markCellImgCustom(realCellEl);
  }

  /* [신규] 보정 4종을 한번에 기본값으로 되돌린다(위치·크기·회전·자르기는 그대로 둠). */
  function resetCellImgFilter(idx) {
    const cur = cellImgTransforms[idx];
    if (!cur || !cur.filter) return;
    glPushUndo();
    /* [버그 수정] delete cur.filter로 기존 객체를 직접 건드리면, 방금 위에서
       glPushUndo()가 저장한 스냅샷도 같은 객체를 참조하고 있어(얕은 복사라서)
       "되돌리기"를 눌러도 filter가 이미 지워진 채로 나오는 문제가 있었다.
       새 객체를 만들어 교체하는 방식(다른 함수들과 동일한 패턴)으로 바꾼다. */
    const next = Object.assign({}, cur);
    delete next.filter;
    cellImgTransforms[idx] = next;
    const realCellEl = els.canvasScroll && els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
    const realImgEl = realCellEl && realCellEl.querySelector(".gl-cell-img");
    if (realImgEl) { realImgEl.style.filter = ""; realImgEl.style.opacity = ""; }
    if (glCellEditIdx === idx) {
      const cloneImgEl = els.cellFocusPreview && els.cellFocusPreview.querySelector(".gl-cell-img");
      if (cloneImgEl) { cloneImgEl.style.filter = ""; cloneImgEl.style.opacity = ""; }
    }
  }

  /* [신규] 슬라이더 값과 옆의 "72%" 같은 숫자 라벨을 한번에 맞춘다. */
  function setCellFilterSliderUI(sliderEl, valLabelId, value, unit) {
    if (sliderEl) sliderEl.value = String(value);
    const lbl = document.getElementById(valLabelId);
    if (lbl) lbl.textContent = value + (unit || "");
  }



  /* 신규: 이 칸의 이미지를 90도 단위로 회전한다(deltaDeg에 ±90을 넘겨 시계/반시계 공용으로 사용).
     0~359도로 정규화해 여러 번 눌러도 값이 계속 커지지 않게 한다. */
  function rotateCellImg(idx, deltaDeg) {
    glPushUndo();
    const cur = cellImgTransforms[idx] || { scaleX: 1, scaleY: 1, x: 0, y: 0 };
    const nextRotate = (((cur.rotate || 0) + deltaDeg) % 360 + 360) % 360;
    cellImgTransforms[idx] = Object.assign({}, cur, { rotate: nextRotate });
    syncCellImgVisual(idx);
  }

  /* ★FLIP: 이 칸의 이미지를 좌우(axis="x") 또는 상하(axis="y")로 뒤집는다.
     포토샵의 "가로로 뒤집기/세로로 뒤집기"와 동일한 개념. scaleX/scaleY 값
     자체는 그대로 두고 flipX/flipY 불리언만 토글해, 이후 크기 조절 핸들을
     계속 써도(양수 기준으로 계산되므로) 반전 상태가 깨지지 않는다. */
  function flipCellImg(idx, axis) {
    glPushUndo();
    const cur = cellImgTransforms[idx] || { scaleX: 1, scaleY: 1, x: 0, y: 0 };
    const next = axis === "y"
      ? Object.assign({}, cur, { flipY: !cur.flipY })
      : Object.assign({}, cur, { flipX: !cur.flipX });
    cellImgTransforms[idx] = next;
    syncCellImgVisual(idx);
  }

  /* [버그 수정: "회전/반전 버튼을 눌러도 바로 안 됨"]
     예전 rotateCellImg/flipCellImg는 "실제 캔버스 칸에 이미 떠 있는
     <img> 엘리먼트를 찾아서 그 transform만 patch"하는 방식이었다.
     막 이미지를 넣은 직후처럼 그 칸이 아직 (이미지가 있는 상태로)
     다시 그려지지 않아 <img>가 없을 때는 "자가 복구"로 한 번 다시
     그려보긴 했지만, 그 복구 절차 자체가 여러 단계(재조회·재확인)를
     거치다 보니 조건을 하나라도 놓치면 조용히 아무 효과 없이 끝나
     "클릭했는데 안 바뀐다"는 인상을 줄 수 있었다.
     이제는 상태(cellImgTransforms)부터 먼저 확정한 뒤, 그 칸을 항상
     통째로 다시 그린다 — <img>가 있었든 없었든, 처음 누르는 것이든
     아니든 상관없이 매번 같은 한 가지 방식으로만 반영되므로 "첫
     클릭에서만 이상하게 안 먹는" 경우 자체가 생기지 않는다. */
  function syncCellImgVisual(idx) {
    const realCellEl = els.canvasScroll && els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
    if (realCellEl) rebuildCellImgFromState(realCellEl, idx);
    updateEditToolbar();
    /* [버그 수정] "회전·반전 버튼을 눌러도 캔버스(확대 미리보기)에 바로 반영이
       안 됨" — refreshCellFocusPreview는 init() 안에서만 선언된 지역 함수라
       여기(최상위 스코프)에서 이름만으로는 아예 보이지 않아 ReferenceError가
       났고, 그 오류 때문에 실제 칸(rebuildCellImgFromState)은 바뀌어도 사용자가
       보고 있는 확대 미리보기는 갱신되지 않았다(패널을 닫았다 열어야 반영된
       것처럼 보임). window._glRefreshCellFocusPreview로 노출해둔 참조를 대신
       쓴다(자르기 모달 쪽 코드가 이미 같은 방식으로 문제없이 쓰고 있다). */
    if (glCellEditIdx === idx && typeof window._glRefreshCellFocusPreview === "function") window._glRefreshCellFocusPreview();
  }


  /* 3×3(9개) 핸들 — 모서리 4개(nw/ne/sw/se)는 대각선으로 드래그해서
     가로세로 비율을 유지한 채 확대/축소, 변 4개(n/s는 상하, w/e는 좌우)는
     그 방향으로만 늘리거나 줄여 비율이 달라질 수 있다. 정중앙(c) 1개는
     드래그해서 위치 이동. 각 핸들이 중심에서 바깥쪽으로 향하는
     방향(outward direction)을 나타낸다. */
  const IMG_HANDLE_DIR = {
    nw: { x: -1, y: -1 }, n: { x: 0, y: -1 }, ne: { x: 1, y: -1 },
    w:  { x: -1, y:  0 },                     e:  { x: 1, y:  0 },
    sw: { x: -1, y:  1 }, s: { x: 0, y:  1 }, se: { x: 1, y:  1 },
  };
  const IMG_HANDLE_ORDER = ["nw", "n", "ne", "w", "c", "e", "sw", "s", "se"];
  const IMG_HANDLE_TITLE = {
    c:  "드래그: 이미지 위치 이동",
    nw: "드래그: 비율 유지하며 확대·축소", ne: "드래그: 비율 유지하며 확대·축소",
    sw: "드래그: 비율 유지하며 확대·축소", se: "드래그: 비율 유지하며 확대·축소",
    n:  "드래그: 세로로만 늘리기·줄이기", s:  "드래그: 세로로만 늘리기·줄이기",
    w:  "드래그: 가로로만 늘리기·줄이기", e:  "드래그: 가로로만 늘리기·줄이기",
  };

  function buildImgCtrlHtml(idx) {
    const isCustom = !!cellImgTransforms[idx];
    const handles = IMG_HANDLE_ORDER.map(h =>
      '<span class="gl-img-handle" data-h="' + h + '" title="' + (IMG_HANDLE_TITLE[h] || "드래그: 이미지 크기 조절") + '"></span>'
    ).join("");
    /* [신규] 자유 회전 손잡이 — 위쪽 가운데(n) 핸들 살짝 바깥에 따로 두고,
       가는 연결선(stem)으로 이어서 "이건 이미지에 딸린 회전 조절점"임을
       바로 알아보게 했다. 90도씩 딱딱 도는 회전 버튼과 달리 원하는
       각도로 자유롭게 돌릴 수 있다.
       ★ROTATE-POLISH: 아이콘을 90° 버튼과 똑같은 "되돌리기 화살표" 모양에서
       원을 완전히 그리는 두 화살표(자유 회전 느낌) 모양으로 바꿔 서로
       헷갈리지 않게 구분했다. */
    const rotHandle = '<span class="gl-img-rot-stem"></span>' +
      '<span class="gl-img-handle gl-img-handle-rot" data-h="rot" title="드래그: 자유 회전(자동으로 15도 단위 스냅)">' +
      '<svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M4 12a8 8 0 0 1 14.5-4.5"/><path d="M20 12a8 8 0 0 1-14.5 4.5"/><path d="M18 4v4h-4"/><path d="M6 20v-4h4"/></svg>' +
      '</span>';
    return '<div class="gl-cell-img-ctrl' + (isCustom ? " gl-cell-img-custom" : "") + '" data-idx="' + idx + '">' +
      '<button type="button" class="gl-cell-img-rotate" data-act="rotate" tabindex="-1" title="이 칸 이미지 90도 회전" aria-label="이 칸 이미지 90도 회전">90°</button>' +
      '<button type="button" class="gl-cell-img-flip" data-act="flip-x" tabindex="-1" title="이 칸 이미지 좌우 반전" aria-label="이 칸 이미지 좌우 반전">◫</button>' +
      '<button type="button" class="gl-cell-img-flip" data-act="flip-y" tabindex="-1" title="이 칸 이미지 상하 반전" aria-label="이 칸 이미지 상하 반전">⬍</button>' +
      '<button type="button" class="gl-cell-img-crop" data-act="crop" tabindex="-1" title="이 칸 이미지 자르기" aria-label="이 칸 이미지 자르기">✂</button>' +
      '<button type="button" class="gl-cell-img-reset" data-act="reset" tabindex="-1" title="이 칸 이미지 위치·크기·회전·자르기·반전 초기화" aria-label="이 칸 이미지 위치·크기·회전·자르기·반전 초기화">↺</button>' +
    '</div>' +
    '<div class="gl-img-handles" data-idx="' + idx + '">' + handles + rotHandle + '</div>';
  }

  /* [실시간 WYSIWYG 강화] 캔버스는 "한 페이지" 보기(축소해서 전체 보기)나
     "스크롤" 보기의 확대/축소(glZoomLevel)에 따라 .gl-a4-page 전체에
     transform:scale()이 걸려 있을 수 있다. getBoundingClientRect()가
     돌려주는 크기·좌표는 이 배율이 이미 적용된 "화면에 보이는" 값인
     반면, 이미지에 적용하는 translate(x,y) 값은 그 배율이 적용되기
     "이전"의 논리 좌표계 기준이다. 이 둘을 그대로 섞어 쓰면 100%(줌
     없음)일 때는 우연히 맞아떨어지지만, 축소/확대된 상태에서는 마우스가
     움직인 만큼 이미지가 따라오지 않고 배율만큼 어긋난다(예: 50%로
     축소해서 보면 이미지가 마우스 이동량의 절반만 움직임) — 실제
     인쇄될 물리적 비율과 화면이 달라 보이는 원인. 이를 바로잡기 위해
     현재 배율을 구해서 마우스 이동량·기준 rect를 논리 좌표계로
     환산한 뒤 계산한다. */
  /* [신규] 터치 지원 — 9점 핸들·자유 회전·이미지 드래그가 mousedown/mousemove로만
     구현돼 있어 터치스크린 노트북·태블릿에서는 전혀 동작하지 않았다. 마우스
     이벤트든 터치 이벤트든 좌표를 하나로 읽고(glEventXY), 드래그 시작 시
     mousemove/mouseup 뿐 아니라 touchmove/touchend/touchcancel도 함께
     등록·해제하도록(glBindDragMove) 공통화해서, 아래 3개 드래그 함수
     (startImgDrag/startHandleDrag/startImgRotateDrag)를 최소한만 고쳐도
     터치로 그대로 동작하게 한다. */
  function glEventXY(e) {
    if (e.touches && e.touches.length) return { x: e.touches[0].clientX, y: e.touches[0].clientY };
    if (e.changedTouches && e.changedTouches.length) return { x: e.changedTouches[0].clientX, y: e.changedTouches[0].clientY };
    return { x: e.clientX, y: e.clientY };
  }
  function glBindDragMove(onMove, onUp) {
    const moveHandler = (ev) => { if (ev.touches) ev.preventDefault(); /* 드래그 중 화면 스크롤 방지 */ onMove(ev); };
    const upHandler = (ev) => {
      document.removeEventListener("mousemove", moveHandler);
      document.removeEventListener("mouseup", upHandler);
      document.removeEventListener("touchmove", moveHandler);
      document.removeEventListener("touchend", upHandler);
      document.removeEventListener("touchcancel", upHandler);
      onUp(ev);
    };
    document.addEventListener("mousemove", moveHandler);
    document.addEventListener("mouseup", upHandler);
    document.addEventListener("touchmove", moveHandler, { passive: false });
    document.addEventListener("touchend", upHandler);
    document.addEventListener("touchcancel", upHandler);
  }

  function getGlPageScale(fromEl) {
    /* [신규] 칸 포커스 패널의 확대 미리보기(.gl-cell-focus-clone)는 실제 캔버스
       페이지(.gl-a4-page) 밖에 따로 떨어져 있고, 자기 자신에게 별도의
       scale()이 걸려 있다 — 그 배율을 직접 읽어서 써야 드래그·회전이 정확하다. */
    const cloneEl = fromEl && fromEl.closest(".gl-cell-focus-clone");
    if (cloneEl) {
      const m = /scale\(([\d.]+)\)/.exec(cloneEl.style.transform || "");
      return m ? parseFloat(m[1]) : 1;
    }
    const pageEl = fromEl && fromEl.closest(".gl-a4-page");
    if (!pageEl || !pageEl.offsetWidth) return 1;
    return pageEl.getBoundingClientRect().width / pageEl.offsetWidth;
  }

  /* 드래그로 옮긴 오프셋이 칸 밖으로 이미지가 벗어나 빈 공간이 보이지
     않도록 제한한다. 실제 계산은 lib/label-geometry.js(LabelGeometry)에
     있음 — 단위 테스트가 그 파일을 직접 import해서 검증한다. */
  function clampImgOffset(x, y, scaleX, scaleY, rect) {
    return LabelGeometry.clampImgOffset(x, y, scaleX, scaleY, rect);
  }

  /* scaleX/scaleY 값을 9-핸들 조절 허용 범위(IMG_SCALE_MIN~MAX) 안으로 잘라낸다. */
  function clampImgScale(s) {
    return LabelGeometry.clampImgScale(s, IMG_SCALE_MIN, IMG_SCALE_MAX);
  }

  function markCellImgCustom(cellEl) {
    const ctrl = cellEl && cellEl.querySelector(".gl-cell-img-ctrl");
    if (ctrl) ctrl.classList.add("gl-cell-img-custom");
  }
  /* [신규] 칸 포커스 패널의 확대 미리보기에서 드래그했을 때도 "개별 수정됨"
     표시(리셋 아이콘 노출)가 실제 캔버스 칸에 반영되도록 idx로 직접 찾는다. */
  function markCellImgCustomByIdx(idx) {
    const realCellEl = els.canvasScroll && els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
    markCellImgCustom(realCellEl);
  }

  /* 이미지를 마우스(또는 터치)로 눌러서 끄는 동안 위치(x,y)를 옮긴다. */
  function startImgDrag(e, imgEl) {
    const cellEl = imgEl.closest(".gl-label-cell");
    if (!cellEl) return;
    e.preventDefault(); /* 텍스트 선택/포커스 이동 방지 */
    glPushUndo(); /* 신규: 드래그 시작 직전 상태를 한 번만 저장(움직이는 동안 계속 쌓지 않음) */
    const idx  = Number(cellEl.dataset.idx);
    const pageScale = getGlPageScale(cellEl); /* 실시간 WYSIWYG 강화: 현재 캔버스 배율 */
    const rawRect = imgEl.getBoundingClientRect();
    const rect = { width: rawRect.width / pageScale, height: rawRect.height / pageScale }; /* 논리 좌표계로 정규화 */
    const cur  = cellImgTransforms[idx] || { scaleX: 1, scaleY: 1, x: 0, y: 0 };
    const start = glEventXY(e);
    const startX = start.x, startY = start.y;
    const startTX = cur.x, startTY = cur.y;
    imgEl.classList.add("gl-dragging"); /* transition 끔 — 드래그 중 지연 없이 즉시 반응 */

    function onMove(ev) {
      const p = glEventXY(ev);
      const dx = (p.x - startX) / pageScale; /* 화면 이동량 → 논리 좌표계 이동량 */
      const dy = (p.y - startY) / pageScale;
      const next = clampImgOffset(startTX + dx, startTY + dy, cur.scaleX, cur.scaleY, rect);
      cellImgTransforms[idx] = Object.assign({}, cellImgTransforms[idx] || cur, { scaleX: cur.scaleX, scaleY: cur.scaleY, x: next.x, y: next.y });
      applyCellTransform(imgEl, cellImgTransforms[idx]);
    }
    function onUp() {
      imgEl.classList.remove("gl-dragging");
      markCellImgCustom(cellEl);
      /* [신규] 확대 편집 패널(clone) 안에서 드래그했다면, 실제 캔버스의
         진짜 칸은 건드리지 않은 채로 남아있으므로 여기서 최신 상태로
         다시 그려 동기화한다(패널을 닫을 때 갑자기 "튀어" 보이는 것 방지). */
      if (cellEl.classList.contains("gl-cell-focus-clone") && els.canvasScroll) {
        const real = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
        if (real) rebuildCellImgFromState(real, idx);
      }
    }
    glBindDragMove(onMove, onUp);
  }

  /* 9개 핸들(모서리4·변4·중앙1)을 드래그해서 이미지 크기(배율)·위치를 조절한다.
     - 중앙(c): 이동(startImgDrag와 동일 동작)
     - 모서리(nw/ne/sw/se, 대각선 방향): 가로·세로 비율을 유지한 채 함께 커지거나
       작아진다 — scaleX·scaleY에 같은 배수를 곱해 항상 원래 가로세로 비율을 지킨다.
     - 변(n/s: 상하, w/e: 좌우): 그 방향으로만 늘어나거나 줄어든다 — n/s는 scaleY만,
       w/e는 scaleX만 바뀌고 반대쪽 배율은 그대로 유지된다. */
  function startHandleDrag(e, handleEl) {
    const h = handleEl.dataset.h;
    const wrap = handleEl.closest(".gl-img-handles");
    const cellEl = handleEl.closest(".gl-label-cell");
    if (!wrap || !cellEl) return;
    const idx = Number(wrap.dataset.idx);
    const imgEl = cellEl.querySelector(".gl-cell-img");
    if (!imgEl) return;

    if (h === "c") { startImgDrag(e, imgEl); return; }
    if (h === "rot") { startImgRotateDrag(e, imgEl, wrap, cellEl); return; }

    e.preventDefault();
    e.stopPropagation();
    glPushUndo(); /* 신규: 크기조절 드래그 시작 직전 상태를 한 번만 저장 */
    const isCorner = h.length === 2; /* nw/ne/sw/se */
    const dir = IMG_HANDLE_DIR[h];
    const pageScale = getGlPageScale(cellEl); /* 실시간 WYSIWYG 강화: 현재 캔버스 배율 */
    const rawRect = imgEl.getBoundingClientRect();
    const rect = { width: rawRect.width / pageScale, height: rawRect.height / pageScale }; /* 논리 좌표계로 정규화 —
      배율(rect)과 위치(cur.x/y)를 같은 좌표계로 맞춰야 아래 clampImgOffset의
      경계 계산이 확대/축소 상태와 무관하게 항상 정확하다. */
    const cur  = cellImgTransforms[idx] || { scaleX: 1, scaleY: 1, x: 0, y: 0 };
    const startScaleX = cur.scaleX, startScaleY = cur.scaleY;
    const start = glEventXY(e);
    const startX = start.x, startY = start.y;
    const halfW = rect.width  / 2 || 1;
    const halfH = rect.height / 2 || 1;
    const halfSpan = Math.min(rect.width, rect.height) / 2 || 1; /* 모서리 비율유지 확대의 기준 길이 */
    imgEl.classList.add("gl-dragging");

    function onMove(ev) {
      const p = glEventXY(ev);
      const dx = (p.x - startX) / pageScale, dy = (p.y - startY) / pageScale;
      let nextScaleX = startScaleX, nextScaleY = startScaleY;

      if (isCorner) {
        /* 대각선 방향으로 이동한 거리만큼 가로·세로에 동일한 배수를 곱해
           비율을 유지한 채 함께 확대/축소한다(둘 중 하나만 바뀌지 않음). */
        const mag = Math.hypot(dir.x, dir.y) || 1;
        const ux = dir.x / mag, uy = dir.y / mag;
        const proj = dx * ux + dy * uy;
        const factor = 1 + proj / halfSpan;
        nextScaleX = clampImgScale(startScaleX * factor);
        nextScaleY = clampImgScale(startScaleY * factor);
      } else if (h === "n" || h === "s") {
        /* 상/하 변 — 세로(scaleY)만 조절, 가로는 그대로 */
        const deltaScale = (dy * dir.y) / halfH;
        nextScaleY = clampImgScale(startScaleY + deltaScale);
      } else {
        /* 좌/우 변(w/e) — 가로(scaleX)만 조절, 세로는 그대로 */
        const deltaScale = (dx * dir.x) / halfW;
        nextScaleX = clampImgScale(startScaleX + deltaScale);
      }

      const clamped = clampImgOffset(cur.x, cur.y, nextScaleX, nextScaleY, rect);
      cellImgTransforms[idx] = Object.assign({}, cellImgTransforms[idx] || cur, { scaleX: nextScaleX, scaleY: nextScaleY, x: clamped.x, y: clamped.y });
      applyCellTransform(imgEl, cellImgTransforms[idx]);
    }
    function onUp() {
      imgEl.classList.remove("gl-dragging");
      markCellImgCustom(cellEl);
      /* [신규] 확대 편집 패널(clone)에서 핸들을 드래그했다면 실제 캔버스의
         칸도 최신 상태로 다시 그려 동기화한다. */
      if (cellEl.classList.contains("gl-cell-focus-clone") && els.canvasScroll) {
        const real = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
        if (real) rebuildCellImgFromState(real, idx);
      }
    }
    glBindDragMove(onMove, onUp);
  }

  /* [신규] 회전 손잡이를 눌러서 끄는 동안 이미지를 자유로운 각도로 돌린다.
     90도 버튼과 달리 아무 각도나 가능하고, 0/90/180/270도·15도 배수
     근처(±4도)에서는 자석처럼 딱 붙는(스냅) 느낌을 준다. 이미지 중심을
     기준으로 마우스가 가리키는 방향의 각도(atan2)를 계속 재서 시작
     각도와의 차이만큼 기존 회전값에 더해준다. */
  function startImgRotateDrag(e, imgEl, wrap, cellEl) {
    const idx = Number(wrap.dataset.idx);
    if (!imgEl || Number.isNaN(idx)) return;
    e.preventDefault();
    e.stopPropagation();
    glPushUndo();
    const cur = cellImgTransforms[idx] || { scaleX: 1, scaleY: 1, x: 0, y: 0 };
    const startRotate = cur.rotate || 0;
    const rect = imgEl.getBoundingClientRect();
    const cx = rect.left + rect.width / 2;
    const cy = rect.top + rect.height / 2;
    const start = glEventXY(e);
    const startAngle = Math.atan2(start.y - cy, start.x - cx) * 180 / Math.PI;
    imgEl.classList.add("gl-dragging");
    document.body.classList.add("gl-rotating"); /* 드래그 중 커서를 전역으로 고정 */

    /* ★ROTATE-POLISH: 드래그하는 동안 현재 각도를 손잡이 위에 작은 말풍선으로
       보여준다 — 몇 도인지, 15도 배수에 스냅됐는지 눈으로 바로 확인할 수 있다. */
    const angleBadge = document.createElement("span");
    angleBadge.className = "gl-rot-angle-badge";
    wrap.appendChild(angleBadge);
    function updateAngleBadge(deg, snapped) {
      const shown = deg > 180 ? deg - 360 : deg;
      angleBadge.textContent = Math.round(shown) + "°";
      angleBadge.classList.toggle("gl-rot-angle-snapped", snapped);
    }
    updateAngleBadge(startRotate, false);

    function snapAngle(deg) {
      const SNAP = 15, THRESHOLD = 4;
      const nearest = Math.round(deg / SNAP) * SNAP;
      return Math.abs(deg - nearest) <= THRESHOLD ? nearest : deg;
    }

    function onMove(ev) {
      const p = glEventXY(ev);
      const angle = Math.atan2(p.y - cy, p.x - cx) * 180 / Math.PI;
      let next = startRotate + (angle - startAngle);
      next = ((next % 360) + 360) % 360;
      const snapped = snapAngle(next % 360 === 0 ? 0 : next);
      next = snapped;
      if (next === 360) next = 0;
      cellImgTransforms[idx] = Object.assign({}, cellImgTransforms[idx] || cur, { rotate: next });
      applyCellTransform(imgEl, cellImgTransforms[idx]);
      updateAngleBadge(next, next % 15 === 0);
    }
    function onUp() {
      imgEl.classList.remove("gl-dragging");
      document.body.classList.remove("gl-rotating");
      angleBadge.remove(); /* ★ROTATE-POLISH */
      markCellImgCustomByIdx(idx);
      updateEditToolbar();
      /* 확대 편집 패널(clone)에서 돌렸다면 실제 캔버스 칸도 동기화하고,
         반대로 실제 캔버스에서 돌렸는데 패널이 열려 있다면 미리보기도 갱신한다. */
      if (cellEl.classList.contains("gl-cell-focus-clone") && els.canvasScroll) {
        const real = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
        if (real) rebuildCellImgFromState(real, idx);
      } else if (glCellEditIdx === idx) {
        /* [버그 수정] 같은 이유(refreshCellFocusPreview는 init() 지역 함수)로
           여기서도 이름만으로 부르면 ReferenceError가 나 조용히 실패했다 —
           실제 캔버스에서 자유 회전을 돌렸는데 패널의 확대 미리보기가 그
           변화를 못 따라가던 원인. */
        if (typeof window._glRefreshCellFocusPreview === "function") window._glRefreshCellFocusPreview();
      }
    }
    glBindDragMove(onMove, onUp);
  }

  /* [신규] 방향키로 이미지 위치를 1px(Shift+방향키는 10px) 단위로 미세
     조정한다 — 포토샵/피그마의 방향키 넛지와 같은 관례. 마우스 드래그만으로는
     손이 떨리거나 아주 정밀하게 맞추기 어려울 때 쓴다. 캔버스에서 칸을
     선택했거나(selectedIdx) 칸 포커스 패널이 열려 있으면(glCellEditIdx)
     그 칸에 지금 보이는 이미지를 대상으로 하고, 연속으로 누르고 있는
     동안(600ms 이내 연타)은 하나의 되돌리기 단위로 묶는다(1px씩 수십 번
     눌러도 Ctrl+Z 한 번으로 전부 되돌아가도록). */
  let glLastNudgeAt = 0;
  function nudgeSelectedCellImg(dx, dy) {
    const idx = glCellEditIdx != null ? glCellEditIdx : selectedIdx;
    if (idx == null) return false;
    const eff = glEffContentType(idx);
    if (eff !== "image" && eff !== "both") return false;
    if (!(cellImages[idx] || imageDataUrl)) return false; /* 이 칸에 실제로 보이는 이미지가 없으면 넛지할 대상이 없음 */
    const cellEl = els.canvasScroll && els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
    const imgEl = cellEl && cellEl.querySelector(".gl-cell-img");
    if (!imgEl) return false;

    const now = Date.now();
    if (now - glLastNudgeAt > 600) glPushUndo(); /* 연속 넛지는 하나의 되돌리기 단위로 묶음 */
    glLastNudgeAt = now;

    const pageScale = getGlPageScale(cellEl);
    const rawRect = imgEl.getBoundingClientRect();
    const rect = { width: rawRect.width / pageScale, height: rawRect.height / pageScale };
    const cur = cellImgTransforms[idx] || { scaleX: 1, scaleY: 1, x: 0, y: 0 };
    const next = clampImgOffset(cur.x + dx, cur.y + dy, cur.scaleX, cur.scaleY, rect);
    cellImgTransforms[idx] = Object.assign({}, cur, { x: next.x, y: next.y });
    applyCellTransform(imgEl, cellImgTransforms[idx]);
    markCellImgCustom(cellEl);
    updateEditToolbar();
    if (glCellEditIdx === idx && typeof window._glRefreshCellFocusPreview === "function") window._glRefreshCellFocusPreview();
    return true;
  }


  /* [신규] 이 칸(srcIdx)의 텍스트·이미지·서식·위치·회전·자르기·보정을 통째로
     복사해 나머지 모든 칸에 붙여넣는다. "전체 칸에 적용"(사이드바)은 항상
     공통 기본값만 채우는 것과 달리, 이미 공들여 다듬은 한 칸의 결과를
     그대로 복제하고 싶을 때 쓴다.
     ⚠️ 참조 공유 주의: cellImgTransforms[idx] = cellImgTransforms[srcIdx]처럼
     객체를 그대로 나눠주면 여러 칸이 같은 객체를 공유하게 되어, 나중에
     한 칸만 회전해도 "채워진" 칸들이 전부 같이 바뀌는 사고가 난다(undo
     스냅샷이 얕은 복사라 생겼던 문제와 같은 종류의 함정). 칸마다 독립된
     사본을 만들어 배정한다. */
  function cellCopyField(store, srcIdx, idx, deepClone) {
    const v = store[srcIdx];
    if (v == null) { delete store[idx]; return; }
    if (deepClone) store[idx] = JSON.parse(JSON.stringify(v));
    else if (typeof v === "object") store[idx] = Object.assign({}, v);
    else store[idx] = v;
  }
  async function fillOtherCellsFromCell(srcIdx) {
    const c = CFG[currentTplKey];
    if (!c || !c.perPage) return;
    const total = c.perPage;
    if (total <= 1) { showToast("이 규격은 칸이 하나뿐이라 채울 다른 칸이 없어요.", "info", 2200); return; }
    const ok = await showConfirm(
      (srcIdx + 1) + "번 칸의 텍스트·이미지·서식·위치·회전·자르기·보정을 나머지 " + (total - 1) + "개 칸에 그대로 채웁니다. 각 칸의 기존 내용은 덮어씌워집니다. 계속할까요?",
      { icon: "📋", title: "이 칸을 다른 칸에 채우기", okLabel: "채우기" }
    );
    if (!ok) return;
    glPushUndo();
    for (let idx = 0; idx < total; idx++) {
      if (idx === srcIdx) continue;
      cellCopyField(cellTexts, srcIdx, idx);
      cellCopyField(cellImages, srcIdx, idx);
      cellCopyField(cellImgTransforms, srcIdx, idx, true);   /* 깊은 복사 */
      cellCopyField(cellFontSizes, srcIdx, idx);
      cellCopyField(cellFontFamilies, srcIdx, idx);
      cellCopyField(cellColors, srcIdx, idx);
      cellCopyField(cellStyles, srcIdx, idx, true);
      cellCopyField(cellAligns, srcIdx, idx, true);
      cellCopyField(cellContentTypes, srcIdx, idx);
      cellCopyField(cellContentLayouts, srcIdx, idx);
    }
    rebuildCanvas();
    updateEditToolbar();
    showToast("✅ " + (srcIdx + 1) + "번 칸의 편집 내용을 나머지 " + (total - 1) + "개 칸에 채웠습니다.", "info", 2600);
  }

  function resetCellImgTransform(ctrl) {
    const idx = Number(ctrl.dataset.idx);
    if (cellImgTransforms[idx]) glPushUndo();
    /* [신규] 보정은 전용 "보정 초기화" 버튼(칸 포커스 패널)에서만 지운다 —
       위치·크기·회전·자르기·반전 초기화 버튼들과 스코프를 통일. */
    const filt = cellImgTransforms[idx] && cellImgTransforms[idx].filter;
    if (filt) cellImgTransforms[idx] = { scaleX: 1, scaleY: 1, x: 0, y: 0, filter: filt };
    else delete cellImgTransforms[idx];
    /* [수정] ctrl.closest(".gl-label-cell") 대신 idx로 실제 캔버스 칸을 직접 찾는다 —
       이 ctrl이 칸 포커스 패널의 확대 미리보기(복제본) 안에 있을 수도 있어서,
       그 경우 예전 방식대로면 복제본만 다시 그려지고 실제 캔버스는 그대로 남는
       문제가 있었다. rotateCellImg·flipCellImg와 동일한 방식으로 통일. */
    const realCellEl = els.canvasScroll && els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
    rebuildCellImgFromState(realCellEl, idx);
    /* 확대 미리보기 쪽에서 눌렀다면 미리보기도 최신 상태로 다시 그린다.
       [버그 수정] 아래 typeof 가드는 항상 false였다 — refreshCellFocusPreview는
       init() 지역 함수라 여기서는 애초에 존재하지 않는 이름이었기 때문(그래서
       조용히 아무 일도 안 하고 넘어갔다). window._glRefreshCellFocusPreview로
       노출해둔 참조를 써야 실제로 갱신된다. */
    if (glCellEditIdx === idx && typeof window._glRefreshCellFocusPreview === "function") window._glRefreshCellFocusPreview();
  }

  /* 자르기 적용/초기화처럼 이미지 소스(src) 자체가 바뀌어야 하는 경우를 위해
     캔버스 전체를 재빌드하지 않고 그 칸만 새로 그려 넣는다. */
  function rebuildCellImgFromState(cellEl, idx) {
    if (!cellEl) return;
    const fresh = buildCell(idx);
    cellEl.replaceWith(fresh);
  }

  /* ══════════════════════════════════════════════════════════════
     신규: 칸 선택 + 상단 편집 툴바
     캔버스에서 라벨(칸)을 클릭하면 그 칸이 "선택"되고 상단에 편집
     툴바가 나타난다. "전체 칸에 적용"과는 별개로, 선택된 칸 하나에만
     이미지를 교체하거나 텍스트를 지울 수 있다. 한 번에 하나의 칸만
     선택할 수 있다(단순하고 명확한 동작을 위해).
     ══════════════════════════════════════════════════════════════ */
  function selectCell(idx) {
    if (idx === selectedIdx) { updateEditToolbar(); return; }
    const prevIdx = selectedIdx;
    selectedIdx = idx;
    if (prevIdx != null) {
      const prevEl = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + prevIdx + '"]');
      if (prevEl) prevEl.classList.remove("gl-cell-selected");
    }
    const nextEl = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
    if (nextEl) nextEl.classList.add("gl-cell-selected");
    updateEditToolbar();
  }
  function deselectCell() {
    clearMultiSelect(); /* [신규] 빈 여백 클릭 시 다중 선택도 함께 해제 */
    if (selectedIdx == null) return;
    const prevEl = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + selectedIdx + '"]');
    if (prevEl) prevEl.classList.remove("gl-cell-selected");
    selectedIdx = null;
    updateEditToolbar();
  }

  /* ══════════════════════════════════════════════════════════════
     [신규] 여러 칸 한꺼번에 선택 — 상태 관리 + 시각 강조
     ══════════════════════════════════════════════════════════════ */
  function renderMultiSelectHighlight() {
    /* 우선 이전에 칠했던 강조를 전부 지우고(잔여물 방지), 지금 집합만 다시 칠한다.
       DOM을 통째로 다시 그리는 rebuildCanvas() 이후에도 이 함수 한 번이면
       강조가 그대로 복원된다(칸 자체엔 다중 선택 상태를 기억시키지 않음 — 상태는
       항상 multiSelectedIdxs 하나에만 둔다). */
    els.canvasScroll.querySelectorAll(".gl-cell-multiselected").forEach(el => el.classList.remove("gl-cell-multiselected"));
    if (multiSelectedIdxs.size < 2) return;
    multiSelectedIdxs.forEach(i => {
      const el = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + i + '"]');
      if (el) el.classList.add("gl-cell-multiselected");
    });
  }
  function clearMultiSelect() {
    if (!multiSelectedIdxs.size) { multiSelectAnchor = null; return; }
    els.canvasScroll.querySelectorAll(".gl-cell-multiselected").forEach(el => el.classList.remove("gl-cell-multiselected"));
    multiSelectedIdxs = new Set();
    multiSelectAnchor = null;
  }
  /* Ctrl(Cmd)+클릭 — 이 칸 하나만 선택 집합에 넣거나 뺀다(토글) */
  function toggleMultiSelect(idx) {
    if (!multiSelectedIdxs.size && selectedIdx != null) {
      /* 기존에 단일 선택돼 있던 칸이 있으면 그 칸부터 집합에 합류시켜 시작 —
         "지금 선택돼 있던 칸 + 방금 Ctrl+클릭한 칸" 2칸으로 다중 선택이 시작된다. */
      const prevEl = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + selectedIdx + '"]');
      if (prevEl) prevEl.classList.remove("gl-cell-selected");
      multiSelectedIdxs.add(selectedIdx);
    }
    if (multiSelectedIdxs.has(idx)) multiSelectedIdxs.delete(idx);
    else multiSelectedIdxs.add(idx);
    multiSelectAnchor = idx;
    finishMultiSelectChange(idx);
  }
  /* Shift+클릭 — 고정된 시작점(anchor)부터 이 칸까지를 통째로 범위 선택 */
  function extendMultiSelectRange(idx) {
    const anchor = multiSelectAnchor != null ? multiSelectAnchor : (selectedIdx != null ? selectedIdx : idx);
    multiSelectAnchor = anchor;
    if (selectedIdx != null && !multiSelectedIdxs.size) {
      const prevEl = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + selectedIdx + '"]');
      if (prevEl) prevEl.classList.remove("gl-cell-selected");
    }
    const lo = Math.min(anchor, idx), hi = Math.max(anchor, idx);
    multiSelectedIdxs = new Set();
    for (let i = lo; i <= hi; i++) multiSelectedIdxs.add(i);
    finishMultiSelectChange(idx);
  }
  /* 토글/범위 선택 뒤 공통 마무리 — 0~1칸만 남으면 평소의 단일 선택으로
     자연스럽게 되돌리고, 2칸 이상이면 다중 선택 강조를 그린다. */
  function finishMultiSelectChange(lastIdx) {
    if (multiSelectedIdxs.size <= 1) {
      const only = multiSelectedIdxs.size === 1 ? [...multiSelectedIdxs][0] : null;
      multiSelectedIdxs = new Set();
      if (only != null) selectCell(only); else deselectCell();
      return;
    }
    selectedIdx = lastIdx; /* 마지막으로 클릭한 칸을 "대표 칸"으로 — 상단 편집 툴바 등이 이 칸을 대상으로 계속 동작 */
    renderMultiSelectHighlight();
    updateEditToolbar();
  }

  /* [신규] 다중 선택된 칸들에 한번에 적용하는 일괄 작업 — 되돌리기는 한 번에
     묶어서 기록한다(칸마다 따로 기록하면 되돌릴 때 N번 눌러야 하므로).
     각 칸에 실제로 하는 일은 단일 칸용 함수(resetCellFully 등)와 동일하다. */
  function resetMultiSelectedCells() {
    if (multiSelectedIdxs.size < 2) return;
    const n = multiSelectedIdxs.size;
    glPushUndo();
    multiSelectedIdxs.forEach(idx => {
      delete cellTexts[idx]; delete cellFontSizes[idx];
      delete cellImages[idx]; delete cellImgTransforms[idx];
      delete cellFontFamilies[idx]; delete cellColors[idx];
      delete cellStyles[idx]; delete cellAligns[idx];
      delete cellContentTypes[idx]; delete cellContentLayouts[idx];
      delete cellTables[idx];
    });
    rebuildCanvas();
    renderMultiSelectHighlight();
    updateEditToolbar();
    showToast(`✅ 선택한 ${n}칸을 초기화했습니다.`, "success", 1800);
  }
  function clearTextMultiSelectedCells() {
    if (multiSelectedIdxs.size < 2) return;
    const n = multiSelectedIdxs.size;
    glPushUndo();
    multiSelectedIdxs.forEach(idx => { delete cellTexts[idx]; delete cellFontSizes[idx]; delete cellTables[idx]; });
    rebuildCanvas();
    renderMultiSelectHighlight();
    updateEditToolbar();
    showToast(`🧹 선택한 ${n}칸의 텍스트를 지웠습니다.`, "success", 1800);
  }
  function clearImageMultiSelectedCells() {
    if (multiSelectedIdxs.size < 2) return;
    const n = multiSelectedIdxs.size;
    glPushUndo();
    multiSelectedIdxs.forEach(idx => { delete cellImages[idx]; delete cellImgTransforms[idx]; });
    rebuildCanvas();
    renderMultiSelectHighlight();
    updateEditToolbar();
    showToast(`🖼️ 선택한 ${n}칸의 개별 이미지를 해제했습니다.`, "success", 1800);
  }
  /* 선택 상태에 맞춰 상단 편집 툴바를 표시/갱신한다. 콘텐츠 타입에 따라
     이미지·텍스트 관련 버튼을 켜고 끈다(예: "텍스트" 모드에서는 이미지가
     화면에 전혀 보이지 않으므로 이미지 교체 버튼을 비활성화). */
  function updateEditToolbar() {
    if (!els.editToolbar) return;
    if (selectedIdx == null) { els.editToolbar.style.display = "none"; return; }
    els.editToolbar.style.display = "flex";
    if (els.editToolbarIdx) els.editToolbarIdx.textContent = String(selectedIdx + 1);
    /* [신규] "이 칸" 글꼴·크기·색상·스타일 빠른 컨트롤에 지금 선택된 칸의
       실제 적용값을 채운다 — 조작 중(포커스)인 컨트롤은 값을 다시 써서
       사용자 입력을 방해하지 않도록 건너뛴다. */
    const fmt = glEffTextFormat(selectedIdx);
    if (els.editFontSelect && document.activeElement !== els.editFontSelect) els.editFontSelect.value = fmt.fam;
    if (els.editFontSizeSelect && document.activeElement !== els.editFontSizeSelect && typeof window._glSetSelectByFloatValue === "function") window._glSetSelectByFloatValue(els.editFontSizeSelect, fmt.fsz);
    if (els.editColorInput && document.activeElement !== els.editColorInput) els.editColorInput.value = fmt.col;
    if (typeof window._glSetGlBtnPressed === "function") {
      window._glSetGlBtnPressed(els.editBoldBtn, fmt.sty.bold);
      window._glSetGlBtnPressed(els.editItalicBtn, fmt.sty.italic);
      window._glSetGlBtnPressed(els.editUnderlineBtn, fmt.sty.underline);
    }
    if (els.editTextResetBtn) {
      const hasOverride = cellFontFamilies[selectedIdx] || cellColors[selectedIdx] || cellStyles[selectedIdx];
      els.editTextResetBtn.style.display = hasOverride ? "" : "none";
    }
    const effType = glEffContentType(selectedIdx);
    const imgModeOn = effType === "image" || effType === "both";
    const txtModeOn = effType === "text" || effType === "both";
    if (els.editImgReplaceBtn) {
      els.editImgReplaceBtn.disabled = !imgModeOn;
      els.editImgReplaceBtn.title = imgModeOn
        ? "이 칸에만 다른 이미지 넣기"
        : "먼저 좌측에서 '이미지' 또는 '텍스트+이미지' 모드를 선택하세요";
    }
    /* 신규: 이 칸에 실제로 표시 중인 이미지(개별 또는 공통)가 있을 때만 자르기 버튼 노출 */
    if (els.editImgCropBtn) els.editImgCropBtn.style.display = (imgModeOn && !!(cellImages[selectedIdx] || imageDataUrl)) ? "" : "none";
    /* 신규: 자르기와 동일 조건 — 표시 중인 이미지가 있을 때만 회전 버튼 노출 */
    if (els.editImgRotateBtn) els.editImgRotateBtn.style.display = (imgModeOn && !!(cellImages[selectedIdx] || imageDataUrl)) ? "" : "none";
    /* ★FLIP: 반전 버튼도 회전과 동일 조건 */
    if (els.editImgFlipXBtn) els.editImgFlipXBtn.style.display = (imgModeOn && !!(cellImages[selectedIdx] || imageDataUrl)) ? "" : "none";
    if (els.editImgFlipYBtn) els.editImgFlipYBtn.style.display = (imgModeOn && !!(cellImages[selectedIdx] || imageDataUrl)) ? "" : "none";
    /* 신규: 위치·크기·자르기 개별 조정이 실제로 있을 때만 초기화 버튼 노출 */
    if (els.editImgResetBtn) els.editImgResetBtn.style.display = (imgModeOn && !!cellImgTransforms[selectedIdx]) ? "" : "none";
    if (els.editImgClearBtn) els.editImgClearBtn.style.display = (imgModeOn && cellImages[selectedIdx]) ? "" : "none";
    if (els.editTextClearBtn) els.editTextClearBtn.style.display = txtModeOn ? "" : "none";
    /* [신규] 표 빠른 도구 — 이 칸에 실제 표가 있을 때만 노출 */
    const hasTable = !!(cellTables[selectedIdx] && cellTables[selectedIdx].rows && cellTables[selectedIdx].rows.length);
    if (els.editTableGroup)     els.editTableGroup.style.display     = hasTable ? "" : "none";
    if (els.editTableAddRowBtn) els.editTableAddRowBtn.style.display = hasTable ? "" : "none";
    if (els.editTableAddColBtn) els.editTableAddColBtn.style.display = hasTable ? "" : "none";
    if (els.editTableDeleteBtn) els.editTableDeleteBtn.style.display = hasTable ? "" : "none";
  }

  /* 캔버스 영역 델리게이션 mousedown 핸들러 — 글자 크기 미니 컨트롤,
     이미지 위치 초기화 버튼, 이미지 드래그 시작을 한곳에서 처리한다.
     canvasScroll에 1회만 등록되며 rebuildCanvas로 페이지가 교체돼도
     (자식이 바뀌어도) 계속 동작한다(이벤트 위임). */
  function onCanvasMouseDown(e) {
    /* 신규: 어떤 칸을 클릭하든(텍스트 편집·이미지 드래그와 무관하게) 그 칸을 선택하고,
       칸이 아닌 여백(페이지 배경 등)을 클릭하면 선택을 해제한다. */
    const hitCell = e.target.closest(".gl-label-cell");
    /* [신규] Shift+클릭(범위 선택)/Ctrl(Cmd)+클릭(개별 토글) — 여러 칸을 한꺼번에
       선택한다. 두 경우 모두 그 클릭이 텍스트 편집·이미지 드래그로 이어지면
       안 되므로(다중 선택 중 실수로 칸 하나만 수정되는 걸 막기 위해) 여기서
       바로 처리를 끝낸다(다른 델리게이션 로직으로 내려가지 않음). */
    if (hitCell) {
      const idx = Number(hitCell.dataset.idx);
      if (e.shiftKey) { e.preventDefault(); extendMultiSelectRange(idx); return; }
      if (e.ctrlKey || e.metaKey) { e.preventDefault(); toggleMultiSelect(idx); return; }
      if (multiSelectedIdxs.size) clearMultiSelect(); /* 보통 클릭이면 다중 선택을 풀고 이 칸 하나만 선택 */
      selectCell(idx);
    }
    else deselectCell();
    if (e.target.closest(".gl-cell-fsz-btn")) { onCellFszCtrlClick(e); return; }
    const rotateBtn = e.target.closest(".gl-cell-img-rotate");
    if (rotateBtn) {
      e.preventDefault();
      const ctrl = rotateBtn.closest(".gl-cell-img-ctrl");
      if (ctrl) rotateCellImg(Number(ctrl.dataset.idx), 90);
      return;
    }
    const flipBtn = e.target.closest(".gl-cell-img-flip");
    if (flipBtn) {
      e.preventDefault();
      const ctrl = flipBtn.closest(".gl-cell-img-ctrl");
      if (ctrl) flipCellImg(Number(ctrl.dataset.idx), flipBtn.dataset.act === "flip-y" ? "y" : "x");
      return;
    }
    const cropBtn = e.target.closest(".gl-cell-img-crop");
    if (cropBtn) {
      e.preventDefault();
      const ctrl = cropBtn.closest(".gl-cell-img-ctrl");
      if (ctrl) openCropModal(Number(ctrl.dataset.idx));
      return;
    }
    const resetBtn = e.target.closest(".gl-cell-img-reset");
    if (resetBtn) {
      e.preventDefault();
      const ctrl = resetBtn.closest(".gl-cell-img-ctrl");
      if (ctrl) resetCellImgTransform(ctrl);
      return;
    }
    const handle = e.target.closest(".gl-img-handle");
    if (handle) { startHandleDrag(e, handle); return; }
    const imgEl = e.target.closest(".gl-cell-img");
    if (imgEl) { startImgDrag(e, imgEl); return; }
  }

  /* ══════════════════════════════════════════════════════════════
     이미지 자르기(Crop) 모달
     칸의 핸들 배율 조절과는 별개로, 업로드한 원본 이미지에서 실제로
     사용할 사각 영역을 잘라낸다. 잘라낸 결과는 canvas로 그려 새 데이터
     URL(croppedSrc)로 저장되고, 그 칸에서만(다른 칸엔 영향 없이) 쓰인다.
     항상 원본(imageDataUrl) 기준으로 다시 자르므로 여러 번 잘라도
     화질이 누적으로 손실되지 않는다.
     ══════════════════════════════════════════════════════════════ */
  let cropEls = null;   /* 모달 DOM 참조 캐시 */
  let cropCtx  = null;  /* 현재 자르는 중인 상태 { idx, natW, natH, imgLeft, imgTop, imgW, imgH, box:{x,y,w,h} } */
  const CROP_MIN_PX = 24; /* 자르기 박스 최소 크기(px, 모달 화면 기준) */

  function buildCropModal() {
    if (cropEls) return;
    const bg = document.createElement("div");
    bg.id = "gl-crop-modal";
    bg.className = "modal-bg gl-crop-modal";
    bg.style.display = "none";
    bg.innerHTML =
      '<div class="gl-crop-card">' +
        '<div class="gl-crop-head"><span>이미지 자르기</span><button type="button" class="gl-crop-x" title="닫기" aria-label="이미지 자르기 취소">✕</button></div>' +
        '<div class="gl-crop-stage">' +
          '<img class="gl-crop-img" alt="" draggable="false"/>' +
          '<div class="gl-crop-select">' +
            IMG_HANDLE_ORDER.map(h => '<span class="gl-crop-handle" data-h="' + h + '"></span>').join("") +
          '</div>' +
        '</div>' +
        '<p class="gl-crop-hint">모서리·변 핸들을 끌어 자를 영역을 정하고, 가운데를 끌어 옮기세요.</p>' +
        '<div class="gl-crop-foot">' +
          '<button type="button" class="btn btn-ghost gl-crop-restore">원본 복원</button>' +
          '<div class="gl-crop-foot-right">' +
            '<button type="button" class="btn btn-ghost gl-crop-cancel">취소</button>' +
            '<button type="button" class="btn btn-primary gl-crop-apply">✔ 적용</button>' +
          '</div>' +
        '</div>' +
      '</div>';
    document.body.appendChild(bg);

    cropEls = {
      bg,
      stage:      bg.querySelector(".gl-crop-stage"),
      img:        bg.querySelector(".gl-crop-img"),
      select:     bg.querySelector(".gl-crop-select"),
      restoreBtn: bg.querySelector(".gl-crop-restore"),
      cancelBtn:  bg.querySelector(".gl-crop-cancel"),
      applyBtn:   bg.querySelector(".gl-crop-apply"),
      closeBtn:   bg.querySelector(".gl-crop-x"),
    };

    cropEls.closeBtn.addEventListener("click", closeCropModal);
    cropEls.cancelBtn.addEventListener("click", closeCropModal);
    bg.addEventListener("mousedown", (e) => { if (e.target === bg) closeCropModal(); });
    document.addEventListener("keydown", (e) => {
      if (e.key === "Escape" && cropEls.bg.classList.contains("active")) closeCropModal();
    });

    cropEls.restoreBtn.addEventListener("click", () => {
      if (!cropCtx) return;
      const idx = cropCtx.idx;
      const t = cellImgTransforms[idx];
      if (t) {
        delete t.croppedSrc; delete t.crop;
        /* ★FLIP: scaleX/scaleY·위치가 기본값이어도 회전(rotate)이나 반전(flipX/flipY)이
           남아있으면 그 상태를 지우면 안 되므로 함께 확인한다. */
        if (t.scaleX === 1 && t.scaleY === 1 && t.x === 0 && t.y === 0 && !t.rotate && !t.flipX && !t.flipY && !t.filter) delete cellImgTransforms[idx];
      }
      rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
      closeCropModal();
    });

    cropEls.applyBtn.addEventListener("click", applyCrop);

    cropEls.select.querySelectorAll(".gl-crop-handle").forEach(h => {
      h.addEventListener("mousedown", startCropHandleDrag);
      h.addEventListener("touchstart", startCropHandleDrag, { passive: false }); /* [신규] 터치 지원 */
    });
  }

  function openCropModal(idx) {
    /* 변경: 이 칸만의 개별 이미지가 있으면 그 이미지를, 없으면 전역 공통 이미지를 기준으로 자른다 */
    const baseSrc = cellImages[idx] || imageDataUrl;
    if (!baseSrc) return;
    buildCropModal();
    cropCtx = { idx, base: baseSrc };
    const img = cropEls.img;
    img.onload = () => layoutCropStage(idx);
    img.src = baseSrc; /* 항상 원본(이 칸의 개별 이미지 또는 공통 이미지) 기준으로 다시 자르기 시작 */
    cropEls.bg.style.display = "flex";
    requestAnimationFrame(() => cropEls.bg.classList.add("active"));
    if (img.complete && img.naturalWidth) layoutCropStage(idx);
    /* 안전장치: 모달이 열리는 애니메이션이 끝난 직후 한 번 더 계산해,
       카드 크기 변화(반응형 max-width:92vw 등)로 인한 어긋남까지 보정한다. */
    setTimeout(() => { if (cropCtx && cropCtx.idx === idx) layoutCropStage(idx); }, 200);
  }

  function closeCropModal() {
    if (!cropEls) return;
    cropEls.bg.classList.remove("active");
    setTimeout(() => { if (cropEls && !cropEls.bg.classList.contains("active")) cropEls.bg.style.display = "none"; }, 180);
    cropCtx = null;
  }

  /* 모달 안에서 이미지가 실제로 렌더링되는 위치·크기(object-fit:contain 기준)를
     계산하고, 기존에 저장된 자르기 영역이 있으면 그 위치로, 없으면
     이미지 전체 크기로 선택 박스를 초기화한다.
     버그 수정: getBoundingClientRect()는 모달이 열리는 중(scale(.96)→scale(1)
     애니메이션 도중)의 "그려진" 크기를 반환해, 그 순간 기준으로 계산한 선택 박스가
     애니메이션이 끝난 뒤 실제 이미지 전체 크기보다 살짝 작게 어긋나 보이는 문제가
     있었다. clientWidth/clientHeight(레이아웃 크기, transform의 영향을 받지 않음)로
     계산하도록 바꿔 항상 이미지 전체 영역과 정확히 일치하게 한다. */
  function layoutCropStage(idx) {
    const img = cropEls.img;
    const stageW = cropEls.stage.clientWidth, stageH = cropEls.stage.clientHeight;
    const natW = img.naturalWidth, natH = img.naturalHeight;
    if (!natW || !natH || !stageW || !stageH) return;
    const stageRatio = stageW / stageH;
    const imgRatio = natW / natH;
    let dispW, dispH;
    if (imgRatio > stageRatio) { dispW = stageW; dispH = dispW / imgRatio; }
    else { dispH = stageH; dispW = dispH * imgRatio; }
    const left = (stageW - dispW) / 2;
    const top  = (stageH - dispH) / 2;

    cropCtx = Object.assign(cropCtx || {}, { idx, natW, natH, imgLeft: left, imgTop: top, imgW: dispW, imgH: dispH });

    const existing = cellImgTransforms[idx] && cellImgTransforms[idx].crop;
    const rx = existing ? existing.rx : 0, ry = existing ? existing.ry : 0;
    const rw = existing ? existing.rw : 1, rh = existing ? existing.rh : 1;
    cropCtx.box = { x: left + rx * dispW, y: top + ry * dispH, w: rw * dispW, h: rh * dispH };
    renderCropSelect();
  }

  function renderCropSelect() {
    const b = cropCtx.box;
    cropEls.select.style.left   = b.x + "px";
    cropEls.select.style.top    = b.y + "px";
    cropEls.select.style.width  = b.w + "px";
    cropEls.select.style.height = b.h + "px";
  }

  /* 자르기 박스의 9개 핸들 드래그 — 모서리/변은 그 방향으로 박스 경계를
     밀어 크기를 바꾸고(항상 이미지 영역 안쪽으로 제한), 중앙은 박스 전체를
     이동시킨다(스케일용 9핸들과 달리 실제 박스 크기·위치 자체를 바꾼다). */
  function startCropHandleDrag(e) {
    e.preventDefault();
    e.stopPropagation();
    const h = e.currentTarget.dataset.h;
    const start = glEventXY(e);
    const box0 = Object.assign({}, cropCtx.box);
    const bounds = {
      left: cropCtx.imgLeft, top: cropCtx.imgTop,
      right: cropCtx.imgLeft + cropCtx.imgW, bottom: cropCtx.imgTop + cropCtx.imgH,
    };

    function onMove(ev) {
      const p = glEventXY(ev);
      const dx = p.x - start.x, dy = p.y - start.y;
      let { x, y, w, h: hh } = box0;
      if (h === "c") {
        x = Math.min(Math.max(box0.x + dx, bounds.left), bounds.right - w);
        y = Math.min(Math.max(box0.y + dy, bounds.top), bounds.bottom - hh);
      } else {
        if (h.includes("w")) { const nx = Math.min(Math.max(box0.x + dx, bounds.left), box0.x + w - CROP_MIN_PX); w = box0.x + w - nx; x = nx; }
        if (h.includes("e")) { w = Math.min(Math.max(box0.w + dx, CROP_MIN_PX), bounds.right - box0.x); }
        if (h.includes("n")) { const ny = Math.min(Math.max(box0.y + dy, bounds.top), box0.y + hh - CROP_MIN_PX); hh = box0.y + hh - ny; y = ny; }
        if (h.includes("s")) { hh = Math.min(Math.max(box0.h + dy, CROP_MIN_PX), bounds.bottom - box0.y); }
      }
      cropCtx.box = { x, y, w, h: hh };
      renderCropSelect();
    }
    function onUp() {}
    glBindDragMove(onMove, onUp);
  }

  /* 선택 박스를 원본 이미지의 실제 픽셀 좌표로 환산해 canvas에 그린 뒤
     데이터 URL로 저장한다 — 이 칸만 그 잘린 이미지를 사용하게 된다. */
  function applyCrop() {
    if (!cropCtx) return;
    const { idx, natW, natH, imgLeft, imgTop, imgW, imgH, box } = cropCtx;
    const rx = (box.x - imgLeft) / imgW;
    const ry = (box.y - imgTop) / imgH;
    const rw = box.w / imgW;
    const rh = box.h / imgH;

    const sx = Math.max(0, Math.round(rx * natW));
    const sy = Math.max(0, Math.round(ry * natH));
    const sw = Math.max(1, Math.round(rw * natW));
    const sh = Math.max(1, Math.round(rh * natH));

    const canvas = document.createElement("canvas");
    canvas.width = sw; canvas.height = sh;
    const ctx2d = canvas.getContext("2d");
    const srcImg = new Image();
    srcImg.onload = () => {
      ctx2d.drawImage(srcImg, sx, sy, sw, sh, 0, 0, sw, sh);
      const dataUrl = canvas.toDataURL("image/png");
      glPushUndo();
      cellImgTransforms[idx] = { scaleX: 1, scaleY: 1, x: 0, y: 0, croppedSrc: dataUrl, crop: { rx, ry, rw, rh } };
      rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
      closeCropModal();
      /* [버그 수정] refreshCellFocusPreview는 init() 안에서만 선언돼 있어 여기
         (init() 밖)에서 이름으로 직접 부르면 ReferenceError가 난다 —
         window._glRefreshCellFocusPreview로 노출해둔 것을 사용한다. */
      if (window._glRefreshCellFocusPreview) window._glRefreshCellFocusPreview(); /* [신규] 칸 포커스 패널에서 자르기를 열었다면 확대 미리보기도 새 결과로 갱신 */
    };
    srcImg.src = cropCtx.base || imageDataUrl;
  }

  /* ══════════════════════════════════════════════════════════════
     변경: 우클릭 컨텍스트 메뉴 — 주소 라벨과 동일한 형태(.ctx-menu
     공통 스타일 + 열림/닫힘 애니메이션 + 화면 밖으로 넘치지 않는 위치
     보정)로, 이미지가 있는 칸뿐 아니라 텍스트 칸·빈 칸을 포함한 모든
     칸에서 동작한다. 메뉴 내용(자르기·이미지 교체 등)은 칸의 콘텐츠
     타입과 현재 상태에 맞춰 항목을 보이거나 숨긴다.
     ══════════════════════════════════════════════════════════════ */
  let glCtxIdx = null;
  let glCtxBatchMode = false; /* [신규] true면 우클릭 메뉴가 다중 선택 일괄 작업 메뉴로 표시된 상태 */
  let glCellClipboard = null; /* { text, fontMult, imgSrc, transform } — 칸 복사/붙여넣기용 */

  /* [수정] "다른 칸으로 이 칸 채우기"(원본 고르기 모드)는 요청 의도와 달라
     제거하고, 대신 이 칸의 텍스트뿐 아니라 서식(글꼴·크기·색·굵게/기울임/밑줄·
     정렬)·이미지·자르기·회전·확대 상태까지 통째로 스냅샷을 떠서, 라벨의
     "아직 비어 있는" 나머지 칸에만 채워 넣는다(glCellSnapshot/glApplyCellSnapshot
     재사용). 이미 텍스트나 이미지가 있는 칸은 실수로 덮어쓰지 않도록 건드리지 않는다. */
  function isGlCellEmpty(idx) {
    const effText = cellTexts[idx] != null ? cellTexts[idx] : commonText;
    const hasText = !!(effText && String(effText).trim());
    const hasImg  = !!(cellImages[idx] || imageDataUrl);
    /* [버그 수정] 표만 있고 일반 텍스트·이미지는 없는 칸을 "빈 칸"으로
       잘못 취급하던 문제 — 표는 cellTexts가 아니라 cellTables에 따로
       저장되는데 여기서 전혀 확인하지 않았다. 그 결과 (1) 표가 있는
       칸이 "빈 칸 채우기"의 대상이 되어 다른 칸 내용에 덮어써질 수
       있었고, (2) "이 칸 채우기"의 원본으로 표가 있는 칸을 써도 표
       내용 자체는 스냅샷에 담기지 않아(아래 glCellSnapshot 참고)
       빈 칸에 적용해도 표가 나타나지 않았다. */
    const hasTable = !!(cellTables[idx] && cellTables[idx].rows && cellTables[idx].rows.length);
    return !hasText && !hasImg && !hasTable;
  }
  function fillEmptyCellsWithCellSnapshot(sourceIdx) {
    const c = CFG[currentTplKey];
    if (!c) return;
    const snap = glCellSnapshot(sourceIdx);
    const targets = [];
    for (let i = 0; i < c.perPage; i++) {
      if (i === sourceIdx) continue;
      if (isGlCellEmpty(i)) targets.push(i);
    }
    if (!targets.length) { showToast("채울 빈 칸이 없어요 — 나머지 칸에는 이미 내용이 있습니다.", "info", 2400); return; }
    glPushUndo();
    targets.forEach(i => glApplyCellSnapshot(i, snap));
    showToast("✅ 빈 칸 " + targets.length + "개에 이 칸 내용을 채웠습니다.", "success", 2000);
  }

  /* 칸 하나의 현재 내용을 스냅샷으로 저장 — 다른 칸에 붙여넣을 때 사용.
     공통 이미지 자체는 포함하지 않는다(원본 칸이 개별 이미지가 없었다면,
     붙여넣은 칸도 오버라이드 없이 공통 이미지를 그대로 따르게 하기 위함). */
  function glCellSnapshot(idx) {
    return {
      text:     cellTexts[idx] != null ? cellTexts[idx] : commonText,
      fontMult: cellFontSizes[idx] != null ? cellFontSizes[idx] : null,
      fontFam:  cellFontFamilies[idx] || null,
      color:    cellColors[idx] || null,
      style:    cellStyles[idx] ? Object.assign({}, cellStyles[idx]) : null,
      align:    cellAligns[idx] ? Object.assign({}, cellAligns[idx]) : null,
      contentType: cellContentTypes[idx] || null,
      contentLayout: cellContentLayouts[idx] || null,
      imgSrc:   cellImages[idx] || null,
      transform: cellImgTransforms[idx] ? Object.assign({}, cellImgTransforms[idx]) : null,
      /* [버그 수정] 표도 이 칸의 "내용"인데 스냅샷에 빠져 있었다 — 그래서
         표가 있는 칸을 원본으로 "빈 칸에 채우기"를 해도 정작 표는 옮겨지지
         않고 글자만(있다면) 옮겨졌다. glTblDeepClone으로 복사해 담아,
         나중에 원본 표를 고쳐도 이미 채워 넣은 칸에는 영향이 없게 한다. */
      table: (cellTables[idx] && cellTables[idx].rows && cellTables[idx].rows.length)
        ? glTblDeepClone(glTblNormalize(cellTables[idx])) : null,
    };
  }
  function glApplyCellSnapshot(idx, snap) {
    if (!snap) return;
    cellTexts[idx] = snap.text || "";
    if (snap.fontMult != null) cellFontSizes[idx] = snap.fontMult; else delete cellFontSizes[idx];
    if (snap.fontFam) cellFontFamilies[idx] = snap.fontFam; else delete cellFontFamilies[idx];
    if (snap.color) cellColors[idx] = snap.color; else delete cellColors[idx];
    if (snap.style) cellStyles[idx] = Object.assign({}, snap.style); else delete cellStyles[idx];
    if (snap.align) cellAligns[idx] = Object.assign({}, snap.align); else delete cellAligns[idx];
    if (snap.contentType) cellContentTypes[idx] = snap.contentType; else delete cellContentTypes[idx];
    if (snap.contentLayout) cellContentLayouts[idx] = snap.contentLayout; else delete cellContentLayouts[idx];
    if (snap.imgSrc) cellImages[idx] = snap.imgSrc; else delete cellImages[idx];
    if (snap.transform) cellImgTransforms[idx] = Object.assign({}, snap.transform); else delete cellImgTransforms[idx];
    /* [버그 수정] 스냅샷에 표가 담겨 있으면 이 칸에도 그대로 복사해 넣는다
       (원본과 독립적인 사본 — glTblDeepClone을 다시 한 번 거쳐 참조를
       공유하지 않게 한다). 원본에 표가 없었으면 이 칸의 표도 지운다. */
    if (snap.table) cellTables[idx] = glTblDeepClone(snap.table); else delete cellTables[idx];
    rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
    if (selectedIdx === idx) updateEditToolbar();
  }

  /* [신규] 칸 하나의 모든 개별 오버라이드를 지우고 공통 설정으로 되돌린다.
     우클릭 메뉴의 "이 칸 전체 초기화"와 더블클릭 편집 모달의 "이 칸 초기화"가
     이 함수 하나를 공유한다. */
  function resetCellFully(idx) {
    glPushUndo();
    delete cellTexts[idx];
    delete cellFontSizes[idx];
    delete cellImages[idx];
    delete cellImgTransforms[idx];
    delete cellFontFamilies[idx];
    delete cellColors[idx];
    delete cellStyles[idx];
    delete cellAligns[idx];
    delete cellContentTypes[idx];
    delete cellContentLayouts[idx];
    delete cellTables[idx];
    rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
    if (selectedIdx === idx) updateEditToolbar();
  }

  function openGlCtxMenu(x, y, idx) {
    const menu = els.glCtxMenu;
    if (!menu) return;
    glCtxIdx = idx;
    glCtxBatchMode = false;
    const effType = glEffContentType(idx);
    const imgModeOn = effType === "image" || effType === "both";
    const txtModeOn = effType === "text" || effType === "both";
    const hasImgHere = imgModeOn && !!(cellImages[idx] || imageDataUrl);
    const text = cellTexts[idx] != null ? cellTexts[idx] : commonText;

    const setVis = (el, show) => { if (el) el.style.display = show ? "" : "none"; };
    /* [신규] 다중 선택 일괄 작업 그룹은 평소(단일 칸) 메뉴에서는 항상 숨김 */
    setVis(els.glCtxBatchGroup, false);
    setVis(els.glCtxBatchClearText, false);
    setVis(els.glCtxBatchClearImg, false);
    setVis(els.glCtxBatchReset, false);
    const cropGroupVisible = hasImgHere || (imgModeOn && !!cellImgTransforms[idx]);
    setVis(els.glCtxGlabelCrop,  cropGroupVisible);   /* 신규 */
    setVis(els.glCtxCrop,        hasImgHere);
    setVis(els.glCtxResetImg,    imgModeOn && !!cellImgTransforms[idx]);
    setVis(els.glCtxSepA,        imgModeOn);
    setVis(els.glCtxGlabelReplace, imgModeOn);        /* 신규 */
    setVis(els.glCtxReplaceCell, imgModeOn);
    setVis(els.glCtxClearImg,    imgModeOn && !!cellImages[idx]);
    setVis(els.glCtxReplaceAll,  imgModeOn);
    setVis(els.glCtxSepB,        imgModeOn);
    setVis(els.glCtxFillAllText, txtModeOn && !!(text && text.trim())); /* [신규] */
    setVis(els.glCtxClearText,   txtModeOn && !!(text && text.trim()));
    /* [버그 수정] 이 줄이 빠져 있어서, 다중 선택 메뉴(openGlBatchCtxMenu)가
       glCtxResetCell을 display:none으로 숨긴 뒤에는 — 그 다음부터 칸을
       하나만 우클릭해도(openGlCtxMenu) "이 칸 전체 초기화" 버튼이 다시
       보이지 않았다(마지막으로 남은 display 값을 그대로 물려받으므로).
       단일 칸 메뉴에서는 이 항목이 항상 있어야 하므로 매번 명시적으로
       켜준다. */
    setVis(els.glCtxResetCell, true);
    /* [신규] 칸이 하나뿐이면 "다른 칸"이 없으므로 숨긴다. */
    const c0 = CFG[currentTplKey];
    setVis(els.glCtxFillEmptyCells, !!(c0 && c0.perPage > 1));

    if (els.glCtxPaste) {
      if (glCellClipboard) { els.glCtxPaste.removeAttribute("disabled"); els.glCtxPaste.removeAttribute("aria-disabled"); }
      else { els.glCtxPaste.setAttribute("disabled", ""); els.glCtxPaste.setAttribute("aria-disabled", "true"); }
    }
    positionGlCtxMenu(menu, x, y);
  }
  /* [신규] 다중 선택(2칸 이상) 상태에서 그 중 한 칸을 우클릭하면 여는 일괄
     작업 메뉴 — 같은 메뉴 DOM(#gl-context-menu)을 재사용하되, 평소의 단일 칸
     항목들은 모두 숨기고 "선택한 N칸 …" 항목 3개만 보여준다. */
  function openGlBatchCtxMenu(x, y) {
    const menu = els.glCtxMenu;
    if (!menu) return;
    glCtxIdx = null;
    glCtxBatchMode = true;
    const setVis = (el, show) => { if (el) el.style.display = show ? "" : "none"; };
    [els.glCtxGlabelCrop, els.glCtxCrop, els.glCtxResetImg, els.glCtxSepA,
     els.glCtxGlabelReplace, els.glCtxReplaceCell, els.glCtxClearImg, els.glCtxReplaceAll, els.glCtxSepB,
     els.glCtxGlabelCopy, els.glCtxCopy, els.glCtxPaste, els.glCtxSepC,
     els.glCtxGlabelManage, els.glCtxFillAllText, els.glCtxFillEmptyCells, els.glCtxClearText, els.glCtxResetCell]
      .forEach(el => setVis(el, false));
    setVis(els.glCtxBatchGroup, true);
    setVis(els.glCtxBatchClearText, true);
    setVis(els.glCtxBatchClearImg, true);
    setVis(els.glCtxBatchReset, true);
    if (els.glCtxBatchLabel) els.glCtxBatchLabel.textContent = "선택한 " + multiSelectedIdxs.size + "칸";
    positionGlCtxMenu(menu, x, y);
  }
  /* 우클릭 메뉴 위치 계산 — 화면 넘침 보정(주소 라벨 컨텍스트 메뉴와 동일한 로직).
     openGlCtxMenu/openGlBatchCtxMenu가 공유한다. */
  function positionGlCtxMenu(menu, x, y) {
    menu.style.display = "block";
    menu.style.left = "0px"; menu.style.top = "0px";
    menu.classList.remove("ctx-flip-x", "ctx-flip-y", "ctx-open");
    const mw = menu.offsetWidth, mh = menu.offsetHeight;
    const vw = window.innerWidth, vh = window.innerHeight;
    let lx = x, ly = y;
    const flipX = lx + mw > vw - 8, flipY = ly + mh > vh - 8;
    if (flipX) lx = Math.max(4, x - mw);
    if (flipY) ly = Math.max(4, y - mh);
    if (flipX) menu.classList.add("ctx-flip-x");
    if (flipY) menu.classList.add("ctx-flip-y");
    menu.style.left = lx + "px"; menu.style.top = ly + "px";
    requestAnimationFrame(() => menu.classList.add("ctx-open"));
  }
  function closeGlCtxMenu() {
    const menu = els.glCtxMenu;
    if (!menu || !menu.classList.contains("ctx-open")) { glCtxIdx = null; glCtxBatchMode = false; return; }
    menu.classList.remove("ctx-open");
    menu.addEventListener("transitionend", () => {
      if (!menu.classList.contains("ctx-open")) menu.style.display = "none";
    }, { once: true });
    glCtxIdx = null;
    glCtxBatchMode = false;
  }

  function onCanvasContextMenu(e) {
    const cellEl = e.target.closest(".gl-label-cell");
    if (!cellEl) return;               // 칸 바깥이면 브라우저 기본 메뉴 유지
    e.preventDefault();                // 브라우저 기본 메뉴 차단 — 주소 라벨과 동일
    const idx = Number(cellEl.dataset.idx);
    /* [신규] 지금 우클릭한 칸이 다중 선택 집합 안에 있으면(=여러 칸을 선택해둔
       채로 그 중 하나를 우클릭) 선택을 그대로 유지하고 일괄 작업 메뉴를 연다.
       선택 밖의 칸을 우클릭하면(보통의 우클릭) 다중 선택을 풀고 그 칸 하나만
       선택한 뒤 평소의 단일 칸 메뉴를 연다 — 탐색기·구글시트와 같은 관례. */
    if (multiSelectedIdxs.size >= 2 && multiSelectedIdxs.has(idx)) {
      openGlBatchCtxMenu(e.clientX, e.clientY);
      return;
    }
    if (multiSelectedIdxs.size) clearMultiSelect();
    selectCell(idx);                   // 우클릭한 칸도 선택 상태로 전환(상단 툴바·핸들 함께 사용 가능)
    openGlCtxMenu(e.clientX, e.clientY, idx);
  }

  /* ── 전체 적용 / 초기화 ───────────────────────────────────────
     변경: 텍스트뿐 아니라, 업로드만 해두고 아직 반영되지 않은 대기
     이미지(stagedImageDataUrl)가 있으면 이 버튼을 눌렀을 때 함께
     모든 칸에 채운다 — "이미지를 넣으면 자동으로 채우지 말고,
     '전체 칸에 적용'을 눌렀을 때 채워지도록" 하기 위함. */
  function fillAllWithCommonText() {
    glPushUndo();
    commonText = els.textarea.value;
    cellTexts = {};
    cellFontSizes = {};  /* 개별 칸 글자 크기 오버라이드도 함께 초기화(전체 동일 크기로) */
    /* [신규] 폰트/색상/스타일/정렬 오버라이드도 함께 초기화 — cellFontSizes와
       같은 "텍스트 서식" 범주라 여기서 같이 지워야 "전체 칸에 적용" 결과가
       실제로 전체가 똑같아 보인다(더블클릭 개별 편집으로 남겨둔 색상 등이
       남아있으면 헷갈리기 때문). */
    cellFontFamilies = {};
    cellColors = {};
    cellStyles = {};
    cellAligns = {};
    cellContentTypes = {}; /* [신규] "전체 칸에 적용"은 정말 모든 칸이 똑같아 보이게 하는 것이 목적이므로 콘텐츠 타입 오버라이드도 함께 초기화 */
    cellContentLayouts = {}; /* [신규] 텍스트+이미지 배치 오버라이드도 같은 이유로 함께 초기화 */
    cellTables = {}; /* [신규] 개별 칸의 표도 "전체 칸에 적용" 대상 — 공통 텍스트로 통일되므로 표는 지운다 */
    if (stagedImageDataUrl && stagedImageDataUrl !== imageDataUrl) {
      imageDataUrl = stagedImageDataUrl;
      cellImgTransforms = {}; /* 새 이미지로 전체 교체되면 이전 개별 위치/확대 설정은 의미가 없으므로 초기화 */
      cellImages = {};        /* 신규: 전체를 새 공통 이미지로 채우므로 칸별 개별 이미지도 함께 초기화 */
    }
    renderImageDrop();
    rebuildCanvas();
    updateEditToolbar(); /* 선택된 칸이 있었다면(개별 이미지 해제됨) 툴바 상태 갱신 */
  }
  function clearAll() {
    glPushUndo();
    commonText = "";
    cellTexts = {};
    cellFontSizes = {};
    cellImgTransforms = {};
    cellImages = {}; /* 신규 */
    cellFontFamilies = {}; /* 신규 */
    cellColors = {};       /* 신규 */
    cellStyles = {};        /* 신규 */
    cellAligns = {};         /* 신규 */
    cellContentTypes = {};   /* 신규 */
    cellContentLayouts = {}; /* 신규: 텍스트+이미지 배치 오버라이드도 함께 초기화 */
    cellTables = {};         /* [신규] 개별 칸의 표도 전체 초기화 대상 */
    els.textarea.value = "";
    imageDataUrl = null;
    stagedImageDataUrl = null;
    deselectCell(); /* 신규: 전체 초기화 시 선택도 함께 해제 */
    renderImageDrop();
    rebuildCanvas();
    /* [신규] 화면만 비우고 자동 저장된 옛 내용은 그대로 남아있으면,
       다음에 들어왔을 때(또는 다음 5초 자동 저장 전에 새로고침하면)
       "저장된 작업이 있습니다" 배너가 다시 떠서 방금 지운 걸 되살릴
       수 있는 상태가 되어 버린다 — 저장소도 함께 지워 확실하게 빈
       상태로 만든다. */
    if (typeof idbDelete === "function") idbDelete(GL_IDB_KEY).catch(() => {});
    _glLastSaveFingerprint = null;
  }

  /* ── 이미지 배치(fit) 세그먼트 ─────────────────────────────── */
  function renderFitSeg() {
    const fits = [
      { key: "contain", label: "전체보임" },
      { key: "cover",   label: "꽉채움" },
      { key: "stretch", label: "늘리기" },
    ];
    els.fitSeg.innerHTML = fits.map(f =>
      `<button class="gl-fit-btn${f.key === imageFit ? " active" : ""}" data-fit="${f.key}">${f.label}</button>`
    ).join("");
    els.fitSeg.querySelectorAll(".gl-fit-btn").forEach(btn => {
      btn.addEventListener("click", () => {
        imageFit = btn.dataset.fit;
        renderFitSeg();
        rebuildCanvas();
      });
    });
  }

  /* ── 3×3 위치 선택 그리드 — 텍스트 정렬 / 이미지 위치 공용 렌더러 ─
     grid: [top-left, top-center, top-right, mid-left, center, mid-right, bottom-left, bottom-center, bottom-right] */
  const ALIGN_H = ["left", "center", "right"];
  const ALIGN_V = ["top", "middle", "bottom"];
  const ALIGN_TITLES = {
    left: "좌", center: "가운데", right: "우",
    top: "위", middle: "중앙", bottom: "아래",
  };
  function renderAlignGrid(gridEl, curH, curV, onPick) {
    if (!gridEl) return;
    let html = "";
    ALIGN_V.forEach(v => {
      ALIGN_H.forEach(h => {
        const active = (h === curH && v === curV) ? " active" : "";
        html += `<button type="button" class="gl-align-btn${active}" data-h="${h}" data-v="${v}" ` +
          `title="${ALIGN_TITLES[v]} · ${ALIGN_TITLES[h]}" aria-label="${ALIGN_TITLES[v]} ${ALIGN_TITLES[h]} 정렬"></button>`;
      });
    });
    gridEl.innerHTML = html;
    gridEl.querySelectorAll(".gl-align-btn").forEach(btn => {
      btn.addEventListener("click", () => onPick(btn.dataset.h, btn.dataset.v));
    });
  }

  function renderTextAlignGrid() {
    renderAlignGrid(els.textAlignGrid, textAlignH, textAlignV, (h, v) => {
      textAlignH = h; textAlignV = v;
      renderTextAlignGrid();
      rebuildCanvas();
    });
  }

  /* 신규: 글자 방향(가로쓰기/세로쓰기) 세그먼트 — 기본값(가로)은 기존 동작과 동일.
     "분류번호(옆면)"처럼 칸 폭이 좁아 가로쓰기가 불가능한 규격을 위해 추가했지만,
     특정 규격에 종속되지 않은 전역 옵션이라 어떤 일반라벨 규격에서도 켤 수 있다. */
  const WRITING_OPTIONS = [
    { key: "horizontal", label: "가로", title: "가로쓰기 (기본값)" },
    { key: "vertical",   label: "세로", title: "세로쓰기 — 문서철 옆면(책등)처럼 폭이 좁은 칸에 적합" },
  ];
  function renderWritingSeg() {
    if (!els.writingSeg) return;
    els.writingSeg.innerHTML = WRITING_OPTIONS.map(o =>
      `<button type="button" class="gl-fit-btn${o.key === textWritingMode ? " active" : ""}" data-writing="${o.key}" title="${o.title}">${o.label}</button>`
    ).join("");
    els.writingSeg.querySelectorAll(".gl-fit-btn").forEach(btn => {
      btn.addEventListener("click", () => {
        /* 다른 전역 배치 설정(텍스트 정렬·이미지 배치 등)과 동일하게
           모양(스타일) 설정이므로 실행취소(Ctrl+Z) 스택에는 넣지 않는다 */
        textWritingMode = btn.dataset.writing;
        renderWritingSeg();
        rebuildCanvas();
      });
    });
  }

  function renderImgAlignGrid() {
    renderAlignGrid(els.imgAlignGrid, imageAlignH, imageAlignV, (h, v) => {
      imageAlignH = h; imageAlignV = v;
      renderImgAlignGrid();
      applyGlImageTransform();
    });
  }

  /* ── 이미지 크기/위치 적용 — transform: scale() 기반이라 DOM 재빌드 없이
     CSS 변수만 갱신하면 즉시 반영된다. scale=100·center/middle(기본값)일 땐
     transform:scale(1) + origin 50% 50% 로 기존 렌더링과 완전히 동일하다. ── */
  const ALIGN_PCT = { left: "0%", center: "50%", right: "100%", top: "0%", middle: "50%", bottom: "100%" };
  function applyGlImageTransform() {
    const root = document.documentElement.style;
    root.setProperty("--gl-img-scale", (imageScale / 100).toFixed(2));
    root.setProperty("--gl-img-origin", ALIGN_PCT[imageAlignH] + " " + ALIGN_PCT[imageAlignV]);
  }

  /* ── 텍스트+이미지 모드 — 텍스트 위치(레이아웃) 세그먼트 ───────
     텍스트를 이미지 기준 어디에 배치할지 선택한다.
     below(기본값)는 기존 동작(이미지 위·텍스트 아래)과 완전히 동일하므로
     이 기능을 사용하지 않는 사용자는 결과물이 전혀 달라지지 않는다. ── */
  const LAYOUT_OPTIONS = [
    { key: "above",     label: "위",    title: "텍스트를 이미지 위쪽에 표시" },
    { key: "below",     label: "아래",  title: "텍스트를 이미지 아래쪽에 표시 (기본값)" },
    { key: "side-left", label: "옆◀",  title: "텍스트를 이미지 왼쪽에 표시" },
    { key: "side",      label: "옆▶",  title: "텍스트를 이미지 오른쪽에 표시" },
    { key: "overlay",   label: "겹침", title: "이미지를 배경처럼 깔고 그 위에 텍스트를 겹쳐서 표시" },
  ];
  function renderLayoutSeg() {
    if (!els.layoutSeg) return;
    els.layoutSeg.innerHTML = LAYOUT_OPTIONS.map(l =>
      `<button class="gl-fit-btn${l.key === contentLayout ? " active" : ""}" data-layout="${l.key}" title="${l.title}">${l.label}</button>`
    ).join("");
    els.layoutSeg.querySelectorAll(".gl-fit-btn").forEach(btn => {
      btn.addEventListener("click", () => {
        contentLayout = btn.dataset.layout;
        renderLayoutSeg();
        rebuildCanvas();
      });
    });
  }

  /* ── 글자 크기 적용 (주소라벨과 동일하게 12px 기준 배율) ───────── */
  function applyGlFontSize() {
    document.documentElement.style.setProperty("--gl-font-size", (12 * glFontSizeMultiplier).toFixed(1) + "px");
  }

  /* [신규] 텍스트 속성 확장(전역) — 색상/굵기/기울임/밑줄/줄간격/자간을
     CSS 변수로 밀어넣는다. 칸 렌더링을 다시 만들 필요 없이(rebuildCanvas 불필요)
     모든 칸이 즉시 반영되는 이유는 general-labels.css의 .gl-cell-text 규칙이
     이 변수들을 참조하기 때문 — glFontSize와 동일한 패턴. */
  function applyGlTextFormat() {
    const root = document.documentElement.style;
    root.setProperty("--gl-text-color", textColor);
    root.setProperty("--gl-font-weight", textBold ? "700" : "400");
    root.setProperty("--gl-font-style", textItalic ? "italic" : "normal");
    root.setProperty("--gl-text-decoration", textUnderline ? "underline" : "none");
    root.setProperty("--gl-line-height", String(lineHeight));
    root.setProperty("--gl-letter-spacing", letterSpacing ? (letterSpacing + "px") : "normal");
    /* [버그 수정] "겹침 배치에서 글자색을 흰색/밝은 색으로 하면 흰 후광이
       안 먹혀서 다시 안 보인다" — 전역 글자색이 바뀔 때마다(칸별 오버라이드가
       없는 칸들에 한해) 후광 색도 함께 다시 계산해 CSS 변수로 즉시 반영한다.
       rebuildCanvas를 부르지 않아도(=입력하는 동안 매번) 바로 반영되도록,
       칸의 인라인 스타일이 아니라 여기서 CSS 변수만 갱신한다. */
    root.setProperty("--gl-halo-shadow", glHaloShadowFor(textColor));
  }

  /* ── 인쇄 ──────────────────────────────────────────────────── */
  async function printGeneral() {
    if (!currentTplKey) { alert("먼저 라벨 규격을 선택해주세요."); return; }
    const hasAnyContent = Object.values(cellTexts).some(t => t && t.trim()) || commonText.trim() || imageDataUrl || Object.keys(cellImages).length > 0;
    if (!hasAnyContent) {
      const ok = confirm("입력된 내용이 없습니다. 빈 라벨로 인쇄하시겠습니까?");
      if (!ok) return;
    }
    /* index.js의 confirmPrintSettings()를 그대로 공유 — 여백/배율
       "다시 보지 않기" 상태도 주소라벨 탭과 동일하게 유지됨 */
    if (typeof confirmPrintSettings === "function" && !await confirmPrintSettings()) return;
    window.print();
  }

  /* ── 모드 전환 ─────────────────────────────────────────────── */
  function activateGeneralMode() {
    document.body.classList.add("mode-general");
    els.tabGeneral.classList.add("active");
    els.tabAddr.classList.remove("active");
    if (!glInitialized) {
      selectTemplate(currentTplKey || Object.keys(CFG)[0]);
    } else {
      applyGlViewMode(); /* 신규: 다른 탭에 있는 동안 창 크기가 바뀌었을 수 있으므로 다시 계산 */
    }
    /* [버그 수정] 예전엔 여기서 els.body(#general-app-body, "라벨 디자인" 화면)에
       무조건 "active"를 넣어 항상 보이게 했다. 지금은 3단계 마법사가 있어서
       "라벨 디자인"은 그 중 한 단계일 뿐인데, 이 줄 때문에 "규격 선택" 단계로
       처음 들어가도 편집 화면(글꼴·글자크기·캔버스 등)이 그 아래에 함께 떠
       있었다 — 마법사가 기억하는 현재 단계(glWizardStep, 함수 선언은 아래
       있지만 호이스팅되어 여기서도 바로 쓸 수 있다)를 다시 적용해, 실제로
       보여야 할 화면 하나만 보이게 한다. */
    if (typeof setGlWizardStep === "function") setGlWizardStep(glWizardStep);
  }
  function activateAddressMode() {
    document.body.classList.remove("mode-general");
    els.body.classList.remove("active");
    els.tabAddr.classList.add("active");
    els.tabGeneral.classList.remove("active");
  }


  /* ══════════════════════════════════════════════════════════════
     주소라벨 우측 인스펙터 — 라벨 그리드 미리보기 (신규)
     index.js에서 관리하는 TEMPLATE_CONFIGS와 DOM 상태를 읽어서
     우측 패널에 칸 배치 미리보기를 렌더링한다.
     ⚠️ index.js 내부 함수/변수를 직접 호출하지 않고,
        DOM 이벤트·MutationObserver·window 속성만 참조한다.
   ══════════════════════════════════════════════════════════════ */
  function initAddressPreview() {
    const container = document.getElementById("addr-grid-preview-wrap");
    if (!container) return;  // HTML에 없으면 스킵

    /* 주소라벨 규격 select 변경 감지 */
    const tplSelect = document.getElementById("template-select");
    if (tplSelect) {
      tplSelect.addEventListener("change", () => {
        setTimeout(renderAddrGridPreview, 50);
      });
    }

    /* MutationObserver: #post-label-container 변화 감지 → 라벨 수 업데이트 */
    const labelContainer = document.getElementById("post-label-container");
    if (labelContainer) {
      const mo = new MutationObserver(() => renderAddrGridPreview());
      mo.observe(labelContainer, { childList: true, subtree: false });
    }

    /* 초기 렌더 */
    renderAddrGridPreview();
  }

  /* TEMPLATE_CONFIGS는 index.js가 window에 노출하지 않으므로,
     index.js가 :root에 설정해두는 실측 mm 단위 CSS 변수
     (--page-pt/--page-pb/--page-pl/--label-w/--label-h/--label-row-gap)를
     그대로 읽어 정확한 비율로 미리보기를 그린다. */
  function getAddrTemplateInfo() {
    const tplSelect = document.getElementById("template-select");
    if (!tplSelect) return null;
    const selectedOption = tplSelect.options[tplSelect.selectedIndex];
    if (!selectedOption) return null;

    const txt = selectedOption.textContent || "";
    const modelShort = (selectedOption.dataset && selectedOption.dataset.short) || txt.replace(/\s*\(.*/, "");

    const style = getComputedStyle(document.documentElement);
    const num = (name, fallback) => {
      const v = parseFloat(style.getPropertyValue(name));
      return Number.isFinite(v) ? v : fallback;
    };

    const gridCols = style.getPropertyValue("--grid-cols").trim();
    const gridRows = style.getPropertyValue("--grid-rows").trim();
    let colN = 2, rowN = 8;
    const mCols = gridCols.match(/repeat\((\d+)/);
    const mRows = gridRows.match(/repeat\((\d+)/);
    if (mCols) colN = parseInt(mCols[1]);
    if (mRows) rowN = parseInt(mRows[1]);

    return {
      colN, rowN,
      perPage: colN * rowN,
      labelW: num("--label-w", 99.1),
      labelH: num("--label-h", 33.9),
      pt: num("--page-pt", 10),
      pl: num("--page-pl", 5),
      colGap: num("--label-row-gap", 2),
      model: modelShort,
    };
  }

  function renderAddrGridPreview() {
    const wrap = document.getElementById("addr-grid-preview-wrap");
    const grid = document.getElementById("addr-grid-preview");
    const info = document.getElementById("addr-grid-info");
    if (!wrap || !grid) return;

    const t = getAddrTemplateInfo();
    if (!t) return;

    const { colN, rowN, perPage, labelW, labelH, pt, pl, colGap, model } = t;
    const A4_W = 210, A4_H = 297;

    /* 라벨 컨테이너에서 실제 입력 상태 읽기 */
    const labelEls = document.querySelectorAll(".post-label");
    const filledSet  = new Set();
    const selectedSet= new Set();
    const lockedSet  = new Set();
    labelEls.forEach((el, i) => {
      const pageIdx = Math.floor(i / perPage);
      const cellIdx = i % perPage;
      const key = pageIdx + "_" + cellIdx;
      if (el.classList.contains("selected")) selectedSet.add(key);
      if (el.classList.contains("locked"))   lockedSet.add(key);
      const nameEl = el.querySelector(".label-name");
      const addrEl = el.querySelector(".label-addr");
      const hasContent = (nameEl && nameEl.textContent.trim()) || (addrEl && addrEl.textContent.trim());
      if (hasContent) filledSet.add(key);
    });

    /* 총 페이지 계산 */
    const totalLabels = labelEls.length;
    const totalPages  = Math.max(1, Math.ceil(totalLabels / perPage));

    /* 현재 뷰포트에서 보이는 첫 번째 라벨 인덱스로 currentPage 추정 */
    let currentPage = 0;
    const canvasScroll = document.getElementById("canvas-scroll");
    if (canvasScroll) {
      const scrollTop = canvasScroll.scrollTop;
      const firstPage = canvasScroll.querySelector(".a4-page");
      if (firstPage) {
        const pageH = firstPage.offsetHeight;
        if (pageH > 0) currentPage = Math.floor(scrollTop / (pageH + 24));
      }
    }
    currentPage = Math.max(0, Math.min(currentPage, totalPages - 1));

    /* 그리드 렌더 — A4 비율 프레임 안에 실측 mm 비율로 절대 배치 */
    let html = "";
    for (let r = 0; r < rowN; r++) {
      for (let cn = 0; cn < colN; cn++) {
        const cellIdx = r * colN + cn;
        const globalIdx = currentPage * perPage + cellIdx;
        const key = currentPage + "_" + cellIdx;
        const isReal = globalIdx < totalLabels;
        let cls = "agp-cell";
        if (!isReal) cls += " agp-empty";
        else if (selectedSet.has(key)) cls += " agp-selected";
        else if (lockedSet.has(key))   cls += " agp-locked";
        else if (filledSet.has(key))   cls += " agp-filled";
        else cls += " agp-blank";

        const xMm = pl + cn * (labelW + colGap);
        const yMm = pt + r * labelH;
        const leftPct   = (xMm / A4_W) * 100;
        const topPct    = (yMm / A4_H) * 100;
        const widthPct  = (labelW / A4_W) * 100;
        const heightPct = (labelH / A4_H) * 100;

        html += `<div class="${cls}" title="${isReal ? (globalIdx + 1) + "번 라벨" : "빈 칸"}" ` +
          `style="left:${leftPct}%;top:${topPct}%;width:${widthPct}%;height:${heightPct}%"></div>`;
      }
    }
    grid.innerHTML = html;

    /* 정보 텍스트 */
    if (info) {
      const rem = Math.max(0, perPage - (Math.min(totalLabels - currentPage * perPage, perPage)));
      info.innerHTML =
        `<span class="agp-info-model">${escHtml(model)}</span>` +
        `<span class="agp-info-sep">·</span>` +
        `<span class="agp-info-page">${currentPage + 1} / ${totalPages} 페이지</span>` +
        `<span class="agp-info-sep">·</span>` +
        `<span class="agp-info-rem">빈칸 ${rem}개</span>`;
    }
  }

  /* ══════════════════════════════════════════════════════════════
     단축키 모달 — "단축키" / "사용법" 탭 전환
     ⚠️ 모달 자체의 열기/닫기 로직은 index.js에 이미 있으므로 건드리지 않고,
        이 함수는 탭 버튼 클릭에 따른 패널 전환만 추가로 담당한다.
     ══════════════════════════════════════════════════════════════ */
  function initShortcutTabs() {
    const modal = document.getElementById("shortcut-modal");
    if (!modal) return;
    const tabs   = modal.querySelectorAll(".shortcut-tab");
    const panels = modal.querySelectorAll(".shortcut-tab-panel");
    const titleEl = document.getElementById("shortcut-modal-title");
    const descEl  = document.getElementById("shortcut-modal-desc");
    const TAB_META = {
      keys:  { title: "⌨️ 단축키 안내", desc: "자주 쓰는 동작을 키보드로 빠르게" },
      guide: { title: "📖 사용법",       desc: "주소라벨 · 일반라벨 기본 사용 흐름" },
    };
    tabs.forEach(tab => {
      tab.addEventListener("click", () => {
        const key = tab.dataset.tab;
        tabs.forEach(t => t.classList.toggle("active", t === tab));
        panels.forEach(p => p.classList.toggle("active", p.dataset.panel === key));
        if (titleEl && TAB_META[key]) titleEl.textContent = TAB_META[key].title;
        if (descEl  && TAB_META[key]) descEl.textContent  = TAB_META[key].desc;
      });
    });
  }

  /* ── 초기화 ────────────────────────────────────────────────── */
  function init() {
    cacheEls();
    if (!els.body || !els.tabGeneral) return;

    els.tabAddr.addEventListener("click", () => {
      activateAddressMode();
      setTimeout(renderAddrGridPreview, 80);
    });
    els.tabGeneral.addEventListener("click", activateGeneralMode);

    renderTemplateSelect();
    renderContentSeg();
    renderFitSeg();
    renderLayoutSeg();
    renderImageDrop();
    renderTextAlignGrid();
    renderWritingSeg();
    renderImgAlignGrid();
    applyGlImageTransform();

    /* 신규: 이미지 사용법 접이식 토글 */
    if (els.imageHintToggle && els.imageHintBody) {
      els.imageHintToggle.addEventListener("click", () => {
        const expanded = els.imageHintToggle.getAttribute("aria-expanded") === "true";
        els.imageHintToggle.setAttribute("aria-expanded", String(!expanded));
        els.imageHintBody.style.display = expanded ? "none" : "block";
      });
    }

    els.templateSelect.addEventListener("change", () => {
      selectTemplate(els.templateSelect.value);
    });

    els.fontSelect.addEventListener("change", () => {
      const opt = els.fontSelect.options[els.fontSelect.selectedIndex];
      if (els.fontDisplay) els.fontDisplay.textContent = (opt && (opt.dataset.short || opt.textContent)) || "";
      document.documentElement.style.setProperty("--gl-label-font", els.fontSelect.value);
      rebuildCanvas();
    });
    document.documentElement.style.setProperty("--gl-label-font", els.fontSelect.value);

    els.fontSizeSelect.addEventListener("change", () => {
      glFontSizeMultiplier = parseFloat(els.fontSizeSelect.value);
      applyGlFontSize();
    });
    function stepGlFontSize(dir) {
      const opts = Array.from(els.fontSizeSelect.options);
      const idx  = opts.findIndex(o => parseFloat(o.value) === glFontSizeMultiplier);
      const next = idx + dir;
      if (next < 0 || next >= opts.length) return;
      els.fontSizeSelect.selectedIndex = next;
      glFontSizeMultiplier = parseFloat(opts[next].value);
      applyGlFontSize();
    }
    els.fontSizeDown.addEventListener("click", () => stepGlFontSize(-1));
    els.fontSizeUp.addEventListener("click", () => stepGlFontSize(+1));
    applyGlFontSize();

    /* [신규] 텍스트 속성 확장(전역) — 색상/스타일/줄간격/자간.
       모두 즉시(입력하는 대로) 반영되며, rebuildCanvas 없이 CSS 변수만 갱신한다. */
    if (els.textColorInput) {
      els.textColorInput.value = textColor;
      els.textColorInput.addEventListener("input", () => {
        textColor = els.textColorInput.value;
        applyGlTextFormat();
      });
    }
    if (els.textColorReset) {
      els.textColorReset.addEventListener("click", () => {
        textColor = DEFAULT_GL_TEXT_COLOR;
        if (els.textColorInput) els.textColorInput.value = textColor;
        applyGlTextFormat();
      });
    }
    function wireGlStyleToggle(btn, apply) {
      if (!btn) return;
      btn.addEventListener("click", () => {
        const next = btn.getAttribute("aria-pressed") !== "true";
        btn.setAttribute("aria-pressed", next ? "true" : "false");
        apply(next);
        applyGlTextFormat();
      });
    }
    wireGlStyleToggle(els.boldBtn,      v => { textBold = v; });
    wireGlStyleToggle(els.italicBtn,    v => { textItalic = v; });
    wireGlStyleToggle(els.underlineBtn, v => { textUnderline = v; });
    if (els.lineHeightSelect) {
      els.lineHeightSelect.addEventListener("change", () => {
        lineHeight = parseFloat(els.lineHeightSelect.value);
        applyGlTextFormat();
      });
    }
    if (els.letterSpacingSelect) {
      els.letterSpacingSelect.addEventListener("change", () => {
        letterSpacing = parseFloat(els.letterSpacingSelect.value);
        applyGlTextFormat();
      });
    }
    applyGlTextFormat();
    initGlCellFocusPanel();

    /* [버그 수정: "글자 입력하고 나서 Ctrl+Z가 안 먹어요"] 공통 텍스트란은
       "input"마다 commonText만 바로 갱신할 뿐 되돌리기 체크포인트를 전혀
       남기지 않아서, 여기 타이핑한 내용은 Ctrl+Z를 눌러도 되돌릴 스냅샷
       자체가 없었다. 이미지 드래그가 "드래그 시작 직전 상태를 한 번만
       저장"하는 것과 같은 원칙으로, 이 칸에 포커스가 들어오는(타이핑을
       시작하려는) 순간 딱 한 번 지금 상태를 저장해 둔다 — 그래야 이후
       몇 글자를 치든 Ctrl+Z 한 번으로 "이 칸을 만지기 전"으로 돌아간다. */
    els.textarea.addEventListener("focus", () => { glPushUndo(); });
    els.textarea.addEventListener("input", () => {
      commonText = els.textarea.value;
      rebuildCanvas();
    });
    els.imageInput.addEventListener("change", (e) => handleImageFile(e.target.files[0]));
    if (els.bulkImageBtn && els.bulkImageInput) {
      els.bulkImageBtn.addEventListener("click", () => els.bulkImageInput.click());
      els.bulkImageInput.addEventListener("change", (e) => {
        handleBulkImageFiles(e.target.files);
        e.target.value = ""; /* 같은 파일들을 다시 골라도 change가 또 발생하도록 초기화 */
      });
    }
    if (els.importDataBtn && els.importDataInput) {
      els.importDataBtn.addEventListener("click", () => els.importDataInput.click());
      els.importDataInput.addEventListener("change", (e) => {
        const file = e.target.files[0];
        e.target.value = ""; /* 같은 파일을 다시 골라도 change가 또 발생하도록 초기화 */
        if (file) glHandleImportFile(file);
      });
    }
    els.imageDrop.addEventListener("click", () => { forceImmediateApply = false; els.imageInput.click(); });
    els.imageDrop.addEventListener("dragover", (e) => { e.preventDefault(); els.imageDrop.classList.add("drag-over"); });
    els.imageDrop.addEventListener("dragleave", () => els.imageDrop.classList.remove("drag-over"));
    els.imageDrop.addEventListener("drop", (e) => {
      e.preventDefault();
      els.imageDrop.classList.remove("drag-over");
      forceImmediateApply = false;
      handleImageFile(e.dataTransfer.files && e.dataTransfer.files[0]);
    });

    /* 신규: 이미지 크기(%) 조절 — select 변경 + −/＋ 스텝 버튼 */
    if (els.imgScaleSelect) {
      els.imgScaleSelect.addEventListener("change", () => {
        imageScale = parseFloat(els.imgScaleSelect.value);
        applyGlImageTransform();
      });
    }
    function stepGlImageScale(dir) {
      if (!els.imgScaleSelect) return;
      const opts = Array.from(els.imgScaleSelect.options);
      const idx  = opts.findIndex(o => parseFloat(o.value) === imageScale);
      const next = idx + dir;
      if (next < 0 || next >= opts.length) return;
      els.imgScaleSelect.selectedIndex = next;
      imageScale = parseFloat(opts[next].value);
      applyGlImageTransform();
    }
    if (els.imgScaleDown) els.imgScaleDown.addEventListener("click", () => stepGlImageScale(-1));
    if (els.imgScaleUp)   els.imgScaleUp.addEventListener("click", () => stepGlImageScale(+1));

    els.addAllBtn.addEventListener("click", fillAllWithCommonText);
    els.clearBtn.addEventListener("click", clearAll);
    els.printBtn.addEventListener("click", printGeneral);

    /* 신규: 캔버스 보기 모드(한 페이지 / 스크롤) 전환 버튼 */
    if (els.viewModeSeg) {
      els.viewModeSeg.querySelectorAll(".gl-view-btn").forEach(btn => {
        btn.addEventListener("click", () => setGlViewMode(btn.dataset.mode));
      });
    }
    /* 신규: 캔버스 크기조절(확대/축소) 버튼 */
    if (els.glZoomOut) els.glZoomOut.addEventListener("click", () => stepGlZoom(-.1));
    if (els.glZoomIn)  els.glZoomIn.addEventListener("click", () => stepGlZoom(+.1));
    /* [신규] 눈에 보이는 되돌리기/다시실행 버튼 — 하단 상태표시줄의 글자
       배지(↩ 실행취소 N단계)는 있지만 한눈에 띄는 아이콘 버튼이 캔버스
       근처엔 없었다. 되돌리기는 실수 복구에 있어 가장 자주 찾는 동작이라
       상단(포토샵·구글독스 등과 같은 위치)에도 아이콘 버튼을 둔다. 스택이
       비어 있으면 흐리게 비활성화해 지금 눌러봤자 소용없다는 걸 미리
       알려준다(상태표시줄과 같은 판단 기준 함수를 그대로 재사용). */
    function syncGlUndoRedoButtons() {
      if (els.glUndoBtn) els.glUndoBtn.disabled = glUndoStack.length === 0;
      if (els.glRedoBtn) els.glRedoBtn.disabled = glRedoStack.length === 0;
    }
    if (els.glUndoBtn) els.glUndoBtn.addEventListener("click", () => { glUndo(); syncGlUndoRedoButtons(); });
    /* [신규] 왼쪽 사이드바 접기 — 주소라벨의 sidebarToggleBtn과 동일한 방식.
       index.js에 이미 있는 syncPanelToggleA11y(전역 함수)를 그대로 재사용해
       버튼의 title/aria-label/aria-expanded까지 같은 방식으로 맞춘다. */
    if (els.glSidebarToggleBtn) {
      els.glSidebarToggleBtn.addEventListener("click", () => {
        const panel = document.getElementById("gl-sidebar");
        if (!panel) return;
        panel.classList.toggle("collapsed");
        if (typeof syncPanelToggleA11y === "function") syncPanelToggleA11y(els.glSidebarToggleBtn, panel, "사이드바");
      });
    }
    /* [신규] 오른쪽 인스펙터 접기 — 위 사이드바 접기와 완전히 같은 방식. */
    if (els.glInspectorToggleBtn) {
      els.glInspectorToggleBtn.addEventListener("click", () => {
        const panel = document.getElementById("gl-inspector");
        if (!panel) return;
        panel.classList.toggle("collapsed");
        if (typeof syncPanelToggleA11y === "function") syncPanelToggleA11y(els.glInspectorToggleBtn, panel, "인스펙터");
      });
    }
    if (els.glRedoBtn) els.glRedoBtn.addEventListener("click", () => { glRedo(); syncGlUndoRedoButtons(); });
    syncGlUndoRedoButtons();
    setInterval(() => { if (document.body.classList.contains("mode-general")) syncGlUndoRedoButtons(); }, 1000);
    /* 신규: 창 크기가 바뀌면 "한 페이지" 모드의 축소 배율을 다시 계산한다.
       다른 탭(주소라벨)이 보이는 중에는 계산할 필요가 없으므로 mode-general일 때만 동작. */
    let glResizeRaf = null;
    window.addEventListener("resize", () => {
      if (glViewMode !== "fit" || !document.body.classList.contains("mode-general")) return;
      if (glResizeRaf) cancelAnimationFrame(glResizeRaf);
      glResizeRaf = requestAnimationFrame(() => { glResizeRaf = null; applyGlViewMode(); });
    });

    /* 신규: 상단 편집 툴바 — 선택된 칸 하나만 편집 */
    /* [신규] "이 칸" 글꼴·크기 select의 옵션 목록은 사이드바 select와
       완전히 같아야 하므로(개별 편집 패널과 동일한 이유) 직접 나열하지
       않고 그대로 복제한다. */
    if (els.editFontSelect && els.fontSelect) els.editFontSelect.innerHTML = els.fontSelect.innerHTML;
    if (els.editFontSizeSelect && els.fontSizeSelect) els.editFontSizeSelect.innerHTML = els.fontSizeSelect.innerHTML;
    /* 이 칸 서식 오버라이드를 적용하고, 저장하지 않고도 캔버스 전체를
       다시 그리지 않은 채 이 칸 하나만(그리고 열려 있다면 칸 개별 편집
       패널의 확대 미리보기도) 즉시 갱신한다 — 이미지 회전/반전 버튼들과
       동일한 "선택 즉시 반영" 방식. */
    function applyToolbarTextFormat(mutator) {
      const idx = selectedIdx;
      if (idx == null) return;
      mutator(idx);
      const cellEl = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
      rebuildCellImgFromState(cellEl, idx);
      if (glCellEditIdx === idx && typeof window._glRefreshCellFocusPreview === "function") window._glRefreshCellFocusPreview();
      updateEditToolbar();
    }
    if (els.editFontSelect) {
      els.editFontSelect.addEventListener("change", () => {
        glPushUndo();
        applyToolbarTextFormat(idx => { cellFontFamilies[idx] = els.editFontSelect.value; });
      });
    }
    if (els.editFontSizeSelect) {
      els.editFontSizeSelect.addEventListener("change", () => {
        glPushUndo();
        applyToolbarTextFormat(idx => { cellFontSizes[idx] = parseFloat(els.editFontSizeSelect.value); });
      });
    }
    if (els.editColorInput) {
      /* 색상 picker는 드래그하는 동안 input이 계속 발생하므로, 다른 슬라이더성
         입력들과 같은 패턴으로 "이 조작 한 번"에 되돌리기를 1번만 쌓는다. */
      let editColorUndoPushed = false;
      els.editColorInput.addEventListener("input", () => {
        if (!editColorUndoPushed) { glPushUndo(); editColorUndoPushed = true; }
        applyToolbarTextFormat(idx => { cellColors[idx] = els.editColorInput.value; });
      });
      els.editColorInput.addEventListener("change", () => { editColorUndoPushed = false; });
    }
    [["editBoldBtn", "bold"], ["editItalicBtn", "italic"], ["editUnderlineBtn", "underline"]].forEach(([key, styKey]) => {
      const btn = els[key];
      if (!btn) return;
      btn.addEventListener("click", () => {
        glPushUndo();
        applyToolbarTextFormat(idx => {
          const cur = cellStyles[idx] || { bold: textBold, italic: textItalic, underline: textUnderline };
          cellStyles[idx] = Object.assign({}, cur, { [styKey]: !cur[styKey] });
        });
      });
    });
    if (els.editTextResetBtn) {
      els.editTextResetBtn.addEventListener("click", () => {
        const idx = selectedIdx;
        if (idx == null) return;
        if (!(cellFontFamilies[idx] || cellColors[idx] || cellStyles[idx])) return;
        glPushUndo();
        applyToolbarTextFormat(i => { delete cellFontFamilies[i]; delete cellColors[i]; delete cellStyles[i]; });
      });
    }
    if (els.editImgReplaceBtn) {
      els.editImgReplaceBtn.addEventListener("click", () => {
        if (selectedIdx == null || !els.cellImageInput || els.editImgReplaceBtn.disabled) return;
        els.cellImageInput.click();
      });
    }
    /* 신규: 이 칸 이미지 자르기 — 크롭 모달을 열어 이 칸에만 잘라낸 이미지를 적용 */
    if (els.editImgCropBtn) {
      els.editImgCropBtn.addEventListener("click", () => {
        if (selectedIdx == null) return;
        openCropModal(selectedIdx);
      });
    }
    /* 신규: 이 칸 이미지 90도 회전 — 온캔버스 미니 컨트롤(↻)과 동일한 rotateCellImg 재사용 */
    if (els.editImgRotateBtn) {
      els.editImgRotateBtn.addEventListener("click", () => {
        if (selectedIdx == null) return;
        rotateCellImg(selectedIdx, 90);
      });
    }
    /* ★FLIP: 이 칸 이미지 좌우/상하 반전 — 온캔버스 미니 컨트롤(◫/⬍)과 동일한 flipCellImg 재사용 */
    if (els.editImgFlipXBtn) {
      els.editImgFlipXBtn.addEventListener("click", () => {
        if (selectedIdx == null) return;
        flipCellImg(selectedIdx, "x");
      });
    }
    if (els.editImgFlipYBtn) {
      els.editImgFlipYBtn.addEventListener("click", () => {
        if (selectedIdx == null) return;
        flipCellImg(selectedIdx, "y");
      });
    }
    /* 신규: 이 칸의 위치·크기·자르기 개별 설정만 초기화(개별 이미지 자체는 유지) */
    if (els.editImgResetBtn) {
      els.editImgResetBtn.addEventListener("click", () => {
        if (selectedIdx == null) return;
        if (cellImgTransforms[selectedIdx]) glPushUndo();
        delete cellImgTransforms[selectedIdx];
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + selectedIdx + '"]'), selectedIdx);
        updateEditToolbar();
      });
    }
  /* ── 칸별 개별 이미지 적용(파일 선택 · 드래그앤드롭 공용) ─────────────
     "이 칸 이미지 교체" 파일 선택과, 라벨 칸에 이미지 파일을 직접 끌어다
     놓는 드래그앤드롭 모두 이 함수를 거친다 — 검증·최적화·되돌리기 스냅샷·
     재렌더링 로직을 한곳에서 관리한다. */
  function applyImageFileToCell(file, idx) {
    if (!file || idx == null) return;
    if (!SUPPORTED_IMG_EXT.test(file.name) && !/^image\//.test(file.type)) {
      alert("PNG, JPG, JPEG, WEBP, SVG 형식만 지원합니다.");
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      optimizeImageDataUrl(file, reader.result).then((optimized) => {
        warnIfLargeImage(file);
        glPushUndo();
        cellImages[idx] = optimized;
        delete cellImgTransforms[idx]; /* 새 이미지로 바뀌면 이전 위치·확대·자르기 설정은 의미가 없으므로 초기화 */
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        updateEditToolbar();
        refreshCellFocusPreview(); /* [신규] 칸 포커스 패널이 이 칸을 보고 있다면 확대 미리보기도 갱신 */
      });
    };
    reader.readAsDataURL(file);
  }

  /* [신규] "칸 포커스" 패널 안의 버튼 눌림 상태 헬퍼 — 패널 열기/미리보기
     갱신/저장 여러 곳에서 공통으로 쓰여 최상위 함수로 뺐다. */
  function isGlBtnPressed(btn) { return !!btn && btn.getAttribute("aria-pressed") === "true"; }
  function setGlBtnPressed(btn, on) { if (btn) btn.setAttribute("aria-pressed", on ? "true" : "false"); }
  /* [버그 수정] updateEditToolbar()(init() 밖의 최상위 함수)에서 이 이름을
     직접 부르면 ReferenceError가 나 "더블클릭 개별편집 패널이 안 열림"
     등 여러 증상으로 이어졌다. window._glRefreshCellFocusPreview와 같은
     방식으로 노출한다. */
  window._glSetGlBtnPressed = setGlBtnPressed;
  /* select.value = "1"은 option value="1.0"과 문자열이 달라 매칭되지
     않는다 — 숫자로 비교해 정확한 옵션을 찾아 선택한다. */
  function setSelectByFloatValue(selectEl, val) {
    if (!selectEl) return;
    const opts = Array.from(selectEl.options);
    const idx = opts.findIndex(o => parseFloat(o.value) === val);
    if (idx >= 0) selectEl.selectedIndex = idx;
  }
  /* [버그 수정] "저장된 작업 이어하기"를 눌러도 라벨 디자인 화면으로 안 넘어가던
     문제 — 이 함수는 init() 지역 함수라, init() 밖(top-level)에 있는
     glApplySavedSessionSnapshot에서는 이름이 안 보여 호출하는 순간
     ReferenceError가 났다. 그 오류 때문에 뒤에 있던 setGlWizardStep("design")
     줄까지 실행되지 못해 "규격 선택" 화면에 그대로 머물러 있었다.
     window._glRefreshCellFocusPreview와 동일한 방식으로 노출한다. */
  window._glSetSelectByFloatValue = setSelectByFloatValue;

  /* [신규] "칸 포커스" 패널 왼쪽 무대에 그 칸의 확대 미리보기를 그린다.
     아직 저장 전인 폼 입력값(글꼴·크기·색상·스타일·정렬)을 buildCell의
     previewOverrides로 넘겨 실시간으로 반영하고(실제 상태는 안 건드림),
     이미지는 이미 즉시 적용되는 상태(cellImages)를 그대로 읽는다.
     실제 그리드 칸의 렌더링된 픽셀 크기를 그대로 가져와 배율만 계산해
     키우므로, 라벨 규격이 달라도(초소형 라벨이든 대형 라벨이든) 항상
     정확한 비율로 보인다. */
  function refreshCellFocusPreview() {
    const idx = glCellEditIdx;
    if (idx == null || !els.cellFocusPreview) return;
    const showText = glEffContentType(idx) === "text" || glEffContentType(idx) === "both";
    const overrides = showText ? {
      text:       els.cellEditText ? els.cellEditText.value : undefined,
      table:      cellTables[idx], /* [신규] 표가 있으면 미리보기에도 표를 그린다 */
      fontFamily: els.cellEditFont ? els.cellEditFont.value : undefined,
      fontSize:   els.cellEditFontSize ? parseFloat(els.cellEditFontSize.value) : undefined,
      color:      els.cellEditColor ? els.cellEditColor.value : undefined,
      style: {
        bold:      isGlBtnPressed(els.cellEditBold),
        italic:    isGlBtnPressed(els.cellEditItalic),
        underline: isGlBtnPressed(els.cellEditUnderline),
      },
      align: glCellEditPendingAlign,
    } : {};
    const clone = buildCell(idx, overrides);
    clone.classList.add("gl-cell-focus-clone");

    /* 그리드 밖에 홀로 놓이면 크기 기준(그리드 트랙 사이징)이 사라지므로
       실제 칸의 렌더링된 픽셀 크기를 그대로 옮겨와 고정폭/높이로 지정 */
    const realCell = document.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
    const w = (realCell && realCell.offsetWidth) || 120;
    const h = (realCell && realCell.offsetHeight) || 80;
    clone.style.width = w + "px";
    clone.style.height = h + "px";
    clone.style.flexShrink = "0";

    els.cellFocusPreview.innerHTML = "";
    els.cellFocusPreview.appendChild(clone);

    const stage = els.cellFocusStage;
    if (stage) {
      /* [버그 수정] 예전엔 스테이지 전체 폭에서 레일 폭을 손으로 빼서
         계산했는데, 이제 .gl-cell-focus-preview 자체가 CSS에서 레일을
         뺀 "나머지 공간"만 차지하는 flex:1 박스가 됐으므로(레일은 더
         이상 라벨과 한 덩어리로 가운데 정렬되지 않고 스테이지 왼쪽에
         고정) 그 박스의 실제 렌더링 폭을 그대로 기준 폭으로 쓰면 된다.
         칸이 아무리 커져도 이 박스 폭을 넘지 않으므로 레일과 절대
         겹치지 않는다. */
      const previewBoxW = (els.cellFocusPreview && els.cellFocusPreview.clientWidth) || stage.clientWidth - 64;
      const availW = Math.max(40, previewBoxW - 24);
      const availH = Math.max(40, stage.clientHeight - 64);
      const scale = Math.max(0.2, Math.min(availW / w, availH / h, 8));
      clone.style.transform = "scale(" + scale.toFixed(3) + ")";
      /* [수정] 확대 배율이 클수록 9점 핸들·회전 손잡이도 그 배율만큼
         똑같이 커져 보여(부모 transform:scale이 자식 크기까지 함께
         키우므로) "핸들이 너무 크다"는 문제가 있었다. 배율을 CSS
         변수로 내려주고, 핸들 쪽 CSS에서 반대로 나눠(1/배율) 화면상
         실제 크기가 배율과 무관하게 항상 일정하도록 상쇄한다. */
      clone.style.setProperty("--gl-focus-scale", scale.toFixed(3));
    }

    /* [신규] 이미지 교체/삭제 등으로 이 칸의 이미지 유무가 바뀌었을 수 있으니
       보정 슬라이더 표시 여부·값도 매번 같이 맞춰준다. */
    const effType = glEffContentType(idx);
    const hasImgNow = (effType === "image" || effType === "both") && !!(imageDataUrl || cellImages[idx]);
    if (els.cellEditImgFilterField) els.cellEditImgFilterField.style.display = hasImgNow ? "" : "none";
    const filtNow = (cellImgTransforms[idx] && cellImgTransforms[idx].filter) || {};
    setCellFilterSliderUI(els.cellEditImgBrightness, "gl-cell-edit-img-brightness-val", filtNow.brightness != null ? filtNow.brightness : 100, "%");
    setCellFilterSliderUI(els.cellEditImgContrast,   "gl-cell-edit-img-contrast-val",   filtNow.contrast   != null ? filtNow.contrast   : 100, "%");
    setCellFilterSliderUI(els.cellEditImgSaturate,   "gl-cell-edit-img-saturate-val",   filtNow.saturate   != null ? filtNow.saturate   : 100, "%");
    setCellFilterSliderUI(els.cellEditImgGrayscale,  "gl-cell-edit-img-grayscale-val",  filtNow.grayscale  != null ? filtNow.grayscale  : 0,   "%");
    setCellFilterSliderUI(els.cellEditImgOpacity,    "gl-cell-edit-img-opacity-val",    filtNow.opacity    != null ? filtNow.opacity    : 100, "%");
    setCellFilterSliderUI(els.cellEditImgInvert,     "gl-cell-edit-img-invert-val",     filtNow.invert     != null ? filtNow.invert     : 0,   "%");
  }

  /* ═══════════════════════════════════════════════════════════════
     [신규] 칸(라벨) 더블클릭 → 캔버스 안 "칸 포커스" 패널
     화면을 덮는 팝업이 아니라 STEP2 캔버스 영역(.gl-canvas-wrap) 안에
     그 칸만 확대해 보여주고, 옆 패널에서 편집한다. 텍스트/서식은
     "완료"를 눌러야 그 칸에 실제로 저장되고(그 전엔 미리보기만 갱신),
     이미지 교체/삭제는 기존 칸별 이미지 로직 그대로 즉시 적용된다.
     ═══════════════════════════════════════════════════════════════ */
  /* 열 너비 드래그 조절 — boundaryCol과 boundaryCol+1의 폭만 서로 주고받는다.
     드래그 시작 시점에 되돌리기 스냅샷 + 깊은 복사(다른 구조 조작들과 같은
     패턴)를 한 번만 하고, 이후 mousemove는 그 새 객체만 건드린다. */
  function startColResize(ev, idx, boundaryCol, tableEl) {
    ev.preventDefault();
    ev.stopPropagation(); /* 손잡이가 칸 안에 있으므로 textarea의 mousedown(선택 로직)까지 번지지 않게 */
    const tbl = cellTables[idx];
    if (!tbl) return;
    const startX = ev.clientX;
    const tableWidth = tableEl.getBoundingClientRect().width || 1;
    const w0 = tbl.colWidths[boundaryCol], w1 = tbl.colWidths[boundaryCol + 1];
    if (w0 == null || w1 == null) return;
    let moved = false;
    glPushUndo();
    cellTables[idx] = glTblDeepClone(tbl);
    const freshTbl = cellTables[idx];
    function onMove(e) {
      const deltaPct = ((e.clientX - startX) / tableWidth) * 100;
      const minW = 8;
      let nw0 = w0 + deltaPct, nw1 = w1 - deltaPct;
      if (nw0 < minW) { nw1 -= (minW - nw0); nw0 = minW; }
      if (nw1 < minW) { nw0 -= (minW - nw1); nw1 = minW; }
      if (nw0 < minW || nw1 < minW) return;
      moved = true;
      freshTbl.colWidths[boundaryCol] = +nw0.toFixed(2);
      freshTbl.colWidths[boundaryCol + 1] = +nw1.toFixed(2);
      tableEl.querySelectorAll("col").forEach((col, i) => { col.style.width = freshTbl.colWidths[i] + "%"; });
    }
    function onUp() {
      document.removeEventListener("mousemove", onMove);
      document.removeEventListener("mouseup", onUp);
      if (moved) {
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        refreshCellFocusPreview();
      }
    }
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  }
  /* [신규] 행 높이 드래그 조절 — 열 너비 조절과 대칭이지만, 열 너비는
     "전체 100%를 나눠 가지는 비율"이라 이웃 열과 폭을 주고받아야 했던
     것과 달리, 행 높이는 각 행이 서로 독립적인 값(px, 또는 자동)이라
     이 행 하나만 바꾸면 된다(다른 행에서 빼앗아 오지 않음). */
  function startRowResize(ev, idx, boundaryRow, tableEl) {
    ev.preventDefault();
    ev.stopPropagation();
    const tbl = cellTables[idx];
    if (!tbl) return;
    const startY = ev.clientY;
    const trEls = tableEl.querySelectorAll("tbody > tr");
    const rowEl = trEls[boundaryRow];
    if (!rowEl) return;
    const startH = rowEl.getBoundingClientRect().height;
    /* [버그 수정] "표의 범위는 라벨을 벗어나면 안 돼" 피드백 — 행 높이는
       그동안 위쪽 한계(minH)만 있고 아래쪽(얼마나 커질 수 있는지) 한계가
       없었다. .gl-label-cell 자체가 overflow:hidden이라 인쇄 결과가
       "밖으로 삐져나오지"는 않지만, 대신 표 내용이 아무 경고 없이
       조용히 잘려 사라지는 문제가 있었다(이미지 확대 때 밖으로 삐져
       나오던 것과 본질적으로 같은 문제). 이 칸(라벨 셀)의 실제 안쪽
       높이에서, 지금 늘리는 행을 뺀 "다른 행들의 높이 합"을 빼면 이
       행이 최대로 커질 수 있는 높이가 나온다 — 그 이상은 드래그해도
       더 커지지 않게 막아, 표가 칸 안에 항상 온전히 들어가게 한다. */
    let maxH = Infinity;
    const cellEl = tableEl.closest(".gl-label-cell");
    if (cellEl) {
      const cs = getComputedStyle(cellEl);
      const padV = (parseFloat(cs.paddingTop) || 0) + (parseFloat(cs.paddingBottom) || 0);
      const cellInnerH = cellEl.getBoundingClientRect().height - padV;
      let otherRowsH = 0;
      trEls.forEach((tr, i) => { if (i !== boundaryRow) otherRowsH += tr.getBoundingClientRect().height; });
      /* 표와 같은 칸 안에 다른 내용(예: "텍스트+이미지"의 이미지 영역)이
         함께 있을 수 있으므로, 표를 감싼 문단(.gl-cell-text)이 아니라
         칸 전체 높이에서 "이 표를 뺀 나머지가 차지하는 몫"까지 감안해야
         더 정확하지만, 계산이 지나치게 복잡해지는 것을 피하려고 여기선
         "표 자체가 칸 높이를 넘지 않는 선"까지만 안전하게 제한한다
         (표 하나가 칸 전체를 쓰는 "텍스트" 모드에서는 이걸로 충분하고,
         "텍스트+이미지"에서는 어차피 표 영역 자체도 칸의 일부 폭/높이로
         이미 나뉘어 있어 실제 여유는 이보다 더 타이트하게 자연히 제한된다). */
      maxH = Math.max(startH, cellInnerH - otherRowsH - 4);
    }
    let moved = false;
    glPushUndo();
    cellTables[idx] = glTblDeepClone(tbl);
    const freshTbl = cellTables[idx];
    function onMove(e) {
      const deltaPx = e.clientY - startY;
      const minH = 16;
      const newH = Math.min(maxH, Math.max(minH, startH + deltaPx));
      moved = true;
      freshTbl.rowHeights[boundaryRow] = Math.round(newH);
      if (trEls[boundaryRow]) trEls[boundaryRow].style.height = newH + "px";
    }
    function onUp() {
      document.removeEventListener("mousemove", onMove);
      document.removeEventListener("mouseup", onUp);
      if (moved) {
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        refreshCellFocusPreview();
      }
    }
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  }
  /* [버그 수정] "셀 선택해서 늘리고 줄이고를 하는데 인식이 안 된다"는
     피드백 — 예전엔 각 <td> 안에 6px짜리 리사이저를 심어뒀는데, 칸
     경계에 걸쳐 있다 보니 옆 칸에 가려지거나 손잡이가 너무 얇아 마우스로
     정확히 잡기 어려웠다. 이제는 표 전체를 감싸는 컨테이너 위에 "겹쳐서"
     하나의 오버레이 레이어를 올리고, 실제 렌더링된 <col>/<tr> 픽셀
     위치를 계산해 그 경계마다 넉넉한 손잡이(10px)를 배치한다 — 항상
     맨 위(pointer-events는 손잡이에만)라 다른 요소에 가려질 일이 없고,
     칸 안 어디를 클릭하든 "선택"과 "크기 조절"이 서로 헷갈리지 않는다.
     [신규] 이 함수 하나로 "칸 개별 편집" 패널의 표 그리드뿐 아니라
     캔버스에 실제로 그려진 표(메인 격자·칸 포커스 확대뷰)에도 똑같이
     크기 조절 손잡이를 붙일 수 있어 "표를 캔버스에서 직접 늘리고
     줄이고"도 이 함수 하나로 함께 해결한다.
     containerEl: position:relative인, tableEl을 직접 담고 있는 상자
     (다를 경우 오버레이 위치 계산이 어긋난다 — 반드시 tableEl의
     실제 컨테이닝 블록을 넘겨야 한다). */
  function attachTableResizeOverlay(containerEl, tableEl, idx) {
    if (!containerEl || !tableEl) return;
    containerEl.querySelectorAll(":scope > .gl-tbl-resize-overlay").forEach(el => el.remove());
    const cols = tableEl.querySelectorAll("col");
    const trs = tableEl.querySelectorAll("tbody > tr");
    /* [버그 방지] 1×1 표는 열/행 "경계" 손잡이는 없지만(나눌 경계 자체가
       없으므로), 표 전체 크기 조절 손잡이(모서리)는 여전히 필요하므로
       여기서 함수를 통째로 포기하지 않는다 — 아래 경계용 for문들이
       0번 반복되고 모서리 손잡이만 붙는 것으로 자연히 처리된다. */
    const overlay = document.createElement("div");
    overlay.className = "gl-tbl-resize-overlay";
    const containerRect = containerEl.getBoundingClientRect();
    const tableRect = tableEl.getBoundingClientRect();
    const top = tableRect.top - containerRect.top + containerEl.scrollTop;
    const left = tableRect.left - containerRect.left + containerEl.scrollLeft;
    for (let i = 0; i < cols.length - 1; i++) {
      const r = cols[i].getBoundingClientRect();
      const x = left + (r.right - tableRect.left);
      const handle = document.createElement("div");
      handle.className = "gl-tbl-resize-overlay-col";
      handle.title = "드래그해서 열 너비 조절";
      handle.style.left = (x - 5) + "px";
      handle.style.top = top + "px";
      handle.style.height = tableRect.height + "px";
      handle.addEventListener("mousedown", (ev) => startColResize(ev, idx, i, tableEl));
      overlay.appendChild(handle);
    }
    for (let i = 0; i < trs.length - 1; i++) {
      const r = trs[i].getBoundingClientRect();
      const y = top + (r.bottom - tableRect.top);
      const handle = document.createElement("div");
      handle.className = "gl-tbl-resize-overlay-row";
      handle.title = "드래그해서 행 높이 조절";
      handle.style.top = (y - 5) + "px";
      handle.style.left = left + "px";
      handle.style.width = tableRect.width + "px";
      handle.addEventListener("mousedown", (ev) => startRowResize(ev, idx, i, tableEl));
      overlay.appendChild(handle);
    }
    /* [신규: "전체 선택 시 표를 비율 유지하며 통째로 확대/축소"]
       열 하나·행 하나씩 경계를 조절하는 손잡이들과 별개로, 표의
       우하단 모서리에 "표 전체" 크기 조절 손잡이를 하나 더 둔다.
       이미지의 모서리(대각선) 핸들과 같은 방식 — 대각선으로 끌면
       가로세로 비율(지금 각 열·행 사이의 상대적 비율)은 그대로 둔
       채 표 전체가 함께 커지고 작아진다. 표가 1×1이라 위의 경계
       손잡이가 하나도 없는 경우에도(위 얼리리턴과 무관하게) 이
       손잡이는 항상 필요하므로 별도로 둔다. */
    const cornerHandle = document.createElement("div");
    cornerHandle.className = "gl-tbl-resize-overlay-corner";
    cornerHandle.title = "드래그해서 표 전체 크기를 비율대로 조절";
    cornerHandle.style.left = (left + tableRect.width - 7) + "px";
    cornerHandle.style.top = (top + tableRect.height - 7) + "px";
    cornerHandle.addEventListener("mousedown", (ev) => startTableWholeResize(ev, idx, tableEl));
    overlay.appendChild(cornerHandle);
    containerEl.appendChild(overlay);
  }

  /* [신규] 표 전체 크기 조절 — widthPct(칸 안에서 표가 차지하는 가로 폭)와
     모든 행의 높이를, 드래그 시작 시점 값에 대해 "같은 배율"로 함께
     바꾼다. colWidths(열 사이의 상대적 비율)는 건드리지 않으므로 열들의
     "서로 간" 비율은 그대로 유지된 채 표 전체 크기만 바뀐다 — 이미지
     모서리 핸들의 "비율 유지 확대·축소"와 같은 개념. */
  function startTableWholeResize(ev, idx, tableEl) {
    ev.preventDefault();
    ev.stopPropagation();
    const tbl = cellTables[idx];
    if (!tbl) return;
    const startX = ev.clientX, startY = ev.clientY;
    const rect = tableEl.getBoundingClientRect();
    const startW = rect.width || 1, startH = rect.height || 1;
    const startWidthPct = tbl.widthPct || 100;
    const trEls = tableEl.querySelectorAll("tbody > tr");
    /* rowHeights는 칸마다 명시적으로 정한 값(px)이거나 아직 한 번도
       안 건드려 null(자동)일 수 있다 — 자동인 행은 "지금 실제로 보이는
       높이"를 시작값으로 삼아야 배율을 곱했을 때 화면과 어긋나지 않는다. */
    const startRowHeights = Array.from(trEls).map((tr, i) => tbl.rowHeights[i] || tr.getBoundingClientRect().height || 20);
    /* [버그 방지] "표가 라벨 범위를 벗어나면 안 됨" — 이 칸(라벨 셀)의
       실제 안쪽 높이를 넘는 순간부터는 더 안 커지게 막는다(행 높이
       조절 때와 같은 안전장치를 표 전체 확대에도 동일하게 적용). */
    let maxH = Infinity, maxW = Infinity;
    const cellEl = tableEl.closest(".gl-label-cell");
    if (cellEl) {
      const cs = getComputedStyle(cellEl);
      const padV = (parseFloat(cs.paddingTop) || 0) + (parseFloat(cs.paddingBottom) || 0);
      const padH = (parseFloat(cs.paddingLeft) || 0) + (parseFloat(cs.paddingRight) || 0);
      maxH = Math.max(startH, cellEl.getBoundingClientRect().height - padV - 2);
      maxW = Math.max(startW, cellEl.getBoundingClientRect().width - padH - 2);
    }
    let moved = false;
    glPushUndo();
    cellTables[idx] = glTblDeepClone(tbl);
    const freshTbl = cellTables[idx];
    function onMove(e) {
      /* 대각선 이동량으로 배율을 계산 — 가로/세로 각각의 배율을 평균 내
         "하나의 배율"로 합쳐써야 모서리를 어느 각도로 끌든 표 자체의
         가로세로 비(열·행 사이 비율)가 그대로 유지된다(이미지 모서리
         핸들과 동일한 원리). */
      const scaleX = (startW + (e.clientX - startX)) / startW;
      const scaleY = (startH + (e.clientY - startY)) / startH;
      let scale = (scaleX + scaleY) / 2;
      scale = Math.max(0.3, Math.min(4, scale));
      /* 칸 밖으로 못 나가게 배율 자체를 다시 한 번 제한 */
      scale = Math.min(scale, maxH / startH, maxW / startW);
      moved = true;
      const newWidthPct = Math.max(20, Math.min(100, startWidthPct * scale));
      freshTbl.widthPct = +newWidthPct.toFixed(1);
      tableEl.style.width = freshTbl.widthPct + "%";
      trEls.forEach((tr, i) => {
        const nh = Math.max(16, Math.round(startRowHeights[i] * scale));
        freshTbl.rowHeights[i] = nh;
        tr.style.height = nh + "px";
      });
    }
    function onUp() {
      document.removeEventListener("mousemove", onMove);
      document.removeEventListener("mouseup", onUp);
      if (moved) {
        const realCell = document.querySelector('.gl-label-cell[data-idx="' + idx + '"]:not(.gl-cell-focus-clone)');
        if (realCell) rebuildCellImgFromState(realCell, idx);
        refreshCellFocusPreview();
        renderCellTableEditor();
      }
    }
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  }

  function initGlCellFocusPanel() {
    if (!els.cellFocusPanel) return; /* 마크업이 없는 예외 상황에서도 앱이 죽지 않도록 방어 */

    /* 폰트/글자크기 옵션은 사이드바 select와 완전히 같아야 하므로 직접
       나열하지 않고 그대로 복제한다(둘이 서로 어긋날 위험을 없앤다). */
    if (els.cellEditFont && els.fontSelect) els.cellEditFont.innerHTML = els.fontSelect.innerHTML;
    if (els.cellEditFontSize && els.fontSizeSelect) els.cellEditFontSize.innerHTML = els.fontSizeSelect.innerHTML;

    function renderCellEditAlignGrid() {
      renderAlignGrid(els.cellEditAlignGrid, glCellEditPendingAlign.h, glCellEditPendingAlign.v, (h, v) => {
        glCellEditPendingAlign = { h, v };
        renderCellEditAlignGrid();
        refreshCellFocusPreview();
      });
    }

    /* [신규] "이 칸 텍스트를 표로" 도구 — 구조를 바꾸는 조작(표 삽입/삭제,
       행·열 삽입/삭제, 병합/분할, 열 너비 조절)은 콘텐츠 타입 오버라이드와
       같은 방식으로 즉시 cellTables[idx]에 반영하고 그 자리에서 캔버스도
       다시 그린다(따로 "저장"을 누를 필요 없음). 표 칸 안의 글자만 이
       패널의 입력창에서 입력하는 대로 바로 반영된다.
       [버그 수정] 예전엔 칸을 그냥 한 번 클릭(=글자 커서를 놓으려는
       의도)해도 매번 renderCellTableEditor()로 표 전체 DOM을 다시 그려,
       클릭 직후 포커스가 "새로 만들어진" textarea로 넘어가며 커서 위치가
       날아가 버렸다 — "드래그해서 범위 선택하는 건지 그냥 커서를 두려는
       건지 구분이 안 된다"는 문제의 원인이었다. 이제 그냥 클릭은 표를
       다시 그리지 않고 선택 하이라이트만 갱신하며(브라우저 기본 포커스·
       커서 배치를 건드리지 않음), 실제로 마우스를 다른 칸으로 끌고 가야만
       (진짜 드래그) 범위 선택로 바뀐다 — 한글/워드 표와 같은 동작. */
    let glTableEditUndoPushed = false; /* 표 칸 입력 중 되돌리기를 한 번만 기록 */
    let glTblWidthUndoPushed = false;  /* 표 전체 너비 슬라이더 드래그 중 되돌리기를 한 번만 기록 */
    let glTblActive = null;   /* 가장 최근에 클릭한 칸(주인 칸 기준 r,c) — 행/열 삽입·삭제 기준 */
    let glTblSel = null;      /* 현재 선택 범위 {r1,c1,r2,c2} — 병합/분할 기준 */
    let glTblSelSingle = true; /* Shift 없이 클릭한 "단일 칸" 선택인지 (분할 가능 여부 판단용) */

    /* 되돌리기 스냅샷과 지금 상태가 표 객체를 공유하고 있으므로, 구조를
       바꾸는 조작 전엔 항상 깊은 복사로 새 객체로 갈아끼운 뒤 그 새
       객체만 건드린다(기존 "칸 입력" 로직과 같은 패턴, 구조 조작 전체로 확장). */
    function glTblCloneForEdit(idx) {
      cellTables[idx] = glTblDeepClone(glTblNormalize(cellTables[idx]));
      return cellTables[idx];
    }
    function finishTableStructureChange(idx) {
      rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
      updateEditToolbar();
      renderCellTableEditor();
      refreshCellFocusPreview();
    }
    /* 선택 범위(anchor~end)를 두 칸의 "실제 차지 영역"까지 포함해 사각형으로 계산 */
    function glTblComputeSel(tbl, anchor, end) {
      const a = glTblMasterAt(tbl, anchor.r, anchor.c);
      const e = glTblMasterAt(tbl, end.r, end.c);
      if (!a || !e) return null;
      const r1 = Math.min(a.r, e.r), c1 = Math.min(a.c, e.c);
      const r2 = Math.max(a.r + a.cell.rowSpan - 1, e.r + e.cell.rowSpan - 1);
      const c2 = Math.max(a.c + a.cell.colSpan - 1, e.c + e.cell.colSpan - 1);
      return { r1, c1, r2, c2 };
    }
    /* [신규] DOM을 다시 만들지 않고 선택 하이라이트·병합/분할/삭제 버튼의
       활성 상태만 갱신한다 — 칸을 클릭하거나 드래그로 범위를 넓힐 때마다
       이 함수만 부르고, 절대 renderCellTableEditor()를 다시 부르지
       않는다(그러면 입력 중이던 textarea의 포커스·커서가 날아간다). */
    function glTblUpdateSelHighlight() {
      const idx = glCellEditIdx;
      const tbl = idx != null && cellTables[idx];
      if (!tbl) return;
      const sel = glTblSel || { r1: 0, c1: 0, r2: 0, c2: 0 };
      const grid = els.cellTableGrid;
      if (grid) {
        grid.querySelectorAll(".gl-cell-table-edit-td").forEach(td => {
          const r = Number(td.dataset.r), c = Number(td.dataset.c);
          const isSel = r >= sel.r1 && r <= sel.r2 && c >= sel.c1 && c <= sel.c2;
          td.classList.toggle("gl-cell-table-edit-td-sel", isSel);
        });
      }
      /* [신규] 캔버스(메인 격자·확대 미리보기)에 지금 보이는 이 칸의 표에도
         같은 선택 하이라이트를 칠한다 — 사이드 패널을 안 보고 캔버스만
         보고 있어도 뭐가 선택됐는지 알 수 있어야 하므로. */
      document.querySelectorAll('.gl-label-cell[data-idx="' + idx + '"] .gl-cell-table td[data-r]').forEach(td => {
        const r = Number(td.dataset.r), c = Number(td.dataset.c);
        const isSel = r >= sel.r1 && r <= sel.r2 && c >= sel.c1 && c <= sel.c2;
        td.classList.toggle("gl-cell-table-sel", isSel);
      });
      const { R, C } = glTblDims(tbl);
      const selSpans = sel.r1 !== sel.r2 || sel.c1 !== sel.c2;
      const canMerge = selSpans && glTblCanMerge(tbl, sel.r1, sel.c1, sel.r2, sel.c2);
      const selMaster = tbl.rows[sel.r1] && tbl.rows[sel.r1][sel.c1];
      const canSplit = !!(glTblSelSingle && selMaster && (selMaster.rowSpan > 1 || selMaster.colSpan > 1) &&
        (sel.r1 + selMaster.rowSpan - 1 === sel.r2) && (sel.c1 + selMaster.colSpan - 1 === sel.c2));
      if (els.cellTableMergeBtn) els.cellTableMergeBtn.disabled = !canMerge;
      if (els.cellTableSplitBtn) els.cellTableSplitBtn.disabled = !canSplit;
      if (els.cellTableDelRowBtn) els.cellTableDelRowBtn.disabled = R <= 1;
      if (els.cellTableDelColBtn) els.cellTableDelColBtn.disabled = C <= 1;
      /* [신규] 균등 분배 버튼 — 2개 이상의 열/행에 걸친 선택일 때만 뜻이
         있으므로 그 외에는 비활성화한다. */
      if (els.cellTableDistColBtn) els.cellTableDistColBtn.disabled = sel.c2 <= sel.c1;
      if (els.cellTableDistRowBtn) els.cellTableDistRowBtn.disabled = sel.r2 <= sel.r1;
    }
    function renderCellTableEditor() {
      const idx = glCellEditIdx;
      if (idx == null) return;
      if (cellTables[idx]) cellTables[idx] = glTblNormalize(cellTables[idx]);
      const tbl = cellTables[idx];
      const hasTbl = !!(tbl && tbl.rows && tbl.rows.length && tbl.rows[0] && tbl.rows[0].length);
      if (els.cellEditTextPlainWrap) els.cellEditTextPlainWrap.style.display = hasTbl ? "none" : "";
      if (els.cellTableEditorWrap) els.cellTableEditorWrap.style.display = hasTbl ? "" : "none";
      if (!hasTbl) return;
      const { R, C } = glTblDims(tbl);
      /* 표가 바뀌어 이전 선택이 범위를 벗어나면 초기화 */
      if (!glTblActive || glTblActive.r >= R || glTblActive.c >= C) glTblActive = { r: 0, c: 0 };
      if (!glTblSel || glTblSel.r2 >= R || glTblSel.c2 >= C) glTblSel = { r1: 0, c1: 0, r2: 0, c2: 0 };
      if (els.cellTableWidthRange) els.cellTableWidthRange.value = tbl.widthPct || 100;
      if (els.cellTableWidthVal) els.cellTableWidthVal.textContent = (tbl.widthPct || 100) + "%";
      /* [신규] 테두리 컨트롤 — 이 표에 저장된 값으로 맞춰둔다(다른 칸의
         표를 열면 그 표의 값으로 다시 바뀐다 — 표마다 독립적인 설정). */
      if (els.cellTableBorderStyle) els.cellTableBorderStyle.value = tbl.borderStyle || "solid";
      if (els.cellTableBorderWidth) els.cellTableBorderWidth.value = tbl.borderWidth != null ? tbl.borderWidth : 1;
      if (els.cellTableBorderWidthVal) els.cellTableBorderWidthVal.textContent = (tbl.borderWidth != null ? tbl.borderWidth : 1) + "px";
      if (els.cellTableBorderColor) els.cellTableBorderColor.value = tbl.borderColor || "#000000";

      const grid = els.cellTableGrid;
      if (grid) {
        grid.innerHTML = "";
        const table = document.createElement("table");
        table.className = "gl-cell-table-edit";
        table.style.width = (tbl.widthPct || 100) + "%";
        table.style.setProperty("--gl-tbl-bw", (tbl.borderWidth != null ? tbl.borderWidth : 1) + "px");
        table.style.setProperty("--gl-tbl-bs", tbl.borderStyle || "solid");
        if (tbl.borderColor) table.style.setProperty("--gl-tbl-bc", tbl.borderColor);
        const colgroup = document.createElement("colgroup");
        tbl.colWidths.forEach(w => {
          const col = document.createElement("col");
          col.style.width = w + "%";
          colgroup.appendChild(col);
        });
        table.appendChild(colgroup);
        const tbody = document.createElement("tbody");
        const tabOrder = []; /* Tab 이동 순서(읽기 순서)용 textarea 목록 */

        tbl.rows.forEach((row, r) => {
          const tr = document.createElement("tr");
          const rh = tbl.rowHeights && tbl.rowHeights[r];
          if (rh) tr.style.height = rh + "px";
          row.forEach((cell, c) => {
            if (cell === null) return; /* 병합에 덮인 칸은 그리지 않음(진짜 <table>과 동일) */
            const td = document.createElement("td");
            if (cell.rowSpan > 1) td.rowSpan = cell.rowSpan;
            if (cell.colSpan > 1) td.colSpan = cell.colSpan;
            const isSel = r >= glTblSel.r1 && r <= glTblSel.r2 && c >= glTblSel.c1 && c <= glTblSel.c2;
            td.className = "gl-cell-table-edit-td" + (isSel ? " gl-cell-table-edit-td-sel" : "");
            td.dataset.r = r; td.dataset.c = c;
            if (cell.bg && !isSel) td.style.background = cell.bg;
            /* [수정] 이 칸의 변별 테두리 오버라이드(edge)를 편집기 미리보기에도
               그대로 반영 — 최종 인쇄 결과와 편집 화면이 항상 같아 보이게 한다. */
            if (cell.edge) {
              ["top", "right", "bottom", "left"].forEach(s => {
                const e = cell.edge[s];
                if (e) td.style.setProperty("border-" + s, e.w + "px " + e.s + " " + (e.c || "currentColor"));
              });
            }

            const ta = document.createElement("textarea");
            ta.className = "gl-cell-table-cell-input";
            ta.value = cell.text || "";
            ta.rows = 1;
            ta.setAttribute("aria-label", (r + 1) + "행 " + (c + 1) + "열");
            const autoGrow = () => { ta.style.height = "auto"; ta.style.height = Math.min(ta.scrollHeight, 140) + "px"; };
            ta.addEventListener("focus", () => {
              if (!glTableEditUndoPushed) {
                glPushUndo();
                cellTables[idx] = glTblDeepClone(cellTables[idx]);
                glTableEditUndoPushed = true;
              }
            });
            ta.addEventListener("blur", () => { glTableEditUndoPushed = false; });
            ta.addEventListener("input", () => {
              /* focus 시 새 객체로 갈아끼웠으므로, 지금 시점의 cellTables[idx]에서
                 매번 다시 좌표로 찾아 써야 오래된(스냅샷) 객체를 건드리지 않는다. */
              cellTables[idx].rows[r][c].text = ta.value;
              autoGrow();
              rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
              refreshCellFocusPreview();
            });
            ta.addEventListener("keydown", (e) => {
              if (e.key !== "Tab") return;
              e.preventDefault();
              const pos = tabOrder.indexOf(ta);
              const next = tabOrder[pos + (e.shiftKey ? -1 : 1)];
              if (next) next.focus();
            });
            /* [버그 수정] 예전엔 여기서 매번 renderCellTableEditor()를 다시
               불러 표 전체 DOM을 새로 그렸다 — 그러면 방금 클릭한 textarea가
               "새 엘리먼트"로 교체되어 커서 위치가 사라지고, 매 클릭이
               "칸 선택"처럼만 보여 글자 입력이 어려웠다. 이제는:
                 · 그냥 클릭(Shift 없음) → 이 칸 하나만 선택 표시만 갱신하고
                   브라우저 기본 포커스·커서 배치는 절대 건드리지 않는다.
                 · 다른 칸으로 마우스를 끌고 가야만(진짜 드래그) 그 순간부터
                   범위 선택으로 바뀐다(한글/워드 표와 같은 구분법). */
            ta.addEventListener("mousedown", (e) => {
              if (e.shiftKey && glTblActive) {
                glTblSel = glTblComputeSel(tbl, glTblActive, { r, c }) || glTblSel;
                glTblSelSingle = false;
                glTblUpdateSelHighlight();
                return; /* Shift+클릭은 즉시 범위 확정 — 드래그 추적 불필요 */
              }
              glTblActive = { r, c };
              glTblSel = { r1: r, c1: c, r2: r + cell.rowSpan - 1, c2: c + cell.colSpan - 1 };
              glTblSelSingle = true;
              glTblUpdateSelHighlight();
              let dragMoved = false;
              function onMove(ev) {
                const overEl = document.elementFromPoint(ev.clientX, ev.clientY);
                const overTd = overEl && overEl.closest(".gl-cell-table-edit-td");
                if (!overTd || !grid.contains(overTd)) return;
                const or = Number(overTd.dataset.r), oc = Number(overTd.dataset.c);
                if (or === r && oc === c) return; /* 아직 시작 칸 위 — 드래그로 취급하지 않음 */
                if (!dragMoved) {
                  dragMoved = true;
                  /* 드래그가 실제로 칸 경계를 넘는 순간부터만 브라우저의
                     기본 텍스트 선택(파랗게 글자 긁힘)을 꺼서, 우리가 그리는
                     "칸 선택" 하이라이트와 겹쳐 지저분해지지 않게 한다. */
                  const sel0 = document.getSelection(); if (sel0) sel0.removeAllRanges();
                  grid.classList.add("gl-tbl-dragging");
                }
                glTblSel = glTblComputeSel(tbl, { r, c }, { r: or, c: oc }) || glTblSel;
                glTblSelSingle = false;
                glTblUpdateSelHighlight();
              }
              function onUp() {
                document.removeEventListener("mousemove", onMove);
                document.removeEventListener("mouseup", onUp);
                grid.classList.remove("gl-tbl-dragging");
              }
              document.addEventListener("mousemove", onMove);
              document.addEventListener("mouseup", onUp);
            });
            td.appendChild(ta);
            tr.appendChild(td);
            tabOrder.push(ta);
          });
          tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        grid.appendChild(table);
        requestAnimationFrame(() => {
          grid.querySelectorAll(".gl-cell-table-cell-input").forEach(ta => {
            ta.style.height = "auto"; ta.style.height = Math.min(ta.scrollHeight, 140) + "px";
          });
          /* [수정] 예전엔 각 <td> 안에 얇은 손잡이를 심어뒀는데(옆 칸에
             가려지기 쉬워 "인식이 안 된다"는 피드백) — 이제는 글자
             크기까지 반영해 표 레이아웃이 다 자리잡은 이 시점에, 표
             전체 위에 겹쳐 그리는 오버레이 손잡이를 한 번에 붙인다. */
          attachTableResizeOverlay(grid, table, idx);
        });
      }

      glTblUpdateSelHighlight();
    }

    /* [신규] 표 전체 크기(칸 안에서 차지하는 가로 폭) 조절 슬라이더 —
       "표 크기 자체를 조절 못 한다"는 피드백에 대응. 열 너비·행 높이는
       표 "안"의 상대적 배분이고, 이건 표 "전체"가 칸에서 얼마나 넓게
       보일지를 정한다(작게 줄이면 나머지 공간은 비워둔 채 가운데 정렬). */
    if (els.cellTableWidthRange) {
      els.cellTableWidthRange.addEventListener("input", () => {
        const idx = glCellEditIdx;
        const tbl = idx != null && cellTables[idx];
        if (!tbl) return;
        const pct = Number(els.cellTableWidthRange.value) || 100;
        if (els.cellTableWidthVal) els.cellTableWidthVal.textContent = pct + "%";
        if (!glTblWidthUndoPushed) { glPushUndo(); glTblWidthUndoPushed = true; }
        cellTables[idx] = glTblDeepClone(tbl);
        cellTables[idx].widthPct = pct;
        const editTable = els.cellTableGrid && els.cellTableGrid.querySelector(".gl-cell-table-edit");
        if (editTable) editTable.style.width = pct + "%";
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        refreshCellFocusPreview();
      });
      els.cellTableWidthRange.addEventListener("change", () => { glTblWidthUndoPushed = false; });
    }
    /* [신규] 테두리 종류·굵기·색상 — 값이 바뀌면 표 전체(borderWidth/
       borderStyle/borderColor)에 반영하고 그 자리에서 미리보기를
       새로 그린다. 슬라이더는 드래그 중 되돌리기를 한 번만 기록한다. */
    let glTblBorderUndoPushed = false;
    function withCurrentTable(mutate) {
      const idx = glCellEditIdx;
      const tbl = idx != null && cellTables[idx];
      if (!tbl) return null;
      if (!glTblBorderUndoPushed) { glPushUndo(); glTblBorderUndoPushed = true; }
      cellTables[idx] = glTblDeepClone(glTblNormalize(cellTables[idx]));
      mutate(cellTables[idx]);
      rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
      refreshCellFocusPreview();
      return cellTables[idx];
    }
    function stopBorderUndoBatch() { glTblBorderUndoPushed = false; }
    if (els.cellTableBorderStyle) {
      els.cellTableBorderStyle.addEventListener("change", () => {
        withCurrentTable(t => { t.borderStyle = els.cellTableBorderStyle.value; });
        stopBorderUndoBatch();
        const table = els.cellTableGrid && els.cellTableGrid.querySelector(".gl-cell-table-edit");
        if (table) table.style.setProperty("--gl-tbl-bs", els.cellTableBorderStyle.value);
      });
    }
    if (els.cellTableBorderWidth) {
      els.cellTableBorderWidth.addEventListener("input", () => {
        const w = Number(els.cellTableBorderWidth.value) || 0;
        if (els.cellTableBorderWidthVal) els.cellTableBorderWidthVal.textContent = w + "px";
        withCurrentTable(t => { t.borderWidth = w; });
        const table = els.cellTableGrid && els.cellTableGrid.querySelector(".gl-cell-table-edit");
        if (table) table.style.setProperty("--gl-tbl-bw", w + "px");
      });
      els.cellTableBorderWidth.addEventListener("change", stopBorderUndoBatch);
    }
    if (els.cellTableBorderColor) {
      els.cellTableBorderColor.addEventListener("input", () => {
        withCurrentTable(t => { t.borderColor = els.cellTableBorderColor.value; });
        const table = els.cellTableGrid && els.cellTableGrid.querySelector(".gl-cell-table-edit");
        if (table) table.style.setProperty("--gl-tbl-bc", els.cellTableBorderColor.value);
      });
      els.cellTableBorderColor.addEventListener("change", stopBorderUndoBatch);
    }
    if (els.cellTableBorderColorClearBtn) {
      els.cellTableBorderColorClearBtn.addEventListener("click", () => {
        withCurrentTable(t => { t.borderColor = null; });
        stopBorderUndoBatch();
        const table = els.cellTableGrid && els.cellTableGrid.querySelector(".gl-cell-table-edit");
        if (table) table.style.removeProperty("--gl-tbl-bc");
      });
    }
    /* [수정] "선택한 칸(들)의 위/오른쪽/아래/왼쪽 테두리" — 예전엔 그
       변을 그냥 켜고 끄기만 했는데, 이제는 위 "테두리" 줄에서 고른
       종류·굵기·색 그대로 그 위치에 "칠한다"(선을 없애고 싶으면 종류를
       "없음"으로 고른 뒤 위치 버튼을 누르면 된다 — "없애기"도 여러
       칠하기 값 중 하나일 뿐이다). applyBgToSelection과 같은 패턴(깊은
       복사 후 되돌리기 기록). */
    function glTblCurrentEdgeVal() {
      const style = els.cellTableBorderStyle ? els.cellTableBorderStyle.value : "solid";
      if (style === "none") return { w: 0, s: "none", c: null };
      return {
        w: els.cellTableBorderWidth ? (Number(els.cellTableBorderWidth.value) || 0) : 1,
        s: style,
        c: els.cellTableBorderColor ? els.cellTableBorderColor.value : null,
      };
    }
    function applyBorderTarget(target) {
      const idx = glCellEditIdx;
      const tbl = idx != null && cellTables[idx];
      if (!tbl || !glTblSel) return;
      glPushUndo();
      const fresh = glTblCloneForEdit(idx);
      const val = glTblCurrentEdgeVal();
      if (target === "top" || target === "right" || target === "bottom" || target === "left") glTblPaintSide(fresh, glTblSel, target, val);
      else if (target === "outer") glTblPaintOuter(fresh, glTblSel, val);
      else if (target === "inner") glTblPaintInner(fresh, glTblSel, val);
      else if (target === "all")   glTblPaintAll(fresh, glTblSel, val);
      else if (target === "reset") glTblResetEdges(fresh, glTblSel);
      finishTableStructureChange(idx);
    }
    if (els.cellTableBorderTopBtn)    els.cellTableBorderTopBtn.addEventListener("click", () => applyBorderTarget("top"));
    if (els.cellTableBorderRightBtn)  els.cellTableBorderRightBtn.addEventListener("click", () => applyBorderTarget("right"));
    if (els.cellTableBorderBottomBtn) els.cellTableBorderBottomBtn.addEventListener("click", () => applyBorderTarget("bottom"));
    if (els.cellTableBorderLeftBtn)   els.cellTableBorderLeftBtn.addEventListener("click", () => applyBorderTarget("left"));
    if (els.cellTableBorderOuterBtn)  els.cellTableBorderOuterBtn.addEventListener("click", () => applyBorderTarget("outer"));
    if (els.cellTableBorderInnerBtn)  els.cellTableBorderInnerBtn.addEventListener("click", () => applyBorderTarget("inner"));
    if (els.cellTableBorderAllBtn)    els.cellTableBorderAllBtn.addEventListener("click", () => applyBorderTarget("all"));
    if (els.cellTableBorderResetBtn)  els.cellTableBorderResetBtn.addEventListener("click", () => applyBorderTarget("reset"));
    /* [신규] 선택한 칸(들)의 배경색 채우기/지우기 — glTblSel 범위 안의
       "주인" 칸들(병합에 덮인 자리는 건드리지 않음)에만 적용한다. */
    function applyBgToSelection(bg) {
      const idx = glCellEditIdx;
      const tbl = idx != null && cellTables[idx];
      if (!tbl || !glTblSel) return;
      glPushUndo();
      const fresh = glTblCloneForEdit(idx);
      const seen = new Set();
      for (let r = glTblSel.r1; r <= glTblSel.r2; r++) {
        for (let c = glTblSel.c1; c <= glTblSel.c2; c++) {
          const found = glTblMasterAt(fresh, r, c);
          if (found && !seen.has(found.cell)) { seen.add(found.cell); found.cell.bg = bg; }
        }
      }
      finishTableStructureChange(idx);
    }
    if (els.cellTableBgColor) {
      els.cellTableBgColor.addEventListener("input", () => applyBgToSelection(els.cellTableBgColor.value));
    }
    if (els.cellTableBgClearBtn) {
      els.cellTableBgClearBtn.addEventListener("click", () => applyBgToSelection(null));
    }
    /* [신규] 선택한 열/행의 너비·높이를 서로 같게(균등 분배) — 워드의
       "셀 간격을 동일하게"와 같은 기능. 2칸 이상 선택했을 때만 뜻이 있다. */
    if (els.cellTableDistColBtn) {
      els.cellTableDistColBtn.addEventListener("click", () => withActiveTable((t) => {
        const sel = glTblSel;
        if (!sel || sel.c2 <= sel.c1) return;
        let sum = 0;
        for (let c = sel.c1; c <= sel.c2; c++) sum += t.colWidths[c];
        const each = +(sum / (sel.c2 - sel.c1 + 1)).toFixed(2);
        for (let c = sel.c1; c <= sel.c2; c++) t.colWidths[c] = each;
      }));
    }
    if (els.cellTableDistRowBtn) {
      els.cellTableDistRowBtn.addEventListener("click", () => withActiveTable((t) => {
        const sel = glTblSel;
        if (!sel || sel.r2 <= sel.r1) return;
        const trEls = els.cellTableGrid ? els.cellTableGrid.querySelectorAll(".gl-cell-table-edit tbody > tr") : [];
        let sum = 0;
        for (let r = sel.r1; r <= sel.r2; r++) {
          let h = t.rowHeights[r];
          if (h == null) { const el = trEls[r]; h = el ? el.getBoundingClientRect().height : 26; }
          sum += h;
        }
        const each = Math.round(sum / (sel.r2 - sel.r1 + 1));
        for (let r = sel.r1; r <= sel.r2; r++) t.rowHeights[r] = each;
      }));
    }
    if (els.cellTableInsertBtn) {
      els.cellTableInsertBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        if (idx == null) return;
        glPushUndo();
        cellTables[idx] = { rows: [["", ""], ["", ""]] }; /* 2×2로 시작 — 행/열 삽입 버튼으로 필요한 만큼 키움 */
        glTblActive = { r: 0, c: 0 };
        finishTableStructureChange(idx);
      });
    }
    if (els.cellTableDeleteBtn) {
      els.cellTableDeleteBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        if (idx == null || !cellTables[idx]) return;
        glPushUndo();
        delete cellTables[idx];
        finishTableStructureChange(idx);
      });
    }
    /* 행/열 삽입·삭제는 모두 "가장 최근에 클릭한 칸"(glTblActive) 기준으로
       동작한다 — 예전처럼 항상 맨 끝에 추가/맨 끝에서 삭제하는 게 아니라,
       원하는 위치에 바로 삽입·삭제할 수 있다. */
    function withActiveTable(fn) {
      const idx = glCellEditIdx;
      const tbl = idx != null && cellTables[idx];
      if (!tbl || !glTblActive) return;
      glPushUndo();
      const fresh = glTblCloneForEdit(idx);
      fn(fresh, glTblActive.r, glTblActive.c);
      finishTableStructureChange(idx);
    }
    if (els.cellTableInsRowAboveBtn) {
      els.cellTableInsRowAboveBtn.addEventListener("click", () => withActiveTable((t, r) => glTblInsertRow(t, r)));
    }
    if (els.cellTableInsRowBelowBtn) {
      els.cellTableInsRowBelowBtn.addEventListener("click", () => withActiveTable((t, r) => {
        const m = glTblMasterAt(t, r, glTblActive.c);
        glTblInsertRow(t, (m ? m.r + m.cell.rowSpan : r + 1));
      }));
    }
    if (els.cellTableDelRowBtn) {
      els.cellTableDelRowBtn.addEventListener("click", () => withActiveTable((t, r) => {
        if (glTblDims(t).R <= 1) return;
        glTblDeleteRow(t, r);
        glTblActive = { r: Math.min(r, glTblDims(t).R - 1), c: glTblActive.c };
      }));
    }
    if (els.cellTableInsColLeftBtn) {
      els.cellTableInsColLeftBtn.addEventListener("click", () => withActiveTable((t, r, c) => glTblInsertCol(t, c)));
    }
    if (els.cellTableInsColRightBtn) {
      els.cellTableInsColRightBtn.addEventListener("click", () => withActiveTable((t, r, c) => {
        const m = glTblMasterAt(t, r, c);
        glTblInsertCol(t, (m ? m.c + m.cell.colSpan : c + 1));
      }));
    }
    if (els.cellTableDelColBtn) {
      els.cellTableDelColBtn.addEventListener("click", () => withActiveTable((t, r, c) => {
        if (glTblDims(t).C <= 1) return;
        glTblDeleteCol(t, c);
        glTblActive = { r: glTblActive.r, c: Math.min(c, glTblDims(t).C - 1) };
      }));
    }
    if (els.cellTableMergeBtn) {
      els.cellTableMergeBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        const tbl = idx != null && cellTables[idx];
        if (!tbl || !glTblSel) return;
        glPushUndo();
        const fresh = glTblCloneForEdit(idx);
        if (glTblMergeRange(fresh, glTblSel.r1, glTblSel.c1, glTblSel.r2, glTblSel.c2)) {
          glTblActive = { r: glTblSel.r1, c: glTblSel.c1 };
          glTblSelSingle = true;
        }
        finishTableStructureChange(idx);
      });
    }
    if (els.cellTableSplitBtn) {
      els.cellTableSplitBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        const tbl = idx != null && cellTables[idx];
        if (!tbl || !glTblSel) return;
        glPushUndo();
        const fresh = glTblCloneForEdit(idx);
        glTblSplitCell(fresh, glTblSel.r1, glTblSel.c1);
        finishTableStructureChange(idx);
      });
    }

    /* [신규] "셀을 선택(드래그) 후 Ctrl+방향키로 셀 크기를 조절" 피드백
       대응 — 드래그(또는 Shift+클릭)로 잡아둔 glTblSel 범위를 기준으로,
       Ctrl(맥은 Cmd)을 누른 채 방향키를 누르면 마우스로 손잡이를 끄는
       것과 같은 폭/높이 조절을 키보드로 할 수 있다.
       · 좌우: 선택 범위 오른쪽 경계를 옆 열과 주고받는다(마우스 드래그와
         동일한 방식) — 단, 선택이 맨 오른쪽 열까지 걸쳐 있어 오른쪽에
         나눠줄 이웃이 없으면 왼쪽 경계를 대신 움직인다.
       · 상하: 선택된 모든 행의 높이를 각각 늘리거나 줄인다(열과 달리
         행 높이는 서로 100%를 나눠 갖는 비율이 아니라 각자 독립된
         값이라 이웃에게서 빼앗아 올 필요가 없다).
       연속으로 눌러도 한 번의 "누르고 있는 동안"은 되돌리기 한 단계로
       묶는다(Ctrl을 뗄 때 다음 조작부터 새 단계). */
    const GL_TBL_KEY_COL_STEP = 2;   // %p
    const GL_TBL_KEY_ROW_STEP = 4;   // px
    const GL_TBL_KEY_MIN_COL = 8;    // %p
    const GL_TBL_KEY_MIN_ROW = 16;   // px
    let glTblKeyResizeUndoPushed = false;
    function glTblKeyAdjustCol(fresh, sel, growing) {
      const C = fresh.colWidths.length;
      let boundary, sign;
      if (sel.c2 + 1 < C) { boundary = sel.c2; sign = growing ? 1 : -1; }
      else if (sel.c1 - 1 >= 0) { boundary = sel.c1 - 1; sign = growing ? -1 : 1; }
      else return false;
      const delta = sign * GL_TBL_KEY_COL_STEP;
      const w0 = fresh.colWidths[boundary], w1 = fresh.colWidths[boundary + 1];
      const nw0 = w0 + delta, nw1 = w1 - delta;
      if (nw0 < GL_TBL_KEY_MIN_COL || nw1 < GL_TBL_KEY_MIN_COL) return false;
      fresh.colWidths[boundary] = +nw0.toFixed(2);
      fresh.colWidths[boundary + 1] = +nw1.toFixed(2);
      return true;
    }
    function glTblKeyAdjustRow(fresh, sel, growing) {
      const trEls = els.cellTableGrid ? els.cellTableGrid.querySelectorAll(".gl-cell-table-edit tbody > tr") : [];
      let changed = false;
      for (let r = sel.r1; r <= sel.r2; r++) {
        let base = fresh.rowHeights[r];
        if (base == null) { const el = trEls[r]; base = el ? el.getBoundingClientRect().height : 26; }
        const nh = Math.max(GL_TBL_KEY_MIN_ROW, base + (growing ? GL_TBL_KEY_ROW_STEP : -GL_TBL_KEY_ROW_STEP));
        fresh.rowHeights[r] = Math.round(nh);
        changed = true;
      }
      return changed;
    }
    /* [신규] "전체 선택 → 이미지처럼 통째로 비율 유지 리사이즈"를 키보드
       에서도 — 지금 선택 범위가 표 전체(모든 행×모든 열)와 정확히
       같으면, 행/열을 하나씩 늘리는 대신 마우스 모서리 드래그
       (startTableWholeResize)와 완전히 같은 방식으로 widthPct(표 전체
       가로 폭)와 모든 행 높이를 "같은 배율"로 함께 조절한다 — 그래야
       열들 사이의 상대 비율이 흐트러지지 않고 표 전체가 사진처럼
       깨끗하게 커지고 작아진다. */
    const GL_TBL_KEY_WHOLE_SCALE_STEP = 1.06; // 한 번 누를 때 6%씩 확대/축소
    function glTblIsWholeSelected(fresh, sel) {
      const { R, C } = glTblDims(fresh);
      return sel.r1 === 0 && sel.c1 === 0 && sel.r2 === R - 1 && sel.c2 === C - 1;
    }
    function glTblKeyAdjustWhole(fresh, growing) {
      const scale = growing ? GL_TBL_KEY_WHOLE_SCALE_STEP : (1 / GL_TBL_KEY_WHOLE_SCALE_STEP);
      const startWidthPct = fresh.widthPct || 100;
      fresh.widthPct = +Math.max(20, Math.min(100, startWidthPct * scale)).toFixed(1);
      const trEls = els.cellTableGrid ? els.cellTableGrid.querySelectorAll(".gl-cell-table-edit tbody > tr") : [];
      const R = fresh.rows.length;
      for (let r = 0; r < R; r++) {
        let base = fresh.rowHeights[r];
        if (base == null) { const el = trEls[r]; base = el ? el.getBoundingClientRect().height : 26; }
        fresh.rowHeights[r] = Math.max(GL_TBL_KEY_MIN_ROW, Math.round(base * scale));
      }
      return true;
    }
    function glTblRefocusActive() {
      if (!glTblActive || !els.cellTableGrid) return;
      const td = els.cellTableGrid.querySelector('.gl-cell-table-edit-td[data-r="' + glTblActive.r + '"][data-c="' + glTblActive.c + '"]');
      const ta = td && td.querySelector("textarea");
      if (ta) { ta.focus(); const v = ta.value; try { ta.setSelectionRange(v.length, v.length); } catch {} }
    }
    /* [신규/수정] Ctrl(⌘)+방향키로 셀 크기 조절 — 예전엔 사이드 패널
       그리드(#gl-cell-table-grid)에 포커스가 있을 때만 동작했는데, 이제
       캔버스(메인 격자/확대 미리보기)에서 표 칸을 선택했을 때도 똑같이
       동작해야 한다("캔버스에서도 되게 해달라"는 피드백). 그래서 실제
       처리 로직을 공용 함수로 뽑아 두 군데(사이드 패널, 캔버스) keydown
       리스너가 함께 쓰게 했다. 선택 범위가 표 전체와 정확히 같으면
       "전체 비율 유지 리사이즈"(glTblKeyAdjustWhole)를, 아니면 예전처럼
       선택된 열/행만 개별 조절한다. */
    function handleGlTblResizeKeydown(e) {
      if (!(e.ctrlKey || e.metaKey)) return;
      const isCol = e.key === "ArrowLeft" || e.key === "ArrowRight";
      const isRow = e.key === "ArrowUp" || e.key === "ArrowDown";
      if (!isCol && !isRow) return;
      const idx = glCellEditIdx;
      const tbl0 = idx != null && cellTables[idx];
      if (!tbl0 || !glTblSel) return;
      e.preventDefault();
      if (!glTblKeyResizeUndoPushed) { glPushUndo(); glTblKeyResizeUndoPushed = true; }
      cellTables[idx] = glTblDeepClone(glTblNormalize(cellTables[idx]));
      const fresh = cellTables[idx];
      const growing = (e.key === "ArrowRight" || e.key === "ArrowDown");
      const changed = glTblIsWholeSelected(fresh, glTblSel)
        ? glTblKeyAdjustWhole(fresh, growing)
        : (isCol ? glTblKeyAdjustCol(fresh, glTblSel, e.key === "ArrowRight") : glTblKeyAdjustRow(fresh, glTblSel, e.key === "ArrowDown"));
      if (changed) {
        renderCellTableEditor();
        glTblRefocusActive();
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        refreshCellFocusPreview();
      }
    }
    function handleGlTblResizeKeyup(e) {
      if (e.key === "Control" || e.key === "Meta") glTblKeyResizeUndoPushed = false;
    }
    if (els.cellTableGrid) {
      els.cellTableGrid.addEventListener("keydown", handleGlTblResizeKeydown);
      els.cellTableGrid.addEventListener("keyup", handleGlTblResizeKeyup);
    }
    /* [신규] 캔버스(메인 격자·확대 미리보기)의 표에도 같은 단축키 연결 —
       표 칸(td[contenteditable])에 포커스가 가 있을 때 keydown이 그
       td에서 시작해 document까지 버블링되므로, document 레벨에서 한 번만
       걸어도 캔버스 어디의 표든 다 대응된다(사이드 패널은 자체 리스너가
       이미 처리하므로 아래에서 한 번 더 실행되지 않도록 걸러낸다). */
    document.addEventListener("keydown", (e) => {
      if (els.cellTableGrid && els.cellTableGrid.contains(e.target)) return; /* 사이드 패널은 위에서 이미 처리함 */
      const td = e.target.closest && e.target.closest(".gl-cell-table td[contenteditable]");
      if (!td) return;
      handleGlTblResizeKeydown(e);
    });
    document.addEventListener("keyup", (e) => {
      if (els.cellTableGrid && els.cellTableGrid.contains(e.target)) return;
      const td = e.target.closest && e.target.closest(".gl-cell-table td[contenteditable]");
      if (!td) return;
      handleGlTblResizeKeyup(e);
    });

    /* [신규] 표 즐겨찾기(재사용 양식) UI */
    function renderGlTableFavSelect() {
      if (!els.cellTableFavSelect) return;
      const list = getGlTableFavorites();
      const cur = els.cellTableFavSelect.value;
      els.cellTableFavSelect.innerHTML = '<option value="">즐겨찾기에서 불러오기…</option>' +
        list.map(f => '<option value="' + f.id + '">' + escHtml(f.name) + "</option>").join("");
      if (list.some(f => f.id === cur)) els.cellTableFavSelect.value = cur;
    }
    renderGlTableFavSelect();
    if (els.cellTableFavSaveBtn) {
      els.cellTableFavSaveBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        const tbl = idx != null && cellTables[idx];
        if (!tbl) { if (typeof showToast === "function") showToast("먼저 표를 만든 뒤 저장해 주세요.", "info", 2200); return; }
        const name = (els.cellTableFavNameInput && els.cellTableFavNameInput.value.trim()) || "이름 없는 양식";
        addGlTableFavorite(name, glTblNormalize(tbl));
        if (els.cellTableFavNameInput) els.cellTableFavNameInput.value = "";
        renderGlTableFavSelect();
        if (typeof showToast === "function") showToast('"' + name + '" 양식을 즐겨찾기에 저장했습니다.', "success", 2200);
      });
    }
    if (els.cellTableFavApplyBtn) {
      els.cellTableFavApplyBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        if (idx == null) return;
        const id = els.cellTableFavSelect && els.cellTableFavSelect.value;
        const fav = getGlTableFavorites().find(f => f.id === id);
        if (!fav) { if (typeof showToast === "function") showToast("불러올 양식을 먼저 선택해 주세요.", "info", 2200); return; }
        glPushUndo();
        cellTables[idx] = glTblDeepClone(glTblNormalize(fav.table));
        glTblActive = { r: 0, c: 0 };
        glTblSel = { r1: 0, c1: 0, r2: 0, c2: 0 };
        finishTableStructureChange(idx);
        if (typeof showToast === "function") showToast('"' + fav.name + '" 양식을 불러왔습니다.', "success", 2000);
      });
    }
    if (els.cellTableFavDeleteBtn) {
      els.cellTableFavDeleteBtn.addEventListener("click", () => {
        const id = els.cellTableFavSelect && els.cellTableFavSelect.value;
        const fav = getGlTableFavorites().find(f => f.id === id);
        if (!fav) { if (typeof showToast === "function") showToast("삭제할 양식을 먼저 선택해 주세요.", "info", 2200); return; }
        if (!confirm('"' + fav.name + '" 양식을 즐겨찾기에서 삭제할까요?')) return;
        deleteGlTableFavorite(id);
        renderGlTableFavSelect();
        if (typeof showToast === "function") showToast("삭제했습니다.", "info", 1800);
      });
    }

    /* [신규] 텍스트/이미지 탭 전환 — "텍스트+이미지" 규격일 때만 탭 바가
       보이고, 텍스트 전용/이미지 전용 규격은 어차피 칼럼이 하나뿐이라
       탭 없이 그 칼럼만 바로 보여준다. [수정] 전역 contentType 대신
       이 칸의 유효 타입(오버라이드 있으면 그것)을 기준으로 판단한다. */
    let cellFocusTab = "text";
    function applyCellFocusTab() {
      const eff = glEffContentType(glCellEditIdx);
      const showText  = eff === "text" || eff === "both";
      const showImage = eff === "image" || eff === "both";
      const showTabs  = showText && showImage;
      if (els.cellFocusTabs) els.cellFocusTabs.style.display = showTabs ? "" : "none";
      const tab = showTabs ? cellFocusTab : (showText ? "text" : "image");
      if (els.cellFocusTabs) {
        els.cellFocusTabs.querySelectorAll(".gl-cft-btn").forEach(btn => {
          btn.classList.toggle("active", btn.dataset.tab === tab);
        });
      }
      if (els.cellEditTextCol) els.cellEditTextCol.style.display = (showText && tab === "text") ? "" : "none";
      if (els.cellEditImgCol)  els.cellEditImgCol.style.display  = (showImage && tab === "image") ? "" : "none";
      /* [신규] 캔버스 옆 포토샵풍 도구 레일도 이미지 탭이 실제로 보일
         때만 함께 보인다(텍스트만 편집 중일 땐 이미지 도구가 필요 없다). */
      if (els.cellToolRail) els.cellToolRail.style.display = (showImage && tab === "image") ? "" : "none";
    }
    if (els.cellFocusTabs) {
      els.cellFocusTabs.querySelectorAll(".gl-cft-btn").forEach(btn => {
        btn.addEventListener("click", () => {
          cellFocusTab = btn.dataset.tab;
          applyCellFocusTab();
        });
      });
    }

    /* [신규] "이 칸의 콘텐츠" 오버라이드 — 왼쪽 사이드바 전역 설정과
       다르게 이 칸만 텍스트/이미지/텍스트+이미지 중 하나를 쓰게 한다.
       전역과 같은 값을 고르면 오버라이드를 지워 다시 전역을 따르게
       한다(다른 칸별 오버라이드들과 같은 패턴 — "값이 없으면 전역"). */
    function renderCellFocusTypeSeg() {
      const idx = glCellEditIdx;
      if (!els.cellFocusTypeSeg || idx == null) return;
      const eff = glEffContentType(idx);
      const isOverridden = cellContentTypes[idx] != null;
      els.cellFocusTypeSeg.querySelectorAll(".gl-cft-btn").forEach(btn => {
        btn.classList.toggle("active", btn.dataset.ctype === eff);
      });
      if (els.cellFocusTypeHint) {
        els.cellFocusTypeHint.textContent = isOverridden
          ? "이 칸만 다르게 설정되어 있어요 (전역 설정: " + CONTENT_TYPE_LABEL[contentType] + ")"
          : "왼쪽 사이드바의 공통 설정(" + CONTENT_TYPE_LABEL[contentType] + ")을 따르는 중이에요";
      }
    }
    if (els.cellFocusTypeSeg) {
      els.cellFocusTypeSeg.querySelectorAll(".gl-cft-btn").forEach(btn => {
        btn.addEventListener("click", () => {
          const idx = glCellEditIdx;
          if (idx == null) return;
          const newType = btn.dataset.ctype;
          if (newType === glEffContentType(idx) && cellContentTypes[idx] == null) return; /* 변화 없음 */
          glPushUndo();
          if (newType === contentType) delete cellContentTypes[idx]; /* 전역과 같아지면 오버라이드 해제 */
          else cellContentTypes[idx] = newType;
          rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
          updateEditToolbar();
          renderCellFocusTypeSeg();
          renderCellFocusLayoutSeg();
          /* 콘텐츠 타입이 바뀌면 보여줄 탭(텍스트/이미지)도 함께 다시 계산 */
          cellFocusTab = (newType === "image") ? "image" : "text";
          applyCellFocusTab();
          refreshCellFocusPreview();
        });
      });
    }

    /* [신규] "이 칸의 텍스트+이미지 배치" 오버라이드 — renderCellFocusTypeSeg와
       완전히 같은 "값이 없으면 전역을 따른다" 패턴. "텍스트+이미지" 칸일
       때만 블록 자체를 보여준다(다른 콘텐츠 타입엔 배치 개념이 없으므로). */
    function renderCellFocusLayoutSeg() {
      const idx = glCellEditIdx;
      if (!els.cellFocusLayoutBlock || idx == null) return;
      const isBoth = glEffContentType(idx) === "both";
      els.cellFocusLayoutBlock.style.display = isBoth ? "" : "none";
      if (!isBoth || !els.cellFocusLayoutSeg) return;
      const eff = glEffContentLayout(idx);
      const isOverridden = cellContentLayouts[idx] != null;
      els.cellFocusLayoutSeg.innerHTML = LAYOUT_OPTIONS.map(l =>
        `<button type="button" class="gl-fit-btn${l.key === eff ? " active" : ""}" data-layout="${l.key}" title="${l.title}">${l.label}</button>`
      ).join("");
      els.cellFocusLayoutSeg.querySelectorAll(".gl-fit-btn").forEach(btn => {
        btn.addEventListener("click", () => {
          const newLayout = btn.dataset.layout;
          if (newLayout === glEffContentLayout(idx) && cellContentLayouts[idx] == null) return; /* 변화 없음 */
          glPushUndo();
          if (newLayout === contentLayout) delete cellContentLayouts[idx]; /* 전역과 같아지면 오버라이드 해제 */
          else cellContentLayouts[idx] = newLayout;
          rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
          renderCellFocusLayoutSeg();
          refreshCellFocusPreview();
        });
      });
      if (els.cellFocusLayoutHint) {
        els.cellFocusLayoutHint.textContent = isOverridden
          ? "이 칸만 다르게 설정되어 있어요 (전역 설정: " + LAYOUT_LABEL[contentLayout] + ")"
          : "왼쪽 사이드바의 공통 설정(" + LAYOUT_LABEL[contentLayout] + ")을 따르는 중이에요";
      }
    }

    function closeCellFocusPanel() {
      els.cellFocusPanel.style.display = "none";
      els.cellFocusPreview.innerHTML = "";
      glCellEditIdx = null;
    }

    /* [버그 수정: "취소" 버튼이 이미지 편집은 실제로 취소하지 않음]
       텍스트(폰트·크기·색상·스타일·정렬)는 "완료"를 눌러야 실제 상태에
       반영되는 임시 폼 값이라 "취소"를 누르면 자연히 그대로 사라진다.
       반면 이미지 쪽(회전·반전·자르기·보정·위치/크기, 그리고 이 칸의
       콘텐츠 유형)은 버튼을 누르는 즉시 실제 상태(cellImages 등)에
       바로 적용돼 왔다 — 그래서 "취소"를 눌러도 이미 적용된 이미지
       조작은 그대로 남아 있어 혼란을 줬다.
       해결책: 패널을 열 때(openCellFocusPanel) 이 칸의 이미지 관련
       상태를 스냅샷해 두고, "취소"(또는 ✕ 닫기·Esc — 모두 "완료"를
       거치지 않고 나가는 경로이므로 동일하게 취급)를 누르면 그
       스냅샷으로 되돌린다. 패널이 열려 있는 동안 이미지 조작으로
       실행취소 스택에 쌓인 항목도 이제는 "일어난 적 없는 일"이 되므로
       함께 제거해, Ctrl+Z를 눌렀을 때 이미 취소된 조작이 실행취소
       목록에 유령처럼 남아있지 않게 한다. */
    let cellFocusImgSnapshot = null;
    let cellFocusUndoDepthAtOpen = 0;
    function snapshotCellFocusImgState(idx) {
      cellFocusImgSnapshot = {
        image:       cellImages[idx],
        transform:   cellImgTransforms[idx],
        contentType: cellContentTypes[idx],
        table:       cellTables[idx], /* [신규] 표도 즉시 반영되므로 "취소" 시 되돌릴 수 있어야 함 */
      };
      cellFocusUndoDepthAtOpen = glUndoStack.length;
    }
    function cancelCellFocusPanel() {
      const idx = glCellEditIdx;
      if (idx != null && cellFocusImgSnapshot) {
        const snap = cellFocusImgSnapshot;
        if (snap.image === undefined) delete cellImages[idx]; else cellImages[idx] = snap.image;
        if (snap.transform === undefined) delete cellImgTransforms[idx]; else cellImgTransforms[idx] = snap.transform;
        if (snap.contentType === undefined) delete cellContentTypes[idx]; else cellContentTypes[idx] = snap.contentType;
        if (snap.table === undefined) delete cellTables[idx]; else cellTables[idx] = snap.table;
        /* 패널이 열려 있던 동안 이미지 조작(회전·반전·자르기·보정 등)으로
           쌓인 실행취소 항목은 이제 의미가 없으므로 그만큼만 잘라낸다
           (그 이전에 있던, 이 칸 편집과 무관한 실행취소 이력은 그대로 둔다). */
        if (glUndoStack.length > cellFocusUndoDepthAtOpen) glUndoStack.length = cellFocusUndoDepthAtOpen;
        const realCell = els.canvasScroll.querySelector('.gl-label-cell[data-idx="' + idx + '"]');
        if (realCell) rebuildCellImgFromState(realCell, idx);
        updateEditToolbar();
      }
      cellFocusImgSnapshot = null;
      closeCellFocusPanel();
    }


    function openCellFocusPanel(idx) {
      if (idx == null) return;
      glCellEditIdx = idx;
      snapshotCellFocusImgState(idx); /* [버그 수정] "취소" 시 되돌리기 위한 스냅샷 */
      selectCell(idx); /* 상단 툴바·선택 표시와 항상 같은 상태를 유지 */

      const text = cellTexts[idx] != null ? cellTexts[idx] : commonText;
      const fmt  = glEffTextFormat(idx);
      const al   = cellAligns[idx] || { h: textAlignH, v: textAlignV };

      if (els.cellEditTitle) els.cellEditTitle.textContent = (idx + 1) + "번 칸 개별 편집";
      if (els.cellEditText) els.cellEditText.value = text;
      if (els.cellEditFont) els.cellEditFont.value = fmt.fam;
      if (els.cellEditFontSize) setSelectByFloatValue(els.cellEditFontSize, fmt.fsz);
      if (els.cellEditColor) els.cellEditColor.value = fmt.col;
      setGlBtnPressed(els.cellEditBold, fmt.sty.bold);
      setGlBtnPressed(els.cellEditItalic, fmt.sty.italic);
      setGlBtnPressed(els.cellEditUnderline, fmt.sty.underline);
      glCellEditPendingAlign = { h: al.h, v: al.v };
      renderCellEditAlignGrid();
      renderCellTableEditor(); /* [신규] 이 칸에 표가 있으면 표 편집기를, 없으면 일반 텍스트 필드를 보여준다 */

      /* [수정] 텍스트/이미지를 칼럼으로 동시에 늘어놓는 대신 탭으로 나눠
         한 번에 하나만 보여준다 — 열 때마다 "텍스트" 탭부터 시작(둘 다
         있는 경우). 탭 바/칼럼 표시는 applyCellFocusTab()이 콘텐츠
         타입(텍스트/이미지/둘다)에 맞게 정리해준다. */
      cellFocusTab = "text";
      renderCellFocusTypeSeg();
      renderCellFocusLayoutSeg();
      applyCellFocusTab();

      els.cellFocusPanel.style.display = "flex";
      refreshCellFocusPreview();
      /* [수정] 예전엔 열릴 때마다 텍스트칸에 자동으로 포커스를 줘서,
         이미지만 만지려고 더블클릭했을 때도 키보드 포커스가 거기로
         가버리는 게 불편했다(자동 스크롤·모바일 키보드 팝업 등). 이제는
         자동 포커스를 주지 않고, 텍스트를 만지려면 탭이나 입력칸을
         직접 클릭하게 한다. */
    }
    window._glOpenCellFocusPanel = openCellFocusPanel; /* refreshCellFocusPreview 등 외부(이미지 콜백)에서 재사용 */
    /* [신규] Ctrl+Z/다시실행이 이 패널을 다시 채울 수 있게 훅 연결(맨 위
       glApplySnapshot 근처 주석 참고). openCellFocusPanel을 그대로 재사용하되,
       "텍스트"탭부터 시작하는 동작만은 사용자가 보던 탭을 그대로 유지하도록
       덮어쓴다(이미지 탭에서 회전 중 Ctrl+Z를 눌렀는데 텍스트탭으로 튀면
       어색하므로). */
    glRefreshCellFocusPanelHook = function () {
      if (glCellEditIdx == null || !els.cellFocusPanel || els.cellFocusPanel.style.display === "none") return;
      const keepTab = cellFocusTab;
      const idx = glCellEditIdx;
      openCellFocusPanel(idx);
      cellFocusTab = keepTab;
      applyCellFocusTab();
    };
    /* [버그 수정] 캔버스에서 표 칸에 타이핑할 때 사이드 패널의 표 그리드도
       같이 갱신되도록 훅 연결(위 glRefreshTableEditorGridHook 선언부 참고). */
    glRefreshTableEditorGridHook = function () { renderCellTableEditor(); };
    /* [신규] 캔버스에서 표 칸 클릭 시 선택 갱신 — Shift를 누르고 클릭하면
       마지막 클릭(glTblActive) 기준으로 범위를 넓히고, 아니면 그 칸 하나만
       선택한다. 사이드 패널 클릭 때와 완전히 같은 규칙(glTblComputeSel)을
       그대로 재사용해 "캔버스에서 선택 → 병합/분할/테두리/Ctrl+방향키
       크기조절"이 전부 사이드 패널과 똑같이 동작하게 한다. */
    glCanvasTableSelectHook = function (idx, r, c, shiftKey) {
      if (glCellEditIdx !== idx) return; /* "칸 개별 편집" 패널이 이 칸을 대상으로 열려 있을 때만 의미 있음 */
      const tbl = cellTables[idx];
      if (!tbl) return;
      const master = glTblMasterAt(tbl, r, c) || { r, c, cell: tbl.rows[r][c] };
      if (shiftKey && glTblActive) {
        glTblSel = glTblComputeSel(tbl, glTblActive, { r: master.r, c: master.c }) || glTblSel;
      } else {
        glTblActive = { r: master.r, c: master.c };
        glTblSel = { r1: master.r, c1: master.c, r2: master.r + master.cell.rowSpan - 1, c2: master.c + master.cell.colSpan - 1 };
      }
      glTblUpdateSelHighlight();
    };
    /* [버그 수정] refreshCellFocusPreview는 init() 안에서 선언되어 init() 스코프
       밖(예: applyCrop — 자르기 모달 코드는 init() 밖 최상위에 있다)에서는
       그냥 이름만으로 부를 수 없어 "refreshCellFocusPreview is not defined"
       런타임 에러가 났다. window._glOpenCellFocusPanel과 같은 방식으로
       window에 노출해, 자르기처럼 init() 밖에 있는 코드도 안전하게 호출할
       수 있게 한다. */
    window._glRefreshCellFocusPreview = refreshCellFocusPreview;

    /* 캔버스 위임 등록 — rebuildCanvas()가 칸 DOM을 통째로 교체해도
       리스너는 canvasScroll(부모)에 한 번만 걸려 있으므로 항상 살아있다. */
    els.canvasScroll.addEventListener("dblclick", (e) => {
      if (e.target.closest(".gl-cell-fsz-ctrl, .gl-cell-img-ctrl, .gl-img-handle")) return;
      const cellEl = e.target.closest(".gl-label-cell");
      if (!cellEl) return;
      e.preventDefault();
      if (document.activeElement && document.activeElement.blur) document.activeElement.blur(); /* 인라인 편집 중이던 내용을 먼저 저장 */
      openCellFocusPanel(Number(cellEl.dataset.idx));
    });

    if (els.cellEditCloseBtn) els.cellEditCloseBtn.addEventListener("click", cancelCellFocusPanel);
    if (els.cellEditCancelBtn) els.cellEditCancelBtn.addEventListener("click", cancelCellFocusPanel);
    document.addEventListener("keydown", (e) => {
      if (e.key === "Escape" && els.cellFocusPanel.style.display !== "none" && glCellEditIdx != null) cancelCellFocusPanel();
    });
    /* 창 크기가 바뀌면(반응형) 배율을 다시 계산 */
    window.addEventListener("resize", () => { if (glCellEditIdx != null) refreshCellFocusPreview(); });

    /* 텍스트·서식 필드는 입력하는 즉시 확대 미리보기에 반영(아직 저장 전) */
    if (els.cellEditText) els.cellEditText.addEventListener("input", refreshCellFocusPreview);
    if (els.cellEditFont) els.cellEditFont.addEventListener("change", refreshCellFocusPreview);
    if (els.cellEditFontSize) els.cellEditFontSize.addEventListener("change", refreshCellFocusPreview);
    if (els.cellEditColor) els.cellEditColor.addEventListener("input", refreshCellFocusPreview);
    [els.cellEditBold, els.cellEditItalic, els.cellEditUnderline].forEach(btn => {
      if (!btn) return;
      btn.addEventListener("click", () => { setGlBtnPressed(btn, !isGlBtnPressed(btn)); refreshCellFocusPreview(); });
    });

    if (els.cellEditImgReplaceBtn) {
      els.cellEditImgReplaceBtn.addEventListener("click", () => {
        if (glCellEditIdx == null || !els.cellImageInput) return;
        els.cellImageInput.click(); /* selectCell()로 selectedIdx가 이미 이 칸과 같아 기존 change 핸들러가 그대로 동작 */
      });
    }
    /* [신규] 탭 본문의 큼직한 "이미지 불러오기" 버튼 — 새 로직 없이 왼쪽
       도구 모음의 기존 버튼을 그대로 클릭 전달만 한다(항상 같은 동작 보장). */
    if (els.cellEditImgLoadBtn) {
      els.cellEditImgLoadBtn.addEventListener("click", () => {
        if (els.cellEditImgReplaceBtn) els.cellEditImgReplaceBtn.click();
      });
    }
    if (els.cellEditImgClearBtn) {
      els.cellEditImgClearBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        if (idx == null) return;
        if (cellImages[idx] || cellImgTransforms[idx]) glPushUndo();
        delete cellImages[idx];
        delete cellImgTransforms[idx];
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        updateEditToolbar();
        refreshCellFocusPreview();
      });
    }

    /* [신규] 이미지 탭 — 회전·반전·자르기·위치초기화. 상단 편집 툴바
       (gl-edit-img-rotate/crop/reset)와 완전히 같은 동작(rotateCellImg·
       flipCellImg)을 재사용한다. 두 함수 모두 내부에서 실제 그리드 칸과
       (열려 있다면) 확대 미리보기를 항상 함께 갱신하므로(syncCellImgVisual)
       버튼 핸들러에서 따로 다시 그릴 필요가 없다. */
    if (els.cellEditImgRotateBtn) {
      els.cellEditImgRotateBtn.addEventListener("click", () => {
        if (glCellEditIdx == null) return;
        rotateCellImg(glCellEditIdx, 90);
      });
    }
    if (els.cellEditImgFlipXBtn) {
      els.cellEditImgFlipXBtn.addEventListener("click", () => {
        if (glCellEditIdx == null) return;
        flipCellImg(glCellEditIdx, "x");
      });
    }
    if (els.cellEditImgFlipYBtn) {
      els.cellEditImgFlipYBtn.addEventListener("click", () => {
        if (glCellEditIdx == null) return;
        flipCellImg(glCellEditIdx, "y");
      });
    }
    if (els.cellEditImgCropBtn) {
      els.cellEditImgCropBtn.addEventListener("click", () => {
        if (glCellEditIdx == null) return;
        openCropModal(glCellEditIdx); /* 자르기 모달이 닫힐 때(applyCrop) 미리보기도 함께 갱신됨 */
      });
    }
    if (els.cellEditImgPosResetBtn) {
      els.cellEditImgPosResetBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        if (idx == null) return;
        if (cellImgTransforms[idx]) glPushUndo();
        /* [신규] 보정(밝기·대비·채도·흑백)은 전용 "보정 초기화" 버튼이 따로 있으므로
           이 버튼(위치·크기·회전·자르기·반전 초기화)에서는 건드리지 않고 남겨둔다. */
        const filt = cellImgTransforms[idx] && cellImgTransforms[idx].filter;
        if (filt) cellImgTransforms[idx] = { scaleX: 1, scaleY: 1, x: 0, y: 0, filter: filt };
        else delete cellImgTransforms[idx];
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        updateEditToolbar();
        refreshCellFocusPreview();
      });
    }

    /* [신규] 보정(밝기·대비·채도·흑백·불투명도·색상반전) 슬라이더 6종 —
       움직이는 즉시 실제 캔버스 칸과 확대 미리보기 양쪽에 반영한다
       (setCellImgFilter가 둘 다 처리). 불투명도는 최소 10%까지만 허용해
       완전히 안 보이게 만들어버리는 실수를 방지한다. */
    [
      [els.cellEditImgBrightness, "brightness", "gl-cell-edit-img-brightness-val"],
      [els.cellEditImgContrast,   "contrast",   "gl-cell-edit-img-contrast-val"],
      [els.cellEditImgSaturate,   "saturate",   "gl-cell-edit-img-saturate-val"],
      [els.cellEditImgGrayscale,  "grayscale",  "gl-cell-edit-img-grayscale-val"],
      [els.cellEditImgOpacity,    "opacity",    "gl-cell-edit-img-opacity-val"],
      [els.cellEditImgInvert,     "invert",     "gl-cell-edit-img-invert-val"],
    ].forEach(([sliderEl, key, valLabelId]) => {
      if (!sliderEl) return;
      sliderEl.addEventListener("input", () => {
        const idx = glCellEditIdx;
        if (idx == null) return;
        const value = Number(sliderEl.value);
        const lbl = document.getElementById(valLabelId);
        if (lbl) lbl.textContent = value + "%";
        setCellImgFilter(idx, key, value);
      });
    });
    if (els.cellEditImgFilterResetBtn) {
      els.cellEditImgFilterResetBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        if (idx == null) return;
        resetCellImgFilter(idx);
        setCellFilterSliderUI(els.cellEditImgBrightness, "gl-cell-edit-img-brightness-val", 100, "%");
        setCellFilterSliderUI(els.cellEditImgContrast,   "gl-cell-edit-img-contrast-val",   100, "%");
        setCellFilterSliderUI(els.cellEditImgSaturate,   "gl-cell-edit-img-saturate-val",   100, "%");
        setCellFilterSliderUI(els.cellEditImgGrayscale,  "gl-cell-edit-img-grayscale-val",  0,   "%");
        setCellFilterSliderUI(els.cellEditImgOpacity,    "gl-cell-edit-img-opacity-val",    100, "%");
        setCellFilterSliderUI(els.cellEditImgInvert,     "gl-cell-edit-img-invert-val",     0,   "%");
      });
    }

    if (els.cellEditResetBtn) {
      els.cellEditResetBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        if (idx == null) return;
        resetCellFully(idx);
        closeCellFocusPanel();
        showToast("↺ " + (idx + 1) + "번 칸을 공통 설정으로 되돌렸습니다.", "success", 1600);
      });
    }

    if (els.cellEditSaveBtn) {
      els.cellEditSaveBtn.addEventListener("click", () => {
        const idx = glCellEditIdx;
        if (idx == null) return;
        const showText  = glEffContentType(idx) === "text" || glEffContentType(idx) === "both";
        if (showText && els.cellEditText) {
          glPushUndo();
          cellTexts[idx]        = els.cellEditText.value;
          cellFontFamilies[idx] = els.cellEditFont.value;
          cellFontSizes[idx]    = parseFloat(els.cellEditFontSize.value);
          cellColors[idx]       = els.cellEditColor.value;
          cellStyles[idx]       = {
            bold:      isGlBtnPressed(els.cellEditBold),
            italic:    isGlBtnPressed(els.cellEditItalic),
            underline: isGlBtnPressed(els.cellEditUnderline),
          };
          cellAligns[idx] = Object.assign({}, glCellEditPendingAlign);
          rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
          updateEditToolbar();
        }
        closeCellFocusPanel();
        showToast("✅ " + (idx + 1) + "번 칸에 저장했습니다.", "success", 1500);
      });
    }
  }

  if (els.cellImageInput) {
      els.cellImageInput.addEventListener("change", (e) => {
        const file = e.target.files && e.target.files[0];
        e.target.value = ""; /* 같은 파일을 다시 골라도 change가 발생하도록 초기화 */
        applyImageFileToCell(file, selectedIdx);
      });
    }
    if (els.editImgClearBtn) {
      els.editImgClearBtn.addEventListener("click", () => {
        if (selectedIdx == null) return;
        if (cellImages[selectedIdx] || cellImgTransforms[selectedIdx]) glPushUndo();
        delete cellImages[selectedIdx];
        delete cellImgTransforms[selectedIdx];
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + selectedIdx + '"]'), selectedIdx);
        updateEditToolbar();
      });
    }
    if (els.editTextClearBtn) {
      els.editTextClearBtn.addEventListener("click", () => {
        if (selectedIdx == null) return;
        if (cellTexts[selectedIdx] != null || cellFontSizes[selectedIdx] != null || cellTables[selectedIdx] != null) glPushUndo();
        delete cellTexts[selectedIdx];
        delete cellFontSizes[selectedIdx];
        delete cellTables[selectedIdx];
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + selectedIdx + '"]'), selectedIdx);
      });
    }
    /* [신규] 표 빠른 도구 — "행 추가"/"열 추가"/"표 삭제"는 특정 칸을
       선택할 필요 없이 표 구조 자체에 적용되는 단순한 조작이라 캔버스에서
       바로 처리한다(정밀한 병합·분할·테두리 조작은 칸을 더블클릭해
       확대 편집 화면에서 한다). */
    if (els.editTableAddRowBtn) {
      els.editTableAddRowBtn.addEventListener("click", () => {
        const idx = selectedIdx;
        const raw = idx != null && cellTables[idx];
        if (!raw) return;
        glPushUndo();
        const fresh = glTblNormalize(raw);
        glTblInsertRow(fresh, fresh.rows.length);
        cellTables[idx] = fresh;
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        if (typeof glRefreshTableEditorGridHook === "function") glRefreshTableEditorGridHook();
        if (typeof window._glRefreshCellFocusPreview === "function") window._glRefreshCellFocusPreview();
      });
    }
    if (els.editTableAddColBtn) {
      els.editTableAddColBtn.addEventListener("click", () => {
        const idx = selectedIdx;
        const raw = idx != null && cellTables[idx];
        if (!raw) return;
        glPushUndo();
        const fresh = glTblNormalize(raw);
        glTblInsertCol(fresh, fresh.rows[0] ? fresh.rows[0].length : 0);
        cellTables[idx] = fresh;
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        if (typeof glRefreshTableEditorGridHook === "function") glRefreshTableEditorGridHook();
        if (typeof window._glRefreshCellFocusPreview === "function") window._glRefreshCellFocusPreview();
      });
    }
    if (els.editTableDeleteBtn) {
      els.editTableDeleteBtn.addEventListener("click", () => {
        const idx = selectedIdx;
        if (idx == null || !cellTables[idx]) return;
        if (!confirm("이 칸의 표를 지우고 일반 텍스트 칸으로 되돌릴까요?")) return;
        glPushUndo();
        delete cellTables[idx];
        rebuildCellImgFromState(document.querySelector('.gl-label-cell[data-idx="' + idx + '"]'), idx);
        updateEditToolbar();
        if (typeof window._glRefreshCellFocusPreview === "function") window._glRefreshCellFocusPreview();
      });
    }
    if (els.editFillOthersBtn) {
      els.editFillOthersBtn.addEventListener("click", () => {
        if (selectedIdx != null) fillOtherCellsFromCell(selectedIdx);
      });
    }
    if (els.editToolbarCloseBtn) {
      els.editToolbarCloseBtn.addEventListener("click", deselectCell);
    }

    /* 신규: 칸별 글자 크기 미니 컨트롤(−/＋/↺) — canvasScroll에 위임 등록.
       rebuildCanvas()가 내부 페이지 DOM을 교체해도 canvasScroll 요소 자체는
       그대로이므로 한 번만 등록하면 계속 동작한다. */
    els.canvasScroll.addEventListener("mousedown", onCanvasMouseDown);
    els.canvasScroll.addEventListener("touchstart", onCanvasMouseDown, { passive: false }); /* [신규] 터치 지원 — 9점 핸들·자유 회전·이미지 드래그를 터치스크린에서도 시작할 수 있도록 */
    /* [신규] 칸 개별 편집 패널의 확대 미리보기(사본)에도 같은 핸들러를
       등록 — 9점 핸들 드래그·이미지 이동이 거기서도 동일하게 동작하게
       하기 위함(버튼은 selectCell 등 idx만 보고 동작하므로 안전). */
    if (els.cellFocusPreview) {
      els.cellFocusPreview.addEventListener("mousedown", onCanvasMouseDown);
      els.cellFocusPreview.addEventListener("touchstart", onCanvasMouseDown, { passive: false }); /* [신규] 터치 지원 */
    }

    /* 신규: 이미지 칸 우클릭 — 빠른 편집 메뉴(자르기·초기화·이미지 교체) */
    els.canvasScroll.addEventListener("contextmenu", onCanvasContextMenu);

    /* 신규: 라벨 칸에 이미지 파일을 직접 끌어다 놓으면(드래그앤드롭) 그 칸의
       개별 이미지로 자동 인식해 넣어준다. 파일 탐색기·바탕화면 등에서 이미지
       파일을 캔버스의 라벨 칸 위로 드래그해서 놓기만 하면 된다.
       canvasScroll에 위임 등록해 rebuildCanvas로 페이지가 교체돼도 계속 동작한다. */
    let glDropTargetEl = null;
    function clearGlDropTarget() {
      if (glDropTargetEl) { glDropTargetEl.classList.remove("gl-drop-target"); glDropTargetEl = null; }
    }
    els.canvasScroll.addEventListener("dragover", (e) => {
      const cellEl = e.target.closest(".gl-label-cell");
      if (!cellEl || !(e.dataTransfer && Array.from(e.dataTransfer.types || []).includes("Files"))) {
        clearGlDropTarget();
        return;
      }
      e.preventDefault(); /* 캔버스 위 드롭을 허용(기본은 브라우저가 파일을 새 탭으로 여는 등 막아버림) */
      e.dataTransfer.dropEffect = "copy";
      if (glDropTargetEl !== cellEl) {
        clearGlDropTarget();
        glDropTargetEl = cellEl;
        cellEl.classList.add("gl-drop-target");
      }
    });
    els.canvasScroll.addEventListener("dragleave", (e) => {
      if (glDropTargetEl && (e.target === glDropTargetEl || !els.canvasScroll.contains(e.relatedTarget))) clearGlDropTarget();
    });
    els.canvasScroll.addEventListener("drop", (e) => {
      const cellEl = e.target.closest(".gl-label-cell");
      clearGlDropTarget();
      if (!cellEl) return;
      const file = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
      if (!file) return;
      e.preventDefault();
      const idx = Number(cellEl.dataset.idx);
      const imgModeOn = contentType === "image" || contentType === "both";
      if (!imgModeOn) {
        showToast("먼저 좌측에서 '이미지' 또는 '텍스트+이미지' 모드를 선택하세요.", "info", 2200);
        return;
      }
      selectCell(idx); /* 드롭한 칸을 선택 상태로 만들어 편집 툴바가 그 칸을 가리키게 함 */
      applyImageFileToCell(file, idx);
    });

    /* [신규] 칸 개별 편집(포커스) 패널에도 이미지 파일을 드래그앤드롭으로
       바로 넣을 수 있게 한다 — 지금까지는 "이미지 교체" 버튼으로 파일
       탐색기를 여는 방법뿐이었다. 캔버스의 드래그앤드롭과 완전히 같은
       패턴이며, 패널이 열려 있는 한 그 칸(glCellEditIdx)이 대상이다. */
    if (els.cellFocusPanel) {
      els.cellFocusPanel.addEventListener("dragover", (e) => {
        if (glCellEditIdx == null || !(e.dataTransfer && Array.from(e.dataTransfer.types || []).includes("Files"))) return;
        e.preventDefault(); /* 유형 판정과 무관하게 항상 기본 동작(새 탭에서 열기 등)은 막는다 */
        e.dataTransfer.dropEffect = "copy";
        els.cellFocusPanel.classList.add("gl-drop-target");
      });
      els.cellFocusPanel.addEventListener("dragleave", (e) => {
        if (!els.cellFocusPanel.contains(e.relatedTarget)) els.cellFocusPanel.classList.remove("gl-drop-target");
      });
      els.cellFocusPanel.addEventListener("drop", (e) => {
        els.cellFocusPanel.classList.remove("gl-drop-target");
        if (glCellEditIdx == null) return;
        const file = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
        if (!file) return;
        e.preventDefault();
        const ct = glEffContentType(glCellEditIdx);
        if (ct !== "image" && ct !== "both") {
          showToast("이 칸의 콘텐츠 유형을 '이미지' 또는 '텍스트+이미지'로 먼저 바꿔주세요.", "info", 2200);
          return;
        }
        applyImageFileToCell(file, glCellEditIdx);
      });
    }

    /* 신규: 일반 라벨 칸 우클릭 메뉴 — 항목 클릭 처리 + 바깥 클릭/ESC/스크롤로 닫기
       (주소 라벨 컨텍스트 메뉴와 동일한 상호작용 패턴) */
    if (els.glCtxMenu) {
      /* 접근성: index.js에 정의된 공용 방향키 탐색 헬퍼 재사용
         (index.js가 general-labels.js보다 먼저 로드되므로 전역에서 사용 가능) */
      if (typeof attachCtxMenuKeyboardNav === "function") attachCtxMenuKeyboardNav(els.glCtxMenu);
      els.glCtxMenu.addEventListener("mousedown", e => e.stopPropagation());
      els.glCtxMenu.addEventListener("click", (e) => {
        const btn = e.target.closest(".ctx-item");
        const idx = glCtxIdx;
        const batchMode = glCtxBatchMode;
        closeGlCtxMenu();
        if (!btn || btn.hasAttribute("disabled")) return;
        /* [신규] 다중 선택 일괄 작업 메뉴에서 눌렀을 때 — idx 하나가 아니라
           multiSelectedIdxs 전체를 대상으로 한다. */
        if (batchMode) {
          switch (btn.dataset.action) {
            case "batch-reset":      resetMultiSelectedCells(); break;
            case "batch-clear-text": clearTextMultiSelectedCells(); break;
            case "batch-clear-img":  clearImageMultiSelectedCells(); break;
          }
          return;
        }
        if (idx == null) return;
        const cellSel = '.gl-label-cell[data-idx="' + idx + '"]';
        switch (btn.dataset.action) {
          case "crop":
            openCropModal(idx);
            break;
          case "reset-img":
            if (cellImgTransforms[idx]) glPushUndo();
            delete cellImgTransforms[idx];
            rebuildCellImgFromState(document.querySelector(cellSel), idx);
            if (selectedIdx === idx) updateEditToolbar();
            break;
          case "replace-cell":
            selectCell(idx);
            if (els.cellImageInput) els.cellImageInput.click();
            break;
          case "clear-img":
            if (cellImages[idx] || cellImgTransforms[idx]) glPushUndo();
            delete cellImages[idx];
            delete cellImgTransforms[idx];
            rebuildCellImgFromState(document.querySelector(cellSel), idx);
            if (selectedIdx === idx) updateEditToolbar();
            break;
          case "replace-all":
            forceImmediateApply = true; /* 이 메뉴는 "지금 바로 전체 교체"가 목적이므로 대기 없이 즉시 적용 */
            els.imageInput.click();
            break;
          case "fill-all-text":
            /* [신규] 사이드바 "전체 칸에 적용"과 완전히 같은 로직(fillAllWithCommonText)을
               그대로 쓰되, 출처만 사이드바 입력칸이 아니라 방금 우클릭한 칸으로 바꾼다. */
            if (els.textarea) els.textarea.value = cellTexts[idx] != null ? cellTexts[idx] : commonText;
            fillAllWithCommonText();
            showToast("✅ 이 칸 내용을 전체 칸에 채웠습니다.", "success", 1500);
            break;
          case "copy":
            glCellClipboard = glCellSnapshot(idx);
            showToast("📋 칸 내용을 복사했습니다.", "success", 1500);
            break;
          case "paste":
            glPushUndo();
            glApplyCellSnapshot(idx, glCellClipboard);
            showToast("📋 붙여넣었습니다.", "success", 1500);
            break;
          case "fill-empty-cells":
            fillEmptyCellsWithCellSnapshot(idx);
            break;
          case "clear-text":
            if (cellTexts[idx] != null || cellFontSizes[idx] != null || cellTables[idx] != null) glPushUndo();
            delete cellTexts[idx];
            delete cellFontSizes[idx];
            delete cellTables[idx];
            rebuildCellImgFromState(document.querySelector(cellSel), idx);
            break;
          case "reset-cell":
            resetCellFully(idx);
            break;
        }
      });
      document.addEventListener("click", e => {
        if (els.glCtxMenu.classList.contains("ctx-open") && !els.glCtxMenu.contains(e.target)) closeGlCtxMenu();
      });
      document.addEventListener("keydown", e => {
        if (e.key === "Escape" && els.glCtxMenu.classList.contains("ctx-open")) closeGlCtxMenu();
      }, true);
      document.addEventListener("scroll", () => {
        if (els.glCtxMenu.classList.contains("ctx-open")) closeGlCtxMenu();
      }, true);
    }

    /* 신규: 이미지 자르기 모달 1회 생성 */
    buildCropModal();

    /* 신규: 일반라벨 모드 전용 키보드 단축키
       주소라벨 모드의 전역 단축키와 겹치지 않도록 mode-general일 때만 동작하며,
       입력 중(contentEditable/입력창)에는 무시해 타이핑을 방해하지 않는다. */
    document.addEventListener("keydown", (e) => {
      if (!document.body.classList.contains("mode-general")) return;
      /* [수정] 원래는 INPUT/TEXTAREA/SELECT/contentEditable이면 무조건 "입력 중"으로
         보고 우리 되돌리기를 건너뛰었는데, 이러면 밝기·대비·채도·흑백 슬라이더
         (<input type="range">)나 글자 색상(<input type="color">)을 마지막으로
         만진 뒤에는 그 요소가 계속 포커스를 갖고 있어서 Ctrl+Z가 아예 먹통이
         됐다(range/color는 브라우저 자체 되돌리기가 없는 컨트롤이라 아무 일도
         안 일어남). 실제로 "타이핑 중"이라 브라우저 기본 되돌리기에 맡겨야 하는
         경우(텍스트류 입력창·textarea·contentEditable)만 걸러내고, 슬라이더·색상
         picker처럼 텍스트가 아닌 컨트롤은 우리 되돌리기가 그대로 동작하게 한다. */
      const ed = isGlFreeTextEditTarget(e.target);
      /* [버그 수정] 실행취소/다시실행은 "언제든 눌러도 되는 안전망"이라
         입력 칸에 커서가 있어도 항상 동작해야 한다(주소라벨과 동일한
         원칙). 다만 자르기 모달이나 칸 개별 편집 패널처럼 그 자체로
         독립된 편집 세션(자체 취소/완료 버튼이 있음)이 열려 있을 때는
         예외로 둔다 — 그 화면들 뒤에서 전체 상태가 바뀌면 오히려
         혼란스럽다. */
      const inGlOverlay = (cropEls && cropEls.bg && cropEls.bg.classList.contains("active")) || glCellEditIdx != null;
      /* 신규: ESC로 선택된 칸 해제(자르기 모달이 열려있을 땐 모달이 자체적으로 Escape를 처리하므로 건드리지 않음) */
      if (e.key === "Escape") {
        if (cropEls && cropEls.bg.classList.contains("active")) return;
        if (selectedIdx != null) { deselectCell(); return; }
      }
      if ((e.ctrlKey || e.metaKey) && e.key === "p") {
        e.preventDefault(); printGeneral(); return;
      }
      /* 신규: 되돌리기(Ctrl+Z) — [버그 수정: "글자 입력하고 나서 Ctrl+Z가
         안 먹어요"] 텍스트류 입력창에 커서가 남아 있어도(모달/개별편집
         패널 안이 아닌 한) 항상 동작하게 하고, 편집 중이던 내용은 먼저
         blur시켜 커밋한 뒤 되돌린다. */
      if (!inGlOverlay && (e.ctrlKey || e.metaKey) && !e.shiftKey && e.key === "z") {
        e.preventDefault();
        if (ed && document.activeElement && typeof document.activeElement.blur === "function") document.activeElement.blur();
        glUndo();
        return;
      }
      /* [신규] 다시 실행 — Ctrl+Shift+Z / Ctrl+Y (주소라벨과 동일한 관례) */
      if (!inGlOverlay && (e.ctrlKey || e.metaKey) && ((e.shiftKey && e.key === "Z") || (!e.shiftKey && e.key === "y"))) {
        e.preventDefault();
        if (ed && document.activeElement && typeof document.activeElement.blur === "function") document.activeElement.blur();
        glRedo();
        return;
      }
      if (!ed && (e.ctrlKey || e.metaKey) && (e.key === "[" || e.key === "]")) {
        e.preventDefault(); stepGlFontSize(e.key === "[" ? -1 : 1); return;
      }
      if (!ed && (e.ctrlKey || e.metaKey) && (e.key === "-" || e.key === "=" || e.key === "+")) {
        e.preventDefault(); stepGlImageScale(e.key === "-" ? -1 : 1); return;
      }
      /* [신규] 방향키 이미지 넛지 — 1px(Shift+방향키는 10px). Ctrl/Alt와
         함께 눌렸다면(브라우저·OS 자체 단축키일 수 있음) 건드리지 않는다.
         nudgeSelectedCellImg가 대상(선택된 칸에 이미지)이 없으면 false를
         돌려주므로, 그럴 땐 preventDefault하지 않고 방향키의 원래 동작
         (페이지 스크롤 등)을 그대로 둔다. */
      if (!ed && !e.ctrlKey && !e.metaKey && !e.altKey &&
          (e.key === "ArrowUp" || e.key === "ArrowDown" || e.key === "ArrowLeft" || e.key === "ArrowRight")) {
        const step = e.shiftKey ? 10 : 1;
        const dx = e.key === "ArrowLeft" ? -step : e.key === "ArrowRight" ? step : 0;
        const dy = e.key === "ArrowUp" ? -step : e.key === "ArrowDown" ? step : 0;
        if (nudgeSelectedCellImg(dx, dy)) { e.preventDefault(); return; }
      }
    });

    window.addEventListener("beforeprint", () => {
      if (!document.body.classList.contains("mode-general")) return;
      document.querySelectorAll(".gl-label-cell.gl-empty").forEach(el => { el.textContent = ""; });
    });
    window.addEventListener("afterprint", () => {
      if (!document.body.classList.contains("mode-general")) return;
      rebuildCanvas();
    });

    /* 주소라벨 미리보기 초기화 */
    initAddressPreview();

    /* 단축키/사용법 모달 탭 전환 초기화 */
    initShortcutTabs();

    /* canvas-scroll 스크롤 시 미리보기 업데이트 */
    const canvasScroll = document.getElementById("canvas-scroll");
    if (canvasScroll) {
      canvasScroll.addEventListener("scroll", () => {
        if (!document.body.classList.contains("mode-general")) {
          renderAddrGridPreview();
        }
      }, { passive: true });
    }

    /* 주기적으로 주소라벨 미리보기 동기화 (라벨 편집 반영용) */
    setInterval(() => {
      if (!document.body.classList.contains("mode-general")) {
        renderAddrGridPreview();
      }
    }, 1500);
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }

  /* ══════════════════════════════════════════════════════════════
     일반라벨 3단계 마법사 컨트롤러
     ── Step 2(라벨 디자인)는 기존 #general-app-body를 그대로 재사용한다.
        Step 1(규격 선택)·3(확인 및 인쇄)만 새로 만들되, 실제 동작은
        전부 이미 있는 함수(selectTemplate·rebuildCanvas·selectCell·
        renderGlGridPreview·기존 인쇄 버튼)를 그대로 호출해 재사용한다.
        새로 계산/재구현한 로직은 없다 — 화면 전환과 배선만 새로 만들었다.
     [정리] 원래 4단계(규격/디자인/전체확인/인쇄)였으나, 전체확인과
     인쇄가 같은 시트 미리보기를 두 번 보여줄 뿐이라 하나로 합쳐
     3단계(규격/디자인/확인 및 인쇄)로 줄였다. */
  const glStepPanels = {
    spec:   document.getElementById("gl-step-view-spec"),
    design: document.getElementById("general-app-body"),
    review: document.getElementById("gl-step-view-review"),
  };
  const glStepTabs = {
    spec:   document.getElementById("gl-step-tab-spec"),
    design: document.getElementById("gl-step-tab-design"),
    review: document.getElementById("gl-step-tab-review"),
  };
  const GL_STEP_ORDER = ["spec", "design", "review"];
  let glWizardStep = "spec";

  /* [신규] 아직 규격(용지)을 고르지 않았으면 "라벨 디자인"/"확인 및 인쇄" 탭을
     막아둔다 — 이전에는 언제든 클릭할 수 있어서, 맨 처음 화면(규격 선택)에서
     바로 옆의 "라벨 디자인" 탭을 눌러 아무 내용도 없는 빈 화면으로 넘어가
     버리는 문제가 있었다. */
  function updateGlStepTabsGating() {
    const ready = !!currentTplKey;
    [glStepTabs.design, glStepTabs.review].forEach(btn => {
      if (!btn) return;
      btn.disabled = !ready;
      btn.title = ready ? "" : "먼저 라벨 규격을 선택하세요";
    });
  }

  function setGlWizardStep(step) {
    if (!glStepPanels[step]) return;
    glWizardStep = step;
    GL_STEP_ORDER.forEach(s => {
      glStepPanels[s]?.classList.toggle("active", s === step);
      glStepTabs[s]?.classList.toggle("active", s === step);
      glStepTabs[s]?.classList.toggle("done", GL_STEP_ORDER.indexOf(s) < GL_STEP_ORDER.indexOf(step));
    });
    if (step === "spec")   { renderGlSpecMakerTabs(); renderGlSpecList(); renderGlSpecPreview(); }
    if (step === "review") renderGlReviewPreview();
    /* [버그 수정: "이전 작업 이어하기를 눌러도 화면이 안 바뀜"]
       "라벨 디자인" 스텝(캔버스)은 규격 선택 스텝에 있는 동안엔
       display:none으로 숨어 있다. 그런데 이전 작업 복원(또는 규격을
       고르고 "다음"으로 넘어오는 일반적인 흐름)에서 rebuildCanvas()가
       그 "숨어 있는 동안"에 먼저 실행되면, 캔버스 크기 계산(el.
       clientWidth/clientHeight 기반 "한 페이지" 축소 배율 등)이 전부
       0을 기준으로 계산돼 사실상 아무것도 안 보이는 것처럼 나온다 —
       나중에 이 스텝이 실제로 화면에 보이게 바뀌어도 그 잘못된 계산
       결과가 자동으로 다시 계산되지 않았다. → 이 스텝으로 들어올
       때마다(=display:none이 풀려 실제 크기를 알 수 있게 될 때마다)
       캔버스 보기모드를 다시 계산한다. */
    if (step === "design" && typeof applyGlViewMode === "function") {
      requestAnimationFrame(() => applyGlViewMode());
    }
  }
  window._glGoToStep = setGlWizardStep; /* 다른 스크립트(ui-refresh.js 등)에서도 필요하면 쓸 수 있게 노출 */
  window._glCurrentWizardStep = () => glWizardStep; /* [버그 수정] index.js의 탭 전환 로직이 "지금 어느 스텝인지" 물어볼 수 있게 노출 */

  Object.keys(glStepTabs).forEach(key => {
    glStepTabs[key]?.addEventListener("click", () => setGlWizardStep(key));
  });

  /* ── Step 1: 규격 선택 ─────────────────────────────────────────── */
  let glSpecMakerFilter = "전체";
  let glSpecSearchKw = "";

  function renderGlSpecMakerTabs() {
    const wrap = document.getElementById("gl-spec-maker-tabs");
    if (!wrap) return;
    const makers = Array.from(new Set(Object.values(CFG).map(v => v.maker)));
    const orderedMakers = MAKER_ORDER.filter(m => makers.includes(m)).concat(makers.filter(m => !MAKER_ORDER.includes(m)));
    const tabs = ["전체", ...orderedMakers];
    wrap.innerHTML = tabs.map(m =>
      `<button type="button" class="gl-spec-maker-tab${m === glSpecMakerFilter ? " active" : ""}" data-maker="${escHtml(m)}">${escHtml(m.replace(/\s*\(.*?\)/, ""))}</button>`
    ).join("");
    wrap.querySelectorAll(".gl-spec-maker-tab").forEach(btn => {
      btn.addEventListener("click", () => {
        glSpecMakerFilter = btn.dataset.maker;
        renderGlSpecMakerTabs();
        renderGlSpecList();
      });
    });
  }

  function renderGlSpecList() {
    const list = document.getElementById("gl-spec-list");
    if (!list) return;
    const kw = glSpecSearchKw.trim().toLowerCase();
    /* [신규] 검색어도 없고 제조사 필터도 "전체"인 기본 화면일 때만
       "최근 사용"을 보여준다 — 필터링 중에는 관련 없는 최근 항목이
       섞여 보이면 오히려 헷갈리므로. */
    const showRecent = !kw && glSpecMakerFilter === "전체";
    const recentKeys = showRecent ? getGlRecentTemplates().filter(k => CFG[k]) : [];
    const recentSet = new Set(recentKeys);

    const entries = Object.entries(CFG).filter(([key, v]) => {
      if (recentSet.has(key)) return false; /* 최근 사용 섹션과 중복 노출 방지 */
      if (glSpecMakerFilter !== "전체" && v.maker !== glSpecMakerFilter) return false;
      if (kw && !`${v.maker} ${v.model}`.toLowerCase().includes(kw)) return false;
      return true;
    }).sort((a, b) => a[1].perPage - b[1].perPage);

    if (!entries.length && !recentKeys.length) {
      list.innerHTML = `<div class="gl-spec-list-empty">일치하는 규격이 없습니다. 검색어나 제조사 필터를 확인해주세요.</div>`;
      return;
    }
    const rowHtml = (key, v) => `
      <button type="button" class="gl-spec-row${key === currentTplKey ? " active" : ""}" data-key="${key}">
        <span class="gl-spec-radio"></span>
        <span class="gl-spec-row-main"><b>${escHtml(v.model)}</b><span class="gl-spec-row-sub">${escHtml(v.maker)}</span></span>
        <span class="gl-spec-row-meta">${v.colN}×${v.rowN}칸 · ${v.labelW}×${v.labelH}mm</span>
      </button>
    `;
    let html = "";
    if (recentKeys.length) {
      html += `<div class="gl-spec-list-group-label">⏱ 최근 사용</div>` +
        recentKeys.map(key => rowHtml(key, CFG[key])).join("") +
        `<div class="gl-spec-list-divider"></div>`;
    }
    html += entries.map(([key, v]) => rowHtml(key, v)).join("");
    list.innerHTML = html;
    list.querySelectorAll(".gl-spec-row").forEach(row => {
      row.addEventListener("click", () => {
        selectTemplate(row.dataset.key); /* 기존 규격 변경 로직 그대로 재사용(최근 사용 기록도 여기서 함께 저장됨) */
        renderGlSpecList();
        renderGlSpecPreview();
      });
    });
    syncGlQuickTemplateCards();
  }

  /* [신규] "자주 쓰는 라벨지" 카드 — 주소라벨 1단계 카드와 같은 방식으로
     실제 colN×rowN 그대로 미니 그리드 스와치를 그린다(populateTemplateCardShapes와
     동일한 계산이지만, 데이터소스가 CFG(GENERAL_TEMPLATE_CONFIGS)라 별도 함수로 둔다). */
  function populateGlTemplateCardShapes() {
    document.querySelectorAll("#gl-quick-template-cards .wstep-tpl-shape[id^='gl-tpl-shape-']").forEach(shapeEl => {
      const key = shapeEl.id.replace("gl-tpl-shape-", "");
      const c = CFG[key];
      if (!c) return;
      const colN = c.colN || 1, rowN = c.rowN || 1;
      const wNum = parseFloat(c.labelW) || 1, hNum = parseFloat(c.labelH) || 1;
      const visRows = Math.min(rowN, 10);
      shapeEl.style.aspectRatio = `${colN * wNum} / ${visRows * hNum}`;
      shapeEl.style.gridTemplateColumns = `repeat(${colN}, 1fr)`;
      shapeEl.style.gridTemplateRows    = `repeat(${visRows}, 1fr)`;
      shapeEl.innerHTML = Array.from({ length: colN * visRows })
        .map(() => `<span class="wstep-tpl-cell"></span>`).join("");
    });
  }
  populateGlTemplateCardShapes();

  /* 카드 중 현재 선택된 규격과 일치하는 것에 .active를 얹는다 — 목록에서
     골랐든 카드에서 골랐든 항상 같은 카드가 강조되게 renderGlSpecList()가
     호출될 때마다 함께 동기화한다. */
  function syncGlQuickTemplateCards() {
    document.querySelectorAll(".wstep-template-card[data-gl-template-key]").forEach(card => {
      card.classList.toggle("active", card.dataset.glTemplateKey === currentTplKey);
    });
  }

  document.querySelectorAll(".wstep-template-card[data-gl-template-key]").forEach(card => {
    card.addEventListener("click", () => {
      const key = card.dataset.glTemplateKey;
      if (!CFG[key]) return;
      selectTemplate(key); /* 목록 행 클릭과 완전히 같은 경로 재사용(최근 사용 기록 저장 포함) */
      renderGlSpecList();
      renderGlSpecPreview();
    });
  });

  function renderGlSpecPreview() {
    const c = CFG[currentTplKey];
    const modelEl = document.getElementById("gl-spec-sel-model");
    const countEl = document.getElementById("gl-spec-sel-count");
    const sizeEl  = document.getElementById("gl-spec-sel-size");
    if (modelEl) modelEl.textContent = c ? `${c.maker} ${c.model}` : "-";
    if (countEl) countEl.textContent = c ? `${c.colN}×${c.rowN} = ${c.perPage}칸` : "-";
    if (sizeEl)  sizeEl.textContent  = c ? `${c.labelW}×${c.labelH}mm` : "-";
    renderGlGridPreview(document.getElementById("gl-spec-grid-preview"), document.getElementById("gl-spec-grid-info"));
  }

  document.getElementById("gl-step-spec-next")?.addEventListener("click", () => setGlWizardStep("design"));
  document.getElementById("gl-spec-search")?.addEventListener("input", e => {
    glSpecSearchKw = e.target.value;
    renderGlSpecList();
  });

  /* ── Step 3(전체 확인) / Step 4(인쇄) 공용 — 페이지 복제 후 축소 미리보기
     주소라벨 인쇄 미리보기(index.js buildPrintPreviewPages)와 같은 안전한
     방식: 실제 편집 중인 라이브 캔버스를 옮기지 않고 복제본을 붙인다
     (재부모화하면 #general-app-body 하위로 스코프된 CSS가 깨질 위험이 있음). ── */
  function glClonePageInto(wrapId) {
    const wrap = document.getElementById(wrapId);
    if (!wrap) return null;
    wrap.innerHTML = "";
    const page = document.querySelector("#general-app-body .gl-a4-page");
    if (!page) return null;
    const clone = page.cloneNode(true);
    clone.classList.remove("gl-dragging");
    clone.style.removeProperty("transform");
    clone.style.removeProperty("transform-origin");
    clone.querySelectorAll("[contenteditable]").forEach(el => { el.contentEditable = "false"; });
    const fitWrap = document.createElement("div");
    fitWrap.className = "preview-page-fit-wrap";
    fitWrap.appendChild(clone);
    wrap.appendChild(fitWrap);
    glScalePreview(wrapId);
    return clone;
  }
  function glScalePreview(wrapId) {
    const wrap = document.getElementById(wrapId);
    if (!wrap) return;
    const PAGE_W = 794, PAGE_H = 1123; /* A4 @96dpi — 주소라벨 쪽과 동일 상수 */
    const availW = wrap.clientWidth - 48;
    if (availW <= 0) return;
    const scale = Math.min(1, availW / PAGE_W);
    wrap.querySelectorAll(".preview-page-fit-wrap").forEach(fitWrap => {
      const page = fitWrap.firstElementChild;
      if (!page) return;
      if (scale >= 1) {
        fitWrap.style.width = ""; fitWrap.style.height = "";
        page.style.transform = "none"; page.style.transformOrigin = "";
      } else {
        page.style.transformOrigin = "top left";
        page.style.transform = `scale(${scale})`;
        fitWrap.style.width  = Math.round(PAGE_W * scale) + "px";
        fitWrap.style.height = Math.round(PAGE_H * scale) + "px";
      }
    });
  }
  window.addEventListener("resize", () => {
    if (glWizardStep === "review") glScalePreview("gl-review-preview-canvas");
  });

  /* ── Step 3: 확인 및 인쇄 ───────────────────────────────────────
     [정리] 원래 전체확인(Step3)/인쇄(Step4)로 나뉘어 각자
     renderGlReviewPreview()/renderGlPrintSummary()를 갖고 있었으나,
     같은 시트 미리보기를 복제해 보여주는 동일한 로직이라 하나로
     합쳤다 — 인쇄 요약 통계(규격·한 페이지 칸 수)도 이 화면에서
     같이 갱신한다. */
  function renderGlReviewPreview() {
    const c = CFG[currentTplKey];
    const tplEl = document.getElementById("gl-pstep-stat-template");
    const ppEl  = document.getElementById("gl-pstep-stat-perpage");
    if (tplEl) tplEl.textContent = c ? `${c.maker} ${c.model}` : "-";
    if (ppEl)  ppEl.textContent  = c ? `${c.perPage}개 (${c.colN}×${c.rowN})` : "-";

    const clone = glClonePageInto("gl-review-preview-canvas");
    const selBody = document.getElementById("gl-review-sel-body");
    const selHint = document.getElementById("gl-review-sel-hint");
    if (selBody) selBody.style.display = "none";
    if (selHint) selHint.style.display = "";
    if (!clone) return;

    const cells = Array.from(clone.querySelectorAll(".gl-label-cell"));
    let filled = 0;
    cells.forEach((cell, idx) => {
      const hasContent = !!(cell.textContent.trim() || cell.querySelector("img"));
      if (hasContent) filled++;
      cell.classList.add("gl-review-cell-clickable");
      cell.addEventListener("click", () => {
        const idxEl = document.getElementById("gl-review-sel-idx");
        const stateEl = document.getElementById("gl-review-sel-state");
        if (idxEl) idxEl.textContent = `${idx + 1}번`;
        if (stateEl) stateEl.textContent = hasContent ? "입력됨" : "비어있음";
        if (selBody) selBody.style.display = "";
        if (selHint) selHint.style.display = "none";
        const editBtn = document.getElementById("gl-review-sel-edit");
        if (editBtn) editBtn.onclick = () => {
          setGlWizardStep("design");
          setTimeout(() => selectCell(idx), 50);
        };
      });
    });
    const totalEl  = document.getElementById("gl-review-stat-total");
    const filledEl = document.getElementById("gl-review-stat-filled");
    const emptyEl  = document.getElementById("gl-review-stat-empty");
    if (totalEl)  totalEl.textContent  = `${cells.length}개`;
    if (filledEl) filledEl.textContent = `${filled}개`;
    if (emptyEl)  emptyEl.textContent  = `${cells.length - filled}개`;
  }

  /* 기존 인쇄 버튼(printGeneral 로직이 붙어있는 #gl-print-btn)에 클릭을
     그대로 전달 — 인쇄 로직을 다시 구현하지 않는다 */
  document.getElementById("gl-pstep-print-btn")?.addEventListener("click", () => {
    document.getElementById("gl-print-btn")?.click();
  });

  /* ══════════════════════════════════════════════════════════════
     [신규] 일반라벨 자동 저장 + 복원
     지금까지 일반라벨에는 이 기능이 전혀 없어서(위 옛 주석 "세션 복원
     개념이 없어 단순하게 고정한다" 참고), 새로고침 한 번·탭 실수로
     닫기 한 번으로 여러 세션에 걸쳐 만든 칸별 자르기·회전·보정 작업이
     전부 사라질 수 있었다 — 가장 심각한 데이터 손실 위험으로 보고됨.
     주소라벨(index.js)과 완전히 같은 방식(IndexedDB, 5초 간격,
     beforeunload 저장, "확인 후 복원")을 그대로 따른다. index.js가
     전역으로 노출해 둔 idbSet/idbGet/idbDelete를 그대로 가져다 쓰며,
     주소라벨과 키만 다르게 써서(GL_IDB_KEY) 같은 저장소를 서로
     덮어쓰지 않는다.
     ══════════════════════════════════════════════════════════════ */
  const GL_IDB_KEY = "lastGeneralLabels";

  /* 지금 화면 상태를 통째로 복원 가능한 하나의 순수 객체로 모은다.
     DOM을 건드리는 render* 함수는 전혀 부르지 않고 상태 변수만
     읽는다 — 그래야 5초마다 불러도 화면에 아무 영향이 없다("저장"은
     읽기 전용 스냅샷이어야 함). */
  function glGetSnapshot() {
    return {
      v: 1,
      tpl: currentTplKey,
      wizardStep: glWizardStep, /* [버그 수정] 복원 시 "라벨 디자인"으로 무조건 고정되지 않고, 실제로 작업하던 단계(확인·인쇄 포함)로 돌아가게 하려면 저장해둬야 한다 */
      cellEditIdx: glCellEditIdx, /* [신규] 개별편집패널로 특정 칸(텍스트/이미지)을 편집하던 중이었다면 그 칸도 기억해둔다 */
      contentType, imageFit, contentLayout,
      commonText, imageDataUrl,
      glFontSizeMultiplier,
      textColor, textBold, textItalic, textUnderline, lineHeight, letterSpacing,
      textAlignH, textAlignV, textWritingMode,
      imageScale, imageAlignH, imageAlignV,
      fontFamily: els.fontSelect ? els.fontSelect.value : undefined,
      cellTexts, cellFontSizes, cellImgTransforms, cellImages,
      cellFontFamilies, cellColors, cellStyles, cellAligns,
      cellContentTypes, cellContentLayouts, cellTables,
    };
  }

  /* 저장/복원 배너를 띄울 만큼 "의미 있는" 내용이 있는지 — 규격만
     고르고 아무것도 안 채운 상태라면 굳이 저장하거나 복원 배너를
     보여줄 필요가 없다. */
  function glSnapshotHasContent(snap) {
    if (!snap || !snap.tpl) return false;
    return !!(
      (snap.commonText && snap.commonText.trim()) ||
      snap.imageDataUrl ||
      (snap.cellTexts  && Object.keys(snap.cellTexts).length) ||
      (snap.cellImages && Object.keys(snap.cellImages).length) ||
      (snap.cellTables && Object.keys(snap.cellTables).length)
    );
  }

  /* 저장된 스냅샷을 실제로 되돌린다. selectTemplate()이 규격을 바꾸면서
     모든 칸별 오버라이드를 {}로 초기화하는 함수이므로, 반드시 그것부터
     부른 다음 그 위에 저장된 값들을 덮어써야 한다(순서가 중요). 그 뒤
     init()이 처음 시작할 때와 같은 render- / apply- 계열 함수들을 다시
     불러 사이드바 컨트롤(정렬 그리드·세그먼트 버튼 등)도 함께 맞춘다. */
  function glApplySavedSessionSnapshot(snap) {
    if (!snap || !snap.tpl || !CFG[snap.tpl]) return false;
    selectTemplate(snap.tpl);
    contentType     = snap.contentType     || "text";
    imageFit        = snap.imageFit        || "contain";
    contentLayout   = snap.contentLayout   || "below";
    commonText      = snap.commonText      || "";
    imageDataUrl    = snap.imageDataUrl    || null;
    glFontSizeMultiplier = snap.glFontSizeMultiplier || 1.0;
    textColor       = snap.textColor       || DEFAULT_GL_TEXT_COLOR;
    textBold        = !!snap.textBold;
    textItalic      = !!snap.textItalic;
    textUnderline   = !!snap.textUnderline;
    lineHeight      = snap.lineHeight != null ? snap.lineHeight : 1.25;
    letterSpacing   = snap.letterSpacing != null ? snap.letterSpacing : 0;
    textAlignH      = snap.textAlignH      || "center";
    textAlignV      = snap.textAlignV      || "middle";
    textWritingMode = snap.textWritingMode || "horizontal";
    imageScale      = snap.imageScale != null ? snap.imageScale : 100;
    imageAlignH     = snap.imageAlignH     || "center";
    imageAlignV     = snap.imageAlignV     || "middle";
    cellTexts          = snap.cellTexts          || {};
    cellFontSizes       = snap.cellFontSizes       || {};
    cellImgTransforms   = snap.cellImgTransforms   || {};
    cellImages          = snap.cellImages          || {};
    cellFontFamilies    = snap.cellFontFamilies    || {};
    cellColors          = snap.cellColors          || {};
    cellStyles          = snap.cellStyles          || {};
    cellAligns          = snap.cellAligns          || {};
    cellContentTypes    = snap.cellContentTypes    || {};
    cellContentLayouts  = snap.cellContentLayouts  || {};
    cellTables          = {};
    Object.keys(snap.cellTables || {}).forEach((k) => { cellTables[k] = glTblNormalize(snap.cellTables[k]); });

    if (els.textarea) els.textarea.value = commonText;
    if (els.fontSelect && snap.fontFamily) {
      els.fontSelect.value = snap.fontFamily;
      document.documentElement.style.setProperty("--gl-label-font", els.fontSelect.value);
    }
    if (els.fontSizeSelect && typeof window._glSetSelectByFloatValue === "function") window._glSetSelectByFloatValue(els.fontSizeSelect, glFontSizeMultiplier);

    renderContentSeg();
    renderFitSeg();
    renderLayoutSeg();
    renderTextAlignGrid();
    renderWritingSeg();
    renderImgAlignGrid();
    applyGlImageTransform();
    applyGlFontSize();
    applyGlTextFormat();
    renderImageDrop();
    renderInspector();
    renderGlGridPreview();
    rebuildCanvas();
    return true;
  }

  /* 저장 상태 표시 — 주소라벨의 .statusbar-autosave 스타일을 그대로
     재사용한다(#gl-autosave, 하단 상태표시줄 #gl-statusbar 안). */
  function _showGlSaveStatus(state, text) {
    const el = document.getElementById("gl-autosave");
    if (!el) return;
    el.className = "statusbar-autosave " + state;
    el.textContent = text;
    el.style.display = "";
  }

  /* 5초 자동 저장 — 주소라벨과 동일하게 "저장 시점 지문"이 바뀌지
     않았으면 IndexedDB에 다시 쓰지 않고 건너뛴다(불필요한 쓰기 방지). */
  let _glLastSaveFingerprint = null;
  function glSaveToStorage() {
    if (!currentTplKey) return; /* 아직 규격도 안 골랐으면 저장할 게 없음 */
    const snap = glGetSnapshot();
    if (!glSnapshotHasContent(snap)) return; /* 빈 작업은 저장하지 않음(불필요한 복원 배너 방지) */
    let json;
    try { json = JSON.stringify(snap); } catch { return; }
    const fingerprint = json.length + "|" + json.slice(0, 120);
    if (fingerprint === _glLastSaveFingerprint) return;
    _showGlSaveStatus("saving", "저장 중...");
    idbSet(GL_IDB_KEY, json)
      .then(() => {
        _glLastSaveFingerprint = fingerprint;
        const t = new Date();
        const hhmm = t.getHours().toString().padStart(2, "0") + ":" + t.getMinutes().toString().padStart(2, "0");
        _showGlSaveStatus("saved", `자동 저장됨 ${hhmm}`);
      })
      .catch(() => { _showGlSaveStatus("error", "저장 실패"); });
  }
  window._glSaveToStorage = glSaveToStorage; /* beforeunload 등에서 재사용 */

  /* ① 5초 자동 저장 인터벌 — 주소라벨(index.js TASK 07)과 동일한 주기 */
  setInterval(glSaveToStorage, 5000);
  /* ② 브라우저/탭이 강제로 닫힐 때 마지막으로 한 번 더 저장 시도
        (베스트 에포트 — 완료를 보장하진 않지만 5초 주기 저장과 합쳐지면
        데이터 유실 가능성이 크게 줄어든다. 주소라벨과 동일한 방식). */
  window.addEventListener("beforeunload", glSaveToStorage);

  /* ③ 이전 세션 복원 — 주소라벨과 동일하게 "확인 후 복원"만 하고
        자동으로 화면을 덮어쓰지는 않는다(다른 작업을 하려던 사용자가
        모르는 사이 예전 내용으로 바뀌어버리는 사고를 방지). 배너는
        "규격 선택" 단계 위쪽에 표시된다(항상 그 단계부터 시작하므로
        진입하자마자 바로 보인다). */
  idbGet(GL_IDB_KEY).catch(() => null).then((raw) => {
    if (!raw) return;
    let snap = null;
    try { snap = JSON.parse(raw); } catch { snap = null; }
    if (!glSnapshotHasContent(snap)) return;
    const banner  = document.getElementById("gl-restore-banner");
    const hint    = document.getElementById("gl-restore-hint");
    const btn     = document.getElementById("gl-restore-btn");
    const dismissBtn = document.getElementById("gl-restore-dismiss-btn");
    if (!banner || !btn) return;
    const tplInfo = CFG[snap.tpl];
    if (hint) {
      hint.textContent = "💾 저장된 작업이 있습니다" + (tplInfo ? ` — ${tplInfo.maker || ""} ${tplInfo.model || snap.tpl}`.trim() : "");
    }
    banner.style.display = "";
    btn.addEventListener("click", () => {
      glApplySavedSessionSnapshot(snap);
      banner.style.display = "none";
      _glLastSaveFingerprint = null; /* 복원 직후에도 다음 자동 저장이 정상적으로 다시 일어나도록 지문 초기화 */
      /* [수정] "규격 선택"이나 "확인 및 인쇄" 단계로 돌아가면 실제로 다시
         작업을 이어가려면 결국 "라벨 디자인"으로 한 번 더 이동해야 해서
         번거로웠다 — 이제 저장된 단계와 무관하게 항상 "라벨 디자인"(편집)
         화면으로 바로 이동한다. */
      setGlWizardStep("design");
      /* [신규] 개별편집패널로 특정 칸의 텍스트/이미지를 편집하던 중이었다면
         그 패널까지 다시 열어서, 정말로 "하던 부분 그대로"에서 이어갈 수
         있게 한다. */
      const c = CFG[snap.tpl];
      if (snap.cellEditIdx != null && c && snap.cellEditIdx < c.perPage
          && typeof window._glOpenCellFocusPanel === "function") {
        window._glOpenCellFocusPanel(snap.cellEditIdx);
      }
      if (typeof showToast === "function") showToast("이전 작업을 복원했습니다.", "success", 2200);
    });
    if (dismissBtn) {
      dismissBtn.addEventListener("click", () => { banner.style.display = "none"; });
    }
  });

  /* 최초 진입은 항상 "규격 선택"부터 시작한다(복원은 위 배너를 눌러야
     실제로 적용됨 — 자동으로 덮어쓰지 않음). */
  setGlWizardStep("spec");
  updateGlStepTabsGating(); /* [신규] 시작할 땐 아직 규격을 안 골랐으니 2·3단계 탭 비활성화 */

})();
