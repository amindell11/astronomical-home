param(
    [string]$BaseRef = "origin/main",
    [string]$ProjectPath = "src/Asteroids3D",
    [string]$OutDir = "results/resharper-ratchet",
    # Empty resolves from the project's own ProjectVersion.txt (scripts/lib/unity_editor.ps1).
    [string]$UnityPath = "",
    [int]$UnityAccessWaitSec = 900,
    [int]$UnitySyncTimeoutSec = 600,
    [switch]$Audit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
. (Join-Path $PSScriptRoot "unity_access_client.ps1")
. (Join-Path $PSScriptRoot "lib/repo_root.ps1")
. (Join-Path $PSScriptRoot "lib/unity_editor.ps1")

function Resolve-FullPath {
    param([string]$Path, [string]$Base)
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path $Base $Path))
}

function Normalize-RepoPath {
    param([string]$Path)
    return $Path.Replace('\', '/').TrimStart('./')
}

function Get-RelativePath {
    param([string]$Base, [string]$Path)
    $basePath = [System.IO.Path]::GetFullPath($Base).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $baseUri = New-Object Uri($basePath)
    $pathUri = New-Object Uri([System.IO.Path]::GetFullPath($Path))
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Get-ChangedLineMap {
    param([string]$RepoRoot, [string]$DiffBase)

    $pathspec = ":(glob)src/Asteroids3D/Assets/Scripts/**/*.cs"
    $diff = @(& git -C $RepoRoot diff --unified=0 --no-color --diff-filter=ACMR $DiffBase -- $pathspec)
    if ($LASTEXITCODE -ne 0) { throw "Could not diff $DiffBase for the ReSharper ratchet." }

    $files = @{}
    $current = ""
    foreach ($line in $diff) {
        if ($line -like "+++ b/*") {
            $current = Normalize-RepoPath $line.Substring(6)
            if (-not $files.ContainsKey($current)) { $files[$current] = New-Object 'System.Collections.Generic.HashSet[int]' }
            continue
        }
        if ([string]::IsNullOrWhiteSpace($current)) { continue }
        if ($line -match '^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@') {
            $start = [int]$Matches[1]
            $count = if ([string]::IsNullOrWhiteSpace($Matches[2])) { 1 } else { [int]$Matches[2] }
            for ($number = $start; $number -lt ($start + $count); $number++) { [void]$files[$current].Add($number) }
        }
    }
    return $files
}

function Convert-SarifPath {
    param([string]$Uri, [string]$RepoRoot, [string]$SolutionRoot)

    $decoded = [Uri]::UnescapeDataString($Uri).Replace('\', '/')
    if ($decoded -match '^file:/') {
        $absolute = ([Uri]$decoded).LocalPath
    }
    elseif ([System.IO.Path]::IsPathRooted($decoded)) {
        $absolute = $decoded
    }
    else {
        $absolute = Join-Path $SolutionRoot $decoded
    }
    $relative = Get-RelativePath $RepoRoot $absolute
    return Normalize-RepoPath $relative
}

function Test-ReportOnlyRule {
    param([string]$RuleId)
    return $RuleId -in @(
        "Unity.PerformanceCriticalCodeCameraMain",
        "Unity.PerformanceCriticalCodeInvocation",
        "Unity.PerformanceCriticalCodeNullComparison",
        "Unity.InefficientPropertyAccess",
        "Unity.InefficientMultiplicationOrder",
        "Unity.PreferAddressByIdToGraphicsParams"
    )
}

function Read-UnityFindings {
    param([string]$SarifPath, [string]$RepoRoot, [string]$SolutionRoot)

    $sarif = Get-Content -LiteralPath $SarifPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $findings = @()
    foreach ($run in @($sarif.runs)) {
        foreach ($result in @($run.results)) {
            $ruleId = [string]$result.ruleId
            if (-not $ruleId.StartsWith("Unity.", [StringComparison]::Ordinal)) { continue }
            $location = @($result.locations | Select-Object -First 1)
            if ($location.Count -eq 0) { continue }
            $physical = $location[0].physicalLocation
            if ($null -eq $physical -or $null -eq $physical.artifactLocation) { continue }
            $path = Convert-SarifPath ([string]$physical.artifactLocation.uri) $RepoRoot $SolutionRoot
            $region = $physical.region
            $startLine = if ($null -ne $region -and $null -ne $region.startLine) { [int]$region.startLine } else { 0 }
            $endLine = if ($null -ne $region -and $null -ne $region.endLine) { [int]$region.endLine } else { $startLine }
            $findings += [pscustomobject]@{
                ruleId = $ruleId
                level = [string]$result.level
                message = [string]$result.message.text
                path = $path
                startLine = $startLine
                endLine = $endLine
                reportOnly = Test-ReportOnlyRule $ruleId
            }
        }
    }
    return $findings
}

function Test-FindingTouchesChangedLine {
    param([object]$Finding, [hashtable]$ChangedLines)

    if (-not $ChangedLines.ContainsKey($Finding.path) -or $Finding.startLine -le 0) { return $false }
    for ($line = $Finding.startLine; $line -le $Finding.endLine; $line++) {
        if ($ChangedLines[$Finding.path].Contains($line)) { return $true }
    }
    return $false
}

function Sync-UnitySolution {
    param(
        [string]$RepoRoot,
        [string]$SolutionRoot,
        [string]$OutputRoot,
        [string]$UnityExe,
        [int]$WaitSeconds,
        [int]$TimeoutSeconds
    )

    $slot = [string](& git -C $RepoRoot branch --show-current | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($slot)) { $slot = "main" }
    $lease = "resharper-sync-$slot-$([guid]::NewGuid().ToString('N'))"
    $logPath = Join-Path $OutputRoot "unity-sync.log"
    $configPath = Join-Path $OutputRoot "unity-sync.json"
    $config = [ordered]@{ unityPath = $UnityExe; projectPath = $SolutionRoot; repoRoot = $RepoRoot; logPath = $logPath; timeoutSec = $TimeoutSeconds }
    [System.IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json), $Utf8NoBom)

    $coordinator = Join-Path $RepoRoot "scripts/unity_access.ps1"
    $batch = Join-Path $RepoRoot "scripts/sync_unity_solution.ps1"
    $arguments = @(
        "-Action", "RunBatch",
        "-Lease", $lease,
        "-Slot", $slot,
        "-Mode", "batch",
        "-ProjectPath", $SolutionRoot,
        "-WaitSeconds", $WaitSeconds,
        "-BatchLogPath", $logPath,
        "-BatchScript", $batch,
        "-BatchArguments", $configPath
    )
    $call = Invoke-UnityAccessCoordinator -Coordinator $coordinator -CoordinatorArgs $arguments
    $result = $call.result
    # batch_complete exits 0 even when the child failed, so the child's own exitCode is checked too.
    if ($call.exitCode -ne 0 -or $null -eq $result -or $result.status -ne "batch_complete" -or [int]$result.exitCode -ne 0) {
        throw "Unity solution synchronization failed (coordinator exit=$($call.exitCode)): $($call.stdout) $($call.stderr)"
    }
}

function Write-Summary {
    param([string]$Path, [object]$Value)
    [System.IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 8), $Utf8NoBom)
}

if ($MyInvocation.InvocationName -eq '.') { return }

$repoRoot = Get-RepoRoot -ProbePath $PSScriptRoot
$solutionRoot = Resolve-FullPath $ProjectPath $repoRoot
if ([string]::IsNullOrWhiteSpace($UnityPath)) { $UnityPath = Resolve-UnityEditorPath -ProjectPath $solutionRoot }
$outputRoot = Resolve-FullPath $OutDir $repoRoot
$solution = Join-Path $solutionRoot "Asteroids3D.sln"
$settings = Join-Path $repoRoot "scripts/resharper-unity.DotSettings"
$sarifPath = Join-Path $outputRoot "inspectcode.sarif.json"
$summaryPath = Join-Path $outputRoot "summary.json"
$cachePath = Join-Path $solutionRoot "Library/ReSharperCaches"

& git -C $repoRoot rev-parse --verify "$BaseRef^{commit}" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ReSharper ratchet base ref does not resolve: $BaseRef" }

# Diff from the merge base: a moved BaseRef must not attribute other branches' lines to this PR.
$diffBase = (& git -C $repoRoot merge-base $BaseRef HEAD)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($diffBase)) { throw "Could not compute merge-base of $BaseRef and HEAD for the ReSharper ratchet." }

