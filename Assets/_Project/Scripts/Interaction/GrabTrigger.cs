using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>対象の物をつかんだら成立。</summary>
    public class GrabTrigger : BranchTrigger
    {
        public Grabbable[] targets;

        void Update()
        {
            if (!Armed || Fired) return;
            foreach (var g in targets)
                if (g != null && g.IsHeld) { Fire(); return; }
        }
    }
}
