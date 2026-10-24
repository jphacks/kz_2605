using System;
using System.Collections;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>演出用の小さなコルーチン集。ポーズ中は止まるようにスケール時間を使う。</summary>
    public static class Tween
    {
        public static IEnumerator Run(float duration, Action<float> step, bool unscaled = false)
        {
            float t = 0;
            while (t < duration)
            {
                step(Mathf.Clamp01(t / duration));
                yield return null;
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            step(1);
        }

        public static IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds)
            {
                yield return null;
                t += Time.deltaTime;
            }
        }

        public static float Smooth(float t) => t * t * (3 - 2 * t);

        public static IEnumerator Scale(Transform tr, Vector3 to, float duration)
        {
            var from = tr.localScale;
            yield return Run(duration, t => tr.localScale = Vector3.LerpUnclamped(from, to, Smooth(t)));
        }

        public static IEnumerator Move(Transform tr, Vector3 worldTo, float duration)
        {
            var from = tr.position;
            yield return Run(duration, t => tr.position = Vector3.LerpUnclamped(from, worldTo, Smooth(t)));
        }

        public static IEnumerator Rotate(Transform tr, Quaternion localTo, float duration)
        {
            var from = tr.localRotation;
            yield return Run(duration, t => tr.localRotation = Quaternion.SlerpUnclamped(from, localTo, Smooth(t)));
        }

        /// <summary>環境光・太陽・背景色・フォグをまとめて変化させる（ホラー演出用）。</summary>
        public static IEnumerator Environment(Color sky, Color ambient, Color sun, float sunIntensity, Color fogColor, float fogDensity, float duration)
        {
            var gm = GameManager.Instance;
            var cam = gm.Rig.Head.GetComponent<Camera>();
            var sky0 = cam.backgroundColor;
            var amb0 = RenderSettings.ambientLight;
            var sun0 = gm.sun.color;
            var int0 = gm.sun.intensity;
            var fog0 = RenderSettings.fogColor;
            var den0 = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            RenderSettings.fog = true;
            yield return Run(duration, t =>
            {
                cam.backgroundColor = Color.Lerp(sky0, sky, t);
                RenderSettings.ambientLight = Color.Lerp(amb0, ambient, t);
                gm.sun.color = Color.Lerp(sun0, sun, t);
                gm.sun.intensity = Mathf.Lerp(int0, sunIntensity, t);
                RenderSettings.fogColor = Color.Lerp(fog0, fogColor, t);
                RenderSettings.fogDensity = Mathf.Lerp(den0, fogDensity, t);
            });
        }

        /// <summary>子孫のRendererの色をまとめて変える（マテリアルはインスタンス化される。Resettableで元に戻る）。</summary>
        public static void Tint(GameObject root, Color multiply)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var m in r.materials)
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * multiply);
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", m.GetColor("_EmissionColor") * multiply);
                }
            }
        }

        public static void SetColor(GameObject root, Color color)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var m in r.materials)
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            }
        }
    }
}
