# ウミガメモデル

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
プロジェクト既存のシーンやカメの仮モデルは変更していません。
既存の `Swimmer` は移動経路の制御に利用できます。
FBXの色は単色マテリアルです。使用中のレンダーパイプラインに応じてマテリアルを設定してください。
Unity上での再生確認は未実施です。

## 再生成・検証

リポジトリのルートから実行します。

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Turtle/create_turtle.py
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Turtle/verify_turtle.py
```
