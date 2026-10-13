// 개발·시험용: relay/worker.js 를 내 컴퓨터에서 그대로 돌린다 (Cloudflare KV 대신 메모리)
import http from "http";
import w from "../relay/worker.js";

const store = new Map();
const KV = {
  async getWithMetadata(k) { const e = store.get(k); return e ? { value: e.v, metadata: e.m } : { value: null, metadata: null }; },
  async put(k, v, o) { store.set(k, { v, m: o.metadata }); },
};
const port = Number(process.env.PORT || 8787);
http.createServer(async (req, res) => {
  const chunks = [];
  for await (const c of req) chunks.push(c);
  const r = await w.fetch(new Request(`http://localhost:${port}${req.url}`, {
    method: req.method, body: ["GET", "HEAD"].includes(req.method) ? undefined : Buffer.concat(chunks), headers: req.headers }), { KV });
  res.writeHead(r.status, Object.fromEntries(r.headers));
  res.end(Buffer.from(await r.arrayBuffer()));
}).listen(port, () => console.log("relay dev server on", port));
