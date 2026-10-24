# おとぎ配信 視聴ページ・配信サーバー

VRで絵本の世界に入った体験者の様子を、スマホやPCのブラウザでリアルタイムに見て、選択肢に投票・コメントできます。
配信サーバーは **サーバー役のPC 1台**（Quest や開発用PCとは別のPCでよい）で動かします。

```
Quest（Unity）──HTTP──▶ サーバー役のPC（node server.js）◀──ブラウザ── スマホ・PC
  場面・字幕・選択肢・VR視点の画像を送る       視聴ページを配信      見る・投票する・コメントする
  投票・コメント・視聴者数を受け取る           変更をリアルタイムに中継
```

## サーバー役のPCの準備

1. [Node.js](https://nodejs.org/)（18 以上）を入れる。追加のパッケージは不要です。
2. この `Web` フォルダーをまるごと、サーバー役のPCにコピーする（リポジトリを clone してもよい）。
3. 起動する。
   - Windows：`start-server.bat` をダブルクリック
   - Mac / Linux：`Web` フォルダーで `node server.js`
   - ポートを変えるとき：`node server.js 8081`
4. 画面に出る「スマホ・Questから : http://192.168.x.x:8080」を控える。
   - Windows で「ファイアウォールでブロックされています」と出たら、**プライベートネットワーク** で許可する。

データはメモリ上だけに持ちます（サーバーを止めるとコメントなどは消えます）。配信中はPCを起動したままにしてください。

## Unity（Quest）の設定

1. メニュー **JPHACKS2026 > Live Stream Config** を開く。
2. `Database Url` に、控えたURL（`http://192.168.x.x:8080`）を入れる。`Viewer Page Url` は空でよい。
   - `http://` のURLを入れると、Quest から http 通信できるよう Player Settings の
     「Allow downloads over HTTP」が自動で Always allowed になります。
   - URLが空のあいだは配信は無効で、ゲームは今まで通り動きます。
3. Quest にビルドして起動する。タイトル画面に「スマホで見る：URL」が出ます。

## スマホで見る

- スマホを **サーバー役のPCと同じWi-Fi** につなぎ、`http://192.168.x.x:8080` を開く。
- サーバー役のPCで `http://localhost:8080` を開き、右上の「スマホで見る」を押すとQRコードが出ます
  （QRコードの表示にはインターネット接続が必要です。つながっていなければURLだけ出ます）。
- ルームを分けるときは `?room=xxx`（Unity の `Room Id` と合わせる）。

## つながらないとき

- Quest・スマホ・サーバー役のPCが **同じネットワーク** にいるか確認する。
  学内Wi-Fiなどは端末どうしの通信が禁止されていることがあります。そのときはスマホのテザリングや
  持ち込みのWi-Fiルーターに全員をつなぐ。
- サーバー役のPCのファイアウォールで、`node` の受信が許可されているか確認する。
- サーバー役のPCのIPアドレスはネットワークが変わると変わります。起動時の表示を見て、Unity の `Database Url` を合わせる。

## 選択肢の決まり方

- 体験者が VR で行動すれば、それで決まる。
- 体験者が行動しないまま時間切れになったら、視聴者の **投票1位** で物語が進む（同数なら先の選択肢）。
- 投票が1つも無ければ、これまで通りの「時間切れ」。
- コメントに選択肢の名前（例：「助ける」）が入っていると、その選択肢に1票入る。

## 通信の中身（`/rooms/{room}/...`）

サーバーは Firebase Realtime Database の REST と同じ形で読み書きできます（`.json` 付きのパスに `PUT` / `PATCH` / `POST` / `GET`。
`PATCH` のキーに `state/phase` のような複数階層のパスを書ける。`Accept: text/event-stream` で GET するとストリーミング）。

| パス | 書く人 | 中身 |
|---|---|---|
| `state` | Unity | `phase`（title/story/ending/ended。ended で「ご視聴ありがとうございました。」）、`area` / `areaName`、`subtitle`（speaker, text） |
| `frame` | Unity | VR視点の画像（JPEG の base64） |
| `choice` | Unity | `id`, `question`, `options`[label, hint], `timeLimit`, `remaining`, `status`（open/closed）, `result`, `decidedBy`（player/viewers/timeout） |
| `ending` | Unity | 到達したエンディング |
| `audience/votes/{choiceId}/{viewerId}` | 視聴ページ | 選んだ選択肢の番号（0〜5） |
| `audience/comments/{key}` | 視聴ページ（POST） | `name`（20文字まで）, `text`（60文字まで）, `t` |
| `audience/viewers/{viewerId}` | サーバー | 視聴ページがつながっている間だけ存在。この数が視聴者数 |

## Firebase に戻すとき

同じ視聴ページは Firebase でも動きます（`backend.js` が開いた場所で自動的に切り替える）。
`public/config.js` の `DATABASE_URL` に Firebase の URL を入れ、`firebase deploy` で公開し、
Unity の `Database Url` を Firebase の URL に、`Viewer Page Url` を公開URL（`https://<プロジェクトID>.web.app`）にする。

## 注意

- 同じネットワークの中でだけ使う前提で、認証はありません。インターネットに公開しないでください。
