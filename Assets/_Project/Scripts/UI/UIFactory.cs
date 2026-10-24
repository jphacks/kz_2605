using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JPHACKS2026
{
    /// <summary>
    /// ワールド空間UIをコードで組み立てる。1単位＝1mmのキャンバス（scale 0.001）を使う。
    /// </summary>
    public static class UIFactory
    {
        public static TMP_FontAsset Font;

        public static readonly Color PanelColor = new(0.1f, 0.07f, 0.05f, 0.94f);
        public static readonly Color PaperColor = new(0.98f, 0.94f, 0.85f, 1f);
        public static readonly Color AccentColor = new(0.85f, 0.25f, 0.2f, 1f);

        public static Canvas CreateCanvas(string name, Vector2 sizeMm, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMm;
            rt.localScale = Vector3.one * 0.001f;
            return canvas;
        }

        public static Image CreateImage(Transform parent, string name, Color color, Vector2 size, Vector2 anchoredPos = default)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return img;
        }

        public static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, Color color,
            Vector2 boxSize, Vector2 anchoredPos = default, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = boxSize;
            rt.anchoredPosition = anchoredPos;
            return t;
        }

        /// <summary>文字に縁取りをつけて、明るい背景や模様の上でも読めるようにする。</summary>
        public static TextMeshProUGUI AddOutline(TextMeshProUGUI t, float width, Color? color = null)
        {
            t.outlineWidth = width;
            t.outlineColor = color ?? new Color(0.1f, 0.06f, 0.04f, 1f);
            return t;
        }

        public static VRButton CreateButton(Transform parent, string label, Vector2 size, Vector2 anchoredPos, System.Action onClick)
        {
            var img = CreateImage(parent, "Button_" + label, PaperColor, size, anchoredPos);
            var text = CreateText(img.transform, "Label", label, 34, new Color(0.15f, 0.1f, 0.08f), size - new Vector2(20, 10));
            Stretch((RectTransform)text.transform, 8);
            var btn = img.gameObject.AddComponent<VRButton>();
            btn.Setup(img, text, onClick);
            return btn;
        }
    }
}
