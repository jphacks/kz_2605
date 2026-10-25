# 童話配信：アクション予告編

[完成動画](Otogi_Action_Trailer.mp4)

[新しい姫モデルを反映した更新版](Otogi_Action_Trailer_Princess.mp4)

[前半を穏やかにし、問いかけから雰囲気を変えた最新版](Otogi_Trailer_Contrast.mp4)

最新版は約61秒。「めでたし」までのカットを長めにし、控えめなカメラ移動・暖かい色・打楽器を使わない明るいBGMに変更しました。24.5秒の「本当にカメを助けたのでしょうか？」で低音の衝撃音と巨大な亀の接近映像を入れ、以降を緊迫した編集にしています。姫が連続して接近していた2カット目を空の宴会場に変更し、終盤の類似カットも海中・玉手箱を預ける場面に差し替えました。新しい姫モデルを使用しています。

最新版の再編集：`node Tools/video/build-action-trailer.cjs --reuse --contrast`。UI生成時は `action-ui.ps1 -Contrast` を使用。`contrast-timeline.json` に編集区間、`contrast-validation.json` に動画仕様を記録。全編デコードと主要場面の画像を確認済み。

更新版は `Princess_Hacks.fbx` と `JPHacks_Princess.png` を使用。現在のMainシーンを隔離プロジェクトにコピーし、竜宮城の導入・ED4・ED5・ED9を再撮影しました。巻き戻しと高速カットにも新しい映像を反映しています。登場タイミングに合わせて切り出し位置を調整し、約55秒の編集・音声・BGMを引き継ぎました。全編デコードと新モデルの主要カットを確認済みです。

撮影スクリプト：`Tools/video/PrincessTrailerCapture.cs`。撮影素材：`NewModel/`。
再編集：`node Tools/video/build-action-trailer.cjs --reuse --princess`。

約55秒、1920×1080、30fps、H.264 / AACステレオ。

元の構成（絵本の部屋 → 本線 → めでたし → 一時停止・巻き戻し → 亀へのいたずら → 問いかけ → 全9ED）を維持し、テンポと音響を作り直しました。

- シネマスコープの画角、寄りの動き、衝撃に合わせた画面の揺れ。
- 亀の逆襲・禁じられた部屋・脱出をつなぎ、危機が続く順序に編集。
- 全9EDを短いカットで紹介。終盤は0.45秒刻みの連続カット。
- オリジナルの低音・打撃音・リズム・上昇音。タイトル直前に静寂。
- 「物語を、選び直せ。」で締め、最後に亀の接近を挿入。

追加音声：VOICEVOX：栗田まろん（ノーマル）。語りの速度・ピッチ・抑揚を動画に合わせて調整。
UI・BGM・効果音はこの予告編用に作成。ゲーム映像は既存ED動画と前回の撮影素材を使用。

`Voice/` に追加音声、`UI/` に透過PNG、`timeline.json` に編集区間を保存。
再生成：リポジトリのルートで、VOICEVOXエンジンを起動して以下を実行。

```powershell
Invoke-Expression (Get-Content Tools/video/action-ui.ps1 -Raw -Encoding UTF8)
node Tools/video/build-action-trailer.cjs
```

全編デコード、動画の仕様、音量ピーク、主要カットとUIの画像を確認。
