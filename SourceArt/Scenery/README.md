# 浜辺・海中のBlenderモデル

- `FishingBoat.blend`：板を張り合わせた船殻、縁材、肋材、床板、腰掛け、櫂、櫂受け、釘、ロープ。
- `ReefButterfly.blend` / `ReefClownfish.blend` / `ReefTang.blend`：3種類の魚。立体の胴体、配色模様、口、目、えら、各ヒレ、ヒレの条を作成。
- `*_Preview.png`：Blenderでレンダリングしたプレビュー。
- `Unity_*.png`：描画を有効にしたUnityの環境テストで出力する画面確認用画像。
- `../../Assets/Resources/Scenery/`：Unity用FBXと波打ち際シェーダー。

魚は胴体と尾の2メッシュ、船は1メッシュに結合しています。魚の`Nose`で進行方向を合わせ、`Tail`を泳ぎに合わせて動かします。
既存のシーンは`SceneryDetails`が起動時にモデルを差し替えます。移動経路と当たり判定の参照は維持します。

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Scenery/create_scenery.py
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Scenery/verify_scenery.py
```
