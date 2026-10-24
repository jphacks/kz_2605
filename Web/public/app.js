// おとぎ配信 視聴ページ。Firebase Realtime Database からリアルタイムに受け取り、投票・コメントを書き込む。
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js";
import {
  getDatabase, ref, onValue, onChildAdded, set, push, query, limitToLast,
  serverTimestamp, onDisconnect, off,
} from "https://www.gstatic.com/firebasejs/10.12.2/firebase-database.js";
import { DATABASE_URL, DEFAULT_ROOM } from "./config.js";

// ── 準備 ────────────────────────────────────
const params = new URLSearchParams(location.search);
const room = (params.get("room") || DEFAULT_ROOM).replace(/[.#$\[\]\/\\]/g, "");
const db = getDatabase(initializeApp({ databaseURL: DATABASE_URL }));
const roomRef = (path) => ref(db, `rooms/${room}/${path}`);
const $ = (id) => document.getElementById(id);

function load(key, fallback) {
  try { return localStorage.getItem(key) ?? fallback; } catch { return fallback; }
}
function save(key, value) {
  try { localStorage.setItem(key, value); } catch { /* プライベートモードなど */ }
}

const viewerId = load("otogi.viewerId", null) || (() => {
  const id = "v" + Math.random().toString(36).slice(2, 10) + Date.now().toString(36);
  save("otogi.viewerId", id);
  return id;
})();

// ── Firebase の内容を手元の data に写して描画する ─────────
// 視聴者が書く場所（投票・コメント・視聴者数）は audience/ の下にまとめる（Unity はここだけをストリーミングで受け取る）
const data = {};
let connected = false;
let serverOffset = 0;   // 端末の時計とサーバーの時計の差
const serverNow = () => Date.now() + serverOffset;
onValue(ref(db, ".info/serverTimeOffset"), (snap) => { serverOffset = snap.val() || 0; });

// 視聴者数：つながっている間だけ audience/viewers に自分を書き、切断で自動的に消える
let disconnectedSince = Date.now();
onValue(ref(db, ".info/connected"), (snap) => {
  connected = !!snap.val();
  if (!connected) disconnectedSince = Date.now();
  updateLive();
  if (!connected) return;
  const me = roomRef(`audience/viewers/${viewerId}`);
  onDisconnect(me).remove().then(() => set(me, serverTimestamp()));
});

for (const [key, path] of [["viewers", "audience/viewers"], ["frame", "frame"], ["state", "state"], ["ending", "ending"], ["choice", "choice"]]) {
  onValue(roomRef(path), (snap) => {
    data[key] = snap.val();
    if (key === "choice") watchVotes();
    render(key);
  });
}

// 今の選択肢の投票だけを見る
let votesRef = null;
let votesFor = null;
function watchVotes() {
  const id = data.choice?.id ?? null;
  if (id === votesFor) return;
  if (votesRef) off(votesRef);
  votesFor = id;
  data.votes = {};
  votesRef = id ? roomRef(`audience/votes/${id}`) : null;
  if (votesRef) onValue(votesRef, (snap) => { data.votes = { [id]: snap.val() || {} }; render("votes"); });
}

function get(path) {
  return path.split("/").filter(Boolean).reduce((n, s) => (n && typeof n === "object" ? n[s] : undefined), data);
}

function render(top) {
  switch (top) {
    case "viewers": $("viewer-count").textContent = Object.keys(data.viewers || {}).length; break;
    case "frame": renderFrame(); break;
    case "state": renderState(); updateLive(); break;
    case "ending": renderEnding(); break;
    case "choice":
    case "votes": renderChoice(); break;
  }
}

// ── VR視点の画像 ─────────────────────────────
let firstFrameT = undefined;   // ページを開いたときに残っていた画像の時刻（古いかもしれない）
let lastArrival = 0;           // ページを開いてから新しい画像が届いた時刻
function renderFrame() {
  const f = data.frame;
  if (!f || !f.jpg) return;
  $("frame").src = "data:image/jpeg;base64," + f.jpg;
  if (firstFrameT === undefined) firstFrameT = f.t;
  else if (f.t !== firstFrameT) lastArrival = Date.now();
  updateLive();
}

const STALL_MS = 15000;          // 配信側の画像がこれだけ届かなければ、通信が途絶えたとみなす
const OFFLINE_MS = 8000;         // 見ている側がこれだけ切れていれば、通信が途絶えたとみなす
let everConnected = false;
// 新しい画像が届いていればその到着からの時間、まだなら残っていた画像の時刻からの時間（配信側の時計のずれに強くする）
const frameAge = () => (lastArrival ? Date.now() - lastArrival : data.frame?.t ? serverNow() - data.frame.t : Infinity);

/**
 * LIVE表示と「ご視聴ありがとうございました。」。次のどれかで表示する。
 *  ・配信が閉じた（Unity から終了の合図 state.phase = "ended"）
 *  ・配信側の通信が途絶えた（画像が STALL_MS 届かない。配信が一度も始まっていなければ「準備中」のまま）
 *  ・見ている側の通信が途絶えた（Firebase から OFFLINE_MS 以上切れている）
 */
function updateLive() {
  if (connected) everConnected = true;
  const closed = data.state?.phase === "ended";
  const hostLost = !closed && data.frame?.t != null && frameAge() > STALL_MS;
  const selfLost = !connected && everConnected && Date.now() - disconnectedSince > OFFLINE_MS;
  const ended = closed || hostLost || selfLost;
  const fresh = connected && !ended && frameAge() < STALL_MS;

  const badge = $("live-badge");
  badge.textContent = selfLost ? "通信が途絶えました" : ended ? "配信終了" : fresh ? "LIVE" : connected ? "準備中" : "接続中…";
  badge.classList.toggle("offline", !fresh);
  $("screen-empty").hidden = fresh || ended;
  $("thanks").hidden = !ended;
  $("thanks-sub").textContent =
    selfLost ? "通信が途絶えました。電波の良い場所で、ページを再読み込みしてください。" :
    closed ? "配信は終了しました。また絵本の世界でお会いしましょう。" :
    "配信が終了したか、通信が途絶えました。また絵本の世界でお会いしましょう。";
  if (ended) $("ending").hidden = true;
  else renderEnding();
}
setInterval(updateLive, 1000);

// ── 場面と字幕 ───────────────────────────────
function renderState() {
  const s = data.state || {};
  $("area-name").textContent = s.areaName || "";
  const sub = $("subtitle");
  sub.replaceChildren();
  if (s.subtitle && s.subtitle.text) {
    if (s.subtitle.speaker) {
      const sp = document.createElement("span");
      sp.className = "speaker";
      sp.textContent = s.subtitle.speaker;
      sub.append(sp, `「${s.subtitle.text}」`);
    } else {
      sub.append(s.subtitle.text);
    }
  } else {
    const idle = document.createElement("span");
    idle.className = "subtitle-idle";
    idle.textContent = s.phase === "title" || !s.phase ? "― 絵本の部屋で、物語が始まるのを待っています ―" : "";
    sub.append(idle);
  }
}

// ── エンディング ─────────────────────────────
function renderEnding() {
  const e = data.ending;
  $("ending").hidden = !e || !$("thanks").hidden;
  if (!e) return;
  $("ending-title").textContent = `『${e.title}』`;
  $("ending-genre").textContent = `${e.id}　${e.genre}`;
}

// ── 選択肢と投票 ─────────────────────────────
let choiceId = null;
let remainingAt = 0;        // remaining を受け取った時刻（カウントダウンの補間用）
let lastRemaining = null;

function remainingSeconds(choice) {
  if (!choice || choice.status !== "open") return 0;
  return Math.max(0, (choice.remaining ?? 0) - (Date.now() - remainingAt) / 1000);
}

function castVote(index) {
  const choice = data.choice;
  if (!choice || choice.status !== "open") return;
  set(roomRef(`audience/votes/${choice.id}/${viewerId}`), index);
}

function renderChoice() {
  const choice = data.choice;
  const card = $("choice");
  if (!choice) { card.hidden = true; choiceId = null; return; }
  if (choiceId !== choice.id) {
    choiceId = choice.id;
    card.hidden = false;
    // スマホでは、選択肢が出たら投票カードを見える位置に
    if (matchMedia("(max-width: 900px)").matches) card.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }
  if (choice.remaining !== lastRemaining) { lastRemaining = choice.remaining; remainingAt = Date.now(); }
  card.hidden = false;

  const open = choice.status === "open";
  const options = choice.options || [];
  const votes = get(`votes/${choice.id}`) || {};
  const counts = options.map(() => 0);
  for (const v of Object.values(votes)) if (counts[v] !== undefined) counts[v]++;
  const total = counts.reduce((a, b) => a + b, 0);
  const mine = votes[viewerId];

  $("choice-question").textContent = choice.question;
  $("choice-options").replaceChildren(...options.map((o, i) => {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "option";
    btn.disabled = !open;
    if (mine === i) btn.classList.add("mine");
    if (!open && choice.result === i) btn.classList.add("winner");
    const pct = total ? Math.round((counts[i] / total) * 100) : 0;
    btn.innerHTML = `<span class="fill" style="width:${pct}%"></span>
      <span class="row"><span class="label"></span><span class="count">${counts[i]}票・${pct}%</span></span>
      <span class="hint"></span>`;
    btn.querySelector(".label").textContent = (mine === i ? "✓ " : "") + o.label;
    btn.querySelector(".hint").textContent = o.hint || "";
    btn.addEventListener("click", () => castVote(i));
    return btn;
  }));

  const note = $("choice-note");
  if (open) {
    note.className = "choice-note";
    note.textContent = "体験者が行動しないまま時間切れになると、投票1位で物語が進みます。コメントに選択肢の名前を書いても1票になります。";
  } else {
    const label = options[choice.result]?.label;
    note.className = "choice-note result";
    note.textContent =
      choice.decidedBy === "viewers" ? `時間切れ！ みんなの投票で「${label}」に決まりました` :
      choice.decidedBy === "player" ? `体験者が「${label}」を選びました` :
      "時間切れ… 物語は進みませんでした";
  }
  tick();
}

function tick() {
  const choice = data.choice;
  if (!choice) return;
  const r = remainingSeconds(choice);
  const limit = choice.timeLimit || 1;
  $("choice-timer").textContent = choice.status === "open" ? `のこり ${Math.ceil(r)} 秒` : "しめきり";
  const bar = $("choice-timebar");
  bar.firstElementChild.style.width = `${(r / limit) * 100}%`;
  bar.classList.toggle("low", r / limit < 0.3);
}
setInterval(tick, 250);

// ── コメント ─────────────────────────────────
const nameInput = $("comment-name");
nameInput.value = load("otogi.name", "");
nameInput.addEventListener("change", () => save("otogi.name", nameInput.value.trim()));

/** コメントに選択肢の名前が入っていたら、その選択肢の番号を返す（長い名前を優先）。 */
function voteFromText(text) {
  const choice = data.choice;
  if (!choice || choice.status !== "open") return -1;
  let best = -1, bestLen = 0;
  (choice.options || []).forEach((o, i) => {
    if (o.label && text.includes(o.label) && o.label.length > bestLen) { best = i; bestLen = o.label.length; }
  });
  return best;
}

$("comment-form").addEventListener("submit", (e) => {
  e.preventDefault();
  const input = $("comment-text");
  const text = input.value.trim();
  if (!text) return;
  const vote = voteFromText(text);
  const name = nameInput.value.trim() || "名無しの村人";
  push(roomRef("audience/comments"), { name, text, vid: viewerId, t: serverTimestamp() });
  if (vote >= 0) castVote(vote);
  input.value = "";
});

// 最新50件を表示し、ページを開いたあとに来たコメントは画面の上にも流す
const openedAt = Date.now();
onChildAdded(query(roomRef("audience/comments"), limitToLast(50)), (snap) => {
  const c = snap.val();
  addComment(c, typeof c?.t === "number" && c.t > openedAt - 2000);
});

function addComment(c, live) {
  if (!c || !c.text) return;
  const li = document.createElement("li");
  const name = document.createElement("span");
  name.className = "name";
  name.textContent = c.name || "名無しの村人";
  li.append(name, c.text);
  const list = $("comment-list");
  list.append(li);
  while (list.children.length > 100) list.firstElementChild.remove();
  list.scrollTop = list.scrollHeight;
  if (live) flyOnScreen(c.text);
}

function flyOnScreen(text) {
  const layer = $("screen-danmaku");
  const span = document.createElement("span");
  span.textContent = text;
  span.style.top = `${Math.floor(Math.random() * 6) * 15 + 4}%`;
  span.style.animationDuration = `${7 + Math.random() * 2}s`;
  span.addEventListener("animationend", () => span.remove());
  layer.append(span);
}

// ── スマホで見る（QRコード） ─────────────────────
$("share-button").addEventListener("click", () => {
  const panel = $("share");
  panel.hidden = !panel.hidden;
  if (panel.hidden || panel.dataset.ready) return;
  panel.dataset.ready = "1";
  const url = location.href;
  $("share-url").textContent = url;
  if (window.QRCode) new window.QRCode($("share-qr"), { text: url, width: 180, height: 180 });
});
