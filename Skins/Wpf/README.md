# WPF版スキン（skin.json）の書き方

アプリ全体の技術概要は [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md)、使い方は [README](../../README.md) を参照してください。

WPF版スキンは `Skins/Wpf/<スキン名>/skin.json` に置きます。
`スキン(WPF)` タブ上部のドロップダウンで切り替えられます。
編集中の `skin.json` を反映したい場合は、同じタブ上部の `Reload` ボタンを押してください。
スキンは実行ファイルと同じ場所（`bin\...\Skins\Wpf`）から読み込みます。JSON を編集したらそのフォルダ側を更新し、`Reload` で反映してください。

**色の編集:** 各 `skin.json` は JSON Schema（`../skin.schema.json`）を参照しています。Cursor / VS Code では `#FFFFFF` などの色値の左に色見本が表示され、クリックでカラーピッカーが開きます。`surface.background` / `card.background` / `styles.*.foreground` などが対象です。

### colorProfile（アプリのテーマとの関係）

| `colorProfile` | 挙動 |
| --- | --- |
| **省略** | JSON の色はライト基準。共通設定の **ライト / ダーク / システム** に追従（ダーク時はアプリが色をリマップ） |
| **`"light"`** | JSON の色をそのまま使う。アプリのテーマ切替では変更しない |
| **`"dark"`** | JSON の色をそのまま使う。アプリのテーマ切替では変更しない |

固定したい配色があるスキンだけ `"light"` / `"dark"` を指定してください。WB スキンには影響しません。

レイアウトは **ペイン（パネル）の入れ子ツリー** で表現します。

- **コンテナ**: `children` を持つ。`panel: "stack"` または `panel: "grid"`
- **要素**: `type` で描画（`text` / `thumbnail` / `tags`）

---

## 全体構造

```jsonc
{
  "name": "MySkin",
  "type": "card",

  "surface": {
    "background": "#FFFFFF"      // リスト全体の背景色（既定は白＝既存 Small タブ準拠）
  },

  "styles": {
    "title": { "fontSize": 14, "foreground": "#000", "bold": true },
    "meta":  { "fontSize": 12, "foreground": "#555" }
  },

  "thumbnail": {
    "width": 400,                // 1コマの幅(px)
    "height": 225,               // 1コマの高さ(px)。既定は 16:9（width*9/16）
    "columns": 2,                // 横並びコマ数（サムネシート）
    "rows": 2                    // 縦並びコマ数（サムネシート）
  },

  "card": {
    "width": 680,                // カード幅(px)。並び数はこれで決まる
    "height": 0,                 // 任意。固定高さにしたいとき
    "padding": 8,
    "background": "",            // 空ならカード背景なし（リスト背景を透過）
    "layout": { /* ルートペイン */ }
  }
}
```

**カードの横並び数**は `card.width` とウィンドウ幅で決まります（VirtualizingWrapPanel）。
WQHD で 3 列になっても、カード幅を大きくすれば 2 列運用にできます。

---

## コンテナ: Stack

縦/横に単純に積みます。

| キー | 説明 |
| --- | --- |
| `stack` | `vertical`(既定) / `horizontal` |
| `children` | 子ノード配列 |

```jsonc
{ "stack": "vertical", "children": [
    { "type": "text", "field": "title" },
    { "type": "thumbnail" }
]}
```

## コンテナ: Grid（比率・行列）

| キー | 説明 |
| --- | --- |
| `panel` | `"grid"` |
| `rows` | 行定義配列。`auto`, `*`, `2*`, `120` など |
| `columns` | 列定義配列。同上 |
| `children` | 子ノード。各子に `row`, `col`, `rowSpan`, `colSpan` |

**行/列の指定値:**

| 値 | 意味 |
| --- | --- |
| `auto` | 内容に合わせる |
| `*` | 残りを均等分割 |
| `2*` | 残りの 2 倍 |
| `120` | 固定 120px |

```jsonc
{
  "panel": "grid",
  "columns": ["360", "1*"],
  "rows": ["auto"],
  "children": [
    { "type": "thumbnail", "col": 0, "row": 0 },
    { "stack": "vertical", "col": 1, "row": 0, "children": [ /* 情報 */ ] }
  ]
}
```

