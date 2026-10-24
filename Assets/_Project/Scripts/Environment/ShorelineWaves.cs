using UnityEngine;
using UnityEngine.Rendering;

namespace JPHACKS2026
{
    /// <summary>GPUで寄せ波・引き波・白泡を描く、軽量な浜辺用海面。</summary>
    public class ShorelineWaves : MonoBehaviour
    {
        Mesh mesh;
        Material material;

        void Awake()
        {
            var shader = Resources.Load<Shader>("Scenery/ShorelineWater");
            if (shader == null) { Debug.LogError("ShorelineWater shader is missing"); return; }
            material = new Material(shader) { name = "Shoreline water" };
            const int nx = 120, nz = 72;
            var vertices = new Vector3[(nx + 1) * (nz + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[nx * nz * 6];
            for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++)
            {
                int i = z * (nx + 1) + x;
                vertices[i] = new Vector3(-30 + 60f * x / nx, .04f, 6.3f + 35f * z / nz);
                uv[i] = new Vector2(x / (float)nx, z / (float)nz);
            }
            int k = 0;
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                int i = z * (nx + 1) + x;
                triangles[k++] = i; triangles[k++] = i + nx + 1; triangles[k++] = i + 1;
                triangles[k++] = i + 1; triangles[k++] = i + nx + 1; triangles[k++] = i + nx + 2;
            }
            mesh = new Mesh { name = "Shoreline wave grid" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(0, .2f, 24), new Vector3(62, 2, 40));
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
