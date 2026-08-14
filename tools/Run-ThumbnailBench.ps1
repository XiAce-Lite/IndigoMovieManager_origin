param(
    [string]$VideoRoot = 'R:\01.Movies',
    [string]$OutputDir = 'M:\Temp\test',
    [string]$CsvPath = '',
    [string]$ThumbRoot = '',
    [ValidateSet('DefaultSmall', 'DefaultBig10', 'Bench4', 'Bench4x3', 'Bench5', 'Bench5x3', 'Bench7')]
    [string]$Layout = 'DefaultBig10',
    [int]$MaxFiles = 50,
    [int]$Parallelism = 8
)

# Old=OpenCV first, New=FFmpeg coarse first. Pick up to MaxFiles: 5GB+, 4GB+, 3GB+.

$ErrorActionPreference = 'Stop'

$utf8Script = Join-Path $PSScriptRoot 'Enable-Utf8Console.ps1'
if (Test-Path -LiteralPath $utf8Script) {
    . $utf8Script
}

$layoutKey = switch ($Layout) {
    'DefaultSmall' { '120x90x3x1' }
    'DefaultBig10' { '120x90x5x2' }
    'Bench5'       { '120x90x5x1' }
    'Bench4'       { '120x90x4x1' }
    'Bench4x3'     { '360x270x4x1' }
    'Bench5x3'     { '360x270x5x1' }
    'Bench7'       { '120x90x7x1' }
}

if ([string]::IsNullOrWhiteSpace($CsvPath)) {
    $csvBase = switch ($Layout) {
        'DefaultSmall' { 'thumb-bench-defaultsmall.csv' }
        'DefaultBig10' { 'thumb-bench-defaultbig10.csv' }
        'Bench5'       { 'thumb-bench-bench5.csv' }
        'Bench4'       { 'thumb-bench-bench4.csv' }
        'Bench4x3'     { 'thumb-bench-bench4x3.csv' }
        'Bench5x3'     { 'thumb-bench-bench5x3.csv' }
        'Bench7'       { 'thumb-bench-bench7.csv' }
        default        { 'thumb-bench.csv' }
    }
    $CsvPath = Join-Path $OutputDir $csvBase
}

if ([string]::IsNullOrWhiteSpace($ThumbRoot)) {
    $thumbSub = switch ($Layout) {
        'DefaultSmall' { 'thumbs-small' }
        'DefaultBig10' { 'thumbs-big10' }
        'Bench5'       { 'thumbs-bench5' }
        'Bench4'       { 'thumbs-bench4' }
        'Bench4x3'     { 'thumbs-bench4x3' }
        'Bench5x3'     { 'thumbs-bench5x3' }
        'Bench7'       { 'thumbs-bench7' }
        default        { 'thumbs' }
    }
    $ThumbRoot = Join-Path $OutputDir $thumbSub
}

if (-not (Test-Path -LiteralPath $VideoRoot)) {
    throw "Video root not found: $VideoRoot"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $ThumbRoot | Out-Null

$repoRoot = git rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    throw 'Could not resolve Git repository root.'
}

Push-Location $repoRoot
try {
    Write-Host 'Building Release x64...'
    cmd /c "taskkill /IM IndigoMovieManager.exe /F >nul 2>&1"
    dotnet build IndigoMovieManager.csproj -p:Platform=x64 -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'App Release build failed.'
    }

    dotnet build IndigoMovieManager.Tests\IndigoMovieManager.Tests.csproj -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Test project build failed.'
    }

    $env:IMM_THUMB_BENCH_RUN = '1'
    $env:IMM_THUMB_BENCH_ROOT = $VideoRoot
    $env:IMM_THUMB_BENCH_CSV = $CsvPath
    $env:IMM_THUMB_BENCH_THUMB_ROOT = $ThumbRoot
    $env:IMM_THUMB_BENCH_MAX_FILES = "$MaxFiles"
    $env:IMM_THUMB_BENCH_PARALLELISM = "$Parallelism"
    $env:IMM_THUMB_BENCH_LAYOUT = $Layout

    Write-Host ''
    Write-Host "Layout           : $Layout ($layoutKey)"
    Write-Host "Benchmark target : $VideoRoot (top-level, tier 5/4/3 GB, max $MaxFiles files)"
    Write-Host "Thumbnail output : $ThumbRoot\$layoutKey\"
    Write-Host "CSV output       : $CsvPath"
    Write-Host "Parallelism      : $Parallelism"
    Write-Host 'HW decode        : Auto (bench overrides settings)'
    Write-Host 'Old engine       : IMM_THUMB_AUTO_ENGINE=opencv (OpenCV first)'
    Write-Host 'New engine       : default (FFmpeg coarse seek first)'
    Write-Host 'Pass order       : all old, then all new'
    Write-Host ''
    Write-Host 'This may take a while. CSV is written after both passes complete.'
    Write-Host ''

    dotnet test IndigoMovieManager.Tests\IndigoMovieManager.Tests.csproj `
        -c Release `
        --no-build `
        --filter 'FullyQualifiedName~Compare_old_vs_new_thumbnail_engine'

    if ($LASTEXITCODE -ne 0) {
        throw "Benchmark test failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path -LiteralPath $CsvPath)) {
        throw "CSV was not created: $CsvPath"
    }

    Write-Host ''
    Write-Host 'Done. Open in Excel:'
    Write-Host "  $CsvPath"
}
finally {
    Pop-Location
    Remove-Item Env:IMM_THUMB_BENCH_RUN -ErrorAction SilentlyContinue
    Remove-Item Env:IMM_THUMB_BENCH_ROOT -ErrorAction SilentlyContinue
    Remove-Item Env:IMM_THUMB_BENCH_CSV -ErrorAction SilentlyContinue
    Remove-Item Env:IMM_THUMB_BENCH_THUMB_ROOT -ErrorAction SilentlyContinue
    Remove-Item Env:IMM_THUMB_BENCH_MAX_FILES -ErrorAction SilentlyContinue
    Remove-Item Env:IMM_THUMB_BENCH_PARALLELISM -ErrorAction SilentlyContinue
    Remove-Item Env:IMM_THUMB_BENCH_LAYOUT -ErrorAction SilentlyContinue
}
