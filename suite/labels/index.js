/* ══════════════════════════════════════════════════════════════
   우편 라벨지 자동화 v3 — index.js
   기존 기능 100% 유지 + 3패널 UI
   ══════════════════════════════════════════════════════════════ */

const KAKAO_API_KEY = "KakaoAK a1e32e809202e84389f8a7913da38fe1";
const BATCH_SIZE    = 16;  /* 성능 개선: 10→16. 아래 fetchInBatches가 고정 배치
   대기 방식에서 "동시성 워커 풀" 방식으로 바뀌면서 배치 경계에서 생기던
   대기 시간이 사라졌고, 429(요청 과다) 재시도 로직도 추가돼 더 높은
   동시 처리 수를 안전하게 감당할 수 있음. (변수명은 호출부 영향을
   최소화하기 위해 유지하되, 실질적 의미는 "동시 처리 개수") */
const FONT_STEP = 0.1, FONT_MIN = 0.6, FONT_MAX = 1.6;

/* ── 라벨 규격 DB ──────────────────────────────────────────── */
const TEMPLATE_CONFIGS = {
  /* ── 폼텍 (Formtec) ─────────────────────────────────────────
     pt = 상단 페이지 여백  |  pb = 하단 페이지 여백
     pb = 297 − pt − (라벨H × 행수)  →  상하 여백 합 = 297 − 라벨영역

     [수정] pl(좌측 여백) 값을 일반라벨(general-labels-data.js)의
     실측값 기준으로 다시 맞춤. 기존 pl 값은 rowGap(열 간격)을
     더하지 않고 "라벨 폭 + 좌우 여백"만으로 이미 210mm를 채우도록
     되어 있었는데, 그 상태에서 getSafeColumnGapMM()이 210mm 초과를
     막기 위해 남는 공간(leftover)을 계산하다 보니 leftover가 0이
     되어 rowGap(2mm/10mm)이 전부 무시되고 열 사이 공백이 사라졌었다.
     → pl을 "라벨 폭 × 열수 + 간격 × (열수−1) + pl×2 = 210mm"가
     성립하도록 줄여서, 일반라벨 탭과 동일하게 열 간격이 실제로
     보이도록 복원함. ─────────────────────────────────────────── */
  "3107":     { perPage:16, pt:"14.75mm", pb:"11.15mm", pl:"5mm",  cols:"repeat(2,99.1mm)",  rows:"repeat(8,33.9mm)",   w:"99.1mm",  h:"33.9mm",  rowGap:"2.5mm", padding:"4.5mm 6mm 3.5mm 6mm",   addrFs:"14.5", nameFs:"13",   digitW:"17px", digitH:"22px", digitFs:"14",   maker:"폼텍 (Formtec)",    model:"LS-3107",   colN:2, rowN:8  },
  "3108":     { perPage:14, pt:"14.75mm", pb:"15.65mm", pl:"5mm",  cols:"repeat(2,99.1mm)",  rows:"repeat(7,38.1mm)",   w:"99.1mm",  h:"38.1mm",  rowGap:"2.5mm", padding:"5.5mm 6mm 4.5mm 6mm",   addrFs:"15.5", nameFs:"13.5", digitW:"18px", digitH:"23px", digitFs:"14.5", maker:"폼텍 (Formtec)",    model:"LS-3108",   colN:2, rowN:7  },
  "3105":     { perPage:21, pt:"15mm", pb:"15.30mm", pl:"7.25mm",  cols:"repeat(3,63.5mm)",  rows:"repeat(7,38.1mm)",   w:"63.5mm",  h:"38.1mm",  rowGap:"2.5mm", padding:"5mm 5mm 4mm 5mm",        addrFs:"12.5", nameFs:"11",   digitW:"14px", digitH:"18px", digitFs:"12",   maker:"폼텍 (Formtec)",    model:"LS-3105",   colN:3, rowN:7  },
  "3112":     { perPage:12, pt:"8mm", pb:"9mm", pl:"7.25mm",  cols:"repeat(3,63.5mm)",  rows:"repeat(4,70.0mm)",   w:"63.5mm",  h:"70.0mm",  rowGap:"2.5mm", padding:"8mm 6mm 6mm 6mm",        addrFs:"15",   nameFs:"13",   digitW:"17px", digitH:"22px", digitFs:"14",   maker:"폼텍 (Formtec)",    model:"LS-3112",   colN:3, rowN:4  },
  "3109":     { perPage:18, pt:"14mm", pb:"13mm", pl:"4mm",   cols:"repeat(2,100mm)",   rows:"repeat(9,30mm)",     w:"100mm",   h:"30mm",    rowGap:"2.5mm", padding:"3.5mm 6mm 2.5mm 6mm",   addrFs:"13",   nameFs:"11.5", digitW:"15px", digitH:"20px", digitFs:"13",   maker:"폼텍 (Formtec)",    model:"LS-3109",   colN:2, rowN:9  },
  "3510":     { perPage:10, pt:"18mm", pb:"19mm", pl:"11.10mm",  cols:"repeat(2,88.9mm)",  rows:"repeat(5,52.0mm)",   w:"88.9mm",  h:"52.0mm",  rowGap:"10mm", padding:"7mm 7mm 5mm 7mm",        addrFs:"17",   nameFs:"15",   digitW:"20px", digitH:"26px", digitFs:"16",   maker:"폼텍 (Formtec)",    model:"LS-3510",   colN:2, rowN:5  },
  "3114":     { perPage:8,  pt:"14.5mm", pb:"11.70mm", pl:"5mm",  cols:"repeat(2,99.1mm)",  rows:"repeat(4,67.7mm)",   w:"99.1mm",  h:"67.7mm",  rowGap:"2.5mm", padding:"10mm 8mm 8mm 8mm",       addrFs:"20",   nameFs:"17",   digitW:"23px", digitH:"29px", digitFs:"18",   maker:"폼텍 (Formtec)",    model:"LS-3114",   colN:2, rowN:4  },
  /* ── 아이라벨 (iLabel) ─────────────────────────────────────
     [수정] 폼텍과 동일한 원인의 버그. pl(좌측 여백) 값이 일반라벨
     (general-labels-data.js)의 실측값과 어긋나 있어서, colN×w + 간격
     × (colN-1) + pl×2 가 210mm를 초과 → getSafeColumnGapMM()이 이를
     막으려고 leftover를 계산하는 과정에서 간격이 강제로 줄거나(A3120은
     아예 0으로) 눌렸었다. → pl을 general-labels-data.js의 실측값
     (G_IL_CL227/CL437/CL225/CL239)으로 맞추고, rowGap도 그 실측
     간격(2.4~2.6mm)으로 맞춰 "선언된 간격 = 실제 적용되는 간격"이
     되도록 복원함. A3210(CL228)은 이미 pl이 실측값과 같아서 간격이
     완전히 눌리진 않았지만(2.4mm만 적용), 선언값 2.5mm 자체가 실측과
     0.1mm 어긋나 있어 함께 정리함. ───────────────────────────── */
  "A3210":    { perPage:16, pt:"13mm",  pb:"14.64mm", pl:"5mm",   cols:"repeat(2,98.8mm)",  rows:"repeat(8,33.67mm)",  w:"98.8mm",  h:"33.67mm", rowGap:"2.4mm", padding:"4.5mm 6mm 3.5mm 6mm",   addrFs:"14",   nameFs:"12.5", digitW:"16px", digitH:"21px", digitFs:"13.5", maker:"아이라벨 (iLabel)", model:"CL228",     colN:2, rowN:8  },
  "A3120":    { perPage:14, pt:"16mm",  pb:"14.65mm", pl:"5mm",   cols:"repeat(2,98.8mm)",  rows:"repeat(7,38.05mm)",  w:"98.8mm",  h:"38.05mm", rowGap:"2.4mm", padding:"5.5mm 6mm 4.5mm 6mm",   addrFs:"15.5", nameFs:"13.5", digitW:"18px", digitH:"23px", digitFs:"14.5", maker:"아이라벨 (iLabel)", model:"CL227",     colN:2, rowN:7  },
  "A3140":    { perPage:21, pt:"10mm",  pb:"7.21mm", pl:"6.70mm",   cols:"repeat(3,63.8mm)",  rows:"repeat(7,39.97mm)",  w:"63.8mm",  h:"39.97mm", rowGap:"2.6mm", padding:"5.5mm 5mm 4.5mm 5mm",   addrFs:"12.5", nameFs:"11",   digitW:"13px", digitH:"18px", digitFs:"12",   maker:"아이라벨 (iLabel)", model:"CL437",     colN:3, rowN:7  },
  "A3105":    { perPage:10, pt:"6.5mm",  pb:"5.5mm", pl:"4.90mm",  cols:"repeat(2,99.1mm)",  rows:"repeat(5,57.0mm)",   w:"99.1mm",  h:"57.0mm",  rowGap:"2mm", padding:"8mm 7mm 6mm 7mm",        addrFs:"18",   nameFs:"15.5", digitW:"21px", digitH:"27px", digitFs:"16.5", maker:"아이라벨 (iLabel)", model:"CL225",     colN:2, rowN:5  },
  "A3108":    { perPage:27, pt:"10mm",  pb:"16.73mm", pl:"9.10mm",  cols:"repeat(3,62.2mm)",  rows:"repeat(9,30.03mm)",  w:"62.2mm",  h:"30.03mm", rowGap:"2.6mm", padding:"2.5mm 4mm 2mm 4mm",      addrFs:"10.5", nameFs:"9.5",  digitW:"11px", digitH:"15px", digitFs:"10",   maker:"아이라벨 (iLabel)", model:"CL239",     colN:3, rowN:9  },
  /* ── iLIFE ────────────────────────────────────────────────
     [수정] 동일 원인 — pl이 general-labels-data.js(G_ILF_2016/2012/2010)
     실측값(4.90mm / 4.90mm / 8.00mm)보다 커서 leftover가 음수가 되고
     간격이 전부 0으로 눌려 있었음. 실측 pl로 교정. ───────────────── */
  "IL2016":   { perPage:16, pt:"5.90mm",  pb:"19.90mm", pl:"4.90mm",  cols:"repeat(2,99.1mm)",  rows:"repeat(8,33.9mm)",   w:"99.1mm",  h:"33.9mm",  rowGap:"2.5mm", padding:"4.5mm 6mm 3.5mm 6mm",   addrFs:"14.5", nameFs:"13",   digitW:"17px", digitH:"22px", digitFs:"14",   maker:"iLIFE",            model:"2016",      colN:2, rowN:8  },
  "IL2012":   { perPage:12, pt:"16.60mm", pb:"26.60mm", pl:"4.90mm",  cols:"repeat(2,99.1mm)",  rows:"repeat(6,42.3mm)",   w:"99.1mm",  h:"42.3mm",  rowGap:"2.5mm", padding:"6.5mm 7mm 5.5mm 7mm",   addrFs:"16.5", nameFs:"14.5", digitW:"19px", digitH:"25px", digitFs:"16",   maker:"iLIFE",            model:"2012",      colN:2, rowN:6  },
  "IL2010":   { perPage:10, pt:"15.75mm", pb:"23.75mm", pl:"8.00mm",   cols:"repeat(2,96mm)",    rows:"repeat(5,51.5mm)",   w:"96mm",    h:"51.5mm",  rowGap:"2.5mm", padding:"7.5mm 7mm 6mm 7mm",      addrFs:"18",   nameFs:"15.5", digitW:"21px", digitH:"27px", digitFs:"17",   maker:"iLIFE",            model:"2010",      colN:2, rowN:5  },
  /* ── 오피스디포 ──────────────────────────────────────────────
     [주의] OD2016은 실측(G_OD_2016)상 colGap이 원래 0mm인 규격이다
     (라벨 폭 101mm×2 + 여백만으로 이미 210mm를 꽉 채워서 열 사이
     간격이 실제로 없음). 그래서 getSafeColumnGapMM()이 0을 반환하는
     게 정상이며 버그가 아니므로 그대로 둔다. OD2030만 pl이 실측값
     (G_OD_2030: 4.00mm)과 달라 간격이 0으로 눌려 있었으므로 교정. ── */
  "OD2016":   { perPage:16, pt:"5.90mm",  pb:"19.90mm", pl:"4.0mm",   cols:"repeat(2,101mm)",   rows:"repeat(8,33.9mm)",   w:"101mm",   h:"33.9mm",  rowGap:"2.5mm", padding:"4.5mm 6mm 3.5mm 6mm",   addrFs:"14.5", nameFs:"13",   digitW:"17px", digitH:"22px", digitFs:"14",   maker:"오피스디포",        model:"16칸",      colN:2, rowN:8  },
  "OD2030":   { perPage:30, pt:"12.50mm", pb:"30.50mm", pl:"4.00mm",   cols:"repeat(3,66mm)",    rows:"repeat(10,25.4mm)",  w:"66mm",    h:"25.4mm",  rowGap:"2mm", padding:"2.5mm 4.5mm 2mm 4.5mm",  addrFs:"10",   nameFs:"9",    digitW:"11px", digitH:"14px", digitFs:"9.5",  maker:"오피스디포",        model:"30칸",      colN:3, rowN:10 },
  /* ── 아이라벨 LQ ────────────────────────────────────────────
     [수정] pl이 실측(G_LQ_2514/2520: 4.00mm)보다 커서 간격이 0으로
     눌려 있었음. 실측 pl로 교정. ─────────────────────────────── */
  "LQ2514":   { perPage:14, pt:"9.33mm",  pb:"21.32mm", pl:"4.00mm",   cols:"repeat(2,100mm)",   rows:"repeat(7,38.05mm)",  w:"100mm",   h:"38.05mm", rowGap:"2.5mm", padding:"5.5mm 6mm 4.5mm 6mm",   addrFs:"15.5", nameFs:"13.5", digitW:"18px", digitH:"23px", digitFs:"14.5", maker:"아이라벨 LQ",       model:"LQ2514",    colN:2, rowN:7  },
  "LQ2520":   { perPage:20, pt:"4.50mm",  pb:"22.50mm", pl:"4.00mm",   cols:"repeat(2,100mm)",   rows:"repeat(10,27mm)",    w:"100mm",   h:"27mm",    rowGap:"2.5mm", padding:"3mm 5mm 2mm 5mm",        addrFs:"12",   nameFs:"11",   digitW:"13px", digitH:"17px", digitFs:"12",   maker:"아이라벨 LQ",       model:"LQ2520",    colN:2, rowN:10 },
  /* ── 일반 규격 ───────────────────────────────────────────── */
  "STD_A4_8": { perPage:8,  pt:"17.50mm", pb:"23.50mm", pl:"11.0mm",  cols:"repeat(2,94mm)",    rows:"repeat(4,64mm)",     w:"94mm",    h:"64mm",    rowGap:"2.5mm", padding:"10mm 8mm 8mm 8mm",       addrFs:"20",   nameFs:"17",   digitW:"23px", digitH:"29px", digitFs:"18",   maker:"일반 규격",         model:"8칸 대형",   colN:2, rowN:4  },
  "STD_A4_32":{ perPage:32, pt:"21.50mm", pb:"35.50mm", pl:"9.2mm",   cols:"repeat(4,45.9mm)",  rows:"repeat(8,30mm)",     w:"45.9mm",  h:"30mm",    rowGap:"2mm", padding:"3mm 3.5mm 2.5mm 3.5mm",  addrFs:"9",    nameFs:"8",    digitW:"10px", digitH:"13px", digitFs:"9",    maker:"일반 규격",         model:"32칸 소형",  colN:4, rowN:8  },
  /* ── 애니라벨 (프린텍) ────────────────────────────────────
     pb = 297 − pt − (h × rowN)  ·  총폭 = pl×2 + colN×w + (colN-1)×2mm = 210mm (실측 정확)
     ──────────────────────────────────────────────────────── */
  "ANI_V3250": { perPage:10, pt:"18.50mm", pb:"18.50mm", pl:"15.10mm", cols:"repeat(2,88.9mm)",  rows:"repeat(5,52.0mm)",   w:"88.9mm",  h:"52.0mm",  rowGap:"2.5mm", padding:"7mm 7mm 5mm 7mm",         addrFs:"17",   nameFs:"15",   digitW:"20px", digitH:"26px", digitFs:"16",   maker:"애니라벨 (프린텍)", model:"V3250",     colN:2, rowN:5  },
  "ANI_V3270": { perPage:12, pt:"9.30mm",  pb:"9.30mm",  pl:"4.00mm",  cols:"repeat(2,100mm)",   rows:"repeat(6,46.4mm)",   w:"100mm",   h:"46.4mm",  rowGap:"2.5mm", padding:"6.5mm 7mm 5.5mm 7mm",     addrFs:"16",   nameFs:"14.5", digitW:"19px", digitH:"25px", digitFs:"15.5", maker:"애니라벨 (프린텍)", model:"V3270",     colN:2, rowN:6  },
  "ANI_V3230": { perPage:14, pt:"15.15mm", pb:"15.15mm", pl:"5.25mm",  cols:"repeat(2,99.06mm)", rows:"repeat(7,38.1mm)",   w:"99.06mm", h:"38.1mm",  rowGap:"2.5mm", padding:"5.5mm 6mm 4.5mm 6mm",     addrFs:"15.5", nameFs:"13.5", digitW:"18px", digitH:"23px", digitFs:"14.5", maker:"애니라벨 (프린텍)", model:"V3230",     colN:2, rowN:7  },
  "ANI_V3240": { perPage:16, pt:"14mm", pb:"12.22mm", pl:"5.25mm",  cols:"repeat(2,99.06mm)", rows:"repeat(8,33.85mm)",  w:"99.06mm", h:"33.85mm", rowGap:"2.5mm", padding:"4mm 6mm 3.5mm 6mm",       addrFs:"14.5", nameFs:"13",   digitW:"17px", digitH:"22px", digitFs:"14",   maker:"애니라벨 (프린텍)", model:"V3240",     colN:2, rowN:8  },
  "ANI_V3320": { perPage:18, pt:"9.00mm",  pb:"9.00mm",  pl:"7.75mm",  cols:"repeat(3,63.5mm)",  rows:"repeat(6,46.5mm)",   w:"63.5mm",  h:"46.5mm",  rowGap:"2mm", padding:"6mm 5mm 5mm 5mm",          addrFs:"14",   nameFs:"12",   digitW:"16px", digitH:"21px", digitFs:"13",   maker:"애니라벨 (프린텍)", model:"V3320",     colN:3, rowN:6  },
};

/* ── DOM 참조 ──────────────────────────────────────────────── */
const excelInput          = document.getElementById("excel");
const postLabelContainer  = document.getElementById("post-label-container");
const loadingModal        = document.getElementById("loading-modal");
const loadingText         = document.getElementById("loading-text");
const templateSelect      = document.getElementById("template-select");
const fontSelect          = document.getElementById("font-select");
const watermarkCheckbox   = document.getElementById("watermark-checkbox");
const loadBtn             = document.getElementById("load-btn");
const resetBtn            = document.getElementById("reset-btn");
const printBtn            = document.getElementById("print-btn");
const addBtn              = document.getElementById("add-btn");
const copyBtn             = document.getElementById("copy-btn");
const fillBtn             = document.getElementById("fill-btn");
const undoBtn             = document.getElementById("undo-btn");
const searchInput         = document.getElementById("search-input");
const themeToggle         = document.getElementById("theme-toggle");
const uploadZone          = document.getElementById("upload-zone");
const canvasScroll        = document.getElementById("canvas-scroll");
const statusBadge         = document.getElementById("status-badge");

/* ── 상태 ───────────────────────────────────────────────────── */
let fontSizeMultiplier = 1.0;
const addressCache    = new Map();
const addressInFlight = new Map();
const ADDRESS_CACHE_MAX = 500;
let undoStack = [];
const MAX_UNDO = 50;

/* ── Redo 스택 ─────────────────────────────────────── */
let redoStack = [];
const MAX_REDO = 50;

/* ══════════════════════════════════════════════════════════════
   다크모드
   ══════════════════════════════════════════════════════════════ */
(function() {
  // 접근성: 아이콘만 있는 버튼이라 상태를 스크린리더에도 전달하기 위해
  // aria-label(현재 눌렀을 때 벌어질 동작)과 aria-pressed(토글 상태)를 함께 갱신
  function syncThemeA11y(theme) {
    const label = theme === "dark" ? "라이트모드 전환" : "다크모드 전환";
    themeToggle.setAttribute("aria-label", label);
    themeToggle.setAttribute("title", label);
    themeToggle.setAttribute("aria-pressed", theme === "dark" ? "true" : "false");
  }
  const saved = localStorage.getItem("theme") || "light";
  document.documentElement.setAttribute("data-theme", saved);
  themeToggle.textContent = saved === "dark" ? "☀️" : "🌙";
  syncThemeA11y(saved);
  themeToggle.addEventListener("click", () => {
    const cur  = document.documentElement.getAttribute("data-theme");
    const next = cur === "dark" ? "light" : "dark";
    document.documentElement.setAttribute("data-theme", next);
    localStorage.setItem("theme", next);
    themeToggle.textContent = next === "dark" ? "☀️" : "🌙";
    syncThemeA11y(next);
  });
})();

/* ══════════════════════════════════════════════════════════════
   줌
   ══════════════════════════════════════════════════════════════ */
let zoomLevel = 1.0;
const zoomLabel = document.getElementById("zoom-label");
function applyZoom() {
  zoomLabel.textContent = Math.round(zoomLevel * 100) + "%";
  document.querySelectorAll(".a4-page").forEach(p => {
    p.style.transform = zoomLevel !== 1 ? `scale(${zoomLevel})` : "";
    p.style.transformOrigin = "top center";
  });
}
document.getElementById("zoom-in") .addEventListener("click", () => { zoomLevel = Math.min(zoomLevel + .1, 2.0); applyZoom(); });
document.getElementById("zoom-out").addEventListener("click", () => { zoomLevel = Math.max(zoomLevel - .1, .3); applyZoom(); });

/* ══════════════════════════════════════════════════════════════
   한 페이지(Fit) 모드
   ══════════════════════════════════════════════════════════════ */
let viewMode       = localStorage.getItem("viewMode") || "scroll";
let fitCurrentPage = 0;

const fitNav      = document.getElementById("fit-nav");
const fitPrev     = document.getElementById("fit-prev");
const fitNext     = document.getElementById("fit-next");
const fitPageInfo = document.getElementById("fit-page-info");
const viewModeBtn = document.getElementById("view-mode-btn");

function getFitPages() { return Array.from(postLabelContainer.querySelectorAll(".a4-page")); }
function calcFitScale() {
  const avW = canvasScroll.clientWidth  - 48;
  const avH = canvasScroll.clientHeight - 64;
  return Math.min(avW / 794, avH / 1123, 1);
}
function applyFitPage(idx) {
  const pages = getFitPages();
  if (!pages.length) { fitPageInfo.textContent = "- / -"; return; }
  idx = Math.max(0, Math.min(idx, pages.length - 1));
  fitCurrentPage = idx;
  pages.forEach((p, i) => p.classList.toggle("fit-active", i === idx));
  fitPageInfo.textContent = `${idx + 1} / ${pages.length}`;
  fitPrev.disabled = idx === 0;
  fitNext.disabled = idx === pages.length - 1;
  const s = calcFitScale();
  document.documentElement.style.setProperty("--fit-scale", s);
  postLabelContainer.style.height = (1123 * s) + "px";
}
function enterFitMode() {
  viewMode = "fit"; localStorage.setItem("viewMode", "fit");
  document.body.classList.add("view-fit");
  viewModeBtn.classList.add("active");
  // SVG 아이콘 보존: 텍스트 노드만 교체
  const vmText = viewModeBtn.querySelector(".vm-label");
  if (vmText) vmText.textContent = "스크롤";
  else viewModeBtn.innerHTML = "⊞&nbsp;스크롤";
  fitNav.classList.add("visible");
  applyFitPage(fitCurrentPage);
}
function exitFitMode() {
  viewMode = "scroll"; localStorage.setItem("viewMode", "scroll");
  document.body.classList.remove("view-fit");
  viewModeBtn.classList.remove("active");
  // SVG 아이콘 보존: 텍스트 노드만 교체
  const vmText = viewModeBtn.querySelector(".vm-label");
  if (vmText) vmText.textContent = "한페이지";
  else viewModeBtn.innerHTML = "⊡&nbsp;한페이지";
  fitNav.classList.remove("visible");
  getFitPages().forEach(p => p.classList.remove("fit-active"));
  postLabelContainer.style.height = "";
  document.documentElement.style.removeProperty("--fit-scale");
}
viewModeBtn.addEventListener("click", () => viewMode === "fit" ? exitFitMode() : enterFitMode());
fitPrev.addEventListener("click", () => applyFitPage(fitCurrentPage - 1));
fitNext.addEventListener("click", () => applyFitPage(fitCurrentPage + 1));
window.addEventListener("resize", () => { if (viewMode === "fit") applyFitPage(fitCurrentPage); });

/* ══════════════════════════════════════════════════════════════
   사이드바 / 인스펙터 토글
   ══════════════════════════════════════════════════════════════ */
// 접근성: 접힘/펼침 상태를 aria-expanded와 aria-label(다음 동작 안내)로 함께 전달
function syncPanelToggleA11y(btn, panelEl, labelBase) {
  const collapsed = panelEl.classList.contains("collapsed");
  const label = collapsed ? `${labelBase} 펼치기` : `${labelBase} 접기`;
  btn.setAttribute("aria-expanded", collapsed ? "false" : "true");
  btn.setAttribute("aria-label", label);
  btn.setAttribute("title", label);
  btn.dataset.tooltip = label;
}
const sidebarToggleBtn    = document.getElementById("sidebar-toggle");
const inspectorToggleBtn  = document.getElementById("inspector-toggle");
const sidebarPanelEl      = document.getElementById("sidebar");
const inspectorPanelEl    = document.getElementById("inspector");
sidebarToggleBtn.addEventListener("click", () => {
  sidebarPanelEl.classList.toggle("collapsed");
  syncPanelToggleA11y(sidebarToggleBtn, sidebarPanelEl, "사이드바");
});
inspectorToggleBtn.addEventListener("click", () => {
  inspectorPanelEl.classList.toggle("collapsed");
  syncPanelToggleA11y(inspectorToggleBtn, inspectorPanelEl, "인스펙터");
});

/* ══════════════════════════════════════════════════════════════
   인스펙터 업데이트
   ══════════════════════════════════════════════════════════════ */
function updateInspector() {
  const c   = TEMPLATE_CONFIGS[templateSelect.value];
  const opt = templateSelect.options[templateSelect.selectedIndex];
  if (!c) return;
  document.getElementById("insp-maker").textContent  = c.maker;
  document.getElementById("insp-model").textContent  = c.model;
  document.getElementById("insp-size").textContent   = `${c.w} × ${c.h}`;
  document.getElementById("insp-count").textContent  = `${c.colN} × ${c.rowN} = ${c.perPage}칸`;
  document.getElementById("insp-margin").textContent = `${c.pt} / ${c.pl}`;
  document.getElementById("canvas-spec").textContent =
    (opt?.text || "").replace(/\s*\(.*\)/, "").trim() + `  ·  ${c.colN}×${c.rowN}칸  ·  ${c.w} × ${c.h}`;
}

/* ══════════════════════════════════════════════════════════════
   검색 고도화 (기존 runSearch 완전 대체)
   지원: 다중 키워드 AND | OR 검색 | 필드 검색(이름:/주소:/우편번호:)
   기존 searchInput 이벤트·debounce·Enter/Escape 바인딩은 그대로 유지
   ══════════════════════════════════════════════════════════════ */
let searchMatchedLabels = [], searchCurrentIndex = -1;

/* 검색 쿼리 파서 — 반환: { type:'and'|'or'|'field', terms, field? }
   실제 구현은 lib/search-parser.js(SearchParser)에 있음 — 단위 테스트가
   그 파일을 직접 import해서 검증하므로, 여기서는 얇은 래퍼만 유지한다. */
function _parseSearchQuery(raw) {
  return SearchParser.parseSearchQuery(raw);
}

/* 라벨이 쿼리에 매치하는지 판정 — WeakMap 텍스트 캐시로 얻은 값을
   SearchParser.matchesQuery(순수 함수)에 그대로 넘긴다. */
function _labelMatchesQuery(label, query) {
  return SearchParser.matchesQuery(getLabelText(label), query);
}

/* 검색 모드 chip 표시 */
function _updateSearchModeChip(query) {
  const wrap = searchInput.closest(".search-wrap");
  if (!wrap) return;
  let chip = wrap.querySelector(".search-mode-chip");
  if (!query || !searchInput.value.trim()) {
    if (chip) chip.remove();
    wrap.classList.remove("has-chip");
    return;
  }
  if (!chip) { chip = document.createElement("span"); chip.className = "search-mode-chip"; wrap.insertBefore(chip, searchInput); }
  if (query.type === "field")      { chip.className = "search-mode-chip mode-field"; chip.textContent = query.field + ":"; }
  else if (query.type === "or")    { chip.className = "search-mode-chip mode-or";    chip.textContent = "OR"; }
  else if (query.terms?.length > 1){ chip.className = "search-mode-chip mode-and";   chip.textContent = "AND"; }
  else { if (chip) chip.remove(); wrap.classList.remove("has-chip"); return; }
  wrap.classList.add("has-chip");
}

/* ── 검색 <mark> 인라인 하이라이트 헬퍼 ─────────────────── */
/* contenteditable 요소에 안전하게 mark 삽입:
   기존 mark 를 제거 후 TextNode 에서 키워드를 찾아 <mark> 로 감쌈 */
function _applyMarkHighlight(el, terms) {
  /* 1. 기존 mark 제거 (텍스트 복원) */
  el.querySelectorAll("mark.sh").forEach(m => {
    const parent = m.parentNode;
    parent.replaceChild(document.createTextNode(m.textContent), m);
    parent.normalize();
  });
  if (!terms || !terms.length) return;

  /* 2. TextNode 순회하며 매칭 위치에 mark 삽입 */
  const walk = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
  const nodes = [];
  let n;
  while ((n = walk.nextNode())) nodes.push(n);

  nodes.forEach(textNode => {
    const orig = textNode.textContent;
    const low  = orig.toLowerCase();
    let lastIdx = 0;
    const parts = [];
    let matched = false;

    /* 모든 term 에 대해 매칭 위치 수집 후 정렬 */
    const hits = [];
    terms.forEach(term => {
      if (!term) return;
      let i = 0;
      while ((i = low.indexOf(term, i)) !== -1) {
        hits.push({ start: i, end: i + term.length });
        i += term.length;
      }
    });
    if (!hits.length) return;
    /* 겹치는 구간 병합 */
    hits.sort((a,b) => a.start - b.start);
    const merged = [hits[0]];
    for (let k=1;k<hits.length;k++) {
      const prev = merged[merged.length-1];
      if (hits[k].start <= prev.end) prev.end = Math.max(prev.end, hits[k].end);
      else merged.push({ ...hits[k] });
    }

    merged.forEach(({ start, end }) => {
      if (start > lastIdx) parts.push(document.createTextNode(orig.slice(lastIdx, start)));
      const mark = document.createElement("mark");
      mark.className = "sh";
      mark.textContent = orig.slice(start, end);
      parts.push(mark);
      lastIdx = end;
      matched = true;
    });
    if (lastIdx < orig.length) parts.push(document.createTextNode(orig.slice(lastIdx)));

    if (matched && parts.length) {
      const frag = document.createDocumentFragment();
      parts.forEach(p => frag.appendChild(p));
      textNode.parentNode.replaceChild(frag, textNode);
    }
  });
  /* mark 삽입 후 검색 텍스트 캐시 무효화 (캐시가 mark 태그 텍스트 오염 방지) */
  const parentLabel = el.closest(".label");
  if (parentLabel) _labelTextCache.delete(parentLabel);
}

/* ── 라벨 전체 mark 제거 */
/* [버그 수정] replaceChild 호출 직후 m.parentNode는 null이 됨(자기 자신이
   DOM에서 분리되므로). null.normalize() 호출로 예외가 발생해 forEach 루프가
   그 시점에서 전부 중단되고, 그 뒤 라벨들은 검색 하이라이트·숨김 상태가
   그대로 남아있었음 → parent를 미리 변수로 저장해두는 방식으로 수정. */
function _clearMarkHighlight(label) {
  label.querySelectorAll("mark.sh").forEach(m => {
    const parent = m.parentNode;
    if (!parent) return;
    parent.replaceChild(document.createTextNode(m.textContent), m);
    parent.normalize();
  });
}

function runSearch(keyword) {
  const raw    = keyword.trim();
  const query  = _parseSearchQuery(raw);
  const labels = postLabelContainer.querySelectorAll(".label");
  labels.forEach(l => {
    l.classList.remove("search-highlight", "search-active");
    _clearMarkHighlight(l);  /* 기존 mark 제거 */
  });
  searchMatchedLabels = []; searchCurrentIndex = -1;

  _updateSearchModeChip(query);

  if (!raw) {
    /* 검색어 비었을 때: 모든 검색 상태 완전 초기화 */
    labels.forEach(l => {
      /* display 복원 */
      if (l.style.display === "none") l.style.display = "";
      /* 검색 관련 클래스 전부 제거 */
      l.classList.remove("search-highlight", "search-active");
      /* 인라인 <mark> 하이라이트 제거 */
      _clearMarkHighlight(l);
    });
    /* 검색 매칭 상태 초기화 */
    searchMatchedLabels = [];
    searchCurrentIndex  = -1;
    searchInput.classList.remove("has-results", "no-results");
    const srBadge = document.getElementById("search-result-badge");
    if (srBadge) srBadge.style.display = "none";
    /* mode chip 제거 */
    _updateSearchModeChip(null);
    updateLabelCount(); return;
  }

  /* 하이라이트에 사용할 term 배열 추출 */
  let _markTerms = [];
  if (query) {
    if (query.type === "field")      _markTerms = [query.term];
    else if (query.type === "or")    _markTerms = query.terms;
    else /* and */                   _markTerms = query.terms;
  }

  /* 성능: display 변경을 분리해 reflow 최소화 */
  const hits = [], misses = [];
  labels.forEach(l => {
    if (_labelMatchesQuery(l, query)) {
      hits.push(l); l.classList.add("search-highlight");
      /* 매칭 라벨에만 <mark> 인라인 하이라이트 적용 */
      const addrEl = l.querySelector(".addr");
      const nameEl = l.querySelector(".name");
      if (addrEl) _applyMarkHighlight(addrEl, _markTerms);
      if (nameEl) _applyMarkHighlight(nameEl, _markTerms);
    } else misses.push(l);
  });
  /* 표시/숨김을 배치 처리 */
  hits.forEach(l   => { if (l.style.display === "none") l.style.display = ""; });
  misses.forEach(l => { if (l.style.display !== "none") l.style.display = "none"; });
  searchMatchedLabels = hits;

  const count = hits.length;
  statusBadge.textContent = `검색 ${count}개`;
  statusBadge.classList.remove("empty");
  searchInput.classList.toggle("has-results", count > 0);
  searchInput.classList.toggle("no-results",  count === 0);

  const srBadge = document.getElementById("search-result-badge");
  if (srBadge) {
    if (count > 0) { srBadge.textContent = `${count}건`; srBadge.style.display = ""; }
    else if (raw)  { srBadge.textContent = "없음";       srBadge.style.display = ""; }
    else           { srBadge.style.display = "none"; }
  }
  if (count > 0) { searchCurrentIndex = 0; scrollToSearchResult(0); }
}
function scrollToSearchResult(index) {
  if (!searchMatchedLabels.length) return;
  searchMatchedLabels.forEach(l => l.classList.remove("search-active"));
  const target = searchMatchedLabels[index];
  target.classList.add("search-active");
  target.scrollIntoView({ behavior: "smooth", block: "center" });
  statusBadge.textContent = `${index + 1} / ${searchMatchedLabels.length}개`;
}
/* 검색창이 비는 즉시(debounce 없이) 배지·chip·하이라이트 즉시 초기화 */
searchInput.addEventListener("input", e => {
  if (!e.target.value.trim()) {
    /* 글자 전부 지웠을 때 즉시 초기화 */
    const srBadge = document.getElementById("search-result-badge");
    if (srBadge) srBadge.style.display = "none";
    _updateSearchModeChip(null);
    searchInput.classList.remove("has-results", "no-results");
    /* 라벨 하이라이트·숨김도 즉시 복원 */
    postLabelContainer.querySelectorAll(".label").forEach(l => {
      l.style.display = "";
      l.classList.remove("search-highlight", "search-active");
      _clearMarkHighlight(l);
    });
    searchMatchedLabels = []; searchCurrentIndex = -1;
    updateLabelCount();
  }
});
searchInput.addEventListener("input", debounce(e => runSearch(e.target.value), 200));
searchInput.addEventListener("keydown", e => {
  if (e.key === "Enter") {
    e.preventDefault();
    if (!searchMatchedLabels.length) return;
    const len  = searchMatchedLabels.length;
    if (e.shiftKey) {
      const base = searchCurrentIndex < 0 ? 0 : searchCurrentIndex;
      searchCurrentIndex = (base - 1 + len) % len;
    } else {
      searchCurrentIndex = (searchCurrentIndex + 1) % len;
    }
    /* 목적지 라벨로 스크롤 후 검색 상태 자동 초기화 */
    const target = searchMatchedLabels[searchCurrentIndex];
    if (target) {
      searchMatchedLabels.forEach(l => l.classList.remove("search-active"));
      target.classList.add("search-active");
      target.scrollIntoView({ behavior: "smooth", block: "center" });
      /* 0.6초 후 검색창·하이라이트 자동 초기화 */
      setTimeout(() => {
        target.classList.remove("search-active");
        searchInput.value = "";
        runSearch("");
        searchInput.blur();
      }, 600);
    }
    return;
  }
  if (e.key === "Escape") {
    searchInput.value = ""; runSearch(""); searchInput.blur();
    /* runSearch("") 내부에서 _clearMarkHighlight 가 호출됨 */
    const srBadge = document.getElementById("search-result-badge");
    if (srBadge) srBadge.style.display = "none";
  }
});

