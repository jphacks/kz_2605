using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 手が触れたら成立。minSpeedで「追い払う」「叩く」、requireGrip（互換用の名前）でトリガー操作も要求する。
    /// requireStick のときは手ではなく木の棒（HitStick）が minSpeed 以上の速さで当たったら成立する。
    /// </summary>
    public class TouchTrigger : BranchTrigger
    {
        public float minSpeed;
        public bool requireGrip;
        public bool requireStick;

        readonly HashSet<Hand> touching = new();
        readonly HashSet<HitStick> sticks = new();

        void OnTriggerEnter(Collider other)
        {
            var h = other.GetComponentInParent<Hand>();
            if (h != null) touching.Add(h);
            var s = other.GetComponentInParent<HitStick>();
            if (s != null) sticks.Add(s);
        }

        void OnTriggerExit(Collider other)
        {
            var h = other.GetComponentInParent<Hand>();
            if (h != null) touching.Remove(h);
            var s = other.GetComponentInParent<HitStick>();
            if (s != null) sticks.Remove(s);
        }

        void OnDisable()
        {
            touching.Clear();
            sticks.Clear();
        }

        void Update()
        {
            if (!Armed || Fired) return;
            if (requireStick)
            {
                foreach (var s in sticks)
                {
                    if (s == null || s.Speed < minSpeed) continue;
                    Fire();
                    return;
                }
                return;
            }
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
