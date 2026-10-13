// DeskCalendar 중계 서버 (Cloudflare Workers + KV, 무료 플랜으로 충분)
// - 서버에는 PC/폰이 암호화한 데이터 덩어리만 저장됩니다. 암호 키는 서버에 없습니다.
// - 주소의 무작위 id 를 아는 기기만 읽고 쓸 수 있습니다. (id 는 QR 로만 전달)
// 사용법:  GET /v1/<id>  → 저장된 데이터 (없으면 404), 헤더 X-Version 에 버전
//          PUT /v1/<id>  → 저장. If-Match: <버전> 이 다르면 409 (동시에 올린 경우)
const MAX_BYTES = 2 * 1024 * 1024;
const ID_RE = /^[A-Za-z0-9_-]{22,64}$/;

// 웹앱(아이폰 홈 화면 앱)이 브라우저에서 직접 접속할 수 있도록 CORS 를 허용한다
const CORS = {
  "access-control-allow-origin": "*",
  "access-control-allow-methods": "GET, PUT, OPTIONS",
  "access-control-allow-headers": "content-type, if-match",
  "access-control-expose-headers": "X-Version",
};
const json = (obj, status = 200, extra = {}) =>
  new Response(JSON.stringify(obj), { status, headers: { "content-type": "application/json", ...CORS, ...extra } });

export default {
  async fetch(req, env) {
    const url = new URL(req.url);
    if (req.method === "OPTIONS") return new Response(null, { status: 204, headers: CORS });
    if (url.pathname === "/" || url.pathname === "/health") return json({ ok: true, service: "deskcalendar-relay" });
    const m = url.pathname.match(/^\/v1\/([^/]+)$/);
    if (!m || !ID_RE.test(m[1])) return json({ error: "bad id" }, 400);
    if (!env.KV) return json({ error: "KV 바인딩(KV)이 없습니다" }, 500);
    const key = "d:" + m[1];

    if (req.method === "GET") {
      const { value, metadata } = await env.KV.getWithMetadata(key, "arrayBuffer");
      if (value === null) return json({ error: "not found" }, 404);
      return new Response(value, { headers: { "content-type": "application/octet-stream", "X-Version": String(metadata?.v ?? 0), ...CORS } });
    }
    if (req.method === "PUT") {
      const len = Number(req.headers.get("content-length") || 0);
      if (len > MAX_BYTES) return json({ error: "too large" }, 413);
      const body = await req.arrayBuffer();
      if (body.byteLength === 0 || body.byteLength > MAX_BYTES) return json({ error: "bad size" }, 413);
      const cur = await env.KV.getWithMetadata(key, "arrayBuffer");
      const curV = cur.value === null ? 0 : Number(cur.metadata?.v ?? 0);
      const ifMatch = req.headers.get("if-match");
      if (ifMatch !== null && Number(ifMatch) !== curV) return json({ error: "conflict", v: curV }, 409);
      const v = curV + 1;
      await env.KV.put(key, body, { metadata: { v } });
      return json({ v });
    }
    return json({ error: "method not allowed" }, 405);
  },
};
