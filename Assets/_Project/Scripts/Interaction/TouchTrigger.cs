using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>手が触れたら成立。minSpeedを上げると「追い払う」「叩く」、requireGripで「手を置いて握る」。</summary>
    public class TouchTrigger : BranchTrigger
    {
        public float minSpeed;
        public bool requireGrip;

        readonly HashSet<Hand> touching = new();

        void OnTriggerEnter(Collider other)
        {
            var h = other.GetComponentInParent<Hand>();
            if (h != null) touching.Add(h);
        }

        void OnTriggerExit(Collider other)
        {
            var h = other.GetComponentInParent<Hand>();
            if (h != null) touching.Remove(h);
        }

        void OnDisable() => touching.Clear();

        void Update()
        {
            if (!Armed || Fired) return;
            foreach (var h in touching)
            {
                if (h == null) continue;
                if (h.Speed < minSpeed) continue;
                if (requireGrip && !h.GripPressed) continue;
                Fire();
                return;
            }
        }
    }
}