/* ══════════════════════════════════════════════════════════════
   다이얼로그 / 토스트 (원본 로직)
   ══════════════════════════════════════════════════════════════ */
function showDialog({ icon = "⚠️", title = "", message = "", buttons = [] }) {
  return new Promise(resolve => {
    const modal   = document.getElementById("dialog-modal");
    const iconEl  = document.getElementById("dialog-icon");
    iconEl.textContent = icon;
    document.getElementById("dialog-title").textContent   = title;
    document.getElementById("dialog-message").textContent = message;
    const actEl = document.getElementById("dialog-actions");
    actEl.innerHTML = "";
    buttons.forEach(({ label, cls, value }) => {
      const btn = document.createElement("button");
      btn.textContent = label; btn.className = `dialog-btn ${cls}`;
      btn.onclick = () => {
        modal.classList.remove("active");
        setTimeout(() => { modal.style.display = "none"; }, 200);
        resolve(value);
      };
      actEl.appendChild(btn);
    });
    /* 버튼이 3개 이상이면 세로 스택 레이아웃으로 전환(가독성↑) — 2개 이하 기존 다이얼로그는 영향 없음 */
    actEl.classList.toggle("dialog-actions-stack", buttons.length > 2);
    /* 위험(삭제 등) 버튼이 포함된 다이얼로그는 아이콘 배지를 위험색 톤으로 강조 */
    iconEl.classList.toggle("dialog-icon-danger", buttons.some(b => (b.cls || "").includes("danger")));
    modal.style.display = "flex";
    requestAnimationFrame(() => modal.classList.add("active"));
  });
}
function showAlert(msg, icon = "ℹ️", title = "알림") {
  return showDialog({ icon, title, message: msg, buttons: [{ label: "확인", cls: "dialog-btn-ok", value: true }] });
}
function showConfirm(msg, { icon = "⚠️", title = "확인", okLabel = "확인", okCls = "dialog-btn-ok", cancelLabel = "취소" } = {}) {
  return showDialog({ icon, title, message: msg, buttons: [
    { label: cancelLabel, cls: "dialog-btn-cancel", value: false },
    { label: okLabel,     cls: okCls,               value: true  },
  ]});
}
function showToast(msg, type = "info", dur = 3000) {
  let c = document.getElementById("toast-container");
  if (!c) { c = document.createElement("div"); c.id = "toast-container"; document.body.appendChild(c); }
  const t = document.createElement("div");
  t.className = `toast toast-${type}`; t.innerHTML = msg;
  c.appendChild(t);
  requestAnimationFrame(() => t.classList.add("toast-show"));
  setTimeout(() => {
    t.classList.remove("toast-show");
    t.addEventListener("transitionend", () => t.remove(), { once: true });
  }, dur);
}

/* ══════════════════════════════════════════════════════════════
   인쇄 전 확인 체크리스트
   ── 브라우저 인쇄 대화상자의 "여백"이 기본값이면 브라우저가 자체
      여백을 더 얹고, "배율"이 기본값이면 자동 축소가 걸릴 수 있다.
      이 경우 CSS를 mm 단위로 아무리 정확히 맞춰도 실제 라벨지와
      어긋나게 인쇄된다. 인쇄 버튼(정식 인쇄·테스트 인쇄 공통)을
      누르는 시점에 "여백: 없음 / 배율: 100%(사용자 지정)"를 확인하는
      체크리스트를 띄워 이 문제를 예방한다.
   ── "다시 보지 않기"를 체크한 채 확인하면 localStorage에 저장되어
      다음 인쇄부터는 이 모달 없이 바로 진행된다. (주소라벨·일반라벨
      양쪽 인쇄 진입점이 이 함수 하나를 공유하므로 한쪽에서 "다시
      보지 않기"로 끄면 다른 탭에서도 함께 꺼진다)
   ══════════════════════════════════════════════════════════════ */
const PRINT_CHECKLIST_DISMISS_KEY = "printChecklistDismissed";
function confirmPrintSettings() {
  const startOffset = getStoredStartOffset(templateSelect.value);
  const dismissed = localStorage.getItem(PRINT_CHECKLIST_DISMISS_KEY) === "true";
  /* [신규] "다시 보지 않기"로 꺼둔 사람이라도, 시작 위치를 건너뛴 채
     인쇄하려는 순간에는 다시 확인시킨다 — 시작 위치는 실수하면 이미
     쓴 라벨에 겹쳐 찍히거나 멀쩡한 칸을 낭비하게 되는 고위험 설정이라
     "다시 보지 않기"의 대상에서 제외한다. */
  if (dismissed && startOffset === 0) {
    return Promise.resolve(true);
  }
  return new Promise(resolve => {
    const modal      = document.getElementById("print-checklist-modal");
    const okBtn      = document.getElementById("print-checklist-ok");
    const cancelBtn  = document.getElementById("print-checklist-cancel");
    const rememberCb = document.getElementById("print-checklist-remember-cb");
    const rememberRow= document.getElementById("print-checklist-remember-row");
    const startRow   = document.getElementById("print-checklist-startpos-row");
    const startVal   = document.getElementById("print-checklist-startpos-value");
    const marginRows = modal?.querySelectorAll(".print-checklist-item-margin");
    if (!modal || !okBtn || !cancelBtn || !rememberCb) { resolve(true); return; } /* 방어 코드 — 모달을 못 찾으면 인쇄를 막지 않음 */
    rememberCb.checked = false;

    if (startRow) {
      if (startOffset > 0) {
        startRow.style.display = "";
        if (startVal) startVal.textContent = `${startOffset + 1}번째 칸부터`;
      } else {
        startRow.style.display = "none";
      }
    }
    /* 여백/배율은 이미 "다시 보지 않기"로 확인 끝난 사람에게는 다시
       보여줄 필요가 없다 — 이번엔 시작 위치만 재확인하면 된다. */
    const onlyStartPos = dismissed && startOffset > 0;
    marginRows?.forEach(r => { r.style.display = onlyStartPos ? "none" : ""; });
    if (rememberRow) rememberRow.style.display = onlyStartPos ? "none" : "";

    function close(result) {
      modal.classList.remove("active");
      setTimeout(() => { modal.style.display = "none"; }, 200);
      okBtn.removeEventListener("click", onOk);
      cancelBtn.removeEventListener("click", onCancel);
      if (result && rememberCb.checked) localStorage.setItem(PRINT_CHECKLIST_DISMISS_KEY, "true");
      resolve(result);
    }
    function onOk()     { close(true); }
    function onCancel() { close(false); }
    okBtn.addEventListener("click", onOk);
    cancelBtn.addEventListener("click", onCancel);

    modal.style.display = "flex";
    requestAnimationFrame(() => modal.classList.add("active"));
  });
}

/* ══════════════════════════════════════════════════════════════
   유틸
   ══════════════════════════════════════════════════════════════ */
function debounce(fn, ms) { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; }
function escHtml(s) { return String(s).replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;"); }
function scrollToLabel(el, doFocus = false) {
  requestAnimationFrame(() => requestAnimationFrame(() => {
    if (!el?.isConnected) return;
    el.scrollIntoView({ behavior: "smooth", block: "center" });
    el.classList.add("label-highlight");
    setTimeout(() => el.classList.remove("label-highlight"), 1500);
    if (doFocus) el.querySelector(".addr")?.focus();
  }));
}
/* 성능 개선: 기존엔 Promise.all로 N개씩 "고정 배치"를 처리했음.
   고정 배치 방식은 배치 안에서 가장 느린 요청 1건이 끝날 때까지
   나머지 슬롯이 놀면서 다음 배치를 시작하지 못해, 500건처럼 큰
   목록에서 배치 경계마다 불필요한 대기가 누적됐다.
   → "동시성 워커 풀" 방식으로 교체: 정해진 수(concurrency)의 워커가
     각자 큐에서 다음 항목을 즉시 가져가므로, 어떤 슬롯이 먼저
     끝나든 곧바로 다음 항목을 시작한다. 외부 시그니처(items, size, fn)
     와 반환값(원래 순서를 보존한 배열)은 그대로라 호출부 수정이 필요 없다. */
async function fetchInBatches(items, concurrency, fn) {
  const results = new Array(items.length);
  let nextIndex = 0;
  async function worker() {
    while (nextIndex < items.length) {
      const i = nextIndex++;
      results[i] = await fn(items[i], i);
    }
  }
  const workerCount = Math.max(1, Math.min(concurrency, items.length));
  await Promise.all(Array.from({ length: workerCount }, worker));
  return results;
}

/* ══════════════════════════════════════════════════════════════
   상태 표시
   ══════════════════════════════════════════════════════════════ */
function updateLabelCount() {
  if (searchInput.value.trim()) return;
  const labels = postLabelContainer.querySelectorAll(".label");
  const cnt    = labels.length;
  const perPage = TEMPLATE_CONFIGS[templateSelect.value]?.perPage || 16;
  /* 시작 위치(건너뛴 칸)만큼 첫 페이지 용량이 줄어드는 것까지 반영해야
     실제로 인쇄되는 페이지 수(rebuildPages의 total)와 항상 일치한다. */
  const startOffset = Math.min(getStoredStartOffset(templateSelect.value), perPage - 1);
  const pages  = cnt > 0 ? Math.ceil((cnt + startOffset) / perPage) : 0;
  if (cnt > 0) {
    statusBadge.textContent = `총 ${cnt}개 · ${pages}페이지`;
    statusBadge.classList.remove("empty");
  } else {
    statusBadge.textContent = "라벨 없음";
    statusBadge.classList.add("empty");
  }

  // sb-page-info (숨김 span — 기존 로직 유지)
  const pageInfoText = cnt > 0 ? `페이지 1 / ${pages}  ·  라벨 ${cnt}개` : "페이지 0 / 0  ·  라벨 0개";
  document.getElementById("sb-page-info").textContent = pageInfoText;

  // 사이드바 스탯 카드 직접 업데이트 (신규 HTML 요소)
  const lblCountEl = document.getElementById("sb-label-count");
  const pgCountEl  = document.getElementById("sb-page-count");
  const pgInfoEl   = document.getElementById("sb-page-info-display");
  if (lblCountEl) lblCountEl.textContent = cnt > 0 ? `${cnt}개` : "0개";
  if (pgCountEl)  pgCountEl.textContent  = pages > 0 ? `${pages}페이지` : "0페이지";
  if (pgInfoEl)   pgInfoEl.textContent   = cnt > 0 ? `페이지 1 / ${pages} · 라벨 ${cnt}개` : "페이지 0 / 0 · 라벨 0개";

  // 상태바 규격명 업데이트
  const tplNameEl = document.getElementById("sb-template-name");
  if (tplNameEl) {
    const c = TEMPLATE_CONFIGS[templateSelect.value];
    const opt = templateSelect.options[templateSelect.selectedIndex];
    const shortName = opt?.dataset?.short || (c ? `${c.maker} ${c.model}` : "");
    tplNameEl.textContent = shortName;
  }

  // 선택된 라벨 수 표시
  const selLabels = postLabelContainer.querySelectorAll(".label.label-selected");
  const selSep = document.getElementById("sb-sel-sep");
  const selInfo = document.getElementById("sb-sel-info");
  if (selLabels.length > 0 && selSep && selInfo) {
    selSep.style.display = "";
    selInfo.style.display = "";
    selInfo.textContent = `${selLabels.length}개 선택됨`;
  } else if (selSep && selInfo) {
    selSep.style.display = "none";
    selInfo.style.display = "none";
  }

  uploadZone.style.display = cnt === 0 ? "" : "none";

  // 사이드바 실행취소 뱃지
  const undoBadge = document.getElementById("sb-undo-badge");
  if (undoBadge) undoBadge.textContent = undoStack.length || "";
  if (undoBadge) undoBadge.style.display = undoStack.length ? "" : "none";

  /* [신규] 1단계(데이터) 화면의 주소 목록 표/요약도 항상 최신 상태로 유지 */
  if (typeof window.refreshDataStepView === "function") window.refreshDataStepView();
}

/* ══════════════════════════════════════════════════════════════
   카카오 우편번호 API (원본 LRU 캐시 로직)
   성능/안정성 개선:
     1) 개별 요청에 8초 타임아웃 적용 — 응답이 없는 요청 하나가
        워커 슬롯을 무한정 점유해 전체 처리를 지연시키는 것을 방지.
     2) 429(Too Many Requests) 응답 시 지수 백오프로 최대 3회 재시도.
        동시 처리 개수를 늘리면 순간적으로 요청이 몰려 429가 날 수
        있는데, 재시도 없이는 그 항목이 그냥 "우편번호 실패"로 빈칸
        처리되어 버린다. 재시도를 넣어야 동시성을 올린 만큼 실제로도
        더 빠르고 정확해진다.
   ══════════════════════════════════════════════════════════════ */

/* ══════════════════════════════════════════════════════════════
   우편번호 수동 수정 지원 — 헬퍼 함수
   다음/카카오 API에 주소가 등록되어 있지 않아 자동 조회가 실패하는
   경우(빈칸 "     ")를 대비해, 우편번호 5칸을 직접 입력/수정할 수
   있도록 contenteditable을 부여한다. 기존 5곳의 렌더링 코드에서
   공통으로 사용.
   ══════════════════════════════════════════════════════════════ */
function digitsHTML(code) {
  /* 각 칸은 항상 "숫자 1글자" 또는 "공백 1글자"만 갖도록 유지한다.
     (원본 로직이 5칸을 textContent 이어붙이기로 읽으므로, 빈 칸을 완전히
      비워두면 자리가 밀려 우편번호 자릿수가 깨진다 — 그래서 공백 유지) */
  return (code || "     ").padEnd(5, " ").slice(0, 5).split("")
    .map(d => `<div class="post-digit" contenteditable="true" inputmode="numeric" spellcheck="false">${d}</div>`)
    .join("");
}
/* 잠금 상태에 맞춰 우편번호 칸의 편집 가능 여부를 동기화 */
function syncPostcodeLock(labelEl) {
  if (!labelEl) return;
  const locked = labelEl.classList.contains("label-locked");
  labelEl.querySelectorAll(".post-digit").forEach(d => { d.contentEditable = locked ? "false" : "true"; });
}

async function fetchPostCode(address) {
  if (!address?.trim()) return "     ";
  if (addressCache.has(address)) {
    const v = addressCache.get(address);
    addressCache.delete(address); addressCache.set(address, v); return v;
  }
  if (addressInFlight.has(address)) return addressInFlight.get(address);
  const req = (async () => {
    const MAX_RETRY = 3;
    try {
      for (let attempt = 0; attempt <= MAX_RETRY; attempt++) {
        const controller = new AbortController();
        const timeoutId  = setTimeout(() => controller.abort(), 8000);
        try {
          const res = await fetch(
            `https://dapi.kakao.com/v2/local/search/address?query=${encodeURIComponent(address)}`,
            { headers: { Authorization: KAKAO_API_KEY }, signal: controller.signal }
          );
          clearTimeout(timeoutId);
          if (res.status === 429 && attempt < MAX_RETRY) {
            const wait = 250 * (2 ** attempt) + Math.random() * 120;
            await new Promise(r => setTimeout(r, wait));
            continue;
          }
          if (!res.ok) throw new Error("HTTP " + res.status);
          const data = await res.json();
          const code = data.documents?.[0]?.road_address?.zone_no
                    || data.documents?.[0]?.address?.zone_no
                    || "     ";
          if (addressCache.size >= ADDRESS_CACHE_MAX)
            addressCache.delete(addressCache.keys().next().value);
          addressCache.set(address, code);
          return code;
        } catch (err) {
          clearTimeout(timeoutId);
          if (err.name === "AbortError" && attempt < MAX_RETRY) continue; /* 타임아웃 → 재시도 */
          if (attempt >= MAX_RETRY) return "     ";
        }
      }
      return "     ";
    } finally {
      addressInFlight.delete(address);
    }
  })();
  addressInFlight.set(address, req); return req;
}

function appendGuiha(nameEl) {
  const text     = nameEl.innerText.trim();
  const stripped = text.replace(/\s/g, "");
  if (!stripped || stripped === "이름입력" || stripped.endsWith("귀하")) return;
  nameEl.innerText = text + " 귀하";
  const r = document.createRange(), s = window.getSelection();
  r.selectNodeContents(nameEl); r.collapse(false); s.removeAllRanges(); s.addRange(r);
}
function updateEmptyLabelClass(el) {
  if (!el) return;
  const addr = el.querySelector(".addr")?.textContent?.trim() || "";
  const name = el.querySelector(".name")?.textContent?.trim() || "";
  el.classList.toggle("label-empty", !addr && !name);
}
function updateLabelIndexBadges() {
  /* 성능: 기존 뱃지 텍스트와 같으면 DOM 쓰기 생략 */
  postLabelContainer.querySelectorAll(".label").forEach((l, i) => {
    let b = l.querySelector(".label-index-badge");
    const newText = `#${i + 1}`;
    if (!b) {
      b = document.createElement("span");
      b.className = "label-index-badge";
      l.appendChild(b);
    }
    if (b.textContent !== newText) b.textContent = newText;
  });
}

/* ══════════════════════════════════════════════════════════════
   라벨 DOM 생성 (원본 완전 동일)
   ══════════════════════════════════════════════════════════════ */
/* [이벤트 위임으로 교체 — 이유: 예전에는 lock-label-btn 클릭 이벤트를
      createLabel()에서 버튼 하나하나에 직접 addEventListener로 걸었다.
      그래서 postLabelContainer.innerHTML을 통째로 교체하는 경로(작업
      복원, 전체 초기화 후 재구성 등)를 지날 때마다 버튼이 새 DOM 노드로
      바뀌며 이벤트가 사라져, enableDrag()처럼 "다시 호출해서 재바인딩"
      하는 함수(enableLockBtn)를 만들어 호출 지점마다(createLabel,
      reflowPages, 복원 로직 …) 일일이 불러줘야 했다. 실제로 undo(삭제
      되돌리기) 경로 등 일부는 그 호출이 누락돼 있었다.
      delete-btn/copy-label-btn은 이미 postLabelContainer 하나에 위임된
      클릭 핸들러(아래쪽, "다중 선택" 주석 부근)로 처리되고 있었으므로,
      lock-label-btn도 별도 리스너를 새로 추가하지 않고 그 핸들러 안에
      같은 방식으로 합류시킨다 — 아래 postLabelContainer.addEventListener
      ("click", …) 내부의 lock-label-btn 분기를 참고. */

function createLabel(item) {
  const addr     = item["주소"] || "";
  let   nameText = item["이름"] || "";
  if (nameText && !nameText.endsWith("귀하") && nameText !== "이름 입력")
    nameText += " 귀하";
  const isLocked = !!item["locked"];
  const el       = document.createElement("div");
  el.className   = "label"
    + (!addr && !(item["이름"]) ? " label-empty" : "")
    + (isLocked ? " label-locked" : "");
  el.innerHTML = `
    <img class="label-watermark-img" src="./character.png" draggable="false" alt="">
    <span class="drag-handle" title="드래그하여 순서 변경" aria-hidden="true">⠿</span>
    <button class="delete-btn"     title="삭제 (Del)" aria-label="라벨 삭제">×</button>
    <button class="copy-label-btn" title="라벨 복사 (Ctrl+D)" aria-label="라벨 복사">⧉</button>
    <button class="lock-label-btn" title="잠금/해제 (Alt+L)" aria-label="${isLocked ? "잠금 해제" : "라벨 잠금"}">${isLocked ? "🔒" : "🔓"}</button>
    <p class="addr" contenteditable="${isLocked ? "false" : "true"}" spellcheck="false">${escHtml(addr)}</p>
    <p class="name" contenteditable="${isLocked ? "false" : "true"}" spellcheck="false">${escHtml(nameText)}</p>
    <div class="post-code-container">${digitsHTML(item.postCode)}</div>`;

  syncPostcodeLock(el); /* 잠긴 라벨이면 우편번호 칸도 편집 불가로 초기화 */
  enableDrag(el); return el;
}

/* ══════════════════════════════════════════════════════════════
   IndexedDB 기반 임시 저장소 (구 sessionStorage 대체)
   ── 왜 바꿨나: sessionStorage는 브라우저별로 5~10MB 제한이 있어,
     라벨 수가 많아지거나(특히 이미지가 들어가는 칸이 있으면 base64라
     원본보다 ~33% 더 커짐) 금방 한도에 부딪힌다. 한 번 한도를 넘기면
     그 이후의 모든 편집이 "저장 실패" 토스트만 띄운 채 저장되지 않는
     채로 쌓여, 작업 내용이 소리 없이 날아갈 수 있었다.
     IndexedDB는 사실상 무제한(브라우저 여유 공간 기준 수백MB~)이라
     이 문제에서 자유롭다.
   ── [버그 수정] 예전엔 "세션(탭) 종료 시 자동 소멸"을 흉내내려고
     sessionStorage 플래그가 없으면(=브라우저/탭을 완전히 닫았다가 다시
     연 경우) 저장된 데이터를 자동으로 지웠다. 그런데 "이전 작업
     이어하기"가 가장 필요한 상황이 바로 그 경우라서, 매번 복원할
     데이터가 사라져 기능 자체가 무용지물이었다. 개인정보 보호는 이미
     있는 "전체 초기화" 버튼(명시적 idbDelete 호출)으로 충분하다고 보고
     자동 소멸 로직은 제거했다 — 이제 사용자가 직접 지우기 전까지
     평범하게 남아있는다. SESSION_FLAG/idbClearAll은 더 쓰이진 않지만,
     혹시 필요해질 때를 위해 남겨둔다.
   ══════════════════════════════════════════════════════════════ */
const IDB_NAME     = "postLabelAppStore";
const IDB_STORE    = "kv";
const SESSION_FLAG = "plt-session-active"; /* 더 이상 자동 소멸 트리거로 쓰이지 않음 */
let _idbOpenPromise = null;

function idbOpen() {
  if (_idbOpenPromise) return _idbOpenPromise;
  _idbOpenPromise = new Promise((resolve, reject) => {
    if (!("indexedDB" in window)) { reject(new Error("IndexedDB unsupported")); return; }
    const req = indexedDB.open(IDB_NAME, 1);
    req.onupgradeneeded = () => { req.result.createObjectStore(IDB_STORE); };
    req.onsuccess = () => resolve(req.result);
    req.onerror   = () => reject(req.error);
  });
  return _idbOpenPromise;
}
async function idbSet(key, value) {
  const db = await idbOpen();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(IDB_STORE, "readwrite");
    tx.objectStore(IDB_STORE).put(value, key);
    tx.oncomplete = () => resolve(true);
    tx.onerror    = () => reject(tx.error);
  });
}
async function idbGet(key) {
  const db = await idbOpen();
  return new Promise((resolve, reject) => {
    const tx  = db.transaction(IDB_STORE, "readonly");
    const req = tx.objectStore(IDB_STORE).get(key);
    req.onsuccess = () => resolve(req.result);
    req.onerror   = () => reject(req.error);
  });
}
async function idbDelete(key) {
  const db = await idbOpen();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(IDB_STORE, "readwrite");
    tx.objectStore(IDB_STORE).delete(key);
    tx.oncomplete = () => resolve(true);
    tx.onerror    = () => reject(tx.error);
  });
}
async function idbClearAll() {
  const db = await idbOpen();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(IDB_STORE, "readwrite");
    tx.objectStore(IDB_STORE).clear();
    tx.oncomplete = () => resolve(true);
    tx.onerror    = () => reject(tx.error);
  });
}
/* [버그 수정] 예전에는 sessionStorage 플래그가 없으면(=브라우저/탭을
   완전히 닫았다가 다시 연 경우) "이전 세션 종료"로 간주해 IndexedDB에
   저장된 작업 내용을 자동으로 지워버렸다. 그런데 "이전 작업 이어하기"를
   가장 필요로 하는 상황이 정확히 그 경우(창을 닫고 나중에 다시 열어
   이어서 작업하는 것)라서, 매번 복원할 데이터가 자동으로 사라져 버튼
   자체가 아예 뜨지 않는 문제가 있었다("복원 기능이 있었는데 동작을
   안 한다"는 피드백의 원인). 개인정보 보호가 필요하면 이미 있는
   "전체 초기화" 버튼(명시적으로 idbDelete를 호출함)으로 충분하다고
   판단해, 세션 경계마다 자동으로 지우는 동작은 없앴다 — 이제 IndexedDB는
   사용자가 직접 지우기 전까지 평범하게 남아있는다. */
const idbSessionReady = Promise.resolve();

/* ── 저장 상태 표시 헬퍼 ──────────────────────────── */
function _showSaveStatus(state, text) {
  const el = document.getElementById("sb-autosave");
  if (!el) return;
  el.className = "statusbar-autosave " + state;
  el.textContent = text;
  el.style.display = "";
}

function saveToStorage() {
  /* ── 변경 감지 — innerHTML 길이+샘플로 빠른 비교 */
  const html = postLabelContainer.innerHTML;
  const tpl  = templateSelect.value;
  const fingerprint = html.length + "|" + (html.slice(0,120) || "") + "|" + tpl;
  if (fingerprint === saveToStorage._lastFingerprint) return; /* 변경 없으면 스킵 */

  _showSaveStatus("saving", "저장 중...");
  /* IndexedDB는 비동기라 완료를 기다리지 않고도 다음 편집을 계속할 수 있다.
     단, beforeunload에서 호출되는 마지막 저장은 완료를 보장할 수 없는
     best-effort다 (600ms 디바운스 + Ctrl+S로 대부분 이미 저장되어 있음). */
  idbSessionReady
    .then(() => Promise.all([ idbSet("lastLabels", html), idbSet("lastTemplate", tpl) ]))
    .then(() => {
      saveToStorage._lastFingerprint = fingerprint;
      saveToStorage._lastSavedAt     = Date.now();
      const t = new Date();
      const hhmm = t.getHours().toString().padStart(2,"0") + ":" + t.getMinutes().toString().padStart(2,"0");
      _showSaveStatus("saved", `자동 저장됨 ${hhmm}`);
    })
    .catch(() => {
      _showSaveStatus("error", "저장 실패");
      showToast("⚠️ 저장에 실패했습니다.", "warn", 4000);
    });
}
saveToStorage._lastFingerprint = null;
saveToStorage._lastSavedAt     = null;

/* ══════════════════════════════════════════════════════════════
   드래그 앤 드롭 (원본 완전 동일)
   ══════════════════════════════════════════════════════════════ */
let dragSrcLabel = null;
function enableDrag(el) {
  /* 초기에는 draggable=false — 핸들 mousedown 시에만 true로 켬 */
  el.setAttribute("draggable", "false");

  /* 핸들 mousedown → draggable 활성화, 그 외 → 비활성 */
  el.addEventListener("mousedown", e => {
    if (e.target.closest(".drag-handle") && !el.classList.contains("label-locked")) {
      el.setAttribute("draggable", "true");
    } else {
      el.setAttribute("draggable", "false");
    }
  });

  /* mouseup — 드래그 없이 클릭만 한 경우 draggable 원복 */
  el.addEventListener("mouseup", () => {
    el.setAttribute("draggable", "false");
  });

  el.addEventListener("dragstart",  onDragStart);
  el.addEventListener("dragend",    onDragEnd);
  el.addEventListener("dragover",   onDragOver);
  el.addEventListener("dragleave",  onDragLeave);
  el.addEventListener("drop",       onLabelDrop);
}
function onDragStart(e) {
  /* draggable=true는 핸들 mousedown에서만 설정되므로 추가 가드 불필요 */
  if (this.classList.contains("label-locked")) { e.preventDefault(); return; }
  dragSrcLabel = this;
  e.dataTransfer.effectAllowed = "move";
  e.dataTransfer.setData("text/plain", "drag");
  requestAnimationFrame(() => { if (dragSrcLabel) dragSrcLabel.classList.add("dragging"); });
}
function onDragEnd() {
  this.classList.remove("dragging");
  this.setAttribute("draggable", "false"); /* 드래그 완료 후 항상 원복 */
  postLabelContainer.querySelectorAll(".label").forEach(l =>
    l.classList.remove("drag-over"));
  dragSrcLabel = null;
}
function onDragOver(e) {
  e.preventDefault(); e.stopPropagation();
  if (!dragSrcLabel || this === dragSrcLabel) return;
  this.classList.add("drag-over");
}
function onDragLeave(e) {
  if (this.contains(e.relatedTarget)) return;
  this.classList.remove("drag-over");
}
function onLabelDrop(e) {
  e.preventDefault(); e.stopPropagation();
  this.classList.remove("drag-over");
  if (!dragSrcLabel || dragSrcLabel === this) return;
  const all    = Array.from(postLabelContainer.querySelectorAll(".label"));
  const srcIdx = all.indexOf(dragSrcLabel);
  const tgtIdx = all.indexOf(this);
  if (srcIdx < 0 || tgtIdx < 0) return;
  const rect = this.getBoundingClientRect(), srcRect = dragSrcLabel.getBoundingClientRect();
  const rowOverlap    = Math.min(srcRect.bottom, rect.bottom) - Math.max(srcRect.top, rect.top);
  const sameRow       = rowOverlap > rect.height * 0.3;
  const insertBefore  = sameRow ? e.clientX < rect.left + rect.width / 2 : srcIdx > tgtIdx;
  if (insertBefore) this.parentNode.insertBefore(dragSrcLabel, this);
  else {
    const next = this.nextElementSibling;
    next ? this.parentNode.insertBefore(dragSrcLabel, next) : this.parentNode.appendChild(dragSrcLabel);
  }
  saveToStorage(); reflowPages(); updateLabelCount();
}

/* ══════════════════════════════════════════════════════════════
   성능 최적화 유틸
   ══════════════════════════════════════════════════════════════ */
function throttle(fn, ms) {
  let last = 0, raf = null;
  return function(...a) {
    const now = Date.now();
    if (now - last >= ms) { last = now; fn.apply(this, a); }
    else { clearTimeout(raf); raf = setTimeout(() => { last = Date.now(); fn.apply(this, a); }, ms); }
  };
}

/* ── 대량 DOM 삽입을 청크 단위로 rAF에 분산 (UI 블로킹 방지) */
function insertChunked(frag, container, chunkSize, onDone) {
  const children = Array.from(frag.childNodes);
  let idx = 0;
  function step() {
    const end = Math.min(idx + chunkSize, children.length);
    const chunk = document.createDocumentFragment();
    for (; idx < end; idx++) chunk.appendChild(children[idx]);
    container.appendChild(chunk);
    if (idx < children.length) requestAnimationFrame(step);
    else if (onDone) onDone();
  }
  requestAnimationFrame(step);
}

/* ── 검색용 텍스트 캐시 (라벨 재생성 없이 빠른 필터링) */
const _labelTextCache = new WeakMap();
function getLabelText(label) {
  if (_labelTextCache.has(label)) return _labelTextCache.get(label);
  const name = (label.querySelector(".name")?.textContent || "").toLowerCase();
  const addr = (label.querySelector(".addr")?.textContent || "").toLowerCase();
  const zip  = Array.from(label.querySelectorAll(".post-digit")).map(d => d.textContent).join("").toLowerCase();
  const val  = { name, addr, zip };
  _labelTextCache.set(label, val);
  return val;
}
/* 라벨 편집 시 캐시 무효화 */
postLabelContainer.addEventListener("input", e => {
  const label = e.target.closest(".label");
  if (label) _labelTextCache.delete(label);
}, true);
/* [버그 수정] 열(column) 간 가로 간격 계산
   ──────────────────────────────────────────────────────────────
   기존에는 각 규격의 rowGap 값을 그대로 column-gap에 적용했다.
   하지만 대부분의 규격(예: 3107, 3105, 3510 등)은 pl(좌측 여백)과
   w(라벨 너비)만으로 이미 A4 폭(210mm)에 정확히 맞도록 설계되어
   있어서, 간격(rowGap)을 추가로 더하면 그만큼 페이지 실제 너비가
   210mm를 초과해 버렸다 — 이것이 "라벨 DB(TEMPLATE_CONFIGS) 값은
   그대로인데 화면(캔버스)에서 라벨이 페이지 밖으로 삐져나오고,
   인쇄 시에는 210mm로 강제 고정되어 있어 그 초과분만큼 뒤 열이
   잘리거나 밀려 보이던" 문제의 원인이었다.
   → 실제로 남는 여유 공간(leftover)을 계산해, "규격에 정의된
   간격(rowGap)"과 "실제로 남는 공간" 중 더 작은 값만 적용한다.
   여유 공간이 없는 규격(대다수)은 간격이 자동으로 0이 되어 정확히
   210mm에 맞고, 애초에 열 간격을 감안해 설계된 규격(ANI_* 등)은
   기존과 동일하게 그 간격이 그대로 유지된다. */
