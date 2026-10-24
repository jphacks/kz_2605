using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace JPHACKS2026
{
    /// <summary>
    /// プレイヤー本体（浦島太郎）。スティック移動・スナップ回転・場所の移動・亀に乗っての自動移動を担当。
    /// 移動はテレポートではなくスティック移動が基本（03 機能仕様書）。
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        public Transform cameraOffset;
        public Transform head;
        public Hand leftHand;
        public Hand rightHand;

        [Header("Locomotion")]
        public float moveSpeed = 1.6f;
        public float snapTurnDegrees = 30f;

        public Transform Head => head;
        public bool LocomotionEnabled { get; set; } = true;

        /// <summary>亀に乗っている間など、このTransformに追従する。</summary>
        public Transform RideAnchor { get; set; }

        bool snapReady = true;

        void Start()
        {
            SetFloorOrigin();
        }

        static void SetFloorOrigin()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }

        public IEnumerable<Hand> Hands
        {
            get { yield return leftHand; yield return rightHand; }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.IsPaused) return;

            if (RideAnchor != null)
            {
                transform.position = RideAnchor.position;
                return;
            }
            if (!LocomotionEnabled) return;

            Vector2 move = DesktopSimulator.Active ? DesktopSimulator.Move() : GameInput.LeftStick.ReadValue<Vector2>();
            if (move.sqrMagnitude > 0.04f)
            {
                var fwd = head.forward; fwd.y = 0; fwd.Normalize();
                var right = head.right; right.y = 0; right.Normalize();
                var delta = (fwd * move.y + right * move.x) * (moveSpeed * Time.deltaTime);
                transform.position += delta;
                ClampToArea();
            }

            if (!DesktopSimulator.Active)
            {
                float turn = GameInput.RightStick.ReadValue<Vector2>().x;
                if (snapReady && Mathf.Abs(turn) > 0.7f)
                {
                    transform.RotateAround(head.position, Vector3.up, Mathf.Sign(turn) * snapTurnDegrees);
                    snapReady = false;
                }
                else if (Mathf.Abs(turn) < 0.3f) snapReady = true;
            }
        }

        void ClampToArea()
        {
            var area = GameManager.Instance != null ? GameManager.Instance.CurrentArea : null;
            if (area == null) return;
            var clamped = area.ClampToWalkArea(head.position);
            var d = clamped - head.position;
            d.y = 0;
            transform.position += d;
        }

        /// <summary>頭の位置と向きが目標地点に合うようにリグ全体を移動する。</summary>
        public void TeleportTo(Transform target)
        {
            RideAnchor = null;
            float headYaw = head.eulerAngles.y;
            float targetYaw = target.eulerAngles.y;
            transform.RotateAround(head.position, Vector3.up, targetYaw - headYaw);
            var offset = target.position - head.position;
            offset.y = target.position.y - transform.position.y;
            transform.position += offset;
            foreach (var h in Hands) h.ResetVelocity();
        }

        public void SetAgedHands(bool aged)
        {
            foreach (var h in Hands) h.SetAged(aged);
        }
    }
}
