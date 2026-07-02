# Unset core.hooksPath (version bump moved to CI on main merge).
# Run from repo root: .\tools\install-hooks.ps1

$ErrorActionPreference = "Stop"

$root = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    throw "Run this script inside a Git repository."
}

Push-Location $root
try {
    $current = git config --get core.hooksPath 2>$null
    if ($current -eq "tools/hooks") {
        git config --unset core.hooksPath
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to unset core.hooksPath."
        }
        Write-Host "OK: unset core.hooksPath (pre-commit version bump removed)."
    }
    else {
        Write-Host "core.hooksPath is not tools/hooks (no change)."
    }

    Write-Host "Version: CI release job on main push increments FileVersion (4th part)."
    Write-Host "Manual: .\tools\bump-version.ps1"
}
finally {
    Pop-Location
}