function getSafeColumnGapMM(c) {
  const colN = c.colN || parseInt((c.cols || "").match(/repeat\((\d+)/)?.[1] || "1", 10);
  if (colN <= 1) return 0;
  const declared = parseFloat(c.rowGap || "0") || 0;
  const pl = parseFloat(c.pl) || 0;
  const w  = parseFloat(c.w)  || 0;
  const leftover = Math.max(0, (210 - 2 * pl - colN * w) / (colN - 1));
  return Math.min(declared, leftover);
}
/* [버그 수정] 하단 여백(pb) — getSafeColumnGapMM()과 같은 이유로, 세로
   방향도 규격에 손으로 적어둔 pb 값을 그대로 믿지 않고 "실제로 남는
   공간"을 계산해서 쓴다. pt(상단) + 라벨H×행수 + pb(하단)의 합이
   297mm를 넘으면 인쇄 시 페이지가 297mm로 강제 고정되면서 마지막 줄
   라벨이 페이지 밖으로 밀려나거나 잘린다(가로 열 넘침과 동일한 원인의
   세로 버전). 실제로 3109(0.9mm 부족)·3114(4.4mm 초과)·A3105(2mm 초과)
   규격에서 pb 값이 옮겨 적는 과정의 오탈자로 이 공식과 어긋나 있었다. */
function getSafePaddingBottomMM(c) {
  const pt   = parseFloat(c.pt) || 0;
  const rowN = c.rowN || parseInt((c.rows || "").match(/repeat\((\d+)/)?.[1] || "1", 10);
  const h    = parseFloat(c.h) || 0;
  return Math.max(0, 297 - pt - rowN * h);
}
/* ── 인쇄 위치 미세조정 값 저장소 (템플릿별) ──────────────────────
   [수정] 기존에는 offsetPt/offsetPl이 단순 JS 변수(let)라서 새로고침은
   물론, 라벨 추가/삭제 등으로 reflowPages()→rebuildPages()→applyConfig()가
   다시 호출될 때마다 화면에는 보정값이 조용히 초기화되는 문제가 있었다
   (표시 텍스트는 그대로 "+0.25mm"인데 실제 --page-pt는 기준값으로 되돌아감).
   → 프린터마다 급지 위치가 다르므로, 규격(템플릿) 키 하나당 보정값 하나를
   localStorage에 저장해두고, applyConfig()가 호출될 때마다 "기준값 + 저장된
   보정값"을 매번 다시 계산하도록 해 단일 진실 공급원으로 통합함. 이렇게 하면
   reflowPages 등 applyConfig()를 다시 부르는 모든 경로에서도 보정값이
   사라지지 않고, 새로고침·다른 규격 이동 후 복귀에도 그대로 유지된다. */
const PRINT_OFFSET_STORAGE_KEY = "printOffsets"; // { [templateKey]: { pt, pl } } (mm 단위)
function getStoredOffset(templateKey) {
  let all = {};
  try { all = JSON.parse(localStorage.getItem(PRINT_OFFSET_STORAGE_KEY)) || {}; } catch {}
  const saved = all[templateKey];
  return { pt: Number(saved?.pt) || 0, pl: Number(saved?.pl) || 0 };
}
function setStoredOffset(templateKey, pt, pl) {
  let all = {};
  try { all = JSON.parse(localStorage.getItem(PRINT_OFFSET_STORAGE_KEY)) || {}; } catch {}
  if (pt === 0 && pl === 0) delete all[templateKey]; // 보정 없음 상태는 아예 저장하지 않아 데이터 정리
  else all[templateKey] = { pt, pl };
  try { localStorage.setItem(PRINT_OFFSET_STORAGE_KEY, JSON.stringify(all)); } catch {}
}
/* ── 시작 위치(신규) ────────────────────────────────────────────
   이미 일부 칸이 사용된 라벨지를 재활용할 때, 남은 칸부터 인쇄를
   시작할 수 있도록 "건너뛸 칸 수"를 규격(템플릿)별로 저장한다.
   0이면 첫 칸부터(기본). 이 값이 적용되는 곳은 두 군데:
   (1) rebuildPages() — 첫 페이지 맨 앞에 건너뛸 칸 수만큼 빈
       스페이서(.label-spacer)를 실제로 끼워 넣어, 물리적으로 그
       칸들이 비워진 채 인쇄되고 그 다음 칸부터 실제 데이터가 찍힌다.
   (2) renderAddrGridPreview() — 미리보기에서도 동일하게 앞쪽 칸을
       "건너뜀" 상태로 표시해 실제 인쇄 결과와 미리보기가 항상
       일치하도록 한다. */
const START_OFFSET_STORAGE_KEY = "labelStartOffsets"; // { [templateKey]: N } (0-based 건너뛸 칸 수)
function getStoredStartOffset(templateKey) {
  let all = {};
  try { all = JSON.parse(localStorage.getItem(START_OFFSET_STORAGE_KEY)) || {}; } catch {}
  return Math.max(0, Number(all[templateKey]) || 0);
}
function setStoredStartOffset(templateKey, n) {
  let all = {};
  try { all = JSON.parse(localStorage.getItem(START_OFFSET_STORAGE_KEY)) || {}; } catch {}
  if (!n) delete all[templateKey]; // 0(첫칸)은 저장하지 않아 데이터 정리
  else all[templateKey] = n;
  try { localStorage.setItem(START_OFFSET_STORAGE_KEY, JSON.stringify(all)); } catch {}
}
function updateOffsetDisplay(offsetPt, offsetPl) {
  const ptEl = document.getElementById("offset-pt-display");
  const plEl = document.getElementById("offset-pl-display");
  const miniPtEl = document.getElementById("mini-offset-pt-display");
  const miniPlEl = document.getElementById("mini-offset-pl-display");
  const sign = v => (v >= 0 ? "+" : "") + v.toFixed(2) + "mm";
  const color = v => v !== 0 ? "var(--clr-primary)" : "var(--text)";
  if (ptEl) { ptEl.textContent = sign(offsetPt); ptEl.style.color = color(offsetPt); }
  if (plEl) { plEl.textContent = sign(offsetPl); plEl.style.color = color(offsetPl); }
  if (miniPtEl) { miniPtEl.textContent = sign(offsetPt); miniPtEl.style.color = color(offsetPt); }
  if (miniPlEl) { miniPlEl.textContent = sign(offsetPl); miniPlEl.style.color = color(offsetPl); }
  /* [신규] 오프셋이 0이 아니면 미니 버튼 자체도 강조해서, 팝오버를 열지
     않아도 "지금 위치가 조정돼 있다"는 걸 한눈에 알 수 있게 한다. */
  const topbarBtn = document.getElementById("topbar-offset-btn");
  if (topbarBtn) topbarBtn.classList.toggle("has-offset", offsetPt !== 0 || offsetPl !== 0);
}
function applyConfig() {
  const c = TEMPLATE_CONFIGS[templateSelect.value]; if (!c) return;
  const r  = document.documentElement;
  const px = v => (parseFloat(v) || 12) * fontSizeMultiplier + "px";
  const offset       = getStoredOffset(templateSelect.value);
  const effectivePt   = parseFloat(c.pt) + offset.pt;
  const effectivePl   = parseFloat(c.pl) + offset.pl;
  r.style.setProperty("--page-pt",       effectivePt.toFixed(2) + "mm");
  r.style.setProperty("--page-pb",       getSafePaddingBottomMM(Object.assign({}, c, { pt: effectivePt.toFixed(2) + "mm" })).toFixed(2) + "mm");  /* [버그 수정] 규격 데이터를 믿지 않고 항상 297mm에 정확히 맞도록 계산 */
  r.style.setProperty("--page-pl",       effectivePl.toFixed(2) + "mm");
  r.style.setProperty("--grid-cols",     c.cols);
  r.style.setProperty("--grid-rows",     c.rows);
  r.style.setProperty("--label-w",       c.w);
  r.style.setProperty("--label-h",       c.h);
  r.style.setProperty("--label-row-gap", getSafeColumnGapMM(Object.assign({}, c, { pl: effectivePl.toFixed(2) + "mm" })).toFixed(2) + "mm"); /* CSS에서 column-gap 으로 적용 — 210mm를 넘지 않도록 안전하게 계산된 값 */
  r.style.setProperty("--label-padding", c.padding);
  r.style.setProperty("--addr-fs",       px(c.addrFs));
  r.style.setProperty("--name-fs",       px(c.nameFs));
  r.style.setProperty("--digit-fs",      px(c.digitFs));
  r.style.setProperty("--digit-w",       c.digitW);
  r.style.setProperty("--digit-h",       c.digitH);
  updateOffsetDisplay(offset.pt, offset.pl); /* 인스펙터의 미세조정 표시도 항상 최신 상태로 동기화 */
  /* [실시간 WYSIWYG 강화] 기존엔 라벨 배치 미리보기(addr-grid-preview)가
     postLabelContainer의 DOM 변경(MutationObserver, 최대 270ms 지연)에만
     반응해서, 인쇄 위치 미세조정처럼 라벨 목록은 그대로인 채 여백 값만
     바뀌는 경우엔 전혀 갱신되지 않았다. applyConfig()는 템플릿·미세조정
     값이 바뀔 때마다 반드시 호출되는 단일 지점이므로, 여기서 직접(지연
     없이) 다시 그려서 화면에 즉시 반영되도록 한다. */
  if (typeof renderAddrGridPreview === "function") renderAddrGridPreview();
}
function rebuildPages() {
  const c = TEMPLATE_CONFIGS[templateSelect.value]; if (!c) return;
  applyConfig(); updateInspector();
  const labels = Array.from(postLabelContainer.querySelectorAll(".label"));
  if (!labels.length) {
    /* 라벨이 하나도 없으면 A4 용지(page-number-label, a4-page)를 전부 제거하고
       업로드 존이 보이는 초기 화면으로 복귀 */
    postLabelContainer.innerHTML = "";
    updateLabelCount();
    return;
  }

  /* 시작 위치(신규): 첫 페이지 맨 앞에 "건너뛸 칸" 수만큼 빈 스페이서를
     끼워 넣는다 — 이미 사용된 라벨지를 재활용할 때 남은 칸부터 인쇄되도록.
     페이지 수(total) 계산에도 스페이서를 포함시켜야 실제로 찍히는 페이지
     수와 맞아떨어진다. */
  const startOffset = Math.min(getStoredStartOffset(templateSelect.value), c.perPage - 1);
  const renderItems = new Array(startOffset).fill(null).concat(labels); // null = 스페이서(건너뛴 칸)

  const total   = Math.ceil(renderItems.length / c.perPage);
  const frag    = document.createDocumentFragment();
  let   page    = null, pageNum = 0;

  renderItems.forEach((label, i) => {
    if (i % c.perPage === 0) {
      pageNum++;
      const pl = document.createElement("div");
      pl.className   = "page-number-label";
      pl.textContent = `Page ${pageNum} / ${total}`;
      frag.appendChild(pl);
      page = document.createElement("div");
      page.className = "a4-page";
      const wmImg = document.createElement("img");
      wmImg.src = "./character.png";
      wmImg.className = "a4-watermark-img";
      wmImg.setAttribute("aria-hidden", "true");
      page.appendChild(wmImg);
      frag.appendChild(page);
    }
    if (label) {
      enableDrag(label); page.appendChild(label);
    } else {
      /* 건너뛴 칸 — 이미 사용된 스티커 자리이므로 아무것도 인쇄되지 않아야 함 */
      const spacer = document.createElement("div");
      spacer.className = "label-spacer";
      spacer.setAttribute("aria-hidden", "true");
      spacer.title = "건너뛴 칸(이미 사용됨)";
      page.appendChild(spacer);
    }
  });

  /* 성능: 대량 데이터(200개+) 시 청크 삽입으로 메인 스레드 블로킹 방지 */
  const CHUNK_THRESHOLD = 200;
  postLabelContainer.innerHTML = "";

  const doAfter = () => {
    updateLabelIndexBadges();
    postLabelContainer.querySelectorAll(".label").forEach(updateEmptyLabelClass);
    const kw = searchInput.value.trim();
    if (kw) {
      const prev = searchMatchedLabels[searchCurrentIndex] ?? null;
      runSearch(kw);
      if (prev) {
        const r2 = searchMatchedLabels.indexOf(prev);
        if (r2 >= 0) {
          searchCurrentIndex = r2;
          searchMatchedLabels.forEach(l => l.classList.remove("search-active"));
          searchMatchedLabels[r2].classList.add("search-active");
          statusBadge.textContent = `${r2 + 1} / ${searchMatchedLabels.length}개`;
        }
      }
    } else { updateLabelCount(); }
    applyZoom();
  };

  if (labels.length > CHUNK_THRESHOLD) {
    insertChunked(frag, postLabelContainer, c.perPage * 2, doAfter);
  } else {
    postLabelContainer.appendChild(frag);
    doAfter();
  }
}
function reflowPages(full = true) {
  if (full) rebuildPages(); else applyConfig();
  if (viewMode === "fit") {
    const tot = getFitPages().length;
    if (fitCurrentPage >= tot) fitCurrentPage = Math.max(0, tot - 1);
    requestAnimationFrame(() => applyFitPage(fitCurrentPage));
  }
}

/* ══════════════════════════════════════════════════════════════
   가상 스크롤 — Intersection Observer
   뷰포트에서 벗어난 .a4-page 를 visibility:hidden 으로 처리해
   5000건(300+ 페이지)에서도 60fps 유지. 레이아웃(높이)은 그대로.
   ══════════════════════════════════════════════════════════════ */
(function initVirtualScroll() {
  /* 이미 옵저버가 있으면 재사용 — rebuildPages 호출 때마다 재연결 */
  let _vsObs = null;

  function attachVS() {
    if (_vsObs) _vsObs.disconnect();

    const pages = Array.from(postLabelContainer.querySelectorAll(".a4-page"));
    if (pages.length < 4) {
      /* 페이지 수가 적으면 가상 스크롤 불필요 — 모두 visible 복원 */
      pages.forEach(p => p.classList.remove("page-vs-hidden"));
      return;
    }

    /* rootMargin: 뷰포트 위아래로 1페이지(약 1200px) 여유 미리 렌더링 */
    _vsObs = new IntersectionObserver(entries => {
      entries.forEach(entry => {
        entry.target.classList.toggle("page-vs-hidden", !entry.isIntersecting);
      });
    }, { root: canvasScroll, rootMargin: "1200px 0px" });

    pages.forEach(p => _vsObs.observe(p));
  }

  /* rebuildPages 가 끝날 때마다 VS 재연결 (MutationObserver 활용) */
  const _rebuildObs = new MutationObserver(debounce(attachVS, 200));
  _rebuildObs.observe(postLabelContainer, { childList: true });

  /* 초기 연결 */
  window.addEventListener("DOMContentLoaded", () => {
    setTimeout(attachVS, 300);
  });
})();

/* ══════════════════════════════════════════════════════════════
   컬럼 매퍼 모달 — 자동 매핑 키워드 확장 + 자동감지 배지
   ══════════════════════════════════════════════════════════════ */
function showColumnMapper(headers, previewRows) {
  return new Promise((resolve, reject) => {
    const modal   = document.getElementById("column-mapper-modal");
    const nameSel = document.getElementById("name-col-select");
    const addrSel = document.getElementById("addr-col-select");
    const prevEl  = document.getElementById("data-preview-table");

    /* 확장된 자동 인식 키워드 */
    const nameKw = ["이름","성명","민원인","세대주","수취인","고객명","name","받는분",
                    "수령인","받는사람","수신인","주문자","구매자","발송대상","고객","member"];
    const addrKw = ["주소","도로명","지번","수령","배송지","address","addr","받는주소",
                    "배송주소","수취주소","받는곳","거주지","납품지","목적지","delivery"];

    const clean = s => String(s).replace(/\s/g,"").toLowerCase();
    const opts  = () => headers.map(h => `<option value="${escHtml(h)}">${escHtml(h)}</option>`).join("");
    nameSel.innerHTML = opts(); addrSel.innerHTML = opts();

    const autoName = headers.find(h => nameKw.some(k => clean(h).includes(clean(k))));
    const autoAddr = headers.find(h => addrKw.some(k => clean(h).includes(clean(k))));
    nameSel.value = autoName || headers[0];
    addrSel.value = autoAddr || (headers[1] ?? headers[0]);

    /* 자동감지 결과를 모달 제목 영역에 표시 */
    const prevEl2 = document.getElementById("data-preview-table");
    const autoInfoEl = modal.querySelector(".mapper-auto-info") || (() => {
      const el = document.createElement("p");
      el.className = "mapper-auto-info";
      el.style.cssText = "font-size:11.5px;color:var(--text-2);margin-bottom:10px;";
      modal.querySelector(".mapper-box > p")?.after(el);
      return el;
    })();
    if (autoName && autoAddr) {
      autoInfoEl.innerHTML = `<span style="color:var(--clr-success);font-weight:600">✓ 자동 인식 완료</span> — 이름: <b>${escHtml(autoName)}</b>, 주소: <b>${escHtml(autoAddr)}</b><br><span style="color:var(--text-3)">아래에서 확인 후 변경하거나 바로 가져오기를 누르세요.</span>`;
    } else {
      autoInfoEl.innerHTML = `<span style="color:var(--clr-warn);font-weight:600">⚠ 자동 인식 실패</span> — 열을 직접 선택해주세요.`;
    }

    function renderPreview() {
      const nk = nameSel.value, ak = addrSel.value, rows = previewRows.slice(0,6);
      const ths = headers.map(h => {
        let cls="", badge="";
        if (h===nk){cls="col-name-hl"; badge=`<span class="col-badge col-badge-name">이름</span>`+(h===autoName?`<span class="col-auto-badge"></span>`:"");}
        if (h===ak){cls="col-addr-hl"; badge=`<span class="col-badge col-badge-addr">주소</span>`+(h===autoAddr?`<span class="col-auto-badge"></span>`:"");}
        return `<th class="${cls}">${escHtml(h)}${badge}</th>`;
      }).join("");
      const trs = rows.map(r => `<tr>${headers.map(h => {
        const cls = h===nk?"col-name-hl":h===ak?"col-addr-hl":"";
        const val = r[h]!=null?String(r[h]):"";
        return `<td class="${cls}" title="${escHtml(val)}">${escHtml(val)}</td>`;
      }).join("")}</tr>`).join("");
      prevEl.innerHTML = `<div class="mapper-preview"><h4>데이터 미리보기 (상위 ${rows.length}행)</h4><div class="preview-table-wrapper"><table class="preview-table"><thead><tr>${ths}</tr></thead><tbody>${trs}</tbody></table></div></div>`;
    }
    renderPreview();
    nameSel.addEventListener("change", renderPreview);
    addrSel.addEventListener("change", renderPreview);
    modal.style.display = "flex";
    requestAnimationFrame(() => modal.classList.add("active"));

    function cleanup() {
      nameSel.removeEventListener("change", renderPreview);
      addrSel.removeEventListener("change", renderPreview);
      modal.classList.remove("active");
      setTimeout(() => { modal.style.display="none"; }, 200);
      document.removeEventListener("keydown", escH);
    }
    function escH(e) { if (e.key==="Escape"){ cleanup(); reject("cancelled"); } }
    document.addEventListener("keydown", escH);
    document.getElementById("confirm-mapping").onclick = () => { cleanup(); resolve({ nameKey: nameSel.value, addrKey: addrSel.value }); };
    document.getElementById("cancel-mapping") .onclick = () => { cleanup(); reject("cancelled"); };
  });
}

/* [최우선 수정: "병합된 칸이 있는 엑셀을 받으면 제대로 인식 못함"]
   엑셀에서 여러 칸을 하나로 합친(병합) 셀은, 병합 범위의 맨 왼쪽-위
   칸에만 실제 값이 들어있고 나머지 칸들은 SheetJS 기준으로 완전히
   빈 칸으로 취급된다. 예를 들어 "주소" 열이 두 행에 걸쳐 병합돼
   있으면, sheet_to_json으로 읽었을 때 그중 한 행은 주소가 빈 문자열로
   나와 "이름은 있는데 주소가 없는 행"으로 걸러지거나, 반대로 그 값이
   원래 있어야 할 행에 없어 이름·주소가 서로 다른 사람 것끼리 짝지어질
   위험이 있었다. → XLSX.read()로 시트를 연 직후, 병합 범위(sheet['!merges'])를
   순회하며 그 범위 전체 칸에 왼쪽-위 칸의 값을 그대로 복사해 "펼쳐"
   넣는다. sheet_to_json은 이 이후에 실행되므로, 병합이 있든 없든
   모든 행에서 이름·주소를 정상적으로 읽을 수 있게 된다. 자동 열 탐지가
   실패해 수동 "열 매핑" 화면(showColumnMapper)으로 넘어가는 경우에도
   이미 펼쳐진 값을 그대로 보여주므로 미리보기가 비어 보이는 문제도
   함께 해결된다. */
function fillMergedCellsIntoSheet(sheet) {
  const merges = (sheet && sheet['!merges']) || [];
  merges.forEach(range => {
    const startAddr = XLSX.utils.encode_cell(range.s);
    const startCell = sheet[startAddr];
    if (!startCell) return; /* 병합 시작 칸 자체가 비어 있으면 채울 값이 없음 */
    for (let r = range.s.r; r <= range.e.r; r++) {
      for (let c = range.s.c; c <= range.e.c; c++) {
        if (r === range.s.r && c === range.s.c) continue;
        const addr = XLSX.utils.encode_cell({ r, c });
        if (!sheet[addr]) sheet[addr] = { t: startCell.t, v: startCell.v, w: startCell.w };
      }
    }
  });
}

/* ══════════════════════════════════════════════════════════════
   엑셀 파싱 Web Worker 팩토리
   XLSX.read() 는 DOM 불필요 → Worker 분리로 UI 블로킹 방지
   Worker 생성 실패(file:// 등) 시 메인 스레드 폴백
   ══════════════════════════════════════════════════════════════ */
function _makeXlsxWorker() {
  const src = `
    /* Worker 내부 — XLSX CDN 로드 후 파싱 */
    importScripts("https://cdn.sheetjs.com/xlsx-latest/package/dist/xlsx.full.min.js");
    /* [최우선 수정] 메인 스레드의 fillMergedCellsIntoSheet()와 동일 —
       Worker는 별도 스코프라 함수를 공유할 수 없어 그대로 복사해 둔다. */
    function fillMergedCellsIntoSheet(sheet) {
      const merges = (sheet && sheet['!merges']) || [];
      merges.forEach(range => {
        const startAddr = XLSX.utils.encode_cell(range.s);
        const startCell = sheet[startAddr];
        if (!startCell) return;
        for (let r = range.s.r; r <= range.e.r; r++) {
          for (let c = range.s.c; c <= range.e.c; c++) {
            if (r === range.s.r && c === range.s.c) continue;
            const addr = XLSX.utils.encode_cell({ r, c });
            if (!sheet[addr]) sheet[addr] = { t: startCell.t, v: startCell.v, w: startCell.w };
          }
        }
      });
    }
    /* ★TABLE-START: lib/table-import.js의 detectTableStartRow()와 동일 —
       마찬가지로 Worker는 별도 스코프라 그대로 복사해 둔다(파일 경로로
       importScripts하면 이 Worker가 blob: URL에서 만들어져서 상대경로가
       페이지 기준으로 안 풀릴 수 있어 안전하게 문자열로 중복시킨다).
       이름+주소 키워드로 헤더 행을 못 찾았을 때의 안전망 — 병합된 제목
       행이나 안내문이 위에 몇 줄 껴 있어도 최소한 "표 형태가 시작되는
       행"까지는 구조적으로 짚어준다. */
    function detectTableStartRow(raw, scanRows) {
      if (!Array.isArray(raw) || raw.length === 0) return 0;
      const n = Math.min(scanRows || 12, raw.length);
      if (n <= 1) return 0;
      const countFilled = row => (row||[]).reduce((c,cell)=>c+((cell!=null&&String(cell).trim()!=="")?1:0),0);
      const countDistinct = row => new Set((row||[]).map(cell=>cell!=null?String(cell).trim():"").filter(v=>v!=="")).size;
      const counts = []; for (let i=0;i<n;i++) counts.push(countFilled(raw[i]));
      const freq = {}; let modeCount=0, modeVal=0;
      counts.forEach(c => { if(c===0) return; freq[c]=(freq[c]||0)+1; if(freq[c]>modeCount||(freq[c]===modeCount&&c>modeVal)){modeCount=freq[c];modeVal=c;} });
      if (modeVal <= 1) return 0;
      for (let i=0;i<n;i++) if (counts[i]>=modeVal && countDistinct(raw[i])>1) return i;
      return 0;
    }
    self.onmessage = function(e) {
      try {
        const { buffer, nameKws, addrKws } = e.data;
        const wb    = XLSX.read(buffer);
        const sheet = wb.Sheets[wb.SheetNames[0]];
        fillMergedCellsIntoSheet(sheet); /* [최우선 수정] 병합 칸 값을 미리 펼쳐 넣는다 */
        const raw   = XLSX.utils.sheet_to_json(sheet, { header:1 });
        if (!raw.length) { self.postMessage({ type:"empty" }); return; }
        /* 헤더 행 자동 탐색 — 이름+주소 키워드가 같은 행에 함께 있는 행을 우선 찾는다 */
        const clean = s => String(s).replace(/\\s/g,"").toLowerCase();
        let hRow = 0, hRowFound = false;
        for (let i=0;i<Math.min(15,raw.length);i++){
          const rs=(raw[i]||[]).join("").replace(/\\s/g,"");
          if(nameKws.some(k=>rs.includes(k))&&addrKws.some(k=>rs.includes(k))){hRow=i;hRowFound=true;break;}
        }
        /* ★TABLE-START: 키워드로 못 찾았으면(제목 행 표현이 흔치 않은
           단어라서 등) 구조적 탐지로 최소한 표 시작 지점까지는 짚어준다.
           그래도 실제 열 이름이 안 맞으면 이후 "열 매핑" 화면에서 사람이
           직접 고르면 되니, 여기서는 무리하게 확정하지 않고 시도만 한다. */
        if (!hRowFound) hRow = detectTableStartRow(raw, 12);
        const rows    = XLSX.utils.sheet_to_json(sheet, { range:hRow });
        const headers = Object.keys(rows[0]||{});
        let nameKey = headers.find(h=>nameKws.some(k=>clean(h).includes(clean(k))));
        let addrKey = headers.find(h=>addrKws.some(k=>clean(h).includes(clean(k))));
        self.postMessage({ type:"done", rows, headers, nameKey: nameKey||null, addrKey: addrKey||null });
      } catch(err) {
        self.postMessage({ type:"error", message: err.message });
      }
    };
  `;
  try {
    const blob = new Blob([src], { type: "application/javascript" });
    return new Worker(URL.createObjectURL(blob));
  } catch { return null; }
}

/* ══════════════════════════════════════════════════════════════
   파일 처리 — Web Worker 파싱 + 성공/실패 진행률 세분화
   ══════════════════════════════════════════════════════════════ */
/* [신규] 가져오기 시작 전 공통 확인 — 기존 라벨이 있으면 유지할지 새로
   시작할지 묻는다. 엑셀·hwpx·txt 가져오기 세 경로가 모두 이 확인을 거친다. */
async function confirmKeepExistingLabelsForImport() {
  const existingCount = postLabelContainer.querySelectorAll(".label").length;
  if (existingCount > 0) {
    const keep = await showConfirm(
      `현재 라벨 ${existingCount}개가 있습니다.\n기존 라벨을 유지하고 추가할까요?`,
      { icon:"📋", title:"라벨 추가 방식", okLabel:"유지하고 추가", cancelLabel:"새로 시작" }
    );
    if (!keep) {
      postLabelContainer.innerHTML = "";
      idbDelete("lastLabels").catch(() => {}); undoStack.length = 0;
    }
  }
}

/* ══════════════════════════════════════════════════════════════
   [신규] 한글(hwpx) / 메모장(txt) 가져오기
   엑셀은 "열"이 있어 이름·주소를 명확히 구분할 수 있지만, hwpx/txt는
   구조 없는 텍스트라 여러 규칙을 시도해 그중 가장 그럴듯한 결과를
   고른다. 완벽할 수 없으므로, 결과를 실제로 쓰기 전에 반드시
   showImportPreview로 사람이 눈으로 한 번 확인·수정하게 한다.
   ══════════════════════════════════════════════════════════════ */

/* 대략적인 "이름처럼 보임" / "주소처럼 보임" 판정 — 어떤 분리 규칙이
   가장 그럴듯한지 점수를 매겨 고르는 데만 쓴다(최종 판단은 항상 사람). */
function _looksLikeName(s) {
  if (!s) return false;
  const t = s.trim();
  return t.length >= 2 && t.length <= 12 && !/\s/.test(t) && /[가-힣a-zA-Z]/.test(t);
}
function _looksLikeAddress(s) {
  if (!s) return false;
  const t = s.trim();
  if (t.length < 4) return false;
  return /(시|도|군|구|읍|면|동|로|길|번지|아파트|빌딩|층|호)/.test(t);
}
function _scoreRows(rows) {
  if (!rows.length) return -1;
  let ok = 0;
  rows.forEach(r => { if (_looksLikeName(r["이름"]) && _looksLikeAddress(r["주소"])) ok++; });
  return ok / rows.length;
}

/* 텍스트 한 덩어리에서 {이름,주소} 목록을 추측한다. 여러 분리 전략을
   모두 시도해보고 "이름처럼 보이는 값 + 주소처럼 보이는 값" 비율이
   가장 높은 결과를 채택한다. 어느 쪽도 신통치 않으면 마지막 수단으로
   줄 전체를 주소로 두고 이름은 비워서 돌려준다(미리보기에서 채우거나
   지울 수 있음). */
function parseTextForNameAddress(text) {
  const lines = String(text).split(/\r\n|\r|\n/).map(l => l.trim()).filter(l => l.length > 0);
  if (!lines.length) return [];
  const candidates = [];

  /* 전략 1: 탭(Tab) 구분 — 엑셀/표에서 복사해 붙인 경우 흔함 */
  if (lines.some(l => l.includes("\t"))) {
    candidates.push(lines.map(l => {
      const parts = l.split("\t").map(s => s.trim()).filter(Boolean);
      return { "이름": parts[0] || "", "주소": parts.slice(1).join(" ") || "" };
    }).filter(r => r["이름"] || r["주소"]));
  }

  /* 전략 2: 연속 공백(2칸 이상) 구분 — 사람이 눈으로 맞춰 띄어쓴 목록 */
  if (lines.some(l => /\s{2,}/.test(l))) {
    candidates.push(lines.map(l => {
      const parts = l.split(/\s{2,}/).map(s => s.trim()).filter(Boolean);
      return { "이름": parts[0] || "", "주소": parts.slice(1).join(" ") || "" };
    }).filter(r => r["이름"] || r["주소"]));
  }

  /* 전략 3: 쉼표 구분 — 단, 주소 자체에 쉼표가 섞이는 경우가 많아
     "첫 조각이 이름처럼 짧을 때"만 채택 후보로 넣는다. */
  if (lines.some(l => l.includes(","))) {
    candidates.push(lines.map(l => {
      const idx = l.indexOf(",");
      if (idx < 0) return { "이름": "", "주소": l };
      const first = l.slice(0, idx).trim();
      const rest  = l.slice(idx + 1).trim();
      return _looksLikeName(first) ? { "이름": first, "주소": rest } : { "이름": "", "주소": l };
    }));
  }

  /* 전략 4: 빈 줄로 구분된 블록 — 블록의 첫 줄을 이름, 나머지를 주소로 */
  const blocks = String(text).split(/\r?\n\s*\r?\n+/).map(b => b.trim()).filter(Boolean);
  if (blocks.length >= 2) {
    const out = [];
    blocks.forEach(b => {
      const bl = b.split(/\r?\n/).map(s => s.trim()).filter(Boolean);
      if (bl.length >= 2) out.push({ "이름": bl[0], "주소": bl.slice(1).join(" ") });
      else if (bl.length === 1) out.push({ "이름": "", "주소": bl[0] });
    });
    candidates.push(out);
  }

  /* 전략 5: 줄이 짝수 개면 (이름줄, 주소줄) 번갈아 나온 목록으로 추정 */
  if (lines.length >= 2 && lines.length % 2 === 0) {
    const out = [];
    for (let i = 0; i < lines.length; i += 2) out.push({ "이름": lines[i], "주소": lines[i + 1] });
    candidates.push(out);
  }

  /* 가장 그럴듯한(이름·주소 둘 다 그럴듯해 보이는 비율이 가장 높은) 후보를 채택 */
  let best = null, bestScore = -1;
  candidates.forEach(rows => {
    const s = _scoreRows(rows);
    if (s > bestScore) { bestScore = s; best = rows; }
  });

  /* 어떤 전략도 그럴듯하지 않으면(예: 그냥 주소만 죽 나열된 파일) 최종
     폴백 — 줄 전체를 주소로 두고 이름은 비워둔다. 미리보기 화면에서
     사람이 직접 이름을 채우거나 필요없는 줄을 지울 수 있다. */
  if (!best || bestScore <= 0) {
    best = lines.map(l => ({ "이름": "", "주소": l }));
  }
  return best.filter(r => r["이름"] || r["주소"]);
}

/* [신규] hwpx/txt에서 추측한 {이름,주소} 목록을 실제로 라벨을 만들기
   전에 사람이 확인·수정하게 하는 모달. 표의 각 칸이 바로 입력창이라
   틀린 부분을 그 자리에서 고칠 수 있고, 필요없는 줄은 행 오른쪽의
   ✕로 지울 수 있다. "이대로 라벨 생성"을 누르면 그 시점의 표 내용을
   최종 배열로 만들어 resolve한다. */
function showImportPreview(rows, sourceLabel) {
  return new Promise((resolve, reject) => {
    const modal   = document.getElementById("import-preview-modal");
    const descEl  = document.getElementById("import-preview-desc");
    const sumEl   = document.getElementById("import-preview-summary");
    const wrapEl  = document.getElementById("import-preview-table-wrap");

    descEl.textContent = `${sourceLabel}에서 이름·주소를 이렇게 인식했어요. 잘못된 칸은 표에서 바로 고치거나 그 줄을 지운 뒤 가져오세요.`;

    /* 표 내용은 DOM(각 입력창의 값)이 곧 최종 데이터의 원천이 되도록,
       메모리상의 배열을 따로 동기화하지 않고 확정 시점에 DOM에서 직접
       읽어들인다(입력마다 상태 동기화 로직을 만들 필요가 없어 단순함). */
    function renderTable() {
      const bodyRows = rows.map((r, i) => `
        <tr data-row="${i}">
          <td style="width:34px;text-align:center;color:var(--text-3)">${i + 1}</td>
          <td><input type="text" class="import-preview-input ipv-name" value="${escHtml(r["이름"] || "")}" placeholder="이름"/></td>
          <td><input type="text" class="import-preview-input ipv-addr" value="${escHtml(r["주소"] || "")}" placeholder="주소"/></td>
          <td style="width:30px;text-align:center">
            <button type="button" class="import-preview-del-btn" title="이 줄 지우기" data-row="${i}">✕</button>
          </td>
        </tr>`).join("");
      wrapEl.innerHTML = `
        <table class="preview-table">
          <thead><tr><th>#</th><th>👤 이름</th><th>🏠 주소</th><th></th></tr></thead>
          <tbody>${bodyRows}</tbody>
        </table>`;
      updateSummary();
      wrapEl.querySelectorAll(".import-preview-del-btn").forEach(btn => {
        btn.addEventListener("click", () => {
          const i = Number(btn.dataset.row);
          rows.splice(i, 1);
          renderTable();
        });
      });
    }
    function updateSummary() {
      const filled = rows.filter(r => (r["이름"]||"").trim() && (r["주소"]||"").trim()).length;
      sumEl.textContent = `총 ${rows.length}줄 인식됨 · 이름·주소가 모두 채워진 줄 ${filled}개 (빈 줄이 있으면 라벨이 비어 보일 수 있어요)`;
    }
    renderTable();

    modal.style.display = "flex";
    requestAnimationFrame(() => modal.classList.add("active"));

    function readRowsFromDom() {
      return Array.from(wrapEl.querySelectorAll("tbody tr")).map(tr => ({
        "이름": tr.querySelector(".ipv-name").value.trim(),
        "주소": tr.querySelector(".ipv-addr").value.trim(),
      })).filter(r => r["이름"] || r["주소"]);
    }

    function cleanup() {
      modal.classList.remove("active");
      setTimeout(() => { modal.style.display = "none"; }, 200);
      document.removeEventListener("keydown", escH);
      document.getElementById("import-preview-confirm").onclick = null;
      document.getElementById("import-preview-cancel").onclick = null;
    }
    function escH(e) { if (e.key === "Escape") { cleanup(); reject("cancelled"); } }
    document.addEventListener("keydown", escH);
    document.getElementById("import-preview-confirm").onclick = () => {
      const finalRows = readRowsFromDom().filter(r => r["이름"] && r["주소"]);
      cleanup();
      if (!finalRows.length) { showAlert("이름과 주소가 모두 채워진 줄이 없어 가져올 수 없어요.", "📭", "가져올 데이터 없음"); reject("empty"); return; }
      resolve(finalRows);
    };
    document.getElementById("import-preview-cancel").onclick = () => { cleanup(); reject("cancelled"); };
  });
}

/* [신규] 메모장(.txt) 가져오기 — 파일을 텍스트로 읽어 이름·주소를
   추측한 뒤, 반드시 미리보기 확인을 거쳐 finalizeImportedRows로 넘긴다. */
async function processTextFile(file) {
  if (!file) return;
  try {
    const text = await file.text();
    const guessed = parseTextForNameAddress(text);
    if (!guessed.length) { await showAlert("텍스트 파일에서 인식할 수 있는 내용이 없습니다.", "📭", "데이터 없음"); return; }
    let finalRows;
    try {
      finalRows = await showImportPreview(guessed, `"${file.name}"`);
    } catch { return; } /* 취소 */
    await confirmKeepExistingLabelsForImport();
    await finalizeImportedRows(finalRows);
  } catch (err) {
    console.error(err);
    await showAlert("텍스트 파일을 읽는 중 오류가 발생했습니다.", "❌", "오류");
  }
}

/* [신규] JSZip을 CDN에서 한 번만 불러온다(HWPX는 ZIP 컨테이너이므로
   내부 XML을 읽으려면 압축 해제가 필요). 엑셀 가져오기가 SheetJS를
   Worker 안에서 CDN 로드하는 것과 같은 방식 — 이 앱 자체에는 무거운
   압축 라이브러리를 기본 포함하지 않고 필요할 때만 내려받는다. */
let _jszipLoadPromise = null;
function ensureJSZip() {
  if (window.JSZip) return Promise.resolve(window.JSZip);
  if (_jszipLoadPromise) return _jszipLoadPromise;
  _jszipLoadPromise = new Promise((resolve, reject) => {
    const s = document.createElement("script");
    s.src = "https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js";
    s.onload  = () => resolve(window.JSZip);
    s.onerror = () => reject(new Error("JSZip 로드 실패(인터넷 연결을 확인해주세요)"));
    document.head.appendChild(s);
  });
  return _jszipLoadPromise;
}

/* XML 태그의 네임스페이스 접두사(hp:, hs: 등)는 hwpx 버전마다 조금씩
   다를 수 있어, 접두사를 가리지 않고 로컬 이름(:p, :t)만으로 매칭한다. */
function _decodeXmlEntities(s) {
  return String(s)
    .replace(/&lt;/g, "<").replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"').replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&");
}
function _extractHwpxParagraphsFromXml(xml) {
  const paraRe = /<(?:\w+:)?p[ >][\s\S]*?<\/(?:\w+:)?p>/g;
  const paras = xml.match(paraRe) || [xml];
  return paras.map(p => {
    const tRe = /<(?:\w+:)?t\b[^>]*>([\s\S]*?)<\/(?:\w+:)?t>/g;
    let m, text = "";
    while ((m = tRe.exec(p))) text += m[1];
    return _decodeXmlEntities(text);
  }).filter(Boolean).join("\n");
}
async function extractHwpxText(file) {
  const JSZipLib = await ensureJSZip();
  const zip = await JSZipLib.loadAsync(file);
  let sectionNames = Object.keys(zip.files).filter(n => /^Contents\/section\d+\.xml$/i.test(n)).sort();
  if (!sectionNames.length) sectionNames = Object.keys(zip.files).filter(n => /^Contents\/.*\.xml$/i.test(n));
  if (!sectionNames.length) throw new Error("hwpx 안에서 본문(Contents/section*.xml)을 찾지 못했습니다.");
  let fullText = "";
  for (const name of sectionNames) {
    const xml = await zip.files[name].async("text");
    fullText += _extractHwpxParagraphsFromXml(xml) + "\n";
  }
  return fullText;
}

/* [신규] 한글(.hwpx) 가져오기 — hwpx는 zip 컨테이너이므로 압축을 풀어
   본문 XML에서 글자만 뽑아낸 뒤(extractHwpxText), 그 뒤로는 txt
   가져오기와 완전히 같은 경로(추측 → 미리보기 확인 → 마무리)를 탄다. */
async function processHwpxFile(file) {
  if (!file) return;
  loadingModal.style.display = "flex";
  requestAnimationFrame(() => loadingModal.classList.add("active"));
  loadingText.textContent = "한글 파일 읽는 중...";
  const _lpw = document.getElementById("loading-progress-wrap");
  if (_lpw) _lpw.style.display = "none"; /* 이 단계는 진행률을 알 수 없어 막대는 숨김 */
  let text;
  try {
    text = await extractHwpxText(file);
  } catch (err) {
    console.error(err);
    loadingModal.classList.remove("active");
    setTimeout(() => { loadingModal.style.display = "none"; }, 200);
    await showAlert("한글(.hwpx) 파일을 읽는 중 오류가 발생했습니다.\n" + (err && err.message ? err.message : ""), "❌", "오류");
    return;
  }
  loadingModal.classList.remove("active");
  setTimeout(() => { loadingModal.style.display = "none"; }, 200);

  const guessed = parseTextForNameAddress(text);
  if (!guessed.length) { await showAlert("한글 파일에서 인식할 수 있는 내용이 없습니다.", "📭", "데이터 없음"); return; }
  let finalRows;
  try {
    finalRows = await showImportPreview(guessed, `"${file.name}"`);
  } catch { return; } /* 취소 */
  await confirmKeepExistingLabelsForImport();
  await finalizeImportedRows(finalRows);
}

/* [신규] 확장자를 보고 엑셀/hwpx/txt 중 알맞은 가져오기 경로로 보낸다.
   버튼 클릭·드래그앤드롭이 모두 이 함수 하나를 거치게 해 분기 로직을
   한곳에서만 관리한다. */
async function handleImportFile(file) {
  if (!file) return;
  const name = file.name || "";
  if (/\.xlsx?$/i.test(name)) return processFile(file);
  if (/\.txt$/i.test(name))   return processTextFile(file);
  if (/\.hwpx$/i.test(name))  return processHwpxFile(file);
  await showAlert(".xlsx, .xls, .hwpx, .txt 파일만 지원합니다.", "📂", "지원하지 않는 파일");
}

async function processFile(file) {
  if (!file) return;
  await confirmKeepExistingLabelsForImport();
  loadingModal.style.display = "flex";
  requestAnimationFrame(() => loadingModal.classList.add("active"));
  const _lpw = document.getElementById("loading-progress-wrap");
  const _lpb = document.getElementById("loading-progress-bar");
  if (_lpw) _lpw.style.display = "";
  if (_lpb) _lpb.style.width = "0%";

  const nameKws = ["이름","성명","민원인","세대주","수취인","고객명","받는분"];
  const addrKws = ["주소","도로명","지번","수령","배송지","받는주소"];

  try {
    const buffer = await file.arrayBuffer();

    /* ── Worker로 XLSX 파싱 시도 */
    let rows = null, headers = null, nameKey = null, addrKey = null;

    const worker = _makeXlsxWorker();
    if (worker) {
      /* Worker 경로: Worker 내에서 XLSX 파싱 */
      const parsed = await new Promise((resolve, reject) => {
        worker.onmessage  = e => { worker.terminate(); resolve(e.data); };
        worker.onerror    = err => { worker.terminate(); reject(err); };
        worker.postMessage({ buffer: buffer.slice(0), nameKws, addrKws }, [buffer.slice(0)]);
      }).catch(() => null);

      if (parsed && parsed.type === "done") {
        rows = parsed.rows; headers = parsed.headers;
        nameKey = parsed.nameKey; addrKey = parsed.addrKey;
      } else if (parsed && parsed.type === "empty") {
        loadingModal.classList.remove("active");
        setTimeout(() => { loadingModal.style.display = "none"; }, 200);
        await showAlert("엑셀 파일에 데이터가 없습니다.", "📭", "데이터 없음"); return;
      }
      /* Worker 실패 시 폴백은 아래 메인스레드 경로 */
    }

    /* ── 폴백: 메인 스레드 파싱 (Worker 미지원 환경) */
    if (!rows) {
      const wb    = XLSX.read(buffer);
      const sheet = wb.Sheets[wb.SheetNames[0]];
      fillMergedCellsIntoSheet(sheet); /* [최우선 수정] 병합 칸 값을 미리 펼쳐 넣는다 — Worker 경로와 동일 */
      const raw   = XLSX.utils.sheet_to_json(sheet, { header:1 });
      if (!raw.length) {
        loadingModal.classList.remove("active");
        setTimeout(() => { loadingModal.style.display = "none"; }, 200);
        await showAlert("엑셀 파일에 데이터가 없습니다.", "📭", "데이터 없음"); return;
      }
      const clean = s => String(s).replace(/\s/g,"").toLowerCase();
      let hRow = 0, hRowFound = false;
      for (let i=0;i<Math.min(15,raw.length);i++){
        const rs=(raw[i]||[]).join("").replace(/\s/g,"");
        if(nameKws.some(k=>rs.includes(k))&&addrKws.some(k=>rs.includes(k))){hRow=i;hRowFound=true;break;}
      }
      /* ★TABLE-START: 키워드로 헤더 행을 못 찾았을 때의 안전망 — Worker
         경로와 동일하게 구조적 탐지(lib/table-import.js)로 대체 시도한다. */
      if (!hRowFound && typeof TableImport !== "undefined") hRow = TableImport.detectTableStartRow(raw, 12);
      rows    = XLSX.utils.sheet_to_json(sheet, { range:hRow });
      headers = Object.keys(rows[0]||{});
      const cleanH = s => String(s).replace(/\s/g,"");
      nameKey = headers.find(h=>nameKws.some(k=>cleanH(h).includes(cleanH(k))));
      addrKey = headers.find(h=>addrKws.some(k=>cleanH(h).includes(cleanH(k))));
    }

    /* ── 열 매핑 */
    if (!nameKey || !addrKey) {
      loadingModal.classList.remove("active");
      setTimeout(() => { loadingModal.style.display = "none"; }, 200);
      try {
        const m = await showColumnMapper(headers, rows);
        nameKey = m.nameKey; addrKey = m.addrKey;
        loadingModal.style.display = "flex";
        requestAnimationFrame(() => loadingModal.classList.add("active"));
      } catch { return; }
    }

    const valid = rows
      .map(r => ({ "이름": r[nameKey]!=null?String(r[nameKey]).trim():"", "주소": r[addrKey]!=null?String(r[addrKey]).trim():"" }))
      .filter(r => r["주소"] !== "" && r["이름"] !== "");

    await finalizeImportedRows(valid);
  } catch(err) {
    console.error(err);
    loadingModal.classList.remove("active");
    setTimeout(() => { loadingModal.style.display = "none"; }, 200);
    await showAlert("오류가 발생했습니다.\n엑셀 파일 형식을 확인해주세요.", "❌", "오류");
  } finally {
    loadingModal.classList.remove("active");
    setTimeout(() => { loadingModal.style.display = "none"; }, 200);
  }
}

/* [신규] 엑셀·hwpx·txt 가져오기가 공통으로 쓰는 마무리 단계 — {이름,주소}
   배열을 받아 우편번호를 조회하고 실제로 라벨을 만든다. 각 가져오기
   경로(엑셀은 열 매핑 뒤, hwpx/txt는 자체 파싱 + 미리보기 확인 뒤)가
   최종적으로 이 함수 하나로 모여 항상 같은 방식으로 마무리된다. */
async function finalizeImportedRows(valid) {
  loadingModal.style.display = "flex";
  requestAnimationFrame(() => loadingModal.classList.add("active"));
  const _lpw = document.getElementById("loading-progress-wrap");
  const _lpb = document.getElementById("loading-progress-bar");
  if (_lpw) _lpw.style.display = "";
  if (_lpb) _lpb.style.width = "0%";

  let _okCnt = 0, _failCnt = 0;
  const _totalCnt = valid.length;
  function _updateProgress(isSuccess) {
    if (isSuccess) _okCnt++; else _failCnt++;
    const done = _okCnt + _failCnt;
    const pct  = Math.round((done / _totalCnt) * 100);
    if (_lpb) _lpb.style.width = pct + "%";
    loadingText.textContent =
      `${done} / ${_totalCnt} 처리 중 · ✅ ${_okCnt}건 성공` +
      (_failCnt > 0 ? ` · ❌ ${_failCnt}건 실패` : "");
  }

  try {
    if (!_totalCnt) { await showAlert("가져올 이름·주소가 없습니다.", "📭", "데이터 없음"); return; }
    loadingText.textContent = `0 / ${_totalCnt} 처리 중...`;

    const results = await fetchInBatches(valid, BATCH_SIZE, async info => {
      const postCode = await fetchPostCode(info["주소"]);
      const success  = postCode && postCode.trim() !== "" && postCode.trim() !== "     ";
      _updateProgress(success);
      return { ...info, postCode };
    });

    /* [버그 수정] 대량 임포트 시 16개씩 한꺼번에 카카오 API를 두드리면
       일부는 짧은 순간 반복적으로 429(요청 과다)를 맞아 재시도(최대 3회)를
       다 써버리고도 결국 우편번호가 빈칸("     ")으로 남는 경우가 있었다.
       이게 바로 "가져오기 직후엔 몇 개가 비어 보이다가, 그 주소칸을
       클릭했다 빠져나오면(포커스아웃) 다시 잘 채워지는" 현상의 원인 —
       그 순간엔 동시 요청이 하나뿐이라 rate limit에 안 걸려 바로 성공한다.
       같은 효과를 가져오기 완료 시점에 자동으로 한 번 더 내주면 사용자가
       일일이 빈 칸을 찾아 다시 클릭할 필요가 없다. 이번엔 동시 처리 수를
       낮추고(2개) 살짝 텀을 둬서, 막 끝난 동시 폭주의 잔여 rate limit에
       다시 걸리지 않게 한다. */
    const failedIdx = results.map((r, i) => (!r.postCode || !r.postCode.trim()) ? i : -1).filter(i => i >= 0);
    if (failedIdx.length) {
      loadingText.textContent = `우편번호 재조회 중... (${failedIdx.length}건)`;
      await new Promise(r => setTimeout(r, 500)); /* 직전 동시 폭주의 잔여 rate limit이 가라앉을 시간 */
      await fetchInBatches(failedIdx, 2, async idx => {
        const retryCode = await fetchPostCode(results[idx]["주소"]);
        if (retryCode && retryCode.trim()) {
          results[idx] = { ...results[idx], postCode: retryCode };
          _updateProgress(true);
          if (_failCnt > 0) _failCnt--; /* 재시도로 성공 → 실패 집계에서 빼줌 */
        }
      });
    }

    const prevCount = postLabelContainer.querySelectorAll(".label").length;
    const frag = document.createDocumentFragment();
    results.forEach(item => frag.appendChild(createLabel(item)));

    const doAfterInsert = () => {
      saveToStorage(); reflowPages(); updateLabelCount();
      const allAfter = postLabelContainer.querySelectorAll(".label");
      if (allAfter[prevCount]) scrollToLabel(allAfter[prevCount]);
      const failMsg = _failCnt > 0 ? ` (우편번호 실패 ${_failCnt}건)` : "";
      /* [신규] 업로드를 "데이터" 단계에서 했을 때만 "라벨 편집으로 이동" 버튼을
         덧붙인다 — 이미 "라벨 편집" 단계에서 불러온 경우(툴바의 파일 불러오기)엔
         이동할 필요가 없으므로 기존 문구 그대로 보여준다. 기존 삭제-취소 토스트
         (undoDelete 버튼)와 동일하게 .toast-undo-btn 클래스 + inline onclick으로
         전역 함수/클로저 의존 없이 안전하게 기존 스텝 탭 버튼에 클릭만 전달한다. */
      const onDataStep = document.querySelector(".step-item.active")?.dataset.step === "data";
      const gotoEditBtn = onDataStep
        ? ` <button onclick="document.getElementById('step-tab-edit')?.click()" class="toast-undo-btn">라벨 편집으로 이동 →</button>`
        : "";
      showToast(`✅ ${results.length}개의 라벨이 생성되었습니다!${failMsg}${gotoEditBtn}`, "success", gotoEditBtn ? 5000 : 3000);
    };

    if (results.length > 200) {
      insertChunked(frag, postLabelContainer, 32, doAfterInsert);
    } else {
      postLabelContainer.appendChild(frag);
      doAfterInsert();
    }
  } finally {
    loadingModal.classList.remove("active");
    setTimeout(() => { loadingModal.style.display = "none"; }, 200);
  }
}

/* ══════════════════════════════════════════════════════════════
   글꼴 / 크기 컨트롤 (원본 최근사용 로직 포함)
   ══════════════════════════════════════════════════════════════ */
const templateDisplay = document.getElementById("template-display");
const fontDisplay     = document.getElementById("font-display");
const fontSizeSelect  = document.getElementById("font-size-select");

function updateTemplateDisplay() {
  const opt = templateSelect.options[templateSelect.selectedIndex];
  if (templateDisplay) templateDisplay.textContent = opt?.dataset.short || opt?.textContent || "";
}
updateTemplateDisplay(); updateInspector();

const MAX_RECENT = 3;
function getRecent(k) { try { return JSON.parse(localStorage.getItem(k)||"[]"); } catch { return []; } }
function saveRecent(k,v) {
  let l = getRecent(k).filter(x=>x!==v); l.unshift(v);
  if (l.length>MAX_RECENT) l=l.slice(0,MAX_RECENT);
  localStorage.setItem(k, JSON.stringify(l));
}
/* [BUG FIX] rebuildRecentOptgroup: 최근 항목 optgroup 삽입 시
   브라우저가 select 값을 첫 번째 옵션으로 리셋하는 문제 수정.
   _rebuildingRecent 플래그로 change 이벤트에서 reflowPages() 호출을 막아
   "최근폼 선택 시 폼텍 3107로 강제 변경" 버그 해결. */
function rebuildRecentOptgroup(sel, stk) {
  const savedVal = sel.value;           // ① 현재 값 먼저 저장
  sel._rebuildingRecent = true;         // ② change 이벤트 억제 플래그
  sel.querySelector("optgroup[data-recent]")?.remove();
  const rc = getRecent(stk);
  if (!rc.length) {
    sel._rebuildingRecent = false;
    sel.value = savedVal;
    return;
  }
  const grp = document.createElement("optgroup");
  grp.label = "⏱ 최근 사용"; grp.dataset.recent = "1";
  rc.forEach(val => {
    const orig = Array.from(sel.options).find(o => o.value===val && !o.closest("[data-recent]"));
    if (!orig) return;
    const opt = document.createElement("option"); opt.value = val;
    opt.textContent = orig.dataset.short || orig.textContent;
    if (orig.dataset.short) opt.dataset.short = orig.dataset.short;
    grp.appendChild(opt);
  });
  sel.insertBefore(grp, sel.firstChild);
  sel.value = savedVal;                 // ③ 저장해둔 값으로 강제 복원
  sel._rebuildingRecent = false;        // ④ 플래그 해제
}
rebuildRecentOptgroup(templateSelect, "recentTemplates");
saveRecent("recentTemplates", templateSelect.value);
rebuildRecentOptgroup(fontSelect, "recentFonts");
saveRecent("recentFonts", fontSelect.value);

fontSelect.addEventListener("change", () => {
  document.documentElement.style.setProperty("--label-font", fontSelect.value);
  const opt = fontSelect.options[fontSelect.selectedIndex];
  if (fontDisplay) fontDisplay.textContent = opt?.dataset.short || opt?.textContent || "";
  saveRecent("recentFonts", fontSelect.value);
  rebuildRecentOptgroup(fontSelect, "recentFonts");
  fontSelect.value = fontSelect.value;
  reflowPages(false);
});
fontSizeSelect.addEventListener("change", () => {
  fontSizeMultiplier = parseFloat(fontSizeSelect.value);
  reflowPages(false);
});
function stepFontSize(dir) {
  const opts = Array.from(fontSizeSelect.options);
  const idx  = opts.findIndex(o => parseFloat(o.value) === fontSizeMultiplier);
  const next = idx + dir;
  if (next < 0 || next >= opts.length) return;
  const nv = parseFloat(opts[next].value);
  if (nv < FONT_MIN || nv > FONT_MAX) return;
  fontSizeSelect.selectedIndex = next; fontSizeMultiplier = nv;
  reflowPages(false);
  // 원본: 글자 크기 변경 시 토스트
  showToast(`🔤 글자 크기: ${opts[next].textContent}`, "info", 1500);
}
document.getElementById("font-size-down").addEventListener("click", () => stepFontSize(-1));
document.getElementById("font-size-up")  .addEventListener("click", () => stepFontSize(+1));

templateSelect.addEventListener("change", () => {
  // [BUG FIX] rebuildRecentOptgroup 실행 중 발생하는 spurious change 이벤트 무시
  if (templateSelect._rebuildingRecent) return;
  saveRecent("recentTemplates", templateSelect.value);
  rebuildRecentOptgroup(templateSelect, "recentTemplates");
  updateTemplateDisplay(); reflowPages();
});

/* ══════════════════════════════════════════════════════════════
   라벨 컨테이너 이벤트 (원본 완전 동일)
   ══════════════════════════════════════════════════════════════ */
postLabelContainer.addEventListener("input", debounce(saveToStorage, 600));
window.addEventListener("beforeunload", saveToStorage);
/* beforeunload는 브라우저/OS가 탭을 강제 종료할 때(특히 모바일에서 배터리 절약 등의
   이유로) 실행되지 않는 경우가 있다. visibilitychange는 탭 전환·화면 끄기·모바일
   백그라운드 전환 시에도 안정적으로 발생하므로, "hidden"이 되는 시점에 한 번 더
   저장을 트리거해 마지막 편집분이 유실될 가능성을 줄인다. */
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState === "hidden") saveToStorage();
});

