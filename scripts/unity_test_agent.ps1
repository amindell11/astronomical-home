param(
    [string]$UnityPath = "D:\Programs\Unity\Editor\6000.1.8f1\Editor\Unity.exe",
    [string]$ProjectPath = "src/Asteroids3D",
    [string]$OutDir = "results/unity-tests-agent",
    [ValidateSet("Both", "EditMode", "PlayMode")]
    [string]$Mode = "Both",
    [ValidateSet("Workspace", "Feature", "Module", "Smoke")]
    [string]$ScopeType = "Workspace",
    [string]$ScopeName = "",
    [string]$TestFilter = "",
    [string]$TestCategory = "",
    [string]$AssemblyNames = "",
    [string]$OrderedTestListFile = "",
    [string]$RerunFailedFrom = "",
    [int]$MaxFailures = 25,
    [int]$MaxMessageLength = 240,
    [int]$LogTailLines = 40,
    [switch]$IncludeStackTrace,
    [switch]$ValidateScope,
    [string]$ScopeMapPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function To-Int {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) { return 0 }
    $n = 0
    [void][int]::TryParse($Value, [ref]$n)
    return $n
}

function To-Double {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) { return 0.0 }
    $n = 0.0
    [void][double]::TryParse(
        $Value,
        [System.Globalization.NumberStyles]::Float,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [ref]$n
    )
    return $n
}

function Normalize-Message {
    param(
        [string]$Message,
        [int]$MaxLen = 240
    )

    if ([string]::IsNullOrWhiteSpace($Message)) { return "" }

    $flat = ($Message -replace "\s+", " ").Trim()
    if ($flat.Length -le $MaxLen) { return $flat }

    return $flat.Substring(0, $MaxLen) + "..."
}

function Get-Attr {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$Name
    )

    if ($null -eq $Node -or $null -eq $Node.Attributes) {
        return ""
    }

    $attr = $Node.Attributes[$Name]
    if ($null -eq $attr) { return "" }
    return [string]$attr.Value
}

function Get-InnerText {
    param([System.Xml.XmlNode]$Node)

    if ($null -eq $Node) { return "" }
    return [string]$Node.InnerText
}

function Get-TopStackFrame {
    param([string]$StackTrace)

    if ([string]::IsNullOrWhiteSpace($StackTrace)) {
        return ""
    }

    $first = ($StackTrace -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
    if ($null -eq $first) { return "" }

    return ($first.Trim())
}

function Load-ScopeMap {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        $scriptDir = Split-Path -Parent $PSCommandPath
        $Path = Join-Path $scriptDir "unity_test_scopes.json"
    }

    $fullPath = Resolve-FullPath $Path

    if (-not (Test-Path -LiteralPath $fullPath)) {
        Write-Warning "Scope map not found at: $fullPath"
        return $null
    }

    try {
        $raw = Get-Content -LiteralPath $fullPath -Raw
        $map = $raw | ConvertFrom-Json
        return $map
    }
    catch {
        Write-Warning "Failed to parse scope map at $fullPath : $_"
        return $null
    }
}

function Resolve-ScopeFilter {
    param(
        [object]$ScopeMap,
        [string]$ScopeType,
        [string]$ScopeName
    )

    if ($null -eq $ScopeMap) {
        return ""
    }

    $lowerType = $ScopeType.ToLower()
    $lowerName = $ScopeName.ToLower()

    # Special case: "Smoke" scope type
    if ($lowerType -eq "smoke") {
        if ($null -ne $ScopeMap.smoke -and $null -ne $ScopeMap.smoke.testFilter) {
            return [string]$ScopeMap.smoke.testFilter
        }
        return ""
    }

    # "Workspace" scope type (empty filter = all tests)
    if ($lowerType -eq "workspace") {
        if ($null -ne $ScopeMap.modules -and $null -ne $ScopeMap.modules.workspace -and $null -ne $ScopeMap.modules.workspace.testFilter) {
            return [string]$ScopeMap.modules.workspace.testFilter
        }
        return ""
    }

    # "Feature" scope type
    if ($lowerType -eq "feature") {
        if ([string]::IsNullOrWhiteSpace($lowerName)) {
            Write-Warning "ScopeType=Feature requires -ScopeName to be specified"
            return ""
        }

        if ($null -ne $ScopeMap.features) {
            $featuresObj = $ScopeMap.features
            $members = $featuresObj | Get-Member -MemberType NoteProperty | Where-Object { $_.Name -eq $lowerName }
            if ($members) {
                $entry = $featuresObj.$lowerName
                if ($null -ne $entry.testFilter) {
                    return [string]$entry.testFilter
                }
            }
        }

        Write-Warning "Feature '$lowerName' not found in scope map"
        return ""
    }

    # "Module" scope type
    if ($lowerType -eq "module") {
        if ([string]::IsNullOrWhiteSpace($lowerName)) {
            Write-Warning "ScopeType=Module requires -ScopeName to be specified"
            return ""
        }

        if ($null -ne $ScopeMap.modules) {
            $modulesObj = $ScopeMap.modules
            $members = $modulesObj | Get-Member -MemberType NoteProperty | Where-Object { $_.Name -eq $lowerName }
            if ($members) {
                $entry = $modulesObj.$lowerName
                if ($null -ne $entry.testFilter) {
                    return [string]$entry.testFilter
                }
            }
        }

        Write-Warning "Module '$lowerName' not found in scope map"
        return ""
    }

    return ""
}

