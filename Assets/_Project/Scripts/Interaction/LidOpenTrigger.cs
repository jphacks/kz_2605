using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>玉手箱のふたを実際に持ち上げたら成立（03 機能仕様書）。</summary>
    public class LidOpenTrigger : BranchTrigger
    {
        public Grabbable lid;
        public float openDistance = 0.1f;

        Vector3 restLocalPos;

        protected override void Awake()
        {
            base.Awake();
            restLocalPos = transform.InverseTransformPoint(lid.transform.position);
        }

        public bool IsOpen => Vector3.Distance(transform.InverseTransformPoint(lid.transform.position), restLocalPos) > openDistance;

        void Update()
        {
            if (!Armed || Fired) return;
            if (lid.IsHeld && IsOpen) Fire();
        }
    }
}
