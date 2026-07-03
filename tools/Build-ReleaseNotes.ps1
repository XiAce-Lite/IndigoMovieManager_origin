# docs/release-notes-template.md と docs/release-notes-changes.md から
# GitHub Release 本文用の Markdown を生成する。
# CI の release ジョブ、およびローカルでのプレビューに使う。
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$RepoRoot = "",

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($RepoRoot)) {
        throw "Could not resolve Git repository root."
    }
}

$templatePath = Join-Path $RepoRoot "docs/release-notes-template.md"
$changesPath = Join-Path $RepoRoot "docs/release-notes-changes.md"

if (-not (Test-Path $templatePath)) {
    throw "Template not found: $templatePath"
}

function Get-ReleaseChangesBullets([string]$path) {
    if (-not (Test-Path $path)) {
        return @()
    }

    $lines = Get-Content -Path $path -Encoding UTF8
    $bullets = [System.Collections.Generic.List[string]]::new()
    $inHtmlComment = $false

    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ($trimmed -match '^<!--') {
            $inHtmlComment = $true
            if ($trimmed -match '-->$') {
                $inHtmlComment = $false
            }
            continue
        }
        if ($inHtmlComment) {
            if ($trimmed -match '-->$') {
                $inHtmlComment = $false
            }
            continue
        }
        if ($trimmed -match '^-\s+\S') {
            $bullets.Add($trimmed) | Out-Null
        }
    }

    return $bullets.ToArray()
}

$bullets = Get-ReleaseChangesBullets $changesPath
if ($bullets.Count -eq 0) {
    $changesBlock = "- 細かい修正・改善"
}
else {
    $changesBlock = ($bullets -join "`n")
}

$template = Get-Content -Path $templatePath -Raw -Encoding UTF8
$body = $template.Replace("{VERSION}", $Version).Replace("{CHANGES}", $changesBlock)

# 末尾の余分な空行を整えつつ、本文末尾に改行を1つ残す
$body = $body.TrimEnd("`r", "`n") + "`n"

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($OutputPath, $body, $utf8NoBom)
}

Write-Output $body
