using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// タイトル・浜辺・海中・竜宮城・村などの「場所」。同時にアクティブになるのは1つだけ。
    /// 環境光・フォグ・太陽光・背景色をまとめて持ち、入るたびに適用する。
    /// </summary>
    public class Area : MonoBehaviour
    {
        public string areaId;
        public Transform spawnPoint;
        [Tooltip("プレイヤーの頭がこの範囲（XZ、ローカル座標）から出ないように制限する")]
        public Vector2 walkHalfExtents = new(8, 8);
        public Vector2 walkCenter;

        [Header("Environment")]
        public Color skyColor = new(0.55f, 0.75f, 0.95f);
        public Color ambientColor = new(0.6f, 0.6f, 0.65f);
        public Color sunColor = Color.white;
        public float sunIntensity = 1.1f;
        public Vector3 sunEuler = new(50, -30, 0);
        public bool fog;
        public Color fogColor = Color.gray;
        public float fogDensity = 0.02f;

        bool initialized;
        Renderer[] renderers;
        Material[][] materials;

        public void Init()
        {
            if (initialized) return;
            if (areaId == "title") TitleRoomDetails.Build(this);
            SceneryDetails.Build(this);
            OceanAmbience.Build(this);
            initialized = true;
            foreach (var r in GetComponentsInChildren<Resettable>(true)) r.Capture();
            // ホラー演出でエリア全体の色を変えるので、全Rendererのマテリアルを覚えておく
            renderers = GetComponentsInChildren<Renderer>(true);
            materials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++) materials[i] = renderers[i].sharedMaterials;
        }

        public void ResetArea()
        {
            Init();
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterials = materials[i];
            foreach (var r in GetComponentsInChildren<Resettable>(true)) r.ResetState();
            foreach (var t in GetComponentsInChildren<BranchTrigger>(true)) t.Disarm();
        }

        public void ApplyEnvironment()
        {
            var gm = GameManager.Instance;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            if (gm != null && gm.sun != null)
            {
                gm.sun.color = sunColor;
                gm.sun.intensity = sunIntensity;
                gm.sun.transform.rotation = Quaternion.Euler(sunEuler);
                gm.sun.shadows = areaId == "title" ? LightShadows.Hard : LightShadows.None;
                gm.sun.shadowBias = .015f;
                gm.sun.shadowNormalBias = .12f;
            }
            if (gm != null && gm.Rig != null && gm.Rig.Head != null)
            {
                var cam = gm.Rig.Head.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = skyColor;
                }
            }
        }

        public Vector3 ClampToWalkArea(Vector3 worldPos)
        {
            var local = transform.InverseTransformPoint(worldPos);
            local.x = Mathf.Clamp(local.x, walkCenter.x - walkHalfExtents.x, walkCenter.x + walkHalfExtents.x);
            local.z = Mathf.Clamp(local.z, walkCenter.y - walkHalfExtents.y, walkCenter.y + walkHalfExtents.y);
            return transform.TransformPoint(local);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(walkCenter.x, 0, walkCenter.y), new Vector3(walkHalfExtents.x * 2, 0.01f, walkHalfExtents.y * 2));
        }
    }
}
