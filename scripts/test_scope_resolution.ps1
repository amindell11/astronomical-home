Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "unity_test_scope_lib.ps1")

$script:testCount = 0
$script:failCount = 0

function Assert-True {
    param([bool]$Condition, [string]$Name)
    $script:testCount++
    if ($Condition) {
        Write-Host "  PASSED: $Name"
    }
    else {
        Write-Host "  FAILED: $Name"
        $script:failCount++
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Name)
    Assert-True ([string]$Expected -eq [string]$Actual) "$Name (expected '$Expected', got '$Actual')"
}

Write-Host "Scope map loading and filter resolution"
$scopeMap = Load-ScopeMap -Path (Join-Path $PSScriptRoot "unity_test_scopes.json")
Assert-True ($null -ne $scopeMap) "scope map loads"

$smokeFilter = Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Smoke" -ScopeName ""
Assert-True (-not [string]::IsNullOrWhiteSpace($smokeFilter)) "Smoke scope resolves to a non-empty filter"
Assert-True (-not [string]::IsNullOrWhiteSpace((Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Feature" -ScopeName "camera"))) "Feature/camera resolves"
Assert-True (-not [string]::IsNullOrWhiteSpace((Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Module" -ScopeName "ai"))) "Module/ai resolves"
Assert-Equal "" (Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Workspace" -ScopeName "") "Workspace resolves to empty filter"
Assert-Equal "" (Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Feature" -ScopeName "nonexistent") "invalid feature resolves to empty filter (warning expected above)"

$syntheticMap = @'
{
  "smoke": { "testFilter": "SmokeA|SmokeB" },
  "features": {},
  "modules": {
    "alpha": { "testFilter": "AlphaTests|SharedTests", "paths": ["src/Alpha/**", "tests/AlphaFixture.cs*"] },
    "beta": { "testFilter": "BetaTests|SharedTests", "paths": ["src/Beta/**"] },
    "gamma": { "testFilter": "", "paths": ["src/Gamma/**"] },
    "workspace": { "testFilter": "" }
  }
}
'@ | ConvertFrom-Json

Write-Host ""
Write-Host "Auto: single module match unions module filter with smoke"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src/Alpha/Core/Thing.cs")
Assert-Equal "modules" $auto.mode "mode"
Assert-Equal "alpha" (@($auto.matchedModules) -join ",") "matched modules"
Assert-Equal "AlphaTests|SharedTests|SmokeA|SmokeB" $auto.testFilter "filter"

Write-Host ""
Write-Host "Auto: multi-module union deduplicates shared terms"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src/Alpha/A.cs", "src/Beta/Deep/B.cs")
Assert-Equal "modules" $auto.mode "mode"
Assert-Equal "alpha,beta" (@($auto.matchedModules) -join ",") "matched modules"
Assert-Equal "AlphaTests|SharedTests|BetaTests|SmokeA|SmokeB" $auto.testFilter "filter dedupes SharedTests"

Write-Host ""
Write-Host "Auto: any unmatched file falls back to full Workspace"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src/Alpha/A.cs", "scripts/foo.ps1")
Assert-Equal "fallback" $auto.mode "mode"
Assert-Equal "scripts/foo.ps1" (@($auto.unmatchedFiles) -join ",") "unmatched files reported"
Assert-Equal "" $auto.testFilter "fallback filter is Workspace (empty = full suite)"

Write-Host ""
Write-Host "Auto: matched module with empty testFilter falls back (never under-test)"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src/Gamma/G.cs")
Assert-Equal "fallback" $auto.mode "mode"
Assert-Equal "" $auto.testFilter "fallback filter"

Write-Host ""
Write-Host "Auto: md/doc/.claude-only diffs run smoke only"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("README.md", "doc/Feature_Plans/Plan.md", ".claude/settings.local.json", "doc/notes/raw.txt")
Assert-Equal "smoke" $auto.mode "mode"
Assert-Equal "SmokeA|SmokeB" $auto.testFilter "filter"
Assert-Equal 0 (@($auto.consideredFiles).Count) "no files considered"

Write-Host ""
Write-Host "Auto: no changed files at all runs smoke only"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @()
Assert-Equal "smoke" $auto.mode "mode"
Assert-Equal "SmokeA|SmokeB" $auto.testFilter "filter"

Write-Host ""
Write-Host "Auto: nested paths, backslashes, and ./ prefixes normalize before glob matching"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src\Alpha\Deep\Nested\File.cs", "./src/Beta/X.cs", "tests\AlphaFixture.cs.meta")
Assert-Equal "modules" $auto.mode "mode"
Assert-Equal "alpha,beta" (@($auto.matchedModules) -join ",") "matched modules"

Write-Host ""
Write-Host "Auto against the real scope map"
$auto = Resolve-AutoSelection -ScopeMap $scopeMap -ChangedFiles @("src/Asteroids3D/Assets/Scripts/AI/Navigation/MPC/Cost.cs")
Assert-Equal "modules" $auto.mode "MPC source file resolves to modules"
Assert-True (@($auto.matchedModules) -contains "mpc") "MPC source maps to mpc"
Assert-True (@($auto.matchedModules) -contains "ai") "MPC source maps to ai"
Assert-True ($auto.testFilter -like "*MpcSolverTests*") "filter includes MpcSolverTests"
Assert-True ($auto.testFilter -like "*CameraUtilsEditModeTests*") "filter includes smoke tests"

$auto = Resolve-AutoSelection -ScopeMap $scopeMap -ChangedFiles @("src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/ObjectiveTrackerEditModeTests.cs")
Assert-Equal "modules" $auto.mode "objective test fixture resolves to modules"
Assert-Equal "objectives" (@($auto.matchedModules) -join ",") "objective fixture maps to objectives"

$auto = Resolve-AutoSelection -ScopeMap $scopeMap -ChangedFiles @("src/Asteroids3D/Assets/Scripts/Ships/Ship.cs")
Assert-Equal "fallback" $auto.mode "unmapped core source (Ships/Ship.cs) falls back to full suite"

$auto = Resolve-AutoSelection -ScopeMap $scopeMap -ChangedFiles @("scripts/unity_test_agent.ps1")
Assert-Equal "fallback" $auto.mode "tooling script falls back to full suite"

Write-Host ""
Write-Host "========================================"
Write-Host ("{0}/{1} scope resolution assertions passed" -f ($script:testCount - $script:failCount), $script:testCount)
Write-Host "========================================"
if ($script:failCount -gt 0) { exit 1 }
exit 0
