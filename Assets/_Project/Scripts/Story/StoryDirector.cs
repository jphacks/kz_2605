using System;
using System.Collections;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>分岐①〜⑤に対応する章。「直前の選択肢に戻る」とオートセーブの単位。</summary>
    public enum Chapter { Beach, Invite, Ryugu, Return, Village }

    /// <summary>
    /// 物語の進行と分岐（02 ストーリー仕様書）。
    /// ① 浜辺 → ② 竜宮城への誘い → ③ 竜宮城 → ④ 帰還 → ⑤ 玉手箱。各分岐で行動に応じてエンディングへ。
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        [Tooltip("選択肢の制限時間（秒）")]
        public float choiceTime = 20f;
        [Tooltip("歩いて移動が必要な選択肢の制限時間（秒）")]
        public float walkChoiceTime = 30f;
        [Tooltip("配信中、視聴者の投票で決まるまでの時間（制限時間に対する割合）。投票が無ければ体験者の制限時間いっぱいまで待つ")]
        [Range(0.1f, 1f)] public float viewerTimeRatio = 0.5f;
        [Tooltip("亀に乗って海中を進む時間（秒）。台詞が長い場合は再生終了まで待つ")]
        [Min(0.1f)] public float rideDuration = 10f;

        GameManager gm => GameManager.Instance;
        NarrationPlayer narration => gm.narration;
        EndingDirector endings => gm.endings;

        IEnumerator Say(params Line[] lines) => narration.Say(lines);

        bool skipNextIntro;

        const int IntroLinesOnSkip = 2;

        /// <summary>章の導入ナレーション。選択肢から始める（チャプター選択・直前の選択肢に戻る）直後の1回だけは、最後の2つのセリフだけ流して選択肢へ進む。</summary>
        IEnumerator Intro(Line[] lines)
        {
            if (skipNextIntro)
            {
                // 選択肢から始めるときも、いきなり選択肢を出さずに直前の2つのセリフで状況を思い出させる
                skipNextIntro = false;
                yield return Tween.Wait(0.5f);
                yield return Say(lines[Mathf.Max(0, lines.Length - IntroLinesOnSkip)..]);
                yield break;
            }
            yield return Say(lines);
        }

        public IEnumerator Run(Chapter from, bool skipIntro = false)
        {
            skipNextIntro = skipIntro;
            switch (from)
            {
                case Chapter.Beach: yield return Beach(); break;
                case Chapter.Invite: yield return Invite(false); break;
                case Chapter.Ryugu: yield return Ryugu(false); break;
                case Chapter.Return: yield return Return(false); break;
                case Chapter.Village: yield return Village(); break;
            }
        }

        void SetCheckpoint(Chapter c)
        {
            gm.Checkpoint = c;
            SaveData.SaveCheckpoint(c);
        }

        // ── 選択肢 ───────────────────────────────────

        /// <summary>
        /// 選択肢を表示し、いずれかの行動が成立するまで待つ。時間切れなら-1。
        /// 台詞の途中で成立した場合は入力を保持し、台詞が終わってから次へ進む（03 機能仕様書）。
        /// 配信中は視聴者が投票でき、体験者が行動しないまま時間切れになったら投票1位で進む。
        /// </summary>
        public IEnumerator Choose(ChoiceDef def, Action<int> result)
        {
            var live = LiveStream.Instance;
            gm.Choice.Show(def);
            // 視聴者の投票は短めに締め切る（投票があればそこで決まる）。無ければ体験者の制限時間いっぱいまで延ばす
            float viewerTime = def.timeLimit * viewerTimeRatio;
            bool viewerTimeOver = false;
            if (live != null) live.OpenChoice(def, viewerTime);
            foreach (var o in def.options) foreach (var t in o.triggers) if (t != null) t.Arm();

            int chosen = -1;
            float remaining = def.timeLimit;
            while (remaining > 0 && chosen < 0)
            {
                if (gm.Choice.SelectedIndex >= 0) chosen = gm.Choice.SelectedIndex;
                for (int i = 0; i < def.options.Length && chosen < 0; i++)
                    foreach (var t in def.options[i].triggers)
                        if (t != null && t.Fired) { chosen = i; break; }

                int key = GameInput.DebugNumberKeyDown();
                if (chosen < 0 && key >= 0 && key < def.options.Length) chosen = key;
                if (chosen >= 0) break;

                gm.Choice.SetRemaining(remaining / def.timeLimit, remaining);
                if (live != null)
                {
                    float viewerRemaining = viewerTime - (def.timeLimit - remaining);
                    if (!viewerTimeOver && viewerRemaining <= 0)
                    {
                        viewerTimeOver = true;
                        if (live.TopVote() >= 0) break;
                        live.ExtendChoice(remaining);
                    }
                    live.UpdateRemaining(viewerTimeOver ? remaining : viewerRemaining);
                    gm.Choice.SetVotes(live.Votes, live.TotalVotes);
                }
                yield return null;
                remaining -= Time.deltaTime;
            }

            string decidedBy = chosen >= 0 ? "player" : "timeout";
            if (chosen < 0 && live != null && live.TopVote() >= 0)
            {
                chosen = live.TopVote();
                decidedBy = "viewers";
                gm.Choice.ShowDecidedByViewers();
            }
            if (chosen < 0)
            {
                chosen = 0; // 各分岐の先頭が本線。無操作でも物語を続ける。
                gm.Choice.ShowAutoContinue();
            }
            if (live != null) live.CloseChoice(chosen, decidedBy);

            foreach (var o in def.options) foreach (var t in o.triggers) if (t != null) t.Disarm();
            if (chosen >= 0)
            {
                gm.Choice.Highlight(chosen);
                while (narration.IsSpeaking) yield return null;
                yield return Tween.Wait(0.7f);
            }
            gm.Choice.Hide();
            result(chosen);
        }

        static T A<T>(string id) where T : Component => StoryAnchor.Get<T>(id);
        static Transform A(string id) => StoryAnchor.Get(id);

        // ── ① 浜辺：亀がいたずらされている ────────────────

        IEnumerator Beach()
        {
            SetCheckpoint(Chapter.Beach);
            // 一緒にいたずらするには、浜辺の木の棒を拾って亀を叩く（導入の間に叩いても音は鳴る）
            var turtleHit = A<TouchTrigger>("beach.turtle.hit");
            turtleHit.requireStick = true;
            yield return gm.GoToArea("beach");
            yield return Intro(StoryScript.BeachIntro);

            int r = -1;
            yield return Choose(new ChoiceDef
            {
                question = "亀がいたずらされている。どうする？",
                timeLimit = walkChoiceTime,
                options = new[]
                {
                    new ChoiceDef.Option("助ける", "子どもたちを\n手で追い払う",
                        A<TouchTrigger>("beach.child.0"), A<TouchTrigger>("beach.child.1"), A<TouchTrigger>("beach.child.2")),
                    new ChoiceDef.Option("見過ごす", "亀から離れて\n立ち去る", A<LeaveTrigger>("beach.leave")),
                    new ChoiceDef.Option("一緒にいたずらする", "木の棒で\n亀を叩く", turtleHit),
                },
            }, v => r = v);

            switch (r)
            {
                case 0:
                    FleeBeachChildren(3.5f);
                    yield return Say(StoryScript.BeachHelp[0], StoryScript.BeachHelp[1], StoryScript.BeachHelp[2]);
                    var rescuedTurtle = A<SimpleCharacter>("beach.turtle");
                    rescuedTurtle.lookAtPlayer = false;
                    rescuedTurtle.lookTarget = null;
                    rescuedTurtle.StartMoveTo(A("beach.sea.point").position, 1.6f);
                    yield return Say(StoryScript.BeachHelp[3]);
                    yield return Invite(true);
                    break;
                case 1: yield return endings.Play(EndingId.ED1); break;
                case 2: yield return endings.Play(EndingId.ED2); break;
                default: yield return gm.TimeUp(); break;
            }
        }

        /// <summary>子ども全員が浜辺の陸側にある共通の退避場所へ走る。到着時の重なりだけ避ける。</summary>
        public void FleeBeachChildren(float speed)
        {
            var beach = A("beach.turtle").parent;
            for (int i = 0; i < 3; i++)
            {
                var destination = beach.TransformPoint(new Vector3(-12f + i * .6f, 0, -6f));
                A<SimpleCharacter>("beach.child." + i).FleeTo(destination, speed);
            }
        }

        // ── ② 亀から竜宮城への誘い ──────────────────────

        IEnumerator Invite(bool continuing)
        {
            SetCheckpoint(Chapter.Invite);
            yield return StageInvitation(continuing);
            var turtle = A<SimpleCharacter>("beach.turtle");
            var beach = gm.GetArea("beach").transform;
            var arrival = gm.StartCoroutine(turtle.MoveTo(beach.TransformPoint(new Vector3(2.3f, 0, 7.5f)), .8f));
            yield return Intro(StoryScript.InviteIntro);
            yield return arrival;
            turtle.lookAtPlayer = true;

            int r = -1;
            yield return Choose(new ChoiceDef
            {
                question = "亀が竜宮城へ誘っている。",
                timeLimit = choiceTime,
                options = new[]
                {
                    new ChoiceDef.Option("行く", "このボタンを指して\nトリガーで決定") { button = true },
                    new ChoiceDef.Option("行かない", "首を横に振る", A<HeadShakeTrigger>("beach.headshake")),
                },
            }, v => r = v);

            switch (r)
            {
                case 0:
                    yield return Say(StoryScript.InviteGo);
                    yield return RideUndersea();
                    yield return Ryugu(true);
                    break;
                case 1: yield return endings.Play(EndingId.ED3); break;
                default: yield return gm.TimeUp(); break;
            }
        }

        /// <summary>救出当日と区別できる、数日後の朝の釣り場へ切り替える。</summary>
        public IEnumerator StageInvitation(bool showTimeCard)
        {
            gm.rig.LocomotionEnabled = false;
            yield return gm.Fader.FadeOut(.8f);
            if (showTimeCard) yield return gm.Fader.ShowTimeCard("数日後", "朝の浜辺");
            gm.SwitchArea("beach", A("beach.invite.spawn"));
            for (int i = 0; i < 3; i++) A("beach.child." + i).gameObject.SetActive(false);
            InvitationBeachScene.Prepare(gm);
            yield return gm.Fader.FadeIn(1f);
            gm.rig.LocomotionEnabled = true;
        }

        /// <summary>亀に乗って海中を自動移動。まっすぐ一定速度で進み、回転はさせない（VR酔い対策）。</summary>
        IEnumerator RideUndersea()
        {
            var ride = A("sea.ride");
            var end = A("sea.end");
            gm.rig.LocomotionEnabled = false;
            yield return gm.Fader.FadeOut(0.8f);
            gm.SwitchArea("sea", A("sea.seat"));
            gm.rig.RideAnchor = A("sea.seat");
            yield return gm.Fader.FadeIn(1.0f);

            var start = ride.position;
            float t = 0;
            bool narrationDone = false;
            gm.StartCoroutine(SayThen(StoryScript.Undersea, () => narrationDone = true));
            while (t < rideDuration || !narrationDone)
            {
                t += Time.deltaTime;
                ride.position = Vector3.Lerp(start, end.position, Mathf.Clamp01(t / rideDuration));
                yield return null;
            }
            yield return gm.Fader.FadeOut(1.0f);
            gm.rig.RideAnchor = null;
            gm.rig.LocomotionEnabled = true;
        }

        IEnumerator SayThen(Line[] lines, Action done)
        {
            yield return Say(lines);
            done();
        }

        // ── ③ 竜宮城：おもてなし or 探索 ───────────────

        IEnumerator Ryugu(bool continuing)
        {
            SetCheckpoint(Chapter.Ryugu);
            yield return gm.GoToArea("ryugu");
            yield return Intro(StoryScript.RyuguIntro);

            int r = -1;
            yield return Choose(new ChoiceDef
            {
                question = "竜宮城でどう過ごす？",
                timeLimit = walkChoiceTime,
                options = new[]
                {
                    new ChoiceDef.Option("おもてなしを楽しむ", "料理を手に取って\n口に運ぶ", A<BringToHeadTrigger>("ryugu.eat")),
                    new ChoiceDef.Option("城内を探索する", "宴会場の奥の\n通路へ進む", A<ZoneTrigger>("ryugu.corridor.enter")),
                },
            }, v => r = v);

            switch (r)
            {
                case 0:
                    yield return Say(StoryScript.RyuguEnjoy);
                    yield return Return(true);
                    break;
                case 1: yield return endings.Play(EndingId.ED4); break;   // 通常ルートには戻らない
                default: yield return gm.TimeUp(); break;
            }
        }

        // ── ④ 竜宮城からの帰還 ─────────────────────────

        IEnumerator Return(bool continuing)
        {
            SetCheckpoint(Chapter.Return);
            // 時間が経過：暗転してから宴会場に戻す
            yield return gm.GoToArea("ryugu", A("ryugu.return.spawn"), continuing ? 1.2f : 0.6f,
                () => A("ryugu.turtle").gameObject.SetActive(true));
            yield return Intro(StoryScript.ReturnIntro);

            int r = -1;
            yield return Choose(new ChoiceDef
            {
                question = "故郷へ帰る？",
                timeLimit = choiceTime,
                options = new[]
                {
                    new ChoiceDef.Option("帰る", "このボタンを指して\nトリガーで決定") { button = true },
                    new ChoiceDef.Option("帰らない", "乙姫の手を取る", A<TouchTrigger>("ryugu.otohime.touch")),
                },
            }, v => r = v);

            switch (r)
            {
                case 0:
                    var gift = A("ryugu.gift");
                    gift.gameObject.SetActive(true);
                    yield return Say(StoryScript.ReturnGo);
                    yield return Village();
                    break;
                case 1: yield return endings.Play(EndingId.ED5); break;
                default: yield return gm.TimeUp(); break;
            }
        }

        // ── ⑤ 村に戻って玉手箱 ─────────────────────────

        IEnumerator Village()
        {
            SetCheckpoint(Chapter.Village);
            yield return gm.GoToArea("village", null, 1.2f);
            yield return Intro(StoryScript.VillageIntro);

            int r = -1;
            yield return Choose(new ChoiceDef
            {
                question = "玉手箱をどうする？",
                timeLimit = walkChoiceTime,
                options = new[]
                {
                    new ChoiceDef.Option("開ける", "ふたを持ち上げる", A<LidOpenTrigger>("village.box.open")),
                    new ChoiceDef.Option("放置する", "このボタンで選ぶ\nまたは村の道を歩く", A<ZoneTrigger>("village.path.end")) { button = true },
                    new ChoiceDef.Option("おじさんにあげる", "このボタンを指して\nトリガーで決定") { button = true },
                },
            }, v => r = v);

            switch (r)
            {
                case 0: yield return endings.Play(EndingId.ED6); break;
                case 1: yield return endings.Play(EndingId.ED7); break;
                case 2: yield return endings.Play(EndingId.ED8); break;
                default: yield return gm.TimeUp(); break;
            }
        }
    }
}
