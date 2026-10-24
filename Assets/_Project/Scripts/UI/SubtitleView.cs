using TMPro;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>視界の下部に出る字幕。話者名があれば「亀「……」」の形で表示する。</summary>
    public class SubtitleView : MonoBehaviour
    {
        TextMeshProUGUI text;
        GameObject panel;

        public static SubtitleView Create()
        {
            var canvas = UIFactory.CreateCanvas("Subtitle", new Vector2(900, 180));
            canvas.sortingOrder = 20;
            var view = canvas.gameObject.AddComponent<SubtitleView>();
            var follow = canvas.gameObject.AddComponent<WorldPanel>();
            follow.distance = 1.5f;
            follow.heightOffset = -0.42f;
            follow.continuousFollow = true;
            follow.followSharpness = 4f;

            var bg = UIFactory.CreateImage(canvas.transform, "Bg", new Color(0, 0, 0, 0.55f), new Vector2(900, 180));
            view.panel = bg.gameObject;
            view.text = UIFactory.CreateText(bg.transform, "Text", "", 40, Color.white, new Vector2(860, 160));
            view.Hide();
            return view;
        }

        public void Show(string speaker, string line)
        {
            gameObject.SetActive(true);
            text.text = string.IsNullOrEmpty(speaker) ? line : $"<color=#FFD27A>{speaker}</color>「{line}」";
            GetComponent<WorldPanel>().SnapToView();
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
