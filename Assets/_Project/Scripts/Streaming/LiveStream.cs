using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 「おとぎ配信」の配信機能。Firebase Realtime Database を介して、スマホ等の視聴ページとつなぐ。
    /// 送る：場面・字幕・選択肢・エンディング・VR視点の画像（rooms/{room}/state, choice, frame）
    /// 受け取る：投票（votes/{choiceId}）・コメント（comments）・視聴者数（viewers）
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

        FirebaseRest rest;
        SpectatorCamera spectator;
        string choiceId;
        string lastCommentKey;
        bool commentsReady;
        float lastRemainingSent = -1f;

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
            // 前回の配信の選択肢・エンディングが残っていたら消す（コメントは残す）
            Send("PATCH", "", new Dictionary<string, object> { { "choice", null }, { "ending", null } });
            StartCoroutine(PollLoop());
            StartCoroutine(FrameLoop());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Send(string method, string path, object value)
        {
            StartCoroutine(rest.Send(method, path, Json.Serialize(value)));
        }

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ── 送る ─────────────────────────────────

        public void PublishPhase(string phase)
        {
            Send("PATCH", "state", new Dictionary<string, object> { { "phase", phase }, { "t", Now } });
        }

        public void PublishScene(string areaId)
        {
            AreaNames.TryGetValue(areaId, out var name);
            Send("PATCH", "state", new Dictionary<string, object> { { "area", areaId }, { "areaName", name ?? areaId }, { "t", Now } });
        }

        /// <summary>字幕。text が null なら字幕を消す。</summary>
        public void PublishSubtitle(string speaker, string text)
        {
            object subtitle = text == null ? null : new Dictionary<string, object> { { "speaker", speaker ?? "" }, { "text", text } };
            Send("PATCH", "state", new Dictionary<string, object> { { "subtitle", subtitle }, { "t", Now } });
        }

        public void OpenChoice(ChoiceDef def)
        {
            choiceId = Now.ToString();
            Votes = new int[def.options.Length];
            TotalVotes = 0;
            lastRemainingSent = def.timeLimit;
            var options = new List<object>();
            foreach (var o in def.options)
                options.Add(new Dictionary<string, object> { { "label", o.label }, { "hint", o.hint.Replace("\n", "") } });
            Send("PUT", "choice", new Dictionary<string, object>
            {
                { "id", choiceId }, { "question", def.question }, { "options", options },
                { "timeLimit", def.timeLimit }, { "remaining", def.timeLimit }, { "status", "open" }, { "t", Now },
            });
        }

        /// <summary>残り時間をWebに伝える（1秒ごとに間引く）。</summary>
        public void UpdateRemaining(float remaining)
        {
            if (choiceId == null || lastRemainingSent - remaining < 1f) return;
            lastRemainingSent = remaining;
            Send("PATCH", "choice", new Dictionary<string, object> { { "remaining", Mathf.Max(0, remaining) } });
        }

        /// <summary>選択肢を締め切る。result が -1 なら時間切れ。decidedBy は "player" / "viewers" / "timeout"。</summary>
        public void CloseChoice(int result, string decidedBy)
        {
            if (choiceId == null) return;
            Send("PATCH", "choice", new Dictionary<string, object>
            {
                { "status", "closed" }, { "result", result }, { "decidedBy", decidedBy }, { "remaining", 0 },
            });
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
            Send("PUT", "ending", new Dictionary<string, object>
            {
                { "id", id.ToString() }, { "title", EndingCatalog.Title(id) }, { "genre", EndingCatalog.Genre(id) }, { "t", Now },
            });
        }

        public void ClearEnding() => Send("PUT", "ending", null);

        // ── VR視点の画像 ─────────────────────────────

        IEnumerator FrameLoop()
        {
            bool sending = false;
            while (true)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, Config.frameInterval));
                if (sending) continue;   // 回線が遅いときは前の送信が終わるまで撮らない
                spectator.Capture(jpg =>
                {
                    sending = true;
                    StartCoroutine(rest.Send("PUT", "frame", Json.Serialize(new Dictionary<string, object>
                    {
                        { "jpg", jpg }, { "w", Config.frameWidth }, { "h", Config.frameHeight }, { "t", Now },
                    }), _ => sending = false));
                });
            }
        }

        // ── 受け取る ─────────────────────────────────

        IEnumerator PollLoop()
        {
            // 起動前のコメントは流さない：最新のキーだけ覚えておく
            yield return rest.Get("comments", "orderBy=%22%24key%22&limitToLast=1", v =>
            {
                if (v is Dictionary<string, object> d) foreach (var k in d.Keys) lastCommentKey = k;
                commentsReady = true;
            });

            while (true)
            {
                yield return rest.Get("viewers", "shallow=true", v => ViewerCount = v is Dictionary<string, object> d ? d.Count : 0);

                if (choiceId != null)
                {
                    string id = choiceId;
                    yield return rest.Get("votes/" + id, null, v => { if (id == choiceId) CountVotes(v); });
                }

                if (commentsReady)
                {
                    string query = lastCommentKey == null
                        ? "orderBy=%22%24key%22&limitToLast=20"
                        : "orderBy=%22%24key%22&startAt=%22" + Uri.EscapeDataString(lastCommentKey) + "%22&limitToFirst=30";
                    yield return rest.Get("comments", query, ReceiveComments);
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(0.3f, Config.pollInterval));
            }
        }

        void CountVotes(object v)
        {
            var counts = new int[Votes.Length];
            int total = 0;
            if (v is Dictionary<string, object> d)
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

        void ReceiveComments(object v)
        {
            if (v is not Dictionary<string, object> d) return;
            var keys = new List<string>(d.Keys);
            keys.Sort(string.CompareOrdinal);   // FirebaseのpushキーはASCII順＝投稿順
            foreach (var key in keys)
            {
                if (key == lastCommentKey) continue;
                lastCommentKey = key;
                if (d[key] is not Dictionary<string, object> c) continue;
                string text = c.TryGetValue("text", out var t) ? t as string : null;
                if (string.IsNullOrWhiteSpace(text)) continue;
                string name = c.TryGetValue("name", out var n) ? n as string : null;
                CommentReceived?.Invoke(name, text);
            }
        }
    }
}
