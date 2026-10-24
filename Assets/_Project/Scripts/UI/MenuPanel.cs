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

            var bg = UIFactory.CreateImage(canvas.transform, "Bg", UIFactory.PanelColor, new Vector2(800, 620));
            panel.title = UIFactory.CreateText(bg.transform, "Title", "", 64, Color.white, new Vector2(760, 110), new Vector2(0, 230));
            panel.body = UIFactory.CreateText(bg.transform, "Body", "", 34, new Color(1, 1, 1, 0.85f), new Vector2(740, 150), new Vector2(0, 110));
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
            float h = 90, gap = 22;
            float total = items.Length * h + (items.Length - 1) * gap;
            for (int i = 0; i < items.Length; i++)
            {
                var (label, onClick) = items[i];
                float y = total / 2 - h / 2 - i * (h + gap);
                var btn = UIFactory.CreateButton(buttonRoot, label, new Vector2(560, h), new Vector2(0, y), onClick);
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
