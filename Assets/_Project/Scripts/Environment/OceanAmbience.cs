using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>浜辺・海中でのみ波音を流す。エリアの有効・無効と一緒に再生・停止する。</summary>
    [RequireComponent(typeof(AudioSource))]
    public class OceanAmbience : MonoBehaviour
    {
        AudioSource source;
        float targetVolume;

        public static void Build(Area area)
        {
            if (area.areaId != "beach" && area.areaId != "sea") return;
            if (area.transform.Find("Ocean ambience") != null) return;
            var clip = Resources.Load<AudioClip>("Audio/OceanWaves");
            if (clip == null) { Debug.LogWarning("[OceanAmbience] 波音素材が見つかりません"); return; }
            var go = new GameObject("Ocean ambience");
            go.SetActive(false); go.transform.SetParent(area.transform, false);
            var audio = go.AddComponent<AudioSource>();
            audio.clip = clip; audio.loop = true; audio.playOnAwake = false;
            audio.spatialBlend = 0; audio.volume = 0; audio.priority = 180;
            bool underwater = area.areaId == "sea";
            if (underwater)
            {
                var filter = go.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = 850; filter.lowpassResonanceQ = 1;
            }
            var ambience = go.AddComponent<OceanAmbience>();
            ambience.targetVolume = underwater ? .14f : .24f;
            go.SetActive(true);
        }

        void Awake() => source = GetComponent<AudioSource>();
        void OnEnable() { source.volume = 0; source.Play(); }
        void Update() => source.volume = Mathf.MoveTowards(source.volume, targetVolume, Time.deltaTime * .18f);
        void OnDisable() { if (source != null) source.Stop(); }
    }
}