postLabelContainer.addEventListener("focusin", e => {
  if (e.target.classList.contains("addr")) {
    e.target._undoSnapshot  = e.target.innerText;
    const pc = e.target.closest(".label")?.querySelector(".post-code-container");
    e.target._undoPostCode  = pc
      ? Array.from(pc.querySelectorAll(".post-digit")).map(d => d.textContent).join("")
      : "     ";
  }
  if (e.target.classList.contains("name")) e.target._undoSnapshot = e.target.innerText;
});

postLabelContainer.addEventListener("focusout", async e => {
  if (e.target.classList.contains("addr")) {
    const addrEl  = e.target;
    const addr    = addrEl.innerText.trim();
    const labelEl = addrEl.closest(".label");
    const pcCont  = labelEl.querySelector(".post-code-container");
    if (addrEl._undoSnapshot !== undefined && addrEl._undoSnapshot !== addrEl.innerText) {
      undoStack.push({ type:"edit", labelEl, field:"addr", oldValue:addrEl._undoSnapshot, oldPostCode:addrEl._undoPostCode });
      if (undoStack.length > MAX_UNDO) undoStack.shift();
      redoStack.length = 0; /* 새 액션 → Redo 초기화 */
      updateUndoBtn();
    }
    if (addr) {
      pcCont.innerHTML = `<span style="font-size:11px;color:var(--text-3)">검색중..</span>`;
      const code = await fetchPostCode(addr);
      pcCont.innerHTML = digitsHTML(code);
      syncPostcodeLock(labelEl);
      if (!code || !code.trim()) showToast("🔎 우편번호를 찾지 못했습니다. 직접 입력해 주세요.", "warn", 2500);
      saveToStorage();
    }
    updateEmptyLabelClass(labelEl);
  }
  if (e.target.classList.contains("name")) {
    const nameEl  = e.target;
    const labelEl = nameEl.closest(".label");
    if (nameEl._undoSnapshot !== undefined && nameEl._undoSnapshot !== nameEl.innerText) {
      undoStack.push({ type:"edit", labelEl, field:"name", oldValue:nameEl._undoSnapshot });
      if (undoStack.length > MAX_UNDO) undoStack.shift();
      redoStack.length = 0; /* 새 액션 → Redo 초기화 */
      updateUndoBtn();
    }
    appendGuiha(nameEl); saveToStorage(); updateEmptyLabelClass(labelEl);
  }
});

postLabelContainer.addEventListener("keydown", e => {
  const isAddr = e.target.classList.contains("addr");
  const isName = e.target.classList.contains("name");
  if (e.key === "Enter" && (isAddr || isName)) { e.preventDefault(); e.target.blur(); return; }
  if (e.key === "Tab"   && (isAddr || isName)) {
    e.preventDefault();
    const label = e.target.closest(".label");
    const all   = Array.from(postLabelContainer.querySelectorAll(".label"));
    if (!e.shiftKey) {
      if (isAddr) label.querySelector(".name")?.focus();
      else        all[all.indexOf(label)+1]?.querySelector(".addr")?.focus();
    } else {
      if (isName) label.querySelector(".addr")?.focus();
      else        all[all.indexOf(label)-1]?.querySelector(".name")?.focus();
    }
  }
});

/* [버그 수정] 외부(엑셀/워드/웹페이지 등)에서 주소·이름을 복사해 붙여넣을 때
   원본의 글꼴·글자 크기·색상 등 서식이 함께 들어와 라벨 양식에 맞게 반영되지
   않던 문제 수정. 붙여넣기 시 서식을 모두 제거하고 순수 텍스트만 삽입해,
   글꼴·글자 크기가 항상 현재 라벨 양식(사이드바 설정)을 그대로 따르도록 함. */
postLabelContainer.addEventListener("paste", e => {
  const isAddr = e.target.classList.contains("addr");
  const isName = e.target.classList.contains("name");
  if (!isAddr && !isName) return;
  e.preventDefault();
  const raw = (e.clipboardData || window.clipboardData).getData("text/plain") || "";
  /* 줄바꿈(엑셀 여러 행/워드 문단)은 공백으로 합치고, 중복 공백은 정리 */
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
  e.target.dispatchEvent(new Event("input", { bubbles: true }));
});

/* 다중 선택 */
let lastSelectedLabel = null;
postLabelContainer.addEventListener("click", e => {
  if (e.target.classList.contains("lock-label-btn")) {
    const btn = e.target;
    const el  = btn.closest(".label"); if (!el) return;
    const locked = el.classList.toggle("label-locked");
    btn.textContent = locked ? "🔒" : "🔓";
    btn.setAttribute("aria-label", locked ? "잠금 해제" : "라벨 잠금");
    el.querySelector(".addr").contentEditable = locked ? "false" : "true";
    el.querySelector(".name").contentEditable = locked ? "false" : "true";
    syncPostcodeLock(el); /* 잠금 시 우편번호 칸도 함께 잠금/해제 */
    el.setAttribute("draggable", "false"); /* 잠금과 무관하게 기본값 false 유지 — 핸들 mousedown에서만 활성화 */
    if (typeof updateMinimap === "function") updateMinimap();
    saveToStorage();
    showToast(locked ? "🔒 잠겼습니다." : "🔓 잠금 해제됐습니다.", "info", 1400);
    return;
  }
  if (e.target.classList.contains("delete-btn")) {
    const lEl = e.target.closest(".label"); if (!lEl) return;
    undoStack.push({ type:"delete", html:lEl.outerHTML, index:Array.from(postLabelContainer.querySelectorAll(".label")).indexOf(lEl) });
    if (undoStack.length > MAX_UNDO) undoStack.shift();
    redoStack.length = 0; /* 새 액션 → Redo 초기화 */
    lEl.remove(); saveToStorage(); reflowPages();
    const kw = searchInput.value.trim(); if (kw) runSearch(kw); else updateLabelCount();
    updateUndoBtn();
    showToast(`🗑️ 라벨 삭제됨. <button onclick="undoDelete()" class="toast-undo-btn">↩ 취소</button>`, "warn", 5000);
    return;
  }
  if (e.target.classList.contains("copy-label-btn")) {
    const lEl  = e.target.closest(".label"); if (!lEl) return;
    const item = {
      "이름":     (lEl.querySelector(".name")?.textContent||"").replace(/ 귀하$/,""),
      "주소":      lEl.querySelector(".addr")?.textContent||"",
      "postCode": Array.from(lEl.querySelectorAll(".post-digit")).map(d=>d.textContent).join("")||"     "
    };
    const nw = createLabel(item); lEl.after(nw);
    saveToStorage(); reflowPages(); updateLabelCount(); scrollToLabel(nw);
    showToast("📋 라벨 복사됨.", "success"); return;
  }
  const label = e.target.closest(".label");
  if (!label || e.target.isContentEditable || ["INPUT","BUTTON","SELECT"].includes(e.target.tagName)) return;
  if (e.ctrlKey || e.metaKey) {
    label.classList.toggle("label-selected"); lastSelectedLabel = label;
  } else if (e.shiftKey && lastSelectedLabel) {
    const all = Array.from(postLabelContainer.querySelectorAll(".label"));
    const a = all.indexOf(lastSelectedLabel), b = all.indexOf(label);
    all.forEach((l,i) => { if (i>=Math.min(a,b) && i<=Math.max(a,b)) l.classList.add("label-selected"); });
  } else {
    postLabelContainer.querySelectorAll(".label.label-selected").forEach(l=>l.classList.remove("label-selected"));
    label.classList.add("label-selected"); lastSelectedLabel = label;
  }
  if (typeof updateMinimap === "function") updateMinimap();
});

/* ══════════════════════════════════════════════════════════════
   라벨 바깥 클릭 → 선택 해제
   ══════════════════════════════════════════════════════════════ */
document.addEventListener("click", e => {
  if (e.target.closest(".label")) return;
  if (e.target.closest(".ctx-menu, .modal-bg, .toolbar, .sidebar, .inspector, .titlebar, .statusbar, .fav-panel")) return;
  const selected = postLabelContainer.querySelectorAll(".label.label-selected");
  if (!selected.length) return;
  selected.forEach(l => l.classList.remove("label-selected"));
  lastSelectedLabel = null;
  if (typeof updateMinimap === "function") updateMinimap();
  updateLabelCount();
});

/* ══════════════════════════════════════════════════════════════
   실행취소 (원본 완전 동일)
   ══════════════════════════════════════════════════════════════ */
function updateUndoBtn() {
  const has = undoStack.length > 0;
  undoBtn.disabled = !has;
  undoBtn.title    = has ? `실행취소 (${undoStack.length}단계) — Ctrl+Z` : "실행취소 (Ctrl+Z)";
  const badge = document.getElementById("sb-undo-badge");
  if (badge) { badge.textContent = undoStack.length||""; badge.style.display = has ? "" : "none"; }
  /* [신규] 상태바에도 실행취소 가능 단계 수를 표시 — 클릭하면 바로 실행취소 */
  const sbUndoSep  = document.getElementById("sb-undo-sep");
  const sbUndoInfo = document.getElementById("sb-undo-info");
  if (sbUndoInfo) {
    sbUndoInfo.style.display = has ? "" : "none";
    if (sbUndoSep) sbUndoSep.style.display = has ? "" : "none";
    if (has) sbUndoInfo.textContent = `↩ 실행취소 ${undoStack.length}단계`;
  }
  /* ── Redo 단계 수 title 반영 */
  if (redoStack.length > 0) {
    undoBtn.title += ` / 다시실행 ${redoStack.length}단계 — Ctrl+Shift+Z`;
  }
  /* [신규] 캔버스 상단의 눈에 보이는 ↺/↻ 버튼 동기화 — 일반라벨의
     gl-undo-btn/gl-redo-btn과 같은 자리·같은 동작. 되돌릴/다시실행할 게
     없으면 .zoom-btn:disabled 스타일로 자동으로 흐려진다. */
  const canvasUndoBtn = document.getElementById("canvas-undo-btn");
  const canvasRedoBtn = document.getElementById("canvas-redo-btn");
  if (canvasUndoBtn) {
    canvasUndoBtn.disabled = !has;
    canvasUndoBtn.title = has ? `되돌리기 (${undoStack.length}단계) — Ctrl+Z` : "되돌리기 (Ctrl+Z)";
  }
  if (canvasRedoBtn) {
    const hasRedo = redoStack.length > 0;
    canvasRedoBtn.disabled = !hasRedo;
    canvasRedoBtn.title = hasRedo ? `다시 실행 (${redoStack.length}단계) — Ctrl+Shift+Z` : "다시 실행 (Ctrl+Shift+Z)";
  }
}
document.getElementById("canvas-undo-btn")?.addEventListener("click", () => window.undoDelete?.());
document.getElementById("canvas-redo-btn")?.addEventListener("click", () => window.redoAction?.());
document.getElementById("sb-undo-info")?.addEventListener("click", () => window.undoDelete?.());
window.undoDelete = function() {
  if (!undoStack.length) return;
  const entry = undoStack.pop();
  /* ── Undo한 항목을 Redo 스택에 보관 */
  redoStack.push(entry);
  if (redoStack.length > MAX_REDO) redoStack.shift();
  updateUndoBtn();

  if (entry.type === "delete") {
    const tmp = document.createElement("div"); tmp.innerHTML = entry.html;
    const restored = tmp.firstElementChild; enableDrag(restored);
    const all = Array.from(postLabelContainer.querySelectorAll(".label"));
    if (entry.index === 0 || !all.length) {
      const fp = postLabelContainer.querySelector(".a4-page");
      fp ? fp.prepend(restored) : postLabelContainer.appendChild(restored);
    } else { all[Math.min(entry.index, all.length-1)].before(restored); }
    saveToStorage(); reflowPages(); updateLabelCount(); updateUndoBtn(); scrollToLabel(restored);
    showToast("↩ 삭제 취소됨.", "info");
  } else if (entry.type === "edit") {
    const { labelEl, field, oldValue, oldPostCode } = entry;
    if (!labelEl?.isConnected) { updateUndoBtn(); showToast("↩ 라벨을 찾을 수 없습니다.", "warn"); return; }
    if (field === "addr") {
      labelEl.querySelector(".addr").innerText = oldValue;
      if (oldPostCode !== undefined) {
        const pc = labelEl.querySelector(".post-code-container");
        pc.innerHTML = digitsHTML(oldPostCode);
        syncPostcodeLock(labelEl);
      }
    } else { labelEl.querySelector(".name").innerText = oldValue; }
    saveToStorage(); updateUndoBtn(); scrollToLabel(labelEl); updateEmptyLabelClass(labelEl);
    showToast("↩ 편집 취소됨.", "info");
  }
};

/* ══════════════════════════════════════════════════════════════
   버튼 이벤트
   ══════════════════════════════════════════════════════════════ */
excelInput.addEventListener("change", e => { if(e.target.files[0]) handleImportFile(e.target.files[0]); e.target.value=""; });
loadBtn.addEventListener("click", () => excelInput.click());
document.getElementById("uz-load").addEventListener("click", () => excelInput.click());
document.getElementById("uz-add") .addEventListener("click", () => addBtn.click());

copyBtn.addEventListener("click", () => {
  const all = Array.from(postLabelContainer.querySelectorAll(".label"));
  if (!all.length) return showToast("⚠️ 복사할 라벨이 없습니다.", "warn");
  const lEl  = all[all.length-1];
  const item = { "이름":(lEl.querySelector(".name")?.textContent||"").replace(/ 귀하$/,""), "주소":lEl.querySelector(".addr")?.textContent||"", "postCode":Array.from(lEl.querySelectorAll(".post-digit")).map(d=>d.textContent).join("")||"     " };
  const nw = createLabel(item);
  postLabelContainer.appendChild(nw); saveToStorage(); reflowPages(); updateLabelCount(); scrollToLabel(nw);
  showToast("📋 마지막 라벨이 복사되었습니다.", "success");
});
addBtn.addEventListener("click", () => {
  const nw = createLabel({ "이름":"", "주소":"", "postCode":"     " });
  postLabelContainer.appendChild(nw); reflowPages(); saveToStorage(); updateLabelCount(); scrollToLabel(nw, true);
});
resetBtn.addEventListener("click", async () => {
  const all    = postLabelContainer.querySelectorAll(".label");
  const locked = postLabelContainer.querySelectorAll(".label.label-locked");
  if (!all.length) return showToast("⚠️ 삭제할 라벨이 없습니다.", "warn", 2000);
  let choice;
  if (locked.length > 0) {
    choice = await showDialog({ icon:"🗑️", title:"전체 삭제",
      message:`총 ${all.length}개 중 잠긴 라벨 ${locked.length}개가 있습니다.\n어떻게 삭제할까요?`,
      buttons:[
        { label:"취소",                               cls:"dialog-btn-cancel", value:"cancel"   },
        { label:`잠금 제외 삭제 (${all.length-locked.length}개)`, cls:"dialog-btn-ok",     value:"unlocked" },
        { label:`전체 삭제 (${all.length}개)`,         cls:"dialog-btn-danger", value:"all"      },
      ]
    });
  } else {
    const ok = await showConfirm(
      `모든 라벨 ${all.length}개가 삭제됩니다.\n정말 초기화하시겠습니까?`,
      { icon:"🗑️", title:"전체 삭제", okLabel:"전체 삭제", okCls:"dialog-btn-danger" }
    );
    choice = ok ? "all" : "cancel";
  }
  if (!choice || choice==="cancel") return;
  if (choice==="unlocked") {
    postLabelContainer.querySelectorAll(".label:not(.label-locked)").forEach(l=>l.remove());
    undoStack=[]; saveToStorage(); reflowPages(); updateLabelCount(); updateUndoBtn();
    showToast(`🗑️ 잠금 해제 라벨 ${all.length-locked.length}개 삭제 완료`, "warn", 2500);
  } else {
    postLabelContainer.innerHTML = "";
    idbDelete("lastLabels").catch(() => {}); undoStack=[];
    reflowPages(); updateLabelCount(); updateUndoBtn();
  }
});
printBtn.addEventListener("click", async () => {
  const labels = Array.from(postLabelContainer.querySelectorAll(".label"));
  if (!labels.length) { await showAlert("인쇄할 라벨 데이터가 없습니다.", "🖨️", "인쇄"); return; }
  const emptyCount = labels.filter(l => !l.querySelector(".addr")?.textContent?.trim() && !l.querySelector(".name")?.textContent?.trim()).length;
  if (emptyCount>0) {
    const ok = await showConfirm(`비어있는 라벨이 ${emptyCount}개 있습니다.\n그래도 인쇄하시겠습니까?`, { icon:"⚠️", title:"빈 라벨 있음", okLabel:"인쇄" });
    if (!ok) return;
  }
  if (!await confirmPrintSettings()) return;
  document.body.classList.toggle("hide-watermark", !(watermarkCheckbox?.checked??true));
  window.print();
});

// 워터마크 체크박스 — 캔버스 미리보기 실시간 반영
watermarkCheckbox?.addEventListener("change", () => {
  document.body.classList.toggle("hide-watermark", !watermarkCheckbox.checked);
});

