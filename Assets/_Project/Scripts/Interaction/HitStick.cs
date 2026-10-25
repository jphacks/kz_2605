using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>叩くのに使える棒（浜辺の流木）。振った速さを測り、TouchTrigger（requireStick）が判定に使う。叩いたときの音も鳴らす。</summary>
    public class HitStick : MonoBehaviour
    {
        /// <summary>この速さ以上で当たったら音を鳴らす（そっと触れただけでは鳴らさない）。</summary>
        public const float SoundSpeed = 0.35f;

        public float Speed { get; private set; }

        static AudioClip hitClip;
        Vector3 lastPos;
        float lastSoundTime = -1;

        void OnEnable()
        {
            lastPos = transform.position;
            Speed = 0;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;
            // 手に追従した後の位置で測る。投げたときも同じ式で速さが出る
            Speed = Mathf.Lerp(Speed, (transform.position - lastPos).magnitude / dt, 0.5f);
            lastPos = transform.position;
        }

        /// <summary>当たった場所で「コツッ・ゴン」と鳴らす。強く叩くほど大きく、毎回少し高さを変える。</summary>
        public void PlayHit(Vector3 point)
        {
            if (Speed < SoundSpeed || Time.time - lastSoundTime < 0.12f) return;
            if (hitClip == null) hitClip = Resources.Load<AudioClip>("Audio/StickHit");
            if (hitClip == null) return;
            lastSoundTime = Time.time;
            var go = new GameObject("Stick hit sound");
            go.transform.position = point;
            var source = go.AddComponent<AudioSource>();
            source.clip = hitClip;
            source.spatialBlend = 1;
            source.minDistance = 1.5f;
            source.volume = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(SoundSpeed, 2.5f, Speed));
            source.pitch = Random.Range(0.92f, 1.08f);
            source.Play();
            Destroy(go, hitClip.length / source.pitch + 0.1f);
        }
    }
}
