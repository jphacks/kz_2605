namespace JPHACKS2026
{
    /// <summary>
    /// ナレーション・台詞の原稿（仮）。文言の修正はこのファイルだけで完結する。
    /// idは録音した音声ファイル名と対応させる（Assets/_Project/Audio/Voice/Resources/Narration/{id}.wav）。
    /// story_001〜031 はVOICEVOX音声（参考：昔話童話童謡の王国「うらしまたろう」）。
    /// ナレーション：栗田まろん／浦島太郎：ずんだもん／亀：春日部つむぎ／乙姫：四国めたん。
    /// </summary>
    public static class StoryScript
    {
        const string N = "";        // ナレーション
        const string Taro = "浦島太郎";
        const string Kame = "亀";
        const string Kodomo = "子ども";
        const string Oto = "乙姫";
        const string Murabito = "村人";
        const string Elder = "村のおじいさん";

        static Line L(string id, string speaker, string text) => new(id, speaker, text);

        // ── ① 浜辺 ───────────────────────────────────
        public static readonly Line[] BeachIntro =
        {
            L("story_001", N, "むかしむかし、ある海辺の村に、浦島太郎という心やさしい漁師がいました。"),
            L("story_002", N, "ある日のこと、浜辺を歩いていると、子どもたちが亀にいたずらをしていました。"),
            L("beach_03", Kodomo, "やーい、のろまな亀！"),
            L("beach_04", Kodomo, "ひっくり返してやれ！"),
            L("beach_05", N, "子どもたちが、一匹の亀にいたずらをしていたのです。"),
        };
        public static readonly Line[] BeachHelp =
        {
            L("story_003", Taro, "これこれ、かめにいたずらしてはいけないよ。"),
            L("beach_06", Kodomo, "わっ、大人だ！ 逃げろー！"),
            L("story_004", Taro, "はなしておやり。"),
            L("story_005", N, "そう言って浦島太郎は、亀を海へ逃がしてやりました。"),
        };

        // ── ② 竜宮城への誘い ─────────────────────────
        public static readonly Line[] InviteIntro =
        {
            L("story_006", N, "数日すぎたある日、浦島太郎が海で釣りをしていると、一匹の亀がやってきました。"),
            L("story_007", Kame, "浦島太郎さん、僕はこの前助けていただいた亀です。"),
            L("story_008", Kame, "お姫様があなたを竜宮城へお連れするようにと言っています。"),
            L("story_009", Taro, "竜宮城へつれていってくれるのかい？"),
            L("invite_02", Kame, "さあ、僕の背中に乗ってください。"),
        };
        public static readonly Line[] InviteGo =
        {
            L("story_010", Taro, "それなら、少し行ってみようか。"),
            L("story_011", N, "浦島太郎はさっそく亀の背中に乗って、海の中へと入っていきました。"),
        };
        public static readonly Line[] Undersea =
        {
            L("sea_01", N, "海の中は、青く、きらきらと光にあふれていました。"),
            L("sea_02", N, "色とりどりの魚たちが、浦島太郎のまわりを泳いでいきます。"),
            L("sea_03", Kame, "もうすぐ竜宮城ですよ。"),
        };

        // ── ③ 竜宮城 ─────────────────────────────────
        public static readonly Line[] RyuguIntro =
        {
            L("story_012", N, "竜宮城はさんごに囲まれた、それはそれは美しいお城でした。"),
            L("story_013", N, "お姫様はそれはそれは美しい方でした。"),
            L("story_014", Oto, "浦島太郎さん、亀を助けてくださってありがとうございました。"),
            L("story_015", Oto, "どうかごゆっくりしていってください。"),
            L("story_016", N, "太郎は、お城の中の大広間に案内され、たくさんのごちそうでもてなされました。"),
            L("story_017", N, "タイやヒラメやタコが、楽しそうに踊りを見せてくれます。"),
        };
        public static readonly Line[] RyuguEnjoy =
        {
            L("story_018", N, "浦島太郎は時間のたつのも忘れて、楽しく過ごしました。"),
            L("story_019", N, "まるで夢のような毎日でした。"),
        };

        // ── ④ 帰還 ───────────────────────────────────
        public static readonly Line[] ReturnIntro =
        {
            L("story_020", N, "数日が過ぎ、浦島太郎は村のことや、お母さんのことを思い出しました。"),
            L("story_024", Oto, "いつまでも、ここにいてくださいませんか？"),
        };
        public static readonly Line[] ReturnGo =
        {
            L("story_021", N, "別れぎわ、お姫様はさびしそうに浦島太郎を見つめました。"),
            L("story_022", Taro, "もう7日も竜宮城にいたので、そろそろ村へ帰ります。"),
            L("story_023", Taro, "ありがとうございました。"),
            L("story_025", Oto, "では、この玉手箱をお持ちください。"),
            L("story_026", Oto, "でも、この箱は決して開けてはいけませんよ。"),
        };

        // ── ⑤ 村・玉手箱 ─────────────────────────────
        public static readonly Line[] VillageIntro =
        {
            L("story_027", N, "亀に乗って村に帰ってみると、家もお母さんも見当たらず、村の様子はすっかり変わっていました。"),
            L("village_02", Murabito, "浦島太郎？ そんな人が、三百年も前にいたと聞いたことはあるが……"),
            L("village_03", N, "知っている人は、もう誰もいません。"),
            L("village_04", N, "途方に暮れた浦島太郎は、手元の玉手箱を見つめました。"),
        };

        // ── エンディング ───────────────────────────────
        public static readonly Line[] Ed1 =
        {
            L("ed1_01", N, "浦島太郎は、見なかったことにして通り過ぎました。"),
            L("ed1_02", Kame, "……えっ？"),
            L("ed1_03", N, "亀を助ける人は現れず、竜宮城の宴は、誰も来ないまま片づけられたそうです。"),
            L("ed1_04", N, "こうして、あなたの浦島太郎は始まりませんでした。"),
        };
        public static readonly Line[] Ed2a =
        {
            L("ed2_01", Kodomo, "いいぞー！ もっとやれー！"),
            L("ed2_02", N, "そのとき、海が低くうなりました。"),
        };
        public static readonly Line[] Ed2b =
        {
            L("ed2_03", Kame, "……よくも。"),
            L("ed2_06", N, "じつはこの亀は、竜宮城に仕える海の使い。海の力が集まり、その体がみるみる大きくなっていきます。"),
            L("ed2_04", N, "見上げるほど大きくなった亀は、浦島太郎を海の底へと引きずり込みました。"),
            L("ed2_05", N, "浦島太郎の姿を見た者は、それきり誰もいませんでした。"),
        };
        public static readonly Line[] Ed3 =
        {
            L("ed3_01", Kame, "えっ……行かないんですか？"),
            L("ed3_02", Kame, "ここまで準備したのに！ 招待状も、ごちそうも、鯛の踊りの練習も！"),
            L("ed3_03", N, "亀はひっくり返ってすねてしまい、やがて、しょんぼりと海へ帰っていきました。"),
            L("ed3_04", N, "浦島太郎は、いつもどおりの毎日を送ったそうです。"),
        };
        public static readonly Line[] Ed4Corridor =
        {
            L("ed4_01", N, "宴のにぎわいが、だんだんと遠ざかっていきます。"),
            L("ed4_02", N, "通路の奥に、ひっそりとした部屋がありました。"),
        };
        public static readonly Line[] Ed4Room = { L("ed4_03", N, "棚には、古い巻物がいくつも並んでいます。") };
        public static readonly Line[] Ed4Scroll =
        {
            L("ed4_04", N, "『客人 太郎兵衛 ── 帰らず』『客人 与作 ── 帰らず』『客人 ……』"),
            L("ed4_05", N, "竜宮城を訪れた客人は、誰ひとり、帰っていなかったのです。"),
            L("ed4_06", Oto, "……見てしまったのですね。"),
        };
        public static readonly Line[] Ed4End =
        {
            L("ed4_07", N, "灯りが消え、宴の音がやみ、竜宮城はみるみるうちに朽ち果てていきました。"),
            L("ed4_08", N, "暗い海の底に、浦島太郎の名前が、新しく書き加えられました。"),
        };
        public static readonly Line[] Ed5 =
        {
            L("ed5_01", Oto, "うれしい。では、宴を続けましょう。"),
        };
        public static readonly Line[] Ed5Loop1 = { L("ed5_02", N, "宴は続きました。十年……") };
        public static readonly Line[] Ed5Loop2 = { L("ed5_03", N, "百年……") };
        public static readonly Line[] Ed5Loop3 = { L("ed5_04", N, "千年……") };
        public static readonly Line[] Ed5NoReturn = { L("ed5_07", Oto, "帰り道なんて、もうありません。") };
        public static readonly Line[] Ed5End =
        {
            L("ed5_05", Oto, "ずっと、ずうっと、一緒ですよ。"),
            L("ed5_06", N, "浦島太郎が竜宮城を出ることは、二度とありませんでした。"),
        };
        // ED9：竜宮城からの脱出（ED4 の巻物の部屋から逃げる）
        public static readonly Line[] Ed9Escape = { L("ed9_01", Taro, "今だ、逃げよう！") };
        public static readonly Line[] Ed9Beach =
        {
            L("ed9_02", N, "背後から乙姫の声が追いかけてきます。浦島太郎は振り返らず、亀の背に飛び乗りました。"),
            L("ed9_03", N, "気がつくと、そこは懐かしい砂浜でした。波の向こうから、あの笑い声がかすかに聞こえます。"),
            L("ed9_04", Taro, "もう、あの城へは戻らない。"),
        };
        public static readonly Line[] Ed6 =
        {
            L("story_028", N, "どうしたらよいかわからなくなった浦島太郎は、玉手箱を開けてしまいました。"),
            L("story_029", N, "すると白いけむりがもくもくと立ちのぼり、浦島太郎はおじいさんになってしまいました。"),
            L("story_030", N, "竜宮城で楽しく過ごしているあいだに、何百年もの時がたっていたのです。"),
            L("story_031", N, "浦島太郎は、今どこで、どうしているのでしょうか。"),
        };
        public static readonly Line[] Ed7 =
        {
            L("ed7_01", N, "浦島太郎は、玉手箱を開けずに、そっと置いていきました。"),
            L("ed7_02", N, "知らない時代、知らない人たち。それでも、ここから新しい人生を始めよう。"),
            L("ed7_03", N, "浦島太郎は、朝日に向かって歩き出しました。"),
        };

        public static readonly Line[] Ed8Promise =
        {
            L(null, N, "浦島太郎は、玉手箱を抱えたまま、しばらく立ち尽くしていました。"),
            L(null, "浦島太郎", "この箱には、帰れなくなった日々まで入っている気がするんです。……けれど、開けてはいけないと約束しました。"),
            L(null, "浦島太郎", "どうか、預かってもらえませんか。ひとりで持っていると、開けてしまいそうで。"),
            L(null, Elder, "……そうか。大事な約束なんじゃな。なら、わしも一緒に守ろう。"),
        };
        public static readonly Line[] Ed8Receive =
        {
            L(null, N, "おじいさんは、ふたに手をかけることもなく、箱をそっと受け止めました。"),
            L(null, Elder, "中身は聞かんよ。話したくなったら、おまえさんの言葉で聞かせておくれ。"),
            L(null, "浦島太郎", "……ありがとうございます。"),
        };
        public static readonly Line[] Ed8Invitation =
        {
            L(null, Elder, "さて、そろそろ飯にしよう。ひとり分も、ふたり分も、手間はそう変わらん。"),
            L(null, "浦島太郎", "ぼくも……一緒で、いいんですか。"),
            L(null, Elder, "もちろんじゃ。明日は網の繕いを手伝ってくれるかい。"),
            L(null, "浦島太郎", "はい。それなら、得意です。"),
        };
        public static readonly Line[] Ed8End =
        {
            L(null, N, "失くした日々は、戻りません。それでも、明日の約束がひとつできました。"),
            L(null, N, "玉手箱は、とうとう開かれませんでした。代わりに、見知らぬ家の戸が、浦島太郎のために開いたのでした。"),
        };

        public static readonly Line[] Medetashi = { L("medetashi", N, "めでたし、めでたし。") };

        public static readonly Line[] TimeUp =
        {
            L("timeup_01", N, "……浦島太郎は、ただ立ち尽くしていました。"),
        };
    }
}
