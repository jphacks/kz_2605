using System.Collections;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// ED1〜7の演出（仮素材版）。演出が終わったら絵本が閉じて「おわり」へ進む。
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

        public IEnumerator Play(EndingId id)
        {
            switch (id)
            {
                case EndingId.ED1: yield return Ed1(); break;
                case EndingId.ED2: yield return Ed2(); break;
                case EndingId.ED3: yield return Ed3(); break;
                case EndingId.ED4: yield return Ed4(); break;
                case EndingId.ED5: yield return Ed5(); break;
                case EndingId.ED6: yield return Ed6(); break;
                case EndingId.ED7: yield return Ed7(); break;
            }
            yield return gm.Owari(id);
        }

        /// <summary>ED1：見過ごす。物語が始まらないコメディ。</summary>
        IEnumerator Ed1()
        {
            var turtle = A<SimpleCharacter>("beach.turtle");
            turtle.lookAtPlayer = true;
            yield return Say(StoryScript.Ed1[0], StoryScript.Ed1[1]);
            // 色あせていく＝物語が始まらない
            gm.StartCoroutine(Tween.Environment(new Color(0.75f, 0.72f, 0.65f), new Color(0.55f, 0.53f, 0.5f),
                new Color(0.9f, 0.85f, 0.75f), 0.6f, new Color(0.8f, 0.78f, 0.72f), 0.03f, 4f));
            yield return Say(StoryScript.Ed1[2], StoryScript.Ed1[3]);
            yield return Tween.Wait(1f);
        }

        /// <summary>ED2：一緒にいじめる。巨大な亀が報復し、海に引きずり込むホラー。</summary>
        IEnumerator Ed2()
        {
            for (int i = 0; i < 3; i++)
            {
                var c = A<SimpleCharacter>("beach.child." + i);
                c.idle = SimpleCharacter.Idle.Hop;
                c.idleSpeed = 14f;
            }
            yield return Say(StoryScript.Ed2a);

            gm.PlaySfx(rumbleSound);
            for (int i = 0; i < 3; i++)
            {
                var c = A<SimpleCharacter>("beach.child." + i);
                c.Flee(A("beach.turtle").position, 14f, 4f);
            }
            var turtle = A("beach.turtle");
            var tc = turtle.GetComponent<SimpleCharacter>();
            tc.lookAtPlayer = true;
            gm.StartCoroutine(Tween.Environment(new Color(0.15f, 0.02f, 0.03f), new Color(0.25f, 0.05f, 0.05f),
                new Color(0.8f, 0.15f, 0.1f), 0.5f, new Color(0.12f, 0.02f, 0.03f), 0.05f, 4f));
            yield return Tween.Scale(turtle, turtle.localScale * 6f, 4f);
            Tween.Tint(turtle.gameObject, new Color(0.45f, 0.4f, 0.4f));
            yield return Say(StoryScript.Ed2b[0]);

            // 亀が迫り、暗い海の色に沈む
            var head = gm.Rig.Head.position;
            var dir = (head - turtle.position); dir.y = 0;
            tc.StartMoveTo(turtle.position + dir * 0.6f, 2.5f);
            yield return gm.Fader.FadeTo(new Color(0f, 0.05f, 0.18f), 1f, 2.5f);
            yield return Say(StoryScript.Ed2b[1], StoryScript.Ed2b[2]);
        }

        /// <summary>ED3：行かない。亀が「ここまで準備したのに！」と嘆くコメディ。</summary>
        IEnumerator Ed3()
        {
            var turtle = A("beach.turtle");
            var body = turtle.GetComponent<SimpleCharacter>().body;
            yield return Say(StoryScript.Ed3[0], StoryScript.Ed3[1]);
            // ひっくり返ってすねる
            yield return Tween.Rotate(body, Quaternion.Euler(0, 0, 180), 0.8f);
            yield return Tween.Wait(0.8f);
            yield return Tween.Rotate(body, Quaternion.identity, 0.6f);
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
            var scroll = A<GrabTrigger>("ryugu.scroll.grab");
            scroll.Arm();
            t = 0;
            while (!scroll.Fired && t < 25f && GameInput.DebugNumberKeyDown() < 0) { t += Time.deltaTime; yield return null; }
            scroll.Disarm();

            yield return Say(StoryScript.Ed4Scroll[0], StoryScript.Ed4Scroll[1]);

            // 振り返ると乙姫が立っている
            var oto = A("ryugu.otohime");
            var head = gm.Rig.Head;
            var back = -head.forward; back.y = 0; back.Normalize();
            var p = head.position + back * 1.3f;
            p.y = oto.position.y;
            oto.position = p;
            var oc = oto.GetComponent<SimpleCharacter>();
            oc.lookAtPlayer = true;
            oc.idle = SimpleCharacter.Idle.None;
            Tween.Tint(oto.gameObject, new Color(0.35f, 0.3f, 0.4f));
            gm.PlaySfx(rumbleSound);
            yield return Say(StoryScript.Ed4Scroll[2]);

            // 廃墟化
            gm.StartCoroutine(Tween.Environment(new Color(0.02f, 0.01f, 0.02f), new Color(0.18f, 0.08f, 0.08f),
                new Color(0.5f, 0.1f, 0.1f), 0.3f, new Color(0.03f, 0.01f, 0.02f), 0.08f, 3f));
            Tween.Tint(ryugu.gameObject, new Color(0.4f, 0.38f, 0.36f));
            foreach (var s in ryugu.GetComponentsInChildren<Swimmer>()) s.gameObject.SetActive(false);
            yield return Say(StoryScript.Ed4End);
            yield return gm.Fader.FadeOut(2f);
        }

        /// <summary>ED5：帰らない。宴が繰り返され、永遠に閉じ込められるホラー／ミステリー。</summary>
        IEnumerator Ed5()
        {
            var ryugu = gm.GetArea("ryugu");
            var swimmers = ryugu.GetComponentsInChildren<Swimmer>();
            yield return Say(StoryScript.Ed5);

            Line[][] loops = { StoryScript.Ed5Loop1, StoryScript.Ed5Loop2, StoryScript.Ed5Loop3 };
            Color[] skies = { new(0.35f, 0.1f, 0.2f), new(0.2f, 0.05f, 0.2f), new(0.08f, 0.02f, 0.08f) };
            for (int i = 0; i < loops.Length; i++)
            {
                foreach (var s in swimmers) s.SpeedMultiplier = 2f + i * 2.5f;
                var sky = skies[i];
                gm.StartCoroutine(Tween.Environment(sky, sky * 1.6f + new Color(0.1f, 0.05f, 0.05f), new Color(1f, 0.5f - i * 0.15f, 0.4f), 0.9f - i * 0.2f, sky, 0.03f + i * 0.02f, 1.5f));
                yield return gm.Fader.FadeTo(Color.black, 0.85f, 0.4f);
                yield return gm.Fader.FadeIn(0.4f);
                yield return Say(loops[i]);
            }
            var oto = A<SimpleCharacter>("ryugu.otohime");
            oto.lookAtPlayer = true;
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
            gm.StartCoroutine(Tween.Environment(new Color(0.9f, 0.9f, 0.92f), new Color(0.85f, 0.85f, 0.88f),
                Color.white, 1.0f, new Color(0.92f, 0.92f, 0.94f), 0.25f, 5f));
            yield return Say(StoryScript.Ed6[0]);
            gm.Rig.SetAgedHands(true);
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
            gm.StartCoroutine(Tween.Run(5f, k => sun.rotation = Quaternion.Slerp(sun.rotation, Quaternion.Euler(12, 180, 0), k * 0.1f)));
            yield return Say(StoryScript.Ed7);
            yield return Tween.Wait(1f);
            yield return gm.Fader.FadeTo(new Color(1f, 0.95f, 0.85f), 1f, 2.5f);
        }
    }
}