/* ══════════════════════════════════════════════════════════════
   워터마크 선명도(투명도) 슬라이더
   ── 값이 클수록 워터마크가 또렷하게 보임(화면 미리보기·인쇄 동일 반영)
   ══════════════════════════════════════════════════════════════ */
(function() {
  const slider    = document.getElementById("watermark-opacity");
  const valueEl   = document.getElementById("watermark-opacity-value");
  const rowEl     = document.getElementById("wm-opacity-row");
  if (!slider) return;

  function applyOpacity(percent, { persist = true } = {}) {
    const pct = Math.min(40, Math.max(4, Number(percent) || 10));
    document.documentElement.style.setProperty("--wm-opacity", (pct / 100).toFixed(3));
    if (valueEl) valueEl.textContent = pct + "%";
    slider.value = pct;
    if (persist) localStorage.setItem("watermarkOpacity", String(pct));
  }

  function syncDisabledState() {
    const on = watermarkCheckbox?.checked ?? true;
    slider.disabled = !on;
    if (rowEl) rowEl.classList.toggle("wm-opacity-disabled", !on);
  }

  // localStorage에서 복원 (없으면 기존 기본값 10% 유지)
  const saved = localStorage.getItem("watermarkOpacity");
  applyOpacity(saved != null ? Number(saved) : 10, { persist: false });
  syncDisabledState();

  slider.addEventListener("input", () => applyOpacity(slider.value));
  watermarkCheckbox?.addEventListener("change", syncDisabledState);
})();

/* [통일] 굵게(Bold) 로직은 바로 아래 기울임/밑줄과 같은 applyToggleBtn()
   블록으로 합쳐졌다(예전엔 체크박스 스위치로 따로 구현돼 있었음). */
document.getElementById("insp-print")?.addEventListener("click", () => printBtn.click());

/* ══════════════════════════════════════════════════════════════
   신규: 기울임 / 밑줄 / 정렬 / 글자색 — 굵기(Bold) 토글과 동일한 방식으로
   구현했다: <html>에 전역 클래스·속성을 걸어 모든 라벨에 일괄 적용하고,
   localStorage에 저장해 새로고침 후에도 유지한다.
   ══════════════════════════════════════════════════════════════ */
(function() {
  /* ── 굵게 / 기울임 / 밑줄 (각각 독립 토글, 동시 적용 가능) ──
     [통일] 굵게(Bold)도 예전 체크박스 스위치 대신 여기서 같은 방식으로 합쳤다.
     클래스명(label-bold)·localStorage 키(labelBold)는 그대로 유지해
     기존에 저장된 사용자 설정이 그대로 이어지게 했다. */
  const boldBtn       = document.getElementById("bold-toggle-btn");
  const italicBtn    = document.getElementById("italic-toggle-btn");
  const underlineBtn = document.getElementById("underline-toggle-btn");

  function applyToggleBtn(btn, className, storageKey, label) {
    if (!btn) return;
    const saved = localStorage.getItem(storageKey) === "true";
    if (saved) {
      document.documentElement.classList.add(className);
      btn.setAttribute("aria-pressed", "true");
    }
    btn.addEventListener("click", () => {
      const on = btn.getAttribute("aria-pressed") !== "true";
      btn.setAttribute("aria-pressed", on ? "true" : "false");
      document.documentElement.classList.toggle(className, on);
      localStorage.setItem(storageKey, on ? "true" : "false");
      showToast(on ? `🔤 ${label} 적용됨` : `🔤 ${label} 해제됨`, "info", 1200);
    });
  }
  applyToggleBtn(boldBtn,      "label-bold",      "labelBold",      "굵게");
  applyToggleBtn(italicBtn,    "label-italic",    "labelItalic",    "기울임체");
  applyToggleBtn(underlineBtn, "label-underline", "labelUnderline", "밑줄");

  /* ── 글자 색상 — 기본값(#1a1a1a)이면 --label-color-override를 아예
     지워서 원래의 2색 배색(주소/이름 각각 다른 색)으로 되돌린다. */
  const colorInput = document.getElementById("label-color-input");
  const colorReset = document.getElementById("label-color-reset-btn");
  const DEFAULT_LABEL_COLOR = "#1a1a1a";

  function applyLabelColor(hex, persist) {
    if (hex && hex.toLowerCase() !== DEFAULT_LABEL_COLOR) {
      document.documentElement.style.setProperty("--label-color-override", hex);
    } else {
      document.documentElement.style.removeProperty("--label-color-override");
    }
    if (persist) {
      if (hex && hex.toLowerCase() !== DEFAULT_LABEL_COLOR) localStorage.setItem("labelColor", hex);
      else localStorage.removeItem("labelColor");
    }
  }
  if (colorInput) {
    const savedColor = localStorage.getItem("labelColor");
    if (savedColor) { colorInput.value = savedColor; applyLabelColor(savedColor, false); }
    colorInput.addEventListener("input", () => applyLabelColor(colorInput.value, true));
  }
  colorReset?.addEventListener("click", () => {
    if (colorInput) colorInput.value = DEFAULT_LABEL_COLOR;
    applyLabelColor(DEFAULT_LABEL_COLOR, true);
    showToast("🎨 기본 배색으로 되돌렸습니다.", "info", 1200);
  });

  /* ── 정렬 — "기본"(default) 포함 4버튼 중 하나만 활성(라디오 그룹처럼 동작) */
  const alignBtns = Array.from(document.querySelectorAll(".align-btn[data-align]"));
  function applyAlign(value, persist) {
    alignBtns.forEach(b => b.classList.toggle("active", b.dataset.align === value));
    if (value === "default") document.documentElement.removeAttribute("data-label-align");
    else document.documentElement.setAttribute("data-label-align", value);
    if (persist) localStorage.setItem("labelAlign", value);
  }
  if (alignBtns.length) {
    const savedAlign = localStorage.getItem("labelAlign") || "default";
    applyAlign(savedAlign, false);
    alignBtns.forEach(btn => btn.addEventListener("click", () => applyAlign(btn.dataset.align, true)));
  }
})();

/* ══════════════════════════════════════════════════════════════
   정렬 테스트 인쇄 — 내용 없이 "칸 테두리 + 중심 십자선"만 그린
   페이지를 일반 A4 용지에 인쇄한다. 실제 라벨지 위에 겹쳐 빛에
   비춰보면, 라벨지를 낭비하지 않고도 여백/간격이 맞는지 바로
   확인할 수 있다.
   ── 현재 선택된 템플릿(TEMPLATE_CONFIGS)과, 인쇄 위치 미세조정으로
      보정된 --page-pt/--page-pl/--grid-cols 등 CSS 변수를 실제
      라벨 페이지(.a4-page)와 그대로 공유하므로, 화면에 보이는
      라벨 페이지와 100% 동일한 위치/간격으로 인쇄된다.
   ══════════════════════════════════════════════════════════════ */
function buildTestPrintPage() {
  const c = TEMPLATE_CONFIGS[templateSelect.value];
  if (!c) return null;
  const page = document.createElement("div");
  page.className = "a4-page test-print-page";
  const count = c.perPage || (c.colN && c.rowN ? c.colN * c.rowN : 16);
  for (let i = 0; i < count; i++) {
    page.appendChild(document.createElement("div")).className = "test-cell";
  }
  return page;
}
async function printTestGrid() {
  const c = TEMPLATE_CONFIGS[templateSelect.value];
  if (!c) { showToast("⚠️ 라벨 규격을 먼저 선택해주세요.", "warn"); return; }
  /* 정렬 테스트 인쇄는 여백/배율 오차에 특히 더 민감하다(오차가 있으면
     테스트 자체가 무의미해짐) — 정식 인쇄와 동일한 체크리스트로 확인 */
  if (!await confirmPrintSettings()) return;
  document.querySelectorAll(".test-print-page").forEach(p => p.remove());
  const page = buildTestPrintPage();
  if (!page) return;
  /* postLabelContainer가 아닌 canvas-scroll에 직접 붙인다 — 라벨 개수 집계,
     미니맵/가상스크롤/데이터검증 등 postLabelContainer 내부 ".label"·".a4-page"를
     감시하는 기존 로직에 영향을 주지 않기 위함 (기존 기능 완전 보존) */
  canvasScroll.appendChild(page);
  document.body.classList.add("test-print-mode");
  showToast("🖨️ 일반 A4 용지에 인쇄한 뒤, 실제 라벨지 위에 겹쳐 빛에 비춰 확인해보세요.", "info", 4500);
  window.print();
}
document.getElementById("insp-test-print")?.addEventListener("click", printTestGrid);
window.addEventListener("afterprint", () => {
  document.body.classList.remove("test-print-mode");
  document.querySelectorAll(".test-print-page").forEach(p => p.remove());
});

/* ══════════════════════════════════════════════════════════════
   인쇄 미리보기 — 사이드바의 작은 그리드 미리보기(#addr-grid-preview)는
   내용 없는 색상 블록만 보여주고, 브라우저 인쇄 대화상자는 인쇄를
   실행해야만 열린다. 그 사이를 메우기 위해, 실제 .a4-page/.label
   DOM을 그대로 복제해 편집용 UI만 걷어낸 뒤 실물 크기(mm 그대로)로
   보여준다 — 내용·간격이 실제 인쇄 결과와 100% 동일하다.
   [2026-07 수정] 예전에는 "많은 페이지를 그대로 복제하면 무거워질 수
   있다"는 이유로 최대 8페이지까지만 미리보기하고 나머지는 생략했지만,
   실제로는 마지막 페이지까지 인쇄 전에 확인하지 못하는 문제가 더
   컸다. 이제 전체 페이지를 다 보여주되, 성능은 "화면에 보일 때만
   실제로 복제해 채우는" 지연 렌더링(lazy mount)으로 지킨다 — 처음
   몇 페이지만 즉시 그리고, 나머지는 크기만 잡아둔 빈 자리로 두었다가
   스크롤이 다가오면 그때 실제 내용을 채운다(한 번 채운 뒤에는 계속
   유지). */
const PRINT_PREVIEW_EAGER_PAGES = 6; /* 처음 화면에 즉시 그릴 페이지 수 */
/* [일반화] canvasId를 받아 어느 컨테이너에든 동일한 미리보기를 그릴 수 있게 함.
   기존 호출부(인자 없이 호출)는 지금까지와 완전히 동일하게 동작 —
   3단계(출력) 화면에 실물 미리보기를 그대로 재사용하기 위한 확장. */
function buildPrintPreviewPages(canvasId = "print-preview-canvas", infoId = "print-preview-page-info") {
  const wrap = document.getElementById(canvasId);
  if (!wrap) return 0;

  /* 이전 호출에서 만들어 둔 지연 로딩 감시자가 남아 있으면 먼저 정리 */
  if (wrap._lazyPageObserver) { wrap._lazyPageObserver.disconnect(); wrap._lazyPageObserver = null; }

  wrap.innerHTML = "";
  const realPages = Array.from(postLabelContainer.querySelectorAll(".a4-page:not(.test-print-page)"));

  function mountRealPage(fitWrap, page) {
    const clone = page.cloneNode(true);
    clone.classList.remove("page-vs-hidden", "fit-active");
    clone.style.removeProperty("transform");
    clone.style.removeProperty("transform-origin");
    clone.querySelectorAll("[contenteditable]").forEach(el => { el.contentEditable = "false"; });
    fitWrap.innerHTML = "";
    fitWrap.appendChild(clone);
    fitWrap.classList.remove("preview-page-lazy");
  }

  realPages.forEach((page, i) => {
    if (realPages.length > 1) {
      const caption = document.createElement("div");
      caption.className = "preview-page-caption";
      caption.textContent = `${i + 1} 페이지`;
      wrap.appendChild(caption);
    }
    /* [수정] 페이지를 실물 크기(mm) 그대로 넣으면 이 모달(최대 820px 폭)보다
       넓어 가로로 잘리거나, 세로로 넘쳐도 스크롤이 막혀있어 전체를 볼 수
       없었다. .gl-page-fit-wrap(general-labels.js)과 동일한 방식으로 —
       실제 크기 박스는 그대로 두고 transform:scale()만 걸되, 감싸는 wrap의
       px 크기를 축소된 만큼만 잡아줘 레이아웃상 빈 공간이 남지 않게 한다. */
    const fitWrap = document.createElement("div");
    fitWrap.className = "preview-page-fit-wrap";
    fitWrap.id = canvasId + "-page-" + (i + 1); /* 페이지 탐색(pstep-page-nav)의 스크롤 대상 */

    if (i < PRINT_PREVIEW_EAGER_PAGES) {
      mountRealPage(fitWrap, page);
    } else {
      /* 아직 실제 내용은 채우지 않고 자리만 예약해 둔다 —
         scalePrintPreviewPages()가 뒤이어 올바른 축소 크기로 폭/높이를 잡아준다. */
      fitWrap.classList.add("preview-page-lazy");
      fitWrap.dataset.pageIdx = String(i);
    }
    wrap.appendChild(fitWrap);
  });

  scalePrintPreviewPages(canvasId);

  /* 지연된 페이지들은 스크롤로 가까워지면(뷰포트 기준 위아래 1000px 이내)
     그때 실제로 복제해 채운다 */
  const lazyWraps = Array.from(wrap.querySelectorAll(".preview-page-lazy"));
  if (lazyWraps.length) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (!entry.isIntersecting) return;
        const idx = parseInt(entry.target.dataset.pageIdx, 10);
        const page = realPages[idx];
        if (page) mountRealPage(entry.target, page);
        observer.unobserve(entry.target);
      });
    }, { root: wrap, rootMargin: "1000px 0px" });
    lazyWraps.forEach(w => observer.observe(w));
    wrap._lazyPageObserver = observer;
  }

  const infoEl = document.getElementById(infoId);
  if (infoEl) {
    /* [수정] 이제 페이지 수 제한 없이 전체를 보여주므로 "일부만 미리보기"
       안내 문구는 더 필요 없다 — 총 페이지 수만 알려준다(1페이지면 생략). */
    const infoText = realPages.length > 1 ? `총 ${realPages.length}페이지` : "";
    infoEl.textContent = infoText;
    infoEl.style.display = infoText ? "" : "none";
  }
  return realPages.length;
}
/* [신규] 미리보기 캔버스 너비에 맞춰 각 페이지를 축소 — "페이지 하나 전체가
   잘리지 않고 다 보이는" 것을 기본으로 한다. 세로로 여러 페이지가 쌓여
   넘치는 경우는 (이제 정상 동작하는) 세로 스크롤로 훑어보면 된다. */
/* [신규] 3단계(출력) 우측 레일의 "페이지 탐색" 카드를 채운다.
   buildPrintPreviewPages()가 이미 만들어 둔 각 페이지 래퍼(id="{canvasId}-page-N")로
   스크롤만 이동시키는 얇은 보조 UI — 미리보기 렌더링 자체(buildPrintPreviewPages)는
   전혀 건드리지 않으므로 기존 인쇄 미리보기 동작에 아무 영향이 없다. */
function renderPstepPageNav(canvasId, total) {
  const nav = document.getElementById("pstep-page-nav");
  if (!nav) return;
  nav.innerHTML = "";
  if (total <= 0) {
    nav.innerHTML = '<p class="wrail-hint">표시할 페이지가 없습니다.</p>';
    return;
  }
  if (total === 1) {
    nav.innerHTML = '<p class="wrail-hint">전체 1페이지입니다.</p>';
    return;
  }
  /* [수정] 예전에는 미리보기 자체가 8페이지로 제한돼 버튼도 8개까지만
     만들었지만, 이제 전체 페이지를 다 미리보기하므로 버튼도 전체
     페이지 수만큼 만든다 — 페이지가 많아도 .wrail-page-nav에 스크롤을
     둬(general-labels.css) 목록이 넘치지 않는다. */
  for (let i = 1; i <= total; i++) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "wrail-page-btn" + (i === 1 ? " active" : "");
    btn.textContent = String(i);
    btn.title = `${i} 페이지로 이동`;
    btn.addEventListener("click", () => {
      document.getElementById(canvasId + "-page-" + i)
        ?.scrollIntoView({ behavior: "smooth", block: "start" });
      nav.querySelectorAll(".wrail-page-btn").forEach(b => b.classList.remove("active"));
      btn.classList.add("active");
    });
    nav.appendChild(btn);
  }
}
function scalePrintPreviewPages(canvasId = "print-preview-canvas") {
  const wrap = document.getElementById(canvasId);
  if (!wrap) return;
  const PAGE_W = 794, PAGE_H = 1123; /* A4 @96dpi — calcFitScale()과 동일 상수 */
  const availW = wrap.clientWidth - 48; /* padding 24px * 2 */
  if (availW <= 0) return;
  const scale = Math.min(1, availW / PAGE_W);
  wrap.querySelectorAll(".preview-page-fit-wrap").forEach(fitWrap => {
    /* [수정] 아직 실제 내용이 채워지지 않은 지연 로딩 자리(.preview-page-lazy)도
       스크롤 공간을 정확히 예약해야 하므로, 자식(page)이 없어도 wrap
       자체의 폭/높이는 그대로 계산해 적용한다(page가 있을 때만 transform 적용). */
    const page = fitWrap.firstElementChild;
    if (scale >= 1) {
      fitWrap.style.width = ""; fitWrap.style.height = "";
      if (page) { page.style.transform = ""; page.style.transformOrigin = ""; }
    } else {
      if (page) {
        page.style.transformOrigin = "top left";
        page.style.transform = `scale(${scale})`;
      }
      fitWrap.style.width  = Math.round(PAGE_W * scale) + "px";
      fitWrap.style.height = Math.round(PAGE_H * scale) + "px";
    }
  });
}
window.addEventListener("resize", () => {
  /* 3단계(출력) 화면이 열려 있는 동안 창 크기가 바뀌어도 미리보기 축소율을 갱신 */
  const p3 = document.getElementById("step-view-print");
  if (p3 && p3.classList.contains("active")) scalePrintPreviewPages("step3-preview-canvas");
});

/* ══════════════════════════════════════════════════════════════
   인쇄 위치 미세조정 — Inspector offset 버튼
   ── 실제 계산/저장/표시 동기화는 모두 applyConfig()·getStoredOffset()·
      setStoredOffset()로 옮겨졌으므로, 여기서는 "버튼을 누르면 저장된
      값을 바꾸고 applyConfig()를 다시 부른다"는 역할만 담당한다.
   ══════════════════════════════════════════════════════════════ */
(function() {
  const STEP = 0.25;

  function adjust(dPt, dPl) {
    const key = templateSelect.value;
    const cur = getStoredOffset(key);
    const nextPt = Math.min(Math.max(cur.pt + dPt, -10), 10);
    const nextPl = Math.min(Math.max(cur.pl + dPl, -10), 10);
    setStoredOffset(key, Number(nextPt.toFixed(2)), Number(nextPl.toFixed(2)));
    applyConfig();
  }

  document.getElementById("offset-pt-up")   ?.addEventListener("click", () => adjust(STEP, 0));
  document.getElementById("offset-pt-down") ?.addEventListener("click", () => adjust(-STEP, 0));
  document.getElementById("offset-pl-up")   ?.addEventListener("click", () => adjust(0, STEP));
  document.getElementById("offset-pl-down") ?.addEventListener("click", () => adjust(0, -STEP));
  document.getElementById("offset-reset-btn")?.addEventListener("click", () => {
    setStoredOffset(templateSelect.value, 0, 0);
    applyConfig();
  });

  /* [신규] 캔버스 상단바 미니 팝오버 — 같은 adjust()/초기화 로직을 공유 */
  document.getElementById("mini-offset-pt-up")   ?.addEventListener("click", () => adjust(STEP, 0));
  document.getElementById("mini-offset-pt-down") ?.addEventListener("click", () => adjust(-STEP, 0));
  document.getElementById("mini-offset-pl-up")   ?.addEventListener("click", () => adjust(0, STEP));
  document.getElementById("mini-offset-pl-down") ?.addEventListener("click", () => adjust(0, -STEP));
  document.getElementById("mini-offset-reset-btn")?.addEventListener("click", () => {
    setStoredOffset(templateSelect.value, 0, 0);
    applyConfig();
  });
  const topbarOffsetBtn = document.getElementById("topbar-offset-btn");
  const topbarOffsetPop = document.getElementById("topbar-offset-pop");
  if (topbarOffsetBtn && topbarOffsetPop) {
    function closePop() {
      topbarOffsetPop.classList.remove("active");
      topbarOffsetBtn.classList.remove("active");
      topbarOffsetBtn.setAttribute("aria-expanded", "false");
    }
    topbarOffsetBtn.addEventListener("click", e => {
      e.stopPropagation();
      const willOpen = !topbarOffsetPop.classList.contains("active");
      topbarOffsetPop.classList.toggle("active", willOpen);
      topbarOffsetBtn.classList.toggle("active", willOpen);
      topbarOffsetBtn.setAttribute("aria-expanded", String(willOpen));
    });
    document.addEventListener("click", e => {
      if (!topbarOffsetPop.classList.contains("active")) return;
      if (topbarOffsetPop.contains(e.target) || topbarOffsetBtn.contains(e.target)) return;
      closePop();
    });
    document.addEventListener("keydown", e => { if (e.key === "Escape") closePop(); });
  }

  // 최초 로드 시 표시 동기화 (실제 CSS 변수 반영은 최초 applyConfig() 호출에서 이미 처리됨)
  const initial = getStoredOffset(templateSelect.value);
  updateOffsetDisplay(initial.pt, initial.pl);
})();
window.addEventListener("afterprint",  () => { document.body.classList.remove("hide-watermark"); const kw=searchInput.value.trim(); if(kw) runSearch(kw); applyZoom(); /* zoom 복원 */ });
window.addEventListener("beforeprint", () => {
  postLabelContainer.querySelectorAll(".label").forEach(l => l.style.removeProperty("display"));
  /* zoom/scale 인라인 스타일 제거 — 인쇄 시 정확한 mm 규격 보장 */
  postLabelContainer.querySelectorAll(".a4-page").forEach(p => p.style.removeProperty("transform"));
});

fillBtn.addEventListener("click", () => {
  const all = Array.from(postLabelContainer.querySelectorAll(".label"));
  if (!all.length) return showToast("⚠️ 기준이 될 라벨이 없습니다. 먼저 라벨을 추가해주세요.", "warn");
  const perPage = TEMPLATE_CONFIGS[templateSelect.value]?.perPage||16;
  const rem = all.length % perPage;
  if (rem===0) return showToast(`✅ 현재 페이지가 이미 꽉 찼습니다. (${all.length/perPage}페이지 완성)`, "info");
  const needed = perPage - rem;
  const lEl  = all[all.length-1];
  const item = { "이름":(lEl.querySelector(".name")?.textContent||"").replace(/ 귀하$/,""), "주소":lEl.querySelector(".addr")?.textContent||"", "postCode":Array.from(lEl.querySelectorAll(".post-digit")).map(d=>d.textContent).join("")||"     " };
  const frag = document.createDocumentFragment(); let last=null;
  for (let i=0;i<needed;i++) { last=createLabel(item); frag.appendChild(last); }
  postLabelContainer.appendChild(frag); saveToStorage(); reflowPages(); updateLabelCount();
  if (last) scrollToLabel(last);
  showToast(`📄 ${needed}개를 추가해 한 페이지를 채웠습니다.`, "success");
});

/* ══════════════════════════════════════════════════════════════
   파일 드롭
   ══════════════════════════════════════════════════════════════ */
const dropOverlay = document.getElementById("drop-overlay");
/* 버그 수정: 이 문서 전역 드래그앤드롭은 "주소 라벨" 탭의 엑셀 가져오기 전용이다.
   document에 등록돼 있어 "일반 라벨" 탭에서 이미지를 칸에 끌어다 놓을 때도 이벤트가
   버블링되어 함께 실행되는 바람에, 엑셀 오버레이가 뜨거나 "xlsx만 지원합니다" 알림이
   가로채 일반 라벨의 이미지 드래그앤드롭이 동작하지 않았다. mode-general(일반 라벨
   탭이 활성화된 상태)일 때는 이 핸들러가 아예 개입하지 않도록 가장 먼저 걸러낸다. */
document.addEventListener("dragover", e => {
  if (document.body.classList.contains("mode-general")) return;
  e.preventDefault(); e.stopPropagation();
  if (dragSrcLabel) return;
  dropOverlay.classList.add("active");
});
document.addEventListener("dragleave", e => {
  if (document.body.classList.contains("mode-general")) return;
  e.preventDefault(); e.stopPropagation();
  if (e.relatedTarget===null) dropOverlay.classList.remove("active");
});
document.addEventListener("drop", e => {
  if (document.body.classList.contains("mode-general")) return;
  e.preventDefault(); e.stopPropagation(); dropOverlay.classList.remove("active");
  if (dragSrcLabel) return;
  const file = e.dataTransfer.files[0];
  if (file && /\.(xlsx?|txt|hwpx)$/i.test(file.name)) handleImportFile(file);
  else if (file) showAlert(".xlsx, .xls, .hwpx, .txt 파일만 지원합니다.", "📂", "지원하지 않는 파일");
});

/* ══════════════════════════════════════════════════════════════
   인스펙터 방향 버튼
   ══════════════════════════════════════════════════════════════ */
/* [제거] orient-portrait / orient-landscape 이벤트 — HTML에 해당 버튼 요소가 없음.
   인스펙터의 "방향: 세로"는 현재 정적 텍스트로 표시됨 */

/* ══════════════════════════════════════════════════════════════
   단축키 모달
   ══════════════════════════════════════════════════════════════ */
(function(){
  const modal    = document.getElementById("shortcut-modal");
  const openBtn  = document.getElementById("shortcut-btn");
  const guideBtn = document.getElementById("guide-btn");   /* 사용법 버튼 */
  const closeBtn = document.getElementById("shortcut-close");
  if (!modal) return;

  /* ── 탭 전환 헬퍼 */
  function switchTab(tabId) {
    modal.querySelectorAll(".shortcut-tab").forEach(t =>
      t.classList.toggle("active", t.dataset.tab === tabId));
    modal.querySelectorAll(".shortcut-tab-panel").forEach(p =>
      p.classList.toggle("active", p.dataset.panel === tabId));
  }

  /* ── 모달 열기 — tabId 생략 시 기본 "keys" 탭 */
  function open(tabId = "keys") {
    modal.style.display = "flex";
    requestAnimationFrame(() => {
      modal.classList.add("active");
      switchTab(tabId);
    });
  }
  function close() {
    modal.classList.remove("active");
    modal.addEventListener("transitionend", () => {
      if (!modal.classList.contains("active")) modal.style.display = "none";
    }, { once: true });
  }

  openBtn?.addEventListener("click",  () => open("keys"));   /* 단축키 탭으로 열기 */
  guideBtn?.addEventListener("click", () => open("guide"));  /* 사용법 탭으로 바로 열기 */
  closeBtn?.addEventListener("click", close);
  modal.addEventListener("click", e => { if (e.target === modal) close(); });

  /* ── 모달 내 탭 버튼 클릭 → 탭 전환 */
  modal.querySelectorAll(".shortcut-tab").forEach(tab => {
    tab.addEventListener("click", () => switchTab(tab.dataset.tab));
  });

  document.addEventListener("keydown", e => {
    const ed = e.target.isContentEditable || ["INPUT","TEXTAREA","SELECT"].includes(e.target.tagName);
    if (e.key === "Escape" && modal.classList.contains("active")) { close(); return; }
    if (!ed && e.key === "?") open("keys");
  });
})();

undoBtn.addEventListener("click", () => undoDelete());

/* ══════════════════════════════════════════════════════════════
   전역 단축키 (원본 누락 없이 완전 복원)
   ══════════════════════════════════════════════════════════════ */
document.addEventListener("keydown", e => {
  /* [버그 수정: 중요] 이 핸들러는 주소라벨 전용 단축키(Ctrl+Z 되돌리기부터
     Ctrl+N 라벨추가·Delete 삭제까지)를 모아둔 곳인데, 지금까지 "지금
     일반라벨 탭을 보고 있는지"를 전혀 확인하지 않아, 일반라벨 화면에서
     Ctrl+Z를 눌러도 화면엔 보이지도 않는 주소라벨의 undoStack이 함께
     반응했다(일반라벨 자체 실행취소와 이중으로 겹쳐 동작). 드래그앤드롭
     등 다른 곳에선 이미 쓰고 있는 것과 같은 가드를 여기에도 추가한다. */
  if (document.body.classList.contains("mode-general")) return;
  const ed = e.target.isContentEditable||["INPUT","TEXTAREA","SELECT"].includes(e.target.tagName);
  /* [버그 수정] 실행취소/다시실행은 "편집 중이어도 항상 동작"해야 하지만,
     모달(예: 라벨 직접 추가 창, 즐겨찾기 이름 입력 등) 안에서 입력 중일
     때만은 예외로 둔다 — 그 안에서 타이핑한 값을 고치려는 것일 뿐, 뒤에
     가려진 라벨 목록을 되돌리려는 의도가 아닐 가능성이 높고, 모달이 뜬
     채로 뒤 화면이 바뀌면 오히려 혼란스럽다. */
  const inModal = !!e.target.closest(".modal-bg");

  /* [버그 수정: "글자 입력하고 나서 Ctrl+Z/Ctrl+Y가 안 먹어요"]
     실행취소·다시실행은 다른 단축키(Ctrl+N, Delete, Ctrl+A 등)와 성격이
     다르다 — 저것들은 텍스트를 편집하는 중엔 오히려 방해가 되므로 의도적으로
     막아뒀지만(예: 편집 중 Delete는 글자를 지워야지 라벨 전체를 지우면 안 됨),
     실행취소·다시실행은 "언제든 눌러도 되는 안전망"이라 입력 칸에 커서가
     있어도(모달 안이 아닌 한) 항상 동작해야 한다. 그런데 주소/이름 편집
     내용은 blur(포커스 아웃)될 때만 undoStack에 기록되므로, 방금 타이핑한
     뒤 커서가 아직 그 칸에 남아 있는 상태에서 바로 Ctrl+Z를 누르면
     ①아래 !ed 가드에 걸려 아무 반응이 없거나 ②설령 반응해도 그 방금 친
     타이핑은 아직 undoStack에 없어 되돌릴 게 없는 것처럼 보였다. → 모달
     밖에서는 이 두 단축키를 !ed 가드에서 빼고, 먼저 현재 포커스를
     blur시켜 "지금 편집 중이던 내용"을 undoStack에 커밋한 다음
     실행취소/다시실행을 진행한다. */
  if (!inModal && (e.ctrlKey||e.metaKey) && !e.shiftKey && e.key==="z") {
    e.preventDefault();
    if (ed && document.activeElement && typeof document.activeElement.blur === "function") {
      document.activeElement.blur(); /* 방금 편집 중이던 내용을 먼저 undoStack에 커밋 */
    }
    undoStack.length ? undoDelete() : showToast("↩ 더 이상 취소할 내용이 없습니다.", "info", 2000);
    return;
  }
  // Ctrl+Shift+Z / Ctrl+Y : Redo (다시실행) — [버그 수정] 위 실행취소와 동일한 이유로 !ed 가드 제외(모달 안은 예외)
  if (!inModal && (e.ctrlKey||e.metaKey) && (
    (e.shiftKey && e.key === "Z") ||
    (!e.shiftKey && e.key === "y")
  )) {
    e.preventDefault();
    if (ed && document.activeElement && typeof document.activeElement.blur === "function") {
      document.activeElement.blur();
    }
    if (!redoStack.length) { showToast("↪ 더 이상 다시 실행할 내용이 없습니다.", "info", 2000); return; }
    window.redoAction();
    return;
  }

  // Ctrl+N : 라벨 추가 원본에 있었음
  if (!ed && (e.ctrlKey||e.metaKey) && e.key==="n") {
    e.preventDefault(); addBtn.click(); return;
  }
  // Ctrl+Shift+A : 라벨 추가
  if (!ed && (e.ctrlKey||e.metaKey) && e.shiftKey && e.key==="A") {
    e.preventDefault(); addBtn.click(); return;
  }
  // Ctrl+D : 선택 라벨 복사 원본에 있었음
  if (!ed && (e.ctrlKey||e.metaKey) && e.key==="d") {
    e.preventDefault();
    const sel = postLabelContainer.querySelector(".label.label-selected");
    if (sel) sel.querySelector(".copy-label-btn")?.click();
    else copyBtn.click();
    return;
  }
  // Ctrl+[ / Ctrl+] : 글자 크기
  if ((e.ctrlKey||e.metaKey) && (e.key==="["||e.key==="]")) {
    e.preventDefault(); stepFontSize(e.key==="["?-1:1); return;
  }
  // Ctrl+B : 굵게 토글
  if ((e.ctrlKey||e.metaKey) && e.key==="b") {
    e.preventDefault();
    document.getElementById("bold-toggle-btn")?.click();
    return;
  }
  // Ctrl+P : 인쇄
  if ((e.ctrlKey||e.metaKey) && e.key==="p") {
    e.preventDefault(); printBtn.click(); return;
  }
  // Ctrl+S : 강제 저장
  if (!ed && (e.ctrlKey||e.metaKey) && e.key==="s") {
    e.preventDefault(); saveToStorage(); showToast("💾 저장됐습니다.", "info", 1200); return;
  }
  // Ctrl+A : 전체 선택
  if (!ed && (e.ctrlKey||e.metaKey) && e.key==="a") {
    e.preventDefault();
    postLabelContainer.querySelectorAll(".label").forEach(l=>l.classList.add("label-selected"));
    return;
  }
  // ESC : 선택 해제
  if (!ed && e.key==="Escape") {
    postLabelContainer.querySelectorAll(".label.label-selected").forEach(l=>l.classList.remove("label-selected"));
    return;
  }
  // Delete : 선택 라벨 삭제
  if (!ed && e.key==="Delete") {
    const sel = Array.from(postLabelContainer.querySelectorAll(".label.label-selected:not(.label-locked)"));
    if (!sel.length) return;
    sel.forEach(l => {
      undoStack.push({ type:"delete", html:l.outerHTML, index:Array.from(postLabelContainer.querySelectorAll(".label")).indexOf(l) });
      l.remove();
    });
    if (undoStack.length>MAX_UNDO) undoStack.splice(0, undoStack.length-MAX_UNDO);
    redoStack.length = 0; /* 새 액션 → Redo 초기화 */
    saveToStorage(); reflowPages(); updateLabelCount(); updateUndoBtn();
    showToast(`🗑️ ${sel.length}개 삭제됨. <button onclick="undoDelete()" class="toast-undo-btn">↩ 취소</button>`, "warn", 5000);
    return;
  }
  // Alt+L : 잠금/해제 원본에 있었음
  if (!ed && e.altKey && (e.key==="l"||e.key==="L")) {
    postLabelContainer.querySelectorAll(".label.label-selected").forEach(l=>l.querySelector(".lock-label-btn")?.click());
    return;
  }
  // / or Ctrl+F : 검색
  if (!ed && (e.key==="/"||((e.ctrlKey||e.metaKey)&&e.key==="f"))) {
    e.preventDefault(); searchInput.focus(); searchInput.select(); return;
  }
  // Ctrl+Home / Ctrl+End
  if (!ed && (e.ctrlKey||e.metaKey) && e.key==="Home") {
    e.preventDefault();
    const first = postLabelContainer.querySelector(".label"); if(first) scrollToLabel(first); return;
  }
  if (!ed && (e.ctrlKey||e.metaKey) && e.key==="End") {
    e.preventDefault();
    const all = postLabelContainer.querySelectorAll(".label"); if(all.length) scrollToLabel(all[all.length-1]); return;
  }
  // Fit 모드 화살표 / Page Up/Down
  if (viewMode==="fit") {
    const isEd = e.target.isContentEditable||["INPUT","TEXTAREA","SELECT"].includes(e.target.tagName);
    if (!isEd) {
      if (e.key==="ArrowLeft" ||e.key==="PageUp")  { e.preventDefault(); applyFitPage(fitCurrentPage-1); return; }
      if (e.key==="ArrowRight"||e.key==="PageDown") { e.preventDefault(); applyFitPage(fitCurrentPage+1); return; }
    }
  } else {
    // 스크롤 모드 Page Up/Down
    if (!ed && e.key==="PageDown") { e.preventDefault(); canvasScroll.scrollBy({top: canvasScroll.clientHeight*0.9, behavior:"smooth"}); }
    if (!ed && e.key==="PageUp")   { e.preventDefault(); canvasScroll.scrollBy({top:-canvasScroll.clientHeight*0.9, behavior:"smooth"}); }
  }
});

/* ══════════════════════════════════════════════════════════════
   즐겨찾기 (원본 완전 동일)
   ══════════════════════════════════════════════════════════════ */
