using System.IO;
using UnityEditor;
using UnityEngine;

namespace JPHACKS2026.Editor
{
    /// <summary>配信設定（Resources/LiveStreamConfig）を作成・選択する。</summary>
    public static class LiveStreamConfigMenu
    {
        const string Dir = "Assets/_Project/Settings/Resources";
        const string AssetPath = Dir + "/LiveStreamConfig.asset";

        [MenuItem("JPHACKS2026/Live Stream Config")]
        public static void Open()
        {
            var config = AssetDatabase.LoadAssetAtPath<LiveStreamConfig>(AssetPath);
            if (config == null)
            {
                Directory.CreateDirectory(Dir);
                config = ScriptableObject.CreateInstance<LiveStreamConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[LiveStream] 配信設定を作成しました: " + AssetPath + "（Database Url に Firebase のURLを入れると配信が有効になります）");
            }
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
    }
}
