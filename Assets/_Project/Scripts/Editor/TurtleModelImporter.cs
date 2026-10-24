using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace JPHACKS2026.Editor
{
    /// <summary>
    /// ウミガメのモデル（SeaTurtle.fbx）を使えるようにする。
    /// ・Rig を Generic にし、アニメーション「Swim」「Float」をループ再生にする
    /// ・それぞれを再生するだけの Animator Controller を作る（泳ぐ亀＝Swim、浜辺などで待つ亀＝Float）
    /// </summary>
    public static class TurtleModelImporter
    {
        public const string ModelPath = "Assets/_Project/Art/Models/Turtle/SeaTurtle.fbx";
        const string ControllerDir = "Assets/_Project/Art/Animations";
        public const string SwimControllerPath = ControllerDir + "/TurtleSwim.controller";
        public const string FloatControllerPath = ControllerDir + "/TurtleFloat.controller";
        const string ImportVersion = "SeaTurtle-v1";

        /// <summary>インポート設定とコントローラーを用意する。シーン生成（MainSceneBuilder）から呼ぶ。</summary>
        [MenuItem("JPHACKS2026/Refresh Turtle Model")]
        public static void Setup()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Turtle] モデルが見つかりません: " + ModelPath);
                return;
            }

            if (importer.userData != ImportVersion)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                // FBXの中の名前は「SeaTurtle|SeaTurtle|Swim」のようになっているので、最後の部分を名前にする
                var clips = importer.defaultClipAnimations;
                foreach (var c in clips)
                {
                    int bar = c.name.LastIndexOf('|');
                    if (bar >= 0) c.name = c.name.Substring(bar + 1);
                    c.loopTime = true;
                }
                importer.clipAnimations = clips;
                importer.userData = ImportVersion;
                importer.SaveAndReimport();
            }

            EnsureController(SwimControllerPath, "Swim");
            EnsureController(FloatControllerPath, "Float");
        }

        static void EnsureController(string path, string clipName)
        {
            var clip = LoadClip(clipName);
            if (clip == null)
            {
                Debug.LogWarning($"[Turtle] アニメーション「{clipName}」が見つかりません: {ModelPath}");
                return;
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                Directory.CreateDirectory(ControllerDir);
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            var sm = controller.layers[0].stateMachine;
            if (sm.defaultState != null && sm.defaultState.motion == clip) return;
            foreach (var s in sm.states) sm.RemoveState(s.state);
            sm.defaultState = sm.AddState(clipName);
            sm.defaultState.motion = clip;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        static AnimationClip LoadClip(string name)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (a is AnimationClip c && c.name == name) return c;
            return null;
        }

        public static RuntimeAnimatorController LoadController(bool swimming) =>
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(swimming ? SwimControllerPath : FloatControllerPath);
    }
}
