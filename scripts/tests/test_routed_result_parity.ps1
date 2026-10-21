# covers: scripts/unity_test_agent.ps1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot "../unity_test_agent.ps1"), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw $errors[0] }
foreach ($name in @('Invoke-RoutedPlatformRun', 'New-RunRecord', 'Get-JsonProp')) {
    $function = $ast.Find({ param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    Invoke-Expression $function.Extent.Text
}
function Assert-Equal($Expected, $Actual, $What) {
    if ($Expected -ne $Actual) { throw "${What}: expected '$Expected', got '$Actual'" }
}

$UnityTimeoutSec = 60
$MaxFailures = 10
$MaxMessageLength = 400
$IncludeStackTrace = $false
function Wait-RoutedEditorReady { param($TimeoutSec, $What) }
function Invoke-PipelineCommand { param($CommandName, $CommandParams, $What)
    return @{ ok = $true; result = [pscustomobject]@{ result = "running" }; text = "" }
}
$Script:completions = @()
function Wait-RoutedTestCompletion { param($TimeoutSec, $What)
    $next = $Script:completions[0]
    $Script:completions = @($Script:completions | Select-Object -Skip 1)
    return $next
}
function New-Completion([int]$Total, [string[]]$Names) {
    $results = @($Names | ForEach-Object { @{ FullName = $_; Status = "Passed"; Duration = 0.1 } })
    return (@{ status = "completed"; duration = 1.0; summary = @{ total = $Total; passed = $Total; failed = 0 }; results = $results } |
        ConvertTo-Json -Depth 5 | ConvertFrom-Json)
}
function New-Plan([string[]]$Categories, [string[]]$Expected) {
    $expectedSet = @{}
    foreach ($name in $Expected) { $expectedSet[$name] = $true }
    return [ordered]@{ platform = "EditMode"; pipelineMode = "editor"
        calls = @($Categories | ForEach-Object { @{ filter = $_; filterType = "category" } }); expected = $expectedSet }
}

# Mid-run domain reload: only post-reload results survive beside a full summary.
$Script:completions = @(New-Completion 3 @("T.C"))
$run = Invoke-RoutedPlatformRun -Plan (New-Plan @("Sectors") @("T.A", "T.B", "T.C")) -Selection $null
Assert-Equal "infra_error" $run.status "dropped results"
if ($run.note -notlike "run_tests EditMode category=Sectors: the pipeline returned 1 per-test result(s) for a 3-test run*domain reload*") {
    throw "dropped results: the note must lead with the named cause, got: $($run.note)"
}

# A second include-category call can re-cover the lost names, so parity alone would pass.
$Script:completions = @((New-Completion 2 @("T.B")), (New-Completion 2 @("T.A", "T.B")))
$run = Invoke-RoutedPlatformRun -Plan (New-Plan @("Sectors", "Camera") @("T.A", "T.B")) -Selection $null
Assert-Equal "infra_error" $run.status "dropped results masked by parity"

$Script:completions = @(New-Completion 2 @("T.A", "T.B"))
$run = Invoke-RoutedPlatformRun -Plan (New-Plan @("Sectors") @("T.A", "T.B")) -Selection $null
Assert-Equal "passed" $run.status "complete results"
Assert-Equal 2 $run.total "complete results total"
Write-Host 'PASS: routed result parity flags pipeline-dropped results, including ones parity would mask'
