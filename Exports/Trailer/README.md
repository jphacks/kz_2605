# 童話配信・予告編

[動画を開く](Otogi_Trailer.mp4)

90秒 / 1920×1080 / 30fps / H.264 / AACステレオ。

## 構成

| 時間 | 内容 |
| --- | --- |
| 00:00–00:07 | 絵本の部屋。「もし、絵本の世界に入れたら…」 |
| 00:07–00:28 | 浜辺・海中・竜宮城・村・玉手箱の本線ダイジェスト |
| 00:28–00:34 | 「めでたし、めでたし。」→ 配信サービス風の一時停止 |
| 00:34–00:38 | 直前の映像を逆再生して浜辺へ |
| 00:38–00:43 | 浦島太郎の一人称視点で、棒を亀に振り下ろす |
| 00:43–00:48 | 「本当にカメを助けたのでしょうか？」 |
| 00:48–00:51.5 | 「ひとつの選択が、9つの結末へ。」 |
| 00:51.5–01:23 | 全9EDのダイジェスト |
| 01:23–01:30 | 「あなたなら、どう選ぶ？」／タイトル |

追加ナレーション：VOICEVOX：栗田まろん（ノーマル）。
既存ゲーム映像の音声：VOICEVOX：栗田まろん、ずんだもん、春日部つむぎ、四国めたん、小夜/SAYO、ちび式じい。
UIは本動画用に作成。BGM・巻き戻し音も本動画用にプログラムで作曲・合成。

`UI/` は透過PNG素材、`Voice/` は追加ナレーション、`Source/` は追加撮影素材。
`timeline.json` に編集区間を記録。`contact-sheet.jpg` は確認用の一覧画像。

再生成用スクリプト：`Tools/video/TrailerCapture.cs`、`trailer-ui.ps1`、`trailer-voices.cjs`、`build-trailer.cjs`。
Unity撮影スクリプトは隔離プロジェクト `.utmp/banquet-project` の Editor フォルダーで実行。
Windows PowerShell 5でUIを生成するときは、UTF-8として読み込んで実行してください：

```powershell
Invoke-Expression (Get-Content Tools/video/trailer-ui.ps1 -Raw -Encoding UTF8)
node Tools/video/trailer-voices.cjs
node Tools/video/build-trailer.cjs
```

検証：全編の映像・音声デコードにエラーなし。解像度・フレームレート・長さをffprobeで確認し、主要場面とUIの画像を目視確認。
