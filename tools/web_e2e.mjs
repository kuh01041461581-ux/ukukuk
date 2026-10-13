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
await browser.close();
if (!ok) process.exit(1);