(function(){
  const FAV_KEY  = "favorites";
  const panel    = document.getElementById("fav-panel");
  const openBtn  = document.getElementById("fav-btn");
  const closeBtn = document.getElementById("fav-close");
  const sbFav    = document.getElementById("sb-fav");
  const list     = document.getElementById("fav-list");
  const nameInp  = document.getElementById("fav-name");
  const addrInp  = document.getElementById("fav-addr");
  const addBtn2  = document.getElementById("fav-add");

  const loadFavs = () => { try { return JSON.parse(localStorage.getItem(FAV_KEY))||[]; } catch { return []; } };
  const saveFavs = arr => localStorage.setItem(FAV_KEY, JSON.stringify(arr));

  function renderFavs() {
    const favs = loadFavs();
    if (!favs.length) { list.innerHTML='<p class="fav-empty">저장된 즐겨찾기가 없습니다.<br>아래에서 주소를 추가해보세요.</p>'; return; }
    list.innerHTML = favs.map((f,i) => `
      <div class="fav-item">
        <div class="fav-item-info">
          <span class="fav-item-name">${escHtml(f.name)}</span>
          <span class="fav-item-addr">${escHtml(f.addr)}</span>
        </div>
        <div class="fav-item-btns">
          <button class="fav-btn fav-use" data-idx="${i}" title="라벨 추가" aria-label="${escHtml(f.name)} 라벨로 추가">＋</button>
          <button class="fav-btn fav-del" data-idx="${i}" title="삭제" aria-label="${escHtml(f.name)} 즐겨찾기 삭제">×</button>
        </div>
      </div>`).join("");
  }
  function openPanel()  { renderFavs(); panel.classList.add("open"); openBtn?.classList.add("fav-active"); }
  function closePanel() { panel.classList.remove("open"); openBtn?.classList.remove("fav-active"); }

  openBtn?.addEventListener("click",  e=>{ e.stopPropagation(); panel.classList.contains("open")?closePanel():openPanel(); });
  sbFav  ?.addEventListener("click",  e=>{ e.stopPropagation(); panel.classList.contains("open")?closePanel():openPanel(); });
  closeBtn?.addEventListener("click", closePanel);
  document.addEventListener("click",  e=>{ if(!panel.contains(e.target)&&e.target!==openBtn&&e.target!==sbFav) closePanel(); });

  addBtn2?.addEventListener("click", () => {
    const name=nameInp.value.trim(), addr=addrInp.value.trim();
    if (!name||!addr) { showToast("⚠️ 이름과 주소를 모두 입력해주세요.", "warn"); return; }
    const favs=loadFavs();
    if (favs.some(f=>f.addr===addr)) { showToast("이미 등록된 주소입니다.", "info"); return; }
    favs.push({name,addr}); saveFavs(favs); nameInp.value=""; addrInp.value=""; renderFavs();
    showToast(`⭐ "${name}" 즐겨찾기 추가됨`, "success");
  });
  list.addEventListener("click", e => {
    const useBtn = e.target.closest(".fav-use");
    const delBtn = e.target.closest(".fav-del");
    if (useBtn) {
      const idx=parseInt(useBtn.dataset.idx), f=loadFavs()[idx]; if(!f) return;
      const nw=createLabel({"이름":f.name,"주소":f.addr,"postCode":"     "});
      postLabelContainer.appendChild(nw); reflowPages(); saveToStorage(); updateLabelCount(); scrollToLabel(nw,true);
      fetchPostCode(f.addr).then(code=>{
        const pc=nw.querySelector(".post-code-container");
        if(pc) { pc.innerHTML=digitsHTML(code); syncPostcodeLock(nw); }
        saveToStorage();
      });
      closePanel(); showToast(`📮 "${f.name}" 라벨 추가됨`, "success");
    }
    if (delBtn) {
      const idx=parseInt(delBtn.dataset.idx), favs=loadFavs(), removed=favs.splice(idx,1)[0];
      saveFavs(favs); renderFavs(); showToast(`🗑️ "${removed?.name}" 삭제됨`, "info", 2000);
    }
  });
  [nameInp,addrInp].forEach(el=>el?.addEventListener("keydown",e=>{ if(e.key==="Enter"){e.preventDefault();addBtn2.click();} }));
})();

/* ══════════════════════════════════════════════════════════════
   미니맵 (원본 완전 동일)
   ══════════════════════════════════════════════════════════════ */
window.updateMinimap = function() {
  const grid    = document.getElementById("mm-grid");
  const countEl = document.getElementById("mm-count");
  if (!grid) return;
  const labels  = Array.from(postLabelContainer.querySelectorAll(".label"));
  const c       = TEMPLATE_CONFIGS[templateSelect.value];
  const perPage = c?.perPage||16;
  const cols    = c ? parseInt(c.cols.match(/repeat\((\d+)/)?.[1]||"2") : 2;
  if (countEl) countEl.textContent = `(${labels.length}개)`;
  if (!labels.length) { grid.innerHTML='<p class="mm-empty">라벨 없음</p>'; return; }
  const pages = [];
  for (let i=0;i<labels.length;i+=perPage) pages.push(labels.slice(i,i+perPage));
  grid.innerHTML = pages.map((pl,pi) => {
    const lbl  = `<span class="mm-page-label">P${pi+1}</span>`;
    const dots = pl.map((l,li) => {
      const idx = pi*perPage+li;
      const hasContent = !!(l.querySelector(".addr")?.textContent?.trim()||l.querySelector(".name")?.textContent?.trim());
      const isLocked   = l.classList.contains("label-locked");
      const isSel      = l.classList.contains("label-selected");
      let cls = "mm-dot";
      if (isSel)        cls += " mm-selected";
      else if (isLocked)cls += " mm-locked";
      else if (hasContent) cls += " mm-filled";
      const name = l.querySelector(".name")?.textContent?.trim()||"";
      const addr = l.querySelector(".addr")?.textContent?.trim()||"";
      const tip  = name||addr ? `#${idx+1} ${name}${addr?" · "+addr.slice(0,20):""}` : `#${idx+1} (비어있음)`;
      return `<span class="${cls}" data-idx="${idx}" title="${escHtml(tip)}"></span>`;
    }).join("");
    return `<div class="mm-page" style="--mm-cols:${cols}">${lbl}${dots}</div>`;
  }).join("");
  grid.querySelectorAll(".mm-dot").forEach(dot => {
    dot.addEventListener("click", () => {
      const idx    = parseInt(dot.dataset.idx);
      const target = postLabelContainer.querySelectorAll(".label")[idx];
      if (target) { target.scrollIntoView({behavior:"smooth",block:"center"}); target.classList.add("label-highlight"); setTimeout(()=>target.classList.remove("label-highlight"),1500); }
    });
  });
};
/* 성능: 미니맵 업데이트를 throttle로 제한 (대량 편집 시 과도한 호출 방지) */
const _throttledMinimap = throttle(window.updateMinimap, 150);
const mmObs = new MutationObserver(() => {
  clearTimeout(window._mmTimer);
  window._mmTimer = setTimeout(_throttledMinimap, 120);
});
mmObs.observe(postLabelContainer, { childList:true, subtree:true, characterData:true });


/* ══════════════════════════════════════════════════════════════
   컨텍스트 메뉴 공용 키보드 탐색 (접근성)
   ──────────────────────────────────────────────────────────────
   .ctx-menu 구조를 쓰는 모든 우클릭 메뉴(label-context-menu,
   gl-context-menu)에서 공용으로 사용. ESC로 닫기는 각 메뉴에서
   이미 개별 처리 중이므로 여기서는 건드리지 않고, 방향키로
   항목 간 이동 + 메뉴가 열릴 때 첫 항목 자동 포커스만 담당한다.
   ══════════════════════════════════════════════════════════════ */
function attachCtxMenuKeyboardNav(menuEl) {
  if (!menuEl || menuEl.dataset.kbdNavBound) return;
  menuEl.dataset.kbdNavBound = "1";

  const getItems = () => Array.from(menuEl.querySelectorAll(".ctx-item:not([disabled])"))
    .filter(el => el.offsetParent !== null); // gl-context-menu처럼 상황에 따라 display:none으로 숨겨지는 항목 제외

  function moveFocus(delta) {
    const items = getItems();
    if (!items.length) return;
    const curIdx = items.indexOf(document.activeElement);
    const nextIdx = curIdx === -1
      ? (delta > 0 ? 0 : items.length - 1)
      : (curIdx + delta + items.length) % items.length;
    items[nextIdx].focus();
  }

  menuEl.addEventListener("keydown", e => {
    switch (e.key) {
      case "ArrowDown": e.preventDefault(); moveFocus(1);  break;
      case "ArrowUp":   e.preventDefault(); moveFocus(-1); break;
      case "Home": {
        e.preventDefault();
        const items = getItems();
        if (items.length) items[0].focus();
        break;
      }
      case "End": {
        e.preventDefault();
        const items = getItems();
        if (items.length) items[items.length - 1].focus();
        break;
      }
      /* Enter/Space는 <button> 기본 동작이 그대로 클릭을 발생시키므로 별도 처리 불필요 */
    }
  });

  /* 메뉴가 열릴 때(ctx-open 클래스 부착) 첫 번째 활성 항목에 자동 포커스 이동 */
  new MutationObserver(() => {
    if (menuEl.classList.contains("ctx-open")) {
      const items = getItems();
      if (items.length) items[0].focus();
    }
  }).observe(menuEl, { attributes: true, attributeFilter: ["class"] });
}

/* ══════════════════════════════════════════════════════════════
   라벨 우클릭 컨텍스트 메뉴
   ──────────────────────────────────────────────────────────────
   ① 라벨(또는 선택된 라벨들) 위에서 우클릭
   ② 복사 / 붙여넣기 / 삭제 / 잠금 / 현재 페이지 채우기
   ③ 기존 모든 이벤트·함수 100% 유지
   ══════════════════════════════════════════════════════════════ */
(function () {
  /* ── 내부 클립보드 (브라우저 클립보드 API 없이도 동작) ── */
  let _ctxClipboard = null;   // { 이름, 주소, postCode }

  const menu    = document.getElementById("label-context-menu");
  const itmCopy  = document.getElementById("ctx-copy");
  const itmPaste = document.getElementById("ctx-paste");
  const itmDel   = document.getElementById("ctx-delete");
  const itmLock  = document.getElementById("ctx-lock");
  const itmFill  = document.getElementById("ctx-fill");
  const lockLabel = document.getElementById("ctx-lock-label");

  if (!menu) return;
  attachCtxMenuKeyboardNav(menu);

  /* ── 1. 메뉴 열기 ─────────────────────────────────────── */
  let _targetLabel = null;   // 우클릭한 라벨 요소

  function openMenu(x, y, labelEl) {
    _targetLabel = labelEl;

    /* 잠금 상태에 따라 UI 반영 */
    const isLocked = labelEl.classList.contains("label-locked");
    menu.classList.toggle("ctx-locked", isLocked);
    if (lockLabel) lockLabel.textContent = isLocked ? "잠금 해제" : "잠금";

    /* 선택된 라벨이 여러 개일 때: 삭제·잠금에 "N개" 표시 */
    const selCount = postLabelContainer.querySelectorAll(".label.label-selected").length;
    const multi    = selCount > 1;
    if (itmDel)  itmDel.querySelector("span").textContent  = multi ? `삭제 (${selCount}개)` : "삭제";
    if (itmLock) lockLabel.textContent = isLocked
      ? (multi ? `잠금 해제 (${selCount}개)` : "잠금 해제")
      : (multi ? `잠금 (${selCount}개)` : "잠금");

    /* 붙여넣기 비활성 처리 */
    if (itmPaste) {
      if (_ctxClipboard) {
        itmPaste.removeAttribute("disabled");
        itmPaste.removeAttribute("aria-disabled");
      } else {
        itmPaste.setAttribute("disabled", "");
        itmPaste.setAttribute("aria-disabled", "true");
      }
    }

    /* 삭제 — 잠긴 라벨 단독 우클릭 시 비활성 */
    if (itmDel) {
      const lockedSel = postLabelContainer.querySelectorAll(".label.label-selected.label-locked").length;
      const allSelectedLocked = multi ? (lockedSel === selCount) : isLocked;
      if (allSelectedLocked) {
        itmDel.setAttribute("disabled", "");
        itmDel.setAttribute("aria-disabled", "true");
      } else {
        itmDel.removeAttribute("disabled");
        itmDel.removeAttribute("aria-disabled");
      }
    }

    /* 위치 계산 — 화면 넘침 보정 */
    menu.style.display = "block";
    menu.style.left = "0px";
    menu.style.top  = "0px";
    menu.classList.remove("ctx-flip-x", "ctx-flip-y", "ctx-open");

    const mw = menu.offsetWidth;
    const mh = menu.offsetHeight;
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    let lx = x, ly = y;
    const flipX = lx + mw > vw - 8;
    const flipY = ly + mh > vh - 8;
    if (flipX) lx = Math.max(4, x - mw);
    if (flipY) ly = Math.max(4, y - mh);
    if (flipX) menu.classList.add("ctx-flip-x");
    if (flipY) menu.classList.add("ctx-flip-y");

    menu.style.left = lx + "px";
    menu.style.top  = ly + "px";
    requestAnimationFrame(() => menu.classList.add("ctx-open"));
  }

  function closeMenu() {
    menu.classList.remove("ctx-open", "ctx-locked");
    menu.addEventListener("transitionend", () => {
      if (!menu.classList.contains("ctx-open")) menu.style.display = "none";
    }, { once: true });
    _targetLabel = null;
  }

  /* ── 2. 라벨 컨테이너에 contextmenu 이벤트 등록 ────────── */
  postLabelContainer.addEventListener("contextmenu", e => {
    const labelEl = e.target.closest(".label");
    if (!labelEl) return;          // 라벨 바깥이면 무시

    e.preventDefault();            // 브라우저 기본 메뉴 차단

    /* 우클릭한 라벨이 선택 목록에 없으면 단독 선택으로 전환 */
    if (!labelEl.classList.contains("label-selected")) {
      postLabelContainer.querySelectorAll(".label.label-selected")
        .forEach(l => l.classList.remove("label-selected"));
      labelEl.classList.add("label-selected");
      lastSelectedLabel = labelEl;
      if (typeof updateMinimap === "function") updateMinimap();
      updateLabelCount();
    }

    openMenu(e.clientX, e.clientY, labelEl);
  });

  /* ── 3. 외부 클릭·ESC·스크롤로 닫기 ───────────────────── */
  document.addEventListener("click", e => {
    if (menu.classList.contains("ctx-open") && !menu.contains(e.target)) closeMenu();
  });
  document.addEventListener("keydown", e => {
    if (e.key === "Escape" && menu.classList.contains("ctx-open")) { closeMenu(); }
  }, true);
  document.addEventListener("scroll", () => { if (menu.classList.contains("ctx-open")) closeMenu(); }, true);

  /* ── 4. 각 메뉴 항목 동작 ──────────────────────────────── */

  /* 복사 */
  itmCopy?.addEventListener("click", () => {
    closeMenu();
    if (!_targetLabel) return;
    _ctxClipboard = {
      "이름":     (_targetLabel.querySelector(".name")?.textContent || "").replace(/ 귀하$/, ""),
      "주소":      _targetLabel.querySelector(".addr")?.textContent || "",
      "postCode": Array.from(_targetLabel.querySelectorAll(".post-digit")).map(d => d.textContent).join("") || "     "
    };
    /* 기존 copy-label-btn 로직과 동일하게 즉시 복제도 수행 */
    const nw = createLabel(_ctxClipboard);
    _targetLabel.after(nw);
    saveToStorage(); reflowPages(); updateLabelCount(); scrollToLabel(nw);
    showToast("📋 라벨 복사됨.", "success");
  });

  /* 붙여넣기 */
  itmPaste?.addEventListener("click", () => {
    closeMenu();
    if (!_ctxClipboard) return;
    const nw = createLabel(_ctxClipboard);
    /* 우클릭한 라벨 뒤에 삽입, 없으면 컨테이너 맨 끝 */
    if (_targetLabel && _targetLabel.isConnected) _targetLabel.after(nw);
    else postLabelContainer.appendChild(nw);
    saveToStorage(); reflowPages(); updateLabelCount(); scrollToLabel(nw);
    showToast("📋 라벨 붙여넣기 완료.", "success");
  });

  /* 삭제 */
  itmDel?.addEventListener("click", () => {
    closeMenu();
    /* 선택된 라벨 전체 삭제 (기존 Delete 키 로직과 동일) */
    const sel = Array.from(
      postLabelContainer.querySelectorAll(".label.label-selected:not(.label-locked)")
    );
    if (!sel.length) return;
    sel.forEach(l => {
      undoStack.push({
        type: "delete",
        html: l.outerHTML,
        index: Array.from(postLabelContainer.querySelectorAll(".label")).indexOf(l)
      });
      l.remove();
    });
    if (undoStack.length > MAX_UNDO) undoStack.splice(0, undoStack.length - MAX_UNDO);
    redoStack.length = 0; /* 새 액션 → Redo 초기화 */
    saveToStorage(); reflowPages(); updateLabelCount(); updateUndoBtn();
    showToast(
      `🗑️ ${sel.length}개 삭제됨. <button onclick="undoDelete()" class="toast-undo-btn">↩ 취소</button>`,
      "warn", 5000
    );
  });

  /* 잠금 / 잠금 해제 */
  itmLock?.addEventListener("click", () => {
    closeMenu();
    /* 선택된 라벨 전체에 잠금 토글 (기존 Alt+L 로직과 동일) */
    postLabelContainer.querySelectorAll(".label.label-selected")
      .forEach(l => l.querySelector(".lock-label-btn")?.click());
  });

  /* 현재 페이지 채우기 */
  itmFill?.addEventListener("click", () => {
    closeMenu();
    /* 기존 fillBtn.click() 과 완전히 동일한 로직 호출 */
    fillBtn.click();
  });

})(); /* end IIFE */

/* ══════════════════════════════════════════════════════════════
   사이드바 네비게이션 연결
   ══════════════════════════════════════════════════════════════ */
document.getElementById("sb-load")   ?.addEventListener("click", ()=>excelInput.click());
document.getElementById("sb-add")    ?.addEventListener("click", ()=>addBtn.click());
document.getElementById("sb-copy")   ?.addEventListener("click", ()=>copyBtn.click());
document.getElementById("sb-fill")   ?.addEventListener("click", ()=>fillBtn.click());
document.getElementById("sb-undo")   ?.addEventListener("click", ()=>undoDelete());
document.getElementById("sb-reset")  ?.addEventListener("click", ()=>resetBtn.click());
document.getElementById("sb-print")  ?.addEventListener("click", ()=>printBtn.click());
document.getElementById("sb-shortcut")?.addEventListener("click", ()=>document.getElementById("shortcut-btn")?.click());

/* ══════════════════════════════════════════════════════════════
   DOMContentLoaded 초기화
   ══════════════════════════════════════════════════════════════ */
window.addEventListener("DOMContentLoaded", async () => {
  document.documentElement.style.setProperty("--label-font", fontSelect.value);

  updateUndoBtn(); updateLabelCount(); updateMinimap();

  // Fit 모드 복원
  if (viewMode === "fit") {
    document.body.classList.add("view-fit");
    viewModeBtn.classList.add("active");
    const vmText = viewModeBtn.querySelector(".vm-label");
    if (vmText) vmText.textContent = "스크롤";
    else viewModeBtn.innerHTML = "⊞&nbsp;스크롤";
    fitNav.classList.add("visible");
  }

  /* IndexedDB 저장소 읽기는 비동기이므로, "이전 세션 정리 → 복원" 순서가
     보장된 뒤에 진행한다 (idbSessionReady 참고). */
  await idbSessionReady;

  // 마지막 사용 템플릿 복원 (라벨 복원 전에 먼저 적용)
  const savedTemplate = await idbGet("lastTemplate").catch(() => null);
  if (savedTemplate && TEMPLATE_CONFIGS[savedTemplate]) {
    templateSelect.value = savedTemplate;
    updateTemplateDisplay();
    updateInspector();
    rebuildRecentOptgroup(templateSelect, "recentTemplates");
    applyConfig();
  }

  // 저장된 라벨 복원 (원본 로직과 동일한 흐름, 저장소만 IndexedDB로 교체)
  const saved = await idbGet("lastLabels").catch(() => null);
  if (saved) {
    const tmp = document.createElement("div"); tmp.innerHTML = saved;
    const cnt = tmp.querySelectorAll(".label").length;
    if (cnt > 0) {
      const sec = document.createElement("div"); sec.className = "restore-section";
      sec.innerHTML = `<p class="restore-hint">💾 저장된 작업이 있습니다 (라벨 ${cnt}개)</p><button id="restore-btn" class="restore-btn">이전 작업 이어하기</button>`;
      uploadZone.appendChild(sec);
      /* [버그 수정] 이 안내는 "라벨 편집" 탭의 업로드존 안에만 있는데,
         3단계 마법사 도입 이후로는 라벨이 아직 없으면(= 복원 전이라
         당연히 없음) 기본 화면이 "데이터" 탭으로 바뀌어 버려서, 사용자가
         직접 "라벨 편집" 탭을 눌러보기 전엔 이 안내 자체를 볼 수 없었다
         ("저장된 작업 이어하기 기능이 사라졌다"는 피드백의 원인).
         복원 가능한 세션이 있으면 "라벨 편집" 탭으로 바로 이동시켜
         안내가 곧바로 보이게 한다 — 자동 복원은 아니고(클릭해야 실제로
         복원됨), 화면 전환만 대신 해주는 것이라 기존 "확인 후 복원"
         흐름은 그대로 유지된다. */
      document.getElementById("step-tab-edit")?.click();
      document.getElementById("restore-btn").addEventListener("click", async () => {
        // 저장된 템플릿 복원
        const savedTemplate2 = await idbGet("lastTemplate").catch(() => null);
        if (savedTemplate2 && TEMPLATE_CONFIGS[savedTemplate2]) {
          templateSelect.value = savedTemplate2;
          updateTemplateDisplay();
          updateInspector();
          rebuildRecentOptgroup(templateSelect, "recentTemplates");
          applyConfig();
        }
        postLabelContainer.innerHTML = saved;
        postLabelContainer.querySelectorAll(".label").forEach(el => {
          enableDrag(el);   // 잠금 버튼은 이벤트 위임으로 처리되어 재바인딩 불필요
        });
        reflowPages(); updateLabelCount(); updateMinimap();
        if (viewMode === "fit") applyFitPage(0);
        showToast(`📂 이전 작업 복원 완료 (라벨 ${postLabelContainer.querySelectorAll(".label").length}개)`, "success", 2000);
      });
    }
  }
});

/* ══════════════════════════════════════════════════════════════
   TASK 08 — 데이터 검증 시스템
   기존 함수/이벤트/로직 변경 없음 — 순수 추가 기능
   ══════════════════════════════════════════════════════════════ */
(function() {

  /* ── 검증 규칙 정의 ────────────────────────────────────────── */
  /* [버그 수정] 원래 \x00-\x1f 전체(제어문자 0~31)를 "문제 특수문자"로
     막았는데, 이 범위 안에는 \t(9) \n(10) \r(13)도 포함된다. 엑셀 셀에
     "Alt+Enter"로 줄바꿈을 넣어 여러 줄로 적은 주소가 매우 흔한데, 이런
     정상적인 주소를 불러오면 그 줄바꿈 문자가 그대로 이름/주소칸 내용에
     들어가 err-special로 오판정됐다 — 빈 라벨 1개를 추가했을 뿐인데
     "나머지도 전부 오류로 뜨는" 것처럼 보였던 원인이 바로 이것이다(같은
     엑셀 파일에서 여러 줄로 저장된 주소가 여러 행에 걸쳐 있었을 가능성이
     높음). 정말 문제가 되는 제어문자(널 문자, ESC 등)만 남기고
     탭/줄바꿈/캐리지리턴은 제외했다. */
  const SPECIAL_RE  = /[<>{}|\\^`\x00-\x08\x0B\x0C\x0E-\x1f]/;  /* 문제 특수문자 (탭/개행/CR 제외) */
  const POSTCODE_RE = /^\d{5}$/;                /* 정상 우편번호: 5자리 숫자 */

  /* 라벨 하나를 검사해 오류 배열 반환 */
  function validateLabel(label, idx, allAddrs) {
    const errors = [];
    const name   = label.querySelector(".name")?.textContent.trim().replace(/ 귀하$/,"") || "";
    const addr   = label.querySelector(".addr")?.textContent.trim() || "";
    const zip    = Array.from(label.querySelectorAll(".post-digit")).map(d=>d.textContent).join("").trim();

    if (!name) errors.push({ type:"err-missing",  icon:"⚠️", kind:"이름 누락",    msg:`#${idx+1} 이름이 비어있습니다.` });
    if (!addr) errors.push({ type:"err-missing",  icon:"⚠️", kind:"주소 누락",    msg:`#${idx+1} 주소가 비어있습니다.` });
    if (zip && zip.replace(/\s/g,"") && !POSTCODE_RE.test(zip.replace(/\s/g,"")))
               errors.push({ type:"err-postcode", icon:"🔢", kind:"우편번호 오류", msg:`#${idx+1} 우편번호 "${zip}" — 5자리 숫자가 아닙니다.` });
    if (SPECIAL_RE.test(name) || SPECIAL_RE.test(addr))
               errors.push({ type:"err-special",  icon:"⚡", kind:"특수문자",     msg:`#${idx+1} 이름 또는 주소에 허용되지 않는 특수문자가 있습니다.` });
    /* [수정] 텍스트 잘림(overflow) 검증 — .label은 고정 높이(--label-h)에
       overflow:hidden이 걸려있어, 내용이 칸보다 길면 인쇄 시 티 나지 않게
       조용히 잘린다. scrollHeight(실제 내용 높이)가 clientHeight(보이는
       칸 높이)를 넘으면 잘림이 발생한다는 뜻이므로, 인쇄해보기 전에 미리
       잡아낸다.
       [버그 수정] 기존엔 허용치가 1px뿐이라, 실제로는 아무 것도 잘리지
       않는(육안으로 봐도 멀쩡한) 라벨까지 오류로 잡히는 경우가 있었다.
       브라우저는 줄 높이(line-height) 계산 시 폰트마다 반 리딩(half-leading)
       분량의 여유를 서브픽셀 단위로 반올림하는데, 이게 누적되면 실제
       글자가 삐져나오지 않아도 scrollHeight가 clientHeight보다 2~3px
       "계산상"으로만 더 크게 나올 수 있다. 허용치를 넉넉히(4px)로 올려
       이런 오탐을 줄이되, 실제 잘림(보통 한 줄 분량 = 10px 이상)은
       여전히 확실히 잡아낸다. 또한 "왜 오류인지" 기준을 바로 알 수
       있도록 메시지에 실제 초과량(px)을 함께 보여준다. */
    const OVERFLOW_TOLERANCE_PX = 4;
    const overflowPx = Math.round(label.scrollHeight - label.clientHeight);
    if (overflowPx > OVERFLOW_TOLERANCE_PX)
               errors.push({ type:"err-overflow", icon:"📏", kind:"칸 높이 초과", msg:`#${idx+1} 이름/주소가 라벨 칸보다 약 ${overflowPx}px 더 길어 인쇄 시 마지막 줄 일부가 잘릴 수 있습니다. (글자 크기를 줄이거나 주소를 줄여보세요)` });

    return errors;
  }

  /* [비활성] findDuplicates — 중복 주소를 오류로 처리하지 않기로 결정.
     활성화하려면 아래를 구현하고 label-err-dup CSS 주석도 함께 복원할 것.
     현재 이 함수는 항상 []를 반환하므로, 아래 호출부의 forEach는 실행되지 않음 */
  function findDuplicates(/*labels*/) {
    return [];
  }

  /* [개선] 라벨 위 오류 배지의 아이콘 — 원래는 검증 패널과 동일한
     이모지(⚠️,🔢 등)를 그대로 썼는데, 13px 원 안에 색색의 이모지 글리프를
     욱여넣으니 흐릿하고 조잡해 보인다는 지적을 반영했다. 앱의 다른
     모든 아이콘(버튼·컨텍스트 메뉴 등)이 stroke=currentColor 방식의
     단색 선 아이콘이므로, 배지도 같은 방식의 작은 SVG로 통일해
     어느 크기·테마에서도 또렷하게 보이도록 한다. 색상은 검증 패널의
     .val-item.err-* 색상 규칙과 동일하게 CSS에서 타입별로 지정한다. */
  const BADGE_ICON_SVG = {
    "err-missing":   '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>',
    "err-postcode":  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><line x1="4" y1="9" x2="20" y2="9"/><line x1="4" y1="15" x2="20" y2="15"/><line x1="10" y1="3" x2="8" y2="21"/><line x1="16" y1="3" x2="14" y2="21"/></svg>',
    "err-special":   '<svg viewBox="0 0 24 24" fill="currentColor" stroke="none"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/></svg>',
    /* [교체] 원래 자(📏) 아이콘 — "칸 높이 초과"의 원인보다 결과(인쇄 시
       잘려나감)를 그대로 보여주는 가위 아이콘이 메시지("잘릴 수 있음")와
       더 직관적으로 연결된다고 판단해 바꿨다. */
    "err-overflow":  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><circle cx="6" cy="6" r="2.6"/><circle cx="6" cy="18" r="2.6"/><line x1="20" y1="4" x2="8.12" y2="15.88"/><line x1="14.47" y1="14.48" x2="20" y2="20"/><line x1="8.12" y1="8.12" x2="12" y2="12"/></svg>',
    /* ctx-copy 컨텍스트 메뉴와 동일한 "복사" 아이콘 재사용 — 별도 학습 없이
       "겹쳐 있다(중복)"는 뜻이 바로 연결되도록 */
    "err-duplicate": '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"/></svg>',
  };

  /* ── 검증 실행 & 패널 표시 ────────────────────────────────── */
  /* [신규] 라벨 위 오류 아이콘 배지 렌더링 — 검증 패널과 같은 정보를
     outline 색상만으로는 구분 안 되는 사람도 캔버스에서 바로 오류
     종류를 알 수 있게 한다. 같은 라벨에 오류가 여러 개면(예: 이름
     누락 + 우편번호 오류) 타입별로 하나씩만(중복 제거) 나란히 표시한다. */
  function renderErrorBadges(label, entries) {
    let wrap = label.querySelector(":scope > .label-err-badges");
    if (!entries.length) { wrap?.remove(); return; }
    if (!wrap) {
      wrap = document.createElement("div");
      wrap.className = "label-err-badges";
      label.appendChild(wrap);
    }
    wrap.innerHTML = entries.map(({ type, kind }) =>
      `<span class="label-err-badge-icon ${type}" title="${escHtml(kind)}">${BADGE_ICON_SVG[type] || ""}</span>`
    ).join("");
  }

  /* [신규] 검증 오류 건수를 상태바에도 표시 — 클릭하면 검증 패널로 스크롤 */
  function setStatusbarValidationInfo(n) {
    const sep = document.getElementById("sb-valerr-sep");
    const info = document.getElementById("sb-valerr-info");
    if (!info) return;
    if (n > 0) {
      info.style.display = "";
      if (sep) sep.style.display = "";
      info.textContent = `⚠ 검증 오류 ${n}건`;
    } else {
      info.style.display = "none";
      if (sep) sep.style.display = "none";
    }
  }
  document.getElementById("sb-valerr-info")?.addEventListener("click", () => {
    document.getElementById("inspector")?.classList.remove("collapsed");
    document.getElementById("validation-panel")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  });

  window.runValidation = function() {
    const labels = Array.from(postLabelContainer.querySelectorAll(".label"));
    if (!labels.length) { showToast("⚠️ 검증할 라벨이 없습니다.", "warn", 2000); return; }

    /* 기존 오류 강조 클래스·배지 제거 */
    labels.forEach(l => {
      l.classList.remove("label-err-missing","label-err-postcode","label-err-dup","label-err-overflow","label-err-special");
      l.querySelector(":scope > .label-err-badges")?.remove();
    });

    const allErrors = [];

    /* 개별 라벨 오류 */
    labels.forEach((label, idx) => {
      const errs = validateLabel(label, idx, []);
      errs.forEach(e => {
        allErrors.push({ ...e, labelEl: label, idx });
        if (e.type === "err-missing")  label.classList.add("label-err-missing");
        if (e.type === "err-postcode") label.classList.add("label-err-postcode");
        if (e.type === "err-overflow") label.classList.add("label-err-overflow");
        if (e.type === "err-special")  label.classList.add("label-err-special");
      });
      /* 타입별로 중복 제거해 배지로 표시 (이모지 문자가 아니라 타입 기준 —
         missing이 이름/주소 둘 다 걸려도 배지는 하나만 뜨도록) */
      const seen = new Set();
      const badgeEntries = errs.filter(e => {
        if (seen.has(e.type)) return false;
        seen.add(e.type); return true;
      }).map(e => ({ type: e.type, kind: e.kind }));
      renderErrorBadges(label, badgeEntries);
    });

    /* 중복 주소 */
    findDuplicates(labels).forEach(({ idxList, addr }) => {
      idxList.forEach(idx => {
        const label = labels[idx];
        label.classList.add("label-err-dup");
        allErrors.push({
          type: "err-duplicate", icon: "🔁", kind: "중복 주소",
          msg: `#${idx+1} 같은 주소가 ${idxList.length}개 (${idxList.map(i=>`#${i+1}`).join(", ")})`,
          labelEl: label, idx
        });
        /* 이미 위에서 그린 배지에 중복 아이콘을 이어붙임(중복 제거) */
        const wrap = label.querySelector(":scope > .label-err-badges");
        if (wrap && !wrap.querySelector(".err-duplicate")) {
          wrap.insertAdjacentHTML("beforeend", `<span class="label-err-badge-icon err-duplicate" title="중복 주소">${BADGE_ICON_SVG["err-duplicate"]}</span>`);
        } else if (!wrap) {
          renderErrorBadges(label, [{ type: "err-duplicate", kind: "중복 주소" }]);
        }
      });
    });

    /* 패널 표시 */
    const panel     = document.getElementById("validation-panel");
    const list      = document.getElementById("validation-list");
    const countEl   = document.getElementById("validation-count");
    if (!panel || !list) return;

    if (!allErrors.length) {
      panel.style.display = "";
      list.innerHTML = `<div style="text-align:center;padding:12px 4px;color:var(--clr-success);font-size:12px;font-weight:600">✅ 오류 없음 — 모든 라벨이 정상입니다.</div>`;
      countEl.textContent = "0";
      countEl.style.background = "var(--clr-success)";
      setStatusbarValidationInfo(0);
      /* inspector 열기 */
      document.getElementById("inspector")?.classList.remove("collapsed");
      showToast("✅ 데이터 검증 완료 — 오류 없음", "success", 2000);
      return;
    }

    panel.style.display = "";
    countEl.textContent = allErrors.length;
    countEl.style.background = "var(--clr-danger)";
    setStatusbarValidationInfo(allErrors.length);
    list.innerHTML = allErrors.map((e, i) =>
      `<div class="val-item ${e.type}" data-idx="${e.idx}">
         <span class="val-item-icon">${e.icon}</span>
         <div class="val-item-body">
           <span class="val-item-type">${e.kind}</span>
           <span class="val-item-msg">${escHtml(e.msg)}</span>
         </div>
         <span class="val-item-idx">#${e.idx+1}</span>
       </div>`
    ).join("");

    /* 오류 항목 클릭 → 해당 라벨로 이동 */
    list.querySelectorAll(".val-item").forEach((item, i) => {
      item.addEventListener("click", () => {
        const targetIdx = parseInt(item.dataset.idx);
        const target = postLabelContainer.querySelectorAll(".label")[targetIdx];
        if (target) scrollToLabel(target, false);
      });
    });

    /* inspector 열기 */
    document.getElementById("inspector")?.classList.remove("collapsed");
    showToast(`🔍 데이터 검증 완료 — ${allErrors.length}개 오류 발견`, "warn", 3000);
  };

  /* 패널 닫기 */
  document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("validation-clear")?.addEventListener("click", () => {
      const panel = document.getElementById("validation-panel");
      if (panel) panel.style.display = "none";
      postLabelContainer.querySelectorAll(".label").forEach(l => {
        l.classList.remove("label-err-missing","label-err-postcode","label-err-dup","label-err-overflow","label-err-special");
        l.querySelector(":scope > .label-err-badges")?.remove();
      });
      setStatusbarValidationInfo(0);
    });
  });

  /* ── 자동 검증 트리거: 엑셀 불러오기 완료 후 실행 ──────────── */
  /* processFile()이 끝난 시점을 감지하는 가장 안전한 방법:
     postLabelContainer에 새 .label이 추가된 후 2초 뒤 자동 실행
     (기존 MutationObserver가 이미 있으므로 중복 방지 플래그 사용) */
  let _autoValidateTimer = null;
  const _valObs = new MutationObserver(() => {
    clearTimeout(_autoValidateTimer);
    _autoValidateTimer = setTimeout(() => {
      /* 라벨이 10개 이상일 때만 자동 검증 (단순 추가는 제외) */
      const cnt = postLabelContainer.querySelectorAll(".label").length;
      if (cnt >= 5) window.runValidation();
    }, 2500);
  });
  /* postLabelContainer에서 .label 추가 감지 (childList만 — 텍스트 편집 제외) */
  window.addEventListener("DOMContentLoaded", () => {
    _valObs.observe(postLabelContainer, { childList: true });
  });

})(); /* end IIFE */

