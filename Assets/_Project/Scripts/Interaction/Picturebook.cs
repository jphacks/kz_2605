using System.Collections;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 物語の入口と「おわり」に使う絵本。表紙を開閉し、吸い込まれる演出で光る。
    /// </summary>
    public class Picturebook : MonoBehaviour
    {
        public Transform coverHinge;
        public float openAngle = -175f;
        public Renderer glowRenderer;
        public Light glowLight;
        public TouchTrigger openTrigger;

        public bool IsOpen { get; private set; }

        public void SetOpenImmediate(bool open)
        {
            IsOpen = open;
            coverHinge.localRotation = Quaternion.Euler(0, 0, open ? openAngle : 0);
            SetGlow(0);
        }

        public IEnumerator Open(float duration = 1.2f)
        {
            IsOpen = true;
            yield return Tween.Rotate(coverHinge, Quaternion.Euler(0, 0, openAngle), duration);
        }

        public IEnumerator Close(float duration = 1.4f)
        {
            IsOpen = false;
            yield return Tween.Rotate(coverHinge, Quaternion.identity, duration);
        }

        public IEnumerator GlowUp(float duration)
        {
            yield return Tween.Run(duration, t => SetGlow(t * t));
        }

        public void SetGlow(float t)
        {
            if (glowRenderer != null)
            {
                glowRenderer.gameObject.SetActive(t > 0.001f);
                glowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.6f, t);
            }
            if (glowLight != null)
            {
                glowLight.enabled = t > 0.001f;
                glowLight.intensity = Mathf.Lerp(0, 6, t);
            }
        }
    }
}
