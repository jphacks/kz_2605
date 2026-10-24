using System.IO;
using UnityEditor;
using UnityEngine;

namespace JPHACKS2026.Editor
{
    /// <summary>
    /// 玉手箱のマテリアルをアセット（.mat）として作り、FBXのマテリアルを差し替える（リマップ）。
    /// ・模様：base_color.png をBase Colorに使う（FBXではMetallicに接続されて書き出されているため、つなぎ直す）
    /// ・漆：暗い赤
    /// マテリアルの色やつやは、生成された .mat を直接編集してよい（既にあれば上書きしない）。
    /// </summary>
    public sealed class TamatebakoMaterialImporter : AssetPostprocessor
    {
        public const string ModelPath = "Assets/_Project/Art/Models/Props/tamatebako.fbx";
        const string TexturePath = "Assets/_Project/Art/Models/tamatebako.fbm/フタ_ベースカラー.png";
        const string MaterialDir = "Assets/_Project/Art/Materials/Tamatebako";
        public const string PatternMaterialPath = MaterialDir + "/Tamatebako_Pattern.mat";
        public const string LacquerMaterialPath = MaterialDir + "/Tamatebako_Lacquer.mat";
        const string ImportVersion = "TamatebakoMaterialAssets-v4";

        // FBX内のマテリアル名。模様の画像がつながっているのはBlenderの「Material」（Unityでは「Material.001」になることがある）
        static readonly string[] PatternSourceNames = { "Material", "Material.001" };
        static readonly string[] LacquerSourceNames = { "マテリアル" };

        void OnPreprocessTexture()
        {
            // Base Colorとして使う色の画像なのでsRGB
            if (assetPath == TexturePath)
                ((TextureImporter)assetImporter).sRGBTexture = true;
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
            if (importer == null) return;
            if (importer.userData == ImportVersion && File.Exists(PatternMaterialPath) && File.Exists(LacquerMaterialPath)) return;
            Setup();
        }

        /// <summary>マテリアルを用意してFBXに割り当てる。シーン生成（MainSceneBuilder）からも呼ぶ。</summary>
        [MenuItem("JPHACKS2026/Refresh Tamatebako Materials")]
        public static void Setup()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Tamatebako] モデルが見つかりません: " + ModelPath);
                return;
            }
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) Debug.LogError("[Tamatebako] 模様の画像が見つかりません: " + TexturePath);

            // BlenderのBase Colorはリニア値なので、Unityのマテリアル色にはsRGBで渡す
            var pattern = EnsureMaterial(PatternMaterialPath, Color.white, texture, 0.6f);
            var lacquer = EnsureMaterial(LacquerMaterialPath, new Color(0.20790951f, 0.008384568f, 0.006312439f).gamma, null, 0.3f);

            // FBXから読み込まれたマテリアル名（まだ差し替えていないもの）も拾う。「マテリアル」以外は模様とみなす
            var names = new System.Collections.Generic.HashSet<string>(PatternSourceNames);
            names.UnionWith(LacquerSourceNames);
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (a is Material) names.Add(a.name);

            bool changed = importer.userData != ImportVersion;
            foreach (var n in names)
                changed |= Remap(importer, n, System.Array.IndexOf(LacquerSourceNames, n) >= 0 ? lacquer : pattern);
            if (!changed) return;
            importer.userData = ImportVersion;
            importer.SaveAndReimport();
            Debug.Log($"[Tamatebako] マテリアルを割り当てました: {PatternMaterialPath}, {LacquerMaterialPath}");
        }

        static Material EnsureMaterial(string path, Color color, Texture2D baseMap, float smoothness)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
            {
                // 手で調整した値は残し、画像が外れているときだけ付け直す
                if (baseMap != null && m.GetTexture("_BaseMap") == null)
                {
                    m.SetTexture("_BaseMap", baseMap);
                    EditorUtility.SetDirty(m);
                    AssetDatabase.SaveAssets();
                }
                return m;
            }

            Directory.CreateDirectory(MaterialDir);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", baseMap);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);   // 漆のつや
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            return m;
        }

        static bool Remap(ModelImporter importer, string sourceName, Material material)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName);
            if (importer.GetExternalObjectMap().TryGetValue(id, out var current) && current == material) return false;
            importer.AddRemap(id, material);
            return true;
        }
    }
}
