using System.Collections;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// ED1〜9の演出。演出が終わったら絵本が閉じて「おわり」へ進む。
    /// ホラー分岐は照明・色・フォグを変えて、変化がはっきり伝わるようにする（04 デザイン仕様書）。
    /// </summary>
    public class EndingDirector : MonoBehaviour
    {
        public AudioClip rumbleSound;
        public AudioClip smokeSound;

        GameManager gm => GameManager.Instance;
        IEnumerator Say(params Line[] lines) => gm.narration.Say(lines);
        static T A<T>(string id) where T : Component => StoryAnchor.Get<T>(id);
        static Transform A(string id) => StoryAnchor.Get(id);
        bool escaped;
        GameObject effects;
        Material moteMaterial;
        Texture2D moteTexture;

        // All temporary visuals share one lifetime, including when a flow is interrupted.
        public void ClearEffects()
        {
            if (effects != null) { effects.SetActive(false); Destroy(effects); }
            if (moteMaterial != null) Destroy(moteMaterial);
            if (moteTexture != null) Destroy(moteTexture);
            effects = null;
            moteMaterial = null;
            moteTexture = null;
        }

        void OnDestroy() => ClearEffects();

        void Motes(Vector3 position, Color color, float radius, float speed, int count = 70)
        {
            if (effects == null)
            {
                effects = new GameObject("Ending atmosphere");
                effects.transform.SetParent(gm.CurrentArea.transform, false);
            }
            var go = new GameObject("Drifting motes");
            go.transform.SetParent(effects.transform, false);
            go.transform.position = position;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.startLifetime = 4f;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .075f);
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = count / 4f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = speed;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.65f, .2f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            if (moteMaterial == null)
            {
                // A Resources material preserves the transparent shader variant in builds.
                moteMaterial = new Material(Resources.Load<Material>("EndingMotes"));
                moteTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                moteTexture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[32 * 32];
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float alpha = Mathf.Clamp01(1 - new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).sqrMagnitude);
                        pixels[y * 32 + x] = new Color(1, 1, 1, alpha * alpha);
                    }
                moteTexture.SetPixels(pixels);
                moteTexture.Apply(false, true);
                moteMaterial.SetTexture("_BaseMap", moteTexture);
                moteMaterial.SetFloat("_Surface", 1);
                moteMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                moteMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                moteMaterial.SetFloat("_ZWrite", 0);
                moteMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                moteMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = moteMaterial;
            particles.Play();
        }

        IEnumerator Bow(SimpleCharacter character, float angle = 18f)
        {
            character.idle = SimpleCharacter.Idle.None;
            var body = character.body;
            var rest = body.localRotation;
            yield return Tween.Rotate(body, rest * Quaternion.Euler(angle, 0, 0), .55f);
            yield return Tween.Wait(.45f);
            yield return Tween.Rotate(body, rest, .7f);
        }

        public IEnumerator Play(EndingId id)
        {
            ClearEffects();
            escaped = false;
            switch (id)
            {
                case EndingId.ED1: yield return Ed1(); break;
                case EndingId.ED2: yield return Ed2(); break;
                case EndingId.ED3: yield return Ed3(); break;
                case EndingId.ED4: yield return Ed4(); break;
                case EndingId.ED5: yield return Ed5(); break;
                case EndingId.ED6: yield return Ed6(); break;
                case EndingId.ED7: yield return Ed7(); break;
                case EndingId.ED8: yield return Ed8(); break;
            }
            if (escaped) id = EndingId.ED9;
            ClearEffects();
            gm.Rig.LocomotionEnabled = true;
            yield return gm.Owari(id, compact: id == EndingId.ED2);
        }

        IEnumerator Reframe(Vector3 floorPosition, Vector3 focus)
        {
            yield return gm.Fader.FadeOut(.45f);
            var point = new GameObject("Cinematic viewpoint");
            point.transform.position=floorPosition;
            var forward=focus-floorPosition; forward.y=0;
            point.transform.rotation=Quaternion.LookRotation(forward);
            gm.Rig.TeleportTo(point.transform);
            var simulator=gm.Rig.GetComponent<DesktopSimulator>();
            if(DesktopSimulator.Active && simulator!=null) simulator.LookAt(focus);
            Destroy(point);
            yield return gm.Fader.FadeIn(.65f);
        }

        IEnumerator EmergeOtohime(bool archive)
        {
            var area=gm.GetArea("ryugu").transform;
            var oto=A("ryugu.otohime");
            var character=oto.GetComponent<SimpleCharacter>();
            character.StopAllCoroutines(); character.lookAtPlayer=true; character.idle=SimpleCharacter.Idle.None;
            gm.Rig.LocomotionEnabled=false;
            var floor=area.TransformPoint(archive?new Vector3(4,0,14.9f):new Vector3(0,0,-1.6f));
            var destination=area.TransformPoint(archive?new Vector3(4,0,17.05f):new Vector3(0,0,1.0f));
            oto.position=destination-Vector3.up*2.2f;
            Tween.Tint(oto.gameObject,new Color(.38f,.46f,.49f));
            yield return Reframe(floor,destination+Vector3.up*1.25f);
            gm.PlaySfx(rumbleSound);
            Motes(destination + Vector3.up, new Color(.25f,.65f,.64f), 1.7f, .12f);
            yield return Tween.Move(oto,destination,3f);
            var p=oto.position;
            // A pause before the final approach makes the emergence readable.
            yield return Tween.Wait(.65f);
            yield return Tween.Move(oto,Vector3.Lerp(p,floor,.30f),1.2f);
        }

        /// <summary>ED1：見過ごす。物語が始まらないコメディ。</summary>
        IEnumerator Ed1()
        {
            var turtle = A<SimpleCharacter>("beach.turtle");
            turtle.lookAtPlayer = true;
            yield return Say(StoryScript.Ed1[0], StoryScript.Ed1[1]);
            // A hopeful glance, then a slow, defeated bow.
            yield return Bow(turtle, 26f);
            turtle.idle = SimpleCharacter.Idle.None;
            // 色あせていく＝物語が始まらない
            gm.StartCoroutine(Tween.Environment(new Color(0.75f, 0.72f, 0.65f), new Color(0.55f, 0.53f, 0.5f),
                new Color(0.9f, 0.85f, 0.75f), 0.6f, new Color(0.8f, 0.78f, 0.72f), 0.03f, 4f));
            yield return Say(StoryScript.Ed1[2], StoryScript.Ed1[3]);
            yield return Tween.Rotate(turtle.body, turtle.body.localRotation * Quaternion.Euler(0,0,-12), .8f);
            yield return Tween.Wait(1f);
        }

        /// <summary>ED2：一緒にいたずらする。亀の正体は竜宮城の使い。海の力で巨大になって報復し、海に引きずり込むホラー。</summary>
        IEnumerator Ed2()
        {
            var turtle=A("beach.turtle");
            gm.Rig.LocomotionEnabled=false;
            for (int i = 0; i < 3; i++)
            {
                var c = A<SimpleCharacter>("beach.child." + i);
                c.idle = SimpleCharacter.Idle.Hop;
                c.idleSpeed = 14f;
                c.StartCoroutine(c.BullyTarget(turtle,2f));
            }
            var tc=turtle.GetComponent<SimpleCharacter>();
            var body=tc.body;
            var rest=body.localScale;
            var squash=gm.StartCoroutine(Tween.Run(2f,k=>body.localScale=Vector3.Scale(rest,new Vector3(1,1-.10f*Mathf.Abs(Mathf.Sin(k*12)),1))));
            yield return Say(StoryScript.Ed2a[0]);
            yield return squash;
            body.localScale=rest;
            tc.StopAllCoroutines();
            tc.idle=SimpleCharacter.Idle.None;
            tc.lookAtPlayer = true;
            gm.story.FleeBeachChildren(7f);
            // Reposition under black; keep the HMD stationary during the reveal.
            yield return Reframe(turtle.position-turtle.parent.forward*8.5f,turtle.position+Vector3.up*2.5f);
            var facing=gm.Rig.Head.position-turtle.position; facing.y=0;
            if(facing.sqrMagnitude>.01f) turtle.rotation=Quaternion.LookRotation(facing);
            gm.PlaySfx(rumbleSound);
            Motes(turtle.position + Vector3.up, new Color(.24f,.48f,.62f), 3f, .45f, 110);
            // 急に大きくなると何が起きたか分からないので、海の異変 → 亀の怒り → 正体の説明 の順に見せてから巨大化させる
            yield return Say(StoryScript.Ed2a[1]);
            yield return Say(StoryScript.Ed2b[0]);
            yield return Tween.Wait(.4f);
            gm.StartCoroutine(Tween.Environment(new Color(.015f,.035f,.055f),new Color(.12f,.17f,.20f),
                new Color(.65f,.78f,.85f),.65f,new Color(.02f,.055f,.075f),.025f,2.6f));
            // 海の光が亀に集まり、その力で大きくなる。説明のナレーションと同時に、見上げる時間をとってゆっくり伸ばす
            Motes(turtle.position + Vector3.up, new Color(.35f,.75f,.85f), 1.2f, .9f, 140);
            var scale=turtle.localScale;
            var grow=gm.StartCoroutine(Tween.Run(3.5f,k=>turtle.localScale=Vector3.LerpUnclamped(scale,scale*10f,Tween.Smooth(k))));
            yield return Say(StoryScript.Ed2b[1]);
            yield return grow;
            yield return Tween.Wait(.6f);
            // Limit the lunge using the enlarged mesh bounds, then cut to deep sea.
            var direction=gm.Rig.Head.position-turtle.position; direction.y=0;
            var distance=direction.magnitude;
            direction=direction.normalized;
            float front=0;
            foreach(var renderer in turtle.GetComponentsInChildren<Renderer>())
            {
                var bounds=renderer.bounds;
                var extent=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(direction.x),0,Mathf.Abs(direction.z)));
                front=Mathf.Max(front,Vector3.Dot(bounds.center-turtle.position,direction)+extent);
            }
            var start=turtle.position;
            float travel=Mathf.Clamp(distance-front-1.5f,0,1.8f);
            yield return Tween.Run(.95f,k=>turtle.position=start+direction*(travel*k*k));
            yield return gm.Fader.FadeTo(new Color(0f,.025f,.055f),1f,.7f);
            yield return Say(StoryScript.Ed2b[2]);
            yield return Tween.Wait(.6f);
        }

        /// <summary>ED3：行かない。亀が「ここまで準備したのに！」と嘆くコメディ。</summary>
        IEnumerator Ed3()
        {
            var turtle = A("beach.turtle");
            var body = turtle.GetComponent<SimpleCharacter>().body;
            var character = turtle.GetComponent<SimpleCharacter>();
            character.idle = SimpleCharacter.Idle.None;
            var restRotation = body.localRotation;
            yield return Say(StoryScript.Ed3[0], StoryScript.Ed3[1]);
            // ひっくり返ってすねる
            yield return Tween.Rotate(body, restRotation * Quaternion.Euler(0, 0, 180), 0.8f);
            yield return Tween.Run(.9f, k => body.localRotation = restRotation * Quaternion.Euler(0, 0, 180 + Mathf.Sin(k * Mathf.PI * 6) * 12));
            yield return Tween.Wait(0.8f);
            yield return Tween.Rotate(body, restRotation, 0.6f);
            yield return Bow(character, 22f);
            var tc = turtle.GetComponent<SimpleCharacter>();
            tc.lookAtPlayer = false;
            tc.StartMoveTo(A("beach.sea.point").position, 0.6f);
            yield return Say(StoryScript.Ed3[2], StoryScript.Ed3[3]);
            yield return Tween.Wait(1f);
        }

        /// <summary>ED4：城内を探索。禁じられた部屋で過去の客の記録を見つけ、乙姫に発覚。城が廃墟化するホラー。</summary>
        IEnumerator Ed4()
        {
            var ryugu = gm.GetArea("ryugu");
            // 宴の音が遠ざかり、少しずつ暗くなる
            gm.StartCoroutine(Tween.Environment(new Color(0.05f, 0.08f, 0.15f), new Color(0.25f, 0.25f, 0.32f),
                new Color(0.6f, 0.6f, 0.8f), 0.4f, new Color(0.05f, 0.06f, 0.12f), 0.04f, 6f));
            foreach (var s in ryugu.GetComponentsInChildren<Swimmer>()) s.SpeedMultiplier = 0.2f;
            yield return Say(StoryScript.Ed4Corridor);

            var room = A<ZoneTrigger>("ryugu.secret.enter");
            room.Arm();
            float t = 0;
            while (!room.Fired && t < 40f && GameInput.DebugNumberKeyDown() < 0) { t += Time.deltaTime; yield return null; }
            room.Disarm();
            if (!room.Fired)
            {
                yield return gm.Fader.FadeOut(0.6f);
                gm.Rig.TeleportTo(A("ryugu.secret.spawn"));
                yield return gm.Fader.FadeIn(0.6f);
            }

            yield return Say(StoryScript.Ed4Room);
            Motes(A("ryugu.secret.spawn").position + Vector3.up * 1.4f, new Color(.48f,.58f,.55f), 2.5f, -.04f);
            var scroll = A<GrabTrigger>("ryugu.scroll.grab");
            scroll.Arm();
            t = 0;
            while (!scroll.Fired && t < 25f && GameInput.DebugNumberKeyDown() < 0) { t += Time.deltaTime; yield return null; }
            scroll.Disarm();

            yield return Say(StoryScript.Ed4Scroll[0], StoryScript.Ed4Scroll[1]);

            gm.StartCoroutine(Tween.Environment(new Color(.01f,.025f,.035f),new Color(.07f,.10f,.12f),
                new Color(.32f,.48f,.50f),.22f,new Color(.02f,.05f,.06f),.065f,2f));
            yield return EmergeOtohime(true);
            yield return Say(StoryScript.Ed4Scroll[2]);

            int escapeChoice=-1;
            yield return gm.story.Choose(new ChoiceDef
            {
                question="乙姫が出口をふさごうとしている。",
                timeLimit=gm.story.choiceTime,
                options=new[]
                {
                    new ChoiceDef.Option("その場に残る","このボタンで決定") {button=true},
                    new ChoiceDef.Option("逃げる","通路へ逃げ出す") {button=true},
                },
            },v=>escapeChoice=v);
            if(escapeChoice==1)
            {
                yield return EscapeOtohime();
                escaped=true;
                yield break;
            }

            // 廃墟化
            gm.StartCoroutine(Tween.Environment(new Color(0.02f, 0.01f, 0.02f), new Color(0.18f, 0.08f, 0.08f),
                new Color(0.5f, 0.1f, 0.1f), 0.3f, new Color(0.03f, 0.01f, 0.02f), 0.08f, 3f));
            Tween.Tint(ryugu.gameObject, new Color(0.4f, 0.38f, 0.36f));
            gm.PlaySfx(rumbleSound);
            Motes(gm.Rig.Head.position + gm.Rig.Head.forward * 2, new Color(.45f,.32f,.28f), 3f, -.18f, 120);
            foreach (var s in ryugu.GetComponentsInChildren<Swimmer>()) s.gameObject.SetActive(false);
            yield return Say(StoryScript.Ed4End);
            yield return gm.Fader.FadeOut(2f);
        }

        /// <summary>ED5：帰らない。宴が繰り返され、永遠に閉じ込められるホラー／ミステリー。</summary>
        IEnumerator Ed5()
        {
            var ryugu = gm.GetArea("ryugu");
            var swimmers = ryugu.GetComponentsInChildren<Swimmer>();
            var loopPositions = new Vector3[swimmers.Length];
            var loopRotations = new Quaternion[swimmers.Length];
            var loopPhases = new float[swimmers.Length];
            for (int i = 0; i < swimmers.Length; i++)
            {
                loopPositions[i] = swimmers[i].transform.position;
                loopRotations[i] = swimmers[i].transform.rotation;
                loopPhases[i] = swimmers[i].Phase;
            }
            yield return Say(StoryScript.Ed5);

            Line[][] loops = { StoryScript.Ed5Loop1, StoryScript.Ed5Loop2, StoryScript.Ed5Loop3 };
            Color[] skies = { new(0.35f, 0.1f, 0.2f), new(0.2f, 0.05f, 0.2f), new(0.08f, 0.02f, 0.08f) };
            for (int i = 0; i < loops.Length; i++)
            {
                foreach (var s in swimmers) s.SpeedMultiplier = 2f + i * 2.5f;
                var sky = skies[i];
                gm.StartCoroutine(Tween.Environment(sky, sky * 1.6f + new Color(0.1f, 0.05f, 0.05f), new Color(1f, 0.5f - i * 0.15f, 0.4f), 0.9f - i * 0.2f, sky, 0.03f + i * 0.02f, 1.5f));
                yield return gm.Fader.FadeTo(Color.black, 0.85f, 0.4f);
                // The same dancers return to the same places at every repetition.
                for (int j = 0; j < swimmers.Length; j++)
                {
                    swimmers[j].Phase = loopPhases[j];
                    swimmers[j].transform.SetPositionAndRotation(loopPositions[j], loopRotations[j]);
                }
                yield return Tween.Wait(.35f + i * .25f);
                yield return gm.Fader.FadeIn(0.4f);
                yield return Say(loops[i]);
            }
            var oto = A<SimpleCharacter>("ryugu.otohime");
            oto.lookAtPlayer = true;
            foreach(var swimmer in swimmers) swimmer.SpeedMultiplier=0;
            yield return Tween.Wait(1.2f);
            yield return Say(new Line(null,"乙姫","帰り道なんて、もうありません。"));
            yield return EmergeOtohime(false);
            yield return Say(StoryScript.Ed5End);
            yield return gm.Fader.FadeTo(new Color(0.15f, 0f, 0.05f), 1f, 2.5f);
        }

        /// <summary>ED6：玉手箱を開ける。煙で老人になる原作準拠の通常エンド。</summary>
        IEnumerator Ed6()
        {
            gm.PlaySfx(smokeSound);
            var smoke = A<ParticleSystem>("village.smoke");
            smoke.gameObject.SetActive(true);
            smoke.Play();
            Motes(smoke.transform.position + Vector3.up * .5f, new Color(.85f,.9f,1f), .7f, .3f, 90);
            gm.StartCoroutine(Tween.Environment(new Color(0.9f, 0.9f, 0.92f), new Color(0.85f, 0.85f, 0.88f),
                Color.white, 1.0f, new Color(0.92f, 0.92f, 0.94f), 0.25f, 5f));
            yield return Say(StoryScript.Ed6[0]);
            yield return gm.Fader.FadeTo(new Color(.92f,.95f,1), .6f, 1.2f);
            gm.Rig.SetAgedHands(true);
            yield return gm.Fader.FadeIn(1.8f);
            yield return Say(StoryScript.Ed6[1]);
            yield return Tween.Wait(1.5f);   // 自分の手を見る時間
            yield return Say(StoryScript.Ed6[2], StoryScript.Ed6[3]);
            yield return gm.Fader.FadeTo(Color.white, 1f, 2f);
        }

        /// <summary>ED7：開けない。変わった世界で新たな人生を歩む感動エンド。</summary>
        IEnumerator Ed7()
        {
            var box = A<Grabbable>("village.box.lid");
            box.ForceRelease();
            gm.StartCoroutine(Tween.Environment(new Color(1f, 0.72f, 0.5f), new Color(0.75f, 0.6f, 0.5f),
                new Color(1f, 0.75f, 0.5f), 1.4f, new Color(1f, 0.8f, 0.6f), 0.01f, 5f));
            var sun = gm.sun.transform;
            var sunStart = sun.rotation;
            gm.StartCoroutine(Tween.Run(5f, k => sun.rotation = Quaternion.Slerp(sunStart, Quaternion.Euler(12, 180, 0), Tween.Smooth(k))));
            Motes(box.transform.position + Vector3.up, new Color(1f,.78f,.38f), 2f, .07f);
            yield return Say(StoryScript.Ed7);
            yield return Tween.Wait(1f);
            yield return gm.Fader.FadeTo(new Color(1f, 0.95f, 0.85f), 1f, 2.5f);
        }

        IEnumerator Ed8()
        {
            var village=gm.GetArea("village").transform;
            var elder=village.Find("Villager");
            var lid=A<Grabbable>("village.box.lid");
            lid.ForceRelease();
            var box=lid.transform.parent;
            var boxPosition=box.position;
            gm.StartCoroutine(Tween.Environment(new Color(.95f,.7f,.48f), new Color(.7f,.58f,.48f),
                new Color(1f,.8f,.55f), 1.15f, new Color(.95f,.8f,.62f), .012f, 4f));
            yield return Say(new Line(null,"浦島太郎","この玉手箱を、預かってもらえませんか。開けずに、大切にしてください。"));
            if(elder!=null)
            {
                foreach(var grab in box.GetComponentsInChildren<Grabbable>()) grab.ForceRelease();
                foreach(var rb in box.GetComponentsInChildren<Rigidbody>()) rb.isKinematic=true;
                yield return Tween.Move(box,elder.position+Vector3.up*.82f-elder.forward*.26f,1.3f);
                Motes(box.position, new Color(1f,.83f,.45f), .45f, .08f, 35);
                var villager = elder.GetComponent<SimpleCharacter>();
                if (villager != null) yield return Bow(villager);
            }
            yield return Say(new Line(null,"村のおじさん","わかった。約束するよ。あんたも、この村で暮らしていきなさい。"),
                new Line(null,"語り","玉手箱を村のおじさんに託した浦島太郎は、変わり果てた村で、新しい暮らしを始めました。"));
            yield return gm.Fader.FadeTo(new Color(1,.91f,.75f),1,2f);
            box.position=boxPosition;
        }

        IEnumerator EscapeOtohime()
        {
            var area=gm.GetArea("ryugu").transform;
            yield return Say(new Line(null,"浦島太郎","今だ、逃げよう！"));
            yield return Reframe(area.TransformPoint(new Vector3(4,0,13.2f)),area.TransformPoint(new Vector3(4,1.3f,6.5f)));
            // Hide the seated player's displacement under black to avoid forced VR motion.
            var from=gm.Rig.transform.position;
            var to=from+area.TransformVector(new Vector3(0,0,-6));
            gm.PlaySfx(rumbleSound);
            yield return gm.Fader.FadeOut(.5f);
            gm.Rig.transform.position = to;
            yield return Tween.Wait(.8f);
            ClearEffects();
            gm.SwitchArea("beach",A("beach.invite.spawn"));
            Motes(gm.Rig.Head.position + gm.Rig.Head.forward * 3, new Color(.5f,.75f,.85f), 2.5f, .06f, 45);
            gm.Rig.LocomotionEnabled=true;
            yield return gm.Fader.FadeIn(1f);
            yield return Say(new Line(null,"語り","背後から乙姫の声が追いかけてきます。浦島太郎は振り返らず、亀の背に飛び乗りました。"),
                new Line(null,"語り","気がつくと、そこは懐かしい砂浜でした。波の向こうから、あの笑い声がかすかに聞こえます。"),
                new Line(null,"浦島太郎","もう、あの城へは戻らない。"));
            yield return gm.Fader.FadeOut(2f);
        }
    }
}
