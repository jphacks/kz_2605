using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace JPHACKS2026
{
    /// <summary>
    /// Firebase Realtime Database の REST API を UnityWebRequest で叩く（SDK不要）。
    /// パスはルーム（rooms/{roomId}）からの相対パス。
    /// </summary>
    public class FirebaseRest
    {
        readonly string roomUrl;
        const int TimeoutSeconds = 6;

        public FirebaseRest(string databaseUrl, string roomId)
        {
            roomUrl = databaseUrl.TrimEnd('/') + "/rooms/" + UnityWebRequest.EscapeURL(roomId);
        }

        string Url(string path, string query = null)
        {
            string url = roomUrl + (string.IsNullOrEmpty(path) ? "" : "/" + path) + ".json";
            return string.IsNullOrEmpty(query) ? url : url + "?" + query;
        }

        /// <summary>PUT（置き換え）・PATCH（一部更新）・DELETE。jsonは Json.Serialize で作る。</summary>
        public IEnumerator Send(string method, string path, string json, Action<bool> done = null)
        {
            using var req = new UnityWebRequest(Url(path), method, new DownloadHandlerBuffer(),
                json == null ? null : new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)));
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = TimeoutSeconds;
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok) LogError(method, path, req);
            done?.Invoke(ok);
        }

        /// <summary>GET。結果は Json.Parse した値（データが無ければ null）。失敗時は done を呼ばない。</summary>
        public IEnumerator Get(string path, string query, Action<object> done)
        {
            using var req = UnityWebRequest.Get(Url(path, query));
            req.timeout = TimeoutSeconds;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { LogError("GET", path, req); yield break; }
            done(Json.Parse(req.downloadHandler.text));
        }

        float lastErrorLog = -999f;

        void LogError(string method, string path, UnityWebRequest req)
        {
            // 通信が切れているときにログが溢れないよう間引く
            if (Time.unscaledTime - lastErrorLog < 10f) return;
            lastErrorLog = Time.unscaledTime;
            Debug.LogWarning($"[LiveStream] {method} {path} に失敗しました: {req.responseCode} {req.error}");
        }
    }
}
