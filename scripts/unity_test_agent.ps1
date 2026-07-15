param(
    [string]$UnityPath = "D:\Programs\Unity\Editor\6000.1.8f1\Editor\Unity.exe",
    [string]$ProjectPath = "src/Asteroids3D",
    [string]$OutDir = "results/unity-tests-agent",
    [ValidateSet("Both", "EditMode", "PlayMode")]
    [string]$Mode = "Both",
    [ValidateSet("Workspace", "Feature", "Module", "Smoke", "Auto")]
    [string]$ScopeType = "Workspace",
    [string]$ScopeName = "",
    [string]$DiffBase = "origin/main",
    [string]$TestFilter = "",
    [string]$TestCategory = "",
    [string]$AssemblyNames = "",
    [string]$OrderedTestListFile = "",
    [string]$RerunFailedFrom = "",
    [int]$MaxFailures = 25,
    [int]$MaxMessageLength = 240,
    [int]$LogTailLines = 40,
    [int]$UnityTimeoutSec = 1800,
    [string]$ExcludeCategory = "RequiresGraphics",
    [switch]$WithGraphics,
    [switch]$IncludeStackTrace,
    [switch]$ValidateScope,
    [string]$ScopeMapPath = "",
    [switch]$SkipUnityAccess,
    [string]$UnityAccessLease = "",
    [int]$UnityAccessWaitSec = 60,
    [string]$UnityAccessStateRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity_test_scope_lib.ps1")
$Script:IsWindowsPlatform = ($env:OS -eq "Windows_NT")
$Script:UnityAccessRunId = [guid]::NewGuid().ToString("N")
# Boot window ends once licensing + global package-cache work is done and per-project Library work begins (postmortem D6).
$Script:BootCompletePattern = 'Application\.AssetDatabase Initial Refresh Start'
$Script:BootWatchTimeoutSec = 180
$Script:BootAcquireWaitSec = 300

function Get-UnityAccessSlot {
    param([string]$ProjectFullPath)
    $repo = Resolve-FullPath (Join-Path $ProjectFullPath "..\..")
    $branch = (& git -C $repo branch --show-current 2>$null | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($branch)) { return "main" }
    return [string]$branch
}

function Invoke-UnityAccess {
    param([string]$Action, [string]$ProjectFullPath, [int]$ProcessId = 0, [int]$WaitSecondsOverride = 0)
    if ($SkipUnityAccess.IsPresent) { return $null }

    $coordinator = Join-Path $PSScriptRoot "unity_access.ps1"
    $slot = Get-UnityAccessSlot $ProjectFullPath
    $lease = if ([string]::IsNullOrWhiteSpace($UnityAccessLease)) { "unity-tests-$slot-$Script:UnityAccessRunId" } else { $UnityAccessLease }
    $waitSeconds = if ($WaitSecondsOverride -gt 0) { $WaitSecondsOverride } else { $UnityAccessWaitSec }
    $arguments = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $coordinator,
        "-Action", $Action,
        "-Lease", $lease,
        "-Slot", $slot,
        "-Mode", "batch",
        "-ProjectPath", $ProjectFullPath,
        "-WaitSeconds", $waitSeconds,
        "-Json"
    )
    if (-not [string]::IsNullOrWhiteSpace($UnityAccessStateRoot)) { $arguments += @("-StateRoot", $UnityAccessStateRoot) }
    if ($ProcessId -gt 0) { $arguments += @("-ProcessId", $ProcessId) }

    $output = @(& powershell @arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $line = @($output | Where-Object { [string]$_ -match '^\s*\{' } | Select-Object -Last 1)
    $result = if ($line.Count -gt 0) { [string]$line[0] | ConvertFrom-Json } else { $null }
    if ($exitCode -ne 0) {
        if ($Action -in @("Acquire", "Wait")) {
            $cancelArguments = @(
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $coordinator,
                "-Action", "Cancel", "-Lease", $lease, "-Json"
            )
            if (-not [string]::IsNullOrWhiteSpace($UnityAccessStateRoot)) { $cancelArguments += @("-StateRoot", $UnityAccessStateRoot) }
            & powershell @cancelArguments 2>&1 | Out-Null
        }
        if ($null -ne $result -and $result.status -eq "blocked_user_editor") {
            $blocker = @($result.blockers | Select-Object -First 1)
            throw "Unity access is waiting for the user-owned main editor (pid=$($blocker[0].processId)) to close. The request was cancelled; close the editor and rerun."
        }
        throw "Unity access $Action failed (exit=$exitCode): $($output -join ' ')"
    }
    return $result
}

function Enter-UnityAccess {
    param([string]$ProjectFullPath)
    [void](Invoke-UnityAccess -Action "Acquire" -ProjectFullPath $ProjectFullPath)
}

function Attach-UnityAccess {
    param([string]$ProjectFullPath, [int]$ProcessId)
    [void](Invoke-UnityAccess -Action "Attach" -ProjectFullPath $ProjectFullPath -ProcessId $ProcessId)
}

function Exit-UnityAccess {
    param([string]$ProjectFullPath)
    [void](Invoke-UnityAccess -Action "Release" -ProjectFullPath $ProjectFullPath)
}

function Enter-UnityBootLane {
    param([string]$ProjectFullPath)
    if ($SkipUnityAccess.IsPresent) { return $false }
    [void](Invoke-UnityAccess -Action "BootAcquire" -ProjectFullPath $ProjectFullPath -WaitSecondsOverride $Script:BootAcquireWaitSec)
    return $true
}

function Exit-UnityBootLane {
    param([string]$ProjectFullPath)
    # A TTL-expired boot hold may already belong to someone else; that is not a run failure.
    try { [void](Invoke-UnityAccess -Action "BootRelease" -ProjectFullPath $ProjectFullPath) }
    catch { Write-Warning "Boot lane release failed (continuing): $($_.Exception.Message)" }
}

function Test-UnityBootComplete {
    param([string]$LogPath)
    if ([string]::IsNullOrWhiteSpace($LogPath) -or -not (Test-Path -LiteralPath $LogPath)) { return $false }
    return [bool](Select-String -LiteralPath $LogPath -Pattern $Script:BootCompletePattern -Quiet -ErrorAction SilentlyContinue)
}

function Get-ArgumentValue {
    param([string[]]$Arguments, [string]$Name)
    for ($i = 0; $i -lt $Arguments.Count - 1; $i++) {
        if ($Arguments[$i] -ieq $Name) { return [string]$Arguments[$i + 1] }
    }
    return ""
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

function Write-AutoSelection {
    param([object]$Auto, [string]$BaseRef, [string]$MergeBase)

    Write-Host "=== Auto scope resolution (diff base: $BaseRef, merge-base: $MergeBase) ==="
    Write-Host ("Changed files considered: {0}" -f @($Auto.consideredFiles).Count)
    foreach ($file in @($Auto.consideredFiles)) {
        Write-Host "  $file"
    }
    if (@($Auto.ignoredFiles).Count -gt 0) {
        Write-Host ("Ignored as test-irrelevant by design (*.md, doc/**, .claude/**): {0}" -f @($Auto.ignoredFiles).Count)
        foreach ($file in @($Auto.ignoredFiles)) {
            Write-Host "  IGNORED: $file"
        }
    }

    switch ($Auto.mode) {
        "smoke" {
            Write-Host "No test-relevant changed files -> running the SMOKE category only."
        }
        "modules" {
            Write-Host ("Matched modules: {0}" -f (@($Auto.matchedModules) -join ", "))
            Write-Host ("Resolved categories (module fixtures + smoke): {0}" -f $Auto.testCategory)
        }
        "fallback" {
            Write-Host "AUTO SCOPE FALLBACK -> FULL WORKSPACE SUITE (never under-test)."
            foreach ($file in @($Auto.unmatchedFiles)) {
                Write-Host "  UNMATCHED (no module 'paths' glob in unity_test_scopes.json): $file"
            }
            foreach ($moduleName in @($Auto.emptyCategoryModules)) {
                Write-Host "  MODULE '$moduleName' matched but its paths cover no [Category]-tagged fixture"
            }
        }
    }
    Write-Host "=== End auto scope resolution ==="
}

function Test-ScopeFilterMatchesTests {
    param(
        [string]$UnityExe,
        [string]$ProjectPath,
        [string]$Platform,
        [string]$TestFilter
    )

    if ([string]::IsNullOrWhiteSpace($TestFilter)) {
        return $true
    }

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

        [void](Invoke-UnityProcess -UnityExe $UnityExe -Arguments $args)

        if (Test-Path -LiteralPath $xmlPath) {
            [xml]$xml = Get-Content -LiteralPath $xmlPath -Raw
            $run = $xml.SelectSingleNode("/test-run")

            if ($null -ne $run) {
                $total = To-Int (Get-Attr -Node $run -Name "total")
                return ($total -gt 0)
            }
        }

        return $false
    }
    finally {
        if (Test-Path -LiteralPath $tempDir) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-UnityProcess {
    param(
        [string]$UnityExe,
        [string[]]$Arguments,
        [int]$TimeoutSec = 1800
    )

    $processProject = Get-ArgumentValue -Arguments $Arguments -Name "-projectPath"
    $processLog = Get-ArgumentValue -Arguments $Arguments -Name "-logFile"
    $accessHeld = $false
    Enter-UnityAccess -ProjectFullPath $processProject
    $accessHeld = $true
    $bootHeld = $false

    try {
        $bootHeld = Enter-UnityBootLane -ProjectFullPath $processProject
        $proc = Start-Process -FilePath $UnityExe -ArgumentList $Arguments -NoNewWindow -PassThru
        Attach-UnityAccess -ProjectFullPath $processProject -ProcessId $proc.Id

        if ($TimeoutSec -le 0) {
            $TimeoutSec = 1800
        }

        $deadline = (Get-Date).AddSeconds($TimeoutSec)
        $bootDeadline = (Get-Date).AddSeconds($Script:BootWatchTimeoutSec)
        $bootPollDue = Get-Date

        while (-not $proc.HasExited) {
            if ($bootHeld -and (Get-Date) -ge $bootPollDue) {
                if ((Get-Date) -ge $bootDeadline -or (Test-UnityBootComplete -LogPath $processLog)) {
                    Exit-UnityBootLane -ProjectFullPath $processProject
                    $bootHeld = $false
                }
                $bootPollDue = (Get-Date).AddSeconds(2)
            }

            if ((Get-Date) -ge $deadline) {
                if ($Script:IsWindowsPlatform -eq $true) {
                    & taskkill /PID $proc.Id /T /F *> $null
                }
                else {
                    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                }

                Start-Sleep -Milliseconds 300
                return [ordered]@{
                    exitCode = 124
                    timedOut = $true
                    pid = [int]$proc.Id
                }
            }

            Start-Sleep -Milliseconds 500
        }

        $exitCode = 0
        if ($null -ne $proc.ExitCode) {
            $exitCode = [int]$proc.ExitCode
        }

        return [ordered]@{
            exitCode = $exitCode
            timedOut = $false
            pid = [int]$proc.Id
        }
    }
    finally {
        if ($bootHeld) { Exit-UnityBootLane -ProjectFullPath $processProject }
        if ($accessHeld) { Exit-UnityAccess -ProjectFullPath $processProject }
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

if ($ScopeType -eq "Auto") {
    $manualSelectionArgs = [ordered]@{
        "-TestFilter" = $TestFilter
        "-TestCategory" = $TestCategory
        "-AssemblyNames" = $AssemblyNames
        "-OrderedTestListFile" = $OrderedTestListFile
        "-RerunFailedFrom" = $RerunFailedFrom
    }
    $conflicting = @($manualSelectionArgs.Keys | Where-Object { -not [string]::IsNullOrWhiteSpace($manualSelectionArgs[$_]) })
    if ($conflicting.Count -gt 0) {
        throw "-ScopeType Auto cannot be combined with $($conflicting -join ', '): Auto owns test selection so its full-suite fallback stays a true full Workspace run. Narrow with -ExcludeCategory, or drop -ScopeType Auto."
    }
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

$scopeMap = Load-ScopeMap -Path $ScopeMapPath
$resolvedFilter = $TestFilter
$resolvedCategory = $TestCategory
$scopeResolved = $false
$autoSummary = $null
$testsRoot = Join-Path $project "Assets/Scripts/Editor/Tests"

if ([string]::IsNullOrWhiteSpace($TestFilter)) {
    if ($ScopeType -eq "Auto") {
        $autoMergeBase = ""
        $autoSelection = $null
        try {
            $diff = Get-AutoChangedFiles -RepoProbePath $project -BaseRef $DiffBase
            $autoMergeBase = $diff.mergeBase
            $fileCategoryIndex = Get-TestFileCategoryIndex -TestsRoot $testsRoot -RepoRoot $diff.repoRoot
            $autoSelection = Resolve-AutoSelection -ScopeMap $scopeMap -ChangedFiles $diff.files -FileCategoryIndex $fileCategoryIndex
        }
        catch {
            Write-Warning "AUTO SCOPE: git diff against '$DiffBase' failed ($($_.Exception.Message)). Falling back to the FULL Workspace suite."
            $autoSelection = [pscustomobject]@{
                mode = "fallback"
                consideredFiles = @()
                ignoredFiles = @()
                matchedModules = @()
                unmatchedFiles = @()
                emptyCategoryModules = @()
                categories = @()
                testCategory = ""
            }
        }

        Write-AutoSelection -Auto $autoSelection -BaseRef $DiffBase -MergeBase $autoMergeBase
        $resolvedCategory = [string]$autoSelection.testCategory
        $scopeResolved = ($autoSelection.mode -ne "fallback")
        $autoSummary = [ordered]@{
            diffBase = $DiffBase
            mergeBase = $autoMergeBase
            mode = [string]$autoSelection.mode
            matchedModules = @($autoSelection.matchedModules)
            unmatchedFiles = @($autoSelection.unmatchedFiles)
            ignoredFiles = @($autoSelection.ignoredFiles)
            emptyCategoryModules = @($autoSelection.emptyCategoryModules)
            categories = @($autoSelection.categories)
        }
    }
    elseif ($ScopeType -eq "Module" -and [string]::IsNullOrWhiteSpace($TestCategory)) {
        $repoRoot = Get-RepoRoot -ProbePath $project
        $fileCategoryIndex = Get-TestFileCategoryIndex -TestsRoot $testsRoot -RepoRoot $repoRoot
        $moduleCategories = @(Get-ModuleDerivedCategories -ScopeMap $scopeMap -ModuleName $ScopeName.ToLower() -FileCategoryIndex $fileCategoryIndex)
        if ($moduleCategories.Count -eq 0) {
            Write-Warning "Module '$ScopeName' resolved to no [Category]-tagged fixtures (unknown module, or its paths cover none). Running the full Workspace suite."
        }
        else {
            if ($moduleCategories -notcontains "Smoke") { $moduleCategories += "Smoke" }
            $resolvedCategory = (@($moduleCategories | Sort-Object) -join ';')
            $scopeResolved = $true
            Write-Host "Resolved scope (Module/$ScopeName) to categories: $resolvedCategory"
        }
    }
    else {
        $resolvedFilter = Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType $ScopeType -ScopeName $ScopeName
        if (-not [string]::IsNullOrWhiteSpace($resolvedFilter)) {
            $scopeResolved = $true
            Write-Host "Resolved scope ($ScopeType$(if ($ScopeName) { "/$ScopeName" })) to filter: $resolvedFilter"
        }
    }
}

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
$TestCategory = $resolvedCategory

$includeCategories = @()
if (-not [string]::IsNullOrWhiteSpace($TestCategory)) {
    $includeCategories = @($TestCategory -split ';' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
}
$excludeCategories = @()
if (-not [string]::IsNullOrWhiteSpace($ExcludeCategory)) {
    $excludeCategories = @($ExcludeCategory -split ';' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" -and $includeCategories -notcontains $_ })
}
$categoryFilter = (@($includeCategories) + @($excludeCategories | ForEach-Object { "!$_" })) -join ";"
if (-not [string]::IsNullOrWhiteSpace($categoryFilter)) {
    Write-Host "Test category filter: $categoryFilter"
}

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

$selection = [ordered]@{
    scopeType = $ScopeType
    scopeName = $ScopeName
    scopeResolved = $scopeResolved
    testFilter = $TestFilter
    testCategory = $TestCategory
    excludeCategory = $ExcludeCategory
    categoryFilter = $categoryFilter
    assemblyNames = $AssemblyNames
    orderedTestListFile = $orderedListPath
    rerunFailedFrom = $rerunSummaryPath
    auto = $autoSummary
}

$runs = @()

foreach ($platform in $platforms) {
    $xmlPath = Join-Path $outRoot "$stamp-$platform.xml"
    $logPath = Join-Path $outRoot "$stamp-$platform.log"

    $args = @(
        "-batchmode",
        "-projectPath", $project,
        "-runTests",
        "-testPlatform", $platform,
        "-testResults", $xmlPath,
        "-logFile", $logPath
    )
    if (-not $WithGraphics) {
        $args = @($args[0], "-nographics") + $args[1..($args.Count - 1)]
    }

    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $args += @("-testFilter", $TestFilter)
    }

    if (-not [string]::IsNullOrWhiteSpace($categoryFilter)) {
        $args += @("-testCategory", $categoryFilter)
    }

    if (-not [string]::IsNullOrWhiteSpace($AssemblyNames)) {
        $args += @("-assemblyNames", $AssemblyNames)
    }

    if (-not [string]::IsNullOrWhiteSpace($orderedListPath)) {
        $args += @("-orderedTestListFile", $orderedListPath)
    }

    Write-Host "Running Unity $platform tests..."

    $invoke = Invoke-UnityProcess -UnityExe $unityExe -Arguments $args -TimeoutSec $UnityTimeoutSec
    $unityExit = [int]$invoke.exitCode

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

    if ($invoke.timedOut) {
        $parsed.status = "infra_error"
        $parsed.note = "Unity test run timed out after $UnityTimeoutSec seconds and was terminated (pid=$($invoke.pid))."
    }

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
    selection = $selection
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
