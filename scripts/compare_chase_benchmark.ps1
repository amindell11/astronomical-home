param(
    [Parameter(Mandatory = $true)]
    [string]$Baseline,

    [Parameter(Mandatory = $true)]
    [string]$Candidate
)

function Read-JsonLines {
    param([string]$Path)
    Get-Content -LiteralPath $Path | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object {
        $_ | ConvertFrom-Json
    }
}

function Summarize {
    param($Rows)
    $metrics = @(
        "interceptTimeSec",
        "finalSeparation",
        "meanSeparation",
        "minSeparation",
        "pursuerMeanSpeed",
        "evaderMeanSpeed",
        "pursuerCollisions",
        "evaderCollisions",
        "pursuerImpactImpulse",
        "evaderImpactImpulse",
        "pursuerMeanSolveMs",
        "pursuerControlChatterPerSec"
    )

    $summary = [ordered]@{}
    foreach ($metric in $metrics) {
        $values = @($Rows | ForEach-Object { [double]$_.$metric })
        if ($values.Count -eq 0) { continue }
        $mean = ($values | Measure-Object -Average).Average
        $spread = if ($values.Count -gt 1) {
            $sumSq = 0.0
            foreach ($value in $values) { $sumSq += [math]::Pow($value - $mean, 2) }
            [math]::Sqrt($sumSq / ($values.Count - 1))
        } else {
            0.0
        }
        $summary[$metric] = [pscustomobject]@{
            mean = $mean
            spread = $spread
        }
    }
    $summary
}

$baselineRows = @(Read-JsonLines -Path $Baseline)
$candidateRows = @(Read-JsonLines -Path $Candidate)
if ($baselineRows.Count -eq 0) { throw "Baseline file has no JSONL rows: $Baseline" }
if ($candidateRows.Count -eq 0) { throw "Candidate file has no JSONL rows: $Candidate" }

$baseSummary = Summarize -Rows $baselineRows
$candidateSummary = Summarize -Rows $candidateRows
$culture = [System.Globalization.CultureInfo]::InvariantCulture

"metric,baselineMean,baselineSpread,candidateMean,candidateSpread,delta"
foreach ($metric in $baseSummary.Keys) {
    $b = $baseSummary[$metric]
    $c = $candidateSummary[$metric]
    $delta = $c.mean - $b.mean
    "{0},{1},{2},{3},{4},{5}" -f $metric,
        $b.mean.ToString("F4", $culture),
        $b.spread.ToString("F4", $culture),
        $c.mean.ToString("F4", $culture),
        $c.spread.ToString("F4", $culture),
        $delta.ToString("F4", $culture)
}
