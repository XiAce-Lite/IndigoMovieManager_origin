# IndigoMovieManager

WhiteBrowser 互換を目指した、Windows 向けの動画管理ツールです。  
WPF / .NET 8 ベースで、WhiteBrowser の SQLite データベースやサムネイル資産をなるべくそのまま使える方向で作っています。

以下のレポジトリは、より高機能なバージョンです。
<https://github.com/XiAce-Lite/IndigoMovieManager>

本レポジトリのバージョンは、高機能バージョンにフォークする前の版からの改良となります。

## 対応環境

- Windows 専用
- .NET 8 Desktop Runtime x64
- `x64 Release` を前提

Linux / macOS での動作は想定していません。

## 主な仕様

- WhiteBrowser の SQLite データベースファイルをそのまま利用可能
- 既存の WhiteBrowser 系スキン 4 種をベースに動作
- 追加で 5 枚 x 2 段タブも実装
- サムネイル管理、タグ管理、監視フォルダ登録、削除に対応
- ブックマーク対応
- ZIP 形式も一部対応
- 最近開いた管理ファイル一覧、個別設定、共通設定あり

### サムネイル関連

- 既存サムネイルの流用に対応
- アプリ既定のサムネイル保存先は `Thumb`
  - WhiteBrowser の `Thum` ではなく `Thumb`
  - 配下のフォルダ構成は概ね従来互換
- 任意サムネイル作成機能あり
- プレビュー小窓あり

### 検索

- 通常のキーワード検索（ファイル名・パス・コメントは部分一致、タグはタグ単位の完全一致）
  - 例: `★★★` は `★★★` のレコードのみヒットし、`★★★★★` 等はヒットしない
- AND 検索 / OR 検索（` | ` 区切り）
- `!キーワード` : タグのみを対象にした完全一致検索
- `-キーワード` : 除外検索
- `{ ... }` 内に SQL の WHERE 句を直接記述（WhiteBrowser 互換）
  - 例: `{tag = ''}` : タグなしのみ
  - 例: `{tag <> ''}` : タグ付きのみ
  - 例: `{movie_size < 50000}` : サイズ指定 など
  - `INSERT` / `UPDATE` / `DELETE` 等の更新系キーワードや `;` `--` は安全のため無効
- `{:: ... }` 特殊検索（WhiteBrowser 互換）
  - `{::duplication}` : ハッシュ重複のみ（サイズ順に並べ替え）
  - `{::nofile}` : DB 上は存在するが実ファイルがないもののみ
  - `{::error}` : 現在のタブで error サムネイルが表示されているもの（実ファイルはあるがサムネ未作成・生成失敗など。`{::nofile}` とは別）

### 監視・走査対象拡張子

- 共通設定で対象拡張子をカンマ区切りで指定可能
  - 例: `.mp4,.mkv,.zip`
  - `mp4,mkv` のように `.` を省略しても可
- 個別設定で除外拡張子を指定可能
  - 例: `.zip,.jpg`
  - 共通設定の対象拡張子のうち、ここで指定したものを除外

## 外部ツールについて

IndigoMovieManager は単体でも起動し、基本操作は可能です。  
ただし、一部の詳細取得や一部形式のサムネイル生成では外部ツールがあると精度や成功率が上がります。

### `sinku` 関連

以下 4 ファイルは、**必要な場合のみユーザー自身で用意**し、`IndigoMovieManager.exe` と **同じフォルダ** に配置してください。

- `sinku.exe`
- `Sinku.dll`
- `format.ini`
- `codecs.ini`

用途:

- メディア詳細情報（コンテナ、映像、音声、追加情報など）の取得
- ツールメニューの **全ファイル情報再取得**、リスト右クリックの **ファイル情報再取得**

注意:

- 4 ファイルはセット運用前提です
- `Sinku.dll` だけ差し替えて `format.ini` / `codecs.ini` が古いと正常に動かないことがあります
- 本リポジトリ / 標準配布物には同梱しない前提です
- 上記 4 ファイルが `IndigoMovieManager.exe` と同じフォルダに揃っていない場合、**ファイル情報再取得** のメニュー項目は無効になり、機能は使用できません

### `ffmpeg` 関連

以下 2 ファイルは、**必要な場合のみユーザー自身で用意**し、`IndigoMovieManager.exe` と **同じフォルダ** にある `ffmpeg` フォルダへ配置してください。

配置例:

```text
IndigoMovieManager.exe
ffmpeg/
  ffmpeg.exe
  ffprobe.exe
```

用途:

- OpenCV で失敗した動画サムネイル生成のフォールバック
- 一部の ZIP 内 WebP の変換
- 一部の動画長取得やプレビュー補助

補足:

- `ffmpeg` がなくてもアプリは起動します
- `ffmpeg` がない場合、一部形式ではサムネイル生成が失敗し、プレースホルダ画像になることがあります

## 配布方針

### 通常の `x64 Release`

```powershell
dotnet build -c Release -p:Platform=x64
```

出力先:

```text
bin\x64\Release\net8.0-windows
```

現在の Release 出力は、できるだけ配布しやすいよう整理されています。

- `IndigoMovieManager.exe` は単一 Exe 寄りの構成
- 不要な多言語 satellite は `ja` のみ残す
- 非 Windows 向け `runtimes` は削除

### 標準版配布物（publish）

```powershell
.\scripts\publish-standard.ps1
```

または

```powershell
dotnet publish IndigoMovieManager.csproj -c Release -p:Platform=x64 -p:StandardDistributionPublish=true
```

出力先:

```text
bin\x64\Release\net8.0-windows\publish
```

標準版配布物は Framework 依存 SingleFile です。

- Zip に含まれるのは主に **`IndigoMovieManager.exe`（約 100MB・DLL を内包）**、`Images\`、`IndigoMovieManager.dll.config` です
- 起動時にネイティブ DLL 等が一時フォルダへ展開されることがありますが、**配布 Zip 内に DLL がばらけて入る構成ではありません**
- `sinku` 関連 4 ファイル・`ffmpeg` は **同梱しません**（README の「外部ツールについて」を参照）

## 注意事項

- `layout.xml` は実行時に exe 基準で保存 / 読み込みします
- `Thumb`, `temp`, `bookmark` などは実行時に作成されます
- UI や内部実装はまだ整理途中です
- WhiteBrowser 完全互換を保証するものではありません

## 補足

WPF は 2023 年 11 月から触り始めたので、無駄コードやダメコードはまだ多いと思います。  
改善提案やアドバイスは歓迎です。
