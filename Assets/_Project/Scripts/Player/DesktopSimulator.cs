using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace JPHACKS2026
{
    /// <summary>
    /// HMDなしでPlayモード確認するための簡易操作。XRデバイスが無効なときだけ有効になる。
    /// 右ドラッグ：見回す / WASD：移動 / マウス：右手を動かす / 左クリック：UI決定
    /// 左クリック：右手で持つ・離して置く / G・F：従来のグリップ / H：右手を口元へ
    /// 1〜3：選択肢を直接選ぶ / Space：台詞送り / Esc・Tab：ポーズ
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class DesktopSimulator : MonoBehaviour
    {
        public static bool Active { get; private set; }

        public PlayerRig rig;
        public float eyeHeight = 1.6f;
        public float lookSpeed = 0.15f;

        float yaw, pitch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = false;
        float checkTimer = 1.5f;
        bool decided;

        void Update()
        {
            if (!decided)
            {
                // XRの初期化を少し待ってから判定する
                checkTimer -= Time.unscaledDeltaTime;
                if (XRSettings.isDeviceActive && rig.head.GetComponent<TrackedPose>().IsTracking) { decided = true; Active = false; return; }
                if (checkTimer > 0) return;
                decided = true;
                Active = !XRSettings.isDeviceActive;
                if (Active)
                {
                    Debug.Log("[DesktopSimulator] XRデバイスが見つからないため、デスクトップ操作で動かします。");
                    rig.head.localPosition = new Vector3(0, eyeHeight, 0);
                    rig.head.localRotation = Quaternion.identity;
                }
            }
            if (!Active) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                yaw += d.x * lookSpeed;
                pitch = Mathf.Clamp(pitch - d.y * lookSpeed, -80, 80);
                rig.head.localRotation = Quaternion.Euler(pitch, yaw, 0);
            }

            UpdateHands();
        }

        void UpdateHands()
        {
            var cam = rig.head.GetComponent<Camera>();
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // 左手は視界の左下に固定
            rig.leftHand.transform.position = rig.head.TransformPoint(new Vector3(-0.22f, -0.25f, 0.45f));
            rig.leftHand.transform.rotation = rig.head.rotation;

            Vector3 target;
            if (kb != null && kb.hKey.isPressed)
            {
                target = rig.head.TransformPoint(new Vector3(0, -0.08f, 0.12f));
            }
            else
            {
                var ray = mouse != null ? cam.ScreenPointToRay(mouse.position.ReadValue()) : new Ray(rig.head.position, rig.head.forward);
                float dist = 0.6f;
                if (Physics.Raycast(ray, out var hit, 1.3f, ~0, QueryTriggerInteraction.Ignore)) dist = Mathf.Max(0.25f, hit.distance - 0.03f);
                target = ray.GetPoint(dist);
            }
            var rh = rig.rightHand.transform;
            rh.position = Vector3.Lerp(rh.position, target, 1 - Mathf.Exp(-25f * Time.unscaledDeltaTime));
            rh.rotation = rig.head.rotation;
        }

        public static Vector2 Move()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            var v = Vector2.zero;
            if (kb.wKey.isPressed) v.y += 1;
            if (kb.sKey.isPressed) v.y -= 1;
            if (kb.dKey.isPressed) v.x += 1;
            if (kb.aKey.isPressed) v.x -= 1;
            return v;
        }

        public void LookAt(Vector3 worldPoint)
        {
            rig.head.rotation = Quaternion.LookRotation(worldPoint - rig.head.position);
            yaw = rig.head.localEulerAngles.y;
            pitch = Mathf.DeltaAngle(0, rig.head.localEulerAngles.x);
        }

        public static float Grip(HandSide side)
        {
            var kb = Keyboard.current;
            if (kb == null) return 0;
            return (side == HandSide.Right ? kb.gKey.isPressed : kb.fKey.isPressed) ? 1 : 0;
        }

        public static float Trigger(HandSide side)
        {
            var mouse = Mouse.current;
            if (side != HandSide.Right || mouse == null) return 0;
            return mouse.leftButton.isPressed ? 1 : 0;
        }
    }
}
