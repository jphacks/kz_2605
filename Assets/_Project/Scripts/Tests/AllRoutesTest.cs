using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JPHACKS2026.Tests
{
    /// <summary>
    /// 全9エンディング・本線の自動進行・「直前の選択肢に戻る」・環境を検証する。
    /// 選択は GameInput.DebugQueue に番号を積んで行う（数字キー入力と同じ扱い）。
    /// </summary>
    public class AllRoutesTest
    {
        readonly List<string> exceptions = new();

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) exceptions.Add(condition + "\n" + stackTrace);
            if (type == LogType.Error && condition.StartsWith("[StoryAnchor]")) exceptions.Add(condition);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;   // XRランタイムがない環境のエラーは無視
            exceptions.Clear();
            Application.logMessageReceived += OnLog;
            SaveData.ResetAll();
            GameInput.DebugQueue.Clear();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var gm = GameManager.Instance;
            Assert.IsNotNull(gm, "GameManager がシーンにありません");
            gm.narration.secondsPerChar = 0.002f;
            gm.narration.minDuration = 0.02f;
            gm.narration.gapBetweenLines = 0f;
            gm.story.rideDuration = 0.5f;
            Time.timeScale = 6f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
            GameInput.DebugQueue.Clear();
            yield return null;
        }

        static IEnumerator WaitUntilState(GameState state, float timeout = 120f)
        {
            float t = 0;
            while (GameManager.Instance.State != state && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator PlayRoute(EndingId expected, params int[] inputs)
        {
            yield return WaitUntilState(GameState.Title);
            foreach (var i in inputs) GameInput.DebugQueue.Enqueue(i);

            float t = 0;
            while (!SaveData.HasEnding(expected) && t < 150f)
            {
                t += Time.unscaledDeltaTime;
                Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
                yield return null;
            }
            Assert.IsTrue(SaveData.HasEnding(expected), $"{expected} に到達しませんでした（残り入力 {GameInput.DebugQueue.Count}）");
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        // 入力：絵本(0) → 物語を始める(0) → 各分岐の選択 → 「おわり」でタイトルへ(0)
        [UnityTest] public IEnumerator ED1_見過ごす() { yield return PlayRoute(EndingId.ED1, 0, 0, 1, 0); }
        [UnityTest] public IEnumerator ED2_いたずら() { yield return PlayRoute(EndingId.ED2, 0, 0, 2, 0); }
        [UnityTest] public IEnumerator ED3_行かない() { yield return PlayRoute(EndingId.ED3, 0, 0, 0, 1, 0); }
        [UnityTest] public IEnumerator ED4_探索() { yield return PlayRoute(EndingId.ED4, 0, 0, 0, 0, 1, 0, 0, 0, 0); }
        [UnityTest] public IEnumerator ED5_帰らない() { yield return PlayRoute(EndingId.ED5, 0, 0, 0, 0, 0, 1, 0); }
        [UnityTest] public IEnumerator ED6_開ける() { yield return PlayRoute(EndingId.ED6, 0, 0, 0, 0, 0, 0, 0, 0); }
        [UnityTest] public IEnumerator ED7_開けない() { yield return PlayRoute(EndingId.ED7, 0, 0, 0, 0, 0, 0, 1, 0); }
        [UnityTest] public IEnumerator ED8_おじさんにあげる() { yield return PlayRoute(EndingId.ED8, 0, 0, 0, 0, 0, 0, 2, 0); }
        [UnityTest] public IEnumerator ED9_乙姫から逃げる() { yield return PlayRoute(EndingId.ED9, 0, 0, 0, 0, 1, 0, 0, 1, 0); }

        [UnityTest]
        public IEnumerator 環境モデルと操作の反映()
        {
            var gm=GameManager.Instance;
            yield return WaitUntilState(GameState.Title);
            var title=gm.GetArea("title");
            Assert.IsFalse(title.transform.Find("Castle").gameObject.activeSelf);
            Assert.IsFalse(title.transform.Find("MoonReflection").gameObject.activeSelf);
            var coverTitle=title.GetComponentInChildren<Picturebook>().coverHinge.Find("Urashima cover/浦島太郎");
            Assert.IsNotNull(coverTitle);
            Assert.Less(coverTitle.GetComponent<Renderer>().bounds.size.x,.33f,"表紙画像は本の幅に収める");
            Assert.AreSame(Resources.Load<Texture2D>("Book/UrashimaCover"),coverTitle.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"));
            Assert.IsNotNull(title.transform.Find("Window moonlight").GetComponent<Light>().cookie);
            Assert.AreEqual(LightShadows.Hard,gm.sun.shadows);
            Assert.IsTrue(gm.Rig.rightHand.GetComponent<HandGrabber>().useTrigger);
            gm.Choice.Show(new ChoiceDef {question="操作確認",timeLimit=5,options=new[] {new ChoiceDef.Option("行く","トリガーで決定") {button=true}}});
            gm.Choice.GetComponentInChildren<VRButton>().Click();
            Assert.AreEqual(0,gm.Choice.SelectedIndex,"カメへかがまずボタンで選択できる");
            gm.Choice.Hide();
            CaptureView("Title",title.transform.TransformPoint(new Vector3(.3f,1.55f,-.8f)),title.transform.TransformPoint(new Vector3(0,1.3f,2.6f)));

            gm.SwitchArea("beach"); yield return null;
            var beach=gm.GetArea("beach");
            Assert.IsNotNull(beach.GetComponentInChildren<ShorelineWaves>());
            Assert.IsNotNull(beach.transform.Find("SceneryDetails/Fishing boat/Blender FishingBoat"));
            foreach(Transform tr in beach.transform)
                if(tr.name=="Driftwood")
                {
                    Assert.IsNotNull(tr.GetComponent<Grabbable>());
                    var meshCollider=tr.GetComponent<MeshCollider>();
                    if(meshCollider!=null) Assert.IsTrue(meshCollider.convex);
                }
            gm.story.FleeBeachChildren(40);
            yield return Tween.Wait(1f);
            for(int i=0;i<3;i++)
            {
                var child=StoryAnchor.Get("beach.child."+i);
                Assert.Less(Vector3.Distance(child.position,beach.transform.TransformPoint(new Vector3(-12+i*.6f,0,-6))),.08f);
            }
            CaptureView("Beach",beach.transform.TransformPoint(new Vector3(-2,1.8f,-4)),beach.transform.TransformPoint(new Vector3(-6,.4f,6)));

            gm.SwitchArea("sea"); yield return null;
            var sea=gm.GetArea("sea");
            int models=0;
            foreach(var model in sea.GetComponentsInChildren<SceneryModelVisual>()) if(model.tail!=null) models++;
            Assert.GreaterOrEqual(models,24);
            CaptureView("Undersea",sea.transform.TransformPoint(new Vector3(0,.5f,3)),sea.transform.TransformPoint(new Vector3(3,0,12)));

            gm.SwitchArea("ryugu"); yield return null;
            var ryugu=gm.GetArea("ryugu");
            Assert.IsNotNull(ryugu.transform.Find("SceneryDetails/Forbidden archive"));
            RenderSettings.ambientLight=new Color(.10f,.13f,.16f);
            RenderSettings.fogColor=new Color(.025f,.045f,.06f);
            gm.sun.intensity=.15f;
            CaptureView("Archive",ryugu.transform.TransformPoint(new Vector3(4,1.5f,14.5f)),ryugu.transform.TransformPoint(new Vector3(4,1.4f,17.8f)));
            Assert.IsEmpty(exceptions,string.Join("\n",exceptions));
        }

        static void CaptureView(string name,Vector3 position,Vector3 focus)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var go=new GameObject("Visual review camera");
            var camera=go.AddComponent<Camera>(); camera.enabled=false;
            camera.transform.position=position; camera.transform.LookAt(focus);
            camera.fieldOfView=65; camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=GameManager.Instance.CurrentArea.skyColor;
            var target=new RenderTexture(1280,900,24);
            camera.targetTexture=target;
            var previous=RenderTexture.active;
            camera.Render(); RenderTexture.active=target;
            var texture=new Texture2D(1280,900,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1280,900),0,0); texture.Apply();
            var folder=Path.Combine(Application.dataPath,"../SourceArt/Scenery");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,"Unity_"+name+".png"),texture.EncodeToPNG());
            RenderTexture.active=previous; camera.targetTexture=null; target.Release();
            Object.Destroy(target); Object.Destroy(texture); Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator BanquetModelsAndInteractions()
        {
            yield return WaitUntilState(GameState.Title);
            var gm = GameManager.Instance;
            gm.StopAllCoroutines();
            gm.SwitchArea("ryugu");
            yield return null;
            var area = gm.GetArea("ryugu");
            var hall = area.transform.Find("Enkaijou");
            Assert.IsNotNull(hall);
            Assert.IsNotNull(hall.Find("Banquet room model/Tatami_40"));
            Assert.IsFalse(area.transform.Find("HallFloor").GetComponent<Renderer>().enabled);
            Assert.IsFalse(area.transform.Find("TableL").GetComponent<Renderer>().enabled);
            Assert.IsNotNull(StoryAnchor.Get<BringToHeadTrigger>("ryugu.eat"));
            Assert.IsNotNull(StoryAnchor.Get<ZoneTrigger>("ryugu.secret.enter"));
            foreach (var renderer in hall.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            int count = 0;
            foreach (var model in area.GetComponentsInChildren<SceneryModelVisual>())
            {
                if (!model.name.StartsWith("Blender Banquet")) continue;
                count++;
                Assert.IsNotNull(model.tail);
                Assert.IsNotNull(model.finLeft);
                Assert.IsNotNull(model.finRight);
                Assert.AreEqual(4, model.GetComponentsInChildren<MeshFilter>().Length);
                var swimmer = model.GetComponentInParent<Swimmer>();
                swimmer.SpeedMultiplier = 0;
                var rest = model.tail.localRotation;
                yield return Tween.Wait(.2f);
                Assert.Less(Quaternion.Angle(rest, model.tail.localRotation), .01f);
            }
            Assert.AreEqual(6, count);
            CaptureView("Banquet", area.transform.TransformPoint(new Vector3(0,1.6f,-3.4f)), area.transform.TransformPoint(new Vector3(0,1.3f,3)));
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        [UnityTest]
        public IEnumerator 直前の選択肢に戻って別エンド()
        {
            // ED1のあと「直前の選択肢に戻る」(1) → 浜辺の分岐で「いたずらする」(2) → ED2
            yield return PlayRoute(EndingId.ED1, 0, 0, 1, 1, 2, 0);
            float t = 0;
            while (!SaveData.HasEnding(EndingId.ED2) && t < 120f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(SaveData.HasEnding(EndingId.ED2), "リトライ後に ED2 に到達しませんでした");
            Assert.AreEqual(2, SaveData.EndingCount);
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        [UnityTest]
        public IEnumerator 海の波音はエリア切替に追従する()
        {
            yield return WaitUntilState(GameState.Title);
            var gm = GameManager.Instance;
            gm.StopAllCoroutines();
            gm.SwitchArea("beach");
            var beachSound = gm.GetArea("beach").GetComponentInChildren<OceanAmbience>().GetComponent<AudioSource>();
            Assert.IsNotNull(beachSound.clip);
            Assert.IsTrue(beachSound.loop);
            Assert.AreEqual(0, beachSound.spatialBlend);
            yield return Tween.Wait(.5f);
            Assert.Greater(beachSound.volume, 0);
            gm.SwitchArea("sea");
            Assert.IsFalse(beachSound.isPlaying);
            var seaSound = gm.GetArea("sea").GetComponentInChildren<OceanAmbience>().GetComponent<AudioSource>();
            Assert.AreSame(beachSound.clip, seaSound.clip);
            Assert.IsTrue(seaSound.loop);
            Assert.AreEqual(850, seaSound.GetComponent<AudioLowPassFilter>().cutoffFrequency);
            gm.SwitchArea("ryugu");
            Assert.IsFalse(seaSound.isPlaying);
            Assert.IsNull(gm.GetArea("ryugu").GetComponentInChildren<OceanAmbience>());
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        [UnityTest]
        public IEnumerator 数日後は朝の釣り場に切り替わり救出場面に戻せる()
        {
            yield return WaitUntilState(GameState.Title);
            var gm = GameManager.Instance;
            gm.StopAllCoroutines();
            gm.SwitchArea("beach");
            var beach = gm.GetArea("beach");
            var originalTurtle = StoryAnchor.Get("beach.turtle").position;
            yield return gm.story.StageInvitation(true);
            for (int i = 0; i < 3; i++) Assert.IsFalse(StoryAnchor.Get("beach.child." + i).gameObject.activeSelf);
            Assert.IsTrue(beach.transform.Find("Invitation fishing spot").gameObject.activeSelf);
            Assert.Greater(Vector3.Distance(originalTurtle, StoryAnchor.Get("beach.turtle").position), 8f);
            Assert.Less(Vector3.Distance(gm.Rig.transform.position, beach.transform.TransformPoint(new Vector3(2.3f, 0, 5.7f))), .1f);
            var sky = gm.Rig.Head.GetComponent<Camera>().backgroundColor;
            Assert.Greater(sky.r, sky.b);
            Assert.IsTrue(gm.Rig.LocomotionEnabled);
            gm.SwitchArea("beach");
            Assert.IsFalse(beach.transform.Find("Invitation fishing spot").gameObject.activeSelf);
            Assert.IsTrue(StoryAnchor.Get("beach.child.0").gameObject.activeSelf);
            Assert.Less(Vector3.Distance(originalTurtle, StoryAnchor.Get("beach.turtle").position), .01f);
            Assert.AreEqual(beach.skyColor, gm.Rig.Head.GetComponent<Camera>().backgroundColor);
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        [UnityTest]
        public IEnumerator 時間切れでも本線で最後まで進む()
        {
            yield return WaitUntilState(GameState.Title);
            GameManager.Instance.story.walkChoiceTime = 0.5f;
            GameManager.Instance.story.choiceTime = 0.5f;
            GameInput.DebugQueue.Enqueue(0);
            GameInput.DebugQueue.Enqueue(0);
            float t = 0;
            while (!SaveData.HasEnding(EndingId.ED6) && t < 120f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(SaveData.HasEnding(EndingId.ED6), "無操作時は本線のED6まで進む");
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }
    }
}
