# リリース後に docs/release-notes-changes.md をコメントのみの初期状態へ戻す。
param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($RepoRoot)) {
        throw "Could not resolve Git repository root."
    }
}

$changesPath = Join-Path $RepoRoot "docs/release-notes-changes.md"

$resetContent = @"
<!--
次回リリースの「主な変更点」を、このファイルに箇条書きで書く。
main へマージする直前に内容を確定する（エージェント下書き → 人間承認）。

書き方:
- 先頭が「- 」の行だけが Release 本文に入る
- 3〜7 行程度。細かい修正は省略してよい
- この HTML コメントは Release 本文に出ない

リリース後、CI がこのファイルを空（コメントのみ）に戻す。
-->
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($changesPath, $resetContent.TrimEnd() + "`n", $utf8NoBom)
Write-Output "Cleared: $changesPath"
