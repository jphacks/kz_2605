using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace JPHACKS2026
{
    /// <summary>
    /// Firebase Realtime Database の REST API を UnityWebRequest で叩く（SDK不要）。
    /// パスはルーム（rooms/{roomId}）からの相対パス。
    /// ・Send：書き込み（PUT / PATCH / DELETE）
    /// ・Listen：ストリーミング受信（Server-Sent Events）。1本の接続でずっと変更を受け取る
    /// </summary>
    public class FirebaseRest
    {
        readonly string roomUrl;
        const int TimeoutSeconds = 8;

        public FirebaseRest(string databaseUrl, string roomId)
        {
            roomUrl = databaseUrl.TrimEnd('/') + "/rooms/" + UnityWebRequest.EscapeURL(roomId);
        }

        string Url(string path, string query = null)
        {
            string url = roomUrl + (string.IsNullOrEmpty(path) ? "" : "/" + path) + ".json";
            return string.IsNullOrEmpty(query) ? url : url + "?" + query;
        }

        UnityWebRequest CreateWrite(string method, string path, string json)
        {
            var req = new UnityWebRequest(Url(path), method, new DownloadHandlerBuffer(),
                json == null ? null : new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)));
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = TimeoutSeconds;
            return req;
        }

        /// <summary>
        /// PUT（置き換え）・PATCH（一部更新）・DELETE。jsonは Json.Serialize で作る。
        /// done には HTTPステータス（通信できなかったときは 0）を渡す。
        /// </summary>
        public IEnumerator Send(string method, string path, string json, Action<long> done = null)
        {
            using var req = CreateWrite(method, path, json);
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok) LogError(method, path, req);
            done?.Invoke(ok ? 200 : req.responseCode);
        }

        /// <summary>アプリ終了時など、コルーチンが使えないときの書き込み。最大 timeoutMs だけ待つ。</summary>
        public void SendBlocking(string method, string path, string json, int timeoutMs = 1500)
        {
            using var req = CreateWrite(method, path, json);
            var op = req.SendWebRequest();
            var sw = Stopwatch.StartNew();
            while (!op.isDone && sw.ElapsedMilliseconds < timeoutMs) System.Threading.Thread.Sleep(10);
        }

        /// <summary>
        /// path 以下の変更をストリーミングで受け取る。onEvent(event, data) は "put" / "patch" ごとに呼ばれる
        /// （data は {"path": "/...", "data": ...} のJSON）。切れたら少し待ってつなぎ直す。
        /// </summary>
        public IEnumerator Listen(string path, Action<string, string> onEvent)
        {
            while (true)
            {
                var handler = new SseHandler(onEvent);
                using (var req = new UnityWebRequest(Url(path), "GET", handler, null))
                {
                    req.SetRequestHeader("Accept", "text/event-stream");
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success) LogError("LISTEN", path, req);
                }
                yield return new WaitForSecondsRealtime(2f);
            }
        }

        /// <summary>Server-Sent Events を1行ずつ読み、event と data の組を渡す。</summary>
        class SseHandler : DownloadHandlerScript
        {
            readonly Action<string, string> onEvent;
            readonly Decoder decoder = Encoding.UTF8.GetDecoder();
            readonly StringBuilder line = new();
            string eventName;

            public SseHandler(Action<string, string> onEvent) : base(new byte[16 * 1024]) => this.onEvent = onEvent;

            protected override bool ReceiveData(byte[] data, int length)
            {
                var chars = new char[decoder.GetCharCount(data, 0, length)];
                decoder.GetChars(data, 0, length, chars, 0);
                foreach (char c in chars)
                {
                    if (c == '\r') continue;
                    if (c != '\n') { line.Append(c); continue; }
                    string l = line.ToString();
                    line.Clear();
                    if (l.StartsWith("event:")) eventName = l.Substring(6).Trim();
                    else if (l.StartsWith("data:") && eventName != null)
                    {
                        string payload = l.Substring(5).Trim();
                        try { onEvent(eventName, payload); }
                        catch (Exception e) { Debug.LogException(e); }
                        eventName = null;
                    }
                }
                return true;
            }
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
