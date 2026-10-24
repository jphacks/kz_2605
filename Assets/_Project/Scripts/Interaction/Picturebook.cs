using System.Collections;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 物語の入口と「おわり」に使う絵本。表紙を開閉し、吸い込まれる演出で光る。
    /// 光り方（SetGlow 0→1）：ページが灯る → 光の筋と粒が立ちのぼる → 光がプレイヤーを包むように広がる。
    /// </summary>
    public class Picturebook : MonoBehaviour
    {
        public Transform coverHinge;
        public float openAngle = -175f;
        [Tooltip("広がってプレイヤーを包む光（半透明・両面表示の球）")]
        public Renderer glowRenderer;
        public Light glowLight;
        [Tooltip("ページの上に重ねた光る板")]
        public Renderer pageGlow;
        [Tooltip("ページから立ちのぼる光の筋（子のRendererをまとめて扱う）")]
        public Transform rays;
        public ParticleSystem sparkles;
        public TouchTrigger openTrigger;
        public Color glowColor = new(1f, 0.9f, 0.62f);
        public float maxGlowScale = 7f;
        public float maxSparkleRate = 90f;

        public bool IsOpen { get; private set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        Renderer[] rayRenderers;

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

        public IEnumerator GlowUp(float duration) => GlowTo(1f, duration);

        /// <summary>現在の光り具合から目標までなめらかに変える。</summary>
        public IEnumerator GlowTo(float target, float duration)
        {
            float from = Glow;
            yield return Tween.Run(duration, t => SetGlow(Mathf.Lerp(from, target, Tween.Smooth(t))));
        }

        public float Glow { get; private set; }

        void Update()
        {
            if (rays != null && Glow > 0.001f) rays.Rotate(0, 25f * Time.deltaTime, 0, Space.Self);
        }

        public void SetGlow(float t)
        {
            Glow = t;
            block ??= new MaterialPropertyBlock();

            // 1. ページが灯る（序盤で最大に）
            float page = Mathf.Clamp01(t / 0.3f);
            SetAlpha(pageGlow, page);

            // 2. 光の筋が伸びる
            float ray = Mathf.Clamp01((t - 0.1f) / 0.5f);
            if (rays != null)
            {
                rayRenderers ??= rays.GetComponentsInChildren<Renderer>(true);
                rays.gameObject.SetActive(ray > 0.001f);
                rays.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, Tween.Smooth(ray)), 1f);
                foreach (var r in rayRenderers) SetAlpha(r, ray * 0.6f);
            }

            // 3. 光の粒
            if (sparkles != null)
            {
                var emission = sparkles.emission;
                emission.rateOverTime = maxSparkleRate * Mathf.Clamp01(t * 1.5f);
                if (t > 0.001f && !sparkles.isPlaying) sparkles.Play();
                else if (t <= 0.001f && sparkles.isPlaying) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // 4. 光が広がり、プレイヤーを包む（後半で一気に）
            float wrap = Mathf.Clamp01((t - 0.35f) / 0.65f);
            if (glowRenderer != null)
            {
                glowRenderer.gameObject.SetActive(wrap > 0.001f);
                glowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, maxGlowScale, wrap * wrap);
                SetAlpha(glowRenderer, Mathf.Lerp(0.15f, 0.8f, wrap));
            }

            if (glowLight != null)
            {
                glowLight.enabled = t > 0.001f;
                glowLight.intensity = Mathf.Lerp(0, 7, t);
                glowLight.range = Mathf.Lerp(1.5f, 6f, t);
            }
        }

        void SetAlpha(Renderer r, float alpha)
        {
            if (r == null) return;
            r.enabled = alpha > 0.001f;
            var c = glowColor;
            c.a = alpha;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block);
        }
    }
}
