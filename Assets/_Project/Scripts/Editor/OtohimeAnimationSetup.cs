using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JPHACKS2026.Editor
{
    /// <summary>
    /// 乙姫のモデルに Mixamo のアニメーションを付ける。
    /// このモデルは独自の19本の骨で、左右の骨のねじれ（ロール）が鏡写しになっていないため、Unity の Humanoid では腕が正しく動かない。
    /// そこで Mixamo の動き（X Bot の骨格・スキンなし）を1フレームずつ再生し、骨ごとに「T ポーズからの向きの変化（ワールド）」を
    /// 乙姫の対応する骨へ写して、乙姫専用の Generic アニメーションとして焼き込む（Baked/*.anim）。
    /// ・T ポーズは両方のモデルで作り直してから比べる（乙姫は腕が斜め下、Mixamo の FBX は動きの最初の姿勢になっているため）。
    ///   背骨・首・頭はまっすぐ上、脚はまっすぐ下、腕は真横に水平
    /// ・状態 Idle / Talk / Walk / Reach / Bow の Animator Controller を作る（切り替えは OtohimeMotion がコードで行う）
    /// ・シーンの乙姫に Animator・OtohimeMotion・羽衣（Hagoromo）を付ける
    /// </summary>
    public static class OtohimeAnimationSetup
    {
        const string ModelPath = "Assets/_Project/Art/Models/Characters/Princess_Hacks.fbx";
        internal const string AnimFolder = "Assets/_Project/Art/Animations/Otohime";
        const string BakedFolder = AnimFolder + "/Baked";
        const string ControllerPath = AnimFolder + "/Otohime.controller";
        const string HagoromoMaterialPath = "Assets/_Project/Art/Materials/Hagoromo.mat";
        const float SampleRate = 30;
        // シーンの Princess model と同じ向き（FBX は取り込むと -X を向くので、+Z を正面にする）
        static readonly Quaternion ModelFacing = Quaternion.Euler(0, 90, 0);

        // 状態名 → (FBX のファイル名, ループするか)
        static readonly (string state, string file, bool loop)[] Clips =
        {
            (OtohimeMotion.Idle, "Otohime_Idle.fbx", true),
            (OtohimeMotion.Talk, "Otohime_Talk.fbx", true),
            (OtohimeMotion.Walk, "Otohime_Walk.fbx", true),
            (OtohimeMotion.Reach, "Otohime_Reach.fbx", false),
            (OtohimeMotion.Bow, "Otohime_Bow.fbx", false),
        };

        // 乙姫の骨 → Mixamo の骨（mixamorig: を除いた名前）。このモデルは骨の名前の左右が乙姫から見た左右と逆（.R が乙姫の左）。
        // 親から順に並べる（子の向きを求めるときに親の向きを使う）
        static readonly (string bone, string mixamo)[] BoneMap =
        {
            ("Bron", "Hips"), ("Spine", "Spine"), ("Body", "Spine2"), ("Head", "Head"),
            ("UpperArme.R", "LeftArm"), ("LowerArm.R", "LeftForeArm"), ("hand.R", "LeftHand"),
            ("UpperArme.L", "RightArm"), ("LowerArm.L", "RightForeArm"), ("hand.L", "RightHand"),
            ("UpperLeg.R", "LeftUpLeg"), ("LowwerLeg.R", "LeftLeg"),
            ("UpperLeg.L", "RightUpLeg"), ("LowwerLeg.L", "RightLeg"),
        };

        // T ポーズでの骨の向き：(骨, 向きを測る子, 子のある方向)。人物は +Z が正面なので、左は -X。親から順に並べる
        static readonly (string bone, string child, Vector3 dir)[] PrincessTPose =
        {
            ("Bron", "Spine", Vector3.up), ("Spine", "Body", Vector3.up), ("Body", "Head", Vector3.up), ("Head", "Head_end", Vector3.up),
            ("UpperArme.R", "LowerArm.R", Vector3.left), ("LowerArm.R", "hand.R", Vector3.left), ("hand.R", "hand.R_end", Vector3.left),
            ("UpperArme.L", "LowerArm.L", Vector3.right), ("LowerArm.L", "hand.L", Vector3.right), ("hand.L", "hand.L_end", Vector3.right),
            ("UpperLeg.R", "LowwerLeg.R", Vector3.down), ("LowwerLeg.R", "LowwerLeg.R_end", Vector3.down),
            ("UpperLeg.L", "LowwerLeg.L", Vector3.down), ("LowwerLeg.L", "LowwerLeg.L_end", Vector3.down),
        };
        static readonly (string bone, string child, Vector3 dir)[] MixamoTPose =
        {
            ("Hips", "Spine", Vector3.up), ("Spine", "Spine1", Vector3.up), ("Spine1", "Spine2", Vector3.up), ("Spine2", "Neck", Vector3.up),
            ("Neck", "Head", Vector3.up), ("Head", "HeadTop_End", Vector3.up),
            ("LeftShoulder", "LeftArm", Vector3.left), ("LeftArm", "LeftForeArm", Vector3.left), ("LeftForeArm", "LeftHand", Vector3.left), ("LeftHand", "LeftHandMiddle1", Vector3.left),
            ("RightShoulder", "RightArm", Vector3.right), ("RightArm", "RightForeArm", Vector3.right), ("RightForeArm", "RightHand", Vector3.right), ("RightHand", "RightHandMiddle1", Vector3.right),
            ("LeftUpLeg", "LeftLeg", Vector3.down), ("LeftLeg", "LeftFoot", Vector3.down),
            ("RightUpLeg", "RightLeg", Vector3.down), ("RightLeg", "RightFoot", Vector3.down),
        };

        public static void SetupInBatch()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
            SetupInScene();
        }

        [MenuItem("JPHACKS2026/Setup Otohime Animations")]
        public static void SetupInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before setting up the princess animations.");
            foreach (var anchor in Object.FindObjectsByType<StoryAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (anchor.id != "ryugu.otohime") continue;
                var visual = anchor.transform.Find("Body/Princess model");
                if (visual == null) throw new InvalidOperationException("Run JPHACKS2026 > Replace Princess Model first.");
                Undo.RegisterFullObjectHierarchyUndo(anchor.gameObject, "Setup princess animations");
                Apply(visual.gameObject, anchor.gameObject);
                EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                EditorSceneManager.SaveScene(anchor.gameObject.scene);
                Selection.activeGameObject = visual.gameObject;
                Debug.Log("[Otohime] Animations and hagoromo set up, saved " + anchor.gameObject.scene.path);
                return;
            }
            throw new InvalidOperationException("Open Main.unity before setting up the princess animations.");
        }

        /// <summary>乙姫のモデル（Princess model）に Animator・動きの切り替え・羽衣を付ける。PrincessModelSetup からも呼ぶ。</summary>
        public static void Apply(GameObject visual, GameObject character)
        {
            UseGenericRig(ModelPath);
            var controller = SetupController();

            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.avatar = null;   // Generic：骨の名前（階層の道筋）で動かす
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var motion = visual.GetComponent<OtohimeMotion>();
            if (motion == null) motion = visual.AddComponent<OtohimeMotion>();
            var touch = character.GetComponentsInChildren<StoryAnchor>(true).FirstOrDefault(a => a.id == "ryugu.otohime.touch");
            motion.reachWhileArmed = touch != null ? touch.GetComponent<BranchTrigger>() : null;

            var hagoromo = visual.GetComponent<Hagoromo>();
            if (hagoromo == null) hagoromo = visual.AddComponent<Hagoromo>();
            hagoromo.anchorBone = FindDeep(visual.transform, "Body");
            hagoromo.headRenderers = visual.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name == "HeadMesh" || r.name == "hear").ToArray();
            hagoromo.material = HagoromoMaterial();
            EditorUtility.SetDirty(visual);
        }

        internal static void UseGenericRig(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException("Model is missing", path);
            if (importer.animationType == ModelImporterAnimationType.Generic && importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar) return;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.SaveAndReimport();
        }

        static AnimatorController SetupController()
        {
            Directory.CreateDirectory(BakedFolder);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var (state, file, loop) in Clips) clips[state] = Bake(state, AnimFolder + "/" + file, loop);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var (state, _, _) in Clips)
            {
                var s = machine.states.Select(c => c.state).FirstOrDefault(x => x.name == state) ?? machine.AddState(state);
                s.motion = clips[state];
                s.writeDefaultValues = true;
            }
            machine.defaultState = machine.states.Select(c => c.state).First(x => x.name == OtohimeMotion.Idle);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static AnimationClip Bake(string state, string sourcePath, bool loop) =>
            Bake(ModelPath, BakedFolder + "/Otohime_" + state + ".anim", sourcePath, loop);

        /// <summary>Mixamo の動きを乙姫の骨へ写して .anim に焼き込む。少年（Boy_Hacks.fbx）も同じ骨格なので BoyModelSetup からも使う。</summary>
        internal static AnimationClip Bake(string targetModelPath, string path, string sourcePath, bool loop)
        {
            UseGenericRig(sourcePath);
            var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var princessModel = AssetDatabase.LoadAssetAtPath<GameObject>(targetModelPath);
            if (source == null || sourceModel == null || princessModel == null) throw new FileNotFoundException("Animation or model is missing", sourcePath);

            var mixamo = Object.Instantiate(sourceModel);
            var holder = new GameObject("Princess bake holder");
            var princess = Object.Instantiate(princessModel, holder.transform, false);
            princess.transform.localRotation = ModelFacing;
            try
            {
                var pairs = BoneMap.Select(m =>
                {
                    var p = FindDeep(princess.transform, m.bone);
                    var x = FindMixamo(mixamo.transform, m.mixamo);
                    if (p == null || x == null) throw new InvalidOperationException($"Bone is missing: {m.bone} / {m.mixamo}");
                    return (p, x);
                }).ToArray();

                // 両方を T ポーズにしてから、その向きを覚える
                foreach (var (bone, child, dir) in PrincessTPose) Aim(FindDeep(princess.transform, bone), FindDeep(princess.transform, child), dir);
                foreach (var (bone, child, dir) in MixamoTPose) Aim(FindMixamo(mixamo.transform, bone), FindMixamo(mixamo.transform, child), dir);
                var princessT = pairs.Select(q => q.p.rotation).ToArray();
                var mixamoT = pairs.Select(q => q.x.rotation).ToArray();

                int frames = Mathf.Max(2, Mathf.RoundToInt(source.length * SampleRate) + 1);
                var curves = pairs.Select(_ => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() }).ToArray();
                var last = new Quaternion[pairs.Length];
                for (int f = 0; f < frames; f++)
                {
                    float time = Mathf.Min(source.length, f / SampleRate);
                    source.SampleAnimation(mixamo, time);
                    for (int i = 0; i < pairs.Length; i++)
                    {
                        var (p, x) = pairs[i];
                        // 向きの変化をワールドで写す：乙姫(今) = Mixamo(今) × Mixamo(T)⁻¹ × 乙姫(T)
                        var world = x.rotation * Quaternion.Inverse(mixamoT[i]) * princessT[i];
                        p.rotation = world;   // 親から順に置くので、子は親の新しい向きを基準にできる
                        var local = p.localRotation;
                        if (f > 0 && Quaternion.Dot(local, last[i]) < 0) local = new Quaternion(-local.x, -local.y, -local.z, -local.w);
                        last[i] = local;
                        curves[i][0].AddKey(time, local.x); curves[i][1].AddKey(time, local.y);
                        curves[i][2].AddKey(time, local.z); curves[i][3].AddKey(time, local.w);
                    }
                }

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
                clip.ClearCurves();
                clip.frameRate = SampleRate;
                for (int i = 0; i < pairs.Length; i++)
                {
                    var bonePath = AnimationUtility.CalculateTransformPath(pairs[i].p, princess.transform);
                    string[] props = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                    for (int c = 0; c < 4; c++)
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), props[c]), curves[i][c]);
                }
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                return clip;
            }
            finally
            {
                Object.DestroyImmediate(mixamo);
                Object.DestroyImmediate(holder);
            }
        }

        /// <summary>骨を、子のある方向が target を向くように回す（ねじれは変えない）。</summary>
        static void Aim(Transform bone, Transform child, Vector3 target)
        {
            if (bone == null || child == null) throw new InvalidOperationException("T-pose bone is missing: " + (bone == null ? "bone" : child.name));
            var dir = child.position - bone.position;
            if (dir.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(dir, target) * bone.rotation;
        }

        static Transform FindMixamo(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name || t.name.EndsWith(":" + name));

        static Material HagoromoMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(HagoromoMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, HagoromoMaterialPath);
            }
            Hagoromo.SetupMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
