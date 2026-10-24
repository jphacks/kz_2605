using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 「童話配信」の配信機能。Firebase Realtime Database を介して、スマホ等の視聴ページとつなぐ。
    /// 送る：場面・字幕・選択肢・エンディング・VR視点の画像（rooms/{room}/state, choice, ending, frame）
    /// 受け取る：視聴者が書く投票・コメント・視聴者数（rooms/{room}/audience/votes, comments, viewers）
    ///
    /// 回線が遅くても遅れが溜まらないよう、
    /// ・送る変更はためておき、1本のリクエスト（複数パス同時更新）にまとめて送る。前の送信が終わるまで次は送らない
    /// ・受け取りは問い合わせを繰り返さず、ストリーミング（つないだままの1本）で受ける
    /// 設定（LiveStreamConfig）が無い・無効のときは Instance が null のままで、ゲームは今まで通り動く。
    /// </summary>
    public class LiveStream : MonoBehaviour
    {
        public static LiveStream Instance { get; private set; }

        public LiveStreamConfig Config { get; private set; }
        public int ViewerCount { get; private set; }
        public bool ChoiceOpen => choiceId != null;
        /// <summary>今の選択肢への投票数（選択肢の並び順）。</summary>
        public int[] Votes { get; private set; } = Array.Empty<int>();
        public int TotalVotes { get; private set; }
        public event Action<string, string> CommentReceived;   // (名前, 本文)
        /// <summary>視聴者のリアクション（"clap" / "laugh" / "wow" / "love" / "cheer"。Webの REACTIONS と合わせる）。</summary>
        public event Action<string> ReactionReceived;

        FirebaseRest rest;
        SpectatorCamera spectator;
        string choiceId;
        float lastRemainingSent = -1f;

        // 送る前にためておく変更（キーはルームからのパス。値 null は削除）
        readonly Dictionary<string, object> pending = new();
        bool sending;
        // 画像は字幕などとは別に送る（いちばん新しい1枚だけ。送信中に撮れたものは次に回し、古いものは捨てる）
        string pendingFrame;
        bool sendingFrame;

        // 視聴者側のデータ（audience）の手元の写し
        readonly Dictionary<string, object> audience = new();
        readonly HashSet<string> seenComments = new();
        readonly HashSet<string> seenReactions = new();

        static readonly Dictionary<string, string> AreaNames = new()
        {
            { "title", "絵本の部屋" }, { "beach", "浜辺" }, { "sea", "海の中" }, { "ryugu", "竜宮城" }, { "village", "村" },
        };

        public static LiveStream Create(Transform head)
        {
            var config = LiveStreamConfig.Load();
            if (config == null || !config.IsValid)
            {
                Debug.Log("[LiveStream] 配信は無効です（JPHACKS2026 > Live Stream Config の Database Url を設定すると有効になります）。");
                return null;
            }
            var go = new GameObject("LiveStream");
            var ls = go.AddComponent<LiveStream>();
            ls.Config = config;
            ls.rest = new FirebaseRest(config.databaseUrl, config.roomId);
            ls.spectator = SpectatorCamera.Create(head, config.frameWidth, config.frameHeight, config.jpgQuality);
            Instance = ls;
            return ls;
        }

        void Start()
        {
            // 前回の配信の選択肢・エンディングが残っていたら消す（投票は選択肢ごとのIDで分かれるので残してよい）
            Queue("choice", null);
            Queue("ending", null);
            StartCoroutine(SendLoop());
            StartCoroutine(FrameLoop());
            StartCoroutine(FrameSendLoop());
            StartCoroutine(rest.Listen("audience", OnAudienceEvent));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>アプリを終えるとき：視聴ページに「配信終了」を伝える（ご視聴ありがとうございました）。</summary>
        void OnApplicationQuit()
        {
            rest.SendBlocking("PATCH", "", Json.Serialize(new Dictionary<string, object>
            {
                { "state/phase", "ended" }, { "state/subtitle", null }, { "state/t", Now }, { "choice", null },
            }));
        }

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ── 送る（ためて、まとめて送る） ─────────────────────

        /// <summary>
        /// 変更をためる。同じ場所への変更は新しいほうで上書きする。
        /// 親（例 "choice"）と子（例 "choice/remaining"）が同時に入らないよう整理する（Firebase の制約）。
        /// </summary>
        void Queue(string path, object value)
        {
            // 親がすでにためてあれば、その中身を書き換える
            foreach (var key in pending.Keys)
            {
                if (!path.StartsWith(key + "/")) continue;
                if (pending[key] is Dictionary<string, object> parent)
                {
                    SetNested(parent, path.Substring(key.Length + 1), value);
                    return;
                }
                pending.Remove(key);   // 親が削除（null）なら、子の変更を優先する
                break;
            }
            // 子がためてあれば、親で丸ごと置き換えるので消す
            var children = new List<string>();
            foreach (var key in pending.Keys) if (key.StartsWith(path + "/")) children.Add(key);
            foreach (var key in children) pending.Remove(key);
            pending[path] = value;
        }

        static void SetNested(Dictionary<string, object> node, string path, object value)
        {
            var segs = path.Split('/');
            for (int i = 0; i < segs.Length - 1; i++)
            {
                if (node.TryGetValue(segs[i], out var child) && child is Dictionary<string, object> d) node = d;
                else { var n = new Dictionary<string, object>(); node[segs[i]] = n; node = n; }
            }
            if (value == null) node.Remove(segs[^1]);
            else node[segs[^1]] = value;
        }

        IEnumerator SendLoop()
        {
            while (true)
            {
                yield return null;
                if (sending || pending.Count == 0) continue;

                var batch = new Dictionary<string, object>(pending);
                pending.Clear();
                sending = true;
                yield return rest.Send("PATCH", "", Json.Serialize(batch), code =>
                {
                    sending = false;
                    // 通信できなかったときだけもう一度送る。サーバーに断られた（4xx）変更は捨てる
                    if (code != 0) return;
                    foreach (var kv in batch)
                        if (!pending.ContainsKey(kv.Key)) Queue(kv.Key, kv.Value);
                });
            }
        }

        public void PublishPhase(string phase)
        {
            Queue("state/phase", phase);
            Queue("state/t", Now);
        }

        public void PublishScene(string areaId)
        {
            AreaNames.TryGetValue(areaId, out var name);
            Queue("state/area", areaId);
            Queue("state/areaName", name ?? areaId);
            Queue("state/t", Now);
        }

        /// <summary>字幕。text が null なら字幕を消す。</summary>
        public void PublishSubtitle(string speaker, string text)
        {
            Queue("state/subtitle", text == null ? null : new Dictionary<string, object> { { "speaker", speaker ?? "" }, { "text", text } });
            Queue("state/t", Now);
        }

        /// <summary>選択肢を視聴者に出す。timeLimit は視聴者の投票の締め切りまでの秒数。</summary>
        public void OpenChoice(ChoiceDef def, float timeLimit)
        {
            choiceId = Now.ToString();
            Votes = new int[def.options.Length];
            TotalVotes = 0;
            lastRemainingSent = timeLimit;
            var options = new List<object>();
            foreach (var o in def.options)
                options.Add(new Dictionary<string, object> { { "label", o.label }, { "hint", o.hint.Replace("\n", "") } });
            Queue("choice", new Dictionary<string, object>
            {
                { "id", choiceId }, { "question", def.question }, { "options", options },
                { "timeLimit", timeLimit }, { "remaining", timeLimit }, { "status", "open" }, { "t", Now },
            });
            CountVotes();
        }

        /// <summary>締め切りまでに投票が無かったとき：体験者の残り時間いっぱいまで投票を延ばす。</summary>
        public void ExtendChoice(float remaining)
        {
            if (choiceId == null) return;
            lastRemainingSent = remaining;
            Queue("choice/timeLimit", remaining);
            Queue("choice/remaining", remaining);
        }

        /// <summary>残り時間をWebに伝える（1秒ごとに間引く）。</summary>
        public void UpdateRemaining(float remaining)
        {
            if (choiceId == null || lastRemainingSent - remaining < 1f) return;
            lastRemainingSent = remaining;
            Queue("choice/remaining", Mathf.Max(0, remaining));
        }

        /// <summary>選択肢を締め切る。result が -1 なら時間切れ。decidedBy は "player" / "viewers" / "timeout"。</summary>
        public void CloseChoice(int result, string decidedBy)
        {
            if (choiceId == null) return;
            Queue("choice/status", "closed");
            Queue("choice/result", result);
            Queue("choice/decidedBy", decidedBy);
            Queue("choice/remaining", 0);
            choiceId = null;
        }

        /// <summary>投票の1位（同数なら先の選択肢）。投票が無ければ -1。</summary>
        public int TopVote()
        {
            int best = -1;
            for (int i = 0; i < Votes.Length; i++)
                if (Votes[i] > 0 && (best < 0 || Votes[i] > Votes[best])) best = i;
            return best;
        }

        public void PublishEnding(EndingId id)
        {
            Queue("ending", new Dictionary<string, object>
            {
                { "id", id.ToString() }, { "title", EndingCatalog.Title(id) }, { "genre", EndingCatalog.Genre(id) }, { "t", Now },
            });
        }

        public void ClearEnding() => Queue("ending", null);

        // ── VR視点の画像 ─────────────────────────────

        IEnumerator FrameLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, Config.frameInterval));
                // 送信待ちの画像があれば撮らない（古い画像を溜めない）
                if (pendingFrame == null) spectator.Capture(jpg => pendingFrame = jpg);
            }
        }

        IEnumerator FrameSendLoop()
        {
            while (true)
            {
                yield return null;
                if (sendingFrame || pendingFrame == null) continue;
                string json = Json.Serialize(new Dictionary<string, object>
                {
                    { "jpg", pendingFrame }, { "w", Config.frameWidth }, { "h", Config.frameHeight }, { "t", Now },
                });
                pendingFrame = null;
                sendingFrame = true;
                yield return rest.Send("PUT", "frame", json, _ => sendingFrame = false);
            }
        }

        // ── 受け取る（audience のストリーミング） ───────────────

        void OnAudienceEvent(string evt, string payload)
        {
            if (evt != "put" && evt != "patch") return;   // keep-alive など
            if (Json.Parse(payload) is not Dictionary<string, object> msg) return;
            string path = msg.TryGetValue("path", out var p) ? (p as string ?? "/") : "/";
            msg.TryGetValue("data", out var data);
            bool initial = path == "/" && evt == "put";

            if (evt == "put") Apply(path, data);
            else if (data is Dictionary<string, object> d)
                foreach (var kv in d) Apply(path.TrimEnd('/') + "/" + kv.Key, kv.Value);

            ViewerCount = audience.TryGetValue("viewers", out var v) && v is Dictionary<string, object> vs ? vs.Count : 0;
            CountVotes();
            ReceiveComments(initial);
            ReceiveReactions(initial);
        }

        void Apply(string path, object value)
        {
            var segs = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length == 0)
            {
                audience.Clear();
                if (value is Dictionary<string, object> all) foreach (var kv in all) audience[kv.Key] = kv.Value;
                return;
            }
            SetNested(audience, string.Join("/", segs), value);
        }

        void CountVotes()
        {
            var counts = new int[Votes.Length];
            int total = 0;
            if (choiceId != null && audience.TryGetValue("votes", out var all) && all is Dictionary<string, object> byChoice
                && byChoice.TryGetValue(choiceId, out var v) && v is Dictionary<string, object> d)
            {
                foreach (var vote in d.Values)
                {
                    if (vote is not double n) continue;
                    int i = (int)n;
                    if (i < 0 || i >= counts.Length) continue;
                    counts[i]++;
                    total++;
                }
            }
            Votes = counts;
            TotalVotes = total;
        }

        /// <summary>まだ出していないリアクションを出す。出したものは手元の写しから消す（たまり続けないように）。</summary>
        void ReceiveReactions(bool initial)
        {
            if (!audience.TryGetValue("reactions", out var all) || all is not Dictionary<string, object> d) return;
            var keys = new List<string>(d.Keys);
            keys.Sort(string.CompareOrdinal);
            foreach (var key in keys)
            {
                var value = d[key];
                d.Remove(key);
                if (!seenReactions.Add(key) || initial) continue;
                if (value is Dictionary<string, object> r && r.TryGetValue("type", out var type) && type is string s)
                    ReactionReceived?.Invoke(s);
            }
        }

        /// <summary>まだ流していないコメントを流す。つないだ直後に届く過去のコメントは流さない。</summary>
        void ReceiveComments(bool initial)
        {
            if (!audience.TryGetValue("comments", out var all) || all is not Dictionary<string, object> d) return;
            var keys = new List<string>(d.Keys);
            keys.Sort(string.CompareOrdinal);   // FirebaseのpushキーはASCII順＝投稿順
            foreach (var key in keys)
            {
                if (!seenComments.Add(key) || initial) continue;
                if (d[key] is not Dictionary<string, object> c) continue;
                string text = c.TryGetValue("text", out var t) ? t as string : null;
                if (string.IsNullOrWhiteSpace(text)) continue;
                string name = c.TryGetValue("name", out var n) ? n as string : null;
                CommentReceived?.Invoke(name, text);
            }
        }
    }
}
