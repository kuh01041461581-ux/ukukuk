// 웹앱 끝까지 시험: PC 가 올린 암호화 일정을 웹앱이 받아 화면에 그리는지, 아이폰 크기 화면으로 확인한다.
// 사용: BROWSER=webkit|chromium  SITE=http://localhost:8080/ukukuk/  RELAY=http://localhost:8787  OUT=./shots  node tools/web_e2e.mjs
import { createRequire } from "module";
import fs from "fs";
import crypto from "crypto";
const require = createRequire(import.meta.url);
const pw = require(process.env.PW_MODULE || "playwright");

const SITE = process.env.SITE || "http://localhost:8080/ukukuk/";
const RELAY = process.env.RELAY || "http://localhost:8787";
const OUT = process.env.OUT || "shots";
const kind = process.env.BROWSER || "chromium";
fs.mkdirSync(OUT, { recursive: true });

// ── PC 역할: 일정을 DC1 형식으로 암호화해 중계 서버에 올린다
const id = crypto.randomBytes(24).toString("base64url");
const key = crypto.randomBytes(32);
const today = new Date();
const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
const plus = (n) => { const d = new Date(today); d.setDate(d.getDate() + n); return d; };
const items = [
  { id: "t1", text: "PC에서 만든 일정", done: false, pri: 1, updated: Date.now(), date: iso(today), time: "14:00" },
  { id: "t2", text: "도시재생 설명회 참석", done: false, pri: 2, updated: Date.now(), date: iso(plus(2)) },
  { id: "t3", text: "지난 미완료 보고", done: false, pri: 2, updated: Date.now(), date: iso(plus(-3)) },
];
const nonce = crypto.randomBytes(12);
const ck = await crypto.subtle.importKey("raw", key, "AES-GCM", false, ["encrypt"]);
const ct = new Uint8Array(await crypto.subtle.encrypt({ name: "AES-GCM", iv: nonce },
  ck, new TextEncoder().encode(JSON.stringify({ items, memos: {}, deleted: {} }))));
const blob = Buffer.concat([Buffer.from("DC1"), nonce, Buffer.from(ct)]);
const put = await fetch(`${RELAY}/v1/${id}`, { method: "PUT", body: blob, headers: { "if-match": "0" } });
if (!put.ok) throw new Error("중계 서버에 올리지 못함: " + put.status);
const cfg = { relay: RELAY, id, key: key.toString("base64") };

// ── 아이폰 역할: 웹앱을 열어 연결 정보가 있는 상태로 시작
const launchOpts = { args: ["--no-sandbox"] };
if (process.env.CHROMIUM_PATH) launchOpts.executablePath = process.env.CHROMIUM_PATH;
const browser = await pw[kind].launch(launchOpts);
const device = pw.devices["iPhone 14"] || { viewport: { width: 390, height: 664 } };
const ctx = await browser.newContext({ ...device });
const page = await ctx.newPage();
const errors = [];
page.on("pageerror", (e) => errors.push(e.message));
await page.addInitScript((c) => { if (!localStorage.getItem("flutter.sync")) localStorage.setItem("flutter.sync", JSON.stringify(JSON.stringify(c))); }, cfg);
await page.goto(SITE, { waitUntil: "load", timeout: 90000 });
await page.waitForTimeout(7000);
await page.screenshot({ path: `${OUT}/${kind}-1-calendar.png` });
const vp = page.viewportSize();
await page.mouse.click(Math.round(vp.width * 0.85), vp.height - 40); // 설정 탭
await page.waitForTimeout(1500);
await page.screenshot({ path: `${OUT}/${kind}-2-settings.png` });

// 판정: 화면 글자는 DOM 으로 못 읽으니, 앱이 브라우저에 저장한 데이터에 PC 일정이 들어왔는지로 확인
const stored = await page.evaluate(() => localStorage.getItem("flutter.data") || "");
const ok = stored.includes("PC에서 만든 일정") && stored.includes("도시재생 설명회 참석");
console.log(`[${kind}] 웹앱 동기화(암호 해독·병합·저장): ${ok ? "성공" : "실패"} / 화면 오류: ${JSON.stringify(errors.slice(0, 3))}`);

// ── 오프라인: 인터넷이 끊겨도 앱이 열리고 일정이 보이며, 끊긴 동안 만든 일정은 연결되면 PC 쪽으로 올라간다
let offlineOk = true, uploadOk = true;
const sw = await page.evaluate(async () => { const r = await navigator.serviceWorker?.getRegistration(); return !!(r && (r.active || r.waiting || r.installing)); });
await page.waitForTimeout(4000);                       // 서비스 워커가 앱 파일을 보관할 시간
await ctx.setOffline(true);
await page.reload({ waitUntil: "load", timeout: 60000 }).catch(() => { offlineOk = false; });
await page.waitForTimeout(5000);
const hasView = await page.evaluate(() => !!document.querySelector("flt-glass-pane, flutter-view"));
offlineOk = offlineOk && hasView;
await page.screenshot({ path: `${OUT}/${kind}-3-offline.png` });
// 오프라인에서 입력한 일정(앱이 저장하는 곳에 직접 넣어 흉내)
await page.evaluate((d) => {
  const raw = JSON.parse(localStorage.getItem("flutter.data"));
  const data = JSON.parse(raw);
  const t = { id: "off1", text: "오프라인에서 만든 일정", done: false, pri: 2, updated: Date.now() };
  (data[d] ||= { memo: "", todos: [] }).todos.push(t);
  localStorage.setItem("flutter.data", JSON.stringify(JSON.stringify(data)));
}, iso(plus(1)));
await ctx.setOffline(false);
await page.reload({ waitUntil: "load", timeout: 60000 });
await page.waitForTimeout(9000);                       // 시작할 때 바로 동기화한다
const got = await fetch(`${RELAY}/v1/${id}`);
const buf = Buffer.from(await got.arrayBuffer());
const pk = await crypto.subtle.importKey("raw", key, "AES-GCM", false, ["decrypt"]);
const plain = new TextDecoder().decode(await crypto.subtle.decrypt({ name: "AES-GCM", iv: buf.subarray(3, 15) }, pk, buf.subarray(15)));
uploadOk = plain.includes("오프라인에서 만든 일정");
console.log(`[${kind}] 서비스 워커: ${sw ? "등록" : "없음"} / 오프라인 열기: ${offlineOk ? "성공" : "실패"} / 연결 후 PC 쪽으로 올라감: ${uploadOk ? "성공" : "실패"}`);
await browser.close();
if (!ok || !offlineOk || !uploadOk) process.exit(1);
