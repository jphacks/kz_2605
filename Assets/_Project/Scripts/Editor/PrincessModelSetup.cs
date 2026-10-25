using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JPHACKS2026.Editor
{
    public static class PrincessModelSetup
    {
        const string ModelPath = "Assets/_Project/Art/Models/Characters/Princess_Hacks.fbx";
        const string TexturePath = "Assets/_Project/Art/Models/Characters/JPHacks_Princess.png";
        const string MaterialPath = "Assets/_Project/Art/Materials/Princess.mat";

        [MenuItem("JPHACKS2026/Replace Princess Model")]
        public static void ReplaceInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before replacing the princess.");
            foreach (var anchor in Object.FindObjectsByType<StoryAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (anchor.gameObject.name != "Otohime") continue;
                Undo.RegisterFullObjectHierarchyUndo(anchor.gameObject, "Replace princess model");
                ReplaceVisual(anchor.gameObject);
                EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                EditorSceneManager.SaveScene(anchor.gameObject.scene);
                Selection.activeGameObject = anchor.transform.Find("Body/Princess model").gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
                Debug.Log("[Princess] Replaced model and saved " + anchor.gameObject.scene.path);
                return;
            }
            throw new InvalidOperationException("Open Main.unity before replacing the princess.");
        }

        public static void ReplaceVisual(GameObject character)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (model == null || texture == null) throw new FileNotFoundException("Princess model or texture is missing.");
            var body = character.transform.Find("Body");
            if (body == null) throw new InvalidOperationException("Princess Body is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .15f);
            material.SetFloat("_Cull", 0); // The skirt and ribbons include single-sided surfaces.
            EditorUtility.SetDirty(material);

            var previous = body.Find("Princess model");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, body);
            visual.name = "Princess model";
            visual.transform.localPosition = Vector3.zero;
            // The FBX faces -X after Unity import; character movement and LookRotation use +Z.
            visual.transform.localRotation = Quaternion.Euler(0, 90, 0);
            visual.transform.localScale = Vector3.one;
            foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera);
            foreach (var light in visual.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Princess has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
            if (bounds.size.y <= .0001f) throw new InvalidOperationException("Princess has invalid bounds.");
            visual.transform.localScale *= 1.6f / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position += body.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (Transform child in body)
                if (child != visual.transform)
                    foreach (var renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            // Mixamo のアニメーションと羽衣を付ける（置き直したときも外れないように）
            OtohimeAnimationSetup.Apply(visual, character);
            AssetDatabase.SaveAssets();
        }
    }
}
