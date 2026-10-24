using UnityEngine;
using UnityEngine.Serialization;

namespace JPHACKS2026
{
    /// <summary>
    /// 配信（Web視聴・投票・コメント）の設定。Resources/LiveStreamConfig に置く。
    /// メニュー：JPHACKS2026 > Live Stream Config で作成・選択できる。
    /// </summary>
    [CreateAssetMenu(menuName = "JPHACKS2026/Live Stream Config", fileName = "LiveStreamConfig")]
    public class LiveStreamConfig : ScriptableObject
    {
        [Tooltip("配信を有効にする")]
        public bool streamEnabled = true;
        [Tooltip("Firebase Realtime Database のURL（例：https://xxxx-default-rtdb.asia-southeast1.firebasedatabase.app）")]
        [FormerlySerializedAs("serverUrl")]
        public string databaseUrl = "";
        [Tooltip("配信ルームのID。視聴ページの ?room= と合わせる（既定は main）")]
        public string roomId = "main";
        [Tooltip("視聴ページのURL（Firebase Hosting で公開したURL。例：https://xxxx.web.app）。タイトル画面に表示する")]
        public string viewerPageUrl = "";

        /// <summary>タイトル画面に出す視聴ページのURL（未設定なら空）。</summary>
        public string ViewerUrl
        {
            get
            {
                string url = viewerPageUrl.Trim();
                if (url.Length == 0 || roomId == "main" || url.Contains("?")) return url;
                return url.TrimEnd('/') + "/?room=" + roomId;
            }
        }

        [Header("VR視点の画像")]
        [Tooltip("画像を送る間隔（秒）。短くすると滑らかになるが、通信量が増える")]
        public float frameInterval = 1f;
        public int frameWidth = 480;
        public int frameHeight = 270;
        [Range(10, 90)] public int jpgQuality = 45;

        [Header("受信")]
        [Tooltip("投票・コメント・視聴者数を取りに行く間隔（秒）")]
        public float pollInterval = 1f;

        public bool IsValid => streamEnabled && !string.IsNullOrWhiteSpace(databaseUrl) && !string.IsNullOrWhiteSpace(roomId);

        public static LiveStreamConfig Load() => Resources.Load<LiveStreamConfig>("LiveStreamConfig");
    }
}
