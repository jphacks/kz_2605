using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 乙姫の羽衣（領巾）。頭の上に大きく輪をかけ、両肩の後ろから腰の横へ垂れる薄い帯を、毎フレーム形を作り直して波打たせる。
    /// 頭が大きいデフォルメ体型なので、起動時に頭（と髪）の大きさを測り、その外側を通るように道筋を決める。
    /// 人物（+Z が正面）の子として置き、胸の骨が動いた分だけ一緒に動く。長さ・幅はメートル。
    /// </summary>
    [DefaultExecutionOrder(-100)]   // エリアが元の素材を覚える（Area.Init）より前に作っておく
    public class Hagoromo : MonoBehaviour
    {
        const int Segments = 64;

        [Tooltip("一緒に動く骨（胸）。未設定なら動かない")]
        public Transform anchorBone;
        [Tooltip("頭の大きさを測る見た目（頭・髪）。未設定ならモデル全体")]
        public Renderer[] headRenderers;
        public Material material;
        public float width = 0.11f;
        [Tooltip("揺れの大きさ。端ほど大きく揺れる")]
        public float flutter = 1f;
        public Color rootColor = new(1f, 0.72f, 0.84f, 0.85f);
        public Color tipColor = new(1f, 0.97f, 0.98f, 0.35f);

        // 帯の通り道（人物のローカル座標）：左の腰 → 左肩の後ろ → 頭上の輪 → 右肩の後ろ → 右の腰
        Vector3[] path;
        Vector3 anchorRest;
        Transform character;
        Transform ribbon;
        Mesh mesh;
        Vector3[] vertices;
        Vector3[] normals;
        float time;

        void Awake()
        {
            var sc = GetComponentInParent<SimpleCharacter>();
            character = sc != null ? sc.transform : transform;
            if (anchorBone != null) anchorRest = character.InverseTransformPoint(anchorBone.position);
            path = BuildPath();

            var go = new GameObject("Hagoromo ribbon");
            ribbon = go.transform;
            ribbon.SetParent(character, false);
            mesh = new Mesh { name = "Hagoromo" };
            mesh.MarkDynamic();
            int n = (Segments + 1) * 2;
            vertices = new Vector3[n];
            normals = new Vector3[n];
            var colors = new Color[n];
            var uvs = new Vector2[n];
            var tris = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float s = i / (float)Segments;
                // 中央（頭上）ほど濃い桜色、両端へ向かって白く透ける
                var c = Color.Lerp(rootColor, tipColor, Mathf.Abs(s - 0.5f) * 2f);
                colors[i * 2] = colors[i * 2 + 1] = c;
                uvs[i * 2] = new Vector2(s, 0);
                uvs[i * 2 + 1] = new Vector2(s, 1);
                if (i == Segments) break;
                int v = i * 2, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = tris;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material != null ? material : DefaultMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Build();
        }

        void LateUpdate()
        {
            time += Time.deltaTime;
            Build();
        }

        /// <summary>頭の外側を回る道筋。頭の幅・高さ・奥行きから決める。</summary>
        Vector3[] BuildPath()
        {
            var renderers = headRenderers != null && headRenderers.Length > 0 ? headRenderers : GetComponentsInChildren<Renderer>();
            var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = character.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2 - Vector3.one));
                    min = Vector3.Min(min, corner); max = Vector3.Max(max, corner);
                }
            }
            if (float.IsInfinity(min.x)) { min = new Vector3(-0.25f, 1.1f, -0.2f); max = new Vector3(0.25f, 1.6f, 0.2f); }
            float hw = (max.x - min.x) * 0.5f, hd = (max.z - min.z) * 0.5f, cz = (min.z + max.z) * 0.5f;
            float top = max.y, neck = min.y, back = cz - hd - 0.06f;
            var left = new[]
            {
                new Vector3(-(hw * 0.55f + 0.15f), neck * 0.35f, cz + 0.10f),   // 腰の横に垂れた端
                new Vector3(-(hw * 0.80f + 0.05f), neck * 0.70f, cz - 0.02f),
                new Vector3(-(hw + 0.10f), neck + 0.05f, cz - hd * 0.6f),       // 肩の後ろ
                new Vector3(-(hw + 0.14f), (neck + top) * 0.5f, back),
                new Vector3(-(hw * 0.65f), top + 0.08f, back),
            };
            var points = new Vector3[left.Length * 2 + 1];
            for (int i = 0; i < left.Length; i++)
            {
                points[i] = left[i];
                points[points.Length - 1 - i] = new Vector3(-left[i].x, left[i].y, left[i].z);
            }
            points[left.Length] = new Vector3(0, top + 0.16f, back);          // 頭上の頂点
            return points;
        }

        void Build()
        {
            // 胸の骨が動いた分だけ一緒に動く（お辞儀で前に傾くなど）
            ribbon.localPosition = anchorBone != null ? character.InverseTransformPoint(anchorBone.position) - anchorRest : Vector3.zero;
            ribbon.localRotation = Quaternion.identity;
            ribbon.localScale = Vector3.one;

            for (int i = 0; i <= Segments; i++)
            {
                float s = i / (float)Segments;
                var p = Point(s);
                var tangent = (Point(Mathf.Min(1, s + 0.01f)) - Point(Mathf.Max(0, s - 0.01f))).normalized;
                // 帯の面は正面を向き、流れに沿ってゆっくりねじれる
                float twist = Mathf.Sin(time * 1.3f + s * 9f) * 40f * flutter;
                var across = Quaternion.AngleAxis(twist, tangent) * Vector3.Cross(tangent, Vector3.forward).normalized;
                float w = width * (0.75f + 0.25f * Mathf.Sin(s * Mathf.PI));
                vertices[i * 2] = p - across * (w * 0.5f);
                vertices[i * 2 + 1] = p + across * (w * 0.5f);
                normals[i * 2] = normals[i * 2 + 1] = Vector3.Cross(tangent, across).normalized;
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.RecalculateBounds();
        }

        Vector3 Point(float s)
        {
            float f = s * (path.Length - 1);
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, path.Length - 2);
            float t = f - i;
            var p = CatmullRom(path[Mathf.Max(0, i - 1)], path[i], path[i + 1], path[Mathf.Min(path.Length - 1, i + 2)], t);
            // ふわふわ浮かぶ揺れ：頭上はゆったり、垂れた端ほど大きくはためく
            float edge = Mathf.Abs(s - 0.5f) * 2f;
            float amp = (0.025f + 0.07f * edge * edge) * flutter;
            p += new Vector3(Mathf.Sin(time * 1.1f + s * 7f) * 0.6f, Mathf.Sin(time * 0.8f + s * 5f + 1f), Mathf.Sin(time * 0.95f + s * 6f + 2f)) * amp;
            p.y += Mathf.Sin(time * 0.6f) * 0.015f;
            return p;
        }

        static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (3 * b - a - 3 * c + d) * t3);
        }

        /// <summary>素材が未設定のとき用。半透明・両面・頂点カラーの淡い光る布。</summary>
        static Material DefaultMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(shader) { name = "Hagoromo (runtime)" };
            SetupMaterial(m);
            return m;
        }

        /// <summary>URP の Particles/Unlit を半透明・両面にする（エディタの素材作成でも使う）。</summary>
        public static void SetupMaterial(Material m)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_Cull", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetColor("_BaseColor", Color.white);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
