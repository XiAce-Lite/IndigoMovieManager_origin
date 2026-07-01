## Indigo Movie Manager v{VERSION}

### 動作要件

- Windows 10 以降（x64）
- .NET ランタイムのインストールは不要です（.NET 8 ランタイムを同梱した自己完結型の単一 exe 配布）

### 同梱されていないもの（必要に応じて各自で配置）

#### sinku（メディア詳細情報の取得）

以下 **4 ファイルをセット**で用意し、`IndigoMovieManager.exe` と **同じフォルダ** に置いてください。

- `sinku.exe`
- `Sinku.dll`
- `format.ini`
- `codecs.ini`

未配置の場合でもアプリは起動しますが、**ファイル情報の再取得**など sinku を使う機能は動作しません。

#### ffmpeg（サムネイル生成の補助・任意）

必要な場合のみ、exe と同じ階層に `ffmpeg` フォルダを作り、次を配置してください。

```text
ffmpeg/ffmpeg.exe
ffmpeg/ffprobe.exe
```

未配置でも起動は可能です。一部形式ではサムネイルがプレースホルダになることがあります。

### 初回起動後

- `layout.xml` は exe と同じフォルダに保存されます
- `Thumb` / `temp` / `bookmark` などは実行時に作成されます

詳細はリポジトリの [README](https://github.com/XiAce-Lite/IndigoMovieManager_origin/blob/main/README.md)（使い方）および [ARCHITECTURE.md](https://github.com/XiAce-Lite/IndigoMovieManager_origin/blob/main/docs/ARCHITECTURE.md)（技術仕様）を参照してください。
