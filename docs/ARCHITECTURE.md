# IndigoMovieManager — アーキテクチャ

開発者向けの技術概要です。使い方は [README](../README.md) を参照してください。

## 概要

- **スタック**: .NET 8 / WPF / x64 Release
- **目的**: WhiteBrowser の SQLite 管理ファイル（`.wb` 等）とサムネイル資産をなるべくそのまま利用しつつ、モダンな UI で動画ライブラリを管理する
- **互換方針**: WhiteBrowser **完全互換は保証しない**。検索構文・スキン・ホスト API の一部は簡略化または未接続

高機能版フォーク: [IndigoMovieManager](https://github.com/XiAce-Lite/IndigoMovieManager)

## リポジトリ構成（主要）

| パス | 役割 |
|------|------|
| `MainWindow.xaml(.cs)` | メイン UI、検索・フィルタ・サムネ起動 |
| `Thumbnail/` | サムネ生成（OpenCV / FFmpeg）、レイアウトキャッシュ、パス解決 |
| `Services/` | スキン解決、キュー、フィルタ、設定永続化 |
| `Skins/Wpf/` | Indigo ネイティブスキン（`skin.json`） |
| `Skins/WbHost/` | WhiteBrowser 互換スキン（htm/css + WebView2） |
| `UserControls/` | 詳細ペイン、WB 一覧（`SkinView`）、ブックマーク等 |
| `IndigoMovieManager.Tests/` | 単体テスト |

## データとファイル配置

### 管理ファイル

- WhiteBrowser 形式の SQLite（拡張子 `.wb` 等）
- 個別設定は DB の `system` テーブル（例: `thum`, `skin`, `sort`）

### 実行時フォルダ（exe 基準）

| パス | 説明 |
|------|------|
| `Thumb/<DB名>/` | サムネ出力の既定ルート（WB の `Thum` ではない） |
| 個別設定のサムネフォルダ | 空でなければ `system.thum` のパスをそのまま使用 |
| `bookmark/<DB名>/` | ブックマーク画像の既定（個別設定で上書き可） |
| `temp/` | 一時ファイル |
| `layout.xml` | ウィンドウレイアウト |
| `Images/` | エラー・プレースホルダ画像 |

サムネはレイアウトキー **`W×H×C×R`**（幅×高さ×列×行）ごとにサブフォルダへ保存される。

## スキンアーキテクチャ

### 二つのエンジン

| | `SkinEngine.Wpf` | `SkinEngine.Wb` |
|--|------------------|-----------------|
| 一覧 | WPF `ListView` + 動的テンプレート | WebView2 + htm スキン |
| 設定ファイル | `Skins/Wpf/<名>/skin.json` | `Skins/WbHost/<名>/<名>.htm` |
| 切替 UI | ヘッダー「Indigo」+ ドロップダウン + Reload | ヘッダー「WhiteBrowser」+ ドロップダウン |
| 一覧右クリック | WPF `ContextMenu` 接続 | **未接続** |

詳細な WPF スキン記法: [Skins/Wpf/README.md](../Skins/Wpf/README.md)

### レイアウトキー

- `ThumbnailLayoutSpec`（`Thumbnail/ThumbnailLayoutSpec.cs`）で `W×H×C×R` を表現
- `ThumbnailLayoutResolver` がアクティブスキンから一覧用レイアウトを解決
- 詳細ペイン: `DetailPaneLayout`（120×90×1×1）。無い場合は DefaultGrid 互換 160×120×1×1 へフォールバック

### WhiteBrowser スキン読み込み

- ルート: `Skins/WbHost`（`WhiteBrowserSkinSettings.GetWbHostRoot()`）
- エントリ URL: `https://imm-wb.local/<フォルダ>/<フォルダ>.htm`
- htm 内の `thum-width` / `thum-height` / `thum-column` / `thum-row` を正規表現で解析しサムネ生成レイアウトに反映

### WB ブリッジ（`Skins/WbHost/imm-wb-compat.js`）

`window.external.execCmd` の接続状況:

| コマンド | 状態 |
|----------|------|
| `find` | 接続 → タグ検索 |
| `removeTag` | 接続 → タグ削除 |
| `exec` | 接続 → 再生（`play` メッセージ） |
| `focusThum` / `selectThum` / `scrollTo` | 接続 |
| `getFocusThum` / `getSelectThums` / `getInfo` | 接続（一覧 JS 用） |
| `showContextMenu` | **未接続** |
| `copy` / `move` / `makeThum` / `updateInfo` 等 | **未接続**（`default` で空返却） |

一覧のダブルクリック再生は `setupInteraction` 内の `dblclick` → `play` メッセージで C# 側 `SkinView_PlayRequested` に届く。

## サムネイル生成

### パイプライン

```
ThumbnailQueueScheduler
  → ThumbnailJobCoordinator（ジョブ ID・進捗・重複排除）
  → ThumbnailQueueProcessor（並列ワーカー・ステータスバー進捗）
  → ThumbnailCreationOrchestrator（OpenCV → FFmpeg フォールバック）
```

### 自動生成のトリガー

- DB オープン後、スキン切替、検索キーワード変更（絞り込みスコープの変更）
- `forceThumbnailRestart: true`（ヘッダー **更新** ボタン）で DB 再読込込み後に再開
- 検索中はフィルタ結果のみ、検索空はライブラリ全体（`ShouldUseFullLibraryForThumbnailRestart`）

### 手動・サイレント

- 等間隔 / マニュアル: 通常ジョブ（進捗表示あり）
- 詳細ペイン用など: `SilentJobId`（進捗バー非表示）

## 検索・フィルタ

- `MovieListFilter` / `MovieListCoordinator` がキーワード・ソートを適用
- `{ ... }` / `{:: ... }` は `WhiteBrowserBraceSearch` 等で WB 互換解析
- フィルタの世代管理: `MainWindowSessionState.FilterGeneration`（検索連打時の古い完了破棄）

## 設定の永続化

| 種別 | 保存先 |
|------|--------|
| 共通設定 | `Properties.Settings`（ユーザー設定） |
| 最後に開いた DB・スキン選択等 | `AppSettingsPersistence` |
| DB 個別設定 | SQLite `system` テーブル |

## ビルドと配布

### 通常の x64 Release

```powershell
dotnet build -c Release -p:Platform=x64
```

出力: `bin\x64\Release\net8.0-windows`

- `IndigoMovieManager.exe` は単一 Exe 寄りの構成
- 不要な多言語 satellite は `ja` のみ
- 非 Windows 向け `runtimes` は削除

### 標準版配布物（publish）

```powershell
.\scripts\publish-standard.ps1
```

または

```powershell
dotnet publish IndigoMovieManager.csproj -c Release -p:Platform=x64 -p:StandardDistributionPublish=true
```

出力: `bin\x64\Release\net8.0-windows\publish`

- Framework 依存 SingleFile（Zip 内は主に exe・`Images\`・config）
- `sinku` / `ffmpeg` は同梱しない

## テストと CI

- `IndigoMovieManager.Tests` — `dotnet test`（Release）
- GitHub Actions: `main` push でビルド・テスト・リリース

## 既知の制限

- WhiteBrowser ホスト API の大部分は未実装
- UI・内部実装は整理途中
- Linux / macOS 非対応
