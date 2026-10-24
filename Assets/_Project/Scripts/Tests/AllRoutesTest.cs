using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JPHACKS2026.Tests
{
    /// <summary>
    /// 全7エンディング・「直前の選択肢に戻る」を自動で通しプレイする（07 テスト計画 T07〜T13, T17, T19）。
    /// 選択は GameInput.DebugQueue に番号を積んで行う（数字キー入力と同じ扱い）。
    /// </summary>
    public class AllRoutesTest
    {
        readonly List<string> exceptions = new();

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) exceptions.Add(condition + "\n" + stackTrace);
            if (type == LogType.Error && condition.StartsWith("[StoryAnchor]")) exceptions.Add(condition);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;   // XRランタイムがない環境のエラーは無視
            exceptions.Clear();
            Application.logMessageReceived += OnLog;
            SaveData.ResetAll();
            GameInput.DebugQueue.Clear();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var gm = GameManager.Instance;
            Assert.IsNotNull(gm, "GameManager がシーンにありません");
            gm.narration.secondsPerChar = 0.002f;
            gm.narration.minDuration = 0.02f;
            gm.narration.gapBetweenLines = 0f;
            gm.story.rideDuration = 0.5f;
            Time.timeScale = 6f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
            GameInput.DebugQueue.Clear();
            yield return null;
        }

        static IEnumerator WaitUntilState(GameState state, float timeout = 120f)
        {
            float t = 0;
            while (GameManager.Instance.State != state && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator PlayRoute(EndingId expected, params int[] inputs)
        {
            yield return WaitUntilState(GameState.Title);
            foreach (var i in inputs) GameInput.DebugQueue.Enqueue(i);

            float t = 0;
            while (!SaveData.HasEnding(expected) && t < 150f)
            {
                t += Time.unscaledDeltaTime;
                Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
                yield return null;
            }
            Assert.IsTrue(SaveData.HasEnding(expected), $"{expected} に到達しませんでした（残り入力 {GameInput.DebugQueue.Count}）");
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        // 入力：絵本(0) → 物語を始める(0) → 各分岐の選択 → 「おわり」でタイトルへ(0)
        [UnityTest] public IEnumerator ED1_見過ごす() { yield return PlayRoute(EndingId.ED1, 0, 0, 1, 0); }
        [UnityTest] public IEnumerator ED2_いじめる() { yield return PlayRoute(EndingId.ED2, 0, 0, 2, 0); }
        [UnityTest] public IEnumerator ED3_行かない() { yield return PlayRoute(EndingId.ED3, 0, 0, 0, 1, 0); }
        [UnityTest] public IEnumerator ED4_探索() { yield return PlayRoute(EndingId.ED4, 0, 0, 0, 0, 1, 0, 0, 0); }
        [UnityTest] public IEnumerator ED5_帰らない() { yield return PlayRoute(EndingId.ED5, 0, 0, 0, 0, 0, 1, 0); }
        [UnityTest] public IEnumerator ED6_開ける() { yield return PlayRoute(EndingId.ED6, 0, 0, 0, 0, 0, 0, 0, 0); }
        [UnityTest] public IEnumerator ED7_開けない() { yield return PlayRoute(EndingId.ED7, 0, 0, 0, 0, 0, 0, 1, 0); }

        [UnityTest]
        public IEnumerator 直前の選択肢に戻って別エンド()
        {
            // ED1のあと「直前の選択肢に戻る」(1) → 浜辺の分岐で「いじめる」(2) → ED2
            yield return PlayRoute(EndingId.ED1, 0, 0, 1, 1, 2, 0);
            float t = 0;
            while (!SaveData.HasEnding(EndingId.ED2) && t < 120f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(SaveData.HasEnding(EndingId.ED2), "リトライ後に ED2 に到達しませんでした");
            Assert.AreEqual(2, SaveData.EndingCount);
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        [UnityTest]
        public IEnumerator 時間切れでゲームオーバー()
        {
            yield return WaitUntilState(GameState.Title);
            GameManager.Instance.story.walkChoiceTime = 0.5f;
            GameInput.DebugQueue.Enqueue(0);
            GameInput.DebugQueue.Enqueue(0);
            float t = 0;
            while (GameManager.Instance.State != GameState.Menu && t < 60f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(GameState.Menu, GameManager.Instance.State);
            Assert.AreEqual(0, SaveData.EndingCount, "時間切れはエンディング数に数えない");
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }
    }
}
