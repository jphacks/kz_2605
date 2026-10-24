using System;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 手でつかめる物（貝殻・料理・巻物・玉手箱のふたなど）。
    /// 物語に関係ない小物にも付けて、自由に触れられるようにする（03 機能仕様書「小物への干渉」）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        [Tooltip("つかまれるまで物理を止めておく（玉手箱のふたなど、置いた場所から動かしたくない物）")]
        public bool kinematicUntilGrabbed;

        public HandGrabber HeldBy { get; private set; }
        public bool IsHeld => HeldBy != null;
        public bool WasEverGrabbed { get; private set; }
        public event Action<Grabbable> Grabbed;

        Rigidbody rb;
        Vector3 localPosOffset;
        Quaternion localRotOffset;
        bool initialKinematic;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            initialKinematic = rb.isKinematic || kinematicUntilGrabbed;
            rb.isKinematic = initialKinematic;
        }

        public void OnGrab(HandGrabber hand)
        {
            if (HeldBy != null) HeldBy.ReleaseHeld(false);
            HeldBy = hand;
            WasEverGrabbed = true;
            rb.isKinematic = true;
            var ht = hand.transform;
            localPosOffset = Quaternion.Inverse(ht.rotation) * (transform.position - ht.position);
            localRotOffset = Quaternion.Inverse(ht.rotation) * transform.rotation;
            Grabbed?.Invoke(this);
        }

        public void FollowHand()
        {
            if (HeldBy == null) return;
            var ht = HeldBy.transform;
            transform.SetPositionAndRotation(ht.position + ht.rotation * localPosOffset, ht.rotation * localRotOffset);
        }

        public void OnRelease(Vector3 velocity)
        {
            HeldBy = null;
            rb.isKinematic = false;
            rb.linearVelocity = velocity;
        }

        public void ForceRelease()
        {
            if (HeldBy != null) HeldBy.ReleaseHeld(false);
        }

        /// <summary>リセット時：最初の物理状態に戻す。</summary>
        public void ResetPhysics()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            HeldBy = null;
            WasEverGrabbed = false;
            rb.isKinematic = initialKinematic;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
