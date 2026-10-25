using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// プレイヤーの手。速度を推定し（追い払う・叩く判定、投げる処理に使う）、肌の色を切り替える。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Hand : MonoBehaviour
    {
        public HandSide side;
        public Renderer[] skinRenderers;
        public Color skinColor = new(0.91f, 0.72f, 0.58f);
        public Color agedColor = new(0.78f, 0.74f, 0.70f);

        public Vector3 Velocity { get; private set; }
        public float Speed => Velocity.magnitude;
        public bool GripPressed => GripValue > 0.55f;
        public float GripValue => DesktopSimulator.Active ? DesktopSimulator.Grip(side) : GameInput.Grip(side);
        public float TriggerValue => DesktopSimulator.Active ? DesktopSimulator.Trigger(side) : GameInput.Trigger(side);
        public bool TriggerPressed => TriggerValue > 0.55f;

        readonly Vector3[] samples = new Vector3[6];
        int sampleIndex;
        Vector3 lastPos;
        float lastTime;

        void Awake()
        {
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            HandVisual.TryAttach(this);   // 手のモデルがあれば仮のミトンと置き換える
            SetAged(false);
        }

        void OnEnable()
        {
            lastPos = transform.localPosition;
            lastTime = Time.unscaledTime;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledTime - lastTime;
            if (dt <= 0.0001f) return;
            // 移動・亀に乗っての移動の速度を含めないよう、リグ内のローカル座標で速度を測る
            samples[sampleIndex] = (transform.localPosition - lastPos) / dt;
            sampleIndex = (sampleIndex + 1) % samples.Length;
            Vector3 sum = Vector3.zero;
            foreach (var s in samples) sum += s;
            Velocity = transform.parent != null ? transform.parent.TransformVector(sum / samples.Length) : sum / samples.Length;
            lastPos = transform.localPosition;
            lastTime = Time.unscaledTime;
        }

        /// <summary>テレポートなどで瞬間移動した後、速度が跳ねないようにする。</summary>
        public void ResetVelocity()
        {
            for (int i = 0; i < samples.Length; i++) samples[i] = Vector3.zero;
            Velocity = Vector3.zero;
            lastPos = transform.localPosition;
        }

        public void SetAged(bool aged)
        {
            // 老いた手のモデルがあれば差し替える。無ければ肌の色だけ変える
            var visual = GetComponent<HandVisual>();
            if (visual != null && visual.SetAged(aged)) return;
            foreach (var r in skinRenderers)
                if (r != null) r.material.SetColor("_BaseColor", aged ? agedColor : skinColor);
        }
    }
}
