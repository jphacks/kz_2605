// 視聴ページのデータのやり取り。サーバー役のPC（server.js）か Firebase のどちらにもつなげる。
//  ・Firebase Hosting（*.web.app / *.firebaseapp.com）で開いたとき、または ?backend=firebase のとき → Firebase
//  ・それ以外（サーバー役のPCから開いたとき）→ そのPC（同じ場所の /rooms/...）
// どちらも同じ形で使える：listen / onChildAdded / set / push / onConnected / serverNow / registerViewer

import { DATABASE_URL } from "./config.js";

export function useFirebase() {
  const q = new URLSearchParams(location.search).get("backend");
  if (q) return q === "firebase";
  return /\.(web\.app|firebaseapp\.com)$/.test(location.hostname);
}

export async function connect(room) {
  return useFirebase() ? connectFirebase(room) : connectServer(room);
}

// ── サーバー役のPC ─────────────────────────────

function connectServer(room) {
  let tree = {};
  let connected = false;
  let offset = 0;
  let ready = false;
  const listeners = new Set();        // { segs, cb }
  const childWatchers = [];           // { segs, limit, cb, seen }
  const connectedCbs = [];

  const split = (p) => p.split("/").filter(Boolean);
  const getAt = (segs) => segs.reduce((n, s) => (n && typeof n === "object" ? n[s] : undefined), tree) ?? null;
  const isPrefix = (a, b) => a.length <= b.length && a.every((s, i) => s === b[i]);

  function setAt(segs, value) {
    if (segs.length === 0) { tree = value && typeof value === "object" ? value : {}; return; }
    let node = tree;
    for (const s of segs.slice(0, -1)) {
      if (node[s] === null || typeof node[s] !== "object") {
        if (value === null) return;
        node[s] = {};
      }
      node = node[s];
    }
    const last = segs[segs.length - 1];
    if (value === null || value === undefined) delete node[last];
    else node[last] = value;
  }

  function changed(segs) {
    for (const l of listeners)
      if (isPrefix(l.segs, segs) || isPrefix(segs, l.segs)) l.cb(getAt(l.segs));
    for (const w of childWatchers) {
      if (!(isPrefix(w.segs, segs) || isPrefix(segs, w.segs))) continue;
      const node = getAt(w.segs) || {};
      let keys = Object.keys(node).sort().filter((k) => !w.seen.has(k));
      if (!w.started) { keys = keys.slice(-w.limit); w.started = true; }
      for (const k of keys) { w.seen.add(k); w.cb(node[k], k); }
    }
  }

  fetch("/info").then((r) => r.json()).then((info) => { offset = info.now - Date.now(); }).catch(() => {});

  let es = null;
  let viewerId = null;
  function open() {
    es = new EventSource(`/rooms/${encodeURIComponent(room)}.json${viewerId ? `?viewer=${encodeURIComponent(viewerId)}` : ""}`);
    const onMessage = (op) => (e) => {
      const msg = JSON.parse(e.data);
      if (!msg) return;
      const segs = split(msg.path || "/");
      if (op === "patch") for (const [k, v] of Object.entries(msg.data || {})) setAt([...segs, ...split(k)], v);
      else setAt(segs, msg.data);
      ready = true;
      changed(segs);
    };
    es.addEventListener("put", onMessage("put"));
    es.addEventListener("patch", onMessage("patch"));
    es.addEventListener("open", () => { connected = true; connectedCbs.forEach((cb) => cb(true)); });
    es.addEventListener("error", () => { connected = false; connectedCbs.forEach((cb) => cb(false)); });
  }

  const write = (method, path, value) => fetch(`/rooms/${encodeURIComponent(room)}/${path}.json`, {
    method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(value),
  }).catch(() => { /* 切断中は再接続を待つ */ });

  return {
    kind: "server",
    registerViewer(id) { viewerId = id; if (es) es.close(); open(); },   // ストリームがつながっている間だけ視聴者として数えられる
    onConnected(cb) { connectedCbs.push(cb); cb(connected); },
    serverNow: () => Date.now() + offset,
    listen(path, cb) {
      const l = { segs: split(path), cb };
      listeners.add(l);
      if (ready) cb(getAt(l.segs));
      return () => listeners.delete(l);
    },
    onChildAdded(path, limit, cb) {
      const w = { segs: split(path), limit, cb, seen: new Set(), started: false };
      childWatchers.push(w);
      if (ready) changed(w.segs);
    },
    set: (path, value) => write("PUT", path, value),
    push: (path, value) => write("POST", path, value),
    async shareUrl() {
      // localhost で開いているときは、スマホから届くアドレスに置き換える
      if (!/^(localhost|127\.)/.test(location.hostname)) return location.href;
      try {
        const info = await (await fetch("/info")).json();
        if (info.urls && info.urls[0]) return info.urls[0] + location.pathname + location.search;
      } catch { /* そのまま */ }
      return location.href;
    },
  };
}

// ── Firebase ─────────────────────────────────

async function connectFirebase(room) {
  const { initializeApp } = await import("https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js");
  const {
    getDatabase, ref, onValue, onChildAdded, set, push, query, limitToLast, serverTimestamp, onDisconnect,
  } = await import("https://www.gstatic.com/firebasejs/10.12.2/firebase-database.js");
  const db = getDatabase(initializeApp({ databaseURL: DATABASE_URL }));
  const roomRef = (path) => ref(db, `rooms/${room}/${path}`);
  let offset = 0;
  onValue(ref(db, ".info/serverTimeOffset"), (snap) => { offset = snap.val() || 0; });

  return {
    kind: "firebase",
    registerViewer(id) {
      // つながっている間だけ audience/viewers に自分を書き、切断で自動的に消える
      onValue(ref(db, ".info/connected"), (snap) => {
        if (!snap.val()) return;
        const me = roomRef(`audience/viewers/${id}`);
        onDisconnect(me).remove().then(() => set(me, serverTimestamp()));
      });
    },
    onConnected(cb) { onValue(ref(db, ".info/connected"), (snap) => cb(!!snap.val())); },
    serverNow: () => Date.now() + offset,
    listen(path, cb) { return onValue(roomRef(path), (snap) => cb(snap.val())); },
    onChildAdded(path, limit, cb) { onChildAdded(query(roomRef(path), limitToLast(limit)), (snap) => cb(snap.val(), snap.key)); },
    set: (path, value) => set(roomRef(path), value),
    push: (path, value) => push(roomRef(path), { ...value, t: serverTimestamp() }),
    async shareUrl() { return location.href; },
  };
}
