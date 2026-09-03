<#
.SYNOPSIS
    Runs the Unity test suite (cold batch, or -Routed against a resident editor) and writes the
    run summary other tools read.

.DESCRIPTION
    Exit codes: 0 all green, 1 test failures, 2 infra_error (nothing executed - compile failure or
    a launch problem), any other non-zero = the wrapper itself failed before a summary existed.

    Owned state, written to <OutDir>: "<stamp>-summary.json" and "latest-summary.json" (identical
    content). Fields consumers depend on:
      projectPath, mode, status (passed|failed|infra_error), totals, runs[], selection{...}
      transport      - present and "routed" only for a -Routed warm-editor run.
      coverage       - { verdict = "full"|"partial"; reason = "<machine-readable why>" }. THE
                       coverage verdict: "full" means this run covered the whole suite cold,
                       unfiltered and green, and is therefore merge-grade. Readers trust this
                       field; they do not re-derive it from selection/runs. A summary without it
                       (older run, foreign producer) is partial by the reader's fail-closed rule.
      The summary is a snapshot of the WORKING TREE at run time; pairing it with a commit is the
      caller's job (agent_worktree_pool.sh records the tree hash alongside it).

    Machine channel: KEY=value trailers on stdout - UNITY_TEST_SUMMARY_JSON=<path> and
    STATUS=<status> total=... passed=... failed=... skipped=...
