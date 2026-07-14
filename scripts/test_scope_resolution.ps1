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

Write-Host "Scope map loading and name-filter resolution (features/smoke/workspace)"
$scopeMap = Load-ScopeMap -Path (Join-Path $PSScriptRoot "unity_test_scopes.json")
Assert-True ($null -ne $scopeMap) "scope map loads"

$smokeFilter = Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Smoke" -ScopeName ""
Assert-True (-not [string]::IsNullOrWhiteSpace($smokeFilter)) "Smoke scope resolves to a non-empty filter"
Assert-True (-not [string]::IsNullOrWhiteSpace((Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Feature" -ScopeName "camera"))) "Feature/camera resolves"
Assert-Equal "" (Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Workspace" -ScopeName "") "Workspace resolves to empty filter"
Assert-Equal "" (Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Feature" -ScopeName "nonexistent") "invalid feature resolves to empty filter (warning expected above)"
Assert-Equal "" (Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Module" -ScopeName "ai") "Module no longer resolves via name-filter (derives categories instead)"

# Modules carry paths only; category selection is derived from the fixtures those paths cover.
$syntheticMapJson = @'
{
  "smoke": { "testFilter": "SmokeA|SmokeB" },
  "features": {},
  "modules": {
    "alpha": { "paths": ["src/Alpha/**", "tests/AlphaFixture.cs*"] },
    "beta": { "paths": ["src/Beta/**"] },
    "gamma": { "paths": ["src/Gamma/**"] },
    "workspace": { "testFilter": "" }
  }
}
'@
$syntheticMap = $syntheticMapJson | ConvertFrom-Json

# file (repo-relative) -> its [Category] tags. Overlays (Smoke/Slow) present to prove they never seed a scope.
$fakeIndex = [ordered]@{
    "src/Alpha/AlphaTests.cs"      = @("Weapons")
    "tests/AlphaFixture.cs"        = @("Weapons", "Slow")
    "src/Beta/Deep/BetaTests.cs"   = @("Sectors")
    "src/Beta/BetaSmokeTests.cs"   = @("Smoke")
}

Write-Host ""
Write-Host "Auto: single module match derives its fixtures' domain categories + smoke"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src/Alpha/Core/Thing.cs") -FileCategoryIndex $fakeIndex
Assert-Equal "modules" $auto.mode "mode"
Assert-Equal "alpha" (@($auto.matchedModules) -join ",") "matched modules"
Assert-Equal "Smoke;Weapons" $auto.testCategory "testCategory (sorted, Slow dropped, Smoke always added)"

Write-Host ""
Write-Host "Auto: multi-module union deduplicates and stays sorted"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src/Alpha/A.cs", "src/Beta/Deep/B.cs") -FileCategoryIndex $fakeIndex
Assert-Equal "modules" $auto.mode "mode"
Assert-Equal "alpha,beta" (@($auto.matchedModules) -join ",") "matched modules"
Assert-Equal "Sectors;Smoke;Weapons" $auto.testCategory "union of domains + smoke, sorted"

Write-Host ""
Write-Host "Auto: an overlay-only fixture never seeds a domain, but its module's other fixtures do"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src/Beta/BetaSmokeTests.cs") -FileCategoryIndex $fakeIndex
Assert-Equal "modules" $auto.mode "beta smoke-only file matches beta which also has BetaTests(Sectors)"
Assert-Equal "Sectors;Smoke" $auto.testCategory "beta derives Sectors (its other fixture) + smoke"

Write-Host ""
Write-Host "Auto: any unmatched file falls back to full Workspace"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src/Alpha/A.cs", "scripts/foo.ps1") -FileCategoryIndex $fakeIndex
Assert-Equal "fallback" $auto.mode "mode"
Assert-Equal "scripts/foo.ps1" (@($auto.unmatchedFiles) -join ",") "unmatched files reported"
Assert-Equal "" $auto.testCategory "fallback category empty (full suite)"

Write-Host ""
Write-Host "Auto: matched module whose paths cover no tagged fixture falls back (never under-test)"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src/Gamma/G.cs") -FileCategoryIndex $fakeIndex
Assert-Equal "fallback" $auto.mode "mode"
Assert-Equal "" $auto.testCategory "fallback category"
Assert-Equal "gamma" (@($auto.emptyCategoryModules) -join ",") "empty-category module named (not blamed on globs)"
Assert-Equal 0 (@($auto.unmatchedFiles).Count) "no files reported unmatched"

Write-Host ""
Write-Host "Auto: md/doc/.claude-only diffs run the Smoke category only, with the ignored files reported"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("README.md", "doc/Feature_Plans/Plan.md", ".claude/settings.local.json", "doc/notes/raw.txt") -FileCategoryIndex $fakeIndex
Assert-Equal "smoke" $auto.mode "mode"
Assert-Equal "Smoke" $auto.testCategory "testCategory"
Assert-Equal 0 (@($auto.consideredFiles).Count) "no files considered"
Assert-Equal 4 (@($auto.ignoredFiles).Count) "all four files reported as ignored"

Write-Host ""
Write-Host "Auto: ignore list is surfaced, and non-excluded tooling files still force the fallback"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @(".claude/skills/x/tool.ps1", "README.md", "scripts/foo.ps1") -FileCategoryIndex $fakeIndex
Assert-Equal "fallback" $auto.mode "scripts/foo.ps1 is NOT ignored and forces full fallback"
Assert-Equal "scripts/foo.ps1" (@($auto.unmatchedFiles) -join ",") "only the tooling file is unmatched"
Assert-True (@($auto.ignoredFiles) -contains ".claude/skills/x/tool.ps1") "nested .claude file ignored by design"
Assert-True (@($auto.ignoredFiles) -contains "README.md") "md file ignored by design"

Write-Host ""
Write-Host "Auto: no changed files at all runs the Smoke category only"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @() -FileCategoryIndex $fakeIndex
Assert-Equal "smoke" $auto.mode "mode"
Assert-Equal "Smoke" $auto.testCategory "testCategory"

Write-Host ""
Write-Host "Auto: nested paths, backslashes, and ./ prefixes normalize before glob matching"
$auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles @("src\Alpha\Deep\Nested\File.cs", "./src/Beta/X.cs", "tests\AlphaFixture.cs.meta") -FileCategoryIndex $fakeIndex
Assert-Equal "modules" $auto.mode "mode"
Assert-Equal "alpha,beta" (@($auto.matchedModules) -join ",") "matched modules"

Write-Host ""
Write-Host "Get-ModuleDerivedCategories: unknown module -> empty"
Assert-Equal 0 (@(Get-ModuleDerivedCategories -ScopeMap $syntheticMap -ModuleName "nope" -FileCategoryIndex $fakeIndex).Count) "unknown module derives nothing"

# Fixture-category index built from real files on disk.
$idxRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("scope-idx-" + [guid]::NewGuid().ToString("N"))
$idxRepo = Join-Path $idxRoot "repo"
$idxTests = Join-Path $idxRepo "src/Tests/EditMode"
New-Item -ItemType Directory -Force -Path $idxTests | Out-Null
try {
    Write-Host ""
    Write-Host "Get-TestFileCategoryIndex: parses [Category(...)] tags, keys repo-relative, skips untagged files"
    Set-Content -LiteralPath (Join-Path $idxTests "FooTests.cs") -Value "[Category(`"AI`")]`n[ Category ( `"Smoke`" ) ]`npublic class FooTests {}" -Encoding Ascii
    Set-Content -LiteralPath (Join-Path $idxTests "BarTests.cs") -Value "[Category(`"Weapons`")]" -Encoding Ascii
    Set-Content -LiteralPath (Join-Path $idxTests "PlainThing.cs") -Value "public class PlainThing {}" -Encoding Ascii
    $index = Get-TestFileCategoryIndex -TestsRoot $idxTests -RepoRoot $idxRepo
    Assert-True ($index.Contains("src/Tests/EditMode/FooTests.cs")) "index key is repo-relative forward-slash"
    Assert-Equal "AI,Smoke" (@($index["src/Tests/EditMode/FooTests.cs"]) -join ",") "FooTests categories (whitespace-tolerant regex)"
    Assert-Equal "Weapons" (@($index["src/Tests/EditMode/BarTests.cs"]) -join ",") "BarTests categories"
    Assert-True (-not $index.Contains("src/Tests/EditMode/PlainThing.cs")) "untagged file skipped"
}
finally {
    Remove-Item -LiteralPath $idxRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Auto against the real scope map + real fixture tags on disk"
$realRepo = Get-RepoRoot -ProbePath $PSScriptRoot
$realTests = Join-Path $realRepo "src/Asteroids3D/Assets/Scripts/Editor/Tests"
$realIndex = Get-TestFileCategoryIndex -TestsRoot $realTests -RepoRoot $realRepo
Assert-True (@($realIndex.Keys).Count -gt 0 ) "real fixture index is non-empty"

$auto = Resolve-AutoSelection -ScopeMap $scopeMap -ChangedFiles @("src/Asteroids3D/Assets/Scripts/AI/Navigation/MPC/Cost.cs") -FileCategoryIndex $realIndex
Assert-Equal "modules" $auto.mode "MPC source file resolves to modules"
Assert-True (@($auto.matchedModules) -contains "mpc") "MPC source maps to mpc"
Assert-True (@($auto.matchedModules) -contains "ai") "MPC source maps to ai"
Assert-True (@($auto.categories) -contains "MPC") "derived categories include MPC (incl. retagged NavField/MultiSphere)"
Assert-True (@($auto.categories) -contains "AI") "derived categories include AI (NavFieldService lives under mpc paths)"
Assert-True (@($auto.categories) -contains "Smoke") "smoke category always added"
Assert-True (@($auto.categories) -notcontains "Planning") "retagged: Planning no longer appears"

$auto = Resolve-AutoSelection -ScopeMap $scopeMap -ChangedFiles @("src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/ObjectiveTrackerEditModeTests.cs") -FileCategoryIndex $realIndex
Assert-Equal "modules" $auto.mode "objective test fixture resolves to modules"
Assert-Equal "objectives" (@($auto.matchedModules) -join ",") "objective fixture maps to objectives"
Assert-Equal "Objectives;Smoke" $auto.testCategory "objectives derives Objectives + smoke"

$auto = Resolve-AutoSelection -ScopeMap $scopeMap -ChangedFiles @("src/Asteroids3D/Assets/Scripts/Ships/Ship.cs") -FileCategoryIndex $realIndex
Assert-Equal "fallback" $auto.mode "unmapped core source (Ships/Ship.cs) falls back to full suite"

$auto = Resolve-AutoSelection -ScopeMap $scopeMap -ChangedFiles @("scripts/unity_test_agent.ps1") -FileCategoryIndex $realIndex
Assert-Equal "fallback" $auto.mode "tooling script falls back to full suite"

function Invoke-GitSetup {
    param([string[]]$GitArgs)
    & git @GitArgs
    if ($LASTEXITCODE -ne 0) {
        throw "test setup: 'git $($GitArgs -join ' ')' exited $LASTEXITCODE"
    }
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("scope-auto-test-" + [guid]::NewGuid().ToString("N"))
$repo = Join-Path $tempRoot "repo"
New-Item -ItemType Directory -Force -Path (Join-Path $repo "src/Ships") | Out-Null
try {
    Write-Host ""
    Write-Host "Auto: a git rename surfaces BOTH paths (--no-renames)"
    Invoke-GitSetup @("init", "-q", $repo)
    Set-Content -LiteralPath (Join-Path $repo "src/Ships/Ship.cs") -Value "class Ship {}" -Encoding Ascii
    Invoke-GitSetup @("-C", $repo, "add", "src")
    Invoke-GitSetup @("-C", $repo, "-c", "user.email=test@test", "-c", "user.name=test", "-c", "commit.gpgsign=false", "-c", "core.hooksPath=", "commit", "-q", "-m", "init")
    New-Item -ItemType Directory -Force -Path (Join-Path $repo "src/Alpha") | Out-Null
    Invoke-GitSetup @("-C", $repo, "mv", "src/Ships/Ship.cs", "src/Alpha/Ship.cs")
    $changed = Get-AutoChangedFiles -RepoProbePath $repo -BaseRef HEAD
    Assert-True (@($changed.files) -contains "src/Ships/Ship.cs") "rename source path reported"
    Assert-True (@($changed.files) -contains "src/Alpha/Ship.cs") "rename destination path reported"
    Assert-Equal (Resolve-FullPath $repo) (Resolve-FullPath $changed.repoRoot) "repoRoot returned for index keying"
    $auto = Resolve-AutoSelection -ScopeMap $syntheticMap -ChangedFiles $changed.files -FileCategoryIndex $fakeIndex
    Assert-Equal "fallback" $auto.mode "moving an unmapped file into a mapped dir still falls back"

    Write-Host ""
    Write-Host "Runner: -ScopeType Auto rejects manual selection args"
    $runner = Join-Path $PSScriptRoot "unity_test_agent.ps1"
    $output = & cmd /c "powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -ScopeType Auto -TestFilter Foo -SkipUnityAccess 2>&1"
    Assert-True ($LASTEXITCODE -ne 0) "rejection exits nonzero"
    Assert-True (($output -join ' ') -like '*cannot be combined with -TestFilter*') "rejection names the conflicting arg"
    Assert-True (($output -join ' ') -notlike '*Auto scope resolution*') "rejected run never reaches resolution"
    $output = & cmd /c "powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -ScopeType Auto -TestCategory AI -SkipUnityAccess 2>&1"
    Assert-True ($LASTEXITCODE -ne 0 -and ($output -join ' ') -like '*-TestCategory*') "rejection also covers -TestCategory"
    $output = & cmd /c "powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -ScopeType Auto -RerunFailedFrom x.json -SkipUnityAccess 2>&1"
    Assert-True ($LASTEXITCODE -ne 0 -and ($output -join ' ') -like '*-RerunFailedFrom*') "rejection also covers -RerunFailedFrom"

    Write-Host ""
    Write-Host "Runner: Auto fallback is a true full Workspace run end-to-end (stubbed Unity exe)"
    $mapPath = Join-Path $tempRoot "map.json"
    Set-Content -LiteralPath $mapPath -Value $syntheticMapJson -Encoding Ascii
    $outDir = Join-Path $tempRoot "out"
    $stubUnity = Join-Path $env:SystemRoot "System32\where.exe"
    $output = & cmd /c "powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -ScopeType Auto -DiffBase HEAD -Mode EditMode -SkipUnityAccess -UnityPath $stubUnity -ProjectPath $repo -ScopeMapPath $mapPath -OutDir $outDir 2>&1"
    $joined = $output -join "`n"
    Assert-True ($joined -like '*AUTO SCOPE FALLBACK*') "fallback banner printed"
    Assert-True ($joined -like '*UNMATCHED*src/Ships/Ship.cs*') "rename source printed as unmatched"
    $summary = Get-Content -LiteralPath (Join-Path $outDir "latest-summary.json") -Raw | ConvertFrom-Json
    Assert-Equal "fallback" $summary.selection.auto.mode "summary records fallback"
    Assert-Equal "" $summary.selection.testFilter "summary testFilter empty (full suite)"
    Assert-Equal "" $summary.selection.testCategory "no category narrowing"
    Assert-Equal "" $summary.selection.assemblyNames "no assembly narrowing"
    Assert-Equal "" $summary.selection.orderedTestListFile "no ordered-list narrowing"
    Assert-Equal "" $summary.selection.rerunFailedFrom "no rerun narrowing"
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "========================================"
Write-Host ("{0}/{1} scope resolution assertions passed" -f ($script:testCount - $script:failCount), $script:testCount)
Write-Host "========================================"
if ($script:failCount -gt 0) { exit 1 }
exit 0
