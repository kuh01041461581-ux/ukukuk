/**
 * 오피넷 전국 평균 유가 중계 서버 (Cloudflare Workers, 무료)
 *
 * 왜 필요한가: 오피넷 API 는 브라우저에서 직접 부를 수 없어(CORS) 공개 중계 서버에 기대면 자주 실패합니다.
 * 이 Worker 를 한 번 만들어 두면 "서무 종합프로그램 → 관외출장 → 거리 계산기 → 오피넷 중계 주소" 에
 * 주소만 넣으면 바로바로 조회됩니다.
 *
 * 만드는 법 (약 5분)
 *  1) https://dash.cloudflare.com 가입 → Workers & Pages → Create → Worker 만들기 → 이 파일 내용을 붙여넣고 Deploy
 *  2) Worker 설정 → Variables and Secrets → 이름 OPINET_KEY, 값 = 오피넷 인증키 (Secret 으로 저장. 이렇게 하면 키가 화면/파일에 남지 않음)
 *  3) 배포된 주소(예: https://opinet-relay.내계정.workers.dev)를 프로그램의 "오피넷 중계 주소" 칸에 붙여넣기
 *
 * 응답은 6시간 캐시합니다(오피넷 평균가는 하루 한 번 갱신되므로 충분하고, 호출 수도 줄어듭니다).
 * 주의: 오피넷이 해외(클라우드) IP 를 막아 두었다면 이 방법도 실패할 수 있습니다. 그때는 아래 오류 메시지가 화면에 그대로 표시됩니다.
 */
const CORS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, OPTIONS',
  'Access-Control-Allow-Headers': '*',
};

export default {
  async fetch(request, env, ctx) {
    if (request.method === 'OPTIONS') return new Response(null, { headers: CORS });
    const url = new URL(request.url);
    const key = env.OPINET_KEY || url.searchParams.get('k') || '';
    if (!key) return json({ error: 'OPINET_KEY 가 설정되지 않았습니다.' }, 400);

    const cache = caches.default;
    const cacheKey = new Request('https://opinet-relay.cache/avgAll');   // 키와 무관한 전국 평균가이므로 하나로 공유
    const hit = await cache.match(cacheKey);
    if (hit) return withCors(hit, 'HIT');

    const target = 'https://www.opinet.co.kr/api/avgAllPrice.do?out=json&certkey=' + encodeURIComponent(key) + '&code=' + encodeURIComponent(key);
    let res;
    try {
      res = await fetch(target, { headers: { 'User-Agent': 'Mozilla/5.0', 'Accept': 'application/json' }, cf: { cacheTtl: 0 } });
    } catch (e) {
      return json({ error: '오피넷 접속 실패: ' + e.message }, 502);
    }
    const text = await res.text();
    let data;
    try { data = JSON.parse(text); } catch (e) { return json({ error: '오피넷 응답이 JSON 이 아닙니다', status: res.status, head: text.slice(0, 120) }, 502); }
    if (!data || !data.RESULT || !Array.isArray(data.RESULT.OIL) || !data.RESULT.OIL.length) {
      return json({ error: '유가 데이터 없음(인증키/요청 확인)', raw: data }, 502);
    }
    const out = new Response(JSON.stringify(data), {
      headers: { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'public, max-age=21600' },
    });
    ctx.waitUntil(cache.put(cacheKey, out.clone()));
    return withCors(out, 'MISS');
  },
};

function json(obj, status = 200) {
  return new Response(JSON.stringify(obj), { status, headers: { ...CORS, 'Content-Type': 'application/json; charset=utf-8' } });
}
function withCors(res, state) {
  const h = new Headers(res.headers);
  Object.entries(CORS).forEach(([k, v]) => h.set(k, v));
  h.set('X-Relay-Cache', state);
  return new Response(res.body, { status: res.status, headers: h });
}
