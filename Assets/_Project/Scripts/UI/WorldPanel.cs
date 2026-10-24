using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// プレイヤーの正面に浮かぶパネル。視線が大きく外れたら、ゆっくり正面に回り込む（酔いにくい追従）。
    /// </summary>
    public class WorldPanel : MonoBehaviour
    {
        public float distance = 1.4f;
        public float heightOffset = -0.1f;
        public float followAngle = 35f;
        public bool continuousFollow;
        public float followSharpness = 3f;

        Vector3 targetPos;
        bool moving;

        public void SnapToView()
        {
            targetPos = ComputeTarget();
            transform.position = targetPos;
            Face();
        }

        Vector3 ComputeTarget()
        {
            var head = GameManager.Instance.Rig.Head;
            var fwd = head.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 0.001f) fwd = head.up;
            fwd.Normalize();
            return head.position + fwd * distance + Vector3.up * heightOffset;
        }

        void Face()
        {
            var head = GameManager.Instance.Rig.Head;
            var d = transform.position - head.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }

        void LateUpdate()
        {
            if (GameManager.Instance == null) return;
            var head = GameManager.Instance.Rig.Head;
            var toPanel = transform.position - head.position; toPanel.y = 0;
            var fwd = head.forward; fwd.y = 0;
            if (continuousFollow || moving || Vector3.Angle(toPanel, fwd) > followAngle || toPanel.magnitude > distance * 2f)
            {
                targetPos = ComputeTarget();
                moving = true;
            }
            if (moving)
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, 1 - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
                if (!continuousFollow && Vector3.Distance(transform.position, targetPos) < 0.02f) moving = false;
            }
            Face();
        }
    }
}
