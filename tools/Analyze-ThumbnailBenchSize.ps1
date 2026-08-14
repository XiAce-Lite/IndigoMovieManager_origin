param([string]$OutputDir = 'M:\Temp\test')

function Import-BenchRows {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    Import-Csv -LiteralPath $Path | Where-Object {
        $_.FileName -and $_.Speedup -match '^\d' -and $_.OldSuccess -eq '1'
    }
}

function Summarize-Bench {
    param([string]$Label, [string]$Size, [int]$Panels, [object[]]$Rows)
    if ($Rows.Count -eq 0) {
        return [pscustomobject]@{ Label = $Label; Size = $Size; Panels = $Panels; Files = 0 }
    }
    $oldMs = 0L; $newMs = 0L
    foreach ($row in $Rows) {
        $oldMs += [long]$row.OldMs
        $newMs += [long]$row.NewMs
    }
    $newWins = ($Rows | Where-Object { [double]$_.Speedup -gt 1 }).Count
    $oldWins = ($Rows | Where-Object { [double]$_.Speedup -lt 1 }).Count
    [pscustomobject]@{
        Label = $Label
        Size = $Size
        Panels = $Panels
        Files = $Rows.Count
        OverallSpeedup = [math]::Round($oldMs / [double]$newMs, 3)
        NewWins = $newWins
        OldWins = $oldWins
        NewWinPct = [math]::Round(100 * $newWins / $Rows.Count, 1)
        TotalOldSec = [math]::Round($oldMs / 1000, 1)
        TotalNewSec = [math]::Round($newMs / 1000, 1)
    }
}

$comparisons = @(
    @{ Label = 'Bench4';   Size = '120x90';  Panels = 4; Path = 'thumb-bench-bench4.csv' }
    @{ Label = 'Bench4x3'; Size = '360x270'; Panels = 4; Path = 'thumb-bench-bench4x3.csv' }
    @{ Label = 'Bench5';   Size = '120x90';  Panels = 5; Path = 'thumb-bench-bench5.csv' }
    @{ Label = 'Bench5x3'; Size = '360x270'; Panels = 5; Path = 'thumb-bench-bench5x3.csv' }
)

Write-Host '=== Panel size comparison (same 30 files, parallel 8) ==='
$results = foreach ($c in $comparisons) {
    $rows = Import-BenchRows (Join-Path $OutputDir $c.Path)
    Summarize-Bench -Label $c.Label -Size $c.Size -Panels $c.Panels -Rows $rows
}
$results | Format-Table Label, Size, Panels, OverallSpeedup, NewWins, OldWins, NewWinPct, TotalOldSec, TotalNewSec -AutoSize

Write-Host ''
Write-Host '=== Size effect (3x pixels) ==='
$b4 = $results | Where-Object Label -eq 'Bench4'
$b4x = $results | Where-Object Label -eq 'Bench4x3'
$b5 = $results | Where-Object Label -eq 'Bench5'
$b5x = $results | Where-Object Label -eq 'Bench5x3'

if ($b4.Files -gt 0 -and $b4x.Files -gt 0) {
    $winner4 = if ($b4.OverallSpeedup -gt 1 -and $b4x.OverallSpeedup -gt 1) { 'new (both sizes)' }
               elseif ($b4.OverallSpeedup -gt 1 -and $b4x.OverallSpeedup -lt 1) { 'new@120 / old@360 — size flips winner' }
               elseif ($b4.OverallSpeedup -lt 1 -and $b4x.OverallSpeedup -gt 1) { 'old@120 / new@360' }
               elseif ($b4.OverallSpeedup -lt 1 -and $b4x.OverallSpeedup -lt 1) { 'old (both sizes)' }
               else { 'mixed' }
    Write-Host ("4 panels: 120x90 speedup={0} ({1}/{2} new) -> 360x270 speedup={3} ({4}/{5} new) => {6}" -f `
        $b4.OverallSpeedup, $b4.NewWins, $b4.Files, $b4x.OverallSpeedup, $b4x.NewWins, $b4x.Files, $winner4)
}

if ($b5.Files -gt 0 -and $b5x.Files -gt 0) {
    $winner5 = if ($b5.OverallSpeedup -gt 1 -and $b5x.OverallSpeedup -gt 1) { 'new (both sizes)' }
               elseif ($b5.OverallSpeedup -lt 1 -and $b5x.OverallSpeedup -lt 1) { 'old (both sizes)' }
               elseif ($b5.OverallSpeedup -lt 1 -and $b5x.OverallSpeedup -gt 1) { 'old@120 / new@360 — size flips winner' }
               elseif ($b5.OverallSpeedup -gt 1 -and $b5x.OverallSpeedup -lt 1) { 'new@120 / old@360' }
               else { 'mixed' }
    Write-Host ("5 panels: 120x90 speedup={0} ({1}/{2} new) -> 360x270 speedup={3} ({4}/{5} new) => {6}" -f `
        $b5.OverallSpeedup, $b5.NewWins, $b5.Files, $b5x.OverallSpeedup, $b5x.NewWins, $b5x.Files, $winner5)
}

# Per-file flip when size goes 3x
$b4rows = Import-BenchRows (Join-Path $OutputDir 'thumb-bench-bench4.csv')
$b4xrows = Import-BenchRows (Join-Path $OutputDir 'thumb-bench-bench4x3.csv')
$b5rows = Import-BenchRows (Join-Path $OutputDir 'thumb-bench-bench5.csv')
$b5xrows = Import-BenchRows (Join-Path $OutputDir 'thumb-bench-bench5x3.csv')

function Count-Flips($base, $large) {
    $flip = 0; $same = 0
    foreach ($r in $base) {
        $l = $large | Where-Object FileName -eq $r.FileName | Select-Object -First 1
        if (-not $l) { continue }
        $bNew = [double]$r.Speedup -gt 1
        $lNew = [double]$l.Speedup -gt 1
        if ($bNew -eq $lNew) { $same++ } else { $flip++ }
    }
    [pscustomobject]@{ Same = $same; Flip = $flip }
}

Write-Host ''
Write-Host '=== Per-file preference when size 3x (same panel count) ==='
Count-Flips $b4rows $b4xrows | ForEach-Object { Write-Host ("4 panels: same winner {0}/30, flipped {1}/30" -f $_.Same, $_.Flip) }
Count-Flips $b5rows $b5xrows | ForEach-Object { Write-Host ("5 panels: same winner {0}/30, flipped {1}/30" -f $_.Same, $_.Flip) }
