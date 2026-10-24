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
            FleeTo(transform.position + d.normalized * distance, speed);
        }

        public void FleeTo(Vector3 destination, float speed)
        {
            StopAllCoroutines();
            lookTarget = null;
            lookAtPlayer = false;
            idle = Idle.Hop;
            idleSpeed = 12f;
            StartMoveTo(destination, speed);
        }

        public IEnumerator BullyTarget(Transform target, float duration)
        {
            lookTarget = target; lookAtPlayer = false; idle = Idle.None;
            var torso = body.Find("Torso");
            float h = torso != null ? torso.localScale.y / .36f : 1.15f;
            var shoulder = body.Find("Bully arm");
            if (shoulder == null)
            {
                shoulder = new GameObject("Bully arm").transform;
                shoulder.SetParent(body,false); shoulder.localPosition = new Vector3(h*.15f,h*.58f,0);
                var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                arm.transform.SetParent(shoulder,false); arm.transform.localPosition = new Vector3(0,-h*.12f,0);
                arm.transform.localScale = new Vector3(h*.085f,h*.15f,h*.085f);
                var col = arm.GetComponent<Collider>(); col.enabled=false; Destroy(col);
                if(torso != null) arm.GetComponent<Renderer>().sharedMaterial=torso.GetComponent<Renderer>().sharedMaterial;
            }
            var rest = body.localRotation;
            float elapsed=0;
            while(elapsed<duration)
            {
                elapsed+=Time.deltaTime;
                float strike=Mathf.Pow((Mathf.Sin(elapsed*7)+1)*.5f,3);
                shoulder.localRotation=Quaternion.Euler(Mathf.Lerp(-145,-35,strike),0,-12);
                body.localRotation=rest*Quaternion.Euler(8+strike*22,0,0);
                yield return null;
            }
            body.localRotation=rest; shoulder.localRotation=Quaternion.identity;
        }
    }
}
