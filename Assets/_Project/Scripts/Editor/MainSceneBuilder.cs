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

        static GameObject BuildTurtle(string name, Transform parent, Vector3 pos, float yaw, float size, string idPrefix)
        {
            var root = Group(name, parent, pos, yaw);
            var body = Group("Body", root.transform, Vector3.zero);
            var shellMat = Mat("TurtleShell", new Color(0.35f, 0.5f, 0.25f));
            var skinMat = Mat("TurtleSkin", new Color(0.6f, 0.75f, 0.45f));
            var eye = Mat("Black", new Color(0.05f, 0.05f, 0.05f));
            var t = body.transform;
            Prim(PrimitiveType.Sphere, "Shell", t, new Vector3(0, 0.2f, 0) * size, new Vector3(0.75f, 0.32f, 0.9f) * size, shellMat, collider: false);
            Prim(PrimitiveType.Sphere, "Head", t, new Vector3(0, 0.2f, 0.52f) * size, Vector3.one * 0.2f * size, skinMat, collider: false);
            Prim(PrimitiveType.Sphere, "EyeL", t, new Vector3(-0.06f, 0.25f, 0.6f) * size, Vector3.one * 0.04f * size, eye, collider: false);
            Prim(PrimitiveType.Sphere, "EyeR", t, new Vector3(0.06f, 0.25f, 0.6f) * size, Vector3.one * 0.04f * size, eye, collider: false);
            foreach (var (x, z) in new[] { (-0.35f, 0.3f), (0.35f, 0.3f), (-0.32f, -0.3f), (0.32f, -0.3f) })
                Prim(PrimitiveType.Sphere, "Flipper", t, new Vector3(x, 0.1f, z) * size, new Vector3(0.25f, 0.06f, 0.15f) * size, skinMat, collider: false);

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

        static GameObject BuildTamatebako(string name, Transform parent, Vector3 pos, float scale)
        {
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

        // ── タイトル（絵本の部屋） ──────────────────────

        static void BuildTitle(Vector3 origin)
        {
            var area = MakeArea("title", origin, Vector3.zero, 0);
            area.walkHalfExtents = new Vector2(1.5f, 1.5f);
            area.skyColor = new Color(0.04f, 0.04f, 0.1f);
            area.ambientColor = new Color(0.45f, 0.42f, 0.5f);
            area.sunColor = new Color(1f, 0.9f, 0.75f);
            area.sunIntensity = 0.8f;
            var t = area.transform;

            Prim(PrimitiveType.Cylinder, "Floor", t, new Vector3(0, -0.05f, 0), new Vector3(8, 0.05f, 8), Mat("TitleFloor", new Color(0.3f, 0.2f, 0.15f)));
            Prim(PrimitiveType.Cylinder, "Pedestal", t, new Vector3(0, 0.45f, 0.55f), new Vector3(0.45f, 0.45f, 0.45f), Mat("Wood", new Color(0.45f, 0.3f, 0.18f)));
            var rnd = new System.Random(7);
            var star = Mat("Star", new Color(1f, 0.95f, 0.8f), 2f, unlit: true);
            for (int i = 0; i < 70; i++)
            {
                var dir = new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 0.9f + 0.1f, (float)rnd.NextDouble() * 2 - 1).normalized;
                Prim(PrimitiveType.Sphere, "Star", t, dir * 25f, Vector3.one * (0.08f + (float)rnd.NextDouble() * 0.12f), star, collider: false);
            }

            // 絵本
            var book = Group("Picturebook", t, new Vector3(0, 0.92f, 0.55f));
            Anchor(book, "title.book");
            var pb = book.AddComponent<Picturebook>();
            var red = Mat("BookCover", new Color(0.7f, 0.15f, 0.12f));
            Prim(PrimitiveType.Cube, "BackCover", book.transform, new Vector3(0, 0.0075f, 0), new Vector3(0.36f, 0.015f, 0.26f), red, collider: false);
            Prim(PrimitiveType.Cube, "Pages", book.transform, new Vector3(0, 0.03f, 0), new Vector3(0.34f, 0.03f, 0.24f), Mat("Paper", new Color(0.98f, 0.95f, 0.88f)), collider: false);
            var hinge = Group("CoverHinge", book.transform, new Vector3(-0.18f, 0.05f, 0));
            Prim(PrimitiveType.Cube, "Cover", hinge.transform, new Vector3(0.18f, 0, 0), new Vector3(0.36f, 0.015f, 0.26f), red, collider: false);
            Prim(PrimitiveType.Cube, "CoverDeco", hinge.transform, new Vector3(0.18f, 0.009f, 0), new Vector3(0.2f, 0.003f, 0.12f), Mat("Gold", new Color(0.95f, 0.75f, 0.25f), 0.3f), collider: false);
            pb.coverHinge = hinge.transform;
            pb.openAngle = 175f;
            var glow = Prim(PrimitiveType.Sphere, "Glow", book.transform, new Vector3(0, 0.1f, 0), Vector3.one * 0.3f, Mat("Glow", new Color(1f, 0.9f, 0.5f), 3f, unlit: true), collider: false);
            glow.SetActive(false);
            pb.glowRenderer = glow.GetComponent<Renderer>();
            var light = new GameObject("GlowLight").AddComponent<Light>();
            light.transform.SetParent(book.transform, false);
            light.transform.localPosition = new Vector3(0, 0.3f, 0);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.5f);
            light.range = 4;
            light.enabled = false;
            pb.glowLight = light;
            var zone = Group("OpenZone", book.transform, Vector3.zero);
            TriggerBox(zone, new Vector3(0, 0.05f, 0), new Vector3(0.45f, 0.25f, 0.35f));
            pb.openTrigger = zone.AddComponent<TouchTrigger>();
            pb.openTrigger.requireGrip = true;
            pb.openTrigger.armedVisual = Ring("BookMarker", t, new Vector3(0, 0.905f, 0.55f), 0.5f, new Color(1f, 0.85f, 0.3f));

            Point("Hud", t, new Vector3(0, 1.85f, 2.4f), 0, "title.hud");
            Point("MenuAnchor", t, new Vector3(0, 1.38f, 1.15f), 0, "title.menu");
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
            BuildTurtle("RideTurtle", ride.transform, new Vector3(0, -0.75f, 0.2f), 0, 2.2f, null);
            Point("Seat", ride.transform, Vector3.zero, 0, "sea.seat");
            Point("End", t, new Vector3(0, 0, 40), 0, "sea.end");
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
            smokeGo.transform.localPosition = new Vector3(0, 0.25f, 0);
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
