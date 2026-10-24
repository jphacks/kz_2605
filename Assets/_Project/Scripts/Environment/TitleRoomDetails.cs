using UnityEngine;
using UnityEngine.Rendering;

namespace JPHACKS2026
{
    public static class TitleRoomDetails
    {
        public static void Build(Area area)
        {
            area.ambientColor = new Color(.12f, .14f, .22f);
            area.sunColor = new Color(.67f, .79f, 1f);
            area.sunIntensity = 1.15f;
            area.sunEuler = Quaternion.LookRotation(new Vector3(-5.5f, -7.25f, -24.8f)).eulerAngles;
            foreach (var tr in area.GetComponentsInChildren<Transform>(true))
                if (tr.name == "Castle" || tr.name == "CastleTower" || tr.name == "CastleRoof" || tr.name == "MoonReflection") tr.gameObject.SetActive(false);
            foreach (var renderer in area.GetComponentsInChildren<MeshRenderer>(true))
            {
                // The real window opening and bars mask the directional moonlight.
                if (renderer.name.StartsWith("Wall") || renderer.name.StartsWith("Window") || renderer.name == "Ceiling")
                    renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                renderer.receiveShadows = true;
            }
            if(area.transform.Find("Window moonlight")==null)
            {
                var moon = new GameObject("Window moonlight").AddComponent<Light>();
                moon.transform.SetParent(area.transform,false);
                moon.transform.localPosition=new Vector3(.1f,1.9f,3.12f);
                moon.transform.localRotation=Quaternion.LookRotation(new Vector3(-.22f,-.38f,-1));
                moon.type=LightType.Spot; moon.color=new Color(.53f,.72f,1f);
                moon.intensity=7f; moon.range=10; moon.spotAngle=48; moon.innerSpotAngle=36;
                moon.shadows=LightShadows.Hard; moon.shadowBias=.01f; moon.shadowNormalBias=.08f;
                // A four-by-two window cookie keeps mullion shadows visible on mobile too.
                var cookie=new Texture2D(128,128,TextureFormat.RGBA32,false,true) {name="Window lattice moonlight",wrapMode=TextureWrapMode.Clamp};
                for(int y=0;y<128;y++) for(int x=0;x<128;x++)
                {
                    float u=x/127f,v=y/127f;
                    bool frame=u<.04f||u>.96f||v<.04f||v>.96f||Mathf.Abs(v-.5f)<.018f;
                    for(int bar=1;bar<4;bar++) frame|=Mathf.Abs(u-bar*.25f)<.012f;
                    cookie.SetPixel(x,y,frame?Color.black:Color.white);
                }
                cookie.Apply(); moon.cookie=cookie;
            }
            var book = area.GetComponentInChildren<Picturebook>(true);
            if (book == null || book.coverHinge == null || book.coverHinge.Find("Urashima cover") != null) return;
            var texture = Resources.Load<Texture2D>("Book/UrashimaCover");
            if (texture == null)
            {
                Debug.LogWarning("[TitleRoom] 浦島太郎の表紙画像が見つかりません");
                return;
            }
            var old = book.coverHinge.Find("CoverDeco");
            if (old != null) old.gameObject.SetActive(false);
            var art = new GameObject("Urashima cover").transform;
            art.SetParent(book.coverHinge, false);
            var cover = new GameObject("浦島太郎");
            cover.transform.SetParent(art, false);
            // UV top points toward the far edge (+Z), so the title faces the reader.
            float halfHeight = .31f * texture.height / texture.width * .5f;
            var mesh = new Mesh { name = "Urashima illustrated cover" };
            mesh.vertices = new[]
            {
                new Vector3(.025f,.011f,-halfHeight), new Vector3(.335f,.011f,-halfHeight),
                new Vector3(.335f,.011f,halfHeight), new Vector3(.025f,.011f,halfHeight),
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0,2,1, 0,3,2 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            cover.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = Material("Urashima generated cover", Color.white);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", .12f);
            material.SetTexture("_EmissionMap", texture);
            material.SetColor("_EmissionColor", Color.white * .08f);
            material.EnableKeyword("_EMISSION");
            var coverRenderer = cover.AddComponent<MeshRenderer>();
            coverRenderer.sharedMaterial = material;
            coverRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor",color); return material;
        }
    }
}
