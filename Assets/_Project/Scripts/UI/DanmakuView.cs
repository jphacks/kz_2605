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
        const float Width = 1700, Height = 420;
        const int Lanes = 5;
        const int MaxComments = 24;

        public float speed = 260f;   // px/秒

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
            follow.distance = 2.2f;
            follow.heightOffset = 0.55f;
            follow.continuousFollow = true;
            follow.followSharpness = 3f;

            var clip = new GameObject("Clip", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
            clip.transform.SetParent(canvas.transform, false);
            view.root = (RectTransform)clip.transform;
            view.root.sizeDelta = new Vector2(Width, Height);

            var badgeBg = UIFactory.CreateImage(canvas.transform, "LiveBadge", new Color(0.85f, 0.12f, 0.1f, 0.9f), new Vector2(340, 56), new Vector2(-Width / 2 + 180, Height / 2 + 40));
            view.badge = UIFactory.CreateText(badgeBg.transform, "Text", "", 34, Color.white, new Vector2(330, 56));
            live.CommentReceived += view.Enqueue;
            return view;
        }

        void OnDestroy()
        {
            if (live != null) live.CommentReceived -= Enqueue;
        }

        void Enqueue(string name, string text)
        {
            if (pending.Count < 50) pending.Enqueue(text.Length > 40 ? text.Substring(0, 40) + "…" : text);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            badge.text = $"● LIVE　視聴者 {live.ViewerCount}人";

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
            var t = UIFactory.CreateText(root, "Comment", text, 46, Color.white, new Vector2(1200, 70), default, TextAlignmentOptions.Left);
            UIFactory.AddOutline(t, 0.25f, new Color(0, 0, 0, 0.9f));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float w = Mathf.Min(1200, t.GetPreferredValues(text).x + 20);
            var rt = (RectTransform)t.transform;
            rt.sizeDelta = new Vector2(w, 70);
            rt.pivot = new Vector2(0, 0.5f);
            float laneHeight = Height / Lanes;
            rt.anchoredPosition = new Vector2(Width / 2, Height / 2 - laneHeight * (lane + 0.5f));
            flying.Add((rt, w));
            // 前のコメントが十分進んでから同じレーンに次を出す
            laneFreeAt[lane] = Time.unscaledTime + (w + 80) / speed;
        }
    }
}
