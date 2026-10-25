using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using JPHACKS2026;

[InitializeOnLoad] public static class EndingVideoExport
{
    static EndingVideoExport() { if (Array.IndexOf(Environment.GetCommandLineArgs(), "-endingOutput") >= 0) EditorApplication.playModeStateChanged += s => { if(s == PlayModeStateChange.EnteredPlayMode) new GameObject("Ending video export").AddComponent<EndingVideoRunner>(); }; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
        /*EditorApplication.playModeStateChanged += s => {
            if (s == PlayModeStateChange.EnteredPlayMode)
                new GameObject("Ending video export").AddComponent<EndingVideoRunner>();
        };*/
        EditorApplication.isPlaying = true;
    }
}

public class EndingVideoRunner : MonoBehaviour
{
    const int Width = 1920, Height = 1080, Fps = 30;
    string output, ffmpeg;
    System.Diagnostics.Process encoder;
    BinaryWriter audio;
    RenderTexture target;
    Texture2D pixels;
    bool recording;
    int frames, current;

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        var args = Environment.GetCommandLineArgs();
        output = args[Array.IndexOf(args,"-endingOutput")+1];
        ffmpeg = args[Array.IndexOf(args,"-ffmpeg")+1];
        Directory.CreateDirectory(output);
        Time.captureFramerate = Fps;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        AudioRenderer.Start();
        target = new RenderTexture(Width,Height,24);
        pixels = new Texture2D(Width,Height,TextureFormat.RGB24,false);
        for(current=1; current<=9; current++)
        {
            if(current>1) { SceneManager.LoadScene("Main"); yield return null; }
            yield return null;
            var gm=GameManager.Instance;
            gm.StopAllCoroutines();
            gm.narration.Stop();
            gm.Menu.Hide(); gm.Choice.Hide();
            yield return new WaitForSeconds(1.7f);
            gm.StopAllCoroutines();
            string area=current<=3?"beach":current<=5||current==9?"ryugu":"village";
            gm.SwitchArea(area,current==3?StoryAnchor.Get("beach.invite.spawn"):null);
            if(current==3) {
                gm.story.FleeBeachChildren(100);
                StoryAnchor.Get("beach.turtle").position=gm.GetArea("beach").transform.TransformPoint(new Vector3(2.3f,0,7.5f));
            }
            gm.Rig.LocomotionEnabled=false;
            gm.Rig.head.GetComponent<Camera>().fieldOfView=65;
            GameInput.DebugQueue.Clear();
            if(current==4||current==9) {
                GameInput.DebugQueue.Enqueue(0); GameInput.DebugQueue.Enqueue(0);
                GameInput.DebugQueue.Enqueue(current==9?1:0);
            }
            yield return gm.Fader.FadeIn(.01f);
            Begin();
            gm.StartCoroutine(gm.endings.Play((EndingId)(current==9?4:current)));
            float elapsed=0;
            while(!gm.Menu.gameObject.activeSelf && elapsed<300) { elapsed+=Time.deltaTime; yield return null; }
            if(elapsed>=300) throw new Exception("Ending timed out: "+current);
            yield return new WaitForSeconds(4);
            recording=false;
            Finish();
            File.AppendAllText(Path.Combine(output,"progress.txt"),$"ED{current}: {frames} frames, {frames/(float)Fps:F2}s\n");
        }
        AudioRenderer.Stop();
        File.WriteAllText(Path.Combine(output,"complete.txt"),"All 9 endings exported.");
        EditorApplication.Exit(0);
    }

    void Begin()
    {
        frames=0;
        var start=new System.Diagnostics.ProcessStartInfo(ffmpeg,
            $"-y -f rawvideo -pixel_format rgb24 -video_size {Width}x{Height} -framerate {Fps} -i pipe:0 -vf vflip -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p \"{output}/ED{current}_video.mp4\"");
        start.UseShellExecute=false; start.CreateNoWindow=true; start.RedirectStandardInput=true;
        encoder=System.Diagnostics.Process.Start(start);
        audio=new BinaryWriter(File.Create(Path.Combine(output,$"ED{current}.f32")));
        recording=true;
    }

    void LateUpdate()
    {
        int count=AudioRenderer.GetSampleCountForCaptureFrame()*2;
        using(var buffer=new NativeArray<float>(count,Allocator.Temp)) {
            AudioRenderer.Render(buffer);
            if(recording) foreach(float sample in buffer) audio.Write(sample);
        }
        if(!recording) return;
        var cam=GameManager.Instance.Rig.head.GetComponent<Camera>();
        var previous=cam.targetTexture;
        cam.targetTexture=target;
        cam.Render();
        var active=RenderTexture.active; RenderTexture.active=target;
        pixels.ReadPixels(new Rect(0,0,Width,Height),0,0,false); pixels.Apply(false);
        RenderTexture.active=active; cam.targetTexture=previous;
        var bytes=pixels.GetRawTextureData<byte>().ToArray();
        encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);
        frames++;
    }

    void Finish()
    {
        encoder.StandardInput.Close(); encoder.WaitForExit();
        if(encoder.ExitCode!=0) throw new Exception("Video encoder failed");
        audio.Dispose();
        var start=new System.Diagnostics.ProcessStartInfo(ffmpeg,
            $"-y -i \"{output}/ED{current}_video.mp4\" -f f32le -ar {AudioSettings.outputSampleRate} -ac 2 -i \"{output}/ED{current}.f32\" -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart \"{output}/ED{current}.mp4\"");
        start.UseShellExecute=false; start.CreateNoWindow=true;
        var mux=System.Diagnostics.Process.Start(start); mux.WaitForExit();
        if(mux.ExitCode!=0) throw new Exception("Audio mux failed");
        File.Delete(Path.Combine(output,$"ED{current}_video.mp4"));
        File.Delete(Path.Combine(output,$"ED{current}.f32"));
    }
}

