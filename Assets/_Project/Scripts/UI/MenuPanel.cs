using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// タイトル・ポーズ・エンディング結果・時間切れなどに使う汎用メニュー。
    /// ボタンはレイで指してトリガーで押す。
    /// </summary>
    public class MenuPanel : MonoBehaviour
    {
        const float Width = 800, MinHeight = 620;

        RectTransform bg;
        TextMeshProUGUI title;
        TextMeshProUGUI body;
        RectTransform buttonRoot;
        readonly List<GameObject> buttons = new();

        public bool IsOpen => gameObject.activeSelf;

        public static MenuPanel Create(string name, int sortingOrder = 30)
        {
            var canvas = UIFactory.CreateCanvas(name, new Vector2(800, 620));
            canvas.sortingOrder = sortingOrder;
            var panel = canvas.gameObject.AddComponent<MenuPanel>();
            var follow = canvas.gameObject.AddComponent<WorldPanel>();
            follow.distance = 1.3f;
            follow.heightOffset = 0f;

            var bg = UIFactory.CreateImage(canvas.transform, "Bg", UIFactory.PanelColor, new Vector2(Width, MinHeight));
            panel.bg = (RectTransform)bg.transform;
            UIFactory.Stretch((RectTransform)UIFactory.CreateImage(bg.transform, "Frame", new Color(0.95f, 0.75f, 0.3f, 0.9f), Vector2.zero).transform, 10);
            UIFactory.Stretch((RectTransform)UIFactory.CreateImage(bg.transform, "Inner", UIFactory.PanelColor, Vector2.zero).transform, 14);
            panel.title = UIFactory.CreateText(bg.transform, "Title", "", 72, UIFactory.PaperColor, new Vector2(760, 110), new Vector2(0, 230));
            panel.body = UIFactory.CreateText(bg.transform, "Body", "", 40, Color.white, new Vector2(740, 170), new Vector2(0, 110));
            var root = new GameObject("Buttons", typeof(RectTransform));
            root.transform.SetParent(bg.transform, false);
            panel.buttonRoot = (RectTransform)root.transform;
            panel.buttonRoot.anchoredPosition = new Vector2(0, -110);
            canvas.gameObject.SetActive(false);
            return panel;
        }

        public void Show(string titleText, string bodyText, params (string label, Action onClick)[] items)
        {
            foreach (var b in buttons) Destroy(b);
            buttons.Clear();
            title.text = titleText;
            body.text = bodyText;

            // 4つ以上は2列に並べ、入りきらなければパネルを縦に伸ばす
            int cols = items.Length > 3 ? 2 : 1;
            int rows = (items.Length + cols - 1) / cols;
            float w = cols == 1 ? 560 : 340, h = cols == 1 ? 90 : 76, gap = cols == 1 ? 22 : 16, colGap = 24;
            float buttonsHeight = rows * h + (rows - 1) * gap;
            float height = Mathf.Max(MinHeight, 340 + buttonsHeight + 50);
            float top = height / 2;
            bg.sizeDelta = new Vector2(Width, height);
            ((RectTransform)title.transform).anchoredPosition = new Vector2(0, top - 80);
            ((RectTransform)body.transform).anchoredPosition = new Vector2(0, top - 205);
            buttonRoot.anchoredPosition = new Vector2(0, top - 320 - buttonsHeight / 2);

            for (int i = 0; i < items.Length; i++)
            {
                var (label, onClick) = items[i];
                int row = i / cols, col = i % cols;
                float x = cols == 1 ? 0 : (col == 0 ? -1 : 1) * (w + colGap) / 2;
                float y = buttonsHeight / 2 - h / 2 - row * (h + gap);
                var btn = UIFactory.CreateButton(buttonRoot, label, new Vector2(w, h), new Vector2(x, y), onClick);
                buttons.Add(btn.gameObject);
            }
            gameObject.SetActive(true);
            GetComponent<WorldPanel>().SnapToView();
        }

        /// <summary>パネルを追従させず、指定位置に固定して表示する（タイトルの絵本の上など）。</summary>
        public void PlaceAt(Transform anchor)
        {
            var follow = GetComponent<WorldPanel>();
            follow.enabled = false;
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            GetComponent<WorldPanel>().enabled = true;
        }
    }
}
