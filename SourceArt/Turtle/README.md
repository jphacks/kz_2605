# ウミガメモデル

## 造形の詳細

甲羅には隣り合う境界を共有する中央5枚・左右8枚の甲板、26枚の縁甲板、各甲板に4本の細い成長線を立体で追加しています。甲板の厚みと隙間を抑え、連続したドーム状に整えています。
ヒレの鱗、琥珀色の虹彩、細い口元、ヒレの前縁も形状として作成しています。頭は口先から後頭部まで連続したメッシュに変更し、別パーツの口先・下顎・顔の粒状の鱗を廃止しています。目は小さくし、淡色の突出した眼窩を取り除いています。
細部はFBXにも含まれるため、Blender専用のシェーダーなしで形状が維持されます。
ヒレの鱗は断面形状に沿わせています。配色はオリーブ・黄褐色・深緑に調整しています。
メッシュは73,354頂点・79,136面、リグは既存と同じ8ボーンです。面数は四角面を含むBlender上の値です。

- `SeaTurtle.blend`: 編集用モデル・8ボーンのリグ・アニメーション・プレビュー照明。
- `SeaTurtle_Preview.png`: モデルのプレビュー。
- `../../Assets/_Project/Art/Models/Turtle/SeaTurtle.fbx`: Unity用モデルとアニメーション。
- `create_turtle.py`: Blender 4.3で再生成するスクリプト。
- `verify_turtle.py`: ウェイト、ループの一致、動作、FBX再読み込みの検証。

## Blender

ファイルを開き、タイムラインを再生すると `Swim` が再生されます。
Dope SheetをAction Editorに切り替えると `Float` に変更できます。

| アクション | フレーム | 時間（30fps） | 内容 |
| --- | --- | --- | --- |
| Swim | 1–61 | 2秒 | 前ヒレの羽ばたき、後ヒレ・頭・尾・胴体の動き |
| Float | 1–121 | 4秒 | 水中で漂う小さな動き |

どちらもその場でのループです。進行方向への移動はゲーム側で制御します。
各パーツは一つのスキンメッシュにまとめ、パーツ単位でボーンにウェイトを割り当てています。

## Unity

FBXのImport SettingsでRigを `Generic` に設定します。
Animationで `Swim` と `Float` を含むクリップの `Loop Time` をオンにしてApplyします。
Animator Controllerにクリップを追加し、モデルのAnimatorに割り当てて再生します。
既存の `MainSceneBuilder` はこのFBXを参照します。FBXのGUIDとリグ・アニメーション名を維持しています。
既存の `Swimmer` は移動経路の制御に利用できます。
FBXの色は単色マテリアルです。使用中のレンダーパイプラインに応じてマテリアルを設定してください。
Unity上での再生確認は未実施です。

## 再生成・検証

リポジトリのルートから実行します。

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Turtle/create_turtle.py
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Turtle/verify_turtle.py
```
