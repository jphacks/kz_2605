using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>プレイヤーの頭が範囲（XZ）に入ったら成立。</summary>
    public class ZoneTrigger : BranchTrigger
    {
        public Vector3 size = new(2, 3, 2);

        void Update()
        {
            if (!Armed || Fired) return;
            var local = transform.InverseTransformPoint(Head.position);
            if (Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f) Fire();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0.8f, 0.2f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, size.y * 0.5f, 0), size);
        }
    }
}
