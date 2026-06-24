# 標準版配布物を publish フォルダへ出力する（Framework 依存 SingleFile、ffmpeg 非同梱）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    dotnet publish IndigoMovieManager.csproj -c Release -p:Platform=x64 -p:PublishProfile=Win-x64-SingleFile
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $publishDir = Join-Path $root "bin\x64\Release\net8.0-windows\publish"
    Write-Host ""
    Write-Host "配布フォルダ: $publishDir"
    Get-ChildItem $publishDir | Format-Table Name, Length -AutoSize
}
finally {
    Pop-Location
}
