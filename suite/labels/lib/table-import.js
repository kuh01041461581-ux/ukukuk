/* ══════════════════════════════════════════════════════════════
   표 형태로 가져온 데이터([[셀,셀,…], …])에서 "진짜 표가 시작되는 행"을
   구조적으로 추정하는 순수 함수.
   ──────────────────────────────────────────────────────────────
   왜 필요한가 — 엑셀로 명단을 만들 때 흔히:
     1행: "2024년 발송 명단" 같은 제목(여러 칸이 하나로 병합된 경우가 많음)
     2행: 빈 줄
     3행: 진짜 헤더("이름", "주소", …)
     4행부터: 실제 데이터
   처럼 표 앞에 제목·빈 줄이 몇 줄 끼어있는 경우가 아주 흔하다. 이걸
   그대로 "1행부터 표"로 읽으면 제목 줄이 헤더로 오인되거나, 진짜 헤더
   줄이 데이터 줄로 섞여 들어가 버린다(사용자가 겪은 "병합된 칸이나
   3~4행부터 시작하면 인식을 못 한다"는 문제의 원인).

   판단 기준 — 아주 단순하게, "채워진 칸 개수"가 반복되는 지점을 찾는다.
   제목 줄은 보통 칸이 1개(병합 후 펼쳐졌어도 결국 값 1개)만 채워져 있고,
   빈 줄은 0개, 진짜 표는 여러 행에 걸쳐 "채워진 칸 개수"가 일정하게
   반복된다. 그래서 스캔한 구간에서 가장 자주 나오는(최빈) 채워진 칸
   개수를 "표의 정상 열 개수"로 보고, 그 개수에 처음 도달하는 행을
   시작행으로 돌려준다. 확신이 없으면(최빈값이 1 이하 등) 원래대로 0을
   돌려줘서, 데이터를 함부로 건너뛰어 잘라먹는 일이 없게 한다.

   index.js(주소 라벨)·general-labels.js(일반 라벨)의 엑셀/텍스트 가져오기가
   공용으로 쓴다. DOM에 의존하지 않으므로 Node 테스트에서 바로 검증한다.
   ══════════════════════════════════════════════════════════════ */
(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) {
    module.exports = api;         // Node (테스트 러너 등)
  } else {
    root.TableImport = api;       // 브라우저 전역
  }
})(typeof globalThis !== "undefined" ? globalThis : this, function () {

  function countFilled(row) {
    return (row || []).reduce((n, cell) => {
      const v = cell != null ? String(cell).trim() : "";
      return n + (v !== "" ? 1 : 0);
    }, 0);
  }

  /** 채워진 칸들의 "서로 다른 값 개수"를 센다. 병합된 제목 칸을
   *  fillMergedCellsIntoSheet 같은 함수로 펼치면, 병합 범위 안 모든 칸이
   *  똑같은 값을 갖게 된다 — 즉 칸 수는 많아 보여도 서로 다른 값은 1개뿐
   *  이라는 뜻. 이 값이 1 이하면 "여러 칸처럼 보이지만 사실 제목 한 덩어리"
   *  라고 판단해, 진짜 표(칸마다 서로 다른 값)와 구별하는 데 쓴다. */
  function countDistinct(row) {
    const vals = (row || [])
      .map((cell) => (cell != null ? String(cell).trim() : ""))
      .filter((v) => v !== "");
    return new Set(vals).size;
  }

  /** raw: [[셀,셀,…], …] 형태의 원본 행렬(가공 전 — 빈 행도 그대로 포함).
   *  scanRows: 앞에서부터 몇 행까지 살펴볼지(기본 12행 — 최소 요구사항인
   *  5행보다 넉넉하게 잡아, 제목+빈 줄이 여러 줄 겹쳐도 대응한다).
   *  반환값: 표가 실제로 시작한다고 판단한 0-기반 행 인덱스. 확신이 없으면 0. */
  function detectTableStartRow(raw, scanRows) {
    if (!Array.isArray(raw) || raw.length === 0) return 0;
    const n = Math.min(scanRows || 12, raw.length);
    if (n <= 1) return 0;

    const counts = [];
    for (let i = 0; i < n; i++) counts.push(countFilled(raw[i]));

    /* 최빈값(0은 제외 — 빈 줄은 "표의 정상 형태"로 치지 않는다) */
    const freq = {};
    let modeCount = 0, modeVal = 0;
    for (const c of counts) {
      if (c === 0) continue;
      freq[c] = (freq[c] || 0) + 1;
      if (freq[c] > modeCount || (freq[c] === modeCount && c > modeVal)) {
        modeCount = freq[c]; modeVal = c;
      }
    }
    /* 열이 1개짜리 표(이름만 쭉 나열 등)는 "제목 1줄"과 구조적으로
       구별할 수 없으므로, 섣불리 건너뛰지 않고 0행부터 그대로 쓴다. */
    if (modeVal <= 1) return 0;
    /* 최빈 열 개수와 같거나 그 이상 채워진 첫 행을 표의 시작으로 본다 —
       단, 병합 셀이 펼쳐져 칸 수만 많아 보이고 실제로는 같은 값 하나뿐인
       "제목 행"은 건너뛴다(countDistinct로 걸러냄). */
    for (let i = 0; i < n; i++) {
      if (counts[i] >= modeVal && countDistinct(raw[i]) > 1) return i;
    }
    return 0;
  }

  return { detectTableStartRow };
});
