using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JPHACKS2026.Editor
{
    /// <summary>
    /// 仮素材（プリミティブ）だけで全エリアを組み立てた Main.unity を生成する。
    /// メニュー：JPHACKS2026 > Build Main Scene
    /// 本番モデルが届いたら、生成したシーンの仮モデル（Visualの子）を差し替える。StoryAnchorやトリガーは残すこと。
    /// 注意：再生成すると Main.unity は上書きされる（手作業の変更は消える）。
    /// </summary>
    public static class MainSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        const string MatDir = "Assets/_Project/Art/Materials/Placeholder";
        const string FontPath = "Assets/_Project/UI/Fonts/NotoSansJP/NotoSansJP-Bold SDF.asset";

        static readonly Dictionary<string, Material> mats = new();

        [MenuItem("JPHACKS2026/Build Main Scene")]
        public static void BuildMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Main.unity を再生成", "既存の Main.unity を上書きします。よろしいですか？", "上書きする", "キャンセル"))
                return;
            Build();
        }

        [MenuItem("JPHACKS2026/Reset Save Data")]
        public static void ResetSave()
        {
            SaveData.ResetAll();
            Debug.Log("[JPHACKS2026] セーブデータ（達成エンディング・オートセーブ）を消去しました。");
        }

        /// <summary>バッチモードからも呼べる本体。</summary>
        public static void Build()
        {
            mats.Clear();
            Directory.CreateDirectory(MatDir);
            TamatebakoMaterialImporter.Setup();   // 玉手箱のモデルにマテリアル（.mat）を割り当ててから組み立てる
            TurtleModelImporter.Setup();          // 亀のモデルのアニメーション設定
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);

            var rig = BuildPlayer();
            BuildSystems(rig, sun);

            BuildTitle(new Vector3(0, 0, 0));
            BuildBeach(new Vector3(100, 0, 0));
            BuildSea(new Vector3(200, 0, 0));
            BuildRyugu(new Vector3(300, 0, 0));
            BanquetHallModelSetup.Setup();
            foreach (var area in Object.FindObjectsByType<Area>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                BanquetHallVisual.Build(area);
            BuildVillage(new Vector3(400, 0, 0));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[JPHACKS2026] Main.unity を生成しました: " + ScenePath);
        }

        static void AddToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) list.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ── 共通ヘルパー ─────────────────────────────

        static Material Mat(string name, Color color, float emission = 0f, bool unlit = false)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            if (emission > 0)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        /// <summary>光の演出用：半透明・両面表示のUnlit。色と透明度は Picturebook が MaterialPropertyBlock で変える。</summary>
        static Material GlowMat(string name)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            var m = Mat(name, new Color(1f, 0.9f, 0.62f, 0.5f), unlit: true);
            m.SetFloat("_Surface", 1);   // Transparent
            m.SetFloat("_Blend", 0);     // Alpha
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);   // 光の球の内側からも見えるように
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>開いたページから立ちのぼる光の粒。Picturebook.SetGlow が放出量を決める。</summary>
        static ParticleSystem BuildBookSparkles(Transform book)
        {
            var go = Group("Sparkles", book, new Vector3(-0.09f, 0.07f, 0));
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.022f);
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.15f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(1.2f);   // くるくると渦を巻いて昇る
            vel.radial = new ParticleSystem.MinMaxCurve(0.05f);
            vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.3f, 1), new Keyframe(1, 0)));
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Mesh;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            psr.mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            psr.sharedMaterial = Mat("Mote", new Color(1f, 0.88f, 0.55f), 2.5f, unlit: true);
            return ps;
        }

        static GameObject Group(string name, Transform parent, Vector3 localPos, float yaw = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat,
            Vector3 euler = default, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static T Anchor<T>(GameObject go, string id) where T : Component
        {
            go.AddComponent<StoryAnchor>().id = id;
            return go.GetComponent<T>();
        }

        static void Anchor(GameObject go, string id) => go.AddComponent<StoryAnchor>().id = id;

        static Transform Point(string name, Transform parent, Vector3 pos, float yaw, string id)
        {
            var go = Group(name, parent, pos, yaw);
            Anchor(go, id);
            return go.transform;
        }

        static Area MakeArea(string id, Vector3 origin, Vector3 spawnPos, float spawnYaw)
        {
            var go = new GameObject("Area_" + id);
            go.transform.position = origin;
            var area = go.AddComponent<Area>();
            area.areaId = id;
            area.spawnPoint = Point("Spawn", go.transform, spawnPos, spawnYaw, id + ".spawn");
            return area;
        }

        static GameObject Ring(string name, Transform parent, Vector3 pos, float diameter, Color color)
        {
            return Prim(PrimitiveType.Cylinder, name, parent, pos + Vector3.up * 0.01f, new Vector3(diameter, 0.01f, diameter),
                Mat("Ring_" + ColorUtility.ToHtmlStringRGB(color), color, 1.5f), collider: false);
        }

        static Grabbable MakeGrabbable(GameObject go, float mass = 0.2f, bool kinematicUntilGrabbed = false)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            var g = go.AddComponent<Grabbable>();
            g.kinematicUntilGrabbed = kinematicUntilGrabbed;
            go.AddComponent<Resettable>();
            return g;
        }

        static BoxCollider TriggerBox(GameObject go, Vector3 center, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = center;
            box.size = size;
            return box;
        }

        // ── プレイヤー・システム ───────────────────────

        static PlayerRig BuildPlayer()
        {
            var root = new GameObject("PlayerRig");
            var rig = root.AddComponent<PlayerRig>();
            var offset = Group("CameraOffset", root.transform, Vector3.zero);
            rig.cameraOffset = offset.transform;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<TrackedPose>().source = TrackedPose.Source.Head;
            rig.head = camGo.transform;

            rig.leftHand = BuildHand(offset.transform, HandSide.Left);
            rig.rightHand = BuildHand(offset.transform, HandSide.Right);

            var pointer = rig.rightHand.gameObject.AddComponent<UIPointer>();
            pointer.hand = rig.rightHand;
            var line = rig.rightHand.gameObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.widthMultiplier = 0.004f;
            line.sharedMaterial = Mat("PointerLine", new Color(1f, 0.9f, 0.6f), unlit: true);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            pointer.line = line;

            root.AddComponent<DesktopSimulator>().rig = rig;
            return rig;
        }

        static Hand BuildHand(Transform parent, HandSide side)
        {
            var go = new GameObject(side + "Hand");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(side == HandSide.Left ? -0.2f : 0.2f, 1.1f, 0.3f);
            go.AddComponent<TrackedPose>().source = side == HandSide.Left ? TrackedPose.Source.LeftHand : TrackedPose.Source.RightHand;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.06f;
            var hand = go.AddComponent<Hand>();
            hand.side = side;
            go.AddComponent<HandGrabber>();

            // 仮の手：肌色のミトン（握りこぶし）。本番では手のモデルに差し替える
            // コントローラーを握った手に合わせる（TrackedPose参照）：原点＝握った拳の中心、+Z＝手の向き（人差し指側）、+Y＝親指側、
            // X＝手のひらの法線（左手は手のひらが+X、右手は-Xを向く）
            var skin = Mat("Skin", new Color(0.91f, 0.72f, 0.58f));
            float back = side == HandSide.Left ? -1 : 1;   // 手の甲がある側
            var palm = Prim(PrimitiveType.Sphere, "Palm", go.transform, new Vector3(0.012f * back, -0.005f, -0.01f), new Vector3(0.05f, 0.07f, 0.1f), skin, collider: false);
            var thumb = Prim(PrimitiveType.Capsule, "Thumb", go.transform, new Vector3(-0.008f * back, 0.035f, 0.02f), new Vector3(0.022f, 0.028f, 0.022f), skin, new Vector3(80, 0, 0), collider: false);
            hand.skinRenderers = new Renderer[] { palm.GetComponent<Renderer>(), thumb.GetComponent<Renderer>() };
            return hand;
        }

        static void BuildSystems(PlayerRig rig, Light sun)
        {
            var go = new GameObject("GameSystems");
            var gm = go.AddComponent<GameManager>();
            gm.rig = rig;
            gm.sun = sun;
            gm.story = go.AddComponent<StoryDirector>();
            gm.endings = go.AddComponent<EndingDirector>();
            gm.narration = go.AddComponent<NarrationPlayer>();
            var voice = go.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            gm.narration.voiceSource = voice;
            var sfx = go.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            gm.sfxSource = sfx;
            gm.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (gm.font == null) Debug.LogWarning("[JPHACKS2026] 日本語フォントが見つかりません: " + FontPath);
        }

        // ── キャラクター（仮モデル） ────────────────────

        /// <summary>
        /// 亀。原点＝足元の中心、頭が +Z、長さ約 1.1×size。SeaTurtle.fbx があれば使い、なければ仮モデルで組む。
        /// swimming：泳ぐアニメーション（乗って海中を進む亀など）。false なら漂うようなゆったりした動き。
        /// </summary>
        static GameObject BuildTurtle(string name, Transform parent, Vector3 pos, float yaw, float size, string idPrefix, bool swimming = false)
        {
            var root = Group(name, parent, pos, yaw);
            var body = Group("Body", root.transform, Vector3.zero);
            var t = body.transform;
            if (!BuildTurtleModel(t, size, swimming)) BuildTurtlePlaceholder(t, size);

            var ch = root.AddComponent<SimpleCharacter>();
            ch.body = t;
            ch.idle = SimpleCharacter.Idle.Bob;
            ch.idleAmplitude = 0.015f * size;
            root.AddComponent<Resettable>();

            if (idPrefix != null)
            {
                Anchor(root, idPrefix);
                var hit = Group("HitZone", root.transform, Vector3.zero);
                TriggerBox(hit, new Vector3(0, 0.25f, 0.1f) * size, new Vector3(0.9f, 0.6f, 1.2f) * size);
                hit.AddComponent<TouchTrigger>().minSpeed = 0.9f;
                Anchor(hit, idPrefix + ".hit");

                var back = Group("BackZone", root.transform, Vector3.zero);
                TriggerBox(back, new Vector3(0, 0.38f, 0) * size, new Vector3(0.7f, 0.35f, 0.8f) * size);
                var bt = back.AddComponent<TouchTrigger>();
                bt.armedVisual = Ring("BackMarker", back.transform, new Vector3(0, 0.36f * size, 0), 0.5f * size, new Color(1f, 0.85f, 0.3f));
                Anchor(back, idPrefix + ".back");
            }
            return root;
        }

        static void BuildTurtlePlaceholder(Transform t, float size)
        {
            var shellMat = Mat("TurtleShell", new Color(0.35f, 0.5f, 0.25f));
            var skinMat = Mat("TurtleSkin", new Color(0.6f, 0.75f, 0.45f));
            var eye = Mat("Black", new Color(0.05f, 0.05f, 0.05f));
            Prim(PrimitiveType.Sphere, "Shell", t, new Vector3(0, 0.2f, 0) * size, new Vector3(0.75f, 0.32f, 0.9f) * size, shellMat, collider: false);
            Prim(PrimitiveType.Sphere, "Head", t, new Vector3(0, 0.2f, 0.52f) * size, Vector3.one * 0.2f * size, skinMat, collider: false);
            Prim(PrimitiveType.Sphere, "EyeL", t, new Vector3(-0.06f, 0.25f, 0.6f) * size, Vector3.one * 0.04f * size, eye, collider: false);
            Prim(PrimitiveType.Sphere, "EyeR", t, new Vector3(0.06f, 0.25f, 0.6f) * size, Vector3.one * 0.04f * size, eye, collider: false);
            foreach (var (x, z) in new[] { (-0.35f, 0.3f), (0.35f, 0.3f), (-0.32f, -0.3f), (0.32f, -0.3f) })
                Prim(PrimitiveType.Sphere, "Flipper", t, new Vector3(x, 0.1f, z) * size, new Vector3(0.25f, 0.06f, 0.15f) * size, skinMat, collider: false);
        }

        /// <summary>SeaTurtle.fbx を、頭が +Z・長さ 1.1×size・底面の中心が原点になるように置く。モデルが無ければ false。</summary>
        static bool BuildTurtleModel(Transform body, float size, bool swimming)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(TurtleModelImporter.ModelPath);
            if (model == null) return false;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "SeaTurtle";
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // 頭のボーンが +Z 側に来るように回す（FBXの軸の向きに左右されないように）
            Transform head = null;
            foreach (var tr in inst.GetComponentsInChildren<Transform>()) if (tr.name == "Head") { head = tr; break; }
            var all = RendererBounds(inst.transform);
            if (head != null)
            {
                var d = head.position - all.center; d.y = 0;
                if (d.sqrMagnitude > 1e-6f) inst.transform.rotation = Quaternion.FromToRotation(d.normalized, Vector3.forward) * inst.transform.rotation;
            }

            all = RendererBounds(inst.transform);
            inst.transform.localScale *= 1.1f * size / Mathf.Max(0.001f, all.size.z);
            all = RendererBounds(inst.transform);
            inst.transform.position -= new Vector3(all.center.x, all.min.y, all.center.z);
            inst.transform.SetParent(body, false);

            var animator = inst.GetComponent<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = TurtleModelImporter.LoadController(swimming);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = false;
            return true;
        }

        static GameObject BuildHuman(string name, Transform parent, Vector3 pos, float yaw, Color clothes, float height, Color hair)
        {
            var root = Group(name, parent, pos, yaw);
            var body = Group("Body", root.transform, Vector3.zero);
            var t = body.transform;
            float h = height;
            Prim(PrimitiveType.Capsule, "Torso", t, new Vector3(0, h * 0.36f, 0), new Vector3(h * 0.28f, h * 0.36f, h * 0.24f), Mat("Cloth_" + ColorUtility.ToHtmlStringRGB(clothes), clothes), collider: false);
            Prim(PrimitiveType.Sphere, "Head", t, new Vector3(0, h * 0.84f, 0), Vector3.one * h * 0.2f, Mat("Skin", new Color(0.91f, 0.72f, 0.58f)), collider: false);
            Prim(PrimitiveType.Sphere, "Hair", t, new Vector3(0, h * 0.88f, -h * 0.02f), new Vector3(h * 0.21f, h * 0.17f, h * 0.21f), Mat("Hair_" + ColorUtility.ToHtmlStringRGB(hair), hair), collider: false);
            Prim(PrimitiveType.Sphere, "EyeL", t, new Vector3(-h * 0.035f, h * 0.85f, h * 0.095f), Vector3.one * h * 0.025f, Mat("Black", new Color(0.05f, 0.05f, 0.05f)), collider: false);
            Prim(PrimitiveType.Sphere, "EyeR", t, new Vector3(h * 0.035f, h * 0.85f, h * 0.095f), Vector3.one * h * 0.025f, Mat("Black", new Color(0.05f, 0.05f, 0.05f)), collider: false);
            var ch = root.AddComponent<SimpleCharacter>();
            ch.body = t;
            root.AddComponent<Resettable>();
            return root;
        }

        const string TamatebakoModelPath = TamatebakoMaterialImporter.ModelPath;

        /// <summary>
        /// 玉手箱。原点＝底面の中心、幅 0.32×scale。子の "Lid"（ふた）は単体でつかめるよう当たり判定を持つ。
        /// モデル（tamatebako.fbx：本体「tamatebako_hako」＋ふた「tamatebako_huta」）があれば使い、なければ仮モデルで組む。
        /// </summary>
        static GameObject BuildTamatebako(string name, Transform parent, Vector3 pos, float scale)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(TamatebakoModelPath);
            if (model != null)
            {
                var built = BuildTamatebakoFromModel(model, name, parent, pos, scale);
                if (built != null) return built;
            }

            var root = Group(name, parent, pos);
            var lacquer = Mat("BlackLacquer", new Color(0.08f, 0.06f, 0.06f));
            var red = Mat("RedCord", new Color(0.8f, 0.1f, 0.1f));
            var gold = Mat("Gold", new Color(0.95f, 0.75f, 0.25f), 0.3f);
            Prim(PrimitiveType.Cube, "Base", root.transform, new Vector3(0, 0.1f, 0) * scale, new Vector3(0.3f, 0.2f, 0.24f) * scale, lacquer);
            Prim(PrimitiveType.Cube, "GoldBand", root.transform, new Vector3(0, 0.1f, 0) * scale, new Vector3(0.305f, 0.03f, 0.245f) * scale, gold, collider: false);
            var lid = Prim(PrimitiveType.Cube, "Lid", root.transform, new Vector3(0, 0.232f, 0) * scale, new Vector3(0.32f, 0.06f, 0.26f) * scale, lacquer);
            Prim(PrimitiveType.Cube, "CordX", lid.transform, new Vector3(0, 0.55f, 0), new Vector3(1.02f, 0.2f, 0.12f), red, collider: false);
            Prim(PrimitiveType.Cube, "CordZ", lid.transform, new Vector3(0, 0.55f, 0), new Vector3(0.1f, 0.2f, 1.02f), red, collider: false);
            return root;
        }

        static GameObject BuildTamatebakoFromModel(GameObject model, string name, Transform parent, Vector3 pos, float scale)
        {
            // 原点・回転なしの場所で組み立ててから親に付ける（ワールド座標＝ローカル座標として計算できる）
            var root = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform baseMesh = null, lidMesh = null;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                if (r.name.Contains("huta") || r.name.Contains("フタ")) lidMesh = r.transform;
                else if (r.name.Contains("hako") || r.name.Contains("はこ")) baseMesh = r.transform;
            }
            if (baseMesh == null || lidMesh == null)
            {
                Debug.LogWarning($"[JPHACKS2026] 玉手箱モデルに本体（hako）・ふた（huta）のメッシュが見つからないため仮モデルを使います: {TamatebakoModelPath}");
                Object.DestroyImmediate(inst);
                Object.DestroyImmediate(root);
                return null;
            }

            // 幅をそろえ、底面の中心が原点に来るように置く
            var all = RendererBounds(inst.transform);
            float k = 0.32f * scale / Mathf.Max(all.size.x, all.size.z);
            inst.transform.localScale *= k;
            all = RendererBounds(inst.transform);
            inst.transform.position -= new Vector3(all.center.x, all.min.y, all.center.z);

            var visual = inst.transform;
            visual.name = "Visual";
            visual.SetParent(root.transform, true);

            var baseBounds = baseMesh.GetComponent<Renderer>().bounds;
            var baseCol = Group("Base", root.transform, baseBounds.center).AddComponent<BoxCollider>();
            baseCol.size = baseBounds.size;

            // ふた：つかむ・持ち上げる対象。メッシュを "Lid" の子にする
            var lidBounds = lidMesh.GetComponent<Renderer>().bounds;
            var lid = new GameObject("Lid");
            lid.transform.SetParent(root.transform, false);
            lid.transform.localPosition = lidBounds.center;
            lidMesh.SetParent(lid.transform, true);
            var lidCol = lid.AddComponent<BoxCollider>();
            lidCol.size = lidBounds.size;

            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            return root;
        }

        static Bounds RendererBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ── タイトル（絵本の部屋） ──────────────────────

        static void BuildTitle(Vector3 origin)
        {
            var area = MakeArea("title", origin, Vector3.zero, 0);
            area.walkHalfExtents = new Vector2(1.5f, 1.5f);
            area.skyColor = new Color(0.03f, 0.04f, 0.12f);
            area.ambientColor = new Color(0.3f, 0.28f, 0.4f);
            area.sunColor = new Color(0.65f, 0.75f, 1f);   // 窓から差しこむ月明かり
            area.sunIntensity = 0.45f;
            area.sunEuler = new Vector3(35, 180, 0);
            var t = area.transform;

            BuildTitleRoom(t);
            BuildTitleNightView(t);

            // 書見台
            var wood = Mat("Wood", new Color(0.45f, 0.3f, 0.18f));
            var darkWood = Mat("DarkWood", new Color(0.25f, 0.15f, 0.09f));
            var gold = Mat("Gold", new Color(0.95f, 0.75f, 0.25f), 0.3f);
            Prim(PrimitiveType.Cylinder, "PedestalBase", t, new Vector3(0, 0.03f, 0.55f), new Vector3(0.6f, 0.03f, 0.6f), darkWood);
            Prim(PrimitiveType.Cylinder, "PedestalColumn", t, new Vector3(0, 0.45f, 0.55f), new Vector3(0.18f, 0.42f, 0.18f), wood);
            Prim(PrimitiveType.Cylinder, "PedestalRing", t, new Vector3(0, 0.62f, 0.55f), new Vector3(0.22f, 0.015f, 0.22f), gold, collider: false);
            Prim(PrimitiveType.Cube, "PedestalTop", t, new Vector3(0, 0.88f, 0.55f), new Vector3(0.52f, 0.04f, 0.4f), darkWood);

            // 絵本
            var book = Group("Picturebook", t, new Vector3(0, 0.92f, 0.55f));
            Anchor(book, "title.book");
            var pb = book.AddComponent<Picturebook>();
            var red = Mat("BookCover", new Color(0.7f, 0.15f, 0.12f));
            Prim(PrimitiveType.Cube, "BackCover", book.transform, new Vector3(0, 0.0075f, 0), new Vector3(0.36f, 0.015f, 0.26f), red, collider: false);
            Prim(PrimitiveType.Cube, "Pages", book.transform, new Vector3(0, 0.03f, 0), new Vector3(0.34f, 0.03f, 0.24f), Mat("Paper", new Color(0.98f, 0.95f, 0.88f)), collider: false);
            var hinge = Group("CoverHinge", book.transform, new Vector3(-0.18f, 0.05f, 0));
            Prim(PrimitiveType.Cube, "Cover", hinge.transform, new Vector3(0.18f, 0, 0), new Vector3(0.36f, 0.015f, 0.26f), red, collider: false);
            Prim(PrimitiveType.Cube, "CoverDeco", hinge.transform, new Vector3(0.18f, 0.009f, 0), new Vector3(0.2f, 0.003f, 0.12f), gold, collider: false);
            // 表紙の四隅の金具
            foreach (var (cx, cz) in new[] { (0.345f, 0.115f), (0.345f, -0.115f), (0.03f, 0.115f), (0.03f, -0.115f) })
                Prim(PrimitiveType.Cube, "CornerGuard", hinge.transform, new Vector3(cx, 0.0085f, cz), new Vector3(0.03f, 0.003f, 0.03f), gold, new Vector3(0, 45, 0), collider: false);
            // 背表紙：丸みのある背に、金の背バンドと題名の札
            var spineLeather = Mat("BookSpine", new Color(0.5f, 0.1f, 0.08f));
            Prim(PrimitiveType.Cylinder, "Spine", book.transform, new Vector3(-0.18f, 0.029f, 0), new Vector3(0.03f, 0.13f, 0.06f), spineLeather, new Vector3(90, 0, 0), collider: false);
            foreach (float bz in new[] { 0.112f, 0.096f, -0.096f, -0.112f, 0.03f, -0.03f })
                Prim(PrimitiveType.Cylinder, "SpineBand", book.transform, new Vector3(-0.18f, 0.029f, bz), new Vector3(0.034f, 0.004f, 0.064f), gold, new Vector3(90, 0, 0), collider: false);
            Prim(PrimitiveType.Cube, "SpineTitle", book.transform, new Vector3(-0.1955f, 0.029f, 0.063f), new Vector3(0.002f, 0.03f, 0.045f), gold, collider: false);
            Prim(PrimitiveType.Cube, "SpineCrest", book.transform, new Vector3(-0.1955f, 0.029f, -0.063f), new Vector3(0.002f, 0.022f, 0.022f), gold, new Vector3(45, 0, 0), collider: false);
            pb.coverHinge = hinge.transform;
            pb.openAngle = 175f;
            // 吸い込まれる光：ページが灯る → 光の筋と粒 → 半透明の光がプレイヤーを包む（Picturebook.SetGlow）
            var glowMat = GlowMat("BookGlow");
            var pageGlow = Prim(PrimitiveType.Cube, "PageGlow", book.transform, new Vector3(-0.18f, 0.062f, 0), new Vector3(0.7f, 0.002f, 0.25f), glowMat, collider: false);
            pageGlow.GetComponent<Renderer>().enabled = false;
            pb.pageGlow = pageGlow.GetComponent<Renderer>();
            var rays = Group("Rays", book.transform, new Vector3(-0.09f, 0.06f, 0));   // 見開きの中央寄り
            for (int i = 0; i < 7; i++)
            {
                var pivot = Group("RayPivot", rays.transform, Vector3.zero, i * 360f / 7);
                pivot.transform.localRotation *= Quaternion.Euler(12f + (i % 3) * 6f, 0, 0);
                Prim(PrimitiveType.Cube, "Ray", pivot.transform, new Vector3(0, 0.45f, 0), new Vector3(0.025f + (i % 2) * 0.015f, 0.9f, 0.002f), glowMat, collider: false);
            }
            rays.SetActive(false);
            pb.rays = rays.transform;
            pb.sparkles = BuildBookSparkles(book.transform);
            var glow = Prim(PrimitiveType.Sphere, "Glow", book.transform, new Vector3(-0.09f, 0.15f, 0), Vector3.one * 0.3f, glowMat, collider: false);
            glow.SetActive(false);
            pb.glowRenderer = glow.GetComponent<Renderer>();
            var light = new GameObject("GlowLight").AddComponent<Light>();
            light.transform.SetParent(book.transform, false);
            light.transform.localPosition = new Vector3(0, 0.3f, 0);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.55f);
            light.range = 4;
            light.enabled = false;
            pb.glowLight = light;
            var zone = Group("OpenZone", book.transform, Vector3.zero);
            TriggerBox(zone, new Vector3(0, 0.05f, 0), new Vector3(0.45f, 0.25f, 0.35f));
            pb.openTrigger = zone.AddComponent<TouchTrigger>();
            pb.openTrigger.requireGrip = true;
            pb.openTrigger.armedVisual = Ring("BookMarker", t, new Vector3(0, 0.905f, 0.55f), 0.5f, new Color(1f, 0.85f, 0.3f));

            // 絵本からこぼれた物語のかけら：ページが頭上をゆっくり舞う
            var page = Mat("Paper", new Color(0.98f, 0.95f, 0.88f));
            var rnd = new System.Random(11);
            for (int i = 0; i < 9; i++)
            {
                var p = Prim(PrimitiveType.Cube, "FloatingPage", t, Vector3.zero, new Vector3(0.16f, 0.004f, 0.22f), page, collider: false);
                var sw = p.AddComponent<Swimmer>();
                sw.center = new Vector3(0, 2.35f + (float)rnd.NextDouble() * 0.3f, 0.6f);
                sw.radius = 0.8f + (float)rnd.NextDouble() * 0.6f;
                sw.speed = 0.12f + (float)rnd.NextDouble() * 0.12f;
                sw.verticalAmplitude = 0.12f;
            }

            BuildTitleMotes(t);

            // 物語の小物：亀・玉手箱・配信用マイク
            var turtle = BuildTurtle("MiniTurtle", t, new Vector3(0.95f, 0, 1.05f), 220, 0.35f, null);
            turtle.GetComponent<SimpleCharacter>().idleAmplitude = 0.01f;
            BuildSideTable(t, new Vector3(-0.95f, 0, 0.9f), darkWood);
            BuildTamatebako("MiniTamatebako", t, new Vector3(-1.0f, 0.62f, 0.95f), 0.55f);
            BuildCandle(t, new Vector3(-0.82f, 0.62f, 0.78f));
            BuildMicStand(t, new Vector3(0.8f, 0, 0.2f));

            Point("Hud", t, new Vector3(0, 1.85f, 2.4f), 0, "title.hud");
            Point("MenuAnchor", t, new Vector3(0, 1.38f, 1.15f), 0, "title.menu");
        }

        /// <summary>夜の書斎：板張りの床・じゅうたん・壁・天井の梁・本棚・行灯・ON AIRランプ。</summary>
        static void BuildTitleRoom(Transform t)
        {
            const float halfW = 2.6f, front = -2.2f, back = 3.2f, height = 3f;
            const float midZ = (front + back) / 2, depth = back - front;
            var rnd = new System.Random(3);

            // 床（色違いの板を並べる）
            var plankA = Mat("PlankA", new Color(0.36f, 0.24f, 0.16f));
            var plankB = Mat("PlankB", new Color(0.31f, 0.2f, 0.13f));
            Prim(PrimitiveType.Cube, "Floor", t, new Vector3(0, -0.05f, midZ), new Vector3(halfW * 2, 0.1f, depth), plankA);
            for (int i = 0; i < 17; i++)
                Prim(PrimitiveType.Cube, "Plank", t, new Vector3(-halfW + 0.15f + i * 0.31f, 0.001f, midZ), new Vector3(0.29f, 0.002f, depth), i % 2 == 0 ? plankB : plankA, collider: false);

            // じゅうたん
            var gold = Mat("Gold", new Color(0.95f, 0.75f, 0.25f), 0.3f);
            Prim(PrimitiveType.Cylinder, "RugBorder", t, new Vector3(0, 0.006f, 0.4f), new Vector3(2.5f, 0.004f, 2.5f), gold, collider: false);
            Prim(PrimitiveType.Cylinder, "Rug", t, new Vector3(0, 0.01f, 0.4f), new Vector3(2.35f, 0.004f, 2.35f), Mat("RugRed", new Color(0.55f, 0.1f, 0.12f)), collider: false);
            Prim(PrimitiveType.Cylinder, "RugInner", t, new Vector3(0, 0.014f, 0.4f), new Vector3(1.5f, 0.004f, 1.5f), Mat("RugNavy", new Color(0.12f, 0.14f, 0.32f)), collider: false);

            // 壁：奥の壁には窓をあける（x -1.3〜1.3, y 0.9〜2.6）
            var wall = Mat("TitleWall", new Color(0.18f, 0.2f, 0.38f));
            var darkWood = Mat("DarkWood", new Color(0.25f, 0.15f, 0.09f));
            Prim(PrimitiveType.Cube, "WallLeft", t, new Vector3(-halfW, height / 2, midZ), new Vector3(0.1f, height, depth), wall);
            Prim(PrimitiveType.Cube, "WallRight", t, new Vector3(halfW, height / 2, midZ), new Vector3(0.1f, height, depth), wall);
            Prim(PrimitiveType.Cube, "WallFront", t, new Vector3(0, height / 2, front), new Vector3(halfW * 2, height, 0.1f), wall);
            Prim(PrimitiveType.Cube, "WallBackL", t, new Vector3(-1.95f, height / 2, back), new Vector3(1.3f, height, 0.1f), wall);
            Prim(PrimitiveType.Cube, "WallBackR", t, new Vector3(1.95f, height / 2, back), new Vector3(1.3f, height, 0.1f), wall);
            Prim(PrimitiveType.Cube, "WallBackBottom", t, new Vector3(0, 0.45f, back), new Vector3(2.6f, 0.9f, 0.1f), wall);
            Prim(PrimitiveType.Cube, "WallBackTop", t, new Vector3(0, 2.8f, back), new Vector3(2.6f, 0.4f, 0.1f), wall);
            // 腰板
            Prim(PrimitiveType.Cube, "WainscotL", t, new Vector3(-halfW + 0.06f, 0.45f, midZ), new Vector3(0.02f, 0.9f, depth), darkWood, collider: false);
            Prim(PrimitiveType.Cube, "WainscotR", t, new Vector3(halfW - 0.06f, 0.45f, midZ), new Vector3(0.02f, 0.9f, depth), darkWood, collider: false);
            Prim(PrimitiveType.Cube, "WainscotF", t, new Vector3(0, 0.45f, front + 0.06f), new Vector3(halfW * 2, 0.9f, 0.02f), darkWood, collider: false);
            Prim(PrimitiveType.Cube, "WainscotB", t, new Vector3(0, 0.45f, back - 0.06f), new Vector3(halfW * 2, 0.9f, 0.02f), darkWood, collider: false);

            // 天井と梁
            Prim(PrimitiveType.Cube, "Ceiling", t, new Vector3(0, height, midZ), new Vector3(halfW * 2, 0.1f, depth), Mat("Ceiling", new Color(0.2f, 0.13f, 0.09f)), collider: false);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "Beam", t, new Vector3(0, height - 0.1f, front + 0.6f + i * 1.4f), new Vector3(halfW * 2, 0.14f, 0.16f), darkWood, collider: false);

            // 窓枠と格子
            var frame = Mat("WindowFrame", new Color(0.2f, 0.12f, 0.07f));
            Prim(PrimitiveType.Cube, "WindowSill", t, new Vector3(0, 0.88f, back - 0.08f), new Vector3(2.8f, 0.06f, 0.26f), frame);
            Prim(PrimitiveType.Cube, "WindowTop", t, new Vector3(0, 2.62f, back - 0.04f), new Vector3(2.8f, 0.07f, 0.16f), frame, collider: false);
            for (int i = 0; i <= 4; i++)
                Prim(PrimitiveType.Cube, "WindowBarV", t, new Vector3(-1.3f + i * 0.65f, 1.75f, back - 0.02f), new Vector3(i % 4 == 0 ? 0.08f : 0.03f, 1.7f, 0.04f), frame, collider: false);
            Prim(PrimitiveType.Cube, "WindowBarH", t, new Vector3(0, 1.75f, back - 0.02f), new Vector3(2.6f, 0.03f, 0.04f), frame, collider: false);

            // ON AIR ランプ（窓の上）
            var onAir = Group("OnAirSign", t, new Vector3(0, 2.83f, back - 0.07f));
            Prim(PrimitiveType.Cube, "Box", onAir.transform, Vector3.zero, new Vector3(0.62f, 0.2f, 0.06f), Mat("OnAirRed", new Color(0.9f, 0.12f, 0.1f), 2.2f), collider: false);
            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(onAir.transform, false);
            label.transform.localPosition = new Vector3(0, 0, -0.035f);
            label.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            label.text = "ON AIR";
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.2f;
            label.fontSizeMax = 2f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.95f, 0.9f);
            var onAirLight = new GameObject("OnAirLight").AddComponent<Light>();
            onAirLight.transform.SetParent(onAir.transform, false);
            onAirLight.transform.localPosition = new Vector3(0, -0.1f, -0.3f);
            onAirLight.type = LightType.Point;
            onAirLight.color = new Color(1f, 0.25f, 0.2f);
            onAirLight.range = 1.6f;
            onAirLight.intensity = 0.8f;

            // 本棚（左右の壁ぎわに2つずつ）
            var spines = new[]
            {
                Mat("Spine0", new Color(0.6f, 0.15f, 0.12f)), Mat("Spine1", new Color(0.15f, 0.3f, 0.55f)), Mat("Spine2", new Color(0.2f, 0.45f, 0.25f)),
                Mat("Spine3", new Color(0.85f, 0.65f, 0.3f)), Mat("Spine4", new Color(0.45f, 0.25f, 0.5f)), Mat("Spine5", new Color(0.9f, 0.85f, 0.75f)),
                Mat("Spine6", new Color(0.3f, 0.18f, 0.1f)), Mat("Spine7", new Color(0.1f, 0.35f, 0.38f)),
            };
            var spineDeco = new SpineDeco
            {
                gold = Mat("SpineGold", new Color(0.85f, 0.66f, 0.28f), 0.15f),
                label = Mat("SpineLabel", new Color(0.95f, 0.9f, 0.78f)),
                ink = Mat("SpineInk", new Color(0.18f, 0.12f, 0.08f)),
                pageEdge = Mat("Paper", new Color(0.98f, 0.95f, 0.88f)),
            };
            foreach (float side in new[] { -1f, 1f })
            foreach (float z in new[] { -0.9f, 1.7f })
            {
                var shelf = Group("Bookshelf", t, new Vector3(side * (halfW - 0.22f), 0, z), side < 0 ? 90 : -90);
                var st = shelf.transform;
                Prim(PrimitiveType.Cube, "Back", st, new Vector3(0, 1.1f, -0.15f), new Vector3(1.2f, 2.2f, 0.03f), darkWood);
                Prim(PrimitiveType.Cube, "SideL", st, new Vector3(-0.59f, 1.1f, 0), new Vector3(0.03f, 2.2f, 0.32f), darkWood, collider: false);
                Prim(PrimitiveType.Cube, "SideR", st, new Vector3(0.59f, 1.1f, 0), new Vector3(0.03f, 2.2f, 0.32f), darkWood, collider: false);
                for (int k = 0; k < 5; k++)
                {
                    float y = 0.05f + k * 0.52f;
                    Prim(PrimitiveType.Cube, "Board", st, new Vector3(0, y, 0), new Vector3(1.18f, 0.03f, 0.32f), darkWood, collider: false);
                    if (k == 4) break;
                    float x = -0.54f;
                    float floor = y + 0.015f;
                    while (x < 0.5f)
                    {
                        double roll = rnd.NextDouble();
                        if (roll < 0.06) { x += 0.08f; continue; }   // ところどころ隙間
                        if (roll < 0.11 && x < 0.32f)
                        {
                            // 平積み：横に寝かせた本を数冊重ねる
                            float sw = 0.16f + (float)rnd.NextDouble() * 0.06f, sy = floor;
                            int n = 2 + rnd.Next(3);
                            for (int b = 0; b < n; b++)
                            {
                                float th = 0.025f + (float)rnd.NextDouble() * 0.02f;
                                var flat = Group("BookFlat", st, new Vector3(x + sw / 2 + ((float)rnd.NextDouble() - 0.5f) * 0.015f, sy + th / 2, 0.01f), ((float)rnd.NextDouble() - 0.5f) * 8f);
                                flat.transform.localRotation *= Quaternion.Euler(0, 0, 90);
                                BuildShelfBook(flat.transform, th, sw * 0.95f, spines[rnd.Next(spines.Length)], spineDeco, rnd);
                                sy += th;
                            }
                            x += sw + 0.01f;
                            continue;
                        }
                        float w = 0.03f + (float)rnd.NextDouble() * 0.045f;
                        float h = 0.26f + (float)rnd.NextDouble() * 0.18f;
                        var book = Group("Book", st, new Vector3(x + w / 2, floor + h / 2, 0.01f));
                        BuildShelfBook(book.transform, w, h, spines[rnd.Next(spines.Length)], spineDeco, rnd);
                        x += w + 0.003f;
                    }
                }
            }

            // 行灯（部屋の四隅）
            foreach (var pos in new[] { new Vector3(-2.1f, 0, 2.7f), new Vector3(2.1f, 0, 2.7f), new Vector3(-2.1f, 0, -1.7f), new Vector3(2.1f, 0, -1.7f) })
                BuildAndon(t, pos, darkWood, lit: pos.z > 0);
        }

        struct SpineDeco { public Material gold, label, ink, pageEdge; }

        /// <summary>
        /// 本棚の本1冊（中心が原点、背表紙が +Z）。背表紙に金の帯・題箋（題名を書いた紙）・飾り罫を
        /// 本ごとにランダムに組み合わせ、古い和綴じ本や洋書の背表紙が並んでいるように見せる。
        /// </summary>
        static void BuildShelfBook(Transform t, float w, float h, Material cover, SpineDeco deco, System.Random rnd)
        {
            const float Depth = 0.22f;
            float front = Depth / 2;
            Prim(PrimitiveType.Cube, "Cover", t, Vector3.zero, new Vector3(w, h, Depth), cover, collider: false);
            // 天（上の小口）にページの紙色を少し見せる
            Prim(PrimitiveType.Cube, "Pages", t, new Vector3(0, h / 2, -0.004f), new Vector3(w * 0.82f, 0.004f, Depth - 0.012f), deco.pageEdge, collider: false);

            int style = rnd.Next(4);
            float bandH = Mathf.Min(0.012f, h * 0.04f);
            if (style != 3)
            {
                // 上下の金帯（洋書の背バンド風）。太い本は2本ずつ
                int bands = w > 0.05f ? 2 : 1;
                for (int i = 0; i < bands; i++)
                {
                    float off = h * 0.42f - i * bandH * 2.2f;
                    Prim(PrimitiveType.Cube, "Band", t, new Vector3(0, off, front + 0.002f), new Vector3(w * 1.01f, bandH, 0.004f), deco.gold, collider: false);
                    Prim(PrimitiveType.Cube, "Band", t, new Vector3(0, -off, front + 0.002f), new Vector3(w * 1.01f, bandH, 0.004f), deco.gold, collider: false);
                }
            }
            if (style == 0 || style == 3)
            {
                // 題箋：縦長の紙ラベルに、題名のかわりの墨の線
                float lh = h * (0.35f + (float)rnd.NextDouble() * 0.15f);
                float ly = h * 0.12f;
                Prim(PrimitiveType.Cube, "Label", t, new Vector3(0, ly, front + 0.002f), new Vector3(w * 0.62f, lh, 0.003f), deco.label, collider: false);
                int strokes = 2 + rnd.Next(3);
                for (int i = 0; i < strokes; i++)
                    Prim(PrimitiveType.Cube, "Title", t, new Vector3(0, ly + lh * (0.32f - i * 0.18f), front + 0.0038f), new Vector3(w * 0.3f, lh * 0.1f, 0.001f), deco.ink, collider: false);
            }
            else if (style == 1)
            {
                // 金の題名と、その下の小さな紋
                Prim(PrimitiveType.Cube, "Title", t, new Vector3(0, h * 0.12f, front + 0.002f), new Vector3(w * 0.45f, h * 0.28f, 0.003f), deco.gold, collider: false);
                Prim(PrimitiveType.Cube, "Crest", t, new Vector3(0, -h * 0.24f, front + 0.002f), new Vector3(w * 0.4f, w * 0.4f, 0.003f), deco.gold, new Vector3(0, 0, 45), collider: false);
            }
            else if (style == 2)
            {
                // 背の中ほどに色違いの帯（シリーズものの巻数のような印）
                Prim(PrimitiveType.Cube, "Mark", t, new Vector3(0, -h * 0.2f, front + 0.002f), new Vector3(w * 1.01f, h * 0.08f, 0.004f), deco.label, collider: false);
                Prim(PrimitiveType.Cube, "Title", t, new Vector3(0, h * 0.12f, front + 0.002f), new Vector3(w * 0.35f, h * 0.3f, 0.003f), deco.gold, collider: false);
            }
        }

        static void BuildAndon(Transform t, Vector3 pos, Material wood, bool lit)
        {
            var r = Group("Andon", t, pos).transform;
            Prim(PrimitiveType.Cube, "Paper", r, new Vector3(0, 0.55f, 0), new Vector3(0.3f, 0.5f, 0.3f), Mat("AndonPaper", new Color(1f, 0.82f, 0.55f), 1.6f), collider: false);
            foreach (var (x, z) in new[] { (-0.16f, -0.16f), (0.16f, -0.16f), (-0.16f, 0.16f), (0.16f, 0.16f) })
                Prim(PrimitiveType.Cube, "Leg", r, new Vector3(x, 0.42f, z), new Vector3(0.03f, 0.84f, 0.03f), wood, collider: false);
            Prim(PrimitiveType.Cube, "Top", r, new Vector3(0, 0.82f, 0), new Vector3(0.36f, 0.03f, 0.36f), wood, collider: false);
            Prim(PrimitiveType.Cube, "Bottom", r, new Vector3(0, 0.29f, 0), new Vector3(0.36f, 0.03f, 0.36f), wood, collider: false);
            if (!lit) return;   // 実際に照らすのは奥の2つだけ（Questの負荷対策）
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(r, false);
            light.transform.localPosition = new Vector3(0, 0.6f, 0);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.4f);
            light.range = 3.5f;
            var flicker = light.gameObject.AddComponent<LightFlicker>();
            flicker.baseIntensity = 1.4f;
            flicker.amount = 0.12f;
            flicker.speed = 1.5f;
        }

        static void BuildSideTable(Transform t, Vector3 pos, Material wood)
        {
            var r = Group("SideTable", t, pos).transform;
            Prim(PrimitiveType.Cylinder, "Top", r, new Vector3(0, 0.6f, 0), new Vector3(0.5f, 0.02f, 0.5f), wood);
            Prim(PrimitiveType.Cylinder, "Leg", r, new Vector3(0, 0.3f, 0), new Vector3(0.06f, 0.3f, 0.06f), wood, collider: false);
            Prim(PrimitiveType.Cylinder, "Foot", r, new Vector3(0, 0.015f, 0), new Vector3(0.3f, 0.015f, 0.3f), wood, collider: false);
        }

        static void BuildCandle(Transform t, Vector3 pos)
        {
            var r = Group("Candle", t, pos).transform;
            Prim(PrimitiveType.Cylinder, "Dish", r, new Vector3(0, 0.005f, 0), new Vector3(0.1f, 0.005f, 0.1f), Mat("Gold", new Color(0.95f, 0.75f, 0.25f), 0.3f), collider: false);
            Prim(PrimitiveType.Cylinder, "Wax", r, new Vector3(0, 0.06f, 0), new Vector3(0.035f, 0.055f, 0.035f), Mat("Wax", new Color(0.97f, 0.93f, 0.85f)), collider: false);
            Prim(PrimitiveType.Sphere, "Flame", r, new Vector3(0, 0.135f, 0), new Vector3(0.018f, 0.035f, 0.018f), Mat("Flame", new Color(1f, 0.75f, 0.3f), 3f, unlit: true), collider: false);
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(r, false);
            light.transform.localPosition = new Vector3(0, 0.18f, 0);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.35f);
            light.range = 1.8f;
            var flicker = light.gameObject.AddComponent<LightFlicker>();
            flicker.baseIntensity = 0.9f;
            flicker.amount = 0.3f;
            flicker.speed = 5f;
        }

        /// <summary>「配信」らしさ：レトロなスタンドマイク。</summary>
        static void BuildMicStand(Transform t, Vector3 pos)
        {
            var r = Group("MicStand", t, pos, -20).transform;
            var metal = Mat("Metal", new Color(0.55f, 0.55f, 0.6f));
            var black = Mat("Black", new Color(0.05f, 0.05f, 0.05f));
            Prim(PrimitiveType.Cylinder, "Base", r, new Vector3(0, 0.015f, 0), new Vector3(0.22f, 0.015f, 0.22f), black);
            Prim(PrimitiveType.Cylinder, "Pole", r, new Vector3(0, 0.6f, 0), new Vector3(0.02f, 0.6f, 0.02f), metal, collider: false);
            Prim(PrimitiveType.Capsule, "Head", r, new Vector3(0, 1.28f, 0), new Vector3(0.07f, 0.07f, 0.07f), metal, collider: false);
            Prim(PrimitiveType.Cylinder, "Grill", r, new Vector3(0, 1.28f, 0), new Vector3(0.075f, 0.02f, 0.075f), black, collider: false);
        }

        /// <summary>窓の外：月夜の海と星空。</summary>
        static void BuildTitleNightView(Transform t)
        {
            Prim(PrimitiveType.Cube, "SkyBackdrop", t, new Vector3(0, 8, 30), new Vector3(60, 30, 0.1f), Mat("NightSky", new Color(0.04f, 0.06f, 0.18f), unlit: true), collider: false);
            Prim(PrimitiveType.Cube, "NightSea", t, new Vector3(0, -0.4f, 17), new Vector3(60, 0.05f, 26), Mat("NightSea", new Color(0.03f, 0.08f, 0.2f), unlit: true), collider: false);
            Prim(PrimitiveType.Sphere, "Moon", t, new Vector3(5.5f, 9f, 28f), Vector3.one * 3.2f, Mat("Moon", new Color(1f, 0.96f, 0.82f), 2.5f, unlit: true), collider: false);

            var rnd = new System.Random(7);
            var star = Mat("Star", new Color(1f, 0.95f, 0.8f), 2f, unlit: true);
            for (int i = 0; i < 90; i++)
            {
                var pos = new Vector3(((float)rnd.NextDouble() - 0.5f) * 50f, 2f + (float)rnd.NextDouble() * 20f, 29.8f);
                Prim(PrimitiveType.Sphere, "Star", t, pos, Vector3.one * (0.06f + (float)rnd.NextDouble() * 0.14f), star, collider: false);
            }
        }

        /// <summary>部屋に漂う光の粒。</summary>
        static void BuildTitleMotes(Transform t)
        {
            var go = Group("Motes", t, new Vector3(0, 1.5f, 0.5f));
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 8f;
            main.startSpeed = 0.04f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.maxParticles = 120;
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4.4f, 2.4f, 4.6f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.3f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.2f, 1), new Keyframe(0.8f, 1), new Keyframe(1, 0)));
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Mesh;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            psr.mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            psr.sharedMaterial = Mat("Mote", new Color(1f, 0.88f, 0.55f), 2.5f, unlit: true);
        }

        // ── 浜辺 ─────────────────────────────────────

        static void BuildBeach(Vector3 origin)
        {
            var area = MakeArea("beach", origin, new Vector3(0, 0, -2.4f), 0);
            area.walkHalfExtents = new Vector2(9, 7.5f);
            area.walkCenter = new Vector2(0, -0.5f);
            area.skyColor = new Color(0.55f, 0.78f, 0.95f);
            area.ambientColor = new Color(0.65f, 0.62f, 0.6f);
            area.sunColor = new Color(1f, 0.95f, 0.85f);
            area.sunIntensity = 1.2f;
            area.fog = true;
            area.fogColor = new Color(0.7f, 0.85f, 0.95f);
            area.fogDensity = 0.012f;
            var t = area.transform;

            Prim(PrimitiveType.Plane, "Sand", t, Vector3.zero, new Vector3(5, 1, 5), Mat("Sand", new Color(0.93f, 0.85f, 0.65f)));
            Prim(PrimitiveType.Cube, "Sea", t, new Vector3(0, 0.0f, 25), new Vector3(60, 0.06f, 32), Mat("Sea", new Color(0.2f, 0.55f, 0.75f)), collider: false);
            Prim(PrimitiveType.Cube, "Shore", t, new Vector3(0, 0.0f, 8.6f), new Vector3(60, 0.05f, 1.2f), Mat("Foam", new Color(0.95f, 0.97f, 1f)), collider: false);
            var rock = Mat("Rock", new Color(0.5f, 0.48f, 0.46f));
            Prim(PrimitiveType.Sphere, "Rock", t, new Vector3(-5, 0.3f, 3), new Vector3(1.6f, 0.9f, 1.3f), rock);
            Prim(PrimitiveType.Sphere, "Rock", t, new Vector3(6, 0.4f, 5), new Vector3(2.2f, 1.2f, 1.8f), rock);
            Prim(PrimitiveType.Sphere, "Rock", t, new Vector3(-7.5f, 0.5f, -4), new Vector3(1.8f, 1.4f, 2f), rock);
            var wood = Mat("Driftwood", new Color(0.55f, 0.42f, 0.3f));
            Prim(PrimitiveType.Cylinder, "Driftwood", t, new Vector3(3.5f, 0.08f, -2.5f), new Vector3(0.15f, 1.0f, 0.15f), wood, new Vector3(0, 30, 90));
            Prim(PrimitiveType.Cylinder, "Driftwood", t, new Vector3(-3f, 0.07f, 1.5f), new Vector3(0.12f, 0.7f, 0.12f), wood, new Vector3(0, -50, 90));

            // 触れる小物（貝殻）
            var shellMats = new[] { Mat("ShellPink", new Color(1f, 0.75f, 0.75f)), Mat("ShellWhite", new Color(0.97f, 0.95f, 0.9f)) };
            var shellPos = new[] { new Vector3(0.6f, 0.03f, -1.6f), new Vector3(-0.8f, 0.03f, -2.2f), new Vector3(2.8f, 0.03f, -0.8f), new Vector3(-2f, 0.03f, 0.5f), new Vector3(1.4f, 0.03f, -3.2f) };
            for (int i = 0; i < shellPos.Length; i++)
            {
                var sh = Prim(PrimitiveType.Sphere, "Shell" + i, t, shellPos[i], new Vector3(0.08f, 0.04f, 0.07f), shellMats[i % 2]);
                MakeGrabbable(sh, 0.05f);
            }

            // 亀と子どもたち
            var turtle = BuildTurtle("Turtle", t, new Vector3(0, 0, 0.4f), 180, 1f, "beach.turtle");
            var kidColors = new[] { new Color(0.25f, 0.4f, 0.75f), new Color(0.9f, 0.55f, 0.2f), new Color(0.35f, 0.65f, 0.35f) };
            var kidPos = new[] { new Vector3(-0.95f, 0, 0.5f), new Vector3(0.95f, 0, 0.65f), new Vector3(0.15f, 0, 1.4f) };
            for (int i = 0; i < 3; i++)
            {
                var kid = BuildHuman("Child" + i, t, kidPos[i], 0, kidColors[i], 1.15f, new Color(0.08f, 0.06f, 0.05f));
                var ch = kid.GetComponent<SimpleCharacter>();
                ch.idle = SimpleCharacter.Idle.Hop;
                ch.idleAmplitude = 0.08f;
                ch.idleSpeed = 5f + i;
                ch.lookTarget = turtle.transform;
                TriggerBox(kid, new Vector3(0, 0.65f, 0), new Vector3(0.6f, 1.3f, 0.6f));
                kid.AddComponent<TouchTrigger>().minSpeed = 0.8f;
                Anchor(kid, "beach.child." + i);
            }

            var leave = Group("LeaveTrigger", t, new Vector3(0, 0, 0.4f));
            leave.AddComponent<LeaveTrigger>().radius = 6f;
            Anchor(leave, "beach.leave");
            var shake = Group("HeadShake", t, Vector3.zero);
            shake.AddComponent<HeadShakeTrigger>();
            Anchor(shake, "beach.headshake");
            Point("InviteSpawn", t, new Vector3(0, 0, -0.9f), 0, "beach.invite.spawn");
            Point("SeaPoint", t, new Vector3(0, 0, 10f), 0, "beach.sea.point");
        }

        // ── 海中（亀に乗って移動） ──────────────────────

        static void BuildSea(Vector3 origin)
        {
            var area = MakeArea("sea", origin, Vector3.zero, 0);
            area.walkHalfExtents = new Vector2(30, 60);
            area.walkCenter = new Vector2(0, 25);
            area.skyColor = new Color(0.05f, 0.35f, 0.45f);
            area.ambientColor = new Color(0.35f, 0.6f, 0.65f);
            area.sunColor = new Color(0.7f, 0.95f, 1f);
            area.sunIntensity = 0.9f;
            area.sunEuler = new Vector3(75, 0, 0);
            area.fog = true;
            area.fogColor = new Color(0.08f, 0.4f, 0.5f);
            area.fogDensity = 0.035f;
            var t = area.transform;

            Prim(PrimitiveType.Plane, "SeaFloor", t, new Vector3(0, -3f, 25), new Vector3(6, 1, 9), Mat("SeaFloor", new Color(0.75f, 0.7f, 0.5f)), collider: false);
            var rnd = new System.Random(3);
            var coralMats = new[] { Mat("CoralPink", new Color(1f, 0.45f, 0.55f)), Mat("CoralOrange", new Color(1f, 0.6f, 0.25f)), Mat("CoralPurple", new Color(0.6f, 0.35f, 0.8f)) };
            var weed = Mat("Seaweed", new Color(0.2f, 0.55f, 0.3f));
            for (int i = 0; i < 40; i++)
            {
                float side = rnd.Next(2) == 0 ? -1 : 1;
                var p = new Vector3(side * (2.5f + (float)rnd.NextDouble() * 8), -3f, (float)rnd.NextDouble() * 55 - 5);
                if (i % 2 == 0) Prim(PrimitiveType.Sphere, "Coral", t, p, new Vector3(1, 0.8f, 1) * (0.6f + (float)rnd.NextDouble()), coralMats[i % 3], collider: false);
                else Prim(PrimitiveType.Cube, "Seaweed", t, p + Vector3.up * 1f, new Vector3(0.12f, 2f + (float)rnd.NextDouble() * 1.5f, 0.12f), weed, collider: false);
            }
            var fishMats = new[] { Mat("FishYellow", new Color(1f, 0.85f, 0.2f)), Mat("FishBlue", new Color(0.3f, 0.6f, 1f)), Mat("FishRed", new Color(1f, 0.35f, 0.3f)) };
            for (int i = 0; i < 24; i++)
            {
                var fish = Prim(PrimitiveType.Sphere, "Fish", t, Vector3.zero, new Vector3(0.12f, 0.18f, 0.35f), fishMats[i % 3], collider: false);
                var sw = fish.AddComponent<Swimmer>();
                sw.center = new Vector3(((float)rnd.NextDouble() - 0.5f) * 6f, (float)rnd.NextDouble() * 2f - 0.5f, 3 + i * 1.8f);
                sw.radius = 1f + (float)rnd.NextDouble() * 2f;
                sw.speed = (rnd.Next(2) == 0 ? 1 : -1) * (0.4f + (float)rnd.NextDouble() * 0.6f);
                fish.AddComponent<Resettable>();
            }

            // 亀（大きめ）に乗る。sea.rideごと前進する
            var ride = Group("Ride", t, Vector3.zero);
            Anchor(ride, "sea.ride");
            ride.AddComponent<Resettable>();
            BuildTurtle("RideTurtle", ride.transform, new Vector3(0, -0.75f, 0.2f), 0, 2.2f, null, swimming: true);
            Point("Seat", ride.transform, Vector3.zero, 0, "sea.seat");
            Point("End", t, new Vector3(0, 0, 40), 0, "sea.end");

            // 進む先に竜宮城：霧の向こうから少しずつ姿を現す（終点の少し奥。入口をこちらに向ける）
            BuildRyuguExterior(t, new Vector3(0, -3f, 62f), 30f);
        }

        const string RyuguExteriorModelPath = "Assets/_Project/Art/Models/Environment/Ryugu_Iriguchi.fbx";

        /// <summary>竜宮城の外観（Ryugu_Iriguchi.fbx）を、底面の中心が pos、幅が width になるように置く。</summary>
        static void BuildRyuguExterior(Transform parent, Vector3 pos, float width)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RyuguExteriorModelPath);
            if (model == null)
            {
                Debug.LogWarning("[JPHACKS2026] 竜宮城の外観モデルが見つかりません: " + RyuguExteriorModelPath);
                return;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "RyuguExterior";
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // 入口（正面の龍柱 hashira*）が手前（-Z）を向くように回す
            var all = RendererBounds(inst.transform);
            Bounds? front = null;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                if (!r.name.StartsWith("hashira")) continue;
                if (front is Bounds fb) { fb.Encapsulate(r.bounds); front = fb; }
                else front = r.bounds;
            }
            if (front is Bounds f && f.center.z > all.center.z) inst.transform.rotation = Quaternion.Euler(0, 180, 0) * inst.transform.rotation;

            all = RendererBounds(inst.transform);
            inst.transform.localScale *= width / Mathf.Max(0.001f, all.size.x);
            all = RendererBounds(inst.transform);
            inst.transform.position -= new Vector3(all.center.x, all.min.y, all.center.z);

            inst.transform.SetParent(parent, false);
            inst.transform.localPosition += pos;
        }

        // ── 竜宮城 ───────────────────────────────────

        static void BuildRyugu(Vector3 origin)
        {
            var area = MakeArea("ryugu", origin, new Vector3(0, 0, -1.2f), 0);
            area.walkHalfExtents = new Vector2(5.5f, 11.8f);
            area.walkCenter = new Vector2(0, 6f);
            area.skyColor = new Color(0.04f, 0.15f, 0.3f);
            area.ambientColor = new Color(0.7f, 0.58f, 0.52f);
            area.sunColor = new Color(1f, 0.85f, 0.7f);
            area.sunIntensity = 1.0f;
            area.fog = true;
            area.fogColor = new Color(0.05f, 0.18f, 0.32f);
            area.fogDensity = 0.03f;
            var t = area.transform;

            var floor = Mat("RyuguFloor", new Color(0.55f, 0.2f, 0.12f));
            var pillar = Mat("RyuguPillar", new Color(0.85f, 0.15f, 0.1f));
            var gold = Mat("Gold", new Color(0.95f, 0.75f, 0.25f), 0.3f);
            var roof = Mat("RyuguRoof", new Color(0.3f, 0.08f, 0.06f));
            var wall = Mat("RyuguWall", new Color(0.9f, 0.85f, 0.7f));
            var dark = Mat("DarkWood", new Color(0.18f, 0.12f, 0.1f));
            var lantern = Mat("Lantern", new Color(1f, 0.6f, 0.25f), 2f);

            // 宴会場
            Prim(PrimitiveType.Cube, "HallFloor", t, new Vector3(0, -0.05f, 0), new Vector3(12, 0.1f, 12), floor);
            Prim(PrimitiveType.Cube, "Tatami", t, new Vector3(0, 0.005f, 1), new Vector3(6, 0.01f, 5), Mat("Tatami", new Color(0.75f, 0.72f, 0.45f)), collider: false);
            for (int i = 0; i < 8; i++)
            {
                float x = i < 4 ? -5.5f : 5.5f;
                float z = -4.5f + (i % 4) * 3.2f;
                Prim(PrimitiveType.Cylinder, "Pillar", t, new Vector3(x, 2, z), new Vector3(0.45f, 2, 0.45f), pillar, collider: false);
                Prim(PrimitiveType.Sphere, "Lantern", t, new Vector3(x * 0.92f, 2.3f, z), new Vector3(0.35f, 0.45f, 0.35f), lantern, collider: false);
            }
            Prim(PrimitiveType.Cube, "Roof", t, new Vector3(0, 4.2f, 0), new Vector3(12.6f, 0.3f, 12.6f), roof, collider: false);
            Prim(PrimitiveType.Cube, "RoofTrim", t, new Vector3(0, 4.0f, 0), new Vector3(12.8f, 0.12f, 12.8f), gold, collider: false);
            Prim(PrimitiveType.Cube, "WallBackL", t, new Vector3(-1.5f, 2, 6), new Vector3(9, 4, 0.2f), wall, collider: false);
            Prim(PrimitiveType.Cube, "WallBackR", t, new Vector3(5.5f, 2, 6), new Vector3(1, 4, 0.2f), wall, collider: false);
            Prim(PrimitiveType.Cube, "WallBackTop", t, new Vector3(4, 3.4f, 6), new Vector3(2, 1.2f, 0.2f), wall, collider: false);
            Prim(PrimitiveType.Cube, "WallL", t, new Vector3(-6, 2, 0), new Vector3(0.2f, 4, 12), wall, collider: false);
            Prim(PrimitiveType.Cube, "WallR", t, new Vector3(6, 2, 0), new Vector3(0.2f, 4, 12), wall, collider: false);
            Prim(PrimitiveType.Cube, "Gate", t, new Vector3(0, 3.2f, -6), new Vector3(5, 0.5f, 0.4f), pillar, collider: false);

            // 料理（口に運ぶと「楽しむ」）
            var table = Mat("BlackLacquer", new Color(0.08f, 0.06f, 0.06f));
            Prim(PrimitiveType.Cube, "TableL", t, new Vector3(-0.8f, 0.2f, 0.3f), new Vector3(1.0f, 0.4f, 0.55f), table);
            Prim(PrimitiveType.Cube, "TableR", t, new Vector3(0.8f, 0.2f, 0.3f), new Vector3(1.0f, 0.4f, 0.55f), table);
            var foods = new List<Grabbable>();
            var foodDefs = new (PrimitiveType type, Vector3 pos, Vector3 scale, Color color)[]
            {
                (PrimitiveType.Sphere, new Vector3(-1.05f, 0.45f, 0.25f), new Vector3(0.08f, 0.08f, 0.08f), new Color(1f, 0.85f, 0.85f)),
                (PrimitiveType.Cube, new Vector3(-0.6f, 0.43f, 0.3f), new Vector3(0.1f, 0.05f, 0.05f), new Color(1f, 0.45f, 0.3f)),
                (PrimitiveType.Sphere, new Vector3(0.6f, 0.45f, 0.25f), new Vector3(0.12f, 0.06f, 0.2f), new Color(0.95f, 0.3f, 0.3f)),
                (PrimitiveType.Cylinder, new Vector3(1.05f, 0.45f, 0.3f), new Vector3(0.08f, 0.04f, 0.08f), new Color(0.95f, 0.95f, 0.9f)),
            };
            for (int i = 0; i < foodDefs.Length; i++)
            {
                var d = foodDefs[i];
                var f = Prim(d.type, "Food" + i, t, d.pos, d.scale, Mat("Food" + i, d.color));
                foods.Add(MakeGrabbable(f, 0.1f));
            }
            var eat = Group("EatTrigger", t, Vector3.zero);
            eat.AddComponent<BringToHeadTrigger>().targets = foods.ToArray();
            Anchor(eat, "ryugu.eat");

            // 乙姫
            var oto = BuildHuman("Otohime", t, new Vector3(0, 0, 1.6f), 180, new Color(1f, 0.55f, 0.7f), 1.6f, new Color(0.08f, 0.05f, 0.05f));
            Prim(PrimitiveType.Cube, "Sash", oto.transform.Find("Body"), new Vector3(0, 0.62f, 0), new Vector3(0.48f, 0.08f, 0.4f), gold, collider: false);
            oto.GetComponent<SimpleCharacter>().lookAtPlayer = true;
            PrincessModelSetup.ReplaceVisual(oto);
            Anchor(oto, "ryugu.otohime");
            var otoZone = Group("TouchZone", oto.transform, Vector3.zero);
            TriggerBox(otoZone, new Vector3(0, 0.95f, 0), new Vector3(0.8f, 1.9f, 0.8f));
            otoZone.AddComponent<TouchTrigger>().armedVisual = Ring("OtoMarker", oto.transform, Vector3.zero, 0.9f, new Color(1f, 0.6f, 0.8f));
            Anchor(otoZone, "ryugu.otohime.touch");
            var gift = BuildTamatebako("Gift", oto.transform, new Vector3(0, 0.9f, 0.32f), 0.8f);
            gift.SetActive(false);
            gift.AddComponent<Resettable>();
            Anchor(gift, "ryugu.gift");

            // 鯛やひらめの舞い踊り
            var tai = Mat("Tai", new Color(1f, 0.35f, 0.35f));
            var hirame = Mat("Hirame", new Color(0.6f, 0.5f, 0.35f));
            for (int i = 0; i < 6; i++)
            {
                var fish = Prim(PrimitiveType.Sphere, i % 2 == 0 ? "Tai" : "Hirame", t, Vector3.zero,
                    i % 2 == 0 ? new Vector3(0.15f, 0.3f, 0.45f) : new Vector3(0.4f, 0.08f, 0.45f), i % 2 == 0 ? tai : hirame, collider: false);
                var sw = fish.AddComponent<Swimmer>();
                sw.center = new Vector3(0, 1.8f, 3.6f);
                sw.radius = 1.6f;
                sw.speed = 0.8f;
                fish.AddComponent<Resettable>();
            }

            // 帰りの亀（④で登場）
            var turtle = BuildTurtle("ReturnTurtle", t, new Vector3(-1.6f, 0, -3.2f), 30, 1.2f, "ryugu.turtle");
            turtle.SetActive(false);
            Point("ReturnSpawn", t, new Vector3(0, 0, -1.4f), 0, "ryugu.return.spawn");

            // 探索用通路と秘密の部屋（ED4）
            var corridorFloor = Mat("CorridorFloor", new Color(0.2f, 0.14f, 0.12f));
            Prim(PrimitiveType.Cube, "CorridorFloor", t, new Vector3(4, -0.05f, 10), new Vector3(2, 0.1f, 8), corridorFloor);
            Prim(PrimitiveType.Cube, "CorridorWallL", t, new Vector3(3, 1.5f, 10), new Vector3(0.15f, 3, 8), dark, collider: false);
            Prim(PrimitiveType.Cube, "CorridorWallR", t, new Vector3(5, 1.5f, 10), new Vector3(0.15f, 3, 8), dark, collider: false);
            Prim(PrimitiveType.Cube, "CorridorCeil", t, new Vector3(4, 3, 10), new Vector3(2.2f, 0.1f, 8), dark, collider: false);
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Sphere, "DimLantern", t, new Vector3(i % 2 == 0 ? 3.2f : 4.8f, 2f, 7.5f + i * 2.5f), new Vector3(0.2f, 0.28f, 0.2f), Mat("DimLantern", new Color(0.6f, 0.3f, 0.6f), 1.5f), collider: false);
            var corridor = Group("CorridorEnter", t, new Vector3(4, 0, 6.6f));
            var cz = corridor.AddComponent<ZoneTrigger>();
            cz.size = new Vector3(2f, 3f, 1.4f);
            cz.armedVisual = Ring("CorridorMarker", corridor.transform, Vector3.zero, 1.2f, new Color(0.7f, 0.5f, 1f));
            Anchor(corridor, "ryugu.corridor.enter");

            Prim(PrimitiveType.Cube, "SecretFloor", t, new Vector3(4, -0.05f, 16), new Vector3(4, 0.1f, 4), corridorFloor);
            Prim(PrimitiveType.Cube, "SecretWallBack", t, new Vector3(4, 1.5f, 18), new Vector3(4, 3, 0.15f), dark, collider: false);
            Prim(PrimitiveType.Cube, "SecretWallL", t, new Vector3(2, 1.5f, 16), new Vector3(0.15f, 3, 4), dark, collider: false);
            Prim(PrimitiveType.Cube, "SecretWallR", t, new Vector3(6, 1.5f, 16), new Vector3(0.15f, 3, 4), dark, collider: false);
            Prim(PrimitiveType.Cube, "SecretCeil", t, new Vector3(4, 3, 16), new Vector3(4.2f, 0.1f, 4.2f), dark, collider: false);
            Prim(PrimitiveType.Cube, "Shelf", t, new Vector3(4, 0.5f, 17.6f), new Vector3(3, 1.0f, 0.5f), dark);
            Prim(PrimitiveType.Sphere, "Candle", t, new Vector3(2.8f, 1.15f, 17.6f), new Vector3(0.1f, 0.2f, 0.1f), Mat("Candle", new Color(1f, 0.7f, 0.3f), 3f), collider: false);
            var scrollMat = Mat("Scroll", new Color(0.92f, 0.86f, 0.7f));
            var scrolls = new List<Grabbable>();
            for (int i = 0; i < 4; i++)
            {
                var s = Prim(PrimitiveType.Cylinder, "Scroll" + i, t, new Vector3(3.3f + i * 0.45f, 1.04f, 17.5f), new Vector3(0.07f, 0.13f, 0.07f), scrollMat, new Vector3(0, 0, 90));
                scrolls.Add(MakeGrabbable(s, 0.1f));
            }
            var scrollGrab = Group("ScrollGrab", t, Vector3.zero);
            scrollGrab.AddComponent<GrabTrigger>().targets = scrolls.ToArray();
            Anchor(scrollGrab, "ryugu.scroll.grab");
            var secret = Group("SecretEnter", t, new Vector3(4, 0, 14.6f));
            secret.AddComponent<ZoneTrigger>().size = new Vector3(2f, 3f, 1.2f);
            Anchor(secret, "ryugu.secret.enter");
            Point("SecretSpawn", t, new Vector3(4, 0, 15.6f), 0, "ryugu.secret.spawn");
        }

        // ── 帰還後の村 ───────────────────────────────

        static void BuildVillage(Vector3 origin)
        {
            var area = MakeArea("village", origin, new Vector3(0, 0, 0.3f), 0);
            area.walkHalfExtents = new Vector2(8, 12);
            area.walkCenter = new Vector2(0, 5);
            area.skyColor = new Color(0.7f, 0.74f, 0.78f);
            area.ambientColor = new Color(0.58f, 0.58f, 0.6f);
            area.sunColor = new Color(0.95f, 0.93f, 0.9f);
            area.sunIntensity = 0.9f;
            area.fog = true;
            area.fogColor = new Color(0.72f, 0.75f, 0.78f);
            area.fogDensity = 0.03f;
            var t = area.transform;

            Prim(PrimitiveType.Plane, "Ground", t, new Vector3(0, 0, 5), new Vector3(5, 1, 5), Mat("VillageGround", new Color(0.55f, 0.6f, 0.4f)));
            Prim(PrimitiveType.Cube, "Path", t, new Vector3(0, 0.005f, 8), new Vector3(2, 0.01f, 20), Mat("Path", new Color(0.7f, 0.6f, 0.45f)), collider: false);
            var house = Mat("OldHouse", new Color(0.5f, 0.42f, 0.35f));
            var thatch = Mat("Thatch", new Color(0.35f, 0.3f, 0.22f));
            var housePos = new[] { new Vector3(-4, 0, 3), new Vector3(4.2f, 0, 5), new Vector3(-4.5f, 0, 9), new Vector3(4, 0, 12) };
            foreach (var p in housePos)
            {
                Prim(PrimitiveType.Cube, "House", t, p + Vector3.up * 1.1f, new Vector3(3, 2.2f, 3), house, collider: false);
                Prim(PrimitiveType.Cube, "Roof", t, p + Vector3.up * 2.6f, new Vector3(2.4f, 2.4f, 3.2f), thatch, new Vector3(0, 0, 45), collider: false);
            }
            var elder = BuildHuman("Villager", t, new Vector3(1.8f, 0, 2.2f), 200, new Color(0.45f, 0.45f, 0.5f), 1.45f, new Color(0.85f, 0.85f, 0.85f));
            elder.GetComponent<SimpleCharacter>().lookAtPlayer = true;

            // 切り株の上の玉手箱
            Prim(PrimitiveType.Cylinder, "Stump", t, new Vector3(0, 0.35f, 0.95f), new Vector3(0.5f, 0.35f, 0.5f), Mat("Stump", new Color(0.45f, 0.32f, 0.2f)));
            var box = BuildTamatebako("Tamatebako", t, new Vector3(0, 0.7f, 0.95f), 1f);
            var lid = box.transform.Find("Lid").gameObject;
            var lidGrab = MakeGrabbable(lid, 0.3f, kinematicUntilGrabbed: true);
            Anchor(lid, "village.box.lid");
            var open = box.AddComponent<LidOpenTrigger>();
            open.lid = lidGrab;
            Anchor(box, "village.box.open");

            var smokeGo = new GameObject("Smoke");
            smokeGo.transform.SetParent(box.transform, false);
            smokeGo.transform.localPosition = lid.transform.localPosition;
            smokeGo.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var ps = smokeGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 4f;
            main.loop = false;
            main.startLifetime = 3f;
            main.startSpeed = 0.9f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new Color(0.97f, 0.97f, 0.97f);
            main.maxParticles = 300;
            var emission = ps.emission;
            emission.rateOverTime = 30f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.1f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2f));
            // ローポリ調の煙：白い球をメッシュパーティクルで出す（透過なしで軽い）
            var psr = smokeGo.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Mesh;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            psr.mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            psr.sharedMaterial = Mat("Smoke", new Color(0.97f, 0.97f, 0.97f), 0.4f);
            smokeGo.SetActive(false);
            smokeGo.AddComponent<Resettable>();
            Anchor(smokeGo, "village.smoke");

            var pathEnd = Group("PathEnd", t, new Vector3(0, 0, 9));
            var pz = pathEnd.AddComponent<ZoneTrigger>();
            pz.size = new Vector3(3f, 3f, 2f);
            pz.armedVisual = Ring("PathMarker", pathEnd.transform, Vector3.zero, 1.6f, new Color(1f, 0.85f, 0.3f));
            Anchor(pathEnd, "village.path.end");
        }
    }
}
