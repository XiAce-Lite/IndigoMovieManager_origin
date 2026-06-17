# Increment the 4th part of FileVersion / AssemblyVersion in IndigoMovieManager.csproj.
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

$csprojPath = Join-Path $RepoRoot "IndigoMovieManager.csproj"
if (-not (Test-Path $csprojPath)) {
    throw "csproj not found: $csprojPath"
}

function Get-IncrementedVersion([string]$version) {
    $parts = $version.Split(".")
    if ($parts.Count -ne 4) {
        throw "Invalid version format (expected a.b.c.d): $version"
    }

    foreach ($part in $parts) {
        if (-not ($part -match '^\d+$')) {
            throw "Invalid version format (expected a.b.c.d): $version"
        }
    }

    $parts[3] = ([int]$parts[3] + 1).ToString()
    return ($parts -join ".")
}

$content = Get-Content -Path $csprojPath -Raw -Encoding UTF8

$filePattern = '<FileVersion>(?<ver>\d+\.\d+\.\d+\.\d+)</FileVersion>'
$assemblyPattern = '<AssemblyVersion>(?<ver>\d+\.\d+\.\d+\.\d+)</AssemblyVersion>'

$fileMatch = [regex]::Match($content, $filePattern)
if (-not $fileMatch.Success) {
    throw "FileVersion not found in csproj."
}

$assemblyMatch = [regex]::Match($content, $assemblyPattern)
if (-not $assemblyMatch.Success) {
    throw "AssemblyVersion not found in csproj."
}

$currentFileVersion = $fileMatch.Groups["ver"].Value
$currentAssemblyVersion = $assemblyMatch.Groups["ver"].Value
if ($currentFileVersion -ne $currentAssemblyVersion) {
    throw "FileVersion ($currentFileVersion) and AssemblyVersion ($currentAssemblyVersion) differ."
}

$newVersion = Get-IncrementedVersion $currentFileVersion

$content = [regex]::Replace($content, '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$newVersion</FileVersion>")
$content = [regex]::Replace($content, '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$newVersion</AssemblyVersion>")

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($csprojPath, $content, $utf8NoBom)

Write-Output "Version bumped: $currentFileVersion -> $newVersion"