#>
param(
    # Empty resolves from the project's own ProjectVersion.txt (scripts/lib/unity_editor.ps1).
    [string]$UnityPath = "",
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
    [switch]$Windowed,
    [string]$CaptureScenario = "",
    [switch]$IncludeStackTrace,
    [switch]$ValidateScope,
    [string]$ScopeMapPath = "",
    [switch]$SkipUnityAccess,
    [string]$UnityAccessLease = "",
    [int]$UnityAccessWaitSec = 60,
    [string]$UnityAccessStateRoot = "",
    [switch]$Routed,
    [string]$UnityCliPath = "unity"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity_test_scope_lib.ps1")
. (Join-Path $PSScriptRoot "unity_access_client.ps1")
. (Join-Path $PSScriptRoot "lib/unity_editor.ps1")
. (Join-Path $PSScriptRoot "lib/process_tree.ps1")

# ---- Section map -------------------------------------------------------------
#   Parameter validation      flag combinations that can never run
#   Unity access coordination Get-UnityAccessSlot .. Test-UnityBootComplete (lease + boot lane)
#   Parsers & formatting      Get-ArgumentValue .. Get-TopStackFrame, Write-AutoSelection
#   Cold transport            Test-ScopeFilterMatchesTests, Invoke-UnityProcess (boot, watchdog, kill)
#   Run records & results     New-RunRecord, New-FailureEntry, Parse-UnityResultXml, Get-CoverageVerdict
#   Routed transport          attach to a resident editor via the unity CLI pipeline
#   Setup                     paths, output dir, scratch-scenario staging
#   Scope resolution & run    Auto / Module / authored filter -> selection; routed | single-boot | per-platform
#   Summary & exit            totals, coverage stamp, machine channel, exit code
# ------------------------------------------------------------------------------

# ---- Parameter validation --------------------------------------------------

if ($WithGraphics.IsPresent) {
    if ($Mode -ne "PlayMode") {
        throw "-WithGraphics requires -Mode PlayMode: graphics runs are for filtered capture/render tests, never the merge-gate suite."
    }
    if ([string]::IsNullOrWhiteSpace($TestFilter)) {
        throw "-WithGraphics requires an explicit -TestFilter so a graphics run can never widen into the full suite."
    }
    if (-not $PSBoundParameters.ContainsKey('ExcludeCategory')) {
        $ExcludeCategory = ""
    }
}
if ($Windowed.IsPresent -and -not $WithGraphics.IsPresent) {
    throw "-Windowed requires -WithGraphics: only Game View capture needs a windowed editor (Recorder's WaitForEndOfFrame never resumes in -batchmode)."
}
if (-not [string]::IsNullOrWhiteSpace($CaptureScenario)) {
    if (-not $WithGraphics.IsPresent) {
        throw "-CaptureScenario requires -WithGraphics: frame capture needs a graphics device."
    }
    if ($CaptureScenario -notmatch '^[A-Za-z0-9_]+$') {
        throw "-CaptureScenario must be a plain scenario type name (its .cs file name), got '$CaptureScenario'."
    }
}
if ($Routed.IsPresent) {
    $routedIncompatible = [ordered]@{
        "-WithGraphics" = $WithGraphics.IsPresent
        "-Windowed" = $Windowed.IsPresent
        "-CaptureScenario" = -not [string]::IsNullOrWhiteSpace($CaptureScenario)
        "-OrderedTestListFile" = -not [string]::IsNullOrWhiteSpace($OrderedTestListFile)
        "-RerunFailedFrom" = -not [string]::IsNullOrWhiteSpace($RerunFailedFrom)
        "-ValidateScope" = $ValidateScope.IsPresent
        "-SkipUnityAccess" = $SkipUnityAccess.IsPresent
    }
    $routedBad = @($routedIncompatible.Keys | Where-Object { $routedIncompatible[$_] })
    if ($routedBad.Count -gt 0) {
        throw "-Routed cannot be combined with $($routedBad -join ', '): a routed run attaches to a resident editor, so boot-frozen flags cannot reach it (dispatch capture scenarios via 'unity command capture_request_scenario' instead) and it always verifies through the unity_access coordinator."
    }
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

# ---- Unity access coordination ---------------------------------------------
$Script:UnityAccessRunId = [guid]::NewGuid().ToString("N")
$Script:BootCompletePattern = ""
$Script:BootWatchTimeoutSec = 180
$Script:BootAcquireWaitSec = 300

function Get-UnityAccessSlot {
    param([string]$ProjectFullPath)
    $repo = Get-RepoRoot -ProbePath $ProjectFullPath
    $branch = (& git -C $repo branch --show-current 2>$null | Select-Object -First 1)
    # The slot names the coordinator lease. A git failure defaulting to "main" would file this run's
    # lease against the primary tree - the wrong owner, silently.
    if ([string]::IsNullOrWhiteSpace($branch)) {
        throw "Could not read the current branch under '$repo'; the Unity access slot is unknown (detached HEAD, or git failed)."
    }
    return [string]$branch
}

# Without it a coordinator call answers from the machine's real state, not the test's.
function Add-StateRootArgument {
    param([string[]]$Arguments)
    if ([string]::IsNullOrWhiteSpace($UnityAccessStateRoot)) { return $Arguments }
    return $Arguments + @("-StateRoot", $UnityAccessStateRoot)
}

function Invoke-UnityAccess {
    param([string]$Action, [string]$ProjectFullPath, [int]$ProcessId = 0, [int]$WaitSecondsOverride = 0)
    if ($SkipUnityAccess.IsPresent) { return $null }

    $slot = Get-UnityAccessSlot $ProjectFullPath
    $lease = if ([string]::IsNullOrWhiteSpace($UnityAccessLease)) { "unity-tests-$slot-$Script:UnityAccessRunId" } else { $UnityAccessLease }
    $waitSeconds = if ($WaitSecondsOverride -gt 0) { $WaitSecondsOverride } else { $UnityAccessWaitSec }
    $arguments = @(Add-StateRootArgument @(
        "-Action", $Action,
        "-Lease", $lease,
        "-Slot", $slot,
        "-Mode", "batch",
        "-ProjectPath", $ProjectFullPath,
        "-WaitSeconds", $waitSeconds
    ))
    if ($ProcessId -gt 0) { $arguments += @("-ProcessId", $ProcessId) }

    $call = Invoke-UnityAccessCoordinator -CoordinatorArgs $arguments
    $result = $call.result
    if ($call.exitCode -ne 0) {
        if ($Action -in @("Acquire", "Wait")) {
            [void](Invoke-UnityAccessCoordinator -CoordinatorArgs @(Add-StateRootArgument @("-Action", "Cancel", "-Lease", $lease)))
        }
        if ($null -ne $result -and $result.status -eq "blocked_user_editor") {
            $blocker = @($result.blockers | Select-Object -First 1)
            throw "Unity access is waiting for the user-owned main editor (pid=$($blocker[0].processId)) to close. The request was cancelled; close the editor and rerun."
        }
        throw "Unity access $Action failed (exit=$($call.exitCode)): $($call.stdout) $($call.stderr)"
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

# The boot-complete marker has ONE home: the coordinator that owns the boot lane. This run drives that
# lane itself, so it asks rather than keeping a copy that drifts out of step with the lane's own reading.
function Get-BootCompletePattern {
    if (-not [string]::IsNullOrWhiteSpace($Script:BootCompletePattern)) { return $Script:BootCompletePattern }
    $call = Invoke-UnityAccessCoordinator -CoordinatorArgs @("-Action", "Contract")
    if ($call.exitCode -ne 0 -or $null -eq $call.result) { throw "unity_access Contract failed (exit=$($call.exitCode)): $($call.stderr)" }
    $pattern = [string]$call.result.bootCompletePattern
    if ([string]::IsNullOrWhiteSpace($pattern)) { throw "unity_access Contract returned no bootCompletePattern." }
    $Script:BootCompletePattern = $pattern
    return $pattern
}

function Test-UnityBootComplete {
    param([string]$LogPath)
    if ([string]::IsNullOrWhiteSpace($LogPath) -or -not (Test-Path -LiteralPath $LogPath)) { return $false }
    return [bool](Select-String -LiteralPath $LogPath -Pattern (Get-BootCompletePattern) -Quiet -ErrorAction SilentlyContinue)
}

# ---- Parsers & formatting --------------------------------------------------
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

function Split-DelimitedList {
    param([string]$Value, [string]$Delimiter = ';')
    return @($Value -split $Delimiter | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
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

function Get-LogTail {
    param([string]$LogPath, [int]$TailLines)
    if (-not (Test-Path -LiteralPath $LogPath)) { return "" }
    return Normalize-Message -Message ((Get-Content -LiteralPath $LogPath -Tail $TailLines) -join "`n") -MaxLen 5000
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
        Write-Host ("Ignored as test-irrelevant by design (*.md, doc/**, .claude/**, *.gitignore): {0}" -f @($Auto.ignoredFiles).Count)
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

# ---- Cold transport --------------------------------------------------------
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
        [int]$TimeoutSec = 1800,
        # Files whose existence means the run is decided; past the grace window a still-alive
        # editor is a shutdown hang (observed 24 min to never), not progress.
        [string[]]$CompletionFiles = @(),
        [int]$CompletionGraceSec = 120
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
        $completionSeenAt = $null

        while (-not $proc.HasExited) {
            if ($bootHeld -and (Get-Date) -ge $bootPollDue) {
                if ((Get-Date) -ge $bootDeadline -or (Test-UnityBootComplete -LogPath $processLog)) {
                    Exit-UnityBootLane -ProjectFullPath $processProject
                    $bootHeld = $false
                }
                $bootPollDue = (Get-Date).AddSeconds(2)
            }

            if ($CompletionFiles.Count -gt 0 -and $null -eq $completionSeenAt) {
                $allExist = $true
                foreach ($file in $CompletionFiles) {
                    if (-not (Test-Path -LiteralPath $file)) { $allExist = $false; break }
                }
                if ($allExist) { $completionSeenAt = Get-Date }
            }

            $hungAfterResults = ($null -ne $completionSeenAt -and
                                 (Get-Date) -ge $completionSeenAt.AddSeconds($CompletionGraceSec))

            if ($hungAfterResults -or (Get-Date) -ge $deadline) {
                Stop-ProcessTree -ProcessId $proc.Id

                Start-Sleep -Milliseconds 300
                return [ordered]@{
                    exitCode = 124
                    timedOut = -not $hungAfterResults
                    killedAfterResults = $hungAfterResults
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
            killedAfterResults = $false
            pid = [int]$proc.Id
        }
    }
    finally {
        if ($bootHeld) { Exit-UnityBootLane -ProjectFullPath $processProject }
        if ($accessHeld) { Exit-UnityAccess -ProjectFullPath $processProject }
    }
}

# ---- Run records & results -------------------------------------------------
function Get-FailedFullNamesFromSummary {
    param([string]$SummaryPath)

    $fullNames = New-Object System.Collections.Generic.List[string]
    if ([string]::IsNullOrWhiteSpace($SummaryPath) -or -not (Test-Path -LiteralPath $SummaryPath)) { return @() }
    $raw = Get-Content -LiteralPath $SummaryPath -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) { return @() }
    $summary = $raw | ConvertFrom-Json
    if ($null -eq $summary -or $null -eq $summary.runs) { return @() }

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

# The two transports (cold XML, routed pipeline) must produce byte-identical record shapes: the
# summary schema, the coverage stamp and the pool all read one set of keys. These two constructors
# are the only place either shape - and the truncation caps - is written down.
function New-RunRecord {
    param(
        [string]$Platform,
        [string]$XmlPath = "",
        [string]$LogPath = "",
        [int]$UnityExitCode = 0,
        [string]$Status = "infra_error",
        [object]$Selection
    )

    return [ordered]@{
        platform = $Platform
        xmlPath = $XmlPath
        logPath = $LogPath
        unityExitCode = $UnityExitCode
        status = $Status
        total = 0
        passed = 0
        failed = 0
        skipped = 0
        durationSec = 0.0
        failures = @()
        truncatedFailures = 0
        selection = $Selection
    }
}

function New-FailureEntry {
    param(
        [string]$Name,
        [string]$FullName,
        [double]$DurationSec,
        [string]$Message,
        [string]$StackTrace,
        [int]$MessageLimit,
        [switch]$WithStackTrace
    )

    $entry = [ordered]@{
        name = $Name
        fullName = $FullName
        durationSec = $DurationSec
        message = Normalize-Message -Message $Message -MaxLen $MessageLimit
        topStack = Normalize-Message -Message (Get-TopStackFrame -StackTrace $StackTrace) -MaxLen 240
    }
    if ($WithStackTrace.IsPresent) {
        $entry.stackTrace = Normalize-Message -Message $StackTrace -MaxLen 2000
    }
    return $entry
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

    $base = New-RunRecord -Platform $Platform -XmlPath $XmlPath -LogPath $LogPath `
        -UnityExitCode $UnityExitCode -Selection $Selection

    if (-not (Test-Path -LiteralPath $XmlPath)) {
        $base.logTail = Get-LogTail -LogPath $LogPath -TailLines $TailLines
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

            $failures += New-FailureEntry `
                -Name (Get-Attr -Node $testNode -Name "name") `
                -FullName (Get-Attr -Node $testNode -Name "fullname") `
                -DurationSec (To-Double (Get-Attr -Node $testNode -Name "duration")) `
                -Message (Get-InnerText $msgNode) `
                -StackTrace $stackRaw `
                -MessageLimit $MessageLimit `
                -WithStackTrace:$WithStackTrace
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
        if (Test-Path -LiteralPath $LogPath) { $base.logTail = Get-LogTail -LogPath $LogPath -TailLines $TailLines }
    }

    return $base
}

# Merge-grade coverage is the RUNNER's verdict, not a reader's guess: only this script knows what it
# was asked to run and what actually executed. The pool trusts coverage.verdict and checks only the
# one thing it owns (that the summary describes the tree it is about to land) - script-contracts.md
# sec.3. A summary carrying no stamp is partial by the reader's fail-closed rule.
function Get-CoverageVerdict {
    param([object[]]$Runs, [object]$Selection, [string]$OverallStatus)

    if ($Routed.IsPresent) { return [ordered]@{ verdict = "partial"; reason = "transport=routed (warm-editor run; merge-grade proof requires a cold-process run)" } }
    if ($OverallStatus -ne "passed") { return [ordered]@{ verdict = "partial"; reason = "status=$OverallStatus" } }
    if ($Mode -ne "Both") { return [ordered]@{ verdict = "partial"; reason = "mode=$Mode" } }
    if ("$($Selection.scopeType)".ToLowerInvariant() -ne "workspace") { return [ordered]@{ verdict = "partial"; reason = "scopeType=$($Selection.scopeType)" } }

    foreach ($key in @("testFilter", "testCategory", "assemblyNames", "orderedTestListFile", "rerunFailedFrom")) {
        if (-not [string]::IsNullOrWhiteSpace([string]$Selection[$key])) { return [ordered]@{ verdict = "partial"; reason = "$key set" } }
    }

    # RequiresGraphics is excluded from every gate run by design; any other exclusion narrows the suite.
    $extraExclusions = @("$($Selection.excludeCategory)".Split(";") | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ -and $_ -ne "requiresgraphics" })
    if ($extraExclusions.Count -gt 0) { return [ordered]@{ verdict = "partial"; reason = "excludeCategory=$($Selection.excludeCategory)" } }

    # A fully-green run containing ignored tests reports NUnit "Skipped:Ignored" -> per-run status
    # "unknown", so per-platform greenness is failed==0/total>0, never the status label.
    $platforms = @()
    foreach ($run in @($Runs)) {
        $platform = [string]$run.platform
        $runStatus = [string]$run.status
        if ($runStatus -eq "failed" -or $runStatus -eq "infra_error") { return [ordered]@{ verdict = "partial"; reason = "run $platform status=$runStatus" } }
        if ($run.failed -ne 0 -or $run.total -le 0) { return [ordered]@{ verdict = "partial"; reason = "run $platform failed=$($run.failed) total=$($run.total)" } }
        $platforms += $platform
    }
    if ($platforms -notcontains "EditMode" -or $platforms -notcontains "PlayMode") {
        return [ordered]@{ verdict = "partial"; reason = "runs lack passed EditMode+PlayMode" }
    }

    return [ordered]@{ verdict = "full"; reason = "mode=Both scopeType=Workspace excludeCategory=$($Selection.excludeCategory)" }
}

# --- Routed transport -------------------------------------------------------
# Attaches to a resident com.unity.pipeline editor instead of booting one. The
# pipeline's run_tests takes a single include-only filter (no '!' exclusion, no
# ';' lists), so selection is resolved wrapper-side from list_tests ground truth
# and any selection the routed calls cannot reproduce exactly is refused.

function Get-JsonProp {
    param([object]$Object, [string]$Name)

    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Invoke-UnityCliJson {
    param([string[]]$CliArgs, [string]$What)

    # stderr merges into the capture (never 2>$null: that eats error envelopes and a failed call reads as success).
    $raw = @(& { $ErrorActionPreference = 'Continue'; & $Script:UnityCli @CliArgs 2>&1 } | ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
    $text = $raw -join "`n"
    $envelope = $null
    $start = $text.IndexOf('{')
    if ($start -ge 0) {
        try { $envelope = $text.Substring($start) | ConvertFrom-Json } catch { $envelope = $null }
    }
    return [ordered]@{ exitCode = $exitCode; envelope = $envelope; text = $text; what = $What }
}

function Invoke-PipelineCommand {
    param([string]$CommandName, [string[]]$CommandParams = @(), [string]$What = "")

    $cliArgs = @("command", $CommandName) + $CommandParams + @("--project-path", $Script:RoutedProject, "--format", "json")
    $call = Invoke-UnityCliJson -CliArgs $cliArgs -What $(if ($What) { $What } else { $CommandName })
    $result = Get-JsonProp (Get-JsonProp $call.envelope 'data') 'result'
    if ($result -is [string] -and $result.TrimStart().StartsWith('{')) {
        try { $result = $result | ConvertFrom-Json } catch { }
    }
    $call.result = $result
    $call.ok = ($call.exitCode -eq 0 -and $null -ne $call.envelope -and [bool](Get-JsonProp $call.envelope 'success'))
    return $call
}

function Assert-RoutedEditorOwner {
    param([string]$ProjectFullPath)

    # "Who owns this path" is the coordinator's question: -ProjectPath makes Status answer it with its
    # own normalization, so no path-matching rule lives here.
    $call = Invoke-UnityAccessCoordinator -CoordinatorArgs @(Add-StateRootArgument @("-Action", "Status", "-ProjectPath", $ProjectFullPath))
    if ($call.exitCode -ne 0 -or $null -eq $call.result) { throw "-Routed: unity_access Status failed (exit=$($call.exitCode)): $($call.stderr)" }
    $state = $call.result

    $owners = @(Get-JsonProp $state 'projectOwner' | Where-Object { $null -ne $_ })
    if ($owners.Count -eq 0) {
        throw ("-Routed attaches only to an editor your work stream already holds, and the coordinator tracks none on $ProjectFullPath. " +
            "Start one first (unity-access skill): .\scripts\unity_access.ps1 -Action StartEditor -Lease <lease> -Slot <slot> -Mode editor -WaitSeconds 60 -Json " +
            "- or drop -Routed for a cold batch run.")
    }
    $owner = $owners[0]
    $ownerPid = 0
    [void][int]::TryParse([string](Get-JsonProp $owner 'processId'), [ref]$ownerPid)
    if ([string](Get-JsonProp $owner 'mode') -ne "editor" -or $ownerPid -le 0) {
        throw ("-Routed: the project is owned by lease '$(Get-JsonProp $owner 'lease')' in '$(Get-JsonProp $owner 'mode')' mode (pid=$ownerPid), not a live editor. " +
            "Wait for that run to finish, or start an editor via StartEditor.")
    }
    return $ownerPid
}

function Wait-RoutedEditorReady {
    param([int]$TimeoutSec = 60, [string]$What = "editor readiness")

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $lastText = ""
    while ($true) {
        $call = Invoke-PipelineCommand -CommandName "editor_status" -What "editor_status"
        if ($call.ok -and $null -ne $call.result) {
            $compiling = [bool](Get-JsonProp $call.result 'compiling') -or [bool](Get-JsonProp $call.result 'isCompiling')
            $reloading = [bool](Get-JsonProp $call.result 'domainReloadInProgress')
            if (-not $compiling -and -not $reloading) { return $call.result }
        }
        $lastText = $call.text
        if ((Get-Date) -ge $deadline) { break }
        Start-Sleep -Seconds 2
    }
    throw "-Routed: $What not reached within ${TimeoutSec}s (editor_status must answer with no compile/domain-reload in progress). Last output: $(Normalize-Message -Message $lastText -MaxLen 400)"
}

function Set-RoutedAutotick {
    # Autotick resets on every domain reload; an unticked idle editor starves command servicing into 30s timeouts.
    $call = Invoke-PipelineCommand -CommandName "set_autotick" -CommandParams @("--enable", "true") -What "set_autotick"
    if (-not $call.ok) {
        Write-Warning "set_autotick failed (a starved editor will surface as poll timeouts): $(Normalize-Message -Message $call.text -MaxLen 240)"
    }
}

function Get-RoutedTestCatalog {
    param([string]$PipelineMode)

    $call = Invoke-PipelineCommand -CommandName "list_tests" -CommandParams @("--mode", $PipelineMode) -What "list_tests"
    if (-not $call.ok -or $null -eq $call.result) {
        throw "-Routed: list_tests --mode $PipelineMode failed: $(Normalize-Message -Message $call.text -MaxLen 400)"
    }
    return @(Get-JsonProp $call.result 'Tests')
}

function Resolve-RoutedPlatformPlan {
    param([string]$Platform)

    $pipelineMode = if ($Platform -eq "EditMode") { "editor" } else { "playmode" }
    $catalog = Get-RoutedTestCatalog -PipelineMode $pipelineMode
    $candidates = @($catalog | Where-Object { $null -ne $_ -and -not [bool](Get-JsonProp $_ 'Explicit') })

    $calls = @()
    $matched = @{}
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        # The scope lib owns the authored filter format; ask it for the literal names this transport
        # needs. A selection it cannot represent is refused here, never approximated.
        $nameSelection = ConvertTo-TestNameSelection -TestFilter $TestFilter
        if (-not $nameSelection.representable) {
            throw "-Routed cannot honor -TestFilter '$TestFilter': the pipeline's run_tests takes one literal substring per call, and this filter has $($nameSelection.reason). Run it cold (drop -Routed)."
        }
        $filterParts = @($nameSelection.names)
        foreach ($part in $filterParts) { $calls += , @{ filter = $part; filterType = "testname" } }
        foreach ($test in $candidates) {
            $fullName = [string](Get-JsonProp $test 'FullName')
            foreach ($part in $filterParts) {
                if ($fullName.IndexOf($part, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $matched[$fullName] = $test
                    break
                }
            }
        }
    }
    elseif ($includeCategories.Count -gt 0) {
        foreach ($category in $includeCategories) { $calls += , @{ filter = $category; filterType = "category" } }
        foreach ($test in $candidates) {
            $categories = @(Get-JsonProp $test 'Categories')
            foreach ($category in $includeCategories) {
                if (@($categories | Where-Object { [string]$_ -ieq $category }).Count -gt 0) {
                    $matched[[string](Get-JsonProp $test 'FullName')] = $test
                    break
                }
            }
        }
    }
    elseif (-not [string]::IsNullOrWhiteSpace($AssemblyNames)) {
        $assemblies = @(Split-DelimitedList $AssemblyNames)
        foreach ($assembly in $assemblies) { $calls += , @{ filter = $assembly; filterType = "assembly" } }
        foreach ($test in $candidates) {
            $testAssembly = [string](Get-JsonProp $test 'Assembly')
            foreach ($assembly in $assemblies) {
                if ($testAssembly -and $testAssembly.IndexOf($assembly, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $matched[[string](Get-JsonProp $test 'FullName')] = $test
                    break
                }
            }
        }
    }
    else {
        $calls += , @{ filter = ""; filterType = "" }
        foreach ($test in $candidates) { $matched[[string](Get-JsonProp $test 'FullName')] = $test }
    }

    $excludedHits = @()
    $expected = @{}
    foreach ($fullName in $matched.Keys) {
        $categories = @(Get-JsonProp $matched[$fullName] 'Categories')
        $hit = @($categories | Where-Object { $candidate = [string]$_; @($excludeCategories | Where-Object { $_ -ieq $candidate }).Count -gt 0 })
        if ($hit.Count -gt 0) { $excludedHits += $fullName } else { $expected[$fullName] = $true }
    }

    return [ordered]@{
        platform = $Platform
        pipelineMode = $pipelineMode
        calls = $calls
        expected = $expected
        excludedHits = @($excludedHits | Sort-Object)
    }
}

function Wait-RoutedTestCompletion {
    param([int]$TimeoutSec, [string]$What)

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $unreachablePolls = 0
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $call = Invoke-PipelineCommand -CommandName "test_status" -What "test_status"
        if (-not $call.ok -or $null -eq $call.result) {
            # Domain reloads take the command server down transiently; only a long dead stretch is a failure.
            $unreachablePolls++
            if ($unreachablePolls -ge 30) { break }
            continue
        }
        $unreachablePolls = 0
        $status = [string](Get-JsonProp $call.result 'status')
        switch ($status) {
            "completed" { return $call.result }
            "error" { throw "-Routed: $What reported error: $(Get-JsonProp $call.result 'message')" }
            "cancelled" { throw "-Routed: $What was cancelled outside this run." }
            default { }
        }
    }
    [void](Invoke-PipelineCommand -CommandName "cancel_tests" -What "cancel_tests")
    throw "-Routed: $What did not complete within ${TimeoutSec}s (cancel_tests sent to unwedge the editor)."
}

function Get-ShortTestName {
    param([string]$FullName)

    $head = $FullName
    $tail = ""
    $paren = $FullName.IndexOf('(')
    if ($paren -ge 0) {
        $head = $FullName.Substring(0, $paren)
        $tail = $FullName.Substring($paren)
    }
    $dot = $head.LastIndexOf('.')
    if ($dot -ge 0) { $head = $head.Substring($dot + 1) }
    return $head + $tail
}

function Invoke-RoutedPlatformRun {
    param([object]$Plan, [object]$Selection)

    $platform = [string]$Plan.platform
    $notes = @()

    if ($Plan.expected.Count -eq 0) {
        $empty = New-RunRecord -Platform $platform -Status "passed" -Selection $Selection
        $empty.note = "No tests matched on this platform."
        return $empty
    }

    $byName = [ordered]@{}
    $duplicates = 0
    $durationSum = 0.0
    foreach ($callSpec in $Plan.calls) {
        [void](Wait-RoutedEditorReady -TimeoutSec 120 -What "$platform pre-run readiness")
        Set-RoutedAutotick

        $runParams = @("--mode", $Plan.pipelineMode, "--async_tests", "true", "--timeout", [string]$UnityTimeoutSec)
        if (-not [string]::IsNullOrWhiteSpace([string]$callSpec.filter)) {
            $runParams += @("--filter", [string]$callSpec.filter, "--filter_type", [string]$callSpec.filterType)
        }
        Write-Host "Routed ${platform}: run_tests $(if ($callSpec.filter) { "$($callSpec.filterType)=$($callSpec.filter)" } else { '(unfiltered)' }) ..."
        $launch = Invoke-PipelineCommand -CommandName "run_tests" -CommandParams $runParams -What "run_tests $platform"
        $launchState = [string](Get-JsonProp $launch.result 'result')
        if (-not $launch.ok -or $launchState -ne "running") {
            $detail = [string](Get-JsonProp $launch.result 'error')
            if ([string]::IsNullOrWhiteSpace($detail)) { $detail = Normalize-Message -Message $launch.text -MaxLen 400 }
            throw "-Routed: run_tests launch for $platform failed: $detail"
        }

        $final = Wait-RoutedTestCompletion -TimeoutSec ($UnityTimeoutSec + 60) -What "run_tests $platform"
        Set-RoutedAutotick

        $durationSum += [double](Get-JsonProp $final 'duration')
        foreach ($result in @(@(Get-JsonProp $final 'results') | Where-Object { $null -ne $_ })) {
            $fullName = [string](Get-JsonProp $result 'FullName')
            if ($byName.Contains($fullName)) { $duplicates++ } else { $byName[$fullName] = $result }
        }
    }

    if ($duplicates -gt 0) {
        $notes += "$duplicates duplicate execution(s) across include-category calls collapsed in totals."
    }

    $passed = 0; $failed = 0; $skipped = 0; $inconclusive = 0
    $failures = @()
    foreach ($result in $byName.Values) {
        switch ([string](Get-JsonProp $result 'Status')) {
            "Passed" { $passed++ }
            "Skipped" { $skipped++ }
            "Inconclusive" { $inconclusive++ }
            default {
                $failed++
                if ($failures.Count -lt $MaxFailures) {
                    $fullName = [string](Get-JsonProp $result 'FullName')
                    $stackRaw = [string](Get-JsonProp $result 'StackTrace')
                    $failures += , (New-FailureEntry `
                        -Name (Get-ShortTestName -FullName $fullName) `
                        -FullName $fullName `
                        -DurationSec ([double](Get-JsonProp $result 'Duration')) `
                        -Message ([string](Get-JsonProp $result 'Message')) `
                        -StackTrace $stackRaw `
                        -MessageLimit $MaxMessageLength `
                        -WithStackTrace:$IncludeStackTrace)
                }
            }
        }
    }

    $status = if ($failed -gt 0 -or $inconclusive -gt 0) { "failed" } else { "passed" }
    if ($inconclusive -gt 0) { $notes += "$inconclusive inconclusive test(s) counted as failing." }

    # Parity against the list_tests ground truth: a drifted executed set is an infra problem, not a verdict.
    $executedNames = @($byName.Keys)
    $missing = @($Plan.expected.Keys | Where-Object { -not $byName.Contains($_) } | Sort-Object)
    $extra = @($executedNames | Where-Object { -not $Plan.expected.Contains($_) } | Sort-Object)
    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
        $status = "infra_error"
        if ($missing.Count -gt 0) { $notes += "Executed set is missing $($missing.Count) expected test(s): $(@($missing | Select-Object -First 10) -join ', ')" }
        if ($extra.Count -gt 0) { $notes += "Executed set has $($extra.Count) unexpected test(s): $(@($extra | Select-Object -First 10) -join ', ')" }
    }

    $run = New-RunRecord -Platform $platform -Status $status -Selection $Selection
    $run.total = $byName.Count
    $run.passed = $passed
    $run.failed = $failed
    $run.skipped = $skipped
    $run.durationSec = [Math]::Round($durationSum, 3)
    $run.failures = $failures
    $run.truncatedFailures = [Math]::Max(0, $failed - $failures.Count)
    if ($notes.Count -gt 0) { $run.note = $notes -join " " }
    return $run
}

function New-RoutedRefusal {
    param([string[]]$Platforms, [object]$Selection, [string]$Reason)

    $records = @()
    foreach ($platform in $Platforms) {
        $record = New-RunRecord -Platform $platform -Status "infra_error" -Selection $Selection
        $record.note = $Reason
        $records += , $record
    }
    return $records
}

function Invoke-RoutedSuite {
    param([string[]]$Platforms, [object]$Selection, [string]$ProjectFullPath)

    $cliCommand = Get-Command $UnityCliPath -ErrorAction SilentlyContinue
    if ($null -eq $cliCommand) {
        throw "-Routed requires the unity CLI ('$UnityCliPath' not found on PATH; install per doc/agents/unity-cli.md)."
    }
    $Script:UnityCli = $cliCommand.Source
    $Script:RoutedProject = $ProjectFullPath

    $selectorAxes = @()
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) { $selectorAxes += "-TestFilter" }
    if ($includeCategories.Count -gt 0) { $selectorAxes += "category selection" }
    if (-not [string]::IsNullOrWhiteSpace($AssemblyNames)) { $selectorAxes += "-AssemblyNames" }
    if ($selectorAxes.Count -gt 1) {
        throw "-Routed supports one selector axis per run and got: $($selectorAxes -join ' + '). The pipeline's run_tests takes a single filter; combine axes only on cold runs."
    }

    $Script:RoutedEditorPid = Assert-RoutedEditorOwner -ProjectFullPath $ProjectFullPath
    Write-Host "Routed run attaching to resident editor pid=$Script:RoutedEditorPid ..."

    $ready = Wait-RoutedEditorReady -TimeoutSec 90 -What "attach"
    $playModeValue = Get-JsonProp $ready 'playMode'
    $isPlaying = if ($playModeValue -is [bool]) { $playModeValue } else { [string]$playModeValue -match '^Play' }
    if ($isPlaying) {
        throw "-Routed: the resident editor is in Play Mode; stop it before routing tests (unity command editor_stop --project-path $ProjectFullPath)."
    }
    Set-RoutedAutotick

    # Plan every platform before running any: a selection the routed transport cannot honor must refuse up front, not half-run.
    $plans = @()
    foreach ($platform in $Platforms) { $plans += , (Resolve-RoutedPlatformPlan -Platform $platform) }

    # A refusal is a real verdict, not a crash: it lands as infra_error runs so the caller gets the
    # same summary + exit 2 it gets for every other "no tests ran" outcome, instead of a bare throw.
    $refusals = @($plans | Where-Object { @($_.excludedHits).Count -gt 0 })
    if ($refusals.Count -gt 0) {
        $lines = foreach ($plan in $refusals) {
            "[$($plan.platform)] $(@($plan.excludedHits | Select-Object -First 10) -join ', ')$(if (@($plan.excludedHits).Count -gt 10) { ", ... ($(@($plan.excludedHits).Count) total)" })"
        }
        return New-RoutedRefusal -Platforms $Platforms -Selection $Selection -Reason (
            "-Routed cannot honor -ExcludeCategory '$ExcludeCategory': run_tests has no exclusion filter and the selection matches excluded-category tests: " +
            ($lines -join "; ") + ". Run this selection cold (drop -Routed), or pass -ExcludeCategory '' to run them deliberately in the resident editor.")
    }

    $expectedTotal = 0
    foreach ($plan in $plans) { $expectedTotal += $plan.expected.Count }
    if ($expectedTotal -eq 0) {
        return New-RoutedRefusal -Platforms $Platforms -Selection $Selection -Reason (
            "-Routed: the selection matches no tests on any requested platform (list_tests ground truth). A zero-test run reports success it never earned; fix the selection.")
    }

    $routedRuns = @()
    foreach ($plan in $plans) {
        Write-Host "Routed $($plan.platform): $($plan.expected.Count) test(s) expected."
        $routedRuns += , (Invoke-RoutedPlatformRun -Plan $plan -Selection $Selection)
    }
    return $routedRuns
}

# ---- Setup -----------------------------------------------------------------
$project = Resolve-FullPath $ProjectPath
$unityExe = if ([string]::IsNullOrWhiteSpace($UnityPath)) { Resolve-UnityEditorPath -ProjectPath $project } else { Resolve-FullPath $UnityPath }
$outRoot = Resolve-FullPath $OutDir
$orderedListPath = Resolve-FullPath $OrderedTestListFile
$rerunSummaryPath = Resolve-FullPath $RerunFailedFrom

if (-not $Routed.IsPresent -and -not (Test-Path -LiteralPath $unityExe)) {
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

# Slot-prepare's `git clean -fd` preserves this gitignored staging dir, so a stranded scenario from a killed run would break the next compile — sweep every run.
$scratchStagingDir = Join-Path $testsRoot "PlayMode/Scratch"
if (Test-Path -LiteralPath $scratchStagingDir) {
    Get-ChildItem -LiteralPath $scratchStagingDir -File |
        Where-Object { $_.Name -like "*.cs" -or $_.Name -like "*.cs.meta" } |
        Remove-Item -Force
}
$stagedScenarioPath = ""
if (-not [string]::IsNullOrWhiteSpace($CaptureScenario)) {
    $repoRoot = Get-RepoRoot -ProbePath $PSScriptRoot
    $scratchSource = Join-Path $repoRoot "scratch/capture/$CaptureScenario.cs"
    $committedSource = Join-Path $testsRoot "PlayMode/Scenarios/$CaptureScenario.cs"
    if (Test-Path -LiteralPath $scratchSource) {
        New-Item -ItemType Directory -Force -Path $scratchStagingDir | Out-Null
        $stagedScenarioPath = Join-Path $scratchStagingDir "$CaptureScenario.cs"
        Copy-Item -LiteralPath $scratchSource -Destination $stagedScenarioPath -Force
        Write-Host "Staged scratch scenario: $scratchSource"
    }
    elseif (Test-Path -LiteralPath $committedSource) {
        Write-Host "Using committed scenario: $committedSource"
    }
    else {
        throw "-CaptureScenario ${CaptureScenario}: no $CaptureScenario.cs in scratch/capture/ (repo root) or committed under Tests/PlayMode/Scenarios/."
    }
    # Agents redirect pre-run logs into results/capture; a missing dir kills that redirect before Unity launches.
    New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot "results/capture") | Out-Null
}

# ---- Scope resolution & run ------------------------------------------------
# Everything below can throw (scope resolution/validation, Unity runs); the finally must always unstage the scratch scenario.
try {

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
            # One structured selection, two transports: the cold path takes its alternation, the
            # routed path takes its literal names (Resolve-RoutedPlatformPlan).
            $scopeSelection = Resolve-ScopeSelection -ScopeMap $scopeMap -ScopeType $ScopeType -ScopeName $ScopeName
            $resolvedFilter = [string]$scopeSelection.testFilter
            if (-not [string]::IsNullOrWhiteSpace($resolvedFilter)) {
                $scopeResolved = $true
                Write-Host "Resolved scope ($ScopeType$(if ($ScopeName) { "/$ScopeName" })) to filter: $resolvedFilter"
            }
        }
    }

    $platforms = switch ($Mode) {
        "Both" { @("EditMode", "PlayMode") }
        default { @($Mode) }
    }

    if ($ValidateScope.IsPresent -and -not [string]::IsNullOrWhiteSpace($resolvedFilter)) {
        Write-Host "Validating scope filter matches at least one test..."

        $anyMatches = $false
        foreach ($platform in $platforms) {
            Write-Host "  Checking $platform..."
            # Not $matches: that is the regex automatic variable, and assigning it breaks any -match in scope.
            $filterMatches = Test-ScopeFilterMatchesTests -UnityExe $unityExe -ProjectPath $project -Platform $platform -TestFilter $resolvedFilter

            if ($filterMatches) {
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

    $includeCategories = @(Split-DelimitedList $TestCategory)
    $excludeCategories = @(Split-DelimitedList $ExcludeCategory | Where-Object { $includeCategories -notcontains $_ })
    $categoryFilter = (@($includeCategories) + @($excludeCategories | ForEach-Object { "!$_" })) -join ";"
    if (-not [string]::IsNullOrWhiteSpace($categoryFilter)) {
        Write-Host "Test category filter: $categoryFilter"
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
        withGraphics = $WithGraphics.IsPresent
        captureScenario = $CaptureScenario
    }

    $runs = @()

    # The selection flags both cold launch shapes pass to Unity.
    $selectionArgs = @()
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) { $selectionArgs += @("-testFilter", $TestFilter) }
    if (-not [string]::IsNullOrWhiteSpace($categoryFilter)) { $selectionArgs += @("-testCategory", $categoryFilter) }
    if (-not [string]::IsNullOrWhiteSpace($AssemblyNames)) { $selectionArgs += @("-assemblyNames", $AssemblyNames) }
    $parseOptions = @{
        FailureLimit = $MaxFailures
        MessageLimit = $MaxMessageLength
        WithStackTrace = $IncludeStackTrace
        TailLines = $LogTailLines
        Selection = $selection
    }

    # Single boot for the plain Both-mode (gate) shape: GateTestRunner drives EditMode then PlayMode
    # through one editor session (the UTF CLI would boot per platform, ~25s overhead each).
    # Ordered-list/rerun runs stay on the stock path (ExecutionSettings.orderedTestNames is internal-only).
    $singleBoot = ($Mode -eq "Both" -and
                   -not $WithGraphics.IsPresent -and
                   [string]::IsNullOrWhiteSpace($CaptureScenario) -and
                   [string]::IsNullOrWhiteSpace($orderedListPath))

    if ($Routed.IsPresent) {
        # @() re-wraps a single-platform result: PowerShell unrolls a one-element array on return, which would serialize runs as an object.
        $runs = @(Invoke-RoutedSuite -Platforms $platforms -Selection $selection -ProjectFullPath $project)
    }
    elseif ($singleBoot) {
        $xmlEdit = Join-Path $outRoot "$stamp-EditMode.xml"
        $xmlPlay = Join-Path $outRoot "$stamp-PlayMode.xml"
        $logPath = Join-Path $outRoot "$stamp-Both.log"

        $args = @(
            "-batchmode",
            "-nographics",
            "-projectPath", $project,
            "-executeMethod", "Tests.EditMode.GateTestRunner.Run",
            "-gateEditResults", $xmlEdit,
            "-gatePlayResults", $xmlPlay,
            "-logFile", $logPath
        ) + $selectionArgs

        Write-Host "Running Unity EditMode+PlayMode tests (single boot)..."

        $invoke = Invoke-UnityProcess -UnityExe $unityExe -Arguments $args -TimeoutSec $UnityTimeoutSec `
            -CompletionFiles @($xmlEdit, $xmlPlay)
        $unityExit = [int]$invoke.exitCode

        # A killed process with both gate XMLs on disk is a decided run wearing a shutdown hang:
        # the XMLs carry the verdict, so parse them as truth instead of voiding a green run.
        $processKilled = $invoke.timedOut -or $invoke.killedAfterResults
        $resultsComplete = (Test-Path -LiteralPath $xmlEdit) -and (Test-Path -LiteralPath $xmlPlay)

        foreach ($entry in @(@{ platform = "EditMode"; xml = $xmlEdit }, @{ platform = "PlayMode"; xml = $xmlPlay })) {
            # Exit 2 means both phases completed and the XMLs carry the failures; feeding 2 into the
            # per-platform parse would misread a passing phase (exit!=0 + failed==0) as infra_error.
            $phaseExit = if ($unityExit -eq 2 -or ($processKilled -and $resultsComplete)) { 0 } else { $unityExit }

            $parsed = Parse-UnityResultXml -XmlPath $entry.xml -Platform $entry.platform -LogPath $logPath -UnityExitCode $phaseExit @parseOptions

            if ($processKilled) {
                if ($resultsComplete -and $parsed.status -ne "infra_error") {
                    $parsed.note = if ($invoke.killedAfterResults) {
                        "Unity editor hung after writing results and was killed by the watchdog (pid=$($invoke.pid)); results parsed from XML. A killed cleanup can leave Assets/InitTestScene*.unity scaffold."
                    } else {
                        "Unity test run hit the ${UnityTimeoutSec}s timeout with complete results on disk (pid=$($invoke.pid)); results parsed from XML."
                    }
                }
                else {
                    $parsed.status = "infra_error"
                    $parsed.note = "Unity test run timed out after $UnityTimeoutSec seconds and was terminated (pid=$($invoke.pid))."
                }
            }

            $runs += $parsed
        }
    }
    else {
        foreach ($platform in $platforms) {
            $xmlPath = Join-Path $outRoot "$stamp-$platform.xml"
            $logPath = Join-Path $outRoot "$stamp-$platform.log"

            $args = @()
            if (-not $Windowed.IsPresent) {
                $args += "-batchmode"
            }
            if (-not $WithGraphics.IsPresent) {
                $args += "-nographics"
            }
            $args += @(
                "-projectPath", $project,
                "-runTests",
                "-testPlatform", $platform,
                "-testResults", $xmlPath,
                "-logFile", $logPath
            ) + $selectionArgs

            if (-not [string]::IsNullOrWhiteSpace($orderedListPath)) {
                $args += @("-orderedTestListFile", $orderedListPath)
            }

            if (-not [string]::IsNullOrWhiteSpace($CaptureScenario)) {
                $args += @("-captureScenario", $CaptureScenario)
            }

            Write-Host "Running Unity $platform tests..."

            $invoke = Invoke-UnityProcess -UnityExe $unityExe -Arguments $args -TimeoutSec $UnityTimeoutSec
            $unityExit = [int]$invoke.exitCode

            $parsed = Parse-UnityResultXml -XmlPath $xmlPath -Platform $platform -LogPath $logPath -UnityExitCode $unityExit @parseOptions

            if ($invoke.timedOut) {
                $parsed.status = "infra_error"
                $parsed.note = "Unity test run timed out after $UnityTimeoutSec seconds and was terminated (pid=$($invoke.pid))."
            }

            $runs += $parsed
        }
    }
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($stagedScenarioPath)) {
        Remove-Item -LiteralPath $stagedScenarioPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath "$stagedScenarioPath.meta" -Force -ErrorAction SilentlyContinue
    }
}

# ---- Summary & exit --------------------------------------------------------
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

$zeroTestGraphicsRun = ($WithGraphics.IsPresent -and $total -eq 0)
if ($zeroTestGraphicsRun) {
    Write-Host "[FAIL] -WithGraphics run executed zero tests: the filter matched nothing, so no frame was ever rendered."
}

$overallStatus = if ($hasInfraError) {
    "infra_error"
} elseif ($failed -gt 0 -or $zeroTestGraphicsRun) {
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
$summary.coverage = Get-CoverageVerdict -Runs $runs -Selection $selection -OverallStatus $overallStatus
if ($Routed.IsPresent) {
    # Warm-run marker; the coverage stamp already reports routed runs as partial (the merge gate stays cold-process).
    $summary.transport = "routed"
    $summary.editorPid = [int]$Script:RoutedEditorPid
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
    # No tests ran — surface the cause (usually a compile failure) inline so callers don't have to spelunk the log.
    Write-Host "INFRA ERROR: no tests executed (compile failure or Unity launch problem, not a test failure)."
    foreach ($run in $runs) {
        if ($run.status -ne "infra_error") { continue }
        $note = if ($run.Contains('note')) { [string]$run.note } else { "" }
        Write-Host ("  [{0}] {1}" -f $run.platform, $note)
        $diag = @()
        $logPath = [string]$run.logPath
        if ($logPath -and (Test-Path -LiteralPath $logPath)) {
            $diag = @(Select-String -LiteralPath $logPath -Pattern 'error CS\d+|Aborting batchmode due to failure|Scripts have compiler errors' |
                      Select-Object -ExpandProperty Line -First 15)
        }
        if ($diag.Count -eq 0 -and $run.Contains('logTail') -and $run.logTail) {
            $diag = @([string]$run.logTail -split "`r?`n" | Select-Object -Last 12)
        }
        foreach ($line in $diag) { Write-Host ("    " + $line.Trim()) }
        if ($logPath) { Write-Host ("    (full log: {0})" -f $logPath) }
    }
    exit 2
}
if ($overallStatus -eq "failed") {
    exit 1
}
exit 0
