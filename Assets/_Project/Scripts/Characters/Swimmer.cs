using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>魚・鯛やひらめの舞い踊りなど、中心の周りをぐるぐる回る簡易アニメーション。</summary>
    public class Swimmer : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 2f;
        public float speed = 0.5f;
        public float verticalAmplitude = 0.3f;
        public float SpeedMultiplier { get; set; } = 1f;

        float angle;

        void Awake()
        {
            angle = Random.value * Mathf.PI * 2;
            if (center == Vector3.zero) center = transform.localPosition;
        }

        void Update()
        {
            angle += speed * SpeedMultiplier * Time.deltaTime;
            var p = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle * 2.3f) * verticalAmplitude, Mathf.Sin(angle) * radius);
            var tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * Mathf.Sign(speed);
            transform.localPosition = p;
            if (tangent.sqrMagnitude > 0.001f) transform.localRotation = Quaternion.LookRotation(tangent);
        }
    }
}
