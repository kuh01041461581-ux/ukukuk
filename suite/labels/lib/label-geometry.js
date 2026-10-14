/* ══════════════════════════════════════════════════════════════
   라벨(일반 라벨 칸) 좌표 계산 — 순수 함수 모음
   ──────────────────────────────────────────────────────────────
   칸 안에서 이미지를 드래그로 옮기거나 9-핸들로 확대/축소할 때,
   이미지가 칸 밖으로 벗어나 빈 공간이 보이지 않도록 좌표/배율을
   제한(clamp)하는 계산만 모아둔 파일. DOM에 의존하지 않으므로
   Node 테스트에서 바로 검증할 수 있다.
   general-labels.js는 이 파일을 <script> 태그로 먼저 불러온 뒤
   window.LabelGeometry를 사용한다.
   ══════════════════════════════════════════════════════════════ */
(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) {
    module.exports = api;         // Node (테스트 러너 등)
  } else {
    root.LabelGeometry = api;     // 브라우저 전역
  }
})(typeof globalThis !== "undefined" ? globalThis : this, function () {

  /** 드래그로 옮긴 오프셋(x, y)이 칸 밖으로 이미지가 벗어나 빈 공간이
   *  보이지 않도록 제한한다. 가로/세로 배율이 서로 다를 수 있으므로
   *  x는 scaleX, y는 scaleY 기준으로 각각 계산한다.
   *  rect: 드래그 시작 시점의 이미지 렌더 크기 { width, height } */
  function clampImgOffset(x, y, scaleX, scaleY, rect) {
    const maxX = Math.max(0, (scaleX - 1) * rect.width  / 2);
    const maxY = Math.max(0, (scaleY - 1) * rect.height / 2);
    return {
      x: Math.min(maxX, Math.max(-maxX, x)),
      y: Math.min(maxY, Math.max(-maxY, y)),
    };
  }

  /** scaleX/scaleY 값을 9-핸들 조절 허용 범위(min~max) 안으로 잘라내고
   *  부동소수점 오차 방지를 위해 소수점 3자리로 반올림한다. */
  function clampImgScale(s, min = 0.4, max = 3) {
    return Math.round(Math.min(max, Math.max(min, s)) * 1000) / 1000;
  }

  return { clampImgOffset, clampImgScale };
});