function Test-ScopeFilterMatchesTests {
    param(
        [string]$UnityExe,
        [string]$ProjectPath,
        [string]$Platform,
        [string]$TestFilter
    )

    if ([string]::IsNullOrWhiteSpace($TestFilter)) {
        # Empty filter means "all tests", which always matches
        return $true
    }

    # Create temp directory for dry-run
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("unity-scope-validate-" + [System.Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

    try {
        $xmlPath = Join-Path $tempDir "test-list.xml"
        $logPath = Join-Path $tempDir "test-list.log"

        $args = @(
            "-batchmode",
            "-nographics",
            "-projectPath", $ProjectPath,
            "-runTests",
            "-testPlatform", $Platform,
            "-testResults", $xmlPath,
            "-logFile", $logPath,
            "-testFilter", $TestFilter
        )

        # Run Unity with the filter
        $proc = Start-Process -FilePath $UnityExe -ArgumentList $args -Wait -NoNewWindow -PassThru
        $exitCode = 0
        if ($null -ne $proc -and $null -ne $proc.ExitCode) {
            $exitCode = [int]$proc.ExitCode
        }

        # Parse the XML to see if any tests were found
        if (Test-Path -LiteralPath $xmlPath) {
            [xml]$xml = Get-Content -LiteralPath $xmlPath -Raw
            $run = $xml.SelectSingleNode("/test-run")

            if ($null -ne $run) {
                $total = To-Int (Get-Attr -Node $run -Name "total")
                return ($total -gt 0)
            }
        }

        # If XML doesn't exist or parsing failed, assume no tests matched
        return $false
    }
    finally {
        # Clean up temp directory
        if (Test-Path -LiteralPath $tempDir) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-FailedFullNamesFromSummary {
    param([string]$SummaryPath)

    $fullNames = New-Object System.Collections.Generic.List[string]

    if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
        return @()
    }

    if (-not (Test-Path -LiteralPath $SummaryPath)) {
        return @()
    }

    $raw = Get-Content -LiteralPath $SummaryPath -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }

    $summary = $raw | ConvertFrom-Json
    if ($null -eq $summary -or $null -eq $summary.runs) {
        return @()
    }

    foreach ($run in $summary.runs) {
        if ($null -eq $run.failures) { continue }
        foreach ($failure in $run.failures) {
            $name = [string]$failure.fullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            if (-not $fullNames.Contains($name)) {
                $fullNames.Add($name)
            }
        }
    }

    return $fullNames.ToArray()
}

function Parse-UnityResultXml {
    param(
        [string]$XmlPath,
        [string]$Platform,
        [string]$LogPath,
        [int]$UnityExitCode,
        [int]$FailureLimit,
        [int]$MessageLimit,
        [switch]$WithStackTrace,
        [int]$TailLines,
        [hashtable]$Selection
    )

    $base = [ordered]@{
        platform = $Platform
        xmlPath = $XmlPath
        logPath = $LogPath
        unityExitCode = $UnityExitCode
        status = "infra_error"
        total = 0
        passed = 0
        failed = 0
        skipped = 0
        durationSec = 0.0
        failures = @()
        truncatedFailures = 0
        selection = $Selection
    }

    if (-not (Test-Path -LiteralPath $XmlPath)) {
        $tail = ""
        if (Test-Path -LiteralPath $LogPath) {
            $tail = (Get-Content -LiteralPath $LogPath -Tail $TailLines) -join "`n"
        }
        $base.logTail = Normalize-Message -Message $tail -MaxLen 5000
        $base.note = "Result XML not found"
        return $base
    }

    [xml]$xml = Get-Content -LiteralPath $XmlPath -Raw
    $run = $xml.SelectSingleNode("/test-run")

    if ($null -eq $run) {
        $base.note = "Invalid Unity test XML (missing /test-run node)"
        return $base
    }

    $failedNodes = $xml.SelectNodes("//test-case[@result='Failed' or @result='Error']")
    $failedCount = 0
    if ($null -ne $failedNodes) {
        $failedCount = $failedNodes.Count
    }

    $failures = @()
    if ($failedCount -gt 0) {
        $take = [Math]::Min($FailureLimit, $failedCount)
        for ($i = 0; $i -lt $take; $i++) {
            $testNode = $failedNodes.Item($i)
            $msgNode = $testNode.SelectSingleNode("failure/message")
            $stackNode = $testNode.SelectSingleNode("failure/stack-trace")
            $stackRaw = Get-InnerText $stackNode

            $entry = [ordered]@{
                name = Get-Attr -Node $testNode -Name "name"
                fullName = Get-Attr -Node $testNode -Name "fullname"
                durationSec = To-Double (Get-Attr -Node $testNode -Name "duration")
                message = Normalize-Message -Message (Get-InnerText $msgNode) -MaxLen $MessageLimit
                topStack = Normalize-Message -Message (Get-TopStackFrame -StackTrace $stackRaw) -MaxLen 240
            }

            if ($WithStackTrace.IsPresent) {
                $entry.stackTrace = Normalize-Message -Message $stackRaw -MaxLen 2000
            }

            $failures += $entry
        }
    }

    $statusFromXml = Get-Attr -Node $run -Name "result"
    $status = switch ($statusFromXml) {
        "Passed" { "passed" }
        "Failed" { "failed" }
        "Inconclusive" { "failed" }
        default { "unknown" }
    }

    $base.status = $status
    $base.total = To-Int (Get-Attr -Node $run -Name "total")
    $base.passed = To-Int (Get-Attr -Node $run -Name "passed")
    $base.failed = To-Int (Get-Attr -Node $run -Name "failed")
    $base.skipped = To-Int (Get-Attr -Node $run -Name "skipped")
    $base.durationSec = To-Double (Get-Attr -Node $run -Name "duration")
    $base.failures = $failures
    $base.truncatedFailures = [Math]::Max(0, $failedCount - $failures.Count)

    if ($UnityExitCode -ne 0 -and $base.failed -eq 0) {
        $base.status = "infra_error"
        if (Test-Path -LiteralPath $LogPath) {
            $tail = (Get-Content -LiteralPath $LogPath -Tail $TailLines) -join "`n"
            $base.logTail = Normalize-Message -Message $tail -MaxLen 5000
        }
    }

    return $base
}

$unityExe = Resolve-FullPath $UnityPath
$project = Resolve-FullPath $ProjectPath
$outRoot = Resolve-FullPath $OutDir
$orderedListPath = Resolve-FullPath $OrderedTestListFile
$rerunSummaryPath = Resolve-FullPath $RerunFailedFrom

if (-not (Test-Path -LiteralPath $unityExe)) {
    throw "Unity executable not found: $unityExe"
}

if (-not (Test-Path -LiteralPath $project)) {
    throw "Unity project path not found: $project"
}

New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

# Load scope map and resolve filter if not explicitly provided
$scopeMap = Load-ScopeMap -Path $ScopeMapPath
$resolvedFilter = $TestFilter
$scopeResolved = $false

if ([string]::IsNullOrWhiteSpace($TestFilter)) {
    $resolvedFilter = Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType $ScopeType -ScopeName $ScopeName
    if (-not [string]::IsNullOrWhiteSpace($resolvedFilter)) {
        $scopeResolved = $true
        Write-Host "Resolved scope ($ScopeType$(if ($ScopeName) { "/$ScopeName" })) to filter: $resolvedFilter"
    }
}

# Validate scope if requested
if ($ValidateScope.IsPresent -and -not [string]::IsNullOrWhiteSpace($resolvedFilter)) {
    Write-Host "Validating scope filter matches at least one test..."

    $platformsToValidate = switch ($Mode) {
        "Both" { @("EditMode", "PlayMode") }
        default { @($Mode) }
    }

    $anyMatches = $false
    foreach ($platform in $platformsToValidate) {
        Write-Host "  Checking $platform..."
        $matches = Test-ScopeFilterMatchesTests -UnityExe $unityExe -ProjectPath $project -Platform $platform -TestFilter $resolvedFilter

        if ($matches) {
            Write-Host "  [OK] ${platform}: Filter matches tests"
            $anyMatches = $true
        }
        else {
            Write-Host "  [FAIL] ${platform}: Filter matches NO tests"
        }
    }

    if (-not $anyMatches) {
        throw "SCOPE VALIDATION FAILED: Filter '$resolvedFilter' (from $ScopeType$(if ($ScopeName) { "/$ScopeName" })) matched NO tests in any platform. The scope definition may be stale or incorrect."
    }

    Write-Host "[OK] Scope validation passed"
}

$TestFilter = $resolvedFilter

$platforms = switch ($Mode) {
    "Both" { @("EditMode", "PlayMode") }
    default { @($Mode) }
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"

if (
    [string]::IsNullOrWhiteSpace($orderedListPath) -and
    -not [string]::IsNullOrWhiteSpace($rerunSummaryPath) -and
    [string]::IsNullOrWhiteSpace($TestFilter)
) {
    $failedNames = Get-FailedFullNamesFromSummary -SummaryPath $rerunSummaryPath
    if ($failedNames.Count -gt 0) {
        $orderedListPath = Join-Path $outRoot "$stamp-rerun-tests.txt"
        $failedNames | Set-Content -LiteralPath $orderedListPath -Encoding UTF8
    }
}

$runs = @()

foreach ($platform in $platforms) {
    $xmlPath = Join-Path $outRoot "$stamp-$platform.xml"
    $logPath = Join-Path $outRoot "$stamp-$platform.log"

    $args = @(
        "-batchmode",
        "-nographics",
        "-projectPath", $project,
        "-runTests",
        "-testPlatform", $platform,
        "-testResults", $xmlPath,
        "-logFile", $logPath
    )

    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $args += @("-testFilter", $TestFilter)
    }

    if (-not [string]::IsNullOrWhiteSpace($TestCategory)) {
        $args += @("-testCategory", $TestCategory)
    }

    if (-not [string]::IsNullOrWhiteSpace($AssemblyNames)) {
        $args += @("-assemblyNames", $AssemblyNames)
    }

    if (-not [string]::IsNullOrWhiteSpace($orderedListPath)) {
        $args += @("-orderedTestListFile", $orderedListPath)
    }

    Write-Host "Running Unity $platform tests..."

    $proc = Start-Process -FilePath $unityExe -ArgumentList $args -Wait -NoNewWindow -PassThru
    $unityExit = 0
    if ($null -ne $proc -and $null -ne $proc.ExitCode) {
        $unityExit = [int]$proc.ExitCode
    }

    $selection = [ordered]@{
        scopeType = $ScopeType
        scopeName = $ScopeName
        scopeResolved = $scopeResolved
        testFilter = $TestFilter
        testCategory = $TestCategory
        assemblyNames = $AssemblyNames
        orderedTestListFile = $orderedListPath
        rerunFailedFrom = $rerunSummaryPath
    }

    $parsed = Parse-UnityResultXml `
        -XmlPath $xmlPath `
        -Platform $platform `
        -LogPath $logPath `
        -UnityExitCode $unityExit `
        -FailureLimit $MaxFailures `
        -MessageLimit $MaxMessageLength `
        -WithStackTrace:$IncludeStackTrace `
        -TailLines $LogTailLines `
        -Selection $selection

    $runs += $parsed
}

$total = 0
$passed = 0
$failed = 0
$skipped = 0
$duration = 0.0
$hasInfraError = $false

foreach ($run in $runs) {
    $total += $run.total
    $passed += $run.passed
    $failed += $run.failed
    $skipped += $run.skipped
    $duration += $run.durationSec
    if ($run.status -eq "infra_error") {
        $hasInfraError = $true
    }
}

$overallStatus = if ($hasInfraError) {
    "infra_error"
} elseif ($failed -gt 0) {
    "failed"
} else {
    "passed"
}

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("o")
    projectPath = $project
    unityPath = $unityExe
    mode = $Mode
    selection = [ordered]@{
        scopeType = $ScopeType
        scopeName = $ScopeName
        scopeResolved = $scopeResolved
        testFilter = $TestFilter
        testCategory = $TestCategory
        assemblyNames = $AssemblyNames
        orderedTestListFile = $orderedListPath
        rerunFailedFrom = $rerunSummaryPath
    }
    status = $overallStatus
    totals = [ordered]@{
        total = $total
        passed = $passed
        failed = $failed
        skipped = $skipped
        durationSec = [Math]::Round($duration, 3)
    }
    runs = $runs
}

$summaryPath = Join-Path $outRoot "$stamp-summary.json"
$latestPath = Join-Path $outRoot "latest-summary.json"

# Write UTF-8 JSON without BOM so strict JSON parsers (e.g., Node JSON.parse)
# can read the summary files directly.
$json = $summary | ConvertTo-Json -Depth 12
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($summaryPath, $json, $utf8NoBom)
[System.IO.File]::WriteAllText($latestPath, $json, $utf8NoBom)

Write-Host ("UNITY_TEST_SUMMARY_JSON={0}" -f $summaryPath)
Write-Host ("STATUS={0} total={1} passed={2} failed={3} skipped={4}" -f $overallStatus, $total, $passed, $failed, $skipped)

if ($overallStatus -eq "infra_error") {
    exit 2
}
if ($overallStatus -eq "failed") {
    exit 1
}
exit 0
