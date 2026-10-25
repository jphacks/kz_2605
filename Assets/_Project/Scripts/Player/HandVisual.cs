using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 手のモデル（Resources/Hands/Hand_L・Hand_R、指の骨つき）を仮のミトンと置き換え、コントローラーを握った形に指を曲げる。
    /// 人差し指はトリガー、ほかの指は握り具合に合わせて曲がり、物を持っている間はすべて握る。
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
            visual.Bind(hand, byName, suffix, tr.TransformDirection(palmNormal));
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