- **左右比率**: `columns: ["1*", "2*"]` → 左1:右2
- **サムネが高さ全部**: `rowSpan: 2` + `valign: "stretch"`（BigInfo 参照）
- **中央サムネ＋周囲**: 3×3 Grid（CenterThumb 参照）

---

## 要素ノード

### `text`

| キー | 説明 |
| --- | --- |
| `field` | MVフィールド（下表） |
| `label` | 見出し（例 `"尺: "`） |
| `format` | `filesize` でサイズ整形 |
| `style` | `styles` 辞書のキー |
| `fontSize` / `foreground` / `bold` / `italic` / `wrap` / `align` | 個別上書き |

### `thumbnail`

| キー | 説明 |
| --- | --- |
| `width` / `height` | 個別サイズ（省略時は `thumbnail` セクション） |
| `source` | `local` / `comment1`。枠を分割して同居させるとき指定。省略＝兼用枠（`preferJacket` 時はジャケ差し替え） |
| `valign: "stretch"` | Grid セルいっぱいに伸ばす |

簡易エディタの項目パレットでは **「サムネイル（ローカル）」** と **「ジャケ写（Comment1）」** が別項目です（各1枠まで）。配置・削除に合わせて `thumbnail.sources` を同期します。`source` 無しの兼用枠はローカル扱い（ジャケ写は未配置のまま追加可＝同居へ）。

### `tags`

タグを WrapPanel で表示。クリックで検索、×で削除。

---

## 共通プロパティ（コンテナ・要素）

| キー | 説明 |
| --- | --- |
| `width` / `height` | 固定サイズ |
| `minWidth` / `maxWidth` / `minHeight` / `maxHeight` | 制約 |
| `margin` | 外側余白(px) |
| `padding` | 内側余白(px)。背景色と併用で枠表現 |
| `background` | 背景色 |
| `valign` | `top` / `center` / `bottom` / `stretch` |
| `halign` | `left` / `center` / `right` / `stretch` |

---

## styles（共通スタイル）

ノードで `style: "title"` と書くと `styles.title` を適用し、
ノード個別の `fontSize` 等で上書きできます。
ノード側で `foreground` 等を**明示しなければ** `styles` の値がそのまま効きます
（未指定の既定値は空＝センチネル扱いで、`styles` を上書きしません）。

色の既定値は既存 Small タブ準拠です。

- リスト背景: `#FFFFFF`（白）
- 文字前景: `#000000`（黒）
- カード背景: なし（リスト背景を透過）

```jsonc
"styles": {
  "title": { "fontSize": 14, "foreground": "#000000", "bold": true, "wrap": true },
  "meta":  { "fontSize": 12, "foreground": "#555555" }
}
```

---

## MVフィールド一覧

| 別名 | 内容 | 別名 | 内容 |
| --- | --- | --- | --- |
| `title` / `name` | ファイル名 | `score` | スコア |
| `body` | 別表示名 | `viewCount` | 再生回数 |
| `length` | 再生時間 | `container` | コンテナ |
| `size` | サイズ | `video` / `audio` | 映像/音声 |
| `fileDate` | 更新日 | `ext` | 拡張子 |
| `path` | フルパス | `comment1`〜`3` | コメント |
| `artist` | メーカー（DB 列名は artist） | `tags` | タグチップ |

### thumbnail.preferJacket

`true` のとき、一覧は次の順で表示する（**`thumbnail.sources` が有効なときは無視**）。

1. スキンの `thumbnail`（W×H×C×R）で生成した**ローカルサムネを先に表示**（枠サイズは JSON の幅×高さ）
2. `comment1` が HTTP(S) のジャケ写 URL なら裏で取得し、成功したら**差し替え**（ディスクへは保存しない）
3. 差し替え時の枠は **幅＝JSON の幅、高さ＝ジャケ写の縦横比から自動**（黒帯なし）。Card / List 共通
4. 取得失敗時はローカルサムネのまま

