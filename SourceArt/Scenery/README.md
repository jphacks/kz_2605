# 浜辺・海中のBlenderモデル

- `BeachStick.blend`：砂浜の流木の棒（約1.1m）。曲がった軸、木目の溝とひび、樹皮の残り、節、折れた小枝、裂けた先端。`create_beach_stick.py`で再生成。Unityでは`SceneryDetails`が既存の`Driftwood`をこの棒に置き換え、拾って亀を叩けるようにします。
- `FishingBoat.blend`：板を張り合わせた船殻、縁材、肋材、床板、腰掛け、櫂、櫂受け、釘、ロープ。
- `ReefButterfly.blend` / `ReefClownfish.blend` / `ReefTang.blend`：3種類の魚。立体の胴体、配色模様、口、目、えら、各ヒレ、ヒレの条を作成。
- `*_Preview.png`：Blenderでレンダリングしたプレビュー。
- `Unity_*.png`：描画を有効にしたUnityの環境テストで出力する画面確認用画像。
- `../../Assets/Resources/Scenery/`：Unity用FBXと波打ち際シェーダー。

魚は胴体と尾の2メッシュ、船は1メッシュに結合しています。魚の`Nose`で進行方向を合わせ、`Tail`を泳ぎに合わせて動かします。
既存のシーンは`SceneryDetails`が起動時にモデルを差し替えます。移動経路と当たり判定の参照は維持します。

## 宴会場

- `BanquetTai.blend`：赤い鯛。立体的な鱗、青い斑点、側線、えら蓋、唇、虹彩、背びれの棘とひれの条。
- `BanquetHirame.blend`：平たいヒラメ。上面に寄った両目、迷彩斑点、体を囲むひれと条。
- 宴会場の魚は胴体・尾・左右の胸びれの4メッシュ。`Tail`、`FinLeft`、`FinRight`を動かし、宴の停止・加速に合わせて泳ぎも変わります。
- `create_banquet_fish.py`でBlender原本、Unity用FBX、確認用PNGを再生成します。`verify_scenery.py`でFBXの再読み込み・階層・有限座標を検証します。
- 宴会場本体はユーザー提供の`Assets/_Project/Art/Models/enkaijou/enkaijou.fbx`を使用。Unityメニューの`JPHACKS2026 > Replace Banquet Hall Model`でURP用マテリアルとResources内のPrefabを生成し、開いているシーンへ反映します。手で調整した既存のマテリアルは維持します。
- 元の床・机の操作用Colliderと料理・人物・分岐の参照を残し、仮の建築物のRendererを非表示にします。
- `extract_banquet_textures.py`は提供された`.blend`内の12枚の画像を`enkaijou/Textures`に取り出します。畳・木目・襖・障子・照明の元テクスチャをURPのマテリアルに割り当てています。
- `Unity_Banquet.png`はPlayModeテストで出力した実際の配置・描画の確認画像です。

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Scenery/create_banquet_fish.py
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background Assets/_Project/Art/Models/enkaijou/enkaijou.blend --python SourceArt/Scenery/extract_banquet_textures.py
```

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Scenery/create_scenery.py
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Scenery/create_beach_stick.py
& 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe' --background --python SourceArt/Scenery/verify_scenery.py
```
