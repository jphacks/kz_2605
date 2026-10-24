using UnityEngine;
using UnityEngine.InputSystem;

namespace JPHACKS2026
{
    /// <summary>
    /// HMD・コントローラーの位置と向きをローカル座標に反映する。
    /// 手は「コントローラーを握っている手」を基準にする：位置は握った拳の中心（グリップ姿勢）、
    /// 向きは手の向き＝指し示す方向（エイム姿勢）。グリップ姿勢の向きは持ち手の軸に沿って傾いているため使わない。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TrackedPose : MonoBehaviour
    {
        public enum Source { Head, LeftHand, RightHand }
        public Source source;
        [Tooltip("手の向きの微調整（度）。エイム姿勢からの追加回転")]
        public Vector3 handRotationOffset;

        InputAction position;
        InputAction rotation;
        InputAction aimRotation;
        InputAction tracked;

        /// <summary>XRデバイスから実際に値が来ているか。</summary>
        public bool IsTracking { get; private set; }

        void Awake()
        {
            string device = source switch
            {
                Source.Head => "<XRHMD>",
                Source.LeftHand => "<XRController>{LeftHand}",
                _ => "<XRController>{RightHand}",
            };
            string posName = source == Source.Head ? "centerEyePosition" : "devicePosition";
            string rotName = source == Source.Head ? "centerEyeRotation" : "deviceRotation";

            position = new InputAction(type: InputActionType.Value, binding: $"{device}/{posName}", expectedControlType: "Vector3");
            rotation = new InputAction(type: InputActionType.Value, binding: $"{device}/{rotName}", expectedControlType: "Quaternion");
            tracked = new InputAction(type: InputActionType.Button, binding: $"{device}/isTracked");
            if (source != Source.Head)
                aimRotation = new InputAction(type: InputActionType.Value, binding: $"{device}/pointerRotation", expectedControlType: "Quaternion");
        }

        void OnEnable()
        {
            position.Enable();
            rotation.Enable();
            aimRotation?.Enable();
            tracked.Enable();
            Application.onBeforeRender += Apply;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= Apply;
            position.Disable();
            rotation.Disable();
            aimRotation?.Disable();
            tracked.Disable();
        }

        void Update() => Apply();

        void Apply()
        {
            if (DesktopSimulator.Active) { IsTracking = false; return; }
            IsTracking = position.activeControl != null;
            if (!IsTracking) return;
            transform.localPosition = position.ReadValue<Vector3>();
            if (aimRotation != null && aimRotation.activeControl != null)
                transform.localRotation = aimRotation.ReadValue<Quaternion>() * Quaternion.Euler(handRotationOffset);
            else
                transform.localRotation = rotation.ReadValue<Quaternion>();
        }
    }
}
