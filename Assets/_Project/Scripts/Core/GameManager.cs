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
            if (IsPaused)
            {
                Time.timeScale = 1;
                AudioListener.pause = false;
                IsPaused = false;
            }
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
            Menu.Show("おとぎ配信", "絵本の世界へ入ります\n<size=80%>チャプターを選ぶと、その選択肢から始まります</size>", items.ToArray());
            Menu.PlaceAt(StoryAnchor.Get("title.menu"));
            while (start == null)
            {
                int k = GameInput.DebugNumberKeyDown();
                if (k >= 0 && k < items.Count) items[k].Item2();
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
            UIFactory.CreateImage(canvas.transform, "Line", new Color(0.95f, 0.75f, 0.3f, 0.8f), new Vector2(900, 4), new Vector2(0, 30));
            UIFactory.AddOutline(UIFactory.CreateText(canvas.transform, "Title", "おとぎ配信", 150, UIFactory.PaperColor, new Vector2(1300, 220), new Vector2(0, 170)), 0.2f);
            UIFactory.AddOutline(UIFactory.CreateText(canvas.transform, "Sub", "〜 え！本当に… 絵本の世界へ 〜", 58, new Color(1, 0.88f, 0.6f), new Vector2(1300, 90), new Vector2(0, 75)), 0.15f);
            UIFactory.AddOutline(UIFactory.CreateText(canvas.transform, "Guide", "目の前の絵本に手をのばして\nグリップボタンで開こう", 54, Color.white, new Vector2(1300, 160), new Vector2(0, -90)), 0.15f);
            hudText = UIFactory.CreateText(canvas.transform, "Count", "", 40, new Color(1, 1, 1, 0.85f), new Vector2(560, 60), new Vector2(330, -270), TextAlignmentOptions.Right);
        }

        void ShowHud(bool show)
        {
            hud.SetActive(show);
            hudText.text = $"達成エンディング {SaveData.EndingCount} / {EndingCatalog.Count}";
        }

        // ── エンディング後 ───────────────────────────

        /// <summary>絵本が閉じて「おわり」。達成数を表示し、タイトルか直前の選択肢へ戻る。</summary>
        public IEnumerator Owari(EndingId id)
        {
            State = GameState.Menu;
            narration.Stop();
            if (Fader.Alpha < 0.99f) yield return Fader.FadeOut(1.0f);
            rig.RideAnchor = null;
            rig.SetAgedHands(false);
            SwitchArea("title");
            ShowHud(false);
            var book = StoryAnchor.Get<Picturebook>("title.book");
            book.SetOpenImmediate(true);
            yield return Tween.Wait(0.3f);
            StartCoroutine(book.GlowTo(0.2f, 1.0f));   // 物語を終えたページがほのかに光る
            yield return Fader.FadeIn(1.0f);
            yield return Medetashi();
            StartCoroutine(book.GlowTo(0f, 1.2f));
            yield return book.Close();

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
            var canvas = UIFactory.CreateCanvas("Medetashi", new Vector2(1000, 260), StoryAnchor.Get("title.menu"));
            canvas.transform.localPosition = new Vector3(0, -0.05f, -0.2f);
            canvas.transform.localRotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one * 0.0012f;
            var text = UIFactory.CreateText(canvas.transform, "Text", "めでたし　めでたし", 120, UIFactory.PaperColor, new Vector2(1000, 220));
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
            PauseMenu.Show("ポーズ", "", ("再開", Resume), ("最初からやり直す", RestartStory), ("タイトルに戻る", GoToTitle));
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1;
            AudioListener.pause = false;
            PauseMenu.Hide();
        }
    }
}
