using UnityEngine;
using UnityEngine.InputSystem;

namespace JPHACKS2026
{
    /// <summary>
    /// 右手（または左手）からレイを出してVRButtonを選ぶ。トリガーで決定。
    /// メニューが表示されているときだけレイを見せる。デスクトップ時はマウスで代用。
    /// </summary>
    public class UIPointer : MonoBehaviour
    {
        public Hand hand;
        public LineRenderer line;
        public float maxDistance = 6f;

        VRButton hovered;
        bool wasPressed;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Update()
        {
            bool menuVisible = VRButton.ActiveCount > 0;
            if (line != null) line.enabled = menuVisible && !DesktopSimulator.Active;
            if (!menuVisible)
            {
                SetHovered(null);
                wasPressed = hand.TriggerPressed;
                return;
            }

            Ray ray;
            if (DesktopSimulator.Active && Mouse.current != null)
                ray = GameManager.Instance.Rig.Head.GetComponent<Camera>().ScreenPointToRay(Mouse.current.position.ReadValue());
            else
                ray = new Ray(hand.transform.position, hand.transform.forward);

            VRButton best = null;
            float bestDist = maxDistance;
            int n = Physics.RaycastNonAlloc(ray, hits, maxDistance, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var b = hits[i].collider.GetComponent<VRButton>();
                if (b != null && hits[i].distance < bestDist) { best = b; bestDist = hits[i].distance; }
            }
            SetHovered(best);

            if (line != null)
            {
                line.SetPosition(0, ray.origin);
                line.SetPosition(1, ray.GetPoint(best != null ? bestDist : 1.5f));
            }

            bool pressed = hand.TriggerPressed;
            if (pressed && !wasPressed && hovered != null) hovered.Click();
            wasPressed = pressed;
        }

        void SetHovered(VRButton b)
        {
            if (hovered == b) return;
            if (hovered != null) hovered.SetHover(false);
            hovered = b;
            if (hovered != null) hovered.SetHover(true);
        }
    }
}
