using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace JPHACKS2026
{
    /// <summary>
    /// 視聴者向けの「配信カメラ」。体験者の頭を少し遅れて追いかけ（傾き・細かな揺れを抑えて酔いにくく）、
    /// 一定間隔で低解像度の画像を撮ってJPEGで渡す。普段は描画を止めておき、撮るときだけ1フレーム描画する。
    /// JPEGへの変換は別スレッドで行い、VRの描画が引っかからないようにする。
    /// </summary>
    public class SpectatorCamera : MonoBehaviour
    {
        public float positionSharpness = 6f;
        public float rotationSharpness = 4f;

        Camera cam;
        Transform head;
        RenderTexture rt;
        Texture2D tex;
        int quality;
        bool busy;

        public static SpectatorCamera Create(Transform head, int width, int height, int jpgQuality)
        {
            var go = new GameObject("SpectatorCamera");
            var sc = go.AddComponent<SpectatorCamera>();
            sc.head = head;
            sc.quality = jpgQuality;
            sc.rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "SpectatorRT" };
            sc.tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            sc.cam = go.AddComponent<Camera>();
            sc.cam.stereoTargetEye = StereoTargetEyeMask.None;   // VRの左右の目ではなく、普通の1枚絵として描く
            sc.cam.targetTexture = sc.rt;
            sc.cam.fieldOfView = 70f;
            sc.cam.enabled = false;
            go.transform.SetPositionAndRotation(head.position, LevelRotation(head.rotation));
            return sc;
        }

        static Quaternion LevelRotation(Quaternion q)
        {
            var e = q.eulerAngles;
            return Quaternion.Euler(e.x, e.y, 0);   // 首の傾き（ロール）は取り除く
        }

        void LateUpdate()
        {
            if (head == null) return;
            float dt = Time.unscaledDeltaTime;
            transform.position = Vector3.Lerp(transform.position, head.position, 1 - Mathf.Exp(-positionSharpness * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, LevelRotation(head.rotation), 1 - Mathf.Exp(-rotationSharpness * dt));
        }

        /// <summary>1枚撮ってJPEG（base64）を返す。前の撮影が終わっていなければ何もしない。</summary>
        public void Capture(Action<string> done)
        {
            if (busy) return;
            StartCoroutine(CaptureRoutine(done));
        }

        IEnumerator CaptureRoutine(Action<string> done)
        {
            busy = true;
            var main = head.GetComponent<Camera>();
            if (main != null)
            {
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
                cam.cullingMask = main.cullingMask;
                cam.nearClipPlane = main.nearClipPlane;
                cam.farClipPlane = main.farClipPlane;
            }
            cam.enabled = true;
            yield return new WaitForEndOfFrame();
            cam.enabled = false;

            byte[] pixels;
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                var request = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
                while (!request.done) yield return null;
                if (request.hasError) { busy = false; yield break; }
                pixels = request.GetData<byte>().ToArray();
            }
            else
            {
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
                RenderTexture.active = prev;
                pixels = tex.GetRawTextureData();
            }

            // JPEG化とbase64化は重いので別スレッドで（ImageConversion.EncodeArrayToJPG はスレッドセーフ）
            uint w = (uint)rt.width, h = (uint)rt.height;
            int q = quality;
            var task = Task.Run(() => Convert.ToBase64String(
                ImageConversion.EncodeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_UNorm, w, h, 0, q)));
            while (!task.IsCompleted) yield return null;
            busy = false;
            if (task.Status == TaskStatus.RanToCompletion) done(task.Result);
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
        }
    }
}
