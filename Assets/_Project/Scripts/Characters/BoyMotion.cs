using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 浜辺の子ども（少年のモデル）のアニメーションを切り替える。ふだんは待機、動いている間は歩く（速いほど速く再生して走って見せる）、
    /// 亀をいじめている間は手を突き出す。Animator の状態名は BoyModelSetup と合わせる。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class BoyMotion : MonoBehaviour
    {
        public const string Idle = "Idle", Walk = "Walk", Reach = "Reach";

        [Tooltip("この速さ（m/秒、水平方向）以上で動いていたら歩く")]
        public float walkSpeed = 0.15f;
        [Tooltip("歩きのアニメーションが等倍に見える速さ（m/秒）")]
        public float walkClipSpeed = 1.2f;
        public float crossFade = 0.2f;
        [HideInInspector] public bool bullying;

        Animator animator;
        Transform root;
        Vector3 lastPos;
        float speed;
        string current;

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
            bullying = false;
        }

        void Update()
        {
            if (Time.deltaTime <= 0) return;
            var delta = root.position - lastPos; delta.y = 0;
            speed = Mathf.Lerp(speed, delta.magnitude / Time.deltaTime, 0.3f);
            lastPos = root.position;

            var state = bullying ? Reach : speed > walkSpeed ? Walk : Idle;
            animator.speed = state == Walk ? Mathf.Clamp(speed / walkClipSpeed, 1f, 2.5f) : 1f;
            if (state == current) return;
            current = state;
            animator.CrossFadeInFixedTime(state, crossFade);
        }
    }
}
