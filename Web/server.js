// おとぎ配信サーバー（サーバー役のPCで動かす）。Node.js だけで動き、追加のインストールは不要。
//   起動：node server.js            （ポートを変えるとき：node server.js 8081）
//
// Firebase Realtime Database の REST と同じ形で読み書きできるので、Unity 側は URL を変えるだけで使える。
//   読み書き：GET / PUT / PATCH / POST / DELETE  /rooms/{room}/{path}.json
//             PATCH のキーに "state/phase" のような複数階層のパスを書ける（複数パス同時更新）
//   ストリーミング：上と同じURLに Accept: text/event-stream で GET すると、
//             最初に全体（put "/"）、そのあと変更（put / patch）を送り続ける（Firebase と同じ形式）
//             ?viewer={id} を付けたストリームがつながっている間は、その人を視聴者（audience/viewers）として数える
// 視聴ページ（public/）も配信する。データはメモリ上だけに持つ（サーバーを止めると消える）。

"use strict";
const http = require("node:http");
const fs = require("node:fs");
const path = require("node:path");
const os = require("node:os");

const PORT = Number(process.argv[2] || process.env.PORT || 8080);
const PUBLIC_DIR = path.join(__dirname, "public");
const MAX_BODY = 4 * 1024 * 1024;
const MAX_COMMENTS = 500;

const root = {};
const listeners = new Set();   // { segs, res }

// ── データの木 ──────────────────────────────

function getAt(segs) {
  let node = root;
  for (const s of segs) {
    if (node === null || typeof node !== "object" || !(s in node)) return null;
    node = node[s];
  }
  return node;
}

/** 値を置く。null なら消し、空になった親も消す（Firebase と同じ）。 */
function setAt(segs, value) {
  if (segs.length === 0) return;
  const parents = [root];
  let node = root;
  for (const s of segs.slice(0, -1)) {
    if (node[s] === null || typeof node[s] !== "object") {
      if (value === null) return;
      node[s] = {};
    }
    node = node[s];
    parents.push(node);
  }
  const last = segs[segs.length - 1];
  if (value === null || value === undefined) delete node[last];
  else node[last] = value;
  for (let i = parents.length - 1; i > 0; i--) {
    if (Object.keys(parents[i]).length > 0) break;
    delete parents[i - 1][segs[i - 1]];
  }
}

const isPrefix = (a, b) => a.length <= b.length && a.every((s, i) => s === b[i]);

/** segs の値が変わったことを、関係するストリームに伝える。 */
function notify(segs, value) {
  for (const l of listeners) {
    if (isPrefix(l.segs, segs)) {
      sendEvent(l.res, "put", { path: "/" + segs.slice(l.segs.length).join("/"), data: value });
    } else if (isPrefix(segs, l.segs)) {
      sendEvent(l.res, "put", { path: "/", data: getAt(l.segs) });   // 親ごと置き換わった
    }
  }
}

function write(segs, value) {
  setAt(segs, value);
  notify(segs, value === undefined ? null : value);
}

