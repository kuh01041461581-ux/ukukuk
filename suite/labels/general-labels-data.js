/* ══════════════════════════════════════════════════════════════
   일반라벨 규격 데이터베이스 — general-labels-data.js
   ⚠️ 이 파일은 "일반라벨" 모드 전용이며, 기존 주소라벨(TEMPLATE_CONFIGS,
      index.js)과는 완전히 독립된 데이터입니다.

   필드 설명
   ── maker      : 제조사 표시명
   ── model      : 모델/제품번호
   ── perPage    : 한 페이지(A4)당 칸 수 = colN × rowN
   ── colN/rowN  : 가로 칸 수 / 세로 칸 수
   ── labelW/H   : 라벨 1칸의 가로/세로 크기 (mm)
   ── colGap     : 칸과 칸 사이 가로(좌우) 간격 (mm)
   ── pt/pb/pl   : 페이지 상단/하단/좌우 여백 (mm)
   ── source     : 규격 출처/근거 (참고용)
   ══════════════════════════════════════════════════════════════ */
(function (global) {
  "use strict";

  const GENERAL_TEMPLATE_CONFIGS = {

    /* ═══════════════════ Formtec (폼텍) ═══════════════════ */
    "G_F_3107": {
      maker: "Formtec (폼텍)", model: "LS-3107", category: "주소호환",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 99.1, labelH: 33.9, colGap: 2.5,
      pt: 14.25, pb: 11.55, pl: 5.25,
      source: "실측 — 99.1×33.9mm 16칸"
    },
    "G_F_3108": {
      maker: "Formtec (폼텍)", model: "LS-3108", category: "주소호환",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 99.1, labelH: 38.1, colGap: 2.5,
      pt: 15.15, pb: 15.15, pl: 4.65,
      source: "실측 — 99.1×38.1mm 14칸"
    },
    "G_F_3105": {
      maker: "Formtec (폼텍)", model: "LS-3105", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 63.5, labelH: 38.1, colGap: 2.5,
      pt: 15.15, pb: 15.15, pl: 7.25,
      source: "실측 — 63.5×38.1mm 21칸"
    },
    "G_F_3112": {
      maker: "Formtec (폼텍)", model: "LS-3112", category: "물류/소포",
      perPage: 12, colN: 3, rowN: 4,
      labelW: 63.5, labelH: 70.0, colGap: 2.5,
      pt: 8.50, pb: 8.50, pl: 7.25,
      source: "실측 — 63.5×70.0mm 12칸"
    },
    "G_F_3109": {
      maker: "Formtec (폼텍)", model: "LS-3109", category: "주소호환",
      perPage: 18, colN: 2, rowN: 9,
      labelW: 100.0, labelH: 30.0, colGap: 2.5,
      pt: 14, pb: 13, pl: 4,
      source: "실측 — 100×30mm 18칸"
    },
    "G_F_3510": {
      maker: "Formtec (폼텍)", model: "LS-3510", category: "명함분류",
      perPage: 10, colN: 2, rowN: 5,
      labelW: 88.9, labelH: 52.0, colGap: 10,
      pt: 18.50, pb: 18.50, pl: 11.10,
      source: "실측 — 88.9×52.0mm 10칸"
    },
    "G_F_3114": {
      maker: "Formtec (폼텍)", model: "LS-3114", category: "대형",
      perPage: 8, colN: 2, rowN: 4,
      labelW: 99.1, labelH: 67.7, colGap: 2.5,
      pt: 13.10, pb: 13.10, pl: 4.65,
      source: "실측 — 99.1×67.7mm 8칸 (칸사이 간격 2.5mm로 통일 — 우편라벨 모드(index.js)의 3107/3108/3114와 동일 계열)"
    },
    "G_F_3630": {
      maker: "Formtec (폼텍)", model: "LS-3630", category: "미디어",
      perPage: 12, colN: 2, rowN: 6,
      labelW: 89.0, labelH: 42.0, colGap: 2.4,
      pt: 22.50, pb: 22.50, pl: 14.80,
      source: "실측 — 89×42mm 12칸"
    },
    "G_F_3102": {
      maker: "Formtec (폼텍)", model: "LS-3102", category: "바코드",
      perPage: 40, colN: 4, rowN: 10,
      labelW: 47.0, labelH: 26.9, colGap: 2.4,
      pt: 14.00, pb: 14.00, pl: 7.40,
      source: "실측 — 47×26.9mm 40칸"
    },
    "G_F_3622": {
      maker: "Formtec (폼텍)", model: "LS-3622", category: "분류표기",
      perPage: 96, colN: 6, rowN: 16,
      labelW: 30.0, labelH: 14.0, colGap: 1,
      pt: 36.50, pb: 36.50, pl: 12.50,
      source: "실측 — 30×14mm 96칸"
    },
    "G_F_3130": {
      maker: "Formtec (폼텍)", model: "LS-3130", category: "전지(1칸)",
      perPage: 1, colN: 1, rowN: 1,
      labelW: 210.0, labelH: 297.0, colGap: 0,
      pt: 0, pb: 0, pl: 0,
      source: "전지라벨 — A4 전면 1칸"
    },
    "G_F_STD30": {
      maker: "Formtec (폼텍)", model: "표준 30칸", category: "표준 호환",
      perPage: 30, colN: 3, rowN: 10,
      labelW: 66.0, labelH: 25.4, colGap: 2,
      pt: 21.50, pb: 21.50, pl: 4.00,
      source: "업계 표준 30칸"
    },
    "G_F_3106": {
      maker: "Formtec (폼텍)", model: "LS-3106", category: "주소호환",
      perPage: 20, colN: 4, rowN: 5,
      labelW: 47.5, labelH: 54.0, colGap: 2,
      pt: 13.50, pb: 13.50, pl: 7.00,
      source: "실측 — 47.5×54.0mm 20칸"
    },
    "G_F_3115": {
      maker: "Formtec (폼텍)", model: "LS-3115", category: "대형 소포",
      perPage: 6, colN: 2, rowN: 3,
      labelW: 99.1, labelH: 93.1, colGap: 2,
      pt: 8.85, pb: 8.85, pl: 4.90,
      source: "실측 — 99.1×93.1mm 6칸"
    },
    "G_F_3118": {
      maker: "Formtec (폼텍)", model: "LS-3118", category: "주소호환",
      perPage: 24, colN: 3, rowN: 8,
      labelW: 64.0, labelH: 33.9, colGap: 2,
      pt: 12.90, pb: 12.90, pl: 7.00,
      source: "실측 — 64×33.9mm 24칸"
    },
    "G_F_3640": {
      maker: "Formtec (폼텍)", model: "LS-3640", category: "CD/DVD",
      perPage: 2, colN: 1, rowN: 2,
      labelW: 117.0, labelH: 117.0, colGap: 0,
      pt: 31.50, pb: 31.50, pl: 46.50,
      source: "실측 — 117×117mm 2칸(원형)"
    },
    "G_F_3650": {
      maker: "Formtec (폼텍)", model: "LS-3650", category: "CD/DVD",
      perPage: 1, colN: 1, rowN: 1,
      labelW: 117.0, labelH: 117.0, colGap: 0,
      pt: 90.0, pb: 90.0, pl: 46.50,
      source: "실측 — 117×117mm 1칸(원형)"
    },
    "G_F_3120": {
      maker: "Formtec (폼텍)", model: "LS-3120", category: "물류/소포(상하 2단)",
      perPage: 2, colN: 1, rowN: 2,
      labelW: 200.0, labelH: 140.0, colGap: 0,
      pt: 8.50, pb: 8.50, pl: 5.00,
      source: "실측 — 200×140mm 2칸(소포/물류관리용, A4를 상하로 2등분)"
    },

    /* ═══════════════════ 애니라벨 (프린텍) ═══════════════════ */
    "G_A_V3220": {
      maker: "애니라벨 (프린텍)", model: "V3220", category: "우편/대형",
      perPage: 8, colN: 2, rowN: 4,
      labelW: 99.06, labelH: 67.71, colGap: 2.5,
      pt: 13.08, pb: 13.08, pl: 4.94,
      source: "실측 — 99.06×67.71mm 8칸"
    },
    "G_A_V3250": {
      maker: "애니라벨 (프린텍)", model: "V3250", category: "우편발송",
      perPage: 10, colN: 2, rowN: 5,
      labelW: 88.9, labelH: 52.0, colGap: 2.5,
      pt: 18.50, pb: 18.50, pl: 15.10,
      source: "실측 — 88.9×52.0mm 10칸"
    },
    "G_A_V3270": {
      maker: "애니라벨 (프린텍)", model: "V3270", category: "우편발송",
      perPage: 12, colN: 2, rowN: 6,
      labelW: 100.0, labelH: 46.4, colGap: 2.5,
      pt: 9.30, pb: 9.30, pl: 4.00,
      source: "실측 — 100×46.4mm 12칸"
    },
    "G_A_V3230": {
      maker: "애니라벨 (프린텍)", model: "V3230", category: "우편발송",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 99.06, labelH: 38.1, colGap: 2.5,
      pt: 15.15, pb: 15.15, pl: 5.25,
      source: "실측 — 99.06×38.1mm 14칸"
    },
    "G_A_V3240": {
      maker: "애니라벨 (프린텍)", model: "V3240", category: "우편발송",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 99.06, labelH: 33.85, colGap: 2.5,
      pt: 14, pb: 12.22, pl: 5.25,
      source: "실측 — 99.06×33.85mm 16칸"
    },
    "G_A_V3320": {
      maker: "애니라벨 (프린텍)", model: "V3320", category: "우편발송",
      perPage: 18, colN: 3, rowN: 6,
      labelW: 63.5, labelH: 46.5, colGap: 2,
      pt: 9.00, pb: 9.00, pl: 7.75,
      source: "실측 — 63.5×46.5mm 18칸"
    },
    "G_A_V3330": {
      maker: "애니라벨 (프린텍)", model: "V3330", category: "우편발송",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 63.5, labelH: 38.1, colGap: 2,
      pt: 15.15, pb: 15.15, pl: 7.75,
      source: "실측 — 63.5×38.1mm 21칸"
    },
    "G_A_V3340": {
      maker: "애니라벨 (프린텍)", model: "V3340", category: "우편발송",
      perPage: 24, colN: 3, rowN: 8,
      labelW: 64.0, labelH: 33.86, colGap: 2,
      pt: 13.06, pb: 13.06, pl: 7.00,
      source: "실측 — 64×33.86mm 24칸"
    },
    "G_A_V3350": {
      maker: "애니라벨 (프린텍)", model: "V3350", category: "바코드",
      perPage: 27, colN: 3, rowN: 9,
      labelW: 62.7, labelH: 30.1, colGap: 2,
      pt: 13.05, pb: 13.05, pl: 8.95,
      source: "실측 — 62.7×30.1mm 27칸"
    },
    "G_A_V3410": {
      maker: "애니라벨 (프린텍)", model: "V3410", category: "바코드",
      perPage: 40, colN: 4, rowN: 10,
      labelW: 47.0, labelH: 26.9, colGap: 2,
      pt: 14.00, pb: 14.00, pl: 8.00,
      source: "실측 — 47×26.9mm 40칸"
    },
    "G_A_V8990": {
      maker: "애니라벨 (프린텍)", model: "V8990", category: "방수/소형",
      perPage: 60, colN: 4, rowN: 15,
      labelW: 45.7, labelH: 18.0, colGap: 2,
      pt: 13.50, pb: 13.50, pl: 10.60,
      source: "업계 표준 60칸 패턴"
    },
    "G_A_V3510": {
      maker: "애니라벨 (프린텍)", model: "V3510", category: "바코드/소형",
      perPage: 65, colN: 5, rowN: 13,
      labelW: 38.1, labelH: 21.08, colGap: 2,
      pt: 11.48, pb: 11.48, pl: 5.75,
      source: "실측 — 38.1×21.08mm 65칸"
    },
    "G_A_V3110": {
      maker: "애니라벨 (프린텍)", model: "V3110", category: "전지(1칸)",
      perPage: 1, colN: 1, rowN: 1,
      labelW: 210.0, labelH: 297.0, colGap: 0,
      pt: 0, pb: 0, pl: 0,
      source: "전지라벨 A4 전면 1칸"
    },
    "G_A_V3210": {
      maker: "애니라벨 (프린텍)", model: "V3210", category: "대형 소포",
      perPage: 6, colN: 2, rowN: 3,
      labelW: 99.06, labelH: 93.0, colGap: 2.5,
      pt: 9.00, pb: 9.00, pl: 4.94,
      source: "실측 — 99.06×93.0mm 6칸"
    },
    "G_A_V3120": {
      maker: "애니라벨 (프린텍)", model: "V3120", category: "물류/소포(상하 2단)",
      perPage: 2, colN: 1, rowN: 2,
      labelW: 199.9, labelH: 143.51, colGap: 0,
      pt: 4.99, pb: 4.99, pl: 5.05,
      source: "실측 — 199.9×143.51mm 2칸(소포/물류관리용, A4를 상하로 2등분)"
    },

    /* ═══════════════════ 아이라벨 (iLabel) ═══════════════════ */
    "G_IL_CL228": {
      maker: "아이라벨 (iLabel)", model: "CL228", category: "주소호환",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 98.8, labelH: 33.67, colGap: 2.4,
      pt: 13.82, pb: 13.82, pl: 5.00,
      source: "실측 — 98.8×33.67mm 16칸"
    },
    "G_IL_CL227": {
      maker: "아이라벨 (iLabel)", model: "CL227", category: "주소호환",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 98.8, labelH: 38.05, colGap: 2.4,
      pt: 15.33, pb: 15.33, pl: 5.00,
      source: "실측 — 98.8×38.05mm 14칸"
    },
    "G_IL_CL437": {
      maker: "아이라벨 (iLabel)", model: "CL437", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 63.8, labelH: 39.97, colGap: 2.6,
      pt: 8.61, pb: 8.61, pl: 6.70,
      source: "실측 — 63.8×39.97mm 21칸"
    },
    "G_IL_CL225": {
      maker: "아이라벨 (iLabel)", model: "CL225", category: "대형",
      perPage: 10, colN: 2, rowN: 5,
      labelW: 99.1, labelH: 57.0, colGap: 2.5,
      pt: 6.00, pb: 6.00, pl: 4.90,
      source: "실측 — 99.1×57.0mm 10칸"
    },
    "G_IL_CL239": {
      maker: "아이라벨 (iLabel)", model: "CL239", category: "바코드",
      perPage: 27, colN: 3, rowN: 9,
      labelW: 62.2, labelH: 30.03, colGap: 2.6,
      pt: 13.37, pb: 13.37, pl: 9.10,
      source: "실측 — 62.2×30.03mm 27칸"
    },
    "G_IL_CL226": {
      maker: "아이라벨 (iLabel)", model: "CL226", category: "주소호환",
      perPage: 12, colN: 2, rowN: 6,
      labelW: 98.8, labelH: 46.5, colGap: 2.4,
      pt: 9.00, pb: 9.00, pl: 5.00,
      source: "실측 — 98.8×46.5mm 12칸"
    },
    "G_IL_CL435": {
      maker: "아이라벨 (iLabel)", model: "CL435", category: "주소호환",
      perPage: 18, colN: 3, rowN: 6,
      labelW: 63.8, labelH: 46.5, colGap: 2.6,
      pt: 9.00, pb: 9.00, pl: 6.70,
      source: "실측 — 63.8×46.5mm 18칸"
    },
    "G_IL_CL450": {
      maker: "아이라벨 (iLabel)", model: "CL450", category: "바코드/소형",
      perPage: 40, colN: 4, rowN: 10,
      labelW: 47.0, labelH: 26.9, colGap: 2,
      pt: 14.00, pb: 14.00, pl: 8.00,
      source: "실측 — 47×26.9mm 40칸"
    },
    "G_IL_CL221": {
      maker: "아이라벨 (iLabel)", model: "CL221", category: "대형 소포",
      perPage: 8, colN: 2, rowN: 4,
      labelW: 99.1, labelH: 67.7, colGap: 2.5,
      pt: 13.10, pb: 13.10, pl: 4.90,
      source: "실측 — 99.1×67.7mm 8칸"
    },
    "G_IL_CL440": {
      maker: "아이라벨 (iLabel)", model: "CL440", category: "바코드/소형",
      perPage: 30, colN: 3, rowN: 10,
      labelW: 66.0, labelH: 25.4, colGap: 2,
      pt: 21.50, pb: 21.50, pl: 4.00,
      source: "표준 30칸 — 66×25.4mm"
    },
    "G_IL_CL320": {
      maker: "아이라벨 (iLabel)", model: "CL320", category: "분류/소형",
      perPage: 65, colN: 5, rowN: 13,
      labelW: 38.1, labelH: 21.2, colGap: 2,
      pt: 10.70, pb: 10.70, pl: 5.75,
      source: "실측 — 38.1×21.2mm 65칸"
    },
    "G_IL_CL612": {
      maker: "아이라벨 (iLabel)", model: "CL612", category: "물류표기(상하 2단)",
      perPage: 2, colN: 1, rowN: 2,
      labelW: 210.0, labelH: 148.5, colGap: 0,
      pt: 0, pb: 0, pl: 0,
      source: "실측 — 210×148.5mm 2칸, 여백없음(R0 직각모서리), A4를 상하로 정확히 2등분"
    },
    "G_IL_CL212": {
      maker: "아이라벨 (iLabel)", model: "CL212", category: "물류표기(상하 2단)",
      perPage: 2, colN: 1, rowN: 2,
      labelW: 199.0, labelH: 143.5, colGap: 0,
      pt: 5.00, pb: 5.00, pl: 5.50,
      source: "실측 — 199×143.5mm 2칸(물류표기용), A4를 상하로 2등분"
    },

    /* ═══════════════════ iLIFE ═══════════════════ */
    "G_ILF_2016": {
      maker: "iLIFE", model: "IL-2016", category: "주소호환",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 99.1, labelH: 33.9, colGap: 2.5,
      pt: 12.90, pb: 12.90, pl: 4.90,
      source: "실측 — 99.1×33.9mm 16칸"
    },
    "G_ILF_2014": {
      maker: "iLIFE", model: "IL-2014", category: "주소호환",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 99.1, labelH: 38.1, colGap: 2.5,
      pt: 15.15, pb: 15.15, pl: 4.90,
      source: "실측 — 99.1×38.1mm 14칸"
    },
    "G_ILF_2012": {
      maker: "iLIFE", model: "IL-2012", category: "주소호환",
      perPage: 12, colN: 2, rowN: 6,
      labelW: 99.1, labelH: 42.3, colGap: 2.5,
      pt: 21.60, pb: 21.60, pl: 4.90,
      source: "실측 — 99.1×42.3mm 12칸"
    },
    "G_ILF_2010": {
      maker: "iLIFE", model: "IL-2010", category: "대형",
      perPage: 10, colN: 2, rowN: 5,
      labelW: 96.0, labelH: 51.5, colGap: 2.5,
      pt: 19.75, pb: 19.75, pl: 8.00,
      source: "실측 — 96×51.5mm 10칸"
    },
    "G_ILF_2008": {
      maker: "iLIFE", model: "IL-2008", category: "대형 소포",
      perPage: 8, colN: 2, rowN: 4,
      labelW: 99.1, labelH: 67.7, colGap: 2.5,
      pt: 13.10, pb: 13.10, pl: 4.90,
      source: "실측 — 99.1×67.7mm 8칸"
    },
    "G_ILF_3021": {
      maker: "iLIFE", model: "IL-3021", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 63.5, labelH: 38.1, colGap: 2,
      pt: 15.15, pb: 15.15, pl: 7.75,
      source: "실측 — 63.5×38.1mm 21칸"
    },
    "G_ILF_3030": {
      maker: "iLIFE", model: "IL-3030", category: "바코드",
      perPage: 30, colN: 3, rowN: 10,
      labelW: 66.0, labelH: 25.4, colGap: 2,
      pt: 21.50, pb: 21.50, pl: 4.00,
      source: "표준 30칸 — 66×25.4mm"
    },
    "G_ILF_4040": {
      maker: "iLIFE", model: "IL-4040", category: "바코드/소형",
      perPage: 40, colN: 4, rowN: 10,
      labelW: 47.0, labelH: 26.9, colGap: 2,
      pt: 14.00, pb: 14.00, pl: 8.00,
      source: "실측 — 47×26.9mm 40칸"
    },

    /* ═══════════════════ 오피스디포 ═══════════════════ */
    "G_OD_2016": {
      maker: "오피스디포", model: "OD-2016", category: "주소호환",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 101.0, labelH: 33.9, colGap: 0,
      pt: 12.90, pb: 12.90, pl: 4.00,
      source: "실측 — 101×33.9mm 16칸"
    },
    "G_OD_2030": {
      maker: "오피스디포", model: "OD-2030", category: "바코드",
      perPage: 30, colN: 3, rowN: 10,
      labelW: 66.0, labelH: 25.4, colGap: 2.5,
      pt: 21.50, pb: 21.50, pl: 4.00,
      source: "실측 — 66×25.4mm 30칸"
    },
    "G_OD_2014": {
      maker: "오피스디포", model: "OD-2014", category: "주소호환",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 101.0, labelH: 38.1, colGap: 0,
      pt: 15.15, pb: 15.15, pl: 4.00,
      source: "실측 — 101×38.1mm 14칸"
    },
    "G_OD_3021": {
      maker: "오피스디포", model: "OD-3021", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 64.0, labelH: 38.1, colGap: 2,
      pt: 15.15, pb: 15.15, pl: 7.00,
      source: "실측 — 64×38.1mm 21칸"
    },
    "G_OD_2008": {
      maker: "오피스디포", model: "OD-2008", category: "대형 소포",
      perPage: 8, colN: 2, rowN: 4,
      labelW: 101.0, labelH: 67.7, colGap: 0,
      pt: 13.10, pb: 13.10, pl: 4.00,
      source: "실측 — 101×67.7mm 8칸"
    },

    /* ═══════════════════ 아이라벨 LQ (레이저/잉크젯) ═══════════════════ */
    "G_LQ_2514": {
      maker: "아이라벨 LQ", model: "LQ-2514", category: "주소호환",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 100.0, labelH: 38.05, colGap: 2.5,
      pt: 15.33, pb: 15.33, pl: 4.00,
      source: "실측 — 100×38.05mm 14칸"
    },
    "G_LQ_2516": {
      maker: "아이라벨 LQ", model: "LQ-2516", category: "주소호환",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 100.0, labelH: 33.9, colGap: 2.5,
      pt: 12.90, pb: 12.90, pl: 4.00,
      source: "실측 — 100×33.9mm 16칸"
    },
    "G_LQ_2520": {
      maker: "아이라벨 LQ", model: "LQ-2520", category: "주소호환",
      perPage: 20, colN: 2, rowN: 10,
      labelW: 100.0, labelH: 27.0, colGap: 2.5,
      pt: 13.50, pb: 13.50, pl: 4.00,
      source: "실측 — 100×27mm 20칸"
    },
    "G_LQ_2521": {
      maker: "아이라벨 LQ", model: "LQ-3521", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 64.0, labelH: 38.05, colGap: 2,
      pt: 15.33, pb: 15.33, pl: 7.00,
      source: "실측 — 64×38.05mm 21칸"
    },
    "G_LQ_4040": {
      maker: "아이라벨 LQ", model: "LQ-4040", category: "바코드",
      perPage: 40, colN: 4, rowN: 10,
      labelW: 47.0, labelH: 26.9, colGap: 2,
      pt: 14.00, pb: 14.00, pl: 8.00,
      source: "실측 — 47×26.9mm 40칸"
    },
    "G_LQ_2508": {
      maker: "아이라벨 LQ", model: "LQ-2508", category: "대형",
      perPage: 8, colN: 2, rowN: 4,
      labelW: 100.0, labelH: 67.7, colGap: 2,
      pt: 13.10, pb: 13.10, pl: 4.00,
      source: "실측 — 100×67.7mm 8칸"
    },

    /* ═══════════════════ 한국후지쯔 / 삼성레이블 ═══════════════════ */
    "G_SAM_S16": {
      maker: "삼성레이블", model: "SL-S16", category: "주소호환",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 99.1, labelH: 33.9, colGap: 2.5,
      pt: 12.90, pb: 12.90, pl: 4.90,
      source: "삼성레이블 — 99.1×33.9mm 16칸"
    },
    "G_SAM_S14": {
      maker: "삼성레이블", model: "SL-S14", category: "주소호환",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 99.1, labelH: 38.1, colGap: 2.5,
      pt: 15.15, pb: 15.15, pl: 4.90,
      source: "삼성레이블 — 99.1×38.1mm 14칸"
    },
    "G_SAM_S21": {
      maker: "삼성레이블", model: "SL-S21", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 63.5, labelH: 38.1, colGap: 2,
      pt: 15.15, pb: 15.15, pl: 7.75,
      source: "삼성레이블 — 63.5×38.1mm 21칸"
    },
    "G_SAM_S30": {
      maker: "삼성레이블", model: "SL-S30", category: "바코드",
      perPage: 30, colN: 3, rowN: 10,
      labelW: 66.0, labelH: 25.4, colGap: 2,
      pt: 21.50, pb: 21.50, pl: 4.00,
      source: "삼성레이블 — 66×25.4mm 30칸"
    },

    /* ═══════════════════ 동방라벨 ═══════════════════ */
    "G_DB_A2016": {
      maker: "동방라벨", model: "A2016", category: "주소호환",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 99.1, labelH: 33.9, colGap: 2.5,
      pt: 12.90, pb: 12.90, pl: 4.90,
      source: "동방라벨 — 99.1×33.9mm 16칸"
    },
    "G_DB_A2014": {
      maker: "동방라벨", model: "A2014", category: "주소호환",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 99.1, labelH: 38.1, colGap: 2.5,
      pt: 15.15, pb: 15.15, pl: 4.90,
      source: "동방라벨 — 99.1×38.1mm 14칸"
    },
    "G_DB_A2021": {
      maker: "동방라벨", model: "A3021", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 63.5, labelH: 38.1, colGap: 2.5,
      pt: 15.15, pb: 15.15, pl: 7.75,
      source: "동방라벨 — 63.5×38.1mm 21칸"
    },
    "G_DB_A2008": {
      maker: "동방라벨", model: "A2008", category: "대형 소포",
      perPage: 8, colN: 2, rowN: 4,
      labelW: 99.1, labelH: 67.7, colGap: 2.5,
      pt: 13.10, pb: 13.10, pl: 4.90,
      source: "동방라벨 — 99.1×67.7mm 8칸"
    },
    "G_DB_A2040": {
      maker: "동방라벨", model: "A4040", category: "바코드",
      perPage: 40, colN: 4, rowN: 10,
      labelW: 47.0, labelH: 26.9, colGap: 2.5,
      pt: 14.00, pb: 14.00, pl: 8.00,
      source: "동방라벨 — 47×26.9mm 40칸"
    },

    /* ═══════════════════ 대한제지 / 아펙스 ═══════════════════ */
    "G_AP_AP16": {
      maker: "아펙스 (Apex)", model: "AP-2016", category: "주소호환",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 99.1, labelH: 33.9, colGap: 2.5,
      pt: 12.90, pb: 12.90, pl: 4.90,
      source: "아펙스 — 99.1×33.9mm 16칸"
    },
    "G_AP_AP14": {
      maker: "아펙스 (Apex)", model: "AP-2014", category: "주소호환",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 99.1, labelH: 38.1, colGap: 2.5,
      pt: 15.15, pb: 15.15, pl: 4.90,
      source: "아펙스 — 99.1×38.1mm 14칸"
    },
    "G_AP_AP21": {
      maker: "아펙스 (Apex)", model: "AP-3021", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 63.5, labelH: 38.1, colGap: 2,
      pt: 15.15, pb: 15.15, pl: 7.75,
      source: "아펙스 — 63.5×38.1mm 21칸"
    },
    "G_AP_AP30": {
      maker: "아펙스 (Apex)", model: "AP-3030", category: "바코드",
      perPage: 30, colN: 3, rowN: 10,
      labelW: 66.0, labelH: 25.4, colGap: 2,
      pt: 21.50, pb: 21.50, pl: 4.00,
      source: "아펙스 — 66×25.4mm 30칸"
    },
    "G_AP_AP08": {
      maker: "아펙스 (Apex)", model: "AP-2008", category: "대형 소포",
      perPage: 8, colN: 2, rowN: 4,
      labelW: 99.1, labelH: 67.7, colGap: 2.5,
      pt: 13.10, pb: 13.10, pl: 4.90,
      source: "아펙스 — 99.1×67.7mm 8칸"
    },
    "G_AP_AP10": {
      maker: "아펙스 (Apex)", model: "AP-2010", category: "대형",
      perPage: 10, colN: 2, rowN: 5,
      labelW: 99.1, labelH: 54.0, colGap: 2.5,
      pt: 13.50, pb: 13.50, pl: 4.90,
      source: "아펙스 — 99.1×54.0mm 10칸"
    },

    /* ═══════════════════ Avery (에이버리) 국제 표준 ═══════════════════ */
    "G_AV_5160": {
      maker: "Avery (에이버리)", model: "5160", category: "주소호환",
      perPage: 30, colN: 3, rowN: 10,
      labelW: 66.675, labelH: 25.4, colGap: 3.175,
      pt: 21.50, pb: 21.50, pl: 1.81,
      source: "Avery 5160 US standard — 30 per sheet"
    },
    "G_AV_5163": {
      maker: "Avery (에이버리)", model: "5163", category: "대형 소포",
      perPage: 10, colN: 2, rowN: 5,
      labelW: 101.6, labelH: 50.8, colGap: 3.175,
      pt: 21.50, pb: 21.50, pl: 1.81,
      source: "Avery 5163 US standard — 10 per sheet"
    },
    "G_AV_L7160": {
      maker: "Avery (에이버리)", model: "L7160 (EU)", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 63.5, labelH: 38.1, colGap: 2.5,
      pt: 15.15, pb: 15.15, pl: 7.25,
      source: "Avery L7160 EU A4 — 21 per sheet"
    },
    "G_AV_L7163": {
      maker: "Avery (에이버리)", model: "L7163 (EU)", category: "대형",
      perPage: 14, colN: 2, rowN: 7,
      labelW: 99.1, labelH: 38.1, colGap: 2.8,
      pt: 15.15, pb: 15.15, pl: 4.50,
      source: "Avery L7163 EU A4 — 14 per sheet"
    },
    "G_AV_L7167": {
      maker: "Avery (에이버리)", model: "L7167 (EU)", category: "전지(1칸)",
      perPage: 1, colN: 1, rowN: 1,
      labelW: 199.6, labelH: 289.0, colGap: 0,
      pt: 4.00, pb: 4.00, pl: 5.20,
      source: "Avery L7167 EU A4 — 1 per sheet"
    },
    "G_AV_5662": {
      maker: "Avery (에이버리)", model: "5662 (분류)", category: "분류표기",
      perPage: 66, colN: 3, rowN: 22,
      labelW: 63.5, labelH: 12.7, colGap: 3.175,
      pt: 8.80, pb: 8.80, pl: 6.58,
      source: "Avery 5662 US standard — 66 per sheet"
    },

    /* ═══════════════════ 업계 표준 (범용) ═══════════════════ */
    "G_STD_8": {
      maker: "업계 표준 (범용)", model: "대형 8칸", category: "대형 소포",
      perPage: 8, colN: 2, rowN: 4,
      labelW: 94.0, labelH: 64.0, colGap: 2,
      pt: 20.50, pb: 20.50, pl: 10.00,
      source: "업계 표준 8칸 — 94×64mm"
    },
    "G_STD_10": {
      maker: "업계 표준 (범용)", model: "표준 10칸", category: "대형",
      perPage: 10, colN: 2, rowN: 5,
      labelW: 99.1, labelH: 57.0, colGap: 2,
      pt: 6.00, pb: 6.00, pl: 4.90,
      source: "업계 표준 10칸 — 99.1×57mm"
    },
    "G_STD_12": {
      maker: "업계 표준 (범용)", model: "표준 12칸", category: "주소호환",
      perPage: 12, colN: 2, rowN: 6,
      labelW: 99.1, labelH: 42.3, colGap: 2,
      pt: 21.60, pb: 21.60, pl: 4.90,
      source: "업계 표준 12칸 — 99.1×42.3mm"
    },
    "G_STD_16": {
      maker: "업계 표준 (범용)", model: "표준 16칸", category: "주소호환",
      perPage: 16, colN: 2, rowN: 8,
      labelW: 99.1, labelH: 33.9, colGap: 2,
      pt: 12.90, pb: 12.90, pl: 4.90,
      source: "업계 표준 16칸 — 99.1×33.9mm"
    },
    "G_STD_21": {
      maker: "업계 표준 (범용)", model: "표준 21칸", category: "주소호환",
      perPage: 21, colN: 3, rowN: 7,
      labelW: 63.5, labelH: 38.1, colGap: 2,
      pt: 15.15, pb: 15.15, pl: 7.75,
      source: "업계 표준 21칸 — 63.5×38.1mm"
    },
    "G_STD_24": {
      maker: "업계 표준 (범용)", model: "표준 24칸", category: "주소호환",
      perPage: 24, colN: 3, rowN: 8,
      labelW: 64.0, labelH: 33.9, colGap: 2,
      pt: 12.90, pb: 12.90, pl: 7.00,
      source: "업계 표준 24칸 — 64×33.9mm"
    },
    "G_STD_30": {
      maker: "업계 표준 (범용)", model: "표준 30칸", category: "바코드",
      perPage: 30, colN: 3, rowN: 10,
      labelW: 66.0, labelH: 25.4, colGap: 2,
      pt: 21.50, pb: 21.50, pl: 4.00,
      source: "업계 표준 30칸 — 66×25.4mm"
    },
    "G_STD_32": {
      maker: "업계 표준 (범용)", model: "표준 32칸", category: "바코드/소형",
      perPage: 32, colN: 4, rowN: 8,
      labelW: 48.5, labelH: 25.4, colGap: 2,
      pt: 46.90, pb: 46.90, pl: 5.00,
      source: "업계 표준 32칸 — 48.5×25.4mm"
    },
    "G_STD_40": {
      maker: "업계 표준 (범용)", model: "표준 40칸", category: "바코드/소형",
      perPage: 40, colN: 4, rowN: 10,
      labelW: 47.0, labelH: 26.9, colGap: 2,
      pt: 14.00, pb: 14.00, pl: 8.00,
      source: "업계 표준 40칸 — 47×26.9mm"
    },
    "G_STD_65": {
      maker: "업계 표준 (범용)", model: "표준 65칸", category: "분류/소형",
      perPage: 65, colN: 5, rowN: 13,
      labelW: 38.1, labelH: 21.2, colGap: 2,
      pt: 10.70, pb: 10.70, pl: 5.75,
      source: "업계 표준 65칸 — 38.1×21.2mm"
    },
    "G_STD_A4_1": {
      maker: "업계 표준 (범용)", model: "전지(A4 전면)", category: "전지(1칸)",
      perPage: 1, colN: 1, rowN: 1,
      labelW: 210.0, labelH: 297.0, colGap: 0,
      pt: 0, pb: 0, pl: 0,
      source: "A4 전면 1칸 라벨"
    },

    /* ═══════════════════ 정부문서화일라벨 (관공서 문서철/바인더 라벨) ═══════════════════
       실제 시판 규격(폼텍 LQ-3629 등)은 한 장(A4)에 "제목·연도·부서·분류번호(측면)·
       분류번호(옆면)" 5가지 크기가 서로 다른 라벨이 함께 배치된 한 세트로, 그 세트가
       한 장에 2개 들어있다. 이 앱의 일반라벨 엔진은 "같은 크기 칸이 균일하게 반복되는
       그리드"만 지원하므로, 5종류를 한 장에 그대로(원본 다이컷 그대로) 재현하려면
       칸마다 다른 크기/좌표를 배치할 수 있는 별도의 "혼합 레이아웃" 엔진이 필요하다
       (현재 미지원 — 채팅 답변 참고). 대신 여기서는 5종류를 각각 "그 크기만 균일
       반복"하는 낱개 템플릿으로 제공한다 — 시판 원본 다이컷 시트에 정확히 맞춰
       인쇄하는 용도가 아니라, 해당 규격 그대로 낱장 라벨지에 필요한 만큼 인쇄하는
       용도에 적합하다.
       ── "분류번호(옆면)"은 가로 16mm로 매우 좁아 가로쓰기 글자가 들어가지 않으므로
          실물도 항상 세로쓰기로 인쇄한다 — 이 앱의 "세로쓰기(글자 방향)" 기능과 함께
          쓰도록 의도된 규격이다(사이드바 "텍스트" 항목의 "글자 방향" 참고). */
    "G_GOV_TITLE": {
      maker: "정부문서화일(공통규격)", model: "제목", category: "정부문서화일",
      perPage: 7, colN: 1, rowN: 7,
      labelW: 123.0, labelH: 36.5, colGap: 0,
      pt: 20.75, pb: 20.75, pl: 43.50,
      source: "정부문서화일라벨 '제목' 칸 규격(폼텍 LQ-3629 계열) — 123×36.5mm. 제조사별로 122×37mm 등 1mm 내외 차이가 있을 수 있음"
    },
    "G_GOV_YEAR": {
      maker: "정부문서화일(공통규격)", model: "연도", category: "정부문서화일",
      perPage: 36, colN: 3, rowN: 12,
      labelW: 63.5, labelH: 23.5, colGap: 2,
      pt: 7.50, pb: 7.50, pl: 7.75,
      source: "정부문서화일라벨 '연도' 칸 규격(폼텍 LQ-3629 계열) — 63.5×23.5mm"
    },
    "G_GOV_YEAR": {
      maker: "정부문서화일(공통규격)", model: "측면짱돌", category: "정부문서화일",
      perPage: 6, colN: 2, rowN: 3,
      labelW: 91.31, labelH: 77.69, colGap: 11, rowGap: 2.5,
      pt: 13.5, pb: 13.5, pl: 7,
      source: "정부문서화일라벨 '연도' 칸 규격(폼텍 LQ-3629 계열) — 91.31×77.69mm"
    },
    "G_GOV_DEPT": {
      maker: "정부문서화일(공통규격)", model: "부서", category: "정부문서화일",
      perPage: 18, colN: 2, rowN: 9,
      labelW: 84.5, labelH: 30.0, colGap: 2,
      pt: 13.50, pb: 13.50, pl: 19.50,
      source: "정부문서화일라벨 '부서' 칸 규격(폼텍 LQ-3629 계열) — 84.5×30.0mm"
    },
    "G_GOV_CLASS_SIDE": {
      maker: "정부문서화일(공통규격)", model: "분류번호(측면)", category: "정부문서화일",
      perPage: 20, colN: 2, rowN: 10,
      labelW: 93.0, labelH: 28.0, colGap: 2,
      pt: 8.50, pb: 8.50, pl: 11.00,
      source: "정부문서화일라벨 '분류번호(측면)' 칸 규격(폼텍 LQ-3629 계열) — 93×28mm"
    },
    "G_GOV_CLASS_SPINE": {
      maker: "정부문서화일(공통규격)", model: "분류번호(옆면) · 세로형", category: "정부문서화일(세로형)",
      perPage: 2, colN: 2, rowN: 1,
      labelW: 16.0, labelH: 257.0, colGap: 8,
      pt: 20.00, pb: 20.00, pl: 85.00,
      suggestVertical: true,
      source: "정부문서화일라벨(문서철 바인더 옆면·책등) '분류번호(옆면)' 규격 — 16×257mm, 실물 시트 기준 1장에 2개. 폭이 좁아 가로쓰기가 불가능해 세로쓰기 전용 — 좌측 '글자 방향'에서 '세로'를 선택하세요"
    }
  };

  global.GENERAL_TEMPLATE_CONFIGS = GENERAL_TEMPLATE_CONFIGS;

})(window);
