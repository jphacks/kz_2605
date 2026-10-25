using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JPHACKS2026.Editor
{
    public static class BanquetHallModelSetup
    {
        const string ModelPath = "Assets/_Project/Art/Models/enkaijou/enkaijou.fbx";
        const string PrefabPath = "Assets/Resources/Scenery/Enkaijou.prefab";
        const string MaterialFolder = "Assets/_Project/Art/Materials/Enkaijou";

        public static void ReplaceInBatch()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
            ReplaceInScene();
        }

        [MenuItem("JPHACKS2026/Replace Banquet Hall Model")]
        public static void ReplaceInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play Mode before replacing the banquet hall.");
            Setup();
            foreach (var area in Object.FindObjectsByType<Area>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (area.areaId != "ryugu") continue;
                Undo.RegisterFullObjectHierarchyUndo(area.gameObject, "Replace banquet hall");
                var old = area.transform.Find(BanquetHallVisual.InstanceName);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                BanquetHallVisual.Build(area);
                EditorSceneManager.MarkSceneDirty(area.gameObject.scene);
                EditorSceneManager.SaveScene(area.gameObject.scene);
                Selection.activeGameObject = area.transform.Find(BanquetHallVisual.InstanceName).gameObject;
                Debug.Log("[Enkaijou] Replaced banquet hall and saved " + area.gameObject.scene.path);
                return;
            }
            throw new System.InvalidOperationException("Open Main.unity before replacing the banquet hall.");
        }

        public static void Setup()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new FileNotFoundException("Banquet hall FBX not found", ModelPath);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var root = new GameObject(BanquetHallVisual.InstanceName);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                visual.transform.SetParent(root.transform, false);
                visual.name = "Banquet room model";
                var tatami = Find(visual.transform, "Tatami_40");
                if (tatami == null) throw new System.InvalidOperationException("Tatami_40 is missing from the FBX.");
                var floor = BoundsOf(tatami);
                visual.transform.position -= new Vector3(floor.center.x, floor.max.y, floor.center.z);
                foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera.gameObject);
                foreach (var light in visual.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var materials = new Dictionary<Material, Material>();
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var source = slots[i];
                        if (source == null) continue;
                        if (!materials.TryGetValue(source, out var converted))
                        {
                            var path = MaterialFolder + "/" + source.name + ".mat";
                            converted = AssetDatabase.LoadAssetAtPath<Material>(path);
                            if (converted == null)
                            {
                                converted = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name, enableInstancing = true };
                                var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                                converted.SetColor("_BaseColor", color);
                                converted.SetFloat("_Smoothness", source.name.Contains("Urushi") || source.name.Contains("Lacquer") ? .55f : .2f);
                                if (source.name.Contains("LampPaper"))
                                {
                                    converted.EnableKeyword("_EMISSION");
                                    converted.SetColor("_EmissionColor", new Color(1f, .76f, .44f) * 1.3f);
                                }
                                AssetDatabase.CreateAsset(converted, path);
                            }
                            materials[source] = converted;
                            var texture = SourceTexture(source.name);
                            if (texture != null && converted.GetTexture("_BaseMap") == null)
                            {
                                converted.SetTexture("_BaseMap", texture);
                                converted.SetColor("_BaseColor", Color.white);
                                if (source.name.Contains("LampPaper")) converted.SetTexture("_EmissionMap", texture);
                                EditorUtility.SetDirty(converted);
                            }
                        }
                        slots[i] = converted;
                    }
                    renderer.sharedMaterials = slots;
                }
                // Align two model tables with the existing reachable food interactions.
                PlaceTable(visual.transform, "Zataku_00", new Vector3(-.8f, .4f, .3f));
                PlaceTable(visual.transform, "Zataku_06", new Vector3(.8f, .4f, .3f));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Enkaijou] Prepared model prefab: " + PrefabPath + "; bounds=" + BoundsOf(visual.transform));
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }

        static Texture2D SourceTexture(string material)
        {
            string[,] textures = {
                {"CeilingCedar", "hall_ceiling_wood"}, {"Hinoki", "hall_hinoki_wood"},
                {"Ranma", "hall_ranma"}, {"Keyaki_Urushi", "zataku_wood_basecolor"},
                {"LampPaper", "hall_ceiling_light"}, {"Heri", "heri_basecolor"},
                {"Igusa", "igusa_basecolor"}, {"(Back)", "back_basecolor"},
                {"FusumaR", "hall_fusuma_r"}, {"(Fusuma)", "hall_fusuma"},
                {"Shoji", "hall_shoji"}, {"Kakejiku", "hall_kakejiku"}
            };
            for (int i = 0; i < textures.GetLength(0); i++)
                if (material.Contains(textures[i, 0]))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Models/enkaijou/Textures/" + textures[i, 1] + ".png");
            return null;
        }

        static Bounds BoundsOf(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new System.InvalidOperationException("Model has no renderers: " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static void PlaceTable(Transform root, string name, Vector3 topCenter)
        {
            var table = Find(root, name);
            if (table == null) throw new System.InvalidOperationException("Missing table: " + name);
            var bounds = BoundsOf(table);
            table.position += topCenter - new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
    }
}
