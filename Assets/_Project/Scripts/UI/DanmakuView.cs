using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 配信のコメントを、視界の上の方に右から左へ流す（弾幕）。左上に「LIVE」と視聴者数も出す。
    /// 視界の中心や字幕・選択肢と重ならないよう、上半分の帯の中だけを流れる。
    /// </summary>
    public class DanmakuView : MonoBehaviour
    {
        const float Width = 1700, Height = 500;
        const int Lanes = 4;
        const int MaxComments = 20;
        const float FontSize = 76;   // VRで読める大きさ（1.8m先で約7.5cm）

        public float speed = 230f;   // px/秒

        RectTransform root;
        TextMeshProUGUI badge;
        readonly List<(RectTransform rt, float width)> flying = new();
        readonly float[] laneFreeAt = new float[Lanes];
        readonly Queue<string> pending = new();
        LiveStream live;

        public static DanmakuView Create(LiveStream live)
        {
            var canvas = UIFactory.CreateCanvas("Danmaku", new Vector2(Width, Height));
            canvas.sortingOrder = 15;
            var view = canvas.gameObject.AddComponent<DanmakuView>();
            view.live = live;
            var follow = canvas.gameObject.AddComponent<WorldPanel>();
            follow.distance = 1.8f;
            follow.heightOffset = 0.5f;
            follow.continuousFollow = true;
            follow.followSharpness = 3f;

            var clip = new GameObject("Clip", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
            clip.transform.SetParent(canvas.transform, false);
            view.root = (RectTransform)clip.transform;
            view.root.sizeDelta = new Vector2(Width, Height);

            var badgeBg = UIFactory.CreateImage(canvas.transform, "LiveBadge", new Color(0.85f, 0.12f, 0.1f, 0.9f), new Vector2(420, 68), new Vector2(-Width / 2 + 220, Height / 2 + 46));
            view.badge = UIFactory.CreateText(badgeBg.transform, "Text", "", 42, Color.white, new Vector2(410, 68));
            live.CommentReceived += view.Enqueue;
            live.ReactionReceived += view.OnReaction;
            return view;
        }

        void OnDestroy()
        {
            if (live == null) return;
            live.CommentReceived -= Enqueue;
            live.ReactionReceived -= OnReaction;
        }

        // ── リアクション：帯の右下から、言葉がふわっと浮かんで消える ──

        static readonly Dictionary<string, (string word, Color color)> Reactions = new()
        {
            { "clap", ("パチパチ", new Color(1f, 0.85f, 0.35f)) },
            { "laugh", ("ワハハ", new Color(1f, 0.6f, 0.3f)) },
            { "wow", ("えっ！", new Color(0.55f, 0.85f, 1f)) },
            { "love", ("すてき", new Color(1f, 0.55f, 0.75f)) },
            { "cheer", ("がんばれ", new Color(0.6f, 1f, 0.55f)) },
        };
        const int MaxReactions = 12;
        const float ReactionLife = 2.2f;
        readonly List<(TextMeshProUGUI text, Vector2 from, float born)> reactions = new();

        void OnReaction(string type)
        {
            if (!Reactions.TryGetValue(type, out var r) || reactions.Count >= MaxReactions) return;
            var t = UIFactory.CreateText(transform, "Reaction", r.word, 64, r.color, new Vector2(360, 90));
            UIFactory.AddOutline(t, 0.3f, new Color(0, 0, 0, 0.9f));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var from = new Vector2(Width / 2 - 160 - Random.Range(0f, 220f), -Height / 2 - 20);
            ((RectTransform)t.transform).anchoredPosition = from;
            reactions.Add((t, from, Time.unscaledTime));
        }

        void UpdateReactions()
        {
            for (int i = reactions.Count - 1; i >= 0; i--)
            {
                var (text, from, born) = reactions[i];
                float k = (Time.unscaledTime - born) / ReactionLife;
                if (k >= 1) { Destroy(text.gameObject); reactions.RemoveAt(i); continue; }
                ((RectTransform)text.transform).anchoredPosition = from + new Vector2(Mathf.Sin(k * 6f) * 18f, k * 260f);
                text.transform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.6f, 1.1f, k / 0.15f) : 1f);
                text.alpha = k < 0.7f ? 1f : 1 - (k - 0.7f) / 0.3f;
            }
        }

        void Enqueue(string name, string text)
        {
            if (pending.Count < 50) pending.Enqueue(text.Length > 30 ? text.Substring(0, 30) + "…" : text);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            badge.text = $"● LIVE　視聴者 {live.ViewerCount}人";
            UpdateReactions();

            // 空いているレーンに次のコメントを出す
            while (pending.Count > 0 && flying.Count < MaxComments)
            {
                int lane = FreeLane();
                if (lane < 0) break;
                Spawn(pending.Dequeue(), lane);
            }

            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var (rt, w) = flying[i];
                rt.anchoredPosition += Vector2.left * speed * dt;
                if (rt.anchoredPosition.x < -Width / 2 - w)
                {
                    Destroy(rt.gameObject);
                    flying.RemoveAt(i);
                }
            }
        }

        int FreeLane()
        {
            int best = -1;
            for (int i = 0; i < Lanes; i++)
                if (laneFreeAt[i] <= Time.unscaledTime && (best < 0 || laneFreeAt[i] < laneFreeAt[best])) best = i;
            return best;
        }

        void Spawn(string text, int lane)
        {
            float lineHeight = FontSize * 1.4f;
            var t = UIFactory.CreateText(root, "Comment", text, FontSize, Color.white, new Vector2(2400, lineHeight), default, TextAlignmentOptions.Left);
            UIFactory.AddOutline(t, 0.3f, new Color(0, 0, 0, 0.95f));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float w = t.GetPreferredValues(text).x + 20;
            var rt = (RectTransform)t.transform;
            rt.sizeDelta = new Vector2(w, lineHeight);
            rt.pivot = new Vector2(0, 0.5f);
            float laneHeight = Height / Lanes;
            rt.anchoredPosition = new Vector2(Width / 2, Height / 2 - laneHeight * (lane + 0.5f));
            flying.Add((rt, w));
            // 前のコメントが十分進んでから同じレーンに次を出す
            laneFreeAt[lane] = Time.unscaledTime + (w + 80) / speed;
        }
    }
}
