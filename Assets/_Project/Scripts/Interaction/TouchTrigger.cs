using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>手が触れたら成立。minSpeedで「追い払う」「叩く」、requireGrip（互換用の名前）でトリガー操作も要求する。</summary>
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
                if (requireGrip && !h.TriggerPressed) continue;
                Fire();
                return;
            }
        }
    }
}
