# Fail if repo text looks like real DMM/FANZA product codes (not allowlisted placeholders).
# Usage (repo root): .\tools\Check-NoRealDmmIdentifiers.ps1
# Exit 0 = clean, 1 = violations.

[CmdletBinding()]
param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (git rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($RepoRoot)) {
        $RepoRoot = Split-Path -Parent $PSScriptRoot
    }
}
$RepoRoot = (Resolve-Path $RepoRoot).Path

# Denylist makers built without contiguous forbidden literals in this file.
function New-Token([string[]]$Parts) { -join $Parts }

$denyMakers = @(
    (New-Token @("a", "e", "g", "e")),
    (New-Token @("d", "u", "i", "b")),
    (New-Token @("s", "v", "d", "v", "d")),
    (New-Token @("s", "s", "n", "i")),
    (New-Token @("s", "s", "i", "s")),
    (New-Token @("s", "o", "e")),
    (New-Token @("i", "p", "x")),
    (New-Token @("i", "p", "z")),
    (New-Token @("s", "t", "a", "r", "s")),
    (New-Token @("m", "i", "d", "v")),
    (New-Token @("m", "i", "a", "a")),
    (New-Token @("p", "r", "e", "d")),
    (New-Token @("w", "a", "n", "z")),
    (New-Token @("m", "v", "s", "d")),
    (New-Token @("j", "u", "l")),
    (New-Token @("a", "d", "n")),
    (New-Token @("a", "b", "p")),
    (New-Token @("n", "t", "r")),
    (New-Token @("d", "a", "s", "d")),
    (New-Token @("e", "b", "o", "d")),
    (New-Token @("m", "e", "y", "d")),
    (New-Token @("j", "u", "f", "e")),
    (New-Token @("h", "n", "d", "s")),
    (New-Token @("h", "n", "d")),
    (New-Token @("o", "n", "e", "z")),
    (New-Token @("f", "s", "e", "t")),
    (New-Token @("m", "e", "r", "c"))
) | ForEach-Object { $_.ToLowerInvariant() }

$denyMakerSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$denyMakers)

