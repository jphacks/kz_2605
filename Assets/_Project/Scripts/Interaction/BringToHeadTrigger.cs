using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>つかんだ物を口元に運んだら成立（料理を食べる）。食べた物は消える。</summary>
    public class BringToHeadTrigger : BranchTrigger
    {
        public Grabbable[] targets;
        public float distance = 0.22f;
        public AudioClip eatSound;

        void Update()
        {
            if (!Armed || Fired) return;
            var mouth = Head.position - Head.up * 0.08f;
            foreach (var g in targets)
            {
                if (g == null || !g.IsHeld) continue;
                if (Vector3.Distance(g.transform.position, mouth) > distance) continue;
                g.ForceRelease();
                g.gameObject.SetActive(false);
                if (eatSound != null) AudioSource.PlayClipAtPoint(eatSound, mouth);
                Fire();
                return;
            }
        }
    }
}
