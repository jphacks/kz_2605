using System.IO;
using UnityEditor;
using UnityEngine;

namespace JPHACKS2026.Editor
{
    /// <summary>
    /// 配信設定（Resources/LiveStreamConfig）を作成・選択する。
    /// サーバー役のPC（Web/server.js）は http:// で動くので、Quest（Android）で http 通信ができるよう Player Settings も整える。
    /// </summary>
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
                Debug.Log("[LiveStream] 配信設定を作成しました: " + AssetPath + "（Database Url に配信サーバーのURLを入れると配信が有効になります）");
            }
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            AllowHttpIfNeeded();
        }

        [InitializeOnLoadMethod]
        static void AllowHttpIfNeeded()
        {
            EditorApplication.delayCall += () =>
            {
                var config = AssetDatabase.LoadAssetAtPath<LiveStreamConfig>(AssetPath);
                if (config == null || !config.databaseUrl.Trim().StartsWith("http://")) return;
                if (PlayerSettings.insecureHttpOption == InsecureHttpOption.AlwaysAllowed) return;
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                Debug.Log("[LiveStream] サーバー役のPC（http://）に接続できるよう、Player Settings の「Allow downloads over HTTP」を Always allowed にしました。");
            };
        }
    }
}