取得は `HttpClient`（User-Agent・約 15 秒タイムアウト）でバイトを取り、メモリから画像化する。完了しない待ちは打ち切ってローカルのままにする。スキン切替時は進行中の取得とステータスバー件数を破棄する。

列・行はローカルサムネ生成用。ジャケ写ありのときは 1 枚を上記の自動枠で表示する。

読み込み中はセル下端に細いインジケータ、他の進捗が無いときはステータスバーに「ジャケ写取得中 n 件」を表示する。

### thumbnail.sources

ジャケ写とローカルサムネを**同居**させる（最大 2、`kind` は `local` / `comment1` のみ）。

```json
"sources": [
  { "kind": "comment1" },
  { "kind": "local" }
]
```

- **sources 優先**: 有効な sources があるとき `preferJacket` は実行時無視
- **list 型**: 描画では sources を無視（preferJacket / local のみ）。JSON の sources は**消さない**（card に戻すと同居が復活）
- `sources: [{ "kind": "comment1" }]` のみ、または分割配置のジャケ枠で URL 無し・取得失敗 → **local をジャケ枠サイズ（既定 360×203）で表示**
- 2 ソース時にジャケ失敗 → ジャケ枠は local フォールバック（上記）。右の local 枠は通常表示
- ローカル枠の表示サイズはノードの `width`/`height` を優先（5×2 なら 600×180）。親幅追従で高さを再計算するときはセル×格子を使う

---

## 同梱サンプル

| スキン | 説明 |
| --- | --- |
| `CardLarge` | タイトル上 + 大サムネ（400×225） |
| `BigInfo` | Big風。左サムネ全高 + 右に詳細情報 |
| `JacketInfo` | BigInfo 系。ジャケ写優先（幅 JSON・高さはジャケ比自動／なしは 360×203）、タグ表示 |
| `JacketInfo3x2` | JacketInfo 派生。ジャケ写優先＋ローカルは 360×202×3×2 の格子サムネ |
| `JacketLocalSide` | 左ジャケ（JacketInfo フォールバック 360×203）＋右上 5×2（DefaultBig10）＋右下ファイル名／タグ。`source` で枠を分割 |

骨格テンプレ「構造から」にも同系の **「左ジャケ ＋ 右ローカル（同居）」** あり（生成セル 120×90×5×2＝4:3）。

| `CenterThumb` | 中央サムネ、左右に尺/サイズ、上下にタイトル/タグ |
| `WideGridInfo` | 大サムネ + 右に詳細情報（WideGrid 風レイアウト） |
| `DarkModeSample` | DefaultSmall と同一レイアウト。色のみダーク版（`colorProfile: "dark"` のサンプル） |

※ `Default*` 系は旧ネイティブタブ再現用。上記サンプルはユーザー改変のベース用。

### 読込失敗時のフォールバック

`skin.json` が読めない・パースできない場合、アプリは例外で止まらず **`CardLarge`** を適用します（`WpfSkinLoader.LoadDefault`）。コンボ上の名前と実際の見た目／サムネフォルダ（`400x225x1x1`）が食い違うことがあります。Debug 出力に失敗理由が残ります。

- 新規スキンの雛形も CardLarge です（メンテ画面の「名前を付けて保存」前）
- `Default*` は保護対象のため、失敗時フォールバックや雛形には使いません
- `margin` / `padding` は数値・`"左,上,右,下"` 文字列・数値配列 `[左,上,右,下]` に対応。未対応形式だと読込全体が失敗し CardLarge に落ちます

---

## サムネの生成とアスペクト比

`thumbnail` セクションの `width` / `height` / `columns` / `rows` で、**このスキン専用のサムネイル**を生成します。
出力先フォルダは既存タブと同じ `{幅}x{高さ}x{列}x{行}` 形式です（例: `400x225x2x2`）。
既存タブと同じサイズを指定すれば、同じフォルダの画像を再利用できます。

- タブ切替時・自動生成・マニュアル生成（等間隔/キャプチャ）すべて、このレイアウトで動作します
- 表示枠は基本 16:9。実画像の比率に応じてクロップ or 黒余白（引き伸ばしなし）
