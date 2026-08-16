# Install local Git hooks (sensitive-identifier scan on commit).
# Run from repo root: .\tools\install-hooks.ps1

$ErrorActionPreference = "Stop"

$root = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    throw "Run this script inside a Git repository."
}

Push-Location $root
try {
    $hooksDir = Join-Path $root "tools/hooks"
    if (-not (Test-Path (Join-Path $hooksDir "pre-commit"))) {
        throw "Missing tools/hooks/pre-commit"
    }

    git config core.hooksPath tools/hooks
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set core.hooksPath"
    }

    Write-Host "OK: core.hooksPath=tools/hooks (pre-commit runs Check-NoRealDmmIdentifiers.ps1)"
    Write-Host "Manual scan: .\tools\Check-NoRealDmmIdentifiers.ps1"
    Write-Host "CI also runs the same scan on push/PR."
}
finally {
    Pop-Location
}
