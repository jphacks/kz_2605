namespace JPHACKS2026
{
    public enum EndingId { ED1 = 1, ED2, ED3, ED4, ED5, ED6, ED7 }

    /// <summary>7つのエンディングの名前とジャンル（内容は仮案。02 ストーリー仕様書）。</summary>
    public static class EndingCatalog
    {
        public const int Count = 7;

        public static string Title(EndingId id) => id switch
        {
            EndingId.ED1 => "始まらない物語",
            EndingId.ED2 => "亀の逆襲",
            EndingId.ED3 => "ここまで準備したのに",
            EndingId.ED4 => "禁じられた部屋",
            EndingId.ED5 => "終わらない宴",
            EndingId.ED6 => "玉手箱の煙",
            EndingId.ED7 => "新しい人生",
            _ => "",
        };

        public static string Genre(EndingId id) => id switch
        {
            EndingId.ED1 or EndingId.ED3 => "コメディ",
            EndingId.ED2 or EndingId.ED4 => "ホラー",
            EndingId.ED5 => "ミステリー",
            EndingId.ED6 => "通常エンド",
            EndingId.ED7 => "感動",
            _ => "",
        };
    }
}
