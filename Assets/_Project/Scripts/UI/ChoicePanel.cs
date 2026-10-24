using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JPHACKS2026
{
    /// <summary>
    /// アドベンチャーゲーム風の選択肢表示。選択肢の下に、対応する行動を薄く表示する。
    /// 決定はボタンではなくVR内の行動で行う（10/23会議）。カウントダウン付き。
    /// </summary>
    public class ChoicePanel : MonoBehaviour
    {
        TextMeshProUGUI question;
        TextMeshProUGUI countdown;
        Image bar;
        RectTransform optionRoot;
        readonly List<GameObject> optionViews = new();
        readonly List<Image> optionBgs = new();

        public static ChoicePanel Create()
        {
            var canvas = UIFactory.CreateCanvas("ChoicePanel", new Vector2(1000, 560));
            var panel = canvas.gameObject.AddComponent<ChoicePanel>();
            var follow = canvas.gameObject.AddComponent<WorldPanel>();
            follow.distance = 1.6f;
            follow.heightOffset = 0.15f;
            follow.followAngle = 45f;

            var bg = UIFactory.CreateImage(canvas.transform, "Bg", UIFactory.PanelColor, new Vector2(1000, 560));
            panel.question = UIFactory.CreateText(bg.transform, "Question", "", 44, Color.white, new Vector2(940, 90), new Vector2(0, 220));
            panel.countdown = UIFactory.CreateText(bg.transform, "Countdown", "", 34, new Color(1, 0.85f, 0.5f), new Vector2(300, 50), new Vector2(0, -245));
            var barBg = UIFactory.CreateImage(bg.transform, "BarBg", new Color(1, 1, 1, 0.15f), new Vector2(900, 12), new Vector2(0, 165));
            panel.bar = UIFactory.CreateImage(barBg.transform, "Bar", new Color(1, 0.75f, 0.3f), new Vector2(900, 12));
            var barRt = (RectTransform)panel.bar.transform;
            barRt.pivot = new Vector2(0, 0.5f);
            barRt.anchoredPosition = new Vector2(-450, 0);

            var root = new GameObject("Options", typeof(RectTransform));
            root.transform.SetParent(bg.transform, false);
            panel.optionRoot = (RectTransform)root.transform;
            panel.optionRoot.sizeDelta = new Vector2(940, 360);
            panel.optionRoot.anchoredPosition = new Vector2(0, -30);

            canvas.gameObject.SetActive(false);
            return panel;
        }

        public void Show(ChoiceDef def)
        {
            foreach (var v in optionViews) Destroy(v);
            optionViews.Clear();
            optionBgs.Clear();

            question.text = def.question;
            int n = def.options.Length;
            float w = 940f / n - 20f;
            for (int i = 0; i < n; i++)
            {
                var o = def.options[i];
                float x = -470f + (i + 0.5f) * (940f / n);
                var obg = UIFactory.CreateImage(optionRoot, "Option" + i, new Color(0.98f, 0.94f, 0.85f, 0.95f), new Vector2(w, 300), new Vector2(x, 0));
                UIFactory.CreateText(obg.transform, "Label", o.label, 46, new Color(0.15f, 0.1f, 0.08f), new Vector2(w - 30, 120), new Vector2(0, 55));
                UIFactory.CreateText(obg.transform, "Hint", o.hint, 28, new Color(0.15f, 0.1f, 0.08f, 0.5f), new Vector2(w - 30, 130), new Vector2(0, -70));
                optionViews.Add(obg.gameObject);
                optionBgs.Add(obg);
            }
            gameObject.SetActive(true);
            GetComponent<WorldPanel>().SnapToView();
            SetRemaining(1, def.timeLimit);
        }

        public void SetRemaining(float normalized, float seconds)
        {
            bar.rectTransform.sizeDelta = new Vector2(900 * Mathf.Clamp01(normalized), 12);
            countdown.text = $"のこり {Mathf.CeilToInt(seconds)} 秒";
            bar.color = normalized < 0.3f ? new Color(1, 0.35f, 0.25f) : new Color(1, 0.75f, 0.3f);
        }

        public void Highlight(int index)
        {
            for (int i = 0; i < optionBgs.Count; i++)
                optionBgs[i].color = i == index ? new Color(1f, 0.82f, 0.45f) : new Color(0.98f, 0.94f, 0.85f, 0.4f);
            countdown.text = "";
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
