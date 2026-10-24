using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JPHACKS2026
{
    /// <summary>
    /// コントローラーのレイ（UIPointer）で押せるボタン。EventSystemを使わず、BoxColliderで当たり判定する。
    /// </summary>
    public class VRButton : MonoBehaviour
    {
        public static int ActiveCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ActiveCount = 0;

        Image image;
        TextMeshProUGUI label;
        Action onClick;
        Color normal;
        bool hovered;

        public void Setup(Image img, TextMeshProUGUI text, Action click)
        {
            image = img;
            label = text;
            onClick = click;
            normal = img.color;
            var rt = (RectTransform)transform;
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(rt.sizeDelta.x, rt.sizeDelta.y, 10);
        }

        void OnEnable() => ActiveCount++;
        void OnDisable()
        {
            ActiveCount--;
            SetHover(false);
        }

        public void SetHover(bool on)
        {
            if (hovered == on || image == null) return;
            hovered = on;
            image.color = on ? new Color(1f, 0.82f, 0.45f) : normal;
            transform.localScale = on ? Vector3.one * 1.05f : Vector3.one;
        }

        public void Click()
        {
            SetHover(false);
            onClick?.Invoke();
        }
    }
}
