using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>手を物に近づけてグリップを握るとつかみ、離すと手放す（投げられる）。</summary>
    [RequireComponent(typeof(Hand))]
    public class HandGrabber : MonoBehaviour
    {
        public float grabRadius = 0.09f;

        public Grabbable Held { get; private set; }

        Hand hand;
        bool wasPressed;
        readonly Collider[] overlap = new Collider[16];

        void Awake() => hand = GetComponent<Hand>();

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.IsPaused) return;

            bool pressed = hand.GripPressed;
            if (pressed && !wasPressed) TryGrab();
            else if (!pressed && wasPressed && Held != null) ReleaseHeld(true);
            wasPressed = pressed;
        }

        void LateUpdate()
        {
            if (Held != null)
            {
                if (!Held.gameObject.activeInHierarchy) { Held = null; return; }
                Held.FollowHand();
            }
        }

        void TryGrab()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, grabRadius, overlap, ~0, QueryTriggerInteraction.Ignore);
            Grabbable best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var g = overlap[i].GetComponentInParent<Grabbable>();
                if (g == null || !g.enabled) continue;
                float d = (overlap[i].ClosestPoint(transform.position) - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = g; }
            }
            if (best == null) return;
            Held = best;
            best.OnGrab(this);
        }

        public void ReleaseHeld(bool throwIt)
        {
            if (Held == null) return;
            var g = Held;
            Held = null;
            g.OnRelease(throwIt ? hand.Velocity : Vector3.zero);
        }
    }
}
