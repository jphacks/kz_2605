using System.Collections;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// ナレーション・台詞を再生し、字幕を同期して表示する。
    /// 音声は Resources/Narration/{行のid} にあれば再生し、なければ文字数から表示時間を決める（仮素材段階）。
    /// </summary>
    public class NarrationPlayer : MonoBehaviour
    {
        public AudioSource voiceSource;
        [Tooltip("音声がないときの1文字あたりの表示時間（秒）。読み上げ速度の調整用")]
        public float secondsPerChar = 0.12f;
        public float minDuration = 1.6f;
        public float gapBetweenLines = 0.25f;

        public bool IsSpeaking { get; private set; }
        /// <summary>読み上げ中の行（登場人物の身ぶりに使う）。読んでいないときは null。</summary>
        public Line? Current { get; private set; }

        SubtitleView subtitle;

        public void Bind(SubtitleView view) => subtitle = view;

        public IEnumerator Say(params Line[] lines)
        {
            foreach (var line in lines) yield return SayOne(line);
        }

        IEnumerator SayOne(Line line)
        {
            IsSpeaking = true;
            Current = line;
            subtitle.Show(line.speaker, line.text);
            if (LiveStream.Instance != null) LiveStream.Instance.PublishSubtitle(line.speaker, line.text);

            var clip = string.IsNullOrEmpty(line.id) ? null : Resources.Load<AudioClip>("Narration/" + line.id);
            float duration;
            if (clip != null)
            {
                voiceSource.clip = clip;
                voiceSource.Play();
                duration = clip.length;
            }
            else
            {
                duration = Mathf.Max(minDuration, 0.6f + line.text.Length * secondsPerChar);
            }
            if (LiveStream.Instance != null) LiveStream.Instance.PublishLine(line, duration);

            float t = 0;
            while (t < duration)
            {
                yield return null;
                t += Time.deltaTime;
                if (GameInput.DebugSkipDown()) break;
            }
            voiceSource.Stop();
            subtitle.Hide();
            if (LiveStream.Instance != null) LiveStream.Instance.PublishSubtitle(null, null);
            IsSpeaking = false;
            Current = null;
            yield return Tween.Wait(gapBetweenLines);
        }

        public void Stop()
        {
            if (voiceSource != null) voiceSource.Stop();
            if (subtitle != null) subtitle.Hide();
            IsSpeaking = false;
            Current = null;
        }
    }
}
