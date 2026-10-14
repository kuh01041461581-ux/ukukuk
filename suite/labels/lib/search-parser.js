/* ══════════════════════════════════════════════════════════════
   검색어 파서 / 매칭 — 순수 함수 모음
   ──────────────────────────────────────────────────────────────
   DOM에 의존하지 않는 로직만 모아둔 파일이라 Node 테스트에서
   그대로 import해서 검증할 수 있다. index.js는 이 파일을
   <script> 태그로 먼저 불러온 뒤 window.SearchParser를 사용한다.

   지원 문법
     - 필드 검색   : "이름:홍길동" / "주소:서울" / "우편번호:06236"
     - OR 검색     : "서울 OR 부산" (대소문자 무관)
     - AND 검색(기본): 공백으로 구분된 여러 키워드 → 모두 포함해야 매치
   ══════════════════════════════════════════════════════════════ */
(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) {
    module.exports = api;       // Node (테스트 러너 등)
  } else {
    root.SearchParser = api;    // 브라우저 전역
  }
})(typeof globalThis !== "undefined" ? globalThis : this, function () {

  /** 검색창 원문을 파싱해 쿼리 객체로 변환.
   *  반환값: null(빈 문자열) | { type:'field', field, term }
   *                          | { type:'or', terms }
   *                          | { type:'and', terms } */
  function parseSearchQuery(raw) {
    const s = String(raw ?? "").trim();
    if (!s) return null;

    const fieldMatch = s.match(/^(이름|name|주소|addr|우편번호|postcode|zip)\s*[:：]\s*(.+)$/i);
    if (fieldMatch) {
      const fKey = fieldMatch[1]
        .replace(/name/i, "이름")
        .replace(/addr(ess)?/i, "주소")
        .replace(/postcode|zip/i, "우편번호");
      return { type: "field", field: fKey.toLowerCase(), term: fieldMatch[2].trim().toLowerCase() };
    }

    if (/\bOR\b/i.test(s)) {
      const terms = s.split(/\bOR\b/i).map(t => t.trim().toLowerCase()).filter(Boolean);
      return { type: "or", terms };
    }

    const terms = s.toLowerCase().split(/\s+/).filter(Boolean);
    return { type: "and", terms };
  }

  /** 라벨의 텍스트 필드({name, addr, zip})가 파싱된 쿼리에 매치하는지 판정.
   *  DOM 접근 없이 순수 문자열만으로 판단하도록 값만 인자로 받는다. */
  function matchesQuery(fields, query) {
    const name = fields?.name ?? "";
    const addr = fields?.addr ?? "";
    const zip  = fields?.zip  ?? "";

    if (!query) return true;

    if (query.type === "field") {
      const hay = query.field === "이름" ? name
                : query.field === "주소" ? addr
                : zip;
      return hay.includes(query.term);
    }
    if (query.type === "or") {
      return query.terms.some(t => name.includes(t) || addr.includes(t) || zip.includes(t));
    }
    /* and (default) */
    return query.terms.every(t => name.includes(t) || addr.includes(t) || zip.includes(t));
  }

  return { parseSearchQuery, matchesQuery };
});
