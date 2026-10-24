using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>プレイヤーがこの地点から一定距離以上離れたら成立（立ち去る）。</summary>
    public class LeaveTrigger : BranchTrigger
    {
        public float radius = 5f;

        void Update()
        {
            if (!Armed || Fired) return;
            var d = Head.position - transform.position;
            d.y = 0;
            if (d.magnitude > radius) Fire();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
