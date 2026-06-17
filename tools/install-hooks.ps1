# Enable pre-commit hook for automatic version bump.
# Run from repo root: .\tools\install-hooks.ps1

$ErrorActionPreference = "Stop"

$root = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    throw "Run this script inside a Git repository."
}

Push-Location $root
try {
    git config core.hooksPath tools/hooks
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set core.hooksPath."
    }

    Write-Host "OK: core.hooksPath = tools/hooks"
    Write-Host "pre-commit bumps the 4th part of FileVersion / AssemblyVersion on each commit."
    Write-Host "Skip hook: git commit --no-verify"
}
finally {
    Pop-Location
}
