using System;
using System.Collections;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using JPHACKS2026;

[InitializeOnLoad] public static class PrincessTrailerCapture
{
 static bool attached;
 static PrincessTrailerCapture(){EditorApplication.update+=Ensure;}
 static void Ensure(){if(!EditorApplication.isPlaying) {attached=false;return;} if(!attached && Array.IndexOf(Environment.GetCommandLineArgs(),"-princessOutput")>=0){attached=true;new GameObject("Princess trailer capture").AddComponent<PrincessTrailerRunner>();}}
 public static void Run(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");JPHACKS2026.Editor.PrincessModelSetup.ReplaceInScene();EditorApplication.isPlaying=true;}
}
public class PrincessTrailerRunner:MonoBehaviour
{
 string output,ffmpeg; RenderTexture rt;Texture2D texture;System.Diagnostics.Process encoder;bool recording;int frames;
 IEnumerator Start(){DontDestroyOnLoad(gameObject);var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"-princessOutput")+1];ffmpeg=args[Array.IndexOf(args,"-ffmpeg")+1];Directory.CreateDirectory(output);Time.timeScale=1;Time.captureFramerate=30;Application.runInBackground=true;QualitySettings.vSyncCount=0;AudioRenderer.Start();rt=new RenderTexture(1920,1080,24);texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
 yield return new WaitForSeconds(2);Prepare();var gm=GameManager.Instance;gm.SwitchArea("ryugu");yield return gm.Fader.FadeIn(.01f);
 var cam=gm.Rig.Head.GetComponent<Camera>();cam.fieldOfView=65;var area=gm.CurrentArea.transform;
 // Same establishing shot used by the existing trailer, with the current princess visual.
 var scenic=new GameObject("Palace trailer camera").AddComponent<Camera>();scenic.enabled=false;scenic.fieldOfView=65;scenic.clearFlags=CameraClearFlags.SolidColor;scenic.backgroundColor=gm.CurrentArea.skyColor;scenic.transform.position=area.TransformPoint(new Vector3(0,1.7f,-4));scenic.transform.LookAt(area.TransformPoint(new Vector3(0,1.5f,7)));
 Begin("palace");for(int f=0;f<120;f++){yield return null;Capture(scenic);}Finish();Destroy(scenic.gameObject);
 int[] ids={4,5,9};int[] lengths={36,27,46};
 for(int i=0;i<ids.Length;i++){
 SceneManager.LoadScene("Main");yield return null;yield return null;yield return new WaitForSeconds(1.7f);Prepare();gm=GameManager.Instance;gm.SwitchArea("ryugu");gm.Rig.LocomotionEnabled=false;gm.Rig.head.GetComponent<Camera>().fieldOfView=65;GameInput.DebugQueue.Clear();if(ids[i]!=5){GameInput.DebugQueue.Enqueue(0);GameInput.DebugQueue.Enqueue(0);GameInput.DebugQueue.Enqueue(ids[i]==9?1:0);}yield return gm.Fader.FadeIn(.01f);
 Begin("ED"+ids[i]);gm.StartCoroutine(gm.endings.Play((EndingId)(ids[i]==9?4:ids[i])));while(frames<lengths[i]*30){yield return null;Capture(gm.Rig.Head.GetComponent<Camera>());}gm.StopAllCoroutines();Finish();
 }
 AudioRenderer.Stop();File.WriteAllText(Path.Combine(output,"complete.txt"),"New princess: palace, ED4, ED5, ED9");EditorApplication.Exit(0);
 }
 void Prepare(){var gm=GameManager.Instance;gm.StopAllCoroutines();gm.narration.Stop();gm.Menu.Hide();gm.Choice.Hide();gm.Subtitle.Hide();typeof(GameManager).GetMethod("ShowHud",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(gm,new object[]{false});foreach(var sim in FindObjectsByType<DesktopSimulator>(FindObjectsSortMode.None))sim.enabled=false;}
 void Begin(string name){frames=0;var psi=new System.Diagnostics.ProcessStartInfo(ffmpeg,$"-y -loglevel error -f rawvideo -pixel_format rgb24 -video_size 1920x1080 -framerate 30 -i pipe:0 -vf vflip -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p \"{output}/{name}.mp4\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true};encoder=System.Diagnostics.Process.Start(psi);recording=true;Debug.Log("PRINCESS RECORD "+name);}
 void Capture(Camera cam){var rig=GameManager.Instance.Rig;rig.leftHand.transform.position=rig.head.TransformPoint(new Vector3(-.22f,-.32f,.45f));rig.rightHand.transform.position=rig.head.TransformPoint(new Vector3(.22f,-.32f,.45f));rig.leftHand.transform.rotation=rig.head.rotation;rig.rightHand.transform.rotation=rig.head.rotation;var previous=cam.targetTexture;cam.targetTexture=rt;cam.Render();var active=RenderTexture.active;RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1920,1080),0,0,false);texture.Apply(false);RenderTexture.active=active;cam.targetTexture=previous;var bytes=texture.GetRawTextureData<byte>().ToArray();encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);frames++;}
 void LateUpdate(){int count=AudioRenderer.GetSampleCountForCaptureFrame()*2;using(var buffer=new NativeArray<float>(count,Allocator.Temp))AudioRenderer.Render(buffer);}
 void Finish(){recording=false;encoder.StandardInput.Close();encoder.WaitForExit();if(encoder.ExitCode!=0)throw new Exception("Princess encode failed");File.AppendAllText(Path.Combine(output,"progress.txt"),frames+" frames\n");}
}
