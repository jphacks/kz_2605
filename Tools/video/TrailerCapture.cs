using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JPHACKS2026;

[InitializeOnLoad] public static class TrailerCapture {
 static TrailerCapture() { EditorApplication.update += EnsureRunner; if(Array.IndexOf(Environment.GetCommandLineArgs(),"-trailerOutput")>=0) EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)new GameObject("Trailer capture").AddComponent<TrailerCaptureRunner>();}; }
 static void EnsureRunner(){ if(EditorApplication.isPlaying && Array.IndexOf(Environment.GetCommandLineArgs(),"-trailerOutput")>=0 && UnityEngine.Object.FindFirstObjectByType<TrailerCaptureRunner>()==null) { Debug.Log("TRAILER attaching runner"); new GameObject("Trailer runner").AddComponent<TrailerCaptureRunner>(); } } public static void Run(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");EditorApplication.isPlaying=true;}
}
public class TrailerCaptureRunner:MonoBehaviour {
 System.Diagnostics.Process encoder; RenderTexture rt; Texture2D tex; Camera cam; bool recording; string output,ffmpeg; int frame; Action<float> animate;
 IEnumerator Start(){ Debug.Log("TRAILER START"); Time.timeScale=1;
 var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"-trailerOutput")+1];ffmpeg=args[Array.IndexOf(args,"-ffmpeg")+1];Directory.CreateDirectory(output);
 Time.captureFramerate=30;Application.runInBackground=true;QualitySettings.vSyncCount=0;
 yield return null; yield return null;var gm=GameManager.Instance;gm.StopAllCoroutines();gm.narration.Stop();gm.Menu.Hide();gm.Choice.Hide();typeof(GameManager).GetMethod("ShowHud",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(gm,new object[]{false});foreach(var hand in gm.Rig.Hands)foreach(var rr in hand.GetComponentsInChildren<Renderer>())rr.enabled=false;
 foreach(var b in FindObjectsByType<DesktopSimulator>(FindObjectsSortMode.None))b.enabled=false;
 var go=new GameObject("Trailer camera");cam=go.AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=65;rt=new RenderTexture(1920,1080,24);tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);cam.targetTexture=rt;
 yield return Shot("room","title",new Vector3(.3f,1.65f,-.8f),new Vector3(0,.95f,2.6f),7,k=>{var book=StoryAnchor.Get<Picturebook>("title.book");book.coverHinge.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(0,book.openAngle,Mathf.Clamp01((k-.3f)*2)));book.SetGlow(Mathf.Clamp01((k-.7f)*1.5f));});
 yield return Shot("beach","beach",new Vector3(-2,1.8f,-4),new Vector3(-6,.4f,6),5,null);
 yield return Shot("sea","sea",new Vector3(0,.5f,3),new Vector3(3,0,12),5,null);
 yield return Shot("palace","ryugu",new Vector3(0,1.7f,-4),new Vector3(0,1.5f,7),5,null);
 yield return Shot("village","village",new Vector3(0,1.7f,-3),new Vector3(0,1.0f,5),4,null);
 gm.SwitchArea("beach");var turtle=StoryAnchor.Get("beach.turtle");var area=gm.CurrentArea.transform;
 var stick=GameObject.CreatePrimitive(PrimitiveType.Cylinder);stick.name="Urashima POV stick";stick.transform.localScale=new Vector3(.055f,.65f,.055f);stick.GetComponent<Renderer>().material.color=new Color(.28f,.14f,.055f);Destroy(stick.GetComponent<Collider>());
 var focus=area.InverseTransformPoint(turtle.position+Vector3.up*.35f);var eye=area.InverseTransformPoint(turtle.position+new Vector3(0,1.7f,-1.4f));
 yield return Shot("bully","beach",eye,focus,5,k=>{float hit=Mathf.Max(0,Mathf.Sin(k*Mathf.PI*8));var grip=cam.transform.position+cam.transform.right*.22f+cam.transform.forward*.4f-cam.transform.up*.45f;var aim=Vector3.Slerp(Vector3.up,(turtle.position+Vector3.up*.3f-grip).normalized,hit);stick.transform.position=grip+aim*.65f;stick.transform.rotation=Quaternion.FromToRotation(Vector3.up,aim);var body=turtle.GetComponent<SimpleCharacter>().body;body.localScale=new Vector3(1,1-hit*.12f,1);});
 Destroy(stick);File.WriteAllText(Path.Combine(output,"complete.txt"),"Capture complete");EditorApplication.Exit(0);
 }
 IEnumerator Shot(string name,string area,Vector3 eye,Vector3 focus,float seconds,Action<float> action){
 var gm=GameManager.Instance;gm.SwitchArea(area);gm.narration.Stop();gm.Subtitle.Hide();gm.Menu.Hide();gm.Choice.Hide();typeof(GameManager).GetMethod("ShowHud",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(gm,new object[]{false});foreach(var hand in gm.Rig.Hands)foreach(var rr in hand.GetComponentsInChildren<Renderer>())rr.enabled=false;yield return null;
 var tr=gm.CurrentArea.transform;cam.backgroundColor=gm.CurrentArea.skyColor;cam.clearFlags=CameraClearFlags.SolidColor;var start=tr.TransformPoint(eye);var target=tr.TransformPoint(focus);
 animate=k=>{cam.transform.position=start+tr.right*(k-.5f)*.28f;cam.transform.LookAt(target);action?.Invoke(k);};
 var psi=new System.Diagnostics.ProcessStartInfo(ffmpeg,$"-y -loglevel error -f rawvideo -pixel_format rgb24 -video_size 1920x1080 -framerate 30 -i pipe:0 -vf vflip -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p \"{output}/{name}.mp4\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true};encoder=System.Diagnostics.Process.Start(psi);frame=0;recording=true;int total=Mathf.RoundToInt(seconds*30);
 while(frame<total){animate(frame/(float)total);yield return null;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0,false);tex.Apply(false);RenderTexture.active=null;var bytes=tex.GetRawTextureData<byte>().ToArray();encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);frame++;}
 recording=false;encoder.StandardInput.Close();encoder.WaitForExit();if(encoder.ExitCode!=0)throw new Exception("Capture encode failed");File.AppendAllText(Path.Combine(output,"progress.txt"),name+"\n");
 }
}


