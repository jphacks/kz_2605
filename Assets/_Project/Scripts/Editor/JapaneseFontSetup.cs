using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace JPHACKS2026.Editor
{
    /// <summary>
    /// Noto Sans JP から Dynamic な TMP フォントアセットを作り、TMP のデフォルトフォントに設定する。
    /// </summary>
    public static class JapaneseFontSetup
    {
        const string FontFolder = "Assets/_Project/UI/Fonts/NotoSansJP";
        const string DefaultFontName = "NotoSansJP-Regular";

        [MenuItem("JPHACKS2026/Setup/Create Japanese TMP Font Assets")]
        public static void CreateFontAssets()
        {
            // TMP Essential Resources が未インポートだと TMP Settings が無い
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                Debug.Log("[JapaneseFontSetup] TMP Essential Resources をインポートしました。インポート完了後にもう一度このメニューを実行してください。");
                return;
            }

            TMP_FontAsset defaultFontAsset = null;

            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { FontFolder }))
            {
                string fontPath = AssetDatabase.GUIDToAssetPath(guid);
                string fontName = Path.GetFileNameWithoutExtension(fontPath);
                string assetPath = $"{FontFolder}/{fontName} SDF.asset";

                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (fontAsset == null)
                {
                    // TMP 標準の「Create > TextMeshPro > Font Asset > SDF」と同じ処理で作る
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                    EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Font Asset/SDF");
                    fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                    if (fontAsset == null)
                    {
                        Debug.LogError($"[JapaneseFontSetup] フォントアセットを作成できませんでした: {fontPath}");
                        continue;
                    }
                }

                // 日本語は文字数が多く 1 枚のアトラスに収まらないため複数アトラスを許可する
                fontAsset.isMultiAtlasTexturesEnabled = true;
                EditorUtility.SetDirty(fontAsset);

                if (fontName == DefaultFontName)
                    defaultFontAsset = fontAsset;
            }

            if (defaultFontAsset != null)
            {
                var settings = TMP_Settings.instance;
                TMP_Settings.defaultFontAsset = defaultFontAsset;
                if (!TMP_Settings.fallbackFontAssets.Contains(defaultFontAsset))
                    TMP_Settings.fallbackFontAssets.Add(defaultFontAsset);
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[JapaneseFontSetup] 日本語フォントアセットの設定が完了しました。");
        }
    }
}