$changedLines = Get-ChangedLineMap $repoRoot $diffBase
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
if ($changedLines.Count -eq 0 -and -not $Audit.IsPresent) {
    Write-Summary $summaryPath ([ordered]@{ status = "skipped"; reason = "no changed C# under Assets/Scripts"; baseRef = $BaseRef })
    Write-Host "ReSharper ratchet: no changed C# under Assets/Scripts; skipped."
    exit 0
}

Sync-UnitySolution $repoRoot $solutionRoot $outputRoot $UnityPath $UnityAccessWaitSec $UnitySyncTimeoutSec
if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) { throw "Unity did not generate $solution" }

& dotnet tool restore --tool-manifest (Join-Path $repoRoot ".config/dotnet-tools.json")
if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed." }

& dotnet tool run jb -- inspectcode $solution "--output=$sarifPath" "--settings=$settings" "--caches-home=$cachePath" "--include=Assets/Scripts/**/*.cs" "--severity=HINT" --swea --no-updates --verbosity=WARN
if ($LASTEXITCODE -ne 0) { throw "InspectCode failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $sarifPath -PathType Leaf)) { throw "InspectCode did not write $sarifPath" }

$findings = @(Read-UnityFindings $sarifPath $repoRoot $solutionRoot)
$blocking = @($findings | Where-Object {
    -not $_.reportOnly -and $_.level -in @("error", "warning") -and (Test-FindingTouchesChangedLine $_ $changedLines)
})
$touchedFileFindings = @($findings | Where-Object { $changedLines.ContainsKey($_.path) })
$byRule = @($findings | Group-Object ruleId | Sort-Object Count -Descending | ForEach-Object {
    [ordered]@{ ruleId = $_.Name; count = $_.Count }
})
$status = if ($Audit.IsPresent) { "audit" } elseif ($blocking.Count -gt 0) { "failed" } else { "passed" }
Write-Summary $summaryPath ([ordered]@{
    status = $status
    baseRef = $BaseRef
    changedFiles = $changedLines.Count
    unityFindings = $findings.Count
    touchedFileFindings = $touchedFileFindings.Count
    blockingFindings = $blocking.Count
    findingsByRule = $byRule
})

if ($Audit.IsPresent) {
    Write-Host "ReSharper Unity audit: $($findings.Count) findings across $($byRule.Count) rules."
    $byRule | Select-Object -First 20 | ForEach-Object { Write-Host "  $($_.count) $($_.ruleId)" }
    exit 0
}

foreach ($finding in $touchedFileFindings) {
    $kind = if ($blocking -contains $finding) { "BLOCK" } else { "report" }
    Write-Host "$kind $($finding.path):$($finding.startLine) [$($finding.ruleId)] $($finding.message)"
}
if ($blocking.Count -gt 0) {
    Write-Error "ReSharper ratchet failed: $($blocking.Count) warning/error finding(s) overlap PR-changed lines."
    exit 1
}
Write-Host "ReSharper ratchet passed: no blocking Unity findings on changed lines ($($touchedFileFindings.Count) report-only/touched-file findings)."
