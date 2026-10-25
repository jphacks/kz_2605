using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>叩くのに使える棒（浜辺の流木）。振った速さを測り、TouchTrigger（requireStick）が判定に使う。</summary>
    public class HitStick : MonoBehaviour
    {
        public float Speed { get; private set; }

        Vector3 lastPos;

        void OnEnable()
        {
            lastPos = transform.position;
            Speed = 0;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;
            // 手に追従した後の位置で測る。投げたときも同じ式で速さが出る
            Speed = Mathf.Lerp(Speed, (transform.position - lastPos).magnitude / dt, 0.5f);
            lastPos = transform.position;
        }
    }
}
