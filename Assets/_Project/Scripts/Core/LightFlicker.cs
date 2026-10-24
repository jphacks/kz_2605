using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>行灯・ろうそくの灯りのゆらぎ。強さをノイズで揺らす。</summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public float baseIntensity = 1f;
        public float amount = 0.25f;
        public float speed = 3f;

        Light lightSource;
        float seed;

        void Awake()
        {
            lightSource = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;
            lightSource.intensity = baseIntensity * (1f + n * amount);
        }
    }
}
