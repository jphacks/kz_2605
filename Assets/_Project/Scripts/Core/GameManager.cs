using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace JPHACKS2026
{
    public enum GameState { Boot, Title, Story, Menu }

    /// <summary>
    /// ゲーム全体の司令塔。タイトル → 絵本導入 → 物語 → エンディング → 「おわり」 → タイトル の流れを管理する。
    /// 物語の進行（コルーチン）はすべてこのコンポーネント上で動かし、流れを切り替えるときは一括で止める。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        public PlayerRig rig;
        public Light sun;
        public NarrationPlayer narration;
        public StoryDirector story;
        public EndingDirector endings;
        public TMP_FontAsset font;
        public AudioSource sfxSource;

        public PlayerRig Rig => rig;
        public Area CurrentArea { get; private set; }
        public GameState State { get; private set; } = GameState.Boot;
        public bool IsPaused { get; private set; }
        public Chapter Checkpoint { get; set; }

        public ScreenFader Fader { get; private set; }
        public SubtitleView Subtitle { get; private set; }
        public ChoicePanel Choice { get; private set; }
        public MenuPanel Menu { get; private set; }
        public MenuPanel PauseMenu { get; private set; }

        readonly Dictionary<string, Area> areas = new();
        TextMeshProUGUI hudText;
        GameObject hud;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 72;
            GameInput.Init();
            UIFactory.Font = font;
            StoryAnchor.RebuildRegistry();

            foreach (var a in FindObjectsByType<Area>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                areas[a.areaId] = a;
                a.Init();
                a.gameObject.SetActive(false);
            }

            Fader = ScreenFader.Create(rig.head);
            Subtitle = SubtitleView.Create();
            Choice = ChoicePanel.Create();
            Menu = MenuPanel.Create("Menu");
            PauseMenu = MenuPanel.Create("PauseMenu", 50);
            narration.Bind(Subtitle);
            // 配信（LiveStreamConfig が設定されているときだけ）：Webの視聴ページとつなぎ、コメントを弾幕で流す
            if (!Application.isBatchMode && LiveStream.Create(rig.head) is { } live) DanmakuView.Create(live);
            CreateTitleHud();
        }

        void Start() => GoToTitle();

        void Update()
        {
            if (GameInput.Menu.WasPressedThisFrame())
            {
                if (IsPaused) Resume();
                else if (State == GameState.Story) Pause();
            }
        }

        // ── 流れの切り替え ────────────────────────────

        public void RunFlow(IEnumerator flow)
        {
            StopAllCoroutines();
            Cleanup();
            StartCoroutine(flow);
        }

        void Cleanup()
        {
            if (endings != null) endings.ClearEffects();
            if (IsPaused)
            {
                Time.timeScale = 1;
                AudioListener.pause = false;
                IsPaused = false;
            }
            adjustingEye = false;
            PauseMenu.Hide();
            Menu.Hide();
            Choice.Hide();
            narration.Stop();
            rig.RideAnchor = null;
            rig.LocomotionEnabled = true;
            rig.SetAgedHands(false);
            foreach (var h in rig.Hands) h.GetComponent<HandGrabber>().ReleaseHeld(false);
        }

        public void GoToTitle() => RunFlow(TitleFlow());
        public void BeginStory(Chapter from) => RunFlow(StoryFlow(from, false));
        /// <summary>直前の選択肢から再開。章の導入ナレーションは聞き直さずに選択肢へ進む。</summary>
        public void RetryCheckpoint() => RunFlow(StoryFlow(Checkpoint, true));
        public void RestartStory() => BeginStory(Chapter.Beach);
        /// <summary>タイトルのチャプター選択：その章の選択肢から始める（導入ナレーションは省略）。</summary>
        public void JumpToChapter(Chapter c) => RunFlow(StoryFlow(c, true));

        static readonly (Chapter chapter, string label)[] ChapterMenu =
        {
            (Chapter.Beach, "① 浜辺"),
            (Chapter.Invite, "② 亀の誘い"),
            (Chapter.Ryugu, "③ 竜宮城"),
            (Chapter.Return, "④ 帰還"),
            (Chapter.Village, "⑤ 玉手箱"),
        };

        IEnumerator StoryFlow(Chapter from, bool skipIntro)
        {
            State = GameState.Story;
            if (LiveStream.Instance != null)
            {
                LiveStream.Instance.ClearEnding();
                LiveStream.Instance.PublishPhase("story");
            }
            yield return story.Run(from, skipIntro);
        }

        public Area GetArea(string id)
        {
            if (!areas.TryGetValue(id, out var a)) Debug.LogError($"[GameManager] エリアがありません: {id}");
            return a;
        }

        /// <summary>暗転 → エリアを初期化して切り替え → 出現地点へ移動 → 明転。</summary>
        public IEnumerator GoToArea(string id, Transform spawn = null, float fade = 0.6f, System.Action afterSwitch = null)
        {
            if (Fader.Alpha < 0.99f) yield return Fader.FadeOut(fade);
            // 光に包まれて来たときは、光がほどけるようにゆっくり場面を見せる
            if (Fader.IsWhiteOut) fade = Mathf.Max(fade, 1.8f);
            SwitchArea(id, spawn);
            afterSwitch?.Invoke();
            yield return null;
            yield return Fader.FadeIn(fade);
        }

        public void SwitchArea(string id, Transform spawn = null)
        {
            var area = GetArea(id);
            if (CurrentArea != null && CurrentArea != area) CurrentArea.gameObject.SetActive(false);
            area.ResetArea();
            area.gameObject.SetActive(true);
            CurrentArea = area;
            area.ApplyEnvironment();
            if (LiveStream.Instance != null) LiveStream.Instance.PublishScene(id);
            rig.TeleportTo(spawn != null ? spawn : area.spawnPoint);
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
        }

        // ── タイトル ─────────────────────────────────

        IEnumerator TitleFlow()
        {
            State = GameState.Title;
            if (LiveStream.Instance != null)
            {
                LiveStream.Instance.ClearEnding();
                LiveStream.Instance.PublishPhase("title");
            }
            SwitchArea("title");
            var book = StoryAnchor.Get<Picturebook>("title.book");
            book.SetOpenImmediate(false);
            ShowHud(true);
            yield return Fader.FadeIn(0.8f);

            book.openTrigger.Arm();
            while (!book.openTrigger.Fired && GameInput.DebugNumberKeyDown() < 0) yield return null;
            book.openTrigger.Disarm();
            ShowHud(false);
            yield return book.Open();

            System.Action start = null;
            var items = new List<(string, System.Action)>
            {
                ("物語を始める", () => start = () => BeginStory(Chapter.Beach)),
            };
            if (SaveData.TryGetCheckpoint(out var saved)) items.Add(("続きから", () => start = () => BeginStory(saved)));
            foreach (var (chapter, label) in ChapterMenu)
                items.Add((label, () => start = () => JumpToChapter(chapter)));
            var menuAnchor = StoryAnchor.Get("title.menu");
            void ShowTitleMenu()
            {
                Menu.Show("童話配信", "絵本の世界へ入ります\n<size=80%>チャプターを選ぶと、その選択肢から始まります</size>", items.ToArray());
                Menu.PlaceAt(menuAnchor);
            }
            items.Add(("目線調節", () => { ShowEyeMenu(Menu, ShowTitleMenu); Menu.PlaceAt(menuAnchor); }));
            ShowTitleMenu();
            while (start == null)
            {
                int k = GameInput.DebugNumberKeyDown();
                if (k >= 0 && k < items.Count && !adjustingEye) items[k].Item2();
                yield return null;
            }
            Menu.Hide();

            // 絵本が光り、本の中に吸い込まれる：ページが灯る → 光の筋と粒 → 光に包まれて白く溶ける
            StartCoroutine(book.GlowUp(3.4f));
            yield return Tween.Wait(2.3f);
            yield return Fader.FadeTo(new Color(1f, 0.97f, 0.9f), 1, 1.6f);
            yield return Tween.Wait(0.3f);
            book.SetGlow(0);

            start();
        }

        void CreateTitleHud()
        {
            var anchor = StoryAnchor.Get("title.hud");
            var canvas = UIFactory.CreateCanvas("TitleHud", new Vector2(1400, 700), anchor);
            canvas.transform.localPosition = Vector3.zero;
            canvas.transform.localRotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one * 0.0015f;
            hud = canvas.gameObject;
            // 窓の格子や月に重なっても読めるよう、文字の後ろに板を敷き、縁取りをつける
            UIFactory.CreateImage(canvas.transform, "Backdrop", UIFactory.PanelColor, new Vector2(1300, 640));
            UIFactory.AddOutline(UIFactory.CreateText(canvas.transform, "Title", "童話配信", 150, UIFactory.PaperColor, new Vector2(1300, 220), new Vector2(0, 170)), 0.2f);
            UIFactory.AddOutline(UIFactory.CreateText(canvas.transform, "Sub", "〜 え！本当に… 絵本の世界へ 〜", 58, new Color(1, 0.88f, 0.6f), new Vector2(1300, 90), new Vector2(0, 75)), 0.15f);
            UIFactory.AddOutline(UIFactory.CreateText(canvas.transform, "Guide", "目の前の絵本に手をのばして\n人差し指のトリガーで開こう", 54, Color.white, new Vector2(1300, 160), new Vector2(0, -90)), 0.15f);
            hudText = UIFactory.CreateText(canvas.transform, "Count", "", 40, new Color(1, 1, 1, 0.85f), new Vector2(1240, 110), new Vector2(0, -250));
        }

        void ShowHud(bool show)
        {
            hud.SetActive(show);
            hudText.text = $"達成エンディング {SaveData.EndingCount} / {EndingCatalog.Count}";
            if (LiveStream.Instance != null && LiveStream.Instance.Config.ViewerUrl.Length > 0)
                hudText.text = $"スマホで見る：{LiveStream.Instance.Config.ViewerUrl}\n" + hudText.text;
        }

        // ── エンディング後 ───────────────────────────

        /// <summary>絵本が閉じて「おわり」。達成数を表示し、タイトルか直前の選択肢へ戻る。</summary>
        public IEnumerator Owari(EndingId id, bool compact = false)
        {
            State = GameState.Menu;
            if (LiveStream.Instance != null)
            {
                LiveStream.Instance.PublishEnding(id);
                LiveStream.Instance.PublishPhase("ending");
            }
            narration.Stop();
            // 物語の世界が光にほどけ、絵本の中から部屋へ戻される（入るときの逆）：
            // 光に包まれる → 開いた絵本が強く光った状態で部屋が現れる → 光がページへ吸い込まれていく
            yield return Fader.FadeTo(new Color(1f, 0.97f, 0.9f), 1, compact ? .65f : 1.8f);
            rig.RideAnchor = null;
            rig.SetAgedHands(false);
            SwitchArea("title");
            ShowHud(false);
            var book = StoryAnchor.Get<Picturebook>("title.book");
            book.SetOpenImmediate(true);
            book.SetGlow(1f);
            if (!compact) yield return Tween.Wait(0.6f);
            StartCoroutine(book.GlowTo(0.2f, compact ? 1.4f : 3.0f));   // 光が引いて、物語を終えたページがほのかに光る
            yield return Fader.FadeIn(compact ? 1.2f : 2.2f);
            if (compact) yield return Tween.Wait(.4f);
            if (!compact)
            {
                yield return Tween.Wait(0.8f);
                yield return Medetashi();
            }
            StartCoroutine(book.GlowTo(0f, compact ? .9f : 1.2f));
            yield return book.Close(compact ? 1.1f : 1.4f);

            bool isNew = SaveData.RecordEnding(id);
            SaveData.ClearCheckpoint();
            string body = $"<size=130%><color=#FFE6B0>『{EndingCatalog.Title(id)}』</color></size>"
                        + (isNew ? "  <color=#FFD27A><size=80%>NEW!</size></color>" : "") + "\n"
                        + $"<size=80%>{id}　{EndingCatalog.Genre(id)}　／　達成エンディング {SaveData.EndingCount} / {EndingCatalog.Count}</size>";
            Menu.Show("おわり", body,
                ("タイトルに戻る", GoToTitle),
                ("直前の選択肢に戻る", RetryCheckpoint));
            Menu.PlaceAt(StoryAnchor.Get("title.menu"));
            while (true)
            {
                int k = GameInput.DebugNumberKeyDown();
                if (k == 0) { GoToTitle(); yield break; }
                if (k == 1) { RetryCheckpoint(); yield break; }
                yield return null;
            }
        }

        /// <summary>開いた絵本の上に「めでたし めでたし」を浮かべ、読み上げてから消す。</summary>
        IEnumerator Medetashi()
        {
            var canvas = UIFactory.CreateCanvas("Medetashi", new Vector2(1300, 260), StoryAnchor.Get("title.menu"));
            canvas.transform.localPosition = new Vector3(0, -0.05f, -0.2f);
            canvas.transform.localRotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one * 0.0012f;
            var text = UIFactory.CreateText(canvas.transform, "Text", "めでたし　めでたし", 120, UIFactory.PaperColor, new Vector2(1300, 220));
            text.textWrappingMode = TextWrappingModes.NoWrap;   // 「めでたし めでた／し」のように途中で折り返さない
            UIFactory.AddOutline(text, 0.25f);
            text.alpha = 0;
            yield return Tween.Run(1.0f, t => text.alpha = t);
            yield return narration.Say(StoryScript.Medetashi);
            yield return Tween.Wait(0.6f);
            yield return Tween.Run(0.8f, t => text.alpha = 1 - t);
            Destroy(canvas.gameObject);
        }

        /// <summary>選択肢で時間切れになったとき（ゲームオーバー）。エンディング数には数えない。</summary>
        public IEnumerator TimeUp()
        {
            yield return narration.Say(StoryScript.TimeUp);
            State = GameState.Menu;
            Menu.Show("時間切れ", "物語は進みませんでした……", ("直前の選択肢に戻る", RetryCheckpoint), ("タイトルに戻る", GoToTitle));
            while (true)
            {
                int k = GameInput.DebugNumberKeyDown();
                if (k == 0) { RetryCheckpoint(); yield break; }
                if (k == 1) { GoToTitle(); yield break; }
                yield return null;
            }
        }

        // ── ポーズ ───────────────────────────────────

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0;
            AudioListener.pause = true;
            ShowPauseMenu();
        }

        void ShowPauseMenu()
        {
            PauseMenu.Show("ポーズ", "", ("再開", Resume), ("目線調節", () => ShowEyeMenu(PauseMenu, ShowPauseMenu)),
                ("最初からやり直す", RestartStory), ("タイトルに戻る", GoToTitle));
        }

        // ── 目線調節 ─────────────────────────────────

        bool adjustingEye;

        /// <summary>メニューを目線調節の画面に切り替える。「戻る」で back を呼ぶ。ボタンはレイで指してトリガーで押す。</summary>
        void ShowEyeMenu(MenuPanel panel, System.Action back)
        {
            adjustingEye = true;
            void Apply(System.Action change)
            {
                change();
                panel.SetBody(EyeMenuBody());
                panel.Resnap();
            }
            panel.Show("目線調節", EyeMenuBody(),
                ("目線を上げる", () => Apply(rig.RaiseEye)),
                ("目線を下げる", () => Apply(rig.LowerEye)),
                ("いまの高さに合わせる", () => Apply(rig.CalibrateEye)),
                ("元に戻す", () => Apply(rig.ResetEye)),
                ("戻る", () => { adjustingEye = false; back(); }));
        }

        string EyeMenuBody()
        {
            int cm = Mathf.RoundToInt(rig.EyeHeightOffset * 100);
            return $"いまの補正：{(cm > 0 ? "+" : "")}{cm} cm\n<size=80%>座って遊ぶときは「いまの高さに合わせる」</size>";
        }

        public void Resume()
        {
            IsPaused = false;
            adjustingEye = false;
            Time.timeScale = 1;
            AudioListener.pause = false;
            PauseMenu.Hide();
        }
    }
}
