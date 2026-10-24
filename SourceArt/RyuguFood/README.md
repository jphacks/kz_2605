# 竜宮城の料理

Blender 4.3で作成。Food0: 鯛の握り、Food1: 海老の握り、Food2: 三色団子、Food3: 巻き寿司。

`create_food.py` をBlenderのバックグラウンドモードで実行すると、編集用 `.blend` とUnity用 `Assets/Resources/RyuguFood/Food*.fbx` を再生成します。単位はメートルです。`preview_food.py` で料理一覧をレンダリングできます。

既存のMainシーンは、Area.InitからSceneryDetailsを一度だけ生成します。シーン再生成は不要です。料理の既存Grabbable・Collider・食べるトリガー参照を維持し、表示モデルのみを差し替えます。村・浜辺・海底の装飾も同じ初期化で反映します。静止した背景は材質ごとに結合し、波・海藻・踊り手は個別に動かします。
