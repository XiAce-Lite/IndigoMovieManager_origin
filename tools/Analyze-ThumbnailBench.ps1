param(
    [string]$OutputDir = 'M:\Temp\test'
)

function Import-BenchRows {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    Import-Csv -LiteralPath $Path | Where-Object {
        $_.FileName -and $_.Speedup -match '^\d' -and $_.OldSuccess -eq '1' -and $_.NewSuccess -eq '1'
    }
}

function Summarize-Bench {
    param([string]$Label, [int]$Panels, [object[]]$Rows)
    if ($Rows.Count -eq 0) {
        return [pscustomobject]@{ Label = $Label; Panels = $Panels; Files = 0 }
    }
    $oldMs = 0L
    $newMs = 0L
    foreach ($row in $Rows) {
        $oldMs += [long]$row.OldMs
        $newMs += [long]$row.NewMs
    }
    $newWins = ($Rows | Where-Object { [double]$_.Speedup -gt 1 }).Count
    $oldWins = ($Rows | Where-Object { [double]$_.Speedup -lt 1 }).Count
    $ties = $Rows.Count - $newWins - $oldWins
    [pscustomobject]@{
        Label = $Label
        Panels = $Panels
        Files = $Rows.Count
        TotalOldMs = $oldMs
        TotalNewMs = $newMs
        OverallSpeedup = [math]::Round($oldMs / [double]$newMs, 3)
        NewWins = $newWins
        OldWins = $oldWins
        Ties = $ties
        NewWinPct = [math]::Round(100 * $newWins / $Rows.Count, 1)
    }
}

$sets = @(
    @{ Label = 'DefaultSmall'; Panels = 3;  Path = Join-Path $OutputDir 'thumb-bench-defaultsmall.csv' }
    @{ Label = 'Bench4';       Panels = 4;  Path = Join-Path $OutputDir 'thumb-bench-bench4.csv' }
    @{ Label = 'Bench5';       Panels = 5;  Path = Join-Path $OutputDir 'thumb-bench-bench5.csv' }
    @{ Label = 'Bench7';       Panels = 7;  Path = Join-Path $OutputDir 'thumb-bench-bench7.csv' }
    @{ Label = 'DefaultBig10'; Panels = 10; Path = Join-Path $OutputDir 'thumb-bench-v2.csv' }
)

$summaries = foreach ($s in $sets) {
    $rows = Import-BenchRows -Path $s.Path
    Summarize-Bench -Label $s.Label -Panels $s.Panels -Rows $rows
}

Write-Host '=== Layout summary (successful rows only) ==='
$summaries | Format-Table Label, Panels, Files, OverallSpeedup, NewWins, OldWins, NewWinPct -AutoSize

$bench5 = Import-BenchRows -Path (Join-Path $OutputDir 'thumb-bench-bench5.csv')
$bench7 = Import-BenchRows -Path (Join-Path $OutputDir 'thumb-bench-bench7.csv')
$small = Import-BenchRows -Path (Join-Path $OutputDir 'thumb-bench-defaultsmall.csv')

$common = foreach ($r5 in $bench5) {
    $r7 = $bench7 | Where-Object FileName -eq $r5.FileName | Select-Object -First 1
    $rs = $small | Where-Object FileName -eq $r5.FileName | Select-Object -First 1
    if ($r7) {
        [pscustomobject]@{
            FileName = $r5.FileName
            Speedup3 = if ($rs) { [double]$rs.Speedup } else { [double]::NaN }
            Speedup5 = [double]$r5.Speedup
            Speedup7 = [double]$r7.Speedup
            Duration = [double]$r5.DurationSec
            SizeGB = [double]$r5.FileSizeGB
        }
    }
}

Write-Host ''
Write-Host "=== Same 30 files across Bench5/Bench7 (Small if present) ==="
Write-Host "Count:" $common.Count

$flipAt5 = ($common | Where-Object { $_.Speedup5 -gt 1 }).Count
$flipAt7 = ($common | Where-Object { $_.Speedup7 -gt 1 }).Count
Write-Host "New faster at 5 panels: $flipAt5 / $($common.Count)"
Write-Host "New faster at 7 panels: $flipAt7 / $($common.Count)"

# Estimate crossover: interpolate between panel counts where overall speedup crosses 1.0
$pts = $summaries | Where-Object { $_.Files -gt 0 } | Sort-Object Panels
Write-Host ''
Write-Host '=== Crossover estimate (overall speedup vs panel count) ==='
foreach ($p in $pts) {
    $bias = if ($p.OverallSpeedup -gt 1) { 'new' } elseif ($p.OverallSpeedup -lt 1) { 'old' } else { 'tie' }
    Write-Host ("{0,2} panels: speedup={1} ({2} wins {3}/{4})" -f $p.Panels, $p.OverallSpeedup, $bias, $p.NewWins, $p.Files)
}

for ($i = 0; $i -lt ($pts.Count - 1); $i++) {
    $a = $pts[$i]
    $b = $pts[$i + 1]
    if (($a.OverallSpeedup -ge 1 -and $b.OverallSpeedup -le 1) -or ($a.OverallSpeedup -le 1 -and $b.OverallSpeedup -ge 1)) {
        $t = (1.0 - $a.OverallSpeedup) / ($b.OverallSpeedup - $a.OverallSpeedup)
        $cross = $a.Panels + $t * ($b.Panels - $a.Panels)
        Write-Host ("Linear crossover between {0} and {1} panels: ~{2:N1}" -f $a.Panels, $b.Panels, $cross)
    }
}

Write-Host ''
Write-Host '=== Same top-30 file set (first 30 rows from each CSV) ==='
$top30Names = ($bench5 | Select-Object -First 30).FileName
$subset = foreach ($s in $sets) {
    $all = Import-BenchRows -Path $s.Path
    $rows = $all | Where-Object { $top30Names -contains $_.FileName }
    Summarize-Bench -Label ($s.Label + ' (top30)') -Panels $s.Panels -Rows $rows
}
$subset | Format-Table Label, Panels, Files, OverallSpeedup, NewWins, OldWins, NewWinPct -AutoSize

$short = $common | Where-Object Duration -lt 7200
$long = $common | Where-Object Duration -ge 7200
Write-Host ("Short (<2h): n={0} new@5={1} new@7={2}" -f $short.Count,
    ($short | Where-Object Speedup5 -gt 1).Count,
    ($short | Where-Object Speedup7 -gt 1).Count)
Write-Host ("Long  (>=2h): n={0} new@5={1} new@7={2}" -f $long.Count,
    ($long | Where-Object Speedup5 -gt 1).Count,
    ($long | Where-Object Speedup7 -gt 1).Count)

Write-Host ''
Write-Host '=== Borderline files (speedup 0.95-1.05 at 5 or 7 panels) ==='
$common | Where-Object { $_.Speedup5 -ge 0.95 -and $_.Speedup5 -le 1.05 -or $_.Speedup7 -ge 0.95 -and $_.Speedup7 -le 1.05 } |
    Sort-Object Speedup5 |
    Select-Object -First 10 Duration, SizeGB, Speedup5, Speedup7 |
    Format-Table -AutoSize
