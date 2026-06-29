# WPF版スキン（skin.json）の書き方

WPF版スキンは `Skins/Wpf/<スキン名>/skin.json` に置きます。
`スキン(WPF)` タブ上部のドロップダウンで切り替えられます。
編集中の `skin.json` を反映したい場合は、同じタブ上部の `Reload` ボタンを押してください。
スキンは実行ファイルと同じ場所（`bin\...\Skins\Wpf`）から読み込みます。JSON を編集したらそのフォルダ側を更新し、`Reload` で反映してください。

**色の編集:** 各 `skin.json` は JSON Schema（`../skin.schema.json`）を参照しています。Cursor / VS Code では `#FFFFFF` などの色値の左に色見本が表示され、クリックでカラーピッカーが開きます。`surface.background` / `card.background` / `styles.*.foreground` などが対象です。

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
| `valign: "stretch"` | Grid セルいっぱいに伸ばす |

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

---

## 同梱サンプル

| スキン | 説明 |
| --- | --- |
| `CardLarge` | タイトル上 + 大サムネ（400×225） |
| `SmallCard` | Small風。左サムネ + 右情報 + 下タグ（Grid比率） |
| `BigInfo` | Big風。左サムネ全高 + 右に詳細情報 |
| `CenterThumb` | 中央サムネ、左右に尺/サイズ、上下にタイトル/タグ |

---

## サムネの生成とアスペクト比

`thumbnail` セクションの `width` / `height` / `columns` / `rows` で、**このスキン専用のサムネイル**を生成します。
出力先フォルダは既存タブと同じ `{幅}x{高さ}x{列}x{行}` 形式です（例: `400x225x2x2`）。
既存タブと同じサイズを指定すれば、同じフォルダの画像を再利用できます。

- タブ切替時・自動生成・マニュアル生成（等間隔/キャプチャ）すべて、このレイアウトで動作します
- 表示枠は基本 16:9。実画像の比率に応じてクロップ or 黒余白（引き伸ばしなし）
