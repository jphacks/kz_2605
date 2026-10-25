using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 乙姫のモデルのアニメーション（Mixamo から取り込んだ Humanoid）を場面に合わせて切り替える。
    /// ふだんは待機、乙姫のセリフ中は話す、動いている間は歩く、手を取る選択肢の間は手を差し出す。
    /// 決まったセリフでは身ぶり（お辞儀・手を差し出す）をする。Animator の状態名は OtohimeAnimationSetup と合わせる。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class OtohimeMotion : MonoBehaviour
    {
        public const string Idle = "Idle", Talk = "Talk", Walk = "Walk", Reach = "Reach", Bow = "Bow";

        // このセリフを読み始めたときにする身ぶり（行の id → 状態名）
        static readonly Dictionary<string, string> LineGestures = new()
        {
            { "story_014", Bow },     // 亀を助けてくださってありがとうございました。
            { "story_025", Reach },   // では、この玉手箱をお持ちください。
        };

        public string speakerName = "乙姫";
        [Tooltip("待ち受け中は手を差し出しておく分岐（乙姫の手を取る）")]
        public BranchTrigger reachWhileArmed;
        [Tooltip("この速さ（m/秒、水平方向）以上で動いていたら歩く")]
        public float walkSpeed = 0.15f;
        public float crossFade = 0.3f;

        Animator animator;
        Transform root;
        Vector3 lastPos;
        float speed;
        string current;
        string gesture;
        string gestureLine;

        void Awake()
        {
            animator = GetComponent<Animator>();
            var character = GetComponentInParent<SimpleCharacter>();
            root = character != null ? character.transform : transform;
        }

        void OnEnable()
        {
            lastPos = root.position;
            speed = 0;
            current = null;
            gesture = null;
            gestureLine = null;
        }

        void Update()
        {
            if (Time.deltaTime <= 0) return;
            var delta = root.position - lastPos; delta.y = 0;   // せり上がる登場は歩きにしない
            speed = Mathf.Lerp(speed, delta.magnitude / Time.deltaTime, 0.3f);
            lastPos = root.position;

            var state = Desired();
            if (state == current) return;
            current = state;
            animator.CrossFadeInFixedTime(state, crossFade);
        }

        string Desired()
        {
            var narration = GameManager.Instance != null ? GameManager.Instance.narration : null;
            var line = narration != null ? narration.Current : null;
            bool speaking = line.HasValue && line.Value.speaker == speakerName;

            // 身ぶりは決まったセリフの読み始めに1回だけ。お辞儀は最後までしてから戻る
            if (speaking && line.Value.id != gestureLine)
            {
                gestureLine = line.Value.id;
                gesture = gestureLine != null && LineGestures.TryGetValue(gestureLine, out var g) ? g : null;
            }
            if (!speaking) gestureLine = null;
            if (gesture == Bow && current == Bow && Finished(Bow)) gesture = null;
            if (gesture == Reach && !speaking) gesture = null;

            if (reachWhileArmed != null && reachWhileArmed.Armed) return Reach;
            if (gesture != null) return gesture;
            if (speed > walkSpeed) return Walk;
            return speaking ? Talk : Idle;
        }

        bool Finished(string state)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(state) && !animator.IsInTransition(0) && info.normalizedTime >= 0.95f;
        }
    }
}
