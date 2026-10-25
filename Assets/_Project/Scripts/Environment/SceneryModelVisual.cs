using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>Blenderモデルを装飾の結合から除外し、魚の尾だけを動かす。</summary>
    public class SceneryModelVisual : MonoBehaviour
    {
        public Transform tail;
        public Transform finLeft, finRight;
        Quaternion rest;
        Quaternion leftRest, rightRest;
        Swimmer swimmer;
        float phase;
        void Start()
        {
            if (tail != null) rest = tail.localRotation;
            if (finLeft != null) leftRest = finLeft.localRotation;
            if (finRight != null) rightRest = finRight.localRotation;
            swimmer = GetComponentInParent<Swimmer>();
            phase = Random.value * 6.28f;
        }
        void Update()
        {
            phase += Time.deltaTime * 7 * (swimmer != null ? swimmer.SpeedMultiplier : 1);
            if (tail != null) tail.localRotation = rest * Quaternion.Euler(0, Mathf.Sin(phase) * 22, 0);
            if (finLeft != null) finLeft.localRotation = leftRest * Quaternion.Euler(0, 0, Mathf.Sin(phase * .7f) * 16);
            if (finRight != null) finRight.localRotation = rightRest * Quaternion.Euler(0, 0, -Mathf.Sin(phase * .7f) * 16);
        }
    }
}