function sendEvent(res, event, data) {
  res.write(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`);
}

// ── ストリーミング ─────────────────────────────

function handleStream(req, res, segs, q) {
  res.writeHead(200, {
    "Content-Type": "text/event-stream; charset=utf-8",
    "Cache-Control": "no-cache",
    Connection: "keep-alive",
    "X-Accel-Buffering": "no",
  });
  const me = { segs, res };
  listeners.add(me);
  sendEvent(res, "put", { path: "/", data: getAt(segs) });

  // 視聴ページのストリームなら、つながっている間だけ視聴者として数える
  const viewer = q.get("viewer") ? safeSeg(q.get("viewer")) : null;
  const viewerSegs = viewer && segs[0] === "rooms" && segs[1] ? ["rooms", segs[1], "audience", "viewers", viewer] : null;
  if (viewerSegs) write(viewerSegs, Date.now());

  const ping = setInterval(() => res.write("event: keep-alive\ndata: null\n\n"), 20000);
  req.on("close", () => {
    clearInterval(ping);
    listeners.delete(me);
    if (viewerSegs && ![...listeners].some((l) => l.viewerSegs && l.viewerSegs.join("/") === viewerSegs.join("/"))) {
      write(viewerSegs, null);
    }
  });
  me.viewerSegs = viewerSegs;
}

// ── 読み書き ──────────────────────────────────

function safeSeg(s) {
  return String(s).replace(/[.#$\[\]\/\\]/g, "").slice(0, 64) || "x";
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    req.on("data", (c) => {
      size += c.length;
      if (size > MAX_BODY) { reject(new Error("too large")); req.destroy(); return; }
      chunks.push(c);
    });
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    req.on("error", reject);
  });
}

function sendJson(res, status, value) {
  res.writeHead(status, { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-cache" });
  res.end(JSON.stringify(value === undefined ? null : value));
}

function applyQuery(value, q) {
  if (q.get("shallow") === "true" && value && typeof value === "object")
    return Object.fromEntries(Object.keys(value).map((k) => [k, true]));
  return value;
}

/** 視聴者が書く場所（rooms/{room}/audience/...）の中身を確かめる。問題があればエラー文を返す。 */
function validate(method, segs, value) {
  if (segs[0] !== "rooms" || segs[2] !== "audience") return null;
  const area = segs[3];
  if (area === "votes") {
    if (segs.length !== 6) return "votes/{choiceId}/{viewerId}";
    if (value === null) return null;
    return Number.isInteger(value) && value >= 0 && value < 6 ? null : "vote must be 0-5";
  }
  if (area === "comments") {
    if (method !== "POST" || segs.length !== 4) return "comments are POST only";
    if (!value || typeof value.text !== "string") return "text required";
    const text = value.text.trim();
    if (text.length < 1 || text.length > 60) return "text must be 1-60 chars";
    if (value.name != null && (typeof value.name !== "string" || value.name.length > 20)) return "name must be <= 20 chars";
    return null;
  }
  if (area === "viewers") return null;
  return "not writable";
}

let pushSeq = 0;
function pushKey() {
  pushSeq = (pushSeq + 1) % 10000;
  return "-" + Date.now().toString().padStart(13, "0") + String(pushSeq).padStart(4, "0");   // 文字列順＝時刻順
}

async function handleRest(req, res, pathname, q) {
  const segs = pathname.slice(1, -".json".length).split("/").filter(Boolean).map((s) => safeSeg(decodeURIComponent(s)));

  if (req.method === "GET") {
    if ((req.headers.accept || "").includes("text/event-stream")) return handleStream(req, res, segs, q);
    return sendJson(res, 200, applyQuery(getAt(segs), q));
  }
  if (segs[0] !== "rooms" || segs.length < 2) return sendJson(res, 403, { error: "rooms only" });

  let value = null;
  if (req.method !== "DELETE") {
    try {
      const body = await readBody(req);
      value = body ? JSON.parse(body) : null;
    } catch {
      return sendJson(res, 400, { error: "invalid json" });
    }
  }

  switch (req.method) {
    case "PUT": {
      const error = validate("PUT", segs, value);
      if (error) return sendJson(res, 400, { error });
      write(segs, value);
      return sendJson(res, 200, value);
    }
    case "PATCH": {
      if (!value || typeof value !== "object") return sendJson(res, 400, { error: "object required" });
      const updates = Object.entries(value).map(([k, v]) => [[...segs, ...k.split("/").filter(Boolean).map(safeSeg)], v]);
      for (const [s, v] of updates) {
        const error = validate("PATCH", s, v);
        if (error) return sendJson(res, 400, { error });   // Firebase と同じく、1つでも駄目なら全部やめる
      }
      for (const [s, v] of updates) write(s, v);
      return sendJson(res, 200, value);
    }
    case "POST": {
      const error = validate("POST", segs, value);
      if (error) return sendJson(res, 400, { error });
      const key = pushKey();
      const isComment = segs[2] === "audience" && segs[3] === "comments";
      const item = isComment
        ? { name: (value.name || "").trim().slice(0, 20) || "名無しの村人", text: value.text.trim(), t: Date.now() }
        : value;
      write([...segs, key], item);
      if (isComment) {
        const keys = Object.keys(getAt(segs) || {}).sort();
        for (const old of keys.slice(0, Math.max(0, keys.length - MAX_COMMENTS))) write([...segs, old], null);
      }
      return sendJson(res, 200, { name: key });
    }
    case "DELETE": {
      if (segs[2] === "audience") return sendJson(res, 403, { error: "not deletable" });
      write(segs, null);
      return sendJson(res, 200, null);
    }
    default:
      return sendJson(res, 405, { error: "method not allowed" });
  }
}

// ── 視聴ページ ────────────────────────────────

const TYPES = {
  ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8",
  ".png": "image/png", ".jpg": "image/jpeg", ".svg": "image/svg+xml", ".ico": "image/x-icon", ".json": "application/json",
};

function serveStatic(res, pathname) {
  const rel = pathname === "/" ? "index.html" : decodeURIComponent(pathname).replace(/^\/+/, "");
  const file = path.normalize(path.join(PUBLIC_DIR, rel));
  if (!file.startsWith(PUBLIC_DIR)) { res.writeHead(403); res.end(); return; }
  fs.readFile(file, (err, data) => {
    if (err) { res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" }); res.end("not found"); return; }
    res.writeHead(200, { "Content-Type": TYPES[path.extname(file)] || "application/octet-stream", "Cache-Control": "no-cache" });
    res.end(data);
  });
}

// ── 起動 ────────────────────────────────────

function lanUrls() {
  const urls = [];
  for (const list of Object.values(os.networkInterfaces()))
    for (const a of list || [])
      if (a.family === "IPv4" && !a.internal) urls.push(`http://${a.address}:${PORT}`);
  return urls;
}

const server = http.createServer((req, res) => {
  const url = new URL(req.url, "http://localhost");
  const pathname = url.pathname;
  if (pathname === "/info") return sendJson(res, 200, { urls: lanUrls(), now: Date.now() });
  if (pathname.endsWith(".json") && pathname.startsWith("/rooms")) {
    handleRest(req, res, pathname, url.searchParams).catch((e) => sendJson(res, 500, { error: String(e) }));
    return;
  }
  if (req.method !== "GET") return sendJson(res, 405, { error: "method not allowed" });
  serveStatic(res, pathname);
});
server.keepAliveTimeout = 30000;

server.listen(PORT, "0.0.0.0", () => {
  console.log("");
  console.log("  おとぎ配信サーバーを起動しました");
  console.log("");
  console.log("  このPCで見る      : http://localhost:" + PORT);
  for (const u of lanUrls()) console.log("  スマホ・Questから : " + u);
  console.log("");
  console.log("  Unity の Live Stream Config > Database Url に、上の「スマホ・Questから」のURLを入れてください。");
  console.log("  スマホとQuestは、このPCと同じWi-Fiにつないでください。止めるときは Ctrl+C。");
  console.log("");
});
