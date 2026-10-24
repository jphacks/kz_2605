using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JPHACKS2026
{
    /// <summary>カメラ直前に置いた板で画面を暗転・白転させる。場面転換の酔い対策にも使う。</summary>
    public class ScreenFader : MonoBehaviour
    {
        Image image;
        public float Alpha => image.color.a;
        /// <summary>白（明るい色）で覆われているか。光に包まれて場面が変わるときに使う。</summary>
        public bool IsWhiteOut => image.enabled && image.color.a > 0.99f && image.color.grayscale > 0.5f;

        public static ScreenFader Create(Transform head)
        {
            var canvas = UIFactory.CreateCanvas("ScreenFader", new Vector2(2000, 2000), head);
            canvas.sortingOrder = 100;
            canvas.transform.localPosition = new Vector3(0, 0, 0.12f);
            canvas.transform.localRotation = Quaternion.identity;
            var f = canvas.gameObject.AddComponent<ScreenFader>();
            f.image = UIFactory.CreateImage(canvas.transform, "Fade", Color.black, new Vector2(2000, 2000));
            f.Set(Color.black, 1);
            return f;
        }

        public void Set(Color color, float alpha)
        {
            color.a = alpha;
            image.color = color;
            image.enabled = alpha > 0.001f;
        }

        public IEnumerator FadeOut(float duration = 0.6f) => FadeTo(Color.black, 1, duration);
        public IEnumerator FadeIn(float duration = 0.6f) => FadeTo(image.color, 0, duration);

        /// <summary>暗転した画面の上に時間経過を表示する。</summary>
        public IEnumerator ShowTimeCard(string title, string subtitle)
        {
            var card = new GameObject("Time passage", typeof(RectTransform));
            card.transform.SetParent(image.transform, false);
            UIFactory.CreateText(card.transform, "Title", title, 16, UIFactory.PaperColor, new Vector2(180, 36), new Vector2(0, 15));
            UIFactory.CreateText(card.transform, "Subtitle", subtitle, 8, UIFactory.PaperColor, new Vector2(180, 24), new Vector2(0, -16));
            try { yield return Tween.Wait(1.8f); }
            finally { Destroy(card); }
        }

        public IEnumerator FadeTo(Color color, float alpha, float duration)
        {
            var from = image.color;
            float a0 = image.enabled ? from.a : 0f;
            yield return Tween.Run(duration, t => Set(Color.Lerp(from, color, t), Mathf.Lerp(a0, alpha, t)), unscaled: true);
            Set(color, alpha);
        }
    }
}
