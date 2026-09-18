Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot "../unity_test_agent.ps1"), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw $errors[0] }
$function = $ast.Find({ param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
    $node.Name -eq 'Get-RunWallTiming'
}, $true)
Invoke-Expression $function.Extent.Text
function Assert-Equal($Expected, $Actual) {
    if ($Expected -ne $Actual) { throw "Expected $Expected, got $Actual" }
}
$start = [DateTimeOffset]::Parse('2026-09-09T00:00:00Z')
$runs = @(
    [ordered]@{ startedAt = '2026-09-09T00:00:10Z'; finishedAt = '2026-09-09T00:00:20Z'; durationSec = 7.5 },
    [ordered]@{ startedAt = '2026-09-09T00:00:25Z'; finishedAt = '2026-09-09T00:00:40Z'; durationSec = 12.3 }
)
$params = @{ StartedAt = $start; FinishedAt = $start.AddSeconds(50); Runs = $runs
    Processes = @(@{ launchedAt = $start.AddSeconds(2); firstRun = 0 }); Routed = $false }
$t = Get-RunWallTiming @params
Assert-Equal 8 $t.startupToFirstTestSec
Assert-Equal 25 $t.executionSec
Assert-Equal 17 $t.remainingSec
Assert-Equal $t.elapsedSec ($t.startupToFirstTestSec + $t.executionSec + $t.remainingSec)
Assert-Equal 7.5 $runs[0].durationSec
$params.Processes += @{ launchedAt = $start.AddSeconds(22); firstRun = 1 }
$t = Get-RunWallTiming @params
Assert-Equal 11 $t.startupToFirstTestSec
Assert-Equal 14 $t.remainingSec
foreach ($bad in @('', 'not-a-date', '2026-09-09T00:00:15Z', '2026-09-09T00:01:00Z')) {
    $runs[1].startedAt = $bad
    $t = Get-RunWallTiming @params
    Assert-Equal $null $t.executionSec
    if (-not $t.unavailableReason) { throw 'Missing unavailable reason' }
}
$params.Runs = @([ordered]@{ durationSec = 0 })
Assert-Equal $null (Get-RunWallTiming @params).startupToFirstTestSec
$params.Routed = $true
$t = Get-RunWallTiming @params
Assert-Equal 50 $t.elapsedSec
Assert-Equal $null $t.startupToFirstTestSec
Write-Host 'PASS: runner wall intervals, multi-process attribution, unavailable timestamps and routed transport'