# fc2-ppv as a platform-shaped token (not a simple maker).
$denyFc2Ppv = New-Token @("f", "c", "2", "-", "p", "p", "v")
$fc2PpvRe = [regex]::new(('\b{0}\b' -f [regex]::Escape($denyFc2Ppv)), [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

# Placeholder makers allowed in docs/tests/comments (see .cursor/rules/no-real-dmm-identifiers.mdc).
$allowMakers = [System.Collections.Generic.HashSet[string]]::new([string[]]@(
    "abc", "abcd", "efgh", "xxxx", "xxx", "xyz", "yyyy", "zzzz", "unique", "aff", "zz", "ppv", "xxdvd"
))

$ignoreMakerTokens = [System.Collections.Generic.HashSet[string]]::new([string[]]@(
    "sha", "md5", "crc", "utf", "rgb", "argb", "dpi", "win", "mac", "iso", "api", "sql", "xml", "json",
    "http", "row", "col", "net", "ver", "guid", "test", "step", "bug", "item", "case", "page", "part",
    "disc", "file", "path", "name", "type", "code", "data", "info", "main", "build", "release",
    "draft", "top", "min", "max", "set", "get", "put", "run", "job", "log", "tmp", "temp", "cfg"
))

$includeExt = [System.Collections.Generic.HashSet[string]]::new([string[]]@(
    ".cs", ".md", ".mdc", ".xaml", ".xml", ".json", ".txt", ".ps1", ".yml", ".yaml", ".csv"
))

$excludeDirNames = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@("bin", "obj", ".git", "artifacts", "node_modules"),
    [StringComparer]::OrdinalIgnoreCase
)

$excludeRelPaths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$excludeRelPaths.Add("tools/Check-NoRealDmmIdentifiers.ps1") | Out-Null
$excludeRelPaths.Add("tools/hooks/pre-commit") | Out-Null

$hyphenRe = [regex]::new('\b([A-Za-z]{3,6})-(\d{2,6})[A-Za-z]?\b', [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$compactRe = [regex]::new('\b(?:h_)?\d{0,4}([A-Za-z]{3,6})(\d{4,6})\b', [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$dvdGlueRe = [regex]::new('\b([A-Za-z]{2,4}dvd\d{2,5})\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$zzPpvRe = [regex]::new('\bzz-ppv-\d+\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
$xxdvdRe = [regex]::new('\bxxdvd\d{2,5}\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

function Get-RelativePath([string]$FullPath) {
    $full = $FullPath.Replace('\', '/')
    $root = $RepoRoot.Replace('\', '/').TrimEnd('/') + '/'
    if ($full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($root.Length)
    }
    return $full
}

function Test-IsExcludedDir([System.IO.DirectoryInfo]$Dir) {
    while ($null -ne $Dir) {
        if ($excludeDirNames.Contains($Dir.Name)) { return $true }
        if ($Dir.FullName.TrimEnd('\') -eq $RepoRoot.TrimEnd('\')) { break }
        $Dir = $Dir.Parent
    }
    return $false
}

function Test-MakerAllowed([string]$Maker) {
    $m = $Maker.ToLowerInvariant()
    if ($denyMakerSet.Contains($m)) { return $false }
    if ($ignoreMakerTokens.Contains($m)) { return $true }
    return $allowMakers.Contains($m)
}

$violations = New-Object System.Collections.Generic.List[string]

function Add-Hit([string]$Rel, [int]$Line, [string]$Kind, [string]$Token) {
    $violations.Add("${Rel}:${Line}: [$Kind] $Token") | Out-Null
}

$files = Get-ChildItem -Path $RepoRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    if (-not $includeExt.Contains($_.Extension.ToLowerInvariant())) { return $false }
    if (Test-IsExcludedDir $_.Directory) { return $false }
    $rel = Get-RelativePath $_.FullName
    if ($excludeRelPaths.Contains($rel)) { return $false }
    if ($rel.StartsWith("tools/sinku/", [StringComparison]::OrdinalIgnoreCase)) { return $false }
    return $true
}

foreach ($file in $files) {
    $rel = Get-RelativePath $file.FullName
    $lines = @(Get-Content -LiteralPath $file.FullName -Encoding UTF8 -ErrorAction SilentlyContinue)
    if ($lines.Count -eq 0) { continue }

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = [string]$lines[$i]
        $lineNo = $i + 1

        foreach ($m in $fc2PpvRe.Matches($line)) {
            Add-Hit $rel $lineNo "deny-platform" $m.Value
        }

        foreach ($m in $hyphenRe.Matches($line)) {
            $maker = $m.Groups[1].Value
            if ($zzPpvRe.IsMatch($m.Value)) { continue }
            if (Test-MakerAllowed $maker) { continue }
            Add-Hit $rel $lineNo "hyphen-code" $m.Value
        }

        foreach ($m in $compactRe.Matches($line)) {
            $maker = $m.Groups[1].Value
            if (Test-MakerAllowed $maker) { continue }
            Add-Hit $rel $lineNo "compact-cid" $m.Value
        }

        foreach ($m in $dvdGlueRe.Matches($line)) {
            $token = $m.Value
            if ($xxdvdRe.IsMatch($token)) { continue }
            Add-Hit $rel $lineNo "dvd-glue" $m.Value
        }
    }
}

$unique = $violations | Select-Object -Unique

if ($unique.Count -gt 0) {
    Write-Host "Check-NoRealDmmIdentifiers: FAILED ($($unique.Count) hit(s))" -ForegroundColor Red
    $unique | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Use placeholders only (abcd / efgh / xxxx / xxdvd100 / zz-ppv-...). See .cursor/rules/no-real-dmm-identifiers.mdc" -ForegroundColor Yellow
    exit 1
}

Write-Host "Check-NoRealDmmIdentifiers: OK ($($files.Count) files scanned)"
exit 0