/* ══════════════════════════════════════════════════════════════
   TASK 07 — 데이터 관리 시스템
   ① 5초 자동 저장  ② Redo 액션  ③ 복원 메시지 개선
   ══════════════════════════════════════════════════════════════ */
(function() {

  /* ── 1. 자동 저장 인터벌 (5초) ─────────────────────────── */
  const AUTO_SAVE_INTERVAL = 5000;

  setInterval(() => {
    /* 라벨이 없으면 자동저장 불필요 */
    if (!postLabelContainer.querySelectorAll(".label").length) return;
    saveToStorage();
  }, AUTO_SAVE_INTERVAL);

  /* 저장 상태 표시 — 3초 후 자동 페이드 */
  const _origSave = saveToStorage;
  /* saveToStorage 자체가 상태 표시를 포함하므로
     "saved" 상태일 때 3초 후 흐리게 처리 */
  const observer = new MutationObserver(() => {
    const el = document.getElementById("sb-autosave");
    if (!el || !el.classList.contains("saved")) return;
    clearTimeout(el._fadeTimer);
    el._fadeTimer = setTimeout(() => {
      el.style.opacity = "0.45";
    }, 3000);
    el.style.opacity = "";
  });
  window.addEventListener("DOMContentLoaded", () => {
    const el = document.getElementById("sb-autosave");
    if (el) observer.observe(el, { childList:true, characterData:true, subtree:true });
  });

  /* ── 2. Redo 액션 구현 ──────────────────────────────────── */
  window.redoAction = function() {
    if (!redoStack.length) return;
    const entry = redoStack.pop();

    if (entry.type === "delete") {
      /* Undo가 "삭제된 것을 복원"했으므로 Redo는 "다시 삭제" */
      const all = Array.from(postLabelContainer.querySelectorAll(".label"));
      const target = all[entry.index] || null;

      /* 복원된 라벨을 다시 찾아 제거 — innerHTML로 비교 */
      const tmpDiv = document.createElement("div");
      tmpDiv.innerHTML = entry.html;
      const refHtml = tmpDiv.firstElementChild?.outerHTML || "";

      /* index 기반으로 재삭제 */
      const toRemove = all[Math.min(entry.index, all.length - 1)];
      if (toRemove) {
        undoStack.push({
          type: "delete",
          html: toRemove.outerHTML,
          index: Array.from(postLabelContainer.querySelectorAll(".label")).indexOf(toRemove)
        });
        if (undoStack.length > MAX_UNDO) undoStack.shift();
        toRemove.remove();
        saveToStorage(); reflowPages(); updateLabelCount(); updateUndoBtn();
        showToast("↪ 삭제 다시 실행됨.", "info");
      }

    } else if (entry.type === "edit") {
      /* Undo가 "이전 값 복원"했으므로 Redo는 "수정 값 재적용" */
      /* entry.oldValue = undo 전 값(= redo 적용 대상의 이전 값)
         현재 라벨의 텍스트 = undo 이후 복원된 값
         → Redo는 "undo 직전 상태"를 알아야 함
         ※ 단순 구현: edit 타입은 현재 값을 읽어 newValue 역할로 swap */
      const { labelEl, field } = entry;
      if (!labelEl?.isConnected) { updateUndoBtn(); showToast("↪ 라벨을 찾을 수 없습니다.", "warn"); return; }

      const fieldEl = labelEl.querySelector("." + field);
      if (!fieldEl) { updateUndoBtn(); return; }

      /* entry에 redoValue가 있으면 적용, 없으면 현재 값 유지 (최초 1회는 noop) */
      if (entry.redoValue !== undefined) {
        /* undo stack에 현재 상태 push */
        undoStack.push({
          type: "edit", labelEl, field,
          oldValue: fieldEl.textContent,
          oldPostCode: field === "addr"
            ? Array.from(labelEl.querySelectorAll(".post-digit")).map(d=>d.textContent).join("")
            : undefined,
          redoValue: entry.oldValue
        });
        if (undoStack.length > MAX_UNDO) undoStack.shift();
        fieldEl.innerText = entry.redoValue;
        if (field === "addr" && entry.redoPostCode !== undefined) {
          const pc = labelEl.querySelector(".post-code-container");
          if (pc) { pc.innerHTML = digitsHTML(entry.redoPostCode); syncPostcodeLock(labelEl); }
        }
        saveToStorage(); updateUndoBtn(); scrollToLabel(labelEl); updateEmptyLabelClass(labelEl);
        showToast("↪ 편집 다시 실행됨.", "info");
      } else {
        updateUndoBtn();
        showToast("↪ 다시 실행할 편집 내용이 없습니다.", "info", 2000);
      }
    }
  };

  /* ── 3. undoDelete에서 redoStack push 시 redoValue 보완 ── */
  /* edit 타입 undo 시 "현재 값"을 redoValue로 기록해야
     Redo 적용이 가능 — undoDelete를 한 번 더 래핑 */
  const _origUndoDelete = window.undoDelete;
  window.undoDelete = function() {
    /* 현재 top 엔트리에 redoValue 미리 기록 */
    const entry = undoStack[undoStack.length - 1];
    if (entry && entry.type === "edit" && entry.redoValue === undefined) {
      const fieldEl = entry.labelEl?.isConnected
        ? entry.labelEl.querySelector("." + entry.field)
        : null;
      if (fieldEl) {
        entry.redoValue = fieldEl.textContent;
        if (entry.field === "addr") {
          entry.redoPostCode = Array.from(
            entry.labelEl.querySelectorAll(".post-digit")
          ).map(d => d.textContent).join("");
        }
      }
    }
    _origUndoDelete();
  };

  /* ── 4. 복원 완료 토스트에 "자동 저장됨" 메시지 표시 ─── */
  /* DOMContentLoaded 이후 restore-btn 존재 시 오버라이드 */
  window.addEventListener("DOMContentLoaded", () => {
    /* restore-btn은 DOMContentLoaded 내부에서 동적 생성되므로
       이 IIFE는 그보다 나중에 실행됨 → MutationObserver로 감지 */
    const uploadZoneObs = new MutationObserver(() => {
      const btn = document.getElementById("restore-btn");
      if (!btn || btn._Wrapped) return;
      btn._Wrapped = true;
      const origClick = btn.onclick;
      btn.addEventListener("click", () => {
        /* 복원 후 자동저장 상태 표시 */
        setTimeout(() => {
          _showSaveStatus("saved", "복구된 작업을 불러왔습니다.");
          const el = document.getElementById("sb-autosave");
          if (el) { el.style.display = ""; el.style.opacity = ""; }
        }, 400);
      });
      uploadZoneObs.disconnect();
    });
    const uz = document.getElementById("upload-zone");
    if (uz) uploadZoneObs.observe(uz, { childList: true, subtree: true });
  });

})(); /* end IIFE */


/* ══════════════════════════════════════════════════════════════
   라벨 캔버스 드래그 다중 선택 (Rubber-band selection)
   - 기존 기능/이벤트는 전혀 건드리지 않고 새 레이어로 추가
   - 캔버스(라벨이 없는 빈 공간)에서 마우스 드래그 시 사각 선택 영역 표시
   - 영역과 겹치는 라벨들에 기존 ".label-selected" 클래스를 적용
   - [버그수정] 드래그 종료 시 브라우저가 자동 발생시키는 합성 click 이벤트가
     기존 "캔버스 바깥 클릭 → 선택 해제" 핸들러를 그대로 통과해 방금 만든
     선택을 즉시 지워버리던 문제를 해결 (capture 단계에서 그 1회 click만
     전파 차단 — 기존 클릭 핸들러 코드는 단 한 줄도 수정하지 않음)
   ══════════════════════════════════════════════════════════════ */
(function () {
  if (!canvasScroll || !postLabelContainer) return;

  /* 선택 박스 엘리먼트 (1개만 생성) */
  let selBox = document.getElementById("drag-select-box");
  if (!selBox) {
    selBox = document.createElement("div");
    selBox.id = "drag-select-box";
    document.body.appendChild(selBox);
  }

  const DRAG_THRESHOLD = 4; /* px — 이 이상 움직여야 "드래그"로 인정 (단순 클릭과 구분) */

  let dragging          = false;
  let dragMoved         = false;
  let suppressNextClick = false;
  let startX = 0, startY = 0, lastX = 0, lastY = 0;
  let dragMode = "replace"; /* replace | add | toggle */
  let preDragSelected = new Set();

  function isInteractiveTarget(target) {
    if (!target) return false;
    if (target.closest(".label")) return true; /* 라벨 위에서는 기존 클릭/선택 로직이 처리 */
    if (target.isContentEditable) return true;
    if (["INPUT", "BUTTON", "SELECT", "TEXTAREA", "A"].includes(target.tagName)) return true;
    if (target.closest(".ctx-menu, .modal-bg, .toolbar, .sidebar, .inspector, .titlebar, .statusbar, .fav-panel, .fit-nav")) return true;
    return false;
  }

  function getRect(x1, y1, x2, y2) {
    return {
      x1: Math.min(x1, x2), y1: Math.min(y1, y2),
      x2: Math.max(x1, x2), y2: Math.max(y1, y2)
    };
  }

  function intersects(r1, r2) {
    return !(r2.x1 > r1.x2 || r2.x2 < r1.x1 || r2.y1 > r1.y2 || r2.y2 < r1.y1);
  }

  function updateSelectionFromBox() {
    const r = getRect(startX, startY, lastX, lastY);
    postLabelContainer.querySelectorAll(".label").forEach(l => {
      const rect = l.getBoundingClientRect();
      const hit  = intersects(r, { x1: rect.left, y1: rect.top, x2: rect.right, y2: rect.bottom });
      const wasSelected = preDragSelected.has(l);

      if (dragMode === "toggle")      l.classList.toggle("label-selected", hit ? !wasSelected : wasSelected);
      else if (dragMode === "add")    l.classList.toggle("label-selected", wasSelected || hit);
      else                            l.classList.toggle("label-selected", hit);
    });
    if (typeof updateMinimap === "function") updateMinimap();
    if (typeof updateLabelCount === "function") updateLabelCount();
  }

  canvasScroll.addEventListener("mousedown", e => {
    if (e.button !== 0) return;          /* 좌클릭만 */
    if (isInteractiveTarget(e.target)) return; /* 라벨/버튼/입력창 등은 기존 동작 그대로 */

    dragging  = true;
    dragMoved = false;
    startX = lastX = e.clientX;
    startY = lastY = e.clientY;

    if (e.ctrlKey || e.metaKey)      dragMode = "toggle";
    else if (e.shiftKey)             dragMode = "add";
    else {
      dragMode = "replace";
      postLabelContainer.querySelectorAll(".label.label-selected").forEach(l => l.classList.remove("label-selected"));
      if (typeof lastSelectedLabel !== "undefined") lastSelectedLabel = null;
    }
    preDragSelected = new Set(postLabelContainer.querySelectorAll(".label.label-selected"));

    selBox.style.display = "block";
    selBox.style.left = startX + "px";
    selBox.style.top  = startY + "px";
    selBox.style.width  = "0px";
    selBox.style.height = "0px";

    e.preventDefault(); /* 텍스트 드래그 선택 방지 */
  });

  document.addEventListener("mousemove", e => {
    if (!dragging) return;
    lastX = e.clientX;
    lastY = e.clientY;

    if (!dragMoved && (Math.abs(lastX - startX) > DRAG_THRESHOLD || Math.abs(lastY - startY) > DRAG_THRESHOLD)) {
      dragMoved = true; /* 실제로 "드래그"가 시작된 시점 */
    }

    const r = getRect(startX, startY, lastX, lastY);
    selBox.style.left   = r.x1 + "px";
    selBox.style.top    = r.y1 + "px";
    selBox.style.width  = (r.x2 - r.x1) + "px";
    selBox.style.height = (r.y2 - r.y1) + "px";

    if (dragMoved) updateSelectionFromBox();
  });

  function endDrag() {
    if (!dragging) return;
    dragging = false;
    selBox.style.display = "none";

    if (dragMoved) {
      /* 핵심 버그수정          mousedown → mousemove(드래그) → mouseup 이후, 브라우저는 보통의 클릭과
         동일하게 합성 "click" 이벤트를 한 번 더 발생시킨다. 이 click의 target은
         라벨이 아닌 상위 요소(캔버스/페이지)가 되어, 기존에 있던
         "라벨 바깥을 클릭하면 선택 해제" 핸들러(document, click, bubble 단계)의
         조건(.closest('.label') 없음)을 그대로 만족시켜 방금 만든 드래그 선택을
         즉시 지워버린다. 아래 capture 단계 리스너로 그 1회 click만 무력화한다. */
      suppressNextClick = true;
      setTimeout(() => { suppressNextClick = false; }, 0); /* click이 안 오는 예외 상황 대비 안전장치 */
    }
    dragMoved = false;
  }

  document.addEventListener("mouseup", endDrag);
  window.addEventListener("blur", endDrag); /* 브라우저 밖에서 마우스를 놓는 경우 대비 */

  /* 드래그 직후 1회의 합성 click만 capture 단계에서 가로채 전파를 막는다.
     - capture 단계는 bubble 단계보다 항상 먼저 실행되므로, 여기서 전파를
       막으면 target에 도달하기 전에 이벤트가 소멸되어 기존 bubble 단계의
       "바깥 클릭 → 선택 해제" 핸들러는 아예 호출되지 않는다.
     - 일반적인 단순 클릭(드래그 없음)에는 전혀 관여하지 않으므로,
       "다른 곳을 클릭하면 선택이 풀리는" 기존 동작은 그대로 유지된다. */
  document.addEventListener("click", e => {
    if (suppressNextClick) {
      suppressNextClick = false;
      e.stopPropagation();
    }
  }, true);

})(); /* end 라벨 드래그 다중 선택 */

/* ══════════════════════════════════════════════════════════════
   FIX-A  주소라벨 배치 미리보기 렌더링
   — #addr-grid-preview 안에 .agp-cell을 mm 비율 그대로 배치
   신규: 시작 위치(건너뛸 칸) 지정 — 미리보기를 클릭해 남은 칸부터
   인쇄를 시작하도록 지정할 수 있다("피킹 모드").
   ══════════════════════════════════════════════════════════════ */
let addrGridPickMode = false; /* true인 동안엔 칸 클릭 = 라벨로 스크롤이 아니라 "이 칸부터 시작"으로 지정 */

function setAddrGridPickMode(on) {
  addrGridPickMode = !!on;
  const frame = document.getElementById("addr-grid-preview");
  const btn   = document.getElementById("addr-start-pos-btn");
  if (frame) frame.classList.toggle("agp-pick-mode", addrGridPickMode);
  if (btn) {
    btn.classList.toggle("active", addrGridPickMode);
    btn.textContent = addrGridPickMode ? "취소" : "시작 위치 지정";
  }
  renderAddrGridPreview();
}

function renderAddrGridPreview() {
  const frame  = document.getElementById("addr-grid-preview");
  const infoEl = document.getElementById("addr-grid-info");
  if (!frame) return;

  const c = TEMPLATE_CONFIGS[templateSelect.value];
  if (!c) { frame.innerHTML = ""; return; }

  const labels  = Array.from(postLabelContainer.querySelectorAll(".label"));
  const colN    = c.colN  || 2;
  const rowN    = c.rowN  || 8;
  const perPage = c.perPage || (colN * rowN);
  const startOffset = Math.min(getStoredStartOffset(templateSelect.value), perPage - 1);

  /* mm → % 변환  (A4 = 210 × 297 mm)
     [버그 수정] 이전에는 이 미리보기가 실제 캔버스/인쇄와 별도로
     "남는 공간" 기준으로 가로 간격을 계산하고, 세로 방향으로는
     rowGap을 그대로 더해 행 사이 간격을 만들었다. 그런데 실제
     .a4-page 그리드는 (1) 가로 간격은 rowGap 값을 그대로 쓰고
     (2) 세로 간격은 항상 0(row-gap:0, 행 높이가 이미 페이지에
     정확히 맞도록 설계됨)이라서, 미리보기와 실제 배치가 서로
     어긋나 보였다. → 가로 간격은 applyConfig()와 완전히 동일한
     getSafeColumnGapMM()을 그대로 재사용하고, 세로 간격은 실제
     캔버스와 동일하게 추가하지 않는다(행 높이만으로 배치).
     [실시간 WYSIWYG 강화] 상단/좌측 여백도 applyConfig()가 실제로
     페이지에 적용하는 값(규격 기본값 + 저장된 미세조정 오프셋)을
     그대로 반영한다 — 이전에는 미세조정값을 무시한 채 규격 기본값만
     사용해서, 인쇄 위치를 보정한 뒤에도 미리보기가 보정 전 상태로
     보이는 불일치가 있었다. */
  const offset = getStoredOffset(templateSelect.value);
  const effPt  = parseFloat(c.pt) + offset.pt;
  const effPl  = parseFloat(c.pl) + offset.pl;
  const ptPct = effPt / 297 * 100;
  const plPct = effPl / 210 * 100;
  const wPct  = parseFloat(c.w)  / 210 * 100;
  const hPct  = parseFloat(c.h)  / 297 * 100;
  const cgPct = getSafeColumnGapMM(Object.assign({}, c, { pl: effPl + "mm" })) / 210 * 100; /* 실제 캔버스/인쇄와 동일한 값 */

  frame.innerHTML = "";

  for (let row = 0; row < rowN; row++) {
    for (let col = 0; col < colN; col++) {
      const idx      = row * colN + col;
      const isSkip   = idx < startOffset; /* 시작 위치 지정으로 건너뛴(이미 사용된) 칸 */
      const label    = isSkip ? null : (labels[idx - startOffset] || null);

      const left = plPct + col * (wPct + cgPct);
      const top  = ptPct + row * hPct; /* 실제 캔버스와 동일: 행 사이 세로 간격 없음 */

      const cell = document.createElement("div");
      cell.className = "agp-cell";
      cell.style.cssText =
        `left:${left.toFixed(2)}%;top:${top.toFixed(2)}%;` +
        `width:${wPct.toFixed(2)}%;height:${hPct.toFixed(2)}%`;

      if (isSkip) {
        cell.classList.add("agp-skip");
        cell.title = `#${idx+1} 건너뜀(이미 사용된 칸)`;
      } else if (label) {
        const hasCont = !!(label.querySelector(".addr")?.textContent?.trim()
                        || label.querySelector(".name")?.textContent?.trim());
        const locked  = label.classList.contains("label-locked");
        const sel     = label.classList.contains("label-selected");
        cell.classList.add(
          sel ? "agp-selected" : locked ? "agp-locked" : hasCont ? "agp-filled" : "agp-blank"
        );
        const tipName = label.querySelector(".name")?.textContent?.trim() || "";
        const tipAddr = label.querySelector(".addr")?.textContent?.trim()?.slice(0, 20) || "";
        cell.title  = tipName || tipAddr ? `${tipName}${tipAddr ? " · " + tipAddr : ""}` : `#${idx+1}`;
      } else {
        cell.classList.add("agp-empty");
        cell.title = `#${idx+1}`;
      }

      if (addrGridPickMode) {
        /* 피킹 모드: 어떤 칸(건너뜀/입력됨/비어있음 무관)을 클릭해도
           그 칸을 새 시작 위치로 지정한다. */
        cell.classList.add("agp-pickable");
        cell.title = `클릭: ${idx+1}번째 칸부터 시작`;
        cell.addEventListener("click", () => {
          setStoredStartOffset(templateSelect.value, idx);
          setAddrGridPickMode(false);
          reflowPages();
          showToast(`${idx+1}번째 칸부터 인쇄하도록 지정했습니다.`, "success");
        });
      } else if (label) {
        cell.style.cursor = "pointer";
        cell.addEventListener("click", () => {
          label.scrollIntoView({ behavior: "smooth", block: "center" });
          label.classList.add("label-highlight");
          setTimeout(() => label.classList.remove("label-highlight"), 1500);
        });
      }
      frame.appendChild(cell);
    }
  }

  /* 범례 옆 요약 정보 */
  if (infoEl) {
    const filled = labels.slice(0, Math.max(0, perPage - startOffset)).filter(l =>
      !!(l.querySelector(".addr")?.textContent?.trim()
      || l.querySelector(".name")?.textContent?.trim())
    ).length;
    infoEl.textContent = startOffset > 0
      ? `${filled}/${perPage}칸 · ${startOffset+1}번째 칸부터 시작`
      : `${filled}/${perPage}칸`;
  }

  /* 시작 위치 버튼 옆 상태 표시 동기화 */
  const posDisplay = document.getElementById("addr-start-pos-display");
  if (posDisplay) {
    posDisplay.textContent = startOffset > 0 ? `${startOffset+1}번째 칸부터` : "첫 칸부터(기본)";
  }
  const resetBtn = document.getElementById("addr-start-pos-reset");
  if (resetBtn) resetBtn.style.display = startOffset > 0 ? "" : "none";
}

/* 시작 위치 지정 버튼 · 초기화 버튼 배선 */
(function() {
  const pickBtn  = document.getElementById("addr-start-pos-btn");
  const resetBtn = document.getElementById("addr-start-pos-reset");
  if (pickBtn) {
    pickBtn.addEventListener("click", () => setAddrGridPickMode(!addrGridPickMode));
  }
  if (resetBtn) {
    resetBtn.addEventListener("click", () => {
      setStoredStartOffset(templateSelect.value, 0);
      setAddrGridPickMode(false);
      reflowPages();
      showToast("시작 위치를 첫 칸으로 초기화했습니다.", "info");
    });
  }
})();

/* updateMinimap 호출 시 미리보기도 함께 갱신 (monkey-patch) */
(function() {
  const _orig = window.updateMinimap;
  window.updateMinimap = function() {
    if (_orig) _orig.apply(this, arguments);
    renderAddrGridPreview();
  };
  /* 페이지 최초 로드 후 즉시 렌더 */
  setTimeout(renderAddrGridPreview, 0);
})();

/* ══════════════════════════════════════════════════════════════
   FIX-B  모드 탭 스위칭 — 주소라벨 ↔ 일반라벨
   ══════════════════════════════════════════════════════════════ */
(function() {
  const tabAddr = document.getElementById("mode-tab-address");
  const tabGen  = document.getElementById("mode-tab-general");
  const genBody = document.getElementById("general-app-body");
  if (!tabAddr || !tabGen) return;

  function switchToAddress() {
    document.body.classList.remove("mode-general");
    tabAddr.classList.add("active");
    tabGen.classList.remove("active");
    if (genBody) genBody.classList.remove("active");
    setTimeout(renderAddrGridPreview, 0);
    setTimeout(() => window._addrStepOnShow?.(), 0);
  }

  function switchToGeneral() {
    document.body.classList.add("mode-general");
    tabGen.classList.add("active");
    tabAddr.classList.remove("active");
    /* [버그 수정] 예전엔 여기서 무조건 genBody(스텝2 "라벨 디자인" 패널)를
       active로 켰다. 일반라벨은 자체 3단계 마법사(규격 선택→라벨 디자인→
       확인 및 인쇄)를 갖고 있고, 그 마법사가 각 스텝의 active 클래스를
       따로 관리한다(setGlWizardStep). 이 줄이 마법사 상태와 무관하게
       항상 genBody를 켜버려서, "규격 선택" 스텝에 있어야 할 때도 라벨
       디자인 캔버스가 함께 겹쳐 보이는 문제가 있었다. → 마법사
       컨트롤러(_glGoToStep)에게 "지금 있어야 할 스텝"을 다시 적용하도록
       위임해, 항상 스텝 하나의 화면만 보이게 한다. */
    if (typeof window._glGoToStep === "function") {
      window._glGoToStep(typeof window._glCurrentWizardStep === "function" ? window._glCurrentWizardStep() : "spec");
    } else if (genBody) {
      genBody.classList.add("active"); /* 안전망: 마법사 스크립트가 아직 로드 안 된 경우 */
    }
    /* general-labels.js가 정의한 초기화 함수가 있으면 호출 */
    if (typeof window._glInit === "function") window._glInit();
  }

  tabAddr.addEventListener("click", switchToAddress);
  tabGen.addEventListener ("click", switchToGeneral);
})();

/* ══════════════════════════════════════════════════════════════
   3단계 마법사(데이터 → 라벨 편집 → 출력) 컨트롤러 — 신규
   ──────────────────────────────────────────────────────────────
   기존 사이드바·캔버스·인스펙터(app-body = 2단계)는 전혀 손대지 않고,
   1단계(데이터)·3단계(출력)만 새로 만들어 세 화면을 전환한다.
   모든 실제 기능(엑셀 불러오기, 라벨 추가, 인쇄, 오프셋 조정, 시작
   위치 지정 등)은 기존 버튼(#load-btn, #add-btn, #print-btn, ...)을
   그대로 클릭시키거나 기존 함수를 그대로 호출하는 방식으로 재사용한다
   — 로직 중복 없이 "화면만" 새로 배치.
   ══════════════════════════════════════════════════════════════ */
