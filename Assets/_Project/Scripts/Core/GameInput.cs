using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JPHACKS2026
{
    /// <summary>
    /// Questコントローラーの入力をコードで定義する（InputActionAssetに依存しない）。
    /// usage（{Grip}など）でバインドするので、OpenXRのTouch系プロファイルならどれでも動く。
    /// </summary>
    public static class GameInput
    {
        public static InputAction LeftGrip { get; private set; }
        public static InputAction RightGrip { get; private set; }
        public static InputAction LeftTrigger { get; private set; }
        public static InputAction RightTrigger { get; private set; }
        public static InputAction LeftStick { get; private set; }
        public static InputAction RightStick { get; private set; }
        public static InputAction Menu { get; private set; }

        static bool initialized;

        public static void Init()
        {
            if (initialized) return;
            initialized = true;

            LeftGrip = Axis("<XRController>{LeftHand}/{Grip}");
            RightGrip = Axis("<XRController>{RightHand}/{Grip}");
            LeftTrigger = Axis("<XRController>{LeftHand}/{Trigger}");
            RightTrigger = Axis("<XRController>{RightHand}/{Trigger}");

            LeftStick = new InputAction("LeftStick", InputActionType.Value, expectedControlType: "Vector2");
            LeftStick.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
            LeftStick.Enable();

            RightStick = new InputAction("RightStick", InputActionType.Value, expectedControlType: "Vector2");
            RightStick.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");
            RightStick.Enable();

            // 右コントローラーのメニュー（Oculusボタン）はシステム予約なので、左のメニューボタンを使う
            Menu = new InputAction("Menu", InputActionType.Button);
            Menu.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            Menu.AddBinding("<Keyboard>/escape");
            Menu.AddBinding("<Keyboard>/tab");
            Menu.Enable();
        }

        static InputAction Axis(string path)
        {
            var a = new InputAction(path, InputActionType.Value, expectedControlType: "Axis");
            a.AddBinding(path);
            a.Enable();
            return a;
        }

        public static float Grip(HandSide side) => (side == HandSide.Left ? LeftGrip : RightGrip).ReadValue<float>();
        public static float Trigger(HandSide side) => (side == HandSide.Left ? LeftTrigger : RightTrigger).ReadValue<float>();

        /// <summary>自動テスト用：ここに積んだ番号を、数字キーが押されたものとして順に返す。</summary>
        public static readonly Queue<int> DebugQueue = new();

        /// <summary>デスクトップ確認用：数字キー1〜3で選択肢を直接選ぶ。押されていなければ-1。</summary>
        public static int DebugNumberKeyDown()
        {
            if (DebugQueue.Count > 0) return DebugQueue.Dequeue();
            var kb = Keyboard.current;
            if (kb == null) return -1;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
            return -1;
        }

        public static bool DebugSkipDown()
        {
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
        }
    }

    public enum HandSide { Left, Right }
}
