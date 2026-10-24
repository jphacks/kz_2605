using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>Blenderモデルを装飾の結合から除外し、魚の尾だけを動かす。</summary>
    public class SceneryModelVisual : MonoBehaviour
    {
        public Transform tail;
        Quaternion rest;
        float phase;
        void Start() { if (tail != null) rest = tail.localRotation; phase = Random.value * 6.28f; }
        void Update()
        {
            if (tail != null) tail.localRotation = rest * Quaternion.Euler(0, Mathf.Sin(Time.time * 7 + phase) * 22, 0);
        }
    }
}
