# 童話配信 視聴ページ

VRで絵本の世界に入った体験者の様子を、スマホやPCのブラウザでリアルタイムに見て、選択肢に投票・コメントできるページです。
Unity（Quest）と視聴ページは Firebase Realtime Database でつながります。

```
Quest（Unity）──REST──▶ Firebase Realtime Database ◀──JS SDK── 視聴ページ（スマホ・PC）
  場面・字幕・選択肢・VR視点の画像を書く          投票・コメント・視聴者数を書く
  投票・コメント・視聴者数を読む                  すべてをリアルタイムに表示
```

## セットアップ

1. [Firebase コンソール](https://console.firebase.google.com/) でプロジェクトを作り、**Realtime Database** を作成する（場所はシンガポールなど近いところ）。
2. Realtime Database の URL（`https://xxxx-default-rtdb.asia-southeast1.firebasedatabase.app` など）を控える。
3. `Web/public/config.js` の `DATABASE_URL` をその URL に書き換える。
4. ルールと視聴ページを公開する（[Firebase CLI](https://firebase.google.com/docs/cli) が必要：`npm install -g firebase-tools`）。

   ```sh
   cd Web
   firebase login
   firebase use --add        # 作ったプロジェクトを選ぶ
   firebase deploy           # database.rules.json と視聴ページを公開
   ```

   公開されたページ：`https://<プロジェクトID>.web.app/`
5. Unity のメニュー **JPHACKS2026 > Live Stream Config** を開き、
   - `Database Url` に手順2の URL を入れる（空のあいだは配信は無効で、ゲームは今まで通り動く）
   - `Viewer Page Url` に手順4の公開URLを入れる（タイトル画面に「スマホで見る：URL」が出る）
6. Quest にビルドして起動する。

## スマホで見る

- 公開URL（`https://<プロジェクトID>.web.app/`）をスマホで開くだけ。モバイル回線でも見られます。
- PC で開いて右上の「スマホで見る」を押すと QR コードが出ます。
- ルームを分けるときは `?room=xxx`（Unity の `Room Id` と合わせる）。

手元で試すだけなら、`Web/public` で `npx serve` などを起動して開けば動きます（`file://` では ES Modules が読めません）。

## 字幕と音声

- VR はセリフを読み始めるたびに `lines/` へ1行ずつ書きます（id・話し手・本文・長さ）。視聴ページはそれを字幕に出します。
  上書きの字幕（`state/subtitle`）と違い、回線が遅くても短いセリフが抜けません。
- 音声は視聴ページに組み込んであり（`public/voice/{id}.mp3`）、受け取った id の音声を再生します。
  スマホは操作のあとでないと音を出せないので、右上の「🔊 音声をオンにする」ボタンを押します。
- VR の音声（`Assets/_Project/Audio/Voice/Resources/Narration/*.wav`）を作り直したら、MP3 も作り直してから公開します。

  ```powershell
  python Tools/audio/export_web_voices.py
  ```

## 選択肢の決まり方

- 体験者が VR で行動すれば、それで決まる。
- 体験者が行動しないまま時間切れになったら、視聴者の **投票1位** で物語が進む（同数なら先の選択肢）。
- 投票も体験者の操作も無ければ、先頭の選択肢（本線）で物語を続ける。
- コメントに選択肢の名前（例：「助ける」）が入っていると、その選択肢に1票入る。

スマホでは投票中のカードを画面下へ固定し、自動スクロールは行いません。
選択肢をタップし、✓ が付くと投票済みです。締め切りまでは変更できます。
リアクションボタンは連続タップに対応し、入力欄は16px以上にしてスマホの自動拡大を防いでいます。ピンチ操作による拡大は利用できます。

物語の終了後は「よかった」「いまひとつ」と感想コメントを送れます。
感想は既存の `audience/comments` に `feedback` と `ending` を付けて保存するため、データベースルールの追加は不要です。

オフラインで投票・感想送信のUIロジックを確認するテスト：

```powershell
node --test Web/tests/viewer.test.cjs
```

## データの形（`rooms/{roomId}/`）

| パス | 書く人 | 中身 |
|---|---|---|
| `state` | Unity | `phase`（title/story/ending/ended。ended で「ご視聴ありがとうございました。」）、`area` / `areaName`、`subtitle`（speaker, text） |
| `frame` | Unity | VR視点の画像（JPEG の base64）。既定は1秒ごと・480×270 |
| `choice` | Unity | `id`, `question`, `options`[label, hint], `timeLimit`, `remaining`, `status`（open/closed）, `result`, `decidedBy`（player/viewers/timeout） |
| `audience/votes/{choiceId}/{viewerId}` | 視聴ページ | 選んだ選択肢の番号 |
| `audience/comments/{pushId}` | 視聴ページ | `name`, `text`（60文字まで）, `t` |
| `audience/viewers/{viewerId}` | 視聴ページ | 接続中だけ存在（切断で自動削除）。この数が視聴者数 |
| `ending` | Unity | 到達したエンディング |

Unity は、送る変更をためて1本の PATCH（複数パス同時更新）にまとめて送り、`audience` だけをストリーミング（Server-Sent Events）で受け取ります。
回線が遅くても遅れが溜まりません。

## 注意

- ルールは**ハッカソン用**で、Unity が書く場所（state / frame / choice / ending）は誰でも書けます。
  一般公開するなら Firebase Authentication（匿名ログイン）で配信者だけが書けるようにしてください。
- 画像は Realtime Database 経由で送っているので、視聴者が多いと通信量（無料枠：月10GB）を使います。
  `Live Stream Config` の `Frame Interval`（間隔）・解像度・画質で調整できます。
- Quest をプロキシのあるネットワーク（学内Wi-Fiなど）につなぐと、Firebase に届かないことがあります。
  そのときはテザリングなど、プロキシの無い回線を使ってください。
