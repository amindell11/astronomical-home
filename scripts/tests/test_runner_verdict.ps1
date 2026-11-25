# covers: scripts/unity_test_agent.ps1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot "../unity_test_agent.ps1"), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw $errors[0] }
$function = $ast.Find({ param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
    $node.Name -eq 'Get-OverallStatus'
}, $true)
Invoke-Expression $function.Extent.Text
function Assert-Verdict([string]$Case, [string]$Expected, [int]$Passed, [int]$Failed, [int]$Skipped, [bool]$InfraError = $false) {
    $out = @(Get-OverallStatus -Passed $Passed -Failed $Failed -Skipped $Skipped -HasInfraError $InfraError 6>&1)
    $status = @($out | Where-Object { $_ -is [string] })
    $lines = @($out | Where-Object { $_ -is [System.Management.Automation.InformationRecord] } | ForEach-Object { "$_" })
    if ($status.Count -ne 1 -or $status[0] -ne $Expected) { throw "${Case}: expected $Expected, got '$($status -join ',')'" }
    return $lines
}
$lines = Assert-Verdict 'all-skipped' 'failed' -Passed 0 -Failed 0 -Skipped 3
if (-not ($lines -match '^\[FAIL\] .*\b3 skipped\b')) { throw "all-skipped: no [FAIL] line naming 3 skipped: $lines" }
$lines = Assert-Verdict 'zero-test' 'failed' -Passed 0 -Failed 0 -Skipped 0
if (-not ($lines -match '^\[FAIL\] .*\b0 skipped\b')) { throw "zero-test: no [FAIL] line naming 0 skipped: $lines" }
$lines = Assert-Verdict 'green' 'passed' -Passed 5 -Failed 0 -Skipped 2
if ($lines) { throw "green: unexpected output: $lines" }
$null = Assert-Verdict 'test failure' 'failed' -Passed 4 -Failed 1 -Skipped 0
$null = Assert-Verdict 'infra error' 'infra_error' -Passed 0 -Failed 0 -Skipped 0 -InfraError $true
Write-Host 'PASS: all-skipped and zero-test runs are failed with a [FAIL] line; green passes; infra_error wins'
