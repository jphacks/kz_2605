using System.Collections;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 登場人物（亀・子ども・乙姫・村人）の簡易な動き。仮モデルでもAnimatorなしで動きが伝わるようにする。
    /// 本番モデルにAnimatorが付いたら、ここからパラメータを送る形に拡張する。
    /// </summary>
    public class SimpleCharacter : MonoBehaviour
    {
        public enum Idle { None, Bob, Hop, Sway }

        [Tooltip("体の見た目部分（上下に揺らす対象）。未設定なら自分自身")]
        public Transform body;
        public Idle idle = Idle.Bob;
        public float idleAmplitude = 0.03f;
        public float idleSpeed = 2f;
        [Tooltip("待機中にこの対象の方を向く")]
        public Transform lookTarget;
        public bool lookAtPlayer;

        Idle initialIdle;
        float initialIdleSpeed;
        Transform initialLookTarget;
        bool initialLookAtPlayer;
        Vector3 bodyBase;
        float phase;

        void Awake() => Init();

        bool initialized;

        void Init()
        {
            if (initialized) return;
            initialized = true;
            if (body == null) body = transform;
            initialIdle = idle;
            initialIdleSpeed = idleSpeed;
            initialLookTarget = lookTarget;
            initialLookAtPlayer = lookAtPlayer;
            bodyBase = body == transform ? Vector3.zero : body.localPosition;
            phase = Random.value * 10f;
        }

        public void ResetBehaviour()
        {
            Init();
            StopAllCoroutines();
            idle = initialIdle;
            idleSpeed = initialIdleSpeed;
            lookTarget = initialLookTarget;
            lookAtPlayer = initialLookAtPlayer;
            if (body != null && body != transform) body.localPosition = bodyBase;
        }

        void Update()
        {
            if (body == null || body == transform) return;
            phase += Time.deltaTime * idleSpeed;
            float y = idle switch
            {
                Idle.Bob => Mathf.Sin(phase) * idleAmplitude,
                Idle.Hop => Mathf.Abs(Mathf.Sin(phase)) * idleAmplitude,
                _ => 0f,
            };
            float sway = idle == Idle.Sway ? Mathf.Sin(phase) * 8f : 0f;
            body.localPosition = bodyBase + Vector3.up * y;
            if (idle == Idle.Sway) body.localRotation = Quaternion.Euler(0, 0, sway);

            Transform target = lookAtPlayer && GameManager.Instance != null ? GameManager.Instance.Rig.Head : lookTarget;
            if (target != null)
            {
                var d = target.position - transform.position;
                d.y = 0;
                if (d.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1 - Mathf.Exp(-4f * Time.deltaTime));
            }
        }

        /// <summary>指定地点まで歩く（走る）。向きも進行方向に合わせる。</summary>
        public IEnumerator MoveTo(Vector3 worldPos, float speed)
        {
            var saveLook = lookTarget; var saveLookPlayer = lookAtPlayer;
            lookTarget = null; lookAtPlayer = false;
            while (true)
            {
                var d = worldPos - transform.position;
                d.y = 0;
                if (d.magnitude < 0.05f) break;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1 - Mathf.Exp(-10f * Time.deltaTime));
                transform.position += d.normalized * Mathf.Min(d.magnitude, speed * Time.deltaTime);
                yield return null;
            }
            lookTarget = saveLook; lookAtPlayer = saveLookPlayer;
        }

        public void StartMoveTo(Vector3 worldPos, float speed) => StartCoroutine(MoveTo(worldPos, speed));

        /// <summary>from から遠ざかる方向へ逃げる。</summary>
        public void Flee(Vector3 from, float distance, float speed)
        {
            var d = transform.position - from;
            d.y = 0;
            if (d.sqrMagnitude < 0.01f) d = transform.forward;
            idle = Idle.Hop;
            idleSpeed = 12f;
            StartMoveTo(transform.position + d.normalized * distance, speed);
        }
    }
}
