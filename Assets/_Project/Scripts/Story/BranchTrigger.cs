using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 分岐の条件になる「行動」の検出器の基底クラス。
    /// Arm()されている間だけ検出し、行動が成立したらFiredがtrueになる。
    /// </summary>
    public abstract class BranchTrigger : MonoBehaviour
    {
        [Tooltip("待ち受け中だけ表示する目印（光る輪など）")]
        public GameObject armedVisual;

        public bool Armed { get; private set; }
        public bool Fired { get; private set; }

        protected virtual void Awake()
        {
            if (armedVisual != null) armedVisual.SetActive(false);
        }

        public void Arm()
        {
            Armed = true;
            Fired = false;
            if (armedVisual != null) armedVisual.SetActive(true);
            OnArmed();
        }

        public void Disarm()
        {
            Armed = false;
            if (armedVisual != null) armedVisual.SetActive(false);
        }

        protected void Fire()
        {
            if (!Armed || Fired) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            Fired = true;
        }

        protected virtual void OnArmed() { }

        protected static Transform Head => GameManager.Instance.Rig.Head;
    }
}
