// 童話配信 視聴ページ。Firebase Realtime Database からリアルタイムに受け取り、投票・コメントを書き込む。
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

// Firebase は手元に残っている値があると、このファイルを読み込んでいる途中でも通知してくる。
// 描画はファイルの読み込みが終わってから行う（後ろで宣言している変数をまだ使えないため）
const afterLoad = (fn) => queueMicrotask(fn);

// 視聴者数：つながっている間だけ audience/viewers に自分を書き、切断で自動的に消える
let disconnectedSince = Date.now();
onValue(ref(db, ".info/connected"), (snap) => {
  connected = !!snap.val();
  if (!connected) disconnectedSince = Date.now();
  afterLoad(updateLive);
  if (!connected) return;
  const me = roomRef(`audience/viewers/${viewerId}`);
  onDisconnect(me).remove().then(() => set(me, serverTimestamp()));
});

for (const [key, path] of [["viewers", "audience/viewers"], ["frame", "frame"], ["state", "state"], ["ending", "ending"], ["choice", "choice"]]) {
  onValue(roomRef(path), (snap) => {
    data[key] = snap.val();
    if (key === "choice") watchVotes();
    afterLoad(() => render(key));
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
let sawLive = false;             // このページを開いてから、配信中の映像を一度でも受け取ったか
let waiting = false;             // 配信が始まるのを待っている（待ち画面を出している）
let commented = false;           // このページで自分が一度でもコメントしたか（最初のコメントのきっかけを消す）
// 新しい画像が届いていればその到着からの時間、まだなら残っていた画像の時刻からの時間（配信側の時計のずれに強くする）
const frameAge = () => (lastArrival ? Date.now() - lastArrival : data.frame?.t ? serverNow() - data.frame.t : Infinity);

/**
 * LIVE表示と「ご視聴ありがとうございました。」。次のどれかで表示する。
 *  ・配信が閉じた（Unity から終了の合図 state.phase = "ended"）
 *  ・配信側の通信が途絶えた（画像が STALL_MS 届かない）
 *  ・見ている側の通信が途絶えた（Firebase から OFFLINE_MS 以上切れている）
 * 配信が始まる前（このページで配信中の映像をまだ受け取っていない）は、前回の配信のデータが残っていても
 * 「終了」とはせず、待ち画面「ちょっと待ってね」を出しておく。
 */
function updateLive() {
  if (connected) everConnected = true;
  const closed = data.state?.phase === "ended";
  const hostLost = !closed && data.frame?.t != null && frameAge() > STALL_MS;
  const selfLost = !connected && everConnected && Date.now() - disconnectedSince > OFFLINE_MS;
  const live = connected && !closed && frameAge() < STALL_MS;
  if (live) sawLive = true;
  const ended = selfLost || (sawLive && (closed || hostLost));
  waiting = !sawLive && !ended;
  $("feedback").hidden = waiting || (!data.ending && !closed);
  const fresh = live && !ended;

  const badge = $("live-badge");
  badge.textContent = selfLost ? "通信が途絶えました" : ended ? "配信終了" : fresh ? "LIVE" : waiting && connected ? "まもなく配信" : connected ? "準備中" : "接続中…";
  badge.classList.toggle("offline", !fresh);
  $("screen-empty").hidden = fresh || ended;
  $("waiting-main").textContent = waiting ? "ちょっと待ってね" : "映像を待っています…";
  $("waiting-sub").replaceChildren(...(
    !connected ? ["配信につないでいます…"] :
    waiting ? ["まもなく配信がはじまります。", document.createElement("br"), "体験者が絵本をひらくと、ここに映像が映ります。"] :
    ["通信が少し不安定です。このままお待ちください。"]));
  $("waiting-chips").hidden = !(waiting && connected);
  $("comment-hint").hidden = !waiting || commented;
  $("comment-text").placeholder = waiting ? "はじまるまで、ひとことどうぞ" : "コメントする（選択肢の名前で1票）";
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
  renderSubtitle();
}

// ── セリフ（字幕と音声） ─────────────────────
// VR が読み始めたセリフを1行ずつ受け取る（rooms/{room}/lines）。上書きの字幕（state.subtitle）と違い、
// 回線が遅くても短いセリフが抜けないので、届いていればこちらを字幕に使い、音声がオンなら VR と同じ音声を流す
let currentLine = null;
let linesSeen = false;      // この VR はセリフを1行ずつ送ってくる（古いビルドなら state.subtitle を使う）
let lineTimer = null;
let soundOn = false;
let voice = null;

onChildAdded(query(roomRef("lines"), limitToLast(1)), (snap) => showLine(snap.val()));

function showLine(line) {
  if (!line || !line.text) return;
  const age = Math.max(0, serverNow() - (line.t || 0));
  const dur = (line.dur || 2) * 1000;
  if (age > dur + 3000) return;   // ページを開く前に読み終わっていたセリフは出さない
  linesSeen = true;
  currentLine = line;
  clearTimeout(lineTimer);
  lineTimer = setTimeout(() => { if (currentLine === line) { currentLine = null; renderSubtitle(); } }, Math.max(800, dur - age) + 300);
  renderSubtitle();
  playVoice(line);
}

function playVoice(line) {
  if (!soundOn || !line.id) return;
  voice ||= new Audio();
  voice.pause();
  voice.src = `voice/${encodeURIComponent(line.id)}.mp3`;
  voice.play().catch(() => { /* 音声ファイルが無い・再生できないときは字幕だけ */ });
}

// 無音の短い WAV（タップしたときに一度鳴らして、以降の再生を許可してもらう）
function silentWav() {
  const n = 800, bytes = new Uint8Array(44 + n * 2), v = new DataView(bytes.buffer);
  const str = (o, s) => [...s].forEach((c, i) => v.setUint8(o + i, c.charCodeAt(0)));
  str(0, "RIFF"); v.setUint32(4, 36 + n * 2, true); str(8, "WAVEfmt "); v.setUint32(16, 16, true);
  v.setUint16(20, 1, true); v.setUint16(22, 1, true); v.setUint32(24, 8000, true); v.setUint32(28, 16000, true);
  v.setUint16(32, 2, true); v.setUint16(34, 16, true); str(36, "data"); v.setUint32(40, n * 2, true);
  return "data:audio/wav;base64," + btoa(String.fromCharCode(...bytes));
}

$("sound-button").addEventListener("click", () => {
  soundOn = !soundOn;
  const button = $("sound-button");
  button.setAttribute("aria-pressed", String(soundOn));
  button.textContent = soundOn ? "🔊 音声オン" : "🔇 音声オフ";
  voice ||= new Audio();
  if (!soundOn) { voice.pause(); return; }
  // タップの中で一度再生しておく（スマホは操作のあとでないと音を出せない）。読み上げ中ならそのセリフを流す
  if (currentLine && currentLine.id) playVoice(currentLine);
  else { voice.src = silentWav(); voice.play().catch(() => {}); }
});

function renderSubtitle() {
  const s = data.state || {};
  const line = currentLine || (!linesSeen && s.subtitle && s.subtitle.text ? s.subtitle : null);
  const sub = $("subtitle");
  sub.replaceChildren();
  if (line) {
    if (line.speaker) {
      const sp = document.createElement("span");
      sp.className = "speaker";
      sp.textContent = line.speaker;
      sub.append(sp, `「${line.text}」`);
    } else {
      sub.append(line.text);
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
  // 待ち画面の間は、前回の配信の結末や感想欄を出さない
  $("ending").hidden = waiting || !e || !$("thanks").hidden;
  $("feedback").hidden = waiting || (!e && data.state?.phase !== "ended");
  if (!e) return;
  $("ending-title").textContent = `『${e.title}』`;
  $("ending-genre").textContent = `${e.id}　${e.genre}`;
}

// ── 選択肢と投票 ─────────────────────────────
let choiceId = null;
let remainingAt = 0;        // remaining を受け取った時刻（カウントダウンの補間用）
let lastRemaining = null;
let renderedOptions = null;

function remainingSeconds(choice) {
  if (!choice || choice.status !== "open") return 0;
  return Math.max(0, (choice.remaining ?? 0) - (Date.now() - remainingAt) / 1000);
}

async function castVote(index) {
  const choice = data.choice;
  if (!choice || choice.status !== "open") return;
  try {
    await set(roomRef(`audience/votes/${choice.id}/${viewerId}`), index);
  } catch {
    $("choice-note").textContent = "投票を送れませんでした。接続を確認して、もう一度タップしてください。";
  }
}

function renderChoice() {
  const choice = data.choice;
  const card = $("choice");
  if (!choice) {
    card.hidden = false;
    card.classList.remove("voting-open");
    document.body.classList.remove("voting-open");
    choiceId = null;
    renderedOptions = null;
    lastRemaining = null;
    $("choice-status").textContent = "現在の投票 · 待機中";
    $("choice-question").textContent = "物語の選択肢が出ると、ここで投票できます";
    $("choice-options").replaceChildren();
    $("choice-timer").textContent = "";
    $("choice-timebar").hidden = true;
    $("choice-note").textContent = "好きな選択肢を1つタップして投票。締め切りまで変更できます。";
    return;
  }
  if (choiceId !== choice.id) {
    choiceId = choice.id;
    card.hidden = false;
    lastRemaining = null;
  }
  if (choice.remaining !== lastRemaining) { lastRemaining = choice.remaining; remainingAt = Date.now(); }
  card.hidden = false;

  const open = choice.status === "open";
  card.classList.toggle("voting-open", open);
  document.body.classList.toggle("voting-open", open);
  $("choice-status").textContent = open ? "現在の投票 · 受付中" : "現在の投票 · 結果";
  $("choice-timebar").hidden = false;
  const options = choice.options || [];
  const votes = get(`votes/${choice.id}`) || {};
  const counts = options.map(() => 0);
  for (const v of Object.values(votes)) if (counts[v] !== undefined) counts[v]++;
  const total = counts.reduce((a, b) => a + b, 0);
  const mine = votes[viewerId];

  $("choice-question").textContent = choice.question;
  const optionKey = JSON.stringify([choice.id, options.map(o => o.label)]);
  const optionRoot = $("choice-options");
  if (renderedOptions !== optionKey) {
    renderedOptions = optionKey;
    optionRoot.replaceChildren(...options.map((o, i) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "option";
      btn.innerHTML = `<span class="fill"></span><span class="row"><span class="label"></span><span class="count"></span></span><span class="hint"></span>`;
      btn.addEventListener("click", () => castVote(i));
      return btn;
    }));
  }
  options.forEach((o, i) => {
    const btn = optionRoot.children[i];
    const pct = total ? Math.round((counts[i] / total) * 100) : 0;
    btn.disabled = !open;
    btn.classList.toggle("mine", mine === i);
    btn.classList.toggle("winner", !open && choice.result === i);
    btn.setAttribute("aria-pressed", String(mine === i));
    btn.querySelector(".fill").style.width = `${pct}%`;
    btn.querySelector(".count").textContent = `${counts[i]}票・${pct}%`;
    btn.querySelector(".label").textContent = (mine === i ? "✓ " : "") + o.label;
    btn.querySelector(".hint").textContent = mine === i ? "投票済み · 締め切りまで変更できます" : "タップして投票";
  });

  const note = $("choice-note");
  if (open) {
    note.className = "choice-note";
    note.textContent = "① 選択肢を1つタップ → ② ✓ が付けば投票完了。時間になると投票1位が物語に反映されます（体験者の選択が優先）。";
  } else {
    const label = options[choice.result]?.label;
    note.className = "choice-note result";
    note.textContent =
      choice.decidedBy === "viewers" ? `時間切れ！ みんなの投票で「${label}」に決まりました` :
      choice.decidedBy === "player" ? `体験者が「${label}」を選びました` :
      label ? `操作がなかったため「${label}」で物語を続けます` : "投票は終了しました";
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

function sendComment(text) {
  const vote = voteFromText(text);
  const name = nameInput.value.trim() || "名無しの村人";
  push(roomRef("audience/comments"), { name, text, vid: viewerId, t: serverTimestamp() });
  if (vote >= 0) castVote(vote);
  commented = true;
  $("comment-hint").hidden = true;
}

$("comment-form").addEventListener("submit", (e) => {
  e.preventDefault();
  const input = $("comment-text");
  const text = input.value.trim();
  if (!text) return;
  sendComment(text);
  input.value = "";
});

// 待ち画面のひとことコメント。タップするとそのままコメントとして送る
const WAITING_PHRASES = ["こんにちは！", "たのしみ！", "わくわく", "がんばれ〜"];
for (const phrase of WAITING_PHRASES) {
  const btn = document.createElement("button");
  btn.type = "button";
  btn.textContent = phrase;
  btn.addEventListener("click", () => sendComment(phrase));
  $("waiting-chips").append(btn);
}

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
  // 新しいものを上（入力欄のすぐ下）に積む
  const list = $("comment-list");
  list.prepend(li);
  while (list.children.length > 100) list.lastElementChild.remove();
  list.scrollTop = 0;
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

// ── リアクション ─────────────────────────────
// key は Unity（LiveStream / DanmakuView）と database.rules.json の種類と合わせる。VRでは絵文字が出せないので word を浮かべる
const REACTIONS = [
  { key: "clap", emoji: "👏", word: "パチパチ" },
  { key: "laugh", emoji: "😆", word: "ワハハ" },
  { key: "wow", emoji: "😲", word: "えっ！" },
  { key: "love", emoji: "💖", word: "すてき" },
  { key: "cheer", emoji: "📣", word: "がんばれ" },
];
const REACTION_COOLDOWN_MS = 100;
let lastReactionAt = 0;

$("reactions").replaceChildren(...REACTIONS.map((r) => {
  const btn = document.createElement("button");
  btn.type = "button";
  btn.className = "reaction";
  btn.innerHTML = `<span class="reaction-emoji" aria-hidden="true">${r.emoji}</span><span class="reaction-word">${r.word}</span>`;
  btn.addEventListener("click", () => {
    if (Date.now() - lastReactionAt < REACTION_COOLDOWN_MS) return;
    lastReactionAt = Date.now();
    push(roomRef("audience/reactions"), { type: r.key, vid: viewerId, t: serverTimestamp() });
  });
  return btn;
}));

// 感想は既存のコメントにも残すので、配信終了後も同じ接続で送れる。
for (const button of document.querySelectorAll("[data-rating]")) {
  button.addEventListener("click", async () => {
    const status = $("feedback-status");
    button.disabled = true;
    try {
      await push(roomRef("audience/comments"), {
        name: nameInput.value.trim() || "名無しの村人",
        text: button.dataset.rating === "good" ? "【物語の感想】👍 よかった" : "【物語の感想】💭 いまひとつ",
        vid: viewerId, ending: data.ending?.id || "", feedback: button.dataset.rating, t: serverTimestamp(),
      });
      for (const other of document.querySelectorAll("[data-rating]")) other.setAttribute("aria-pressed", String(other === button));
      status.textContent = "感想を送りました。ありがとうございます！";
    } catch { status.textContent = "送信できませんでした。接続を確認して、もう一度お試しください。"; }
    finally { button.disabled = false; }
  });
}
$("feedback-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const input = $("feedback-text");
  const text = input.value.trim();
  if (!text) return;
  const button = event.currentTarget.querySelector("button");
  button.disabled = true;
  try {
    await push(roomRef("audience/comments"), {
      name: nameInput.value.trim() || "名無しの村人", text,
      vid: viewerId, ending: data.ending?.id || "", feedback: "comment", t: serverTimestamp(),
    });
    input.value = "";
    $("feedback-status").textContent = "感想コメントを送りました。ありがとうございます！";
  } catch { $("feedback-status").textContent = "送信できませんでした。入力は残っています。もう一度お試しください。"; }
  finally { button.disabled = false; }
});

// ページを開いたあとのリアクション（自分のものも含む）を画面に浮かべる
onChildAdded(query(roomRef("audience/reactions"), limitToLast(10)), (snap) => {
  const r = snap.val();
  if (!r || typeof r.t !== "number" || r.t < openedAt - 2000) return;
  const def = REACTIONS.find((d) => d.key === r.type);
  if (def) floatReaction(def.emoji);
});

function floatReaction(emoji) {
  const layer = $("screen-reactions");
  if (layer.childElementCount > 30) return;
  const span = document.createElement("span");
  span.textContent = emoji;
  span.style.right = `${4 + Math.random() * 14}%`;
  span.style.animationDuration = `${2.2 + Math.random() * 0.8}s`;
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
