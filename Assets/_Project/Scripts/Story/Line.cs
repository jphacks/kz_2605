namespace JPHACKS2026
{
    /// <summary>ナレーション・台詞の1行。idは音声ファイル名（Resources/Narration/{id}）に使う。</summary>
    public readonly struct Line
    {
        public readonly string id;
        public readonly string speaker;
        public readonly string text;

        public Line(string id, string speaker, string text)
        {
            this.id = id;
            this.speaker = speaker;
            this.text = text;
        }
    }
}
