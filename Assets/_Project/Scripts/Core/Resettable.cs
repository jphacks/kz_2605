using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 「直前の選択肢に戻る」ときに、位置・大きさ・表示状態・マテリアルを初期状態に戻す。
    /// エンディング演出で動かしたり色を変えたりするオブジェクトに付ける。
    /// </summary>
    public class Resettable : MonoBehaviour
    {
        Vector3 pos;
        Quaternion rot;
        Vector3 scale;
        bool active;
        Renderer[] renderers;
        Material[][] materials;
        bool captured;

        void Awake() => Capture();

        public void Capture()
        {
            if (captured) return;
            captured = true;
            pos = transform.localPosition;
            rot = transform.localRotation;
            scale = transform.localScale;
            active = gameObject.activeSelf;
            renderers = GetComponentsInChildren<Renderer>(true);
            materials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++) materials[i] = renderers[i].sharedMaterials;
        }

        public void ResetState()
        {
            if (!captured) return;
            var grab = GetComponent<Grabbable>();
            if (grab != null) grab.ForceRelease();

            transform.localPosition = pos;
            transform.localRotation = rot;
            transform.localScale = scale;
            gameObject.SetActive(active);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterials = materials[i];

            var rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (grab != null) grab.ResetPhysics();

            var swimmer = GetComponent<Swimmer>();
            if (swimmer != null) swimmer.SpeedMultiplier = 1f;

            var character = GetComponent<SimpleCharacter>();
            if (character != null) character.ResetBehaviour();
        }
    }
}
