using UnityEditor;
using UnityEngine;

namespace JPHACKS2026.Editor
{
    /// <summary>
    /// FBXのマテリアルをURP用に整え、模様テクスチャ（base_color.png）をBase Colorに割り当てる。
    /// FBXでは模様の画像がMetallicに接続されて書き出されているため、ここでBase Colorにつなぎ直す。
    /// </summary>
    public sealed class TamatebakoMaterialImporter : AssetPostprocessor
    {
        const string ModelPath = "Assets/_Project/Art/Models/Props/Tamatebako.fbx";
        const string TexturePath = "Assets/_Project/Art/Models/Props/玉手箱.fbm/base_color.png";
        const string ImportVersion = "TamatebakoURPMaterials-v2";

        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            // Base Colorとして使う色の画像なのでsRGB
            if (assetPath == TexturePath)
                ((TextureImporter)assetImporter).sRGBTexture = true;
        }

        void OnPostprocessMaterial(Material material)
        {
            if (assetPath != ModelPath) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Tamatebako] URP/Lit シェーダーが見つかりません。");
                return;
            }

            // BlenderのBase Colorはリニア値。Unityのマテリアル色にはsRGBで渡す。
            var color = new Color(0.20790951f, 0.008384568f, 0.006312439f).gamma;
            Texture2D texture = null;
            // 模様の画像がつながっているのはBlenderの「Material」（Unityでは「Material.001」になることがある）
            if (material.name == "Material" || material.name.StartsWith("Material."))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                color = Color.white;   // 画像の色をそのまま出す
                if (texture == null)
                    Debug.LogError("[Tamatebako] 模様の画像が見つかりません: " + TexturePath);
            }
            else if (material.name != "マテリアル") return;

            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetTexture("_MetallicGlossMap", null);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", texture != null ? 0.6f : 0.3f);   // 漆のつや
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
        }

        [InitializeOnLoadMethod]
        static void ScheduleImport()
        {
            EditorApplication.delayCall += ImportIfNeeded;
        }

        static void ImportIfNeeded()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += ImportIfNeeded;
                return;
            }
            var importer = AssetImporter.GetAtPath(ModelPath);
            if (importer == null || importer.userData == ImportVersion) return;
            ReimportMaterials();
        }

        [MenuItem("JPHACKS2026/Refresh Tamatebako Materials")]
        static void ReimportMaterials()
        {
            var importer = AssetImporter.GetAtPath(ModelPath);
            if (importer == null) return;
            // 前回の失敗をキャッシュしないよう、テクスチャを先に読み込む。
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            importer.userData = ImportVersion;
            importer.SaveAndReimport();
            Debug.Log("[Tamatebako] URP用マテリアルと模様画像を再読み込みしました。");
        }
    }
}