(function() {
  const header = document.getElementById("addr-step-header");
  const views = {
    data:  document.getElementById("step-view-data"),
    edit:  document.getElementById("step-view-edit"),
    print: document.getElementById("step-view-print"),
  };
  const tabs = {
    data:  document.getElementById("step-tab-data"),
    edit:  document.getElementById("step-tab-edit"),
    print: document.getElementById("step-tab-print"),
  };
  const order = ["data", "edit", "print"];
  if (!header || !views.data || !views.edit || !views.print) return;
  const stepArrows = header.querySelectorAll(".step-header-steps .step-arrow"); /* [신규] 화살표[0]=데이터↔라벨편집 사이, [1]=라벨편집↔출력 사이 */

  let currentAddrStep = "data";

  function setAddrStep(step) {
    if (!views[step] || document.body.classList.contains("mode-general")) return;
    currentAddrStep = step;
    order.forEach(s => {
      views[s].classList.toggle("active", s === step);
      if (!tabs[s]) return;
      tabs[s].classList.toggle("active", s === step);
      tabs[s].classList.toggle("done", order.indexOf(s) < order.indexOf(step));
    });
    /* [신규] 지나온 구간의 화살표만 옅은 브랜드 컬러로 — 3단계를 가로지르는
       진행의 흐름이 배지뿐 아니라 사이 화살표에도 이어져 보이게 한다 */
    stepArrows.forEach((arrow, i) => {
      arrow.classList.toggle("step-arrow-done", order.indexOf(order[i]) < order.indexOf(step));
    });
    /* 컨텍스트별 툴바 버튼 표시/숨김 — data-step="edit print" 처럼 공백으로 구분 */
    document.querySelectorAll(".toolbar [data-step]").forEach(el => {
      const steps = (el.getAttribute("data-step") || "").split(/\s+/);
      el.classList.toggle("step-hide", !steps.includes(step));
    });

    if (step === "data") {
      refreshDataStepView();
    } else if (step === "edit") {
      /* 캔버스가 display:none으로 숨겨져 있던 동안엔 폭 측정이 불가능했으므로,
         다시 보이게 된 다음 프레임에서 맞춤 배율/미리보기를 재계산한다 */
      requestAnimationFrame(() => {
        if (viewMode === "fit" && typeof applyFitPage === "function") applyFitPage(fitCurrentPage);
        if (typeof renderAddrGridPreview === "function") renderAddrGridPreview();
      });
    } else if (step === "print") {
      refreshPrintStepView();
    }
  }
  window._addrStepOnShow = () => setAddrStep(currentAddrStep);

  order.forEach(s => tabs[s]?.addEventListener("click", () => setAddrStep(s)));
  document.getElementById("step-data-next")?.addEventListener("click", () => setAddrStep("edit"));
  document.getElementById("step-edit-next")?.addEventListener("click", () => setAddrStep("print"));
  document.getElementById("step-print-back")?.addEventListener("click", () => setAddrStep("edit"));

  /* 1단계 소스 카드 — 기존 버튼을 그대로 클릭시켜 위임 (로직 재사용) */
  document.getElementById("wstep-load-excel")?.addEventListener("click", () => loadBtn.click());
  document.getElementById("wstep-add-manual")?.addEventListener("click", () => {
    /* [변경] 예전엔 빈 라벨을 만들고 바로 2단계(편집)로 넘어갔지만,
       이제는 1단계에 머문 채 "주소 직접 입력" 모달을 열어 값을 입력받고,
       입력한 주소를 아래 "주소 목록" 표에 계속 쌓을 수 있도록 한다.
       2단계 이동은 오직 "다음" 버튼을 눌렀을 때만 일어난다. */
    window._openManualEntryModal?.();
  });

  /* [신규] "행 추가" 버튼 — 모달을 띄우지 않고 표에 빈 행을 바로 하나
     만들어, 이름 칸에 포커스를 준다. 엑셀 없이도 표에서 바로바로
     타이핑해 채워나갈 수 있도록(요청: "엑셀불러온거 밑에 보여주는 표에서
     행삽입을 하고 직접 필요한 사항만 입력할 수 있게"). createLabel()을
     그대로 재사용하므로 캔버스·인쇄 등 다른 모든 로직과 완전히 같은
     방식으로 취급되는 "진짜" 라벨이 만들어진다(표 전용 가짜 행이 아님). */
  document.getElementById("wstep-add-row-btn")?.addEventListener("click", () => {
    const blank = createLabel({ "주소": "", "이름": "", postCode: "" });
    postLabelContainer.appendChild(blank);
    updateLabelCount(); /* → refreshDataStepView()가 표를 다시 그림(방금 추가한 행 포함) */
    requestAnimationFrame(() => {
      const lastRow = document.querySelector('#wstep-table-body tr:last-of-type[data-widx]');
      const nameCell = lastRow?.querySelector('[data-field="name"]');
      if (nameCell) {
        nameCell.focus();
        const range = document.createRange();
        range.selectNodeContents(nameCell);
        const sel = window.getSelection();
        sel.removeAllRanges(); sel.addRange(range);
      }
    });
  });

  /* [신규] 표 인라인 편집 — 이름/우편번호/주소/연락처 칸을 contenteditable로
     열어뒀으니(refreshDataStepView 참고), 사용자가 직접 타이핑하면 그
     값을 실제 .label 요소(캔버스·인쇄가 보는 "진짜" 데이터)에 되돌려
     쓴다. 타이핑 중(input)에는 가벼운 동기화만 하고, 칸을 벗어날 때
     (focusout)만 표 전체를 다시 그린다 — 매 키 입력마다 표를 다시
     그리면 innerHTML이 통째로 바뀌어 커서 위치가 튀기 때문이다. */
  function wstepFindLabelForCell(td) {
    const tr = td.closest("tr[data-widx]");
    if (!tr) return null;
    const idx = parseInt(tr.dataset.widx, 10);
    const labels = postLabelContainer.querySelectorAll(".label");
    return labels[idx] || null;
  }
  function wstepSyncCellToLabel(td, { rerender } = {}) {
    const label = wstepFindLabelForCell(td);
    if (!label) return;
    const field = td.dataset.field;
    const text = td.innerText.replace(/\n/g, " ").trim();
    if (field === "name") {
      const nameEl = label.querySelector(".name");
      if (nameEl) nameEl.textContent = text;
    } else if (field === "addr") {
      const addrEl = label.querySelector(".addr");
      if (addrEl) addrEl.textContent = text;
    } else if (field === "zip") {
      const digits = text.replace(/\D/g, "").padEnd(5, " ").slice(0, 5).split("");
      const boxes = label.querySelectorAll(".post-digit");
      boxes.forEach((b, i) => { b.textContent = digits[i] || " "; });
    } else if (field === "phone") {
      if (text) label.dataset.phone = text; else delete label.dataset.phone;
    }
    label.classList.toggle("label-empty", !label.querySelector(".addr")?.textContent.trim() && !label.querySelector(".name")?.textContent.trim());
    if (rerender) updateLabelCount(); /* 페이지·미리보기·표까지 전부 갱신 */
  }
  const wstepTableBody = document.getElementById("wstep-table-body");
  /* [신규] 표에서 주소를 입력/수정하면 우편번호를 자동으로 조회해 채운다.
     엑셀로 불러올 때 이미 쓰던 fetchPostCode()(카카오 주소 검색 API,
     LRU 캐시·429 재시도 내장)를 그대로 재사용한다. label과 주소 텍스트를
     호출자(focusout 핸들러)가 표 재렌더링 "전에" 미리 확보해 넘겨준다 —
     조회는 비동기라 그 사이 표가 다시 그려져도 label은 실제 캔버스에
     남아있는 진짜 라벨 요소라 안전하게 계속 참조할 수 있다. */
  async function wstepAutoFillPostcode(label, addr) {
    if (!label || !addr) return;
    showToast("🔍 우편번호 조회 중…", "info", 1200);
    const code = await fetchPostCode(addr);
    const digits = (code || "     ").padEnd(5, " ").slice(0, 5).split("");
    label.querySelectorAll(".post-digit").forEach((b, i) => { b.textContent = digits[i] || " "; });
    updateLabelCount(); /* 표 재렌더링(refreshDataStepView 포함) */
    if (code && code.trim()) showToast(`✅ 우편번호 자동 입력: ${code.trim()}`, "success", 1800);
    else showToast("⚠️ 우편번호를 찾지 못했습니다 — 우편번호 칸에 직접 입력해주세요.", "warn", 2600);
  }
  if (wstepTableBody) {
    wstepTableBody.addEventListener("input", e => {
      const td = e.target.closest("[data-field]");
      if (td) wstepSyncCellToLabel(td, { rerender: false });
    });
    wstepTableBody.addEventListener("focusout", e => {
      const td = e.target.closest("[data-field]");
      if (!td) return;
      /* [버그 주의] wstepSyncCellToLabel(rerender:true)가 updateLabelCount()→
         refreshDataStepView()를 태워 #wstep-table-body.innerHTML을 통째로
         다시 그리면 지금의 td는 문서에서 떨어져 나간(detached) 노드가 된다.
         detached 노드는 .closest()로 조상은 여전히 찾을 수 있지만(트리 구조
         자체는 유지됨), .innerText는 레이아웃이 없어 빈 문자열이 될 수 있다.
         그래서 재렌더링으로 값이 사라지기 전에 주소 텍스트와 label 참조를
         먼저 확보해둔다. */
      const isAddr = td.dataset.field === "addr";
      const addrText = isAddr ? td.innerText.replace(/\n/g, " ").trim() : "";
      const addrLabel = isAddr ? wstepFindLabelForCell(td) : null;
      wstepSyncCellToLabel(td, { rerender: true });
      /* [신규] 우편번호 자동조회 연동 — 엑셀 불러오기 때 이미 쓰던
         fetchPostCode()(카카오 주소 검색 API, 캐시·재시도 내장)를 그대로
         재사용해, 표에서 주소를 직접 입력/수정해도 우편번호가 자동으로
         채워지도록 한다. */
      if (isAddr && addrText) wstepAutoFillPostcode(addrLabel, addrText);
    });
    /* Enter로 다음 칸(또는 다음 행 첫 칸)으로 이동 — 표에서 엑셀처럼 빠르게 입력.
       [버그 수정] td.blur()가 focusout → wstepSyncCellToLabel(rerender:true) →
       updateLabelCount()를 타면서 #wstep-table-body.innerHTML을 통째로 다시
       그린다. 그러면 blur() 이전에 잡아둔 td/셀 DOM 참조는 더 이상 문서에
       존재하지 않는 "죽은" 노드가 되어, Array.indexOf(td)가 항상 -1을 반환해
       "다음 칸"이 매번 첫 번째 칸으로 잘못 계산되고 있었다(그 결과 입력한
       내용이 전부 첫 칸에 쌓여버림). node 자체를 기억하는 대신, "몇 번째
       행·어떤 필드"라는 좌표만 기억해뒀다가 다시 그려진 뒤 그 좌표로
       새 DOM을 다시 찾는 방식으로 바꿔 해결한다. */
    const FIELD_ORDER = ["name", "zip", "addr", "phone"];

    /* [신규] 현재 캐럿이 셀 텍스트의 맨 앞부터 몇 번째 글자 위치에
       있는지 계산 — 방향키 좌/우가 "텍스트 커서 이동"인지 "옆 칸으로
       이동"인지 구분하는 데 쓴다(스프레드시트/노션과 동일한 동작). */
    function wstepCaretOffset(el) {
      const sel = window.getSelection();
      if (!sel || !sel.rangeCount) return 0;
      const r = sel.getRangeAt(0);
      const pre = document.createRange();
      pre.selectNodeContents(el);
      pre.setEnd(r.endContainer, r.endOffset);
      return pre.toString().length;
    }
    /* [신규] 좌표(행 widx·필드명)로 셀에 포커스 + 커서 위치 지정.
       Enter 이동 로직과 동일하게 "DOM 참조"가 아닌 "좌표"로 다시 찾는다
       — blur() 한 번에 표 전체가 다시 그려져 기존 DOM 참조가 죽은
       노드가 되기 때문(위 Enter 로직 주석 참고). */
    function wstepFocusCell(widx, field, toEnd) {
      const target = wstepTableBody.querySelector(`tr[data-widx="${widx}"] [data-field="${field}"]`);
      if (!target) return false;
      target.focus();
      const range = document.createRange();
      range.selectNodeContents(target);
      range.collapse(!toEnd);
      const sel = window.getSelection();
      sel.removeAllRanges(); sel.addRange(range);
      return true;
    }

    wstepTableBody.addEventListener("keydown", e => {
      const td = e.target.closest("[data-field]");
      if (!td) return;
      const tr = td.closest("tr[data-widx]");
      if (!tr) return;
      const widx  = parseInt(tr.dataset.widx, 10);
      const fIdx  = FIELD_ORDER.indexOf(td.dataset.field);

      if (e.key === "Enter") {
        e.preventDefault();
        let nextWidx = widx, nextFieldIdx = fIdx + 1;
        if (nextFieldIdx >= FIELD_ORDER.length) { nextFieldIdx = 0; nextWidx += 1; }
        const nextField = FIELD_ORDER[nextFieldIdx];
        td.blur(); /* 동기적으로 focusout → 동기화 + 표 재렌더링까지 끝남 */
        requestAnimationFrame(() => wstepFocusCell(nextWidx, nextField, true));
        return;
      }

      /* [신규] 방향키 셀 이동 — 노션/엑셀처럼 마우스 없이 상하좌우로
         자유롭게 다니며 고칠 수 있게 한다. 위/아래는 항상 같은 칸(필드)의
         이전/다음 행으로 이동하고, 좌/우는 텍스트 커서가 글자의 맨
         앞(왼쪽 키) 또는 맨 뒤(오른쪽 키)에 있을 때만 옆 칸으로 넘어가
         — 그 외엔 셀 안에서 평범한 텍스트 커서 이동을 방해하지 않는다. */
      if (e.key === "ArrowUp" || e.key === "ArrowDown") {
        e.preventDefault();
        const nextWidx = widx + (e.key === "ArrowDown" ? 1 : -1);
        if (nextWidx < 0) return;
        td.blur();
        requestAnimationFrame(() => wstepFocusCell(nextWidx, td.dataset.field, true));
        return;
      }
      if (e.key === "ArrowLeft" || e.key === "ArrowRight") {
        const sel = window.getSelection();
        if (!sel || !sel.rangeCount || !sel.getRangeAt(0).collapsed) return; /* 드래그로 선택 중이면 기본 동작 유지 */
        const offset = wstepCaretOffset(td);
        const len = td.textContent.length;
        if (e.key === "ArrowLeft"  && offset > 0)   return; /* 텍스트 중간 — 기본 커서 이동 허용 */
        if (e.key === "ArrowRight" && offset < len) return;
        e.preventDefault();
        let nextWidx = widx, nextFieldIdx = fIdx + (e.key === "ArrowRight" ? 1 : -1);
        if (nextFieldIdx >= FIELD_ORDER.length) { nextFieldIdx = 0; nextWidx += 1; }
        else if (nextFieldIdx < 0) { nextFieldIdx = FIELD_ORDER.length - 1; nextWidx -= 1; }
        if (nextWidx < 0) return;
        const nextField = FIELD_ORDER[nextFieldIdx];
        const toEnd = e.key === "ArrowLeft"; /* 왼쪽으로 넘어간 칸은 그 칸의 끝에서 이어지도록 */
        td.blur();
        requestAnimationFrame(() => wstepFocusCell(nextWidx, nextField, toEnd));
      }
    });
  }

  /* [신규] 표 행 삭제 — 호버 시 보이는 휴지통 아이콘 버튼과 우클릭
     메뉴 둘 다 이 함수 하나를 공유한다. 캔버스의 기존 "삭제"(ctx-delete)와
     동일하게 undoStack에 쌓아 Ctrl+Z로 복구할 수 있게 하고, 실제 데이터는
     여전히 postLabelContainer 안의 진짜 .label 요소이므로 지우고 나면
     updateLabelCount() → refreshDataStepView()가 표를 다시 그려 번호(data-widx)도
     자동으로 다시 매겨진다. */
  function deleteWstepRow(widx) {
    const label = postLabelContainer.querySelectorAll(".label")[widx];
    if (!label) return;
    undoStack.push({
      type: "delete",
      html: label.outerHTML,
      index: Array.from(postLabelContainer.querySelectorAll(".label")).indexOf(label)
    });
    if (undoStack.length > MAX_UNDO) undoStack.splice(0, undoStack.length - MAX_UNDO);
    redoStack.length = 0; /* 새 액션 → Redo 초기화 */
    label.remove();
    saveToStorage(); reflowPages(); updateLabelCount(); updateUndoBtn();
    showToast(
      `🗑️ 주소 1건 삭제됨. <button onclick="undoDelete()" class="toast-undo-btn">↩ 취소</button>`,
      "warn", 5000
    );
  }

  /* 휴지통 아이콘 클릭 — 이벤트 위임(행마다 리스너를 새로 달 필요 없음,
     refreshDataStepView가 매번 표를 통째로 다시 그려도 항상 동작함) */
  wstepTableBody?.addEventListener("click", e => {
    const delBtn = e.target.closest(".wstep-row-delete-btn");
    if (!delBtn) return;
    const tr = delBtn.closest("tr[data-widx]");
    if (!tr) return;
    deleteWstepRow(parseInt(tr.dataset.widx, 10));
  });

  /* [신규] 표 행 우클릭 → "행 삭제" 메뉴 (요청: "행삭제도 가능하게 해줘
     마우스 우클릭이랑"). 기존 라벨 우클릭 메뉴(label-context-menu)와
     완전히 같은 열기/닫기/화면 넘침 보정 패턴을 그대로 따르되, 항목이
     삭제 하나뿐이라 별도의 작은 메뉴(#wstep-row-context-menu)를 쓴다. */
  (function () {
    const menu   = document.getElementById("wstep-row-context-menu");
    const itmDel = document.getElementById("wstep-row-ctx-delete");
    if (!menu || !wstepTableBody) return;
    attachCtxMenuKeyboardNav(menu);

    let targetWidx = null;

    function openMenu(x, y, widx) {
      targetWidx = widx;
      menu.style.display = "block";
      menu.style.left = "0px";
      menu.style.top  = "0px";
      menu.classList.remove("ctx-flip-x", "ctx-flip-y", "ctx-open");

      const mw = menu.offsetWidth, mh = menu.offsetHeight;
      const vw = window.innerWidth, vh = window.innerHeight;
      let lx = x, ly = y;
      const flipX = lx + mw > vw - 8;
      const flipY = ly + mh > vh - 8;
      if (flipX) lx = Math.max(4, x - mw);
      if (flipY) ly = Math.max(4, y - mh);
      if (flipX) menu.classList.add("ctx-flip-x");
      if (flipY) menu.classList.add("ctx-flip-y");
      menu.style.left = lx + "px";
      menu.style.top  = ly + "px";
      requestAnimationFrame(() => menu.classList.add("ctx-open"));
    }
    function closeMenu() {
      menu.classList.remove("ctx-open");
      menu.addEventListener("transitionend", () => {
        if (!menu.classList.contains("ctx-open")) menu.style.display = "none";
      }, { once: true });
      targetWidx = null;
    }

    wstepTableBody.addEventListener("contextmenu", e => {
      const tr = e.target.closest("tr[data-widx]");
      if (!tr) return;
      e.preventDefault();
      openMenu(e.clientX, e.clientY, parseInt(tr.dataset.widx, 10));
    });
    itmDel?.addEventListener("click", () => {
      closeMenu();
      if (targetWidx !== null) deleteWstepRow(targetWidx);
    });
    document.addEventListener("click", e => {
      if (menu.classList.contains("ctx-open") && !menu.contains(e.target)) closeMenu();
    });
    document.addEventListener("keydown", e => {
      if (e.key === "Escape" && menu.classList.contains("ctx-open")) closeMenu();
    }, true);
    document.addEventListener("scroll", () => { if (menu.classList.contains("ctx-open")) closeMenu(); }, true);
  })();

  /* 신규: 1단계 "자주 쓰는 라벨지" 카드 — 템플릿 드롭다운(#template-select)의
     값을 바꾸고 change 이벤트를 그대로 발생시켜, 기존 change 핸들러(최근
     사용 규격 저장, 인스펙터 갱신, 페이지 재구성 등)를 전부 그대로 탄다.
     별도 로직을 새로 만들지 않고 기존 경로를 재사용하는 지름길일 뿐이다. */
  document.querySelectorAll(".wstep-template-card[data-template-key]").forEach(card => {
    card.addEventListener("click", () => {
      const key = card.dataset.templateKey;
      if (!TEMPLATE_CONFIGS[key]) { showToast("⚠️ 이 규격을 찾을 수 없습니다.", "warn", 1800); return; }
      templateSelect.value = key;
      templateSelect.dispatchEvent(new Event("change"));
      refreshDataStepView();
      showToast(`📐 ${TEMPLATE_CONFIGS[key].maker || ""} ${TEMPLATE_CONFIGS[key].model || ""} 규격으로 변경됨`.trim(), "info", 1600);
    });
  });

  /* [신규] "자주 쓰는 라벨지" 카드의 미니 스와치 — 실제 열(colN)×행(rowN)
     그대로 그리드를 그리고, 스와치 전체 비율을 (colN×w)/(rowN×h)로 맞춰
     칸 하나하나의 가로세로 비율이 실제 라벨과 정확히 일치하게 한다.
     행 수가 많은 규격(예: 18칸=2×9)이 와도 안전하도록 시각적 행 수는
     최대 10개로 캡핑(그 이상은 촘촘해서 어차피 잘 안 보임). */
  function populateTemplateCardShapes() {
    document.querySelectorAll(".wstep-tpl-shape[id^='wstep-tpl-shape-']").forEach(shapeEl => {
      const key = shapeEl.id.replace("wstep-tpl-shape-", "");
      const c = TEMPLATE_CONFIGS[key];
      if (!c) return;
      const colN = c.colN || 1, rowN = c.rowN || 1;
      const wNum = parseFloat(c.w) || 1, hNum = parseFloat(c.h) || 1;
      const visRows = Math.min(rowN, 10);
      shapeEl.style.aspectRatio = `${colN * wNum} / ${visRows * hNum}`;
      shapeEl.style.gridTemplateColumns = `repeat(${colN}, 1fr)`;
      shapeEl.style.gridTemplateRows    = `repeat(${visRows}, 1fr)`;
      shapeEl.innerHTML = Array.from({ length: colN * visRows })
        .map(() => `<span class="wstep-tpl-cell"></span>`).join("");
    });
  }
  populateTemplateCardShapes();

  /* [신규] "라벨 1개 미리보기"를 실물 mm 크기 그대로 그린 뒤, 카드 폭에
     맞는 배율로 축소한다(글자·숫자칸·여백이 모두 같은 비율로 작아짐).
     카드 폭이 바뀔 수 있는 경우(사이드바 접기, 창 크기 조절 등)에도
     항상 맞도록 ResizeObserver로 다시 계산한다.
     [버그 수정] 이전에는 availWidth를 wrap.clientWidth로 재고 있었는데,
     wrap 자신의 좌우 padding(14px×2=28px)까지 "쓸 수 있는 폭"으로
     잘못 포함시켜 버렸다 — 실제로 스케일된 박스가 들어갈 공간은
     scaler(=wrap의 padding 안쪽, 패딩 없음)의 폭인데 거기에 28px를
     더 크게 계산한 배율을 적용하니, 스케일된 라벨이 항상 그만큼
     오른쪽으로 wrap의 padding 영역까지 넘어가 overflow:hidden에
     잘려 "오른쪽이 잘려나간 것처럼" 보였다. scaler.clientWidth(패딩
     없는 순수 폭)를 기준으로 계산하도록 수정. 아울러 스케일된 박스의
     실제 폭만큼 scaler 자체의 width도 맞춰주고 가운데 정렬해서,
     라벨보다 큰 빈 여백이 어중간하게 오른쪽에 남지 않게 했다. */
  const MM_TO_PX_PREVIEW = 96 / 25.4;
  function updateSingleLabelPreviewScale() {
    const wrap   = document.getElementById("wstep-single-preview-wrap");
    const scaler = document.getElementById("wstep-single-preview-scaler");
    const box    = document.getElementById("wstep-single-preview");
    const c = TEMPLATE_CONFIGS[templateSelect.value];
    if (!wrap || !scaler || !box || !c?.w || !c?.h) return;
    const wMm = parseFloat(c.w), hMm = parseFloat(c.h);
    if (!(wMm > 0) || !(hMm > 0)) return;
    const actualWpx = wMm * MM_TO_PX_PREVIEW, actualHpx = hMm * MM_TO_PX_PREVIEW;
    /* wrap.clientWidth에서 wrap 자신의 좌우 padding을 직접 빼서 "패딩 안쪽
       순수 사용 가능 폭"을 구한다 — scaler.clientWidth를 쓰면, 아래에서
       scaler 폭 자체를 계속 다시 쓰기 때문에 매번 호출할 때마다 그
       직전에 줄여놓은 값을 기준으로 또 줄어드는 되먹임(feedback loop)
       버그가 생긴다. wrap은 우리가 건드리지 않는 안정적인 기준값이라
       이 문제가 없다. */
    const wrapStyle  = getComputedStyle(wrap);
    const availWidth = wrap.clientWidth
      - (parseFloat(wrapStyle.paddingLeft)  || 0)
      - (parseFloat(wrapStyle.paddingRight) || 0);
    if (!(availWidth > 0)) return;
    const scale = Math.min(1, availWidth / actualWpx);
    box.style.setProperty("--wstep-preview-scale", scale.toFixed(4));
    scaler.style.height = Math.round(actualHpx * scale) + "px";
    scaler.style.width  = Math.round(actualWpx * scale) + "px";
    scaler.style.margin = "0 auto"; /* 라벨 폭이 카드보다 좁아지는 경우 가운데 정렬 */
  }
  window.updateSingleLabelPreviewScale = updateSingleLabelPreviewScale;
  if (typeof ResizeObserver !== "undefined") {
    const previewWrapEl = document.getElementById("wstep-single-preview-wrap");
    if (previewWrapEl) new ResizeObserver(() => updateSingleLabelPreviewScale()).observe(previewWrapEl);
  }

  /* 3단계 인쇄 버튼 — 기존 인쇄/테스트 인쇄 로직 그대로 재사용 */
  document.getElementById("pstep-print")?.addEventListener("click", () => printBtn.click());
  document.getElementById("pstep-test-print")?.addEventListener("click", () => {
    if (typeof printTestGrid === "function") printTestGrid();
  });

  /* [제거] 워터마크 체크박스가 출력 단계로 완전히 이동해 이제 하나뿐이라
     두 요소를 동기화하던 코드가 필요 없어졌다. */

  function escStepHtml(s) {
    return String(s).replace(/[&<>"']/g, m => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&#39;" }[m]));
  }

  /* 1단계: 주소 목록 표 + 데이터 요약 갱신.
     updateLabelCount()에서도 호출되어, 라벨이 바뀔 때마다(엑셀 로드,
     추가/삭제/복사 등) 1단계 화면을 항상 최신 상태로 유지한다. */
  window.refreshDataStepView = function refreshDataStepView() {
    const labels = Array.from(postLabelContainer.querySelectorAll(".label"));
    const cnt = labels.length;
    const c = TEMPLATE_CONFIGS[templateSelect.value];
    const perPage = c?.perPage || 16;
    const startOffset = typeof getStoredStartOffset === "function"
      ? Math.min(getStoredStartOffset(templateSelect.value), perPage - 1) : 0;
    const pages = cnt > 0 ? Math.ceil((cnt + startOffset) / perPage) : 0;

    const totalEl = document.getElementById("wstep-stat-total");
    const pagesEl = document.getElementById("wstep-stat-pages");
    const tplEl   = document.getElementById("wstep-stat-template");
    if (totalEl) totalEl.textContent = cnt + "건";
    if (pagesEl) pagesEl.textContent = pages + "페이지";
    if (tplEl)   tplEl.textContent = c ? `${c.maker || ""} ${c.model || ""}`.trim() : "-";

    const countLabel = document.getElementById("wstep-table-count");
    if (countLabel) countLabel.textContent = `(${cnt}건)`;

    const nextBtn = document.getElementById("step-data-next");
    if (nextBtn) nextBtn.disabled = cnt === 0;

    /* 신규: 라벨 1개 미리보기 — 실제 첫 번째 라벨이 있으면 그 내용을,
       없으면 예시 문구를 그대로 둔다(빈 라벨을 억지로 만들지 않음). */
    const previewAddrEl = document.getElementById("wstep-preview-addr");
    const previewNameEl = document.getElementById("wstep-preview-name");
    const previewHintEl = document.getElementById("wstep-preview-hint");
    const previewPcEl   = document.getElementById("wstep-preview-postcode");
    const firstLabel = labels[0] || null;
    const firstAddr  = firstLabel?.querySelector(".addr")?.textContent?.trim();
    const firstName  = firstLabel?.querySelector(".name")?.textContent?.trim();
    if (previewAddrEl) previewAddrEl.textContent = firstAddr || "서울특별시 강남구 테헤란로 123, 456호";
    if (previewNameEl) previewNameEl.textContent = firstName || "홍길동 귀하";
    if (previewHintEl) previewHintEl.textContent = firstLabel
      ? "첫 번째 라벨(1번)을 그대로 보여줍니다."
      : "아직 데이터가 없어 예시로 표시 중입니다.";
    /* [신규] 우편번호 박스 — 실제 첫 라벨의 .post-digit 5칸을 그대로
       읽어와 보여주고, 없으면 예시("12345")로 채운다. */
    if (previewPcEl) {
      const realDigits = firstLabel
        ? Array.from(firstLabel.querySelectorAll(".post-digit")).map(d => d.textContent || " ")
        : null;
      const digits = (realDigits && realDigits.some(d => d.trim())) ? realDigits : "12345".split("");
      previewPcEl.innerHTML = digits.map(d => `<span class="wspd">${escStepHtml(d.trim() || " ")}</span>`).join("");
    }
    /* [수정] 카드 비율을 실제 규격(w×h)에 맞추던 aspect-ratio 방식 대신,
       실제 라벨과 완전히 같은 mm 크기·글자 크기 변수를 그대로 쓰고
       카드 폭에 맞춰 전체를 축소(scale)하는 방식으로 바꿨다 — 그래야
       글자 크기까지 실물 비율 그대로 작아져 "진짜 라벨을 축소한 모양"
       처럼 보인다. 계산은 updateSingleLabelPreviewScale()이 담당. */
    updateSingleLabelPreviewScale();

    /* 신규: 자주 쓰는 라벨지 카드 — 현재 선택된 규격과 일치하면 강조 표시 */
    document.querySelectorAll(".wstep-template-card[data-template-key]").forEach(card => {
      card.classList.toggle("active", card.dataset.templateKey === templateSelect.value);
    });

    const emptyEl = document.getElementById("wstep-table-empty");
    const tableEl = document.getElementById("wstep-table");
    const bodyEl  = document.getElementById("wstep-table-body");
    if (!emptyEl || !tableEl || !bodyEl) return;
    if (cnt === 0) {
      emptyEl.style.display = "";
      tableEl.style.display = "none";
      return;
    }
    emptyEl.style.display = "none";
    tableEl.style.display = "";

    /* 매우 큰 데이터는 표를 전부 그리면 느려지므로 상한을 둔다 —
       전체 목록/검색은 2단계 캔버스에서 그대로 가능하다. */
    const MAX_ROWS = 500;
    /* [신규] 이름/우편번호/주소 칸을 contenteditable로 만들어 표에서 바로
       입력할 수 있게 한다(엑셀 없이도 "행 추가" 버튼으로 빈 행을 만들고
       여기서 직접 채우는 흐름). data-widx는 postLabelContainer 안의
       .label 순서상 인덱스 — 편집 이벤트 핸들러(wireWstepTableEditing)가
       이 값으로 실제 .label 요소를 다시 찾아 값을 반영한다. */
    /* [신규] 행별 오류 표시 — "칸 넘침(overflow)" 같은 픽셀/레이아웃 기준이
       아니라, 이름·주소가 비어있는지·우편번호가 5자리 숫자인지처럼
       실제 값 자체를 확인해서 판단한다("오류를 픽셀로 판단하냐"는
       피드백 반영). 참고로 overflow 판정(validateLabel()의 err-overflow)은
       .label의 실제 렌더링 높이(scrollHeight/clientHeight)를 읽어야 하는데,
       1단계(데이터)에서는 캔버스 자체가 화면에 없어(display:none 조상 아래)
       그 값을 애초에 신뢰할 수 없다 — 그래서도 데이터 단계 오류 판정에는
       쓰지 않는다. */
    const ZIP_RE = /^\d{5}$/;
    let errRowCount = 0;
    const rowsHtml = labels.slice(0, MAX_ROWS).map((label, i) => {
      const name = label.querySelector(".name")?.textContent?.trim() || "";
      const addr = label.querySelector(".addr")?.textContent?.trim().replace(/\s+/g, " ") || "";
      const zip  = Array.from(label.querySelectorAll(".post-digit")).map(d => d.textContent || " ").join("").trim();
      const phone = label.dataset.phone || "";

      const rowErrs = [];
      if (!name) rowErrs.push("이름 누락");
      if (!addr) rowErrs.push("주소 누락");
      if (zip && !ZIP_RE.test(zip)) rowErrs.push(`우편번호 오류(${escStepHtml(zip)})`);
      const hasErr = rowErrs.length > 0;
      if (hasErr) errRowCount++;

      return `<tr data-widx="${i}"${hasErr ? ' class="wstep-row-err"' : ""}>`
           + `<td class="wstep-td-num">${hasErr
               ? `<span class="wstep-row-err-badge" title="${escStepHtml(rowErrs.join(" · "))}">!</span>`
               : (i + 1)}</td>`
           + `<td contenteditable="true" data-field="name" class="wstep-td-editable${!name ? " wstep-td-empty" : ""}">${escStepHtml(name)}</td>`
           + `<td contenteditable="true" data-field="zip" class="wstep-td-zip wstep-td-editable${zip && !ZIP_RE.test(zip) ? " wstep-td-empty" : ""}">${escStepHtml(zip)}</td>`
           + `<td contenteditable="true" data-field="addr" class="wstep-td-editable${!addr ? " wstep-td-empty" : ""}">${escStepHtml(addr)}</td>`
           + `<td contenteditable="true" data-field="phone" class="wstep-td-zip wstep-td-editable">${escStepHtml(phone)}</td>`
           + `<td class="wstep-td-actions">`
           + `<button type="button" class="wstep-row-delete-btn" title="이 행 삭제" aria-label="이 행 삭제">`
           + `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 01-2 2H8a2 2 0 01-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>`
           + `</button></td></tr>`;
    }).join("");
    bodyEl.innerHTML = rowsHtml + (cnt > MAX_ROWS
      ? `<tr><td colspan="6" style="text-align:center;color:var(--text-3);padding:10px">외 ${cnt - MAX_ROWS}건 더 있음 — 2단계에서 전체를 확인하세요</td></tr>`
      : "");
    if (countLabel) countLabel.textContent = errRowCount > 0 ? `(${cnt}건 · 오류 ${errRowCount}건)` : `(${cnt}건)`;
  };

  /* 3단계: 출력 요약 + 실물 인쇄 미리보기 갱신 */
  function refreshPrintStepView() {
    const labels = Array.from(postLabelContainer.querySelectorAll(".label"));
    const cnt = labels.length;
    const c = TEMPLATE_CONFIGS[templateSelect.value];
    const perPage = c?.perPage || 16;

    const totalEl   = document.getElementById("pstep-stat-total");
    const perPageEl = document.getElementById("pstep-stat-perpage");
    const tplEl     = document.getElementById("pstep-stat-template");
    if (totalEl)   totalEl.textContent = cnt + "건";
    if (perPageEl) perPageEl.textContent = perPage + "개";
    if (tplEl)     tplEl.textContent = c ? `A4 · ${c.model || ""}`.trim() : "-";

    /* [제거] 워터마크 체크박스가 출력 단계로 이동해 동기화가 필요 없어짐 */

    /* 화면에 보이게 된 다음 프레임에서 빌드해야 폭 측정이 정확하다
       (openPrintPreview()와 동일한 이유 — 위 주석 참고) */
    requestAnimationFrame(() => {
      const total = buildPrintPreviewPages("step3-preview-canvas", "pstep-page-info");
      const pagesEl = document.getElementById("pstep-stat-pages");
      if (pagesEl) pagesEl.textContent = total + "페이지";
      renderPstepPageNav("step3-preview-canvas", total); /* 신규: 페이지 탐색 카드 동기화 */
    });
  }

  /* 최초 진입 시: 이전 세션에서 복원된 라벨이 있으면 2단계(편집)부터,
     없으면 1단계(데이터)부터 시작한다. */
  requestAnimationFrame(() => {
    const hasLabels = postLabelContainer.querySelectorAll(".label").length > 0;
    setAddrStep(hasLabels ? "edit" : "data");
  });
})();

/* ══════════════════════════════════════════════════════════════
   1단계: 주소 직접 입력 모달 — 신규
   ──────────────────────────────────────────────────────────────
   "직접 입력하기"를 누르면 2단계로 넘어가지 않고 이 모달이 뜬다.
   여기서 입력한 주소는 엑셀로 불러온 것과 똑같이 기존 createLabel()로
   실제 라벨을 만들어 캔버스(postLabelContainer)에 쌓아두되, 화면은
   1단계에 그대로 머문다 — updateLabelCount()가 자동으로 1단계의
   "주소 목록" 표를 갱신해 준다(이미 훅 되어 있음). 라벨 미리보기는
   "다음: 라벨 편집"을 눌러 2단계로 넘어갈 때 비로소 눈에 보이게 된다.
   ══════════════════════════════════════════════════════════════ */
(function() {
  const modal    = document.getElementById("manual-entry-modal");
  if (!modal) return;
  const nameEl    = document.getElementById("me-name");
  const phoneEl   = document.getElementById("me-phone");
  const addrEl    = document.getElementById("me-addr");
  const addr2El   = document.getElementById("me-addr2");
  const zipEl     = document.getElementById("me-zip");
  const errEl     = document.getElementById("me-error");
  const addBtnEl  = document.getElementById("manual-entry-add");
  const addMoreBtnEl = document.getElementById("manual-entry-add-more");
  const cancelBtnEl  = document.getElementById("manual-entry-cancel");

  function openModal() {
    errEl.style.display = "none";
    modal.style.display = "flex";
    requestAnimationFrame(() => { modal.classList.add("active"); nameEl.focus(); });
  }
  function closeModal() {
    modal.classList.remove("active");
    setTimeout(() => { modal.style.display = "none"; }, 200);
  }
  function clearFields() {
    nameEl.value = ""; phoneEl.value = ""; addrEl.value = ""; addr2El.value = ""; zipEl.value = "";
  }
  function showError(msg) { errEl.textContent = msg; errEl.style.display = ""; }

  /* 입력값으로 실제 라벨 하나를 만들어 캔버스에 추가.
     addBtn 클릭 핸들러(index.js: createLabel → append → reflowPages →
     saveToStorage → updateLabelCount)와 동일한 순서를 그대로 따른다 —
     다른 점은 scrollToLabel/화면 전환을 하지 않는다는 것뿐이다. */
  async function addOneEntry() {
    const name = nameEl.value.trim();
    const addr = addrEl.value.trim();
    const addr2 = addr2El.value.trim();
    if (!name && !addr) { showError("이름 또는 주소 중 하나는 입력해주세요."); return false; }
    errEl.style.display = "none";

    const fullAddr = addr2 ? `${addr} ${addr2}` : addr;
    let zip = zipEl.value.trim();
    if (!zip && fullAddr && typeof fetchPostCode === "function") {
      addBtnEl.disabled = true; addMoreBtnEl.disabled = true;
      addBtnEl.textContent = "조회 중...";
      try { zip = (await fetchPostCode(fullAddr)).trim(); } catch { zip = ""; }
      addBtnEl.disabled = false; addMoreBtnEl.disabled = false;
      addBtnEl.textContent = "추가";
    }

    const item = { "이름": name, "주소": fullAddr, "postCode": zip || "     " };
    const nw = createLabel(item);
    if (phoneEl.value.trim()) nw.dataset.phone = phoneEl.value.trim();
    postLabelContainer.appendChild(nw);
    reflowPages();
    saveToStorage();
    updateLabelCount(); /* → window.refreshDataStepView()가 자동으로 1단계 표를 갱신 */
    return true;
  }

  addBtnEl.addEventListener("click", async () => {
    if (await addOneEntry()) { showToast("✅ 주소 목록에 추가했습니다.", "success"); closeModal(); clearFields(); }
  });
  addMoreBtnEl.addEventListener("click", async () => {
    if (await addOneEntry()) { showToast("✅ 추가했습니다. 계속 입력해주세요.", "success"); clearFields(); nameEl.focus(); }
  });
  cancelBtnEl.addEventListener("click", closeModal);
  modal.addEventListener("click", e => { if (e.target === modal) closeModal(); });
  [nameEl, phoneEl, addrEl, addr2El, zipEl].forEach(el => {
    el.addEventListener("keydown", e => { if (e.key === "Enter") { e.preventDefault(); addBtnEl.click(); } });
  });

  window._openManualEntryModal = openModal;
})();

/* ══════════════════════════════════════════════════════════════
   우편번호 직접 수정 기능
   기존 함수/이벤트/로직 변경 없음 — 순수 추가 기능.
   다음(카카오) 주소 API에 해당 주소가 등록되어 있지 않아 자동 조회가
   빈칸으로 실패하는 경우가 있어, 우편번호 5칸을 클릭해 직접
   입력/수정할 수 있게 한다.
     - 숫자 1자리만 입력되며, 입력 시 자동으로 다음 칸으로 이동
     - Backspace로 빈 칸에서 이전 칸으로 이동
     - ←/→ 화살표로 칸 이동, 5자리를 한 번에 붙여넣기하면 자동 분배
     - 실행취소(Ctrl+Z)/다시실행(Ctrl+Shift+Z)은 기존 주소 편집
       undo 엔트리 형식(field:"addr" + oldPostCode)을 그대로 재사용
   ══════════════════════════════════════════════════════════════ */
(function() {
  function placeCaretEnd(el) {
    const range = document.createRange();
    range.selectNodeContents(el);
    range.collapse(false);
    const sel = window.getSelection();
    sel.removeAllRanges();
    sel.addRange(range);
  }
  function selectAllContent(el) {
    const range = document.createRange();
    range.selectNodeContents(el);
    const sel = window.getSelection();
    sel.removeAllRanges();
    sel.addRange(range);
  }
  function readCode(pcCont) {
    /* 각 칸이 항상 1글자(숫자 또는 공백)라는 전제로 위치 그대로 이어붙인다 */
    return Array.from(pcCont.querySelectorAll(".post-digit")).map(d => d.textContent || " ").join("").padEnd(5, " ").slice(0, 5);
  }

  /* 편집 진입 시 스냅샷 1회 캡처(같은 컨테이너 내 탭 이동 중엔 재캡처 안 함) */
  postLabelContainer.addEventListener("focusin", e => {
    if (!e.target.classList?.contains("post-digit")) return;
    const pcCont = e.target.closest(".post-code-container");
    if (!pcCont) return;
    if (pcCont._pcEditing === undefined) {
      pcCont._pcEditing  = true;
      pcCont._pcSnapshot = readCode(pcCont);
    }
    selectAllContent(e.target);
  });

  /* 컨테이너(5칸)를 완전히 벗어날 때만 실행취소 기록 + 저장 */
  postLabelContainer.addEventListener("focusout", e => {
    if (!e.target.classList?.contains("post-digit")) return;
    const pcCont = e.target.closest(".post-code-container");
    if (!pcCont) return;
    const stillInside = e.relatedTarget && pcCont.contains(e.relatedTarget);
    if (stillInside) return;

    const labelEl = pcCont.closest(".label");
    const newCode = readCode(pcCont);
    const oldCode = pcCont._pcSnapshot;
    pcCont._pcEditing  = undefined;
    pcCont._pcSnapshot = undefined;

    if (oldCode !== undefined && oldCode !== newCode && labelEl) {
      /* 주소 편집 undo 형식(field:"addr")을 재사용: addr 텍스트는 현재 값 그대로 두고
         (되돌려도 변화 없음) 우편번호(oldPostCode)만 되돌리도록 함 */
      undoStack.push({
        type: "edit", labelEl, field: "addr",
        oldValue: labelEl.querySelector(".addr")?.textContent ?? "",
        oldPostCode: oldCode
      });
      if (undoStack.length > MAX_UNDO) undoStack.shift();
      redoStack.length = 0; /* 새 액션 → Redo 초기화 */
      updateUndoBtn();
    }
    saveToStorage();
  });

  /* 숫자 1자리만 허용 + 입력 시 다음 칸으로 자동 이동
     지운 경우에도 칸은 공백 1글자를 유지해 자릿수가 밀리지 않게 함 */
  postLabelContainer.addEventListener("input", e => {
    if (!e.target.classList?.contains("post-digit")) return;
    const el = e.target;
    const raw   = el.textContent.replace(/[^0-9]/g, "");
    const digit = raw.length ? raw.slice(-1) : ""; /* 마지막에 입력한 숫자 1개만 유지 */
    const newContent = digit || " ";
    if (el.textContent !== newContent) el.textContent = newContent;
    placeCaretEnd(el);
    if (digit) {
      const next = el.nextElementSibling;
      if (next && next.classList.contains("post-digit")) { next.focus(); selectAllContent(next); }
    }
  });

  /* Backspace로 빈 칸에서 이전 칸 이동, 화살표로 칸 이동, Enter로 편집 종료 */
  postLabelContainer.addEventListener("keydown", e => {
    if (!e.target.classList?.contains("post-digit")) return;
    const el = e.target;
    if (e.key === "Backspace" && !el.textContent.trim()) {
      const prev = el.previousElementSibling;
      if (prev && prev.classList.contains("post-digit")) {
        e.preventDefault();
        prev.textContent = " ";
        prev.focus(); placeCaretEnd(prev);
      }
    } else if (e.key === "ArrowLeft") {
      const prev = el.previousElementSibling;
      if (prev && prev.classList.contains("post-digit")) { e.preventDefault(); prev.focus(); selectAllContent(prev); }
    } else if (e.key === "ArrowRight") {
      const next = el.nextElementSibling;
      if (next && next.classList.contains("post-digit")) { e.preventDefault(); next.focus(); selectAllContent(next); }
    } else if (e.key === "Enter") {
      e.preventDefault(); el.blur();
    }
  });

  /* 우편번호 5자리를 통째로 붙여넣으면 5칸에 자동 분배 */
  postLabelContainer.addEventListener("paste", e => {
    if (!e.target.classList?.contains("post-digit")) return;
    const text = (e.clipboardData || window.clipboardData).getData("text").replace(/[^0-9]/g, "");
    if (!text) return;
    e.preventDefault();
    const pcCont = e.target.closest(".post-code-container");
    const boxes  = Array.from(pcCont.querySelectorAll(".post-digit"));
    let ti = boxes.indexOf(e.target);
    for (const ch of text) {
      if (ti < 0 || ti >= boxes.length) break;
      boxes[ti].textContent = ch;
      ti++;
    }
    const focusTarget = boxes[Math.min(Math.max(ti, 0), boxes.length - 1)];
    focusTarget.focus(); placeCaretEnd(focusTarget);
    pcCont.dispatchEvent(new Event("input", { bubbles: true })); /* saveToStorage 디바운스 트리거 */
  });
})();
