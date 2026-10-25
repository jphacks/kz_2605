using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JPHACKS2026.Editor
{
    /// <summary>浜辺の子どもたち（Child0〜2）の仮モデルを Boy_Hacks.fbx に置き換える。服の色はテクスチャで青・橙・緑に分ける。</summary>
    public static class BoyModelSetup
    {
        const string ModelPath = "Assets/_Project/Art/Models/Characters/Boy_Hacks.fbx";
        const string Dir = "Assets/_Project/Art/Models/Characters/";
        static readonly string[] TexturePaths = { Dir + "JPHacks_Boys.png", Dir + "JPHacks_Boy_Orange.png", Dir + "JPHacks_Boy_Green.png" };
        static readonly string[] MaterialPaths = { "Assets/_Project/Art/Materials/Boy_Blue.mat", "Assets/_Project/Art/Materials/Boy_Orange.mat", "Assets/_Project/Art/Materials/Boy_Green.mat" };
        const string VisualName = "Boy model";
        const float Height = 1.15f;

        [MenuItem("JPHACKS2026/Replace Boy Models")]
        public static void ReplaceInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before replacing the boys.");
            controller = null;   // メニューから実行したときは動きを焼き直す
            int replaced = 0;
            GameObject first = null;
            foreach (var anchor in Object.FindObjectsByType<StoryAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!anchor.id.StartsWith("beach.child.")) continue;
                if (!int.TryParse(anchor.id.Substring("beach.child.".Length), out var index)) continue;
                Undo.RegisterFullObjectHierarchyUndo(anchor.gameObject, "Replace boy model");
                ReplaceVisual(anchor.gameObject, index);
                EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                if (first == null) first = anchor.gameObject;
                replaced++;
            }
            if (replaced == 0) throw new InvalidOperationException("Open Main.unity before replacing the boys.");
            EditorSceneManager.SaveScene(first.scene);
            Selection.activeGameObject = first.transform.Find("Body/" + VisualName).gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"[Boy] Replaced {replaced} models and saved {first.scene.path}");
        }

        public static void ReplaceVisual(GameObject character, int index)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePaths[index % TexturePaths.Length]);
            if (model == null || texture == null) throw new FileNotFoundException("Boy model or texture is missing.");
            var body = character.transform.Find("Body");
            if (body == null) throw new InvalidOperationException("Boy Body is missing.");
            var materialPath = MaterialPaths[index % MaterialPaths.Length];
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .15f);
            material.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(material);

            var previous = body.Find(VisualName);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, body);
            visual.name = VisualName;
            visual.transform.localPosition = Vector3.zero;
            // 姫と同じ書き出し設定なので、取り込み後は -X 向き。キャラクターの正面 +Z に合わせる
            visual.transform.localRotation = Quaternion.Euler(0, 90, 0);
            visual.transform.localScale = Vector3.one;
            foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera);
            foreach (var light in visual.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Boy has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
            if (bounds.size.y <= .0001f) throw new InvalidOperationException("Boy has invalid bounds.");
            visual.transform.localScale *= Height / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position += body.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (Transform child in body)
                if (child != visual.transform)
                    foreach (var renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            ApplyAnimation(visual);
            AssetDatabase.SaveAssets();
        }

        // 状態名 → (乙姫用に取り込んだ Mixamo の FBX, ループするか)
        static readonly (string state, string file, bool loop)[] Clips =
        {
            (BoyMotion.Idle, "Otohime_Idle.fbx", true),
            (BoyMotion.Walk, "Otohime_Walk.fbx", true),
            (BoyMotion.Reach, "Otohime_Reach.fbx", false),
        };
        const string AnimFolder = "Assets/_Project/Art/Animations/Boy";
        const string ControllerPath = AnimFolder + "/Boy.controller";

        static AnimatorController controller;

        /// <summary>乙姫と同じ骨格なので、同じ Mixamo の動きを少年の骨へ焼き込んで Animator と BoyMotion を付ける。</summary>
        static void ApplyAnimation(GameObject visual)
        {
            OtohimeAnimationSetup.UseGenericRig(ModelPath);
            if (controller == null) controller = SetupController();
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.avatar = null;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (visual.GetComponent<BoyMotion>() == null) visual.AddComponent<BoyMotion>();
            EditorUtility.SetDirty(visual);
        }

        static AnimatorController SetupController()
        {
            Directory.CreateDirectory(AnimFolder + "/Baked");
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                       ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = ctrl.layers[0].stateMachine;
            foreach (var (state, file, loop) in Clips)
            {
                var clip = OtohimeAnimationSetup.Bake(ModelPath, AnimFolder + "/Baked/Boy_" + state + ".anim",
                    OtohimeAnimationSetup.AnimFolder + "/" + file, loop);
                var s = machine.states.Select(c => c.state).FirstOrDefault(x => x.name == state) ?? machine.AddState(state);
                s.motion = clip;
                s.writeDefaultValues = true;
            }
            machine.defaultState = machine.states.Select(c => c.state).First(x => x.name == BoyMotion.Idle);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }
    }
}
