<#
.SYNOPSIS
  Diff two chase-benchmark JSONL result sets (baseline vs candidate) as per-metric
  mean ± spread over the run rows. Headline metrics are per-ship (collisions, impact
  impulse, mean speed, control chatter, solve time); relational metrics (distance-behind,
  intercept) are secondary context — the evader changes build-to-build too.

.EXAMPLE
  pwsh scripts/benchmark_diff.ps1 -Baseline results/chase-benchmark/baseline_runs.jsonl `
                                  -Candidate results/chase-benchmark/latest_runs.jsonl
#>
param(
    [Parameter(Mandatory = $true)][string]$Baseline,
    [Parameter(Mandatory = $true)][string]$Candidate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Load-Rows([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { throw "results file not found: $path" }
    Get-Content -LiteralPath $path |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_ | ConvertFrom-Json }
}

function Stat($values) {
    $arr = @($values)
    $n = $arr.Count
    if ($n -eq 0) { return [pscustomobject]@{ mean = 0.0; std = 0.0; n = 0 } }
    $mean = ($arr | Measure-Object -Average).Average
    $var = 0.0
    foreach ($v in $arr) { $var += [math]::Pow(($v - $mean), 2) }
    $std = if ($n -gt 1) { [math]::Sqrt($var / ($n - 1)) } else { 0.0 }
    [pscustomobject]@{ mean = [double]$mean; std = [double]$std; n = $n }
}

$metrics = @(
    @{ name = "pursuer.collisions";     sel = { param($r) $r.pursuer.collisions } },
    @{ name = "pursuer.impactImpulse";  sel = { param($r) $r.pursuer.impactImpulse } },
    @{ name = "pursuer.meanSpeed";      sel = { param($r) $r.pursuer.meanSpeed } },
    @{ name = "pursuer.chatterPerSec";  sel = { param($r) $r.pursuer.chatterPerSec } },
    @{ name = "pursuer.meanSolveMs";    sel = { param($r) $r.pursuer.meanSolveMs } },
    @{ name = "evader.collisions";      sel = { param($r) $r.evader.collisions } },
    @{ name = "evader.impactImpulse";   sel = { param($r) $r.evader.impactImpulse } },
    @{ name = "evader.meanSpeed";       sel = { param($r) $r.evader.meanSpeed } },
    @{ name = "evader.chatterPerSec";   sel = { param($r) $r.evader.chatterPerSec } },
    @{ name = "minDistance";            sel = { param($r) $r.minDistance } },
    @{ name = "meanDistanceBehind";     sel = { param($r) $r.meanDistanceBehind } },
    @{ name = "interceptRate";          sel = { param($r) if ($r.timeToInterceptSec -ge 0) { 1 } else { 0 } } }
)

$base = Load-Rows $Baseline
$cand = Load-Rows $Candidate

Write-Host ("Baseline : {0} rows  ({1})" -f @($base).Count, $Baseline)
Write-Host ("Candidate: {0} rows  ({1})" -f @($cand).Count, $Candidate)
Write-Host ""
Write-Host ("{0,-24} {1,20} {2,20} {3,12}" -f "metric", "baseline (mean+/-std)", "candidate (mean+/-std)", "delta")
Write-Host ("-" * 80)

foreach ($m in $metrics) {
    $b = Stat ($base | ForEach-Object { & $m.sel $_ })
    $c = Stat ($cand | ForEach-Object { & $m.sel $_ })
    $delta = $c.mean - $b.mean
    Write-Host ("{0,-24} {1,11:F3} +/- {2,-6:F3} {3,11:F3} +/- {4,-6:F3} {5,12:F3}" -f `
        $m.name, $b.mean, $b.std, $c.mean, $c.std, $delta)
}
