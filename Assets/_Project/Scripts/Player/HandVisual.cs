using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 手のモデル（Resources/Hands/Hand_L・Hand_R、指の骨つき）を仮のミトンと置き換え、コントローラーを握った形に指を曲げる。
    /// 人差し指はトリガー、ほかの指は握り具合に合わせて曲がり、物を持っている間はすべて握る。
    /// 老いた手（Hands/HandOld_L・HandOld_R、同じ骨）があれば、玉手箱を開けた後（SetAged）にそちらへ切り替える。
    /// モデルの軸の向きは取り込み設定に左右されるので、骨の位置から「指先の向き・人差し指側・手のひら」を求めて合わせる。
    /// </summary>
    public class HandVisual : MonoBehaviour
    {
        const float PalmToGripCenter = 0.03f;   // 握った拳の中心（コントローラーの持ち手）は手のひらから少し内側

        class Bone
        {
            public Transform transform;
            public Quaternion rest;
            public Vector3 axis;     // 骨のローカル座標での曲げ軸（指先が手のひら側へ回る向き）
            public float maxAngle;
        }

        static readonly string[] Fingers = { "index", "middle", "ring", "pinky", "thumb" };
        static readonly float[] FingerAngles = { 70, 90, 65 };
        static readonly float[] ThumbAngles = { 15, 30, 35 };

        Hand hand;
        HandGrabber grabber;
        readonly List<Bone>[] bones = new List<Bone>[Fingers.Length];
        readonly float[] curl = new float[Fingers.Length];
        Renderer[] youngRenderers = new Renderer[0];
        Renderer[] oldRenderers = new Renderer[0];

        /// <summary>モデルがあれば付けて true。無ければ仮のミトンのまま。</summary>
        public static bool TryAttach(Hand hand)
        {
            string suffix = hand.side == HandSide.Left ? "L" : "R";
            var prefab = Resources.Load<GameObject>("Hands/Hand_" + suffix);
            if (prefab == null) return false;
            var model = Instantiate(prefab, hand.transform, false);
            model.name = "Hand model";
            var renderers = model.GetComponentsInChildren<Renderer>();
            var byName = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>()) byName[t.name] = t;
            if (renderers.Length == 0 || !byName.TryGetValue("hand." + suffix, out var wrist) || !byName.TryGetValue("middle.01." + suffix, out var knuckle)
                || !byName.TryGetValue("index.01." + suffix, out var index) || !byName.TryGetValue("pinky.01." + suffix, out var pinky))
            {
                Debug.LogWarning("[HandVisual] 手のモデルに必要な骨がありません: Hand_" + suffix);
                Destroy(model);
                return false;
            }

            // 手のローカル座標：+Z＝指先の向き、+Y＝親指（人差し指）側、手のひらは左手が+X・右手が-X（TrackedPose参照）
            var tr = hand.transform;
            Vector3 P(Transform b) => tr.InverseTransformPoint(b.position);
            float palmLength = Vector3.Distance(P(knuckle), P(wrist));
            if (palmLength < 0.05f || palmLength > 0.2f) model.transform.localScale *= 0.095f / Mathf.Max(0.0001f, palmLength);
            var fingers = (P(knuckle) - P(wrist)).normalized;
            var palmCenter = (P(knuckle) + P(wrist)) * 0.5f;
            // 手のひらの向き：指の並びに垂直で、親指が垂れている側
            var palm = Vector3.Cross(fingers, Vector3.ProjectOnPlane(P(index) - P(pinky), fingers)).normalized;
            if (byName.TryGetValue("thumb.03." + suffix, out var thumbTip) && Vector3.Dot(P(thumbTip) - palmCenter, palm) < 0) palm = -palm;
            // 指先→+Z、手のひら→左手+X／右手-X になる回転（LookRotation の右方向＝Cross(上, 前)）
            var up = hand.side == HandSide.Left ? Vector3.Cross(fingers, palm) : Vector3.Cross(palm, fingers);
            var align = Quaternion.Inverse(Quaternion.LookRotation(fingers, up));
            var pivot = model.transform.localPosition;
            model.transform.localRotation = align * model.transform.localRotation;   // モデルの原点まわりに回る
            var palmNormal = hand.side == HandSide.Left ? Vector3.right : Vector3.left;
            model.transform.localPosition += -(align * (palmCenter - pivot) + pivot) - palmNormal * PalmToGripCenter + new Vector3(0, 0, -0.01f);

            // 色は肌の色・老いた色の切り替え（Hand.SetAged）に合わせて URP の素材にする
            var skin = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Hand skin" };
            skin.SetColor("_BaseColor", hand.skinColor);
            skin.SetFloat("_Smoothness", 0.25f);
            foreach (var r in renderers)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = skin;
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            foreach (var r in hand.skinRenderers) if (r != null) r.enabled = false;
            hand.skinRenderers = renderers;

            var visual = hand.gameObject.AddComponent<HandVisual>();
            visual.youngRenderers = renderers;
            visual.oldRenderers = AttachOldHand(model.transform, byName, suffix);
            visual.Bind(hand, byName, suffix, tr.TransformDirection(palmNormal));
            return true;
        }

        /// <summary>老いた手を、今の手と同じ位置に置き、今の手の骨で動くようにする（指の曲げも共通になる）。</summary>
        static Renderer[] AttachOldHand(Transform young, Dictionary<string, Transform> bones, string suffix)
        {
            var prefab = Resources.Load<GameObject>("Hands/HandOld_" + suffix);
            if (prefab == null) return new Renderer[0];
            var old = Instantiate(prefab, young.parent, false);
            old.name = "Old hand model";
            old.transform.SetLocalPositionAndRotation(young.localPosition, young.localRotation);
            old.transform.localScale = young.localScale;
            var list = new List<Renderer>();
            foreach (var smr in old.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mapped = new Transform[smr.bones.Length];
                for (int i = 0; i < mapped.Length; i++)
                    mapped[i] = smr.bones[i] != null && bones.TryGetValue(smr.bones[i].name, out var b) ? b : null;
                if (System.Array.IndexOf(mapped, null) >= 0) { Debug.LogWarning("[HandVisual] 老いた手の骨が今の手と合いません: HandOld_" + suffix); Destroy(old); return new Renderer[0]; }
                if (smr.rootBone != null && bones.TryGetValue(smr.rootBone.name, out var root)) smr.rootBone = root;
                // 老いた手のファイルは骨の基準の座標が今の手と違うので、今の手の骨（まだ曲げていない姿勢）に合わせて
                // バインドポーズを作り直す。こうすると、曲げていないときは置いた位置の形のまま表示される
                var mesh = Instantiate(smr.sharedMesh);
                var bind = new Matrix4x4[mapped.Length];
                for (int i = 0; i < mapped.Length; i++) bind[i] = mapped[i].worldToLocalMatrix * smr.transform.localToWorldMatrix;
                mesh.bindposes = bind;
                smr.sharedMesh = mesh;
                smr.bones = mapped;
                smr.updateWhenOffscreen = true;
                // 老いた手の色はモデルの素材の色を使う
                var color = smr.sharedMaterial != null ? smr.sharedMaterial.color : new Color(0.78f, 0.74f, 0.70f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Old hand skin" };
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", 0.2f);
                smr.sharedMaterial = mat;
                smr.enabled = false;
                list.Add(smr);
            }
            // 骨は今の手のものを使うので、老いた手のモデルが持ってきた骨は消す
            foreach (Transform child in old.transform)
                if (child.GetComponent<SkinnedMeshRenderer>() == null) Destroy(child.gameObject);
            return list.ToArray();
        }

        /// <summary>老いた手に切り替える（無ければ false を返し、Hand が色だけ変える）。</summary>
        public bool SetAged(bool aged)
        {
            if (oldRenderers.Length == 0) return false;
            foreach (var r in youngRenderers) if (r != null) r.enabled = !aged;
            foreach (var r in oldRenderers) if (r != null) r.enabled = aged;
            return true;
        }

        void Bind(Hand h, Dictionary<string, Transform> byName, string suffix, Vector3 palmWorld)
        {
            hand = h;
            grabber = GetComponent<HandGrabber>();
            for (int f = 0; f < Fingers.Length; f++)
            {
                bones[f] = new List<Bone>();
                var chain = new List<Transform>();
                for (int s = 1; s <= 3 && byName.TryGetValue($"{Fingers[f]}.0{s}.{suffix}", out var b); s++) chain.Add(b);
                for (int s = 0; s < chain.Count; s++)
                {
                    var b = chain[s];
                    var dir = s + 1 < chain.Count ? chain[s + 1].position - b.position : b.position - chain[s - 1 >= 0 ? s - 1 : 0].position;
                    if (dir.sqrMagnitude < 1e-8f) continue;
                    var axis = Vector3.Cross(dir.normalized, palmWorld);
                    if (axis.sqrMagnitude < 1e-6f) continue;
                    bones[f].Add(new Bone
                    {
                        transform = b,
                        rest = b.localRotation,
                        axis = Quaternion.Inverse(b.rotation) * axis.normalized,
                        maxAngle = (Fingers[f] == "thumb" ? ThumbAngles : FingerAngles)[Mathf.Min(s, 2)],
                    });
                }
            }
        }

        void LateUpdate()
        {
            bool holding = grabber != null && grabber.Held != null;
            float trigger = hand.TriggerValue, grip = hand.GripValue;
            // コントローラーを持っている手なので、何も押していなくても軽く握った形にする
            float[] target =
            {
                holding ? 1 : Mathf.Lerp(0.25f, 1, trigger),
                holding ? 1 : Mathf.Lerp(0.55f, 1, grip),
                holding ? 1 : Mathf.Lerp(0.6f, 1, grip),
                holding ? 1 : Mathf.Lerp(0.65f, 1, grip),
                holding ? 0.9f : 0.45f,
            };
            float k = 1 - Mathf.Exp(-20f * Time.unscaledDeltaTime);
            for (int f = 0; f < Fingers.Length; f++)
            {
                curl[f] = Mathf.Lerp(curl[f], target[f], k);
                foreach (var b in bones[f])
                    b.transform.localRotation = b.rest * Quaternion.AngleAxis(b.maxAngle * curl[f], b.axis);
            }
        }
    }
}
