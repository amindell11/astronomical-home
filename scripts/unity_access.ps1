<#
.SYNOPSIS
    Coordinates access to this machine's shared Unity editors: a per-project owner lease plus a
    machine-wide boot lane, with a FIFO ticket queue per project.

.DESCRIPTION
    The published interface of this module is: -Action x its statuses x exit codes, the machine
    channel, and the three state-file schemas below. Nothing else is contract. Consumers must not
    parse this script's state files, output layout, or the process table - ask through an -Action.
    Law: doc/agents/script-contracts.md.

    MACHINE CHANNEL
      With -Json: stdout carries EXACTLY one compressed JSON line and nothing else. The whole
      stdout stream is parseable with ConvertFrom-Json; no line-sniffing. Every other emission
      (prose, warnings, a RunBatch child's own output, error text) goes to stderr.
      Without -Json: stdout is human prose, with no machine contract at all.
      The sanctioned client is scripts/unity_access_client.ps1 (dot-source it, then call
      Invoke-UnityAccessCoordinator). Do not re-implement the invoke.

    ACTIONS, STATUSES, EXIT CODES
      Every action returns a JSON object. Non-Status results always carry a "status" field, and the
      exit code is a function of that status alone (0 for any status not listed here).
        20 waiting / boot_waiting / blocked_user_editor   21 blocked_unmanaged_unity
        22 ownership_mismatch                             23 editor_did_not_exit
        24 adopt_* (all four refusals)                    25 boot_lane_wedged
        26 editor_profile_failed                          27 record_unreadable
         1 coordinator_error (see FAILURE below)

      Status        (needs no -Lease) -> the state object, no "status" field, always exit 0.
                    Fields: stateRoot, owners[], legacyOwner, boot, bootWedged, queue[], blockers[].
                    Every owner carries normalizedProjectPath - THE key for "is this owner on my
                    project" (this script's own normalization; compare against it, never re-derive).
                    With -ProjectPath, Status also answers "who owns this path":
                      requestedProjectPath, requestedNormalizedProjectPath,
                      projectOwner        - the owner record for that path, or null,
                      projectProcesses[]  - live Unity processes on it, each with processId,
                                            projectPath, normalizedProjectPath and batch (false =
                                            an interactive editor, which makes a batch run
                                            infra_error and is where CLI commands route).
      Contract      (needs no -Lease) -> status "contract" plus the constants a caller must match
                    exactly: bootCompletePattern, ticketTtlSeconds, ownerTtlSeconds,
                    bootTtlSeconds. Hard-coding any of them keeps a copy that drifts.
      Request       -Lease [-Slot|-ProjectPath] [-Mode] -> queued.
      Acquire       -Lease [-Slot|-ProjectPath] [-Mode] [-WaitSeconds] ->
                    acquired | waiting | blocked_user_editor | blocked_unmanaged_unity.
      Wait          as Acquire, but -WaitSeconds defaults to 60.
      Attach        -Lease -ProcessId -> attached | ownership_mismatch.
      AttachBatchChild -Lease -> attached | batch_child_absent | ownership_mismatch.
      Adopt         -Lease -ProcessId -> adopted | adopt_no_process | adopt_already_tracked |
                    adopt_refused_user_editor | adopt_project_owned.
      Release       -Lease [-CloseEditor [-EditorCloseWaitSeconds]] -> released | editor_did_not_exit.
                    Also frees this lease's boot lane and cancels its queued ticket.
      Cancel        -Lease -> cancelled.
      BootAcquire   -Lease [-WaitSeconds] -> boot_acquired | boot_waiting | boot_lane_wedged |
                    ownership_mismatch | blocked_*. -WaitSeconds defaults to 300.
      BootRelease   -Lease -> boot_released | ownership_mismatch | boot_lane_wedged.
      StartEditor   -Lease -Slot|-ProjectPath [-EditorArgs] [-EditorProfile] [-UnityPath] ->
                    attached (carrying a .profile receipt) | editor_profile_failed |
                    any Acquire or BootAcquire status. -UnityPath overrides the
                    editor resolved from the project's own ProjectVersion.txt
                    (scripts/lib/unity_editor.ps1).
      RunBatch      -Lease -BatchScript [-BatchArguments] [-BatchLogPath] [-BatchBootSeconds] ->
                    batch_complete | any Acquire or BootAcquire status.

      TRAP - batch_complete exits 0 even when the child failed. The child's exit code rides in the
      JSON as "exitCode"; a caller that checks only the process exit code reads a failed Unity run
      as success. Require status -eq "batch_complete" AND exitCode -eq 0.

    FAILURE
      Any unexpected failure is reported as status "coordinator_error" with the message in "error",
      exit 1, and the same text on stderr. That includes Write-JsonFile refusing to write into a
      directory a rival already reaped: the lock dir is the mutex, so it is never recreated.
      A state record that exists but cannot be parsed is NEVER silently reaped - it surfaces as
      status "record_unreadable" (exit 27) naming the file.

    WAITSECONDS DEFAULTS
      0 for Acquire/StartEditor/RunBatch (a single attempt), 60 for Wait, 300 for BootAcquire.
      Polling between attempts is -PollSeconds (default 2). TTLs: -TicketTtlSeconds 900,
      -OwnerTtlSeconds 300 (pid-less owners only; a live holder process keeps a lease regardless),
      -BootTtlSeconds 180.

    -BatchLogPath IS LOAD-BEARING
      RunBatch frees the machine-wide boot lane as soon as the child's log shows startup is past
      the contention window ("Application.AssetDatabase Initial Refresh Start"). Without
      -BatchLogPath the lane stays held for -BatchBootSeconds (or -BootTtlSeconds), serializing
      every other project for that long. Pass the log the child actually writes.

    OWNED STATE SCHEMAS (this script writes them; nothing else may read them)
      <StateRoot>/owners/<projectKey>/owner.json - lease, slot, mode, projectPath, projectKey,
        processId (0 until Attach), holderProcessId + holderStartTime (the coordinator holding it),
        acquiredAt, updatedAt. Read it back through Status.owners[].
      <StateRoot>/queue/<timestamp>-<guid>.json - lease, slot, mode, projectPath, requestedAt,
        updatedAt. Read it back through Status.queue[] (position is 1-based, per project).
      <StateRoot>/boot/boot.json - lease, projectPath, processId, acquiredAt. Read it back through
        Status.boot; an unowned dir that cannot be removed surfaces as Status.bootWedged.
      <StateRoot>/owner/owner.json is the retired single-owner record, honored until it clears.

.NOTES
    Routed leaselessness (fork 4, RULED accepted): a -Routed unity_test_agent run attaches to an
    editor someone else already leased and takes no lease of its own. It verifies through Status
    that the project has a live editor owner, then runs beside it. Accepted as dev-loop behavior;
    a read-style co-lease is not planned.

    -ProcessSnapshotPath replaces live process enumeration with a JSON file (tests only).
    -PrimaryRoot overrides the git-derived primary worktree - the thing that makes "the user's main
    editor" and slot-to-project resolution machine-dependent; tests inject it to stay hermetic.
#>
param(
    [ValidateSet("Status", "Contract", "Request", "Acquire", "Wait", "Attach", "AttachBatchChild", "Adopt", "Release", "Cancel", "BootAcquire", "BootRelease", "StartEditor", "RunBatch")]
    [string]$Action = "Status",
    [string]$Lease = "",
    [string]$Slot = "",
    [ValidateSet("editor", "batch")]
    [string]$Mode = "batch",
    [string]$ProjectPath = "",
    [int]$ProcessId = 0,
    [int]$WaitSeconds = 0,
    [int]$PollSeconds = 2,
    [int]$TicketTtlSeconds = 900,
    [int]$OwnerTtlSeconds = 300,
    [int]$BootTtlSeconds = 180,
    [int]$EditorCloseWaitSeconds = 30,
    [string]$StateRoot = "",
    [string]$PrimaryRoot = "",
    [string]$ProcessSnapshotPath = "",
    [string]$UnityPath = "",
    [switch]$CloseEditor,
    [string[]]$EditorArgs = @(),
    [ValidateSet("LowMemory", "HighFidelity")]
    [string]$EditorProfile = "LowMemory",
    [int]$ProfileWaitSeconds = 300,
    [string]$BatchScript = "",
    [string[]]$BatchArguments = @(),
    [string]$BatchLogPath = "",
    [int]$BatchBootSeconds = 0,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---- Section map -------------------------------------------------------------
#   Constants & exit codes    $Exit*, status -> exit table, boot-complete pattern
#   Path & root helpers       Resolve-FullPath, Get-PrimaryRoot, Normalize-Path, Get-ProjectKey
#   Lock records              Read-Record, Get-RecordOrReap, Write-JsonFile, Move-RecordDirIntoPlace
#   Discovery                 Get-WorktreePath, Unity process enumeration, Get-DateValue, Get-MemberValue
#   Ticket queue              Remove-TicketFile .. Ensure-Ticket
#   Editor profile receipts   Get-EditorProfileQuality .. Test-EditorProfileReceipt
#   Owner liveness & records  Get-ProcessStartTime .. Get-TrackedPids
#   Blockers                  Get-Blockers, Get-BlockedStatus
#   Status & Contract         Add-NormalizedProjectPath, Get-StatusValue, Get-ContractValue
#   Project lease             Get-QueuePosition, Request/Try-Acquire/Acquire-Access, Write-OwnerHeartbeat
#   Boot lane                 Start-BootLaneSidecar, Try-AcquireBoot, Acquire-Boot, Release-Boot
#   Attach / adopt / release  Attach-Process, Attach-BatchChild, Adopt-Process, Cancel-Request, Release-Access
#   Composite actions         Start-TrackedEditor, Run-TrackedBatch
#   Result channel & dispatch Require-Lease, Write-Result, state roots, action switch, exit
# ------------------------------------------------------------------------------

# ---- Constants & exit codes ------------------------------------------------
$ExitWaiting = 20
$ExitUnmanaged = 21
$ExitOwnership = 22
$ExitIncomplete = 23
$ExitAdoptRefused = 24
$ExitBootWedged = 25
$ExitProfile = 26
$ExitRecordUnreadable = 27
$RecordUnreadableTag = "UNITY_ACCESS_RECORD_UNREADABLE"
$statusExitCodes = @{
    ownership_mismatch = $ExitOwnership
    editor_did_not_exit = $ExitIncomplete
    blocked_unmanaged_unity = $ExitUnmanaged
    waiting = $ExitWaiting
    boot_waiting = $ExitWaiting
    boot_lane_wedged = $ExitBootWedged
    editor_profile_failed = $ExitProfile
    blocked_user_editor = $ExitWaiting
    adopt_no_process = $ExitAdoptRefused
    adopt_already_tracked = $ExitAdoptRefused
    adopt_refused_user_editor = $ExitAdoptRefused
    adopt_project_owned = $ExitAdoptRefused
    record_unreadable = $ExitRecordUnreadable
    coordinator_error = 1
}
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
# Boot ends once licensing + global package-cache work gives way to per-project Library work
# (postmortem D6). The single home: callers that drive the lane themselves read it via -Action Contract.
$BootCompletePattern = 'Application\.AssetDatabase Initial Refresh Start'

. (Join-Path $PSScriptRoot "lib/repo_root.ps1")
. (Join-Path $PSScriptRoot "lib/unity_editor.ps1")
. (Join-Path $PSScriptRoot "lib/process_tree.ps1")

# ---- Path & root helpers ---------------------------------------------------
function Resolve-FullPath {
    param([string]$Path, [string]$Base = (Get-Location).Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path $Base $Path))
}

function Get-PrimaryRoot {
    if (-not [string]::IsNullOrWhiteSpace($PrimaryRoot)) { return Resolve-FullPath $PrimaryRoot }
    $repo = Get-RepoRoot -ProbePath $PSScriptRoot
    $common = (& git -C $repo rev-parse --path-format=absolute --git-common-dir 2>$null | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($common)) { throw "Could not resolve the primary git directory." }
    return Split-Path -Parent (Resolve-FullPath $common)
}

function Normalize-Path {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    return (Resolve-FullPath $Path).Replace('\', '/').TrimEnd('/').ToLowerInvariant()
}

function Get-ProjectKey {
    param([string]$Path)
    $normalized = Normalize-Path $Path
    if ([string]::IsNullOrWhiteSpace($normalized)) { return "unknown" }
    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try { $digest = [BitConverter]::ToString($sha1.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($normalized))) }
    finally { $sha1.Dispose() }
    $tail = ($normalized -replace '[^a-z0-9]+', '-').Trim('-')
    if ($tail.Length -gt 40) { $tail = $tail.Substring($tail.Length - 40).Trim('-') }
    return "$tail-" + $digest.Replace("-", "").Substring(0, 8).ToLowerInvariant()
}

# ---- Lock records ----------------------------------------------------------
# An absent record and an unreadable one are different facts. Reading a corrupt record as absent
# reaped live state, so a file that exists but will not parse fails the call by name instead.
# A record swapped in by Move-Item is briefly unopenable to a concurrent reader, and that is
# contention, not corruption: opening retries, and only the parse verdict is final.
function Read-Record {
    param([string]$Path)
    $text = $null
    foreach ($attempt in 1..5) {
        if (-not (Test-Path -LiteralPath $Path)) { return $null }
        try { $text = [System.IO.File]::ReadAllText($Path); break }
        catch [System.IO.IOException] { Start-Sleep -Milliseconds 40 }
        catch [System.UnauthorizedAccessException] { Start-Sleep -Milliseconds 40 }
    }
    if ($null -eq $text) { throw "Could not open $($Path) while another coordinator held it." }
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    try { return $text | ConvertFrom-Json }
    catch { throw "$($RecordUnreadableTag)|$Path|$($_.Exception.Message)" }
}

# The one read-and-reap policy for every lock record (project owner, legacy owner, boot lane).
# Its error policy: a record-less dir is leftover garbage and is reaped; a stale record is reaped;
# an unreadable one is never reaped. Concurrent coordinators race every prune here, and a lost race
# leaves the dir already gone, which is the goal - so removal failure is not this call's problem.
function Get-RecordOrReap {
    param([string]$Dir, [string]$FileName, [scriptblock]$IsStale)
    $record = Read-Record (Join-Path $Dir $FileName)
    if ($null -eq $record) {
        if (Test-Path -LiteralPath $Dir) { Remove-Item -LiteralPath $Dir -Recurse -Force -ErrorAction SilentlyContinue }
        return $null
    }
    if (& $IsStale $record) {
        Remove-Item -LiteralPath $Dir -Recurse -Force -ErrorAction SilentlyContinue
        return $null
    }
    return $record
}

function Write-JsonFile {
    param([string]$Path, [object]$Value)
    $parent = Split-Path -Parent $Path
    # A lock dir is the mutex; recreating a missing parent here silently resurrected a lock a
    # rival had already reaped, and both callers walked away believing they held it.
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw "Refusing to write $($Path): its directory is gone." }
    $temp = Join-Path $parent (([System.IO.Path]::GetFileName($Path)) + "." + [guid]::NewGuid().ToString("N") + ".tmp")
    [System.IO.File]::WriteAllText($temp, ($Value | ConvertTo-Json -Depth 8), $Utf8NoBom)
    Move-Item -LiteralPath $temp -Destination $Path -Force
}

# A lock dir is the mutex, so it must never be observable without its record: the record is built in
# a staging dir and that DIRECTORY is renamed into place. The rename is atomic, only one racer's can
# land, and no reader ever sees a record-less dir to reap out from under the winner.
function Move-RecordDirIntoPlace {
    param([string]$Destination, [string]$FileName, [object]$Record)
    New-Item -ItemType Directory -Force -Path $StagingRoot | Out-Null
    $staging = Join-Path $StagingRoot ([guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $staging -ErrorAction Stop | Out-Null
    try {
        Write-JsonFile (Join-Path $staging $FileName) $Record
        [System.IO.Directory]::Move($staging, $Destination)
        return [pscustomobject]@{ moved = $true; error = "" }
    }
    catch {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        return [pscustomobject]@{ moved = $false; error = $_.Exception.Message }
    }
}

# ---- Discovery -------------------------------------------------------------
function Get-WorktreePath {
    param([string]$RequestedSlot)
    if ([string]::IsNullOrWhiteSpace($RequestedSlot) -or $RequestedSlot -eq "main") { return $PrimaryRoot }
    $path = ""
    $candidate = ""
    foreach ($line in (& git -C $PrimaryRoot worktree list --porcelain)) {
        if ($line -like "worktree *") { $candidate = $line.Substring(9) }
        elseif ($line -eq "branch refs/heads/$RequestedSlot") { $path = $candidate; break }
    }
    if ([string]::IsNullOrWhiteSpace($path)) { throw "Unknown worktree slot: $RequestedSlot" }
    return Resolve-FullPath $path
}

function Get-UnityProcesses {
    # An enumeration that failed is not evidence of no Unity: answering @() here reaped live
    # pid-backed owners and boot records on a WMI hiccup, so a failure is loud instead.
    if (-not [string]::IsNullOrWhiteSpace($ProcessSnapshotPath)) {
        $snapshotPath = Resolve-FullPath $ProcessSnapshotPath
        if (-not (Test-Path -LiteralPath $snapshotPath)) { throw "Process snapshot not found: $snapshotPath" }
        $snapshot = Read-Record $snapshotPath
        if ($null -eq $snapshot) { return @() }
        return @($snapshot)
    }
    try {
        return @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction Stop | ForEach-Object {
            [pscustomobject]@{ processId = [int]$_.ProcessId; commandLine = [string]$_.CommandLine }
        })
    }
    catch { throw "Unity process enumeration failed: $($_.Exception.Message)" }
}

function Get-RelevantUnityProcesses {
    $result = @()
    foreach ($process in @(Get-UnityProcesses)) {
        $command = [string]$process.commandLine
        if ([string]::IsNullOrWhiteSpace($command)) { continue }
        if ($command -match '(?i)-name\s+"?AssetImportWorker') { continue }
        if ($command -notmatch '(?i)-projectpath\s+(?:"([^"]+)"|([^\s]+))') { continue }
        $path = if (-not [string]::IsNullOrWhiteSpace($Matches[1])) { $Matches[1] } else { $Matches[2] }
        $result += [pscustomobject]@{
            processId = [int]$process.processId
            commandLine = $command
            projectPath = Resolve-FullPath $path
            normalizedProjectPath = Normalize-Path $path
            batch = ($command -match '(?i)-batchmode')
        }
    }
    return $result
}

function Test-UnityProcessLive {
    param([int]$TargetProcessId)
    return @(Get-RelevantUnityProcesses | Where-Object { $_.processId -eq $TargetProcessId }).Count -gt 0
}

# True once the pid is gone, false if it outlives the wait.
function Wait-ProcessExit {
    param([int]$TargetProcessId, [int]$Seconds)
    $deadline = [datetime]::UtcNow.AddSeconds([Math]::Max(0, $Seconds))
    while ($null -ne (Get-Process -Id $TargetProcessId -ErrorAction SilentlyContinue) -and [datetime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500 }
    return $null -eq (Get-Process -Id $TargetProcessId -ErrorAction SilentlyContinue)
}

function Get-DateValue {
    param([object]$Value)
    $parsed = [datetime]::MinValue
    if ($null -ne $Value) { [void][datetime]::TryParse([string]$Value, [ref]$parsed) }
    return $parsed.ToUniversalTime()
}

function Get-MemberValue {
    param([object]$Object, [string]$Name)
    if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name]) { return $null }
    return $Object.PSObject.Properties[$Name].Value
}

# ---- Ticket queue ----------------------------------------------------------
# Tickets are deleted by whoever gets there first - the owner that seated them, a rival reaping the
# TTL, a release cancelling its own. Deletion is called after the owner dir is already claimed, so a
# racer losing to a concurrent delete or an open handle must not exit nonzero holding a live lease.
function Remove-TicketFile {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { throw "Ticket file path is required." }
    try { [System.IO.File]::Delete($Path) }
    catch [System.IO.IOException] { }
    catch [System.UnauthorizedAccessException] { }
}

function Remove-StaleTickets {
    $now = [datetime]::UtcNow
    foreach ($file in @(Get-ChildItem -LiteralPath $QueueRoot -Filter "*.json" -File -ErrorAction SilentlyContinue)) {
        $ticket = Read-Record $file.FullName
        $updated = if ($null -ne $ticket) { Get-DateValue $ticket.updatedAt } else { [datetime]::MinValue }
        if (($now - $updated).TotalSeconds -gt $TicketTtlSeconds) { Remove-TicketFile $file.FullName }
    }
}

function Get-Tickets {
    Remove-StaleTickets
    $items = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $QueueRoot -Filter "*.json" -File -ErrorAction SilentlyContinue | Sort-Object Name)) {
        $ticket = Read-Record $file.FullName
        if ($null -ne $ticket) { $items += [pscustomobject]@{ file = $file.FullName; data = $ticket } }
    }
    return $items
}

function Find-Ticket {
    param([string]$RequestedLease)
    $matches = @(Get-Tickets | Where-Object { [string]$_.data.lease -eq $RequestedLease })
    if ($matches.Count -eq 0) { return $null }
    return $matches[0]
}

function Ensure-Ticket {
    param([string]$RequestedLease, [string]$RequestedSlot, [string]$RequestedMode, [string]$RequestedProject)
    $existing = Find-Ticket $RequestedLease
    $now = [datetime]::UtcNow.ToString("o")
    if ($null -ne $existing) {
        # A refreshed ticket describes the request being made now, not the one that first minted it:
        # a lease that re-requests with a different slot/mode/project must not queue under the old one.
        $existing.data | Add-Member -NotePropertyName slot -NotePropertyValue $RequestedSlot -Force
        $existing.data | Add-Member -NotePropertyName mode -NotePropertyValue $RequestedMode -Force
        $existing.data | Add-Member -NotePropertyName projectPath -NotePropertyValue $RequestedProject -Force
        $existing.data | Add-Member -NotePropertyName updatedAt -NotePropertyValue $now -Force
        Write-JsonFile $existing.file $existing.data
        return $existing
    }
    $name = ([datetime]::UtcNow.ToString("yyyyMMddHHmmssfffffff")) + "-" + [guid]::NewGuid().ToString("N") + ".json"
    $path = Join-Path $QueueRoot $name
    $ticket = [ordered]@{
        lease = $RequestedLease
        slot = $RequestedSlot
        mode = $RequestedMode
        projectPath = $RequestedProject
        requestedAt = $now
        updatedAt = $now
    }
    Write-JsonFile $path $ticket
    return [pscustomobject]@{ file = $path; data = [pscustomobject]$ticket }
}

# ---- Editor profile receipts -----------------------------------------------
function Get-EditorProfileQuality {
    param([string]$Profile)
    switch ($Profile) {
        "LowMemory" { return "Performant" }
        "HighFidelity" { return "High Fidelity" }
        default { throw "Unknown editor profile: $Profile" }
    }
}

function New-EditorProfileReceiptPath {
    New-Item -ItemType Directory -Force -Path $ProfileReceiptRoot | Out-Null
    return Join-Path $ProfileReceiptRoot ([guid]::NewGuid().ToString("N") + ".json")
}

function Get-EditorProfileReceipt {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { return [pscustomobject]@{ error = "Profile receipt is malformed: $($_.Exception.Message)" } }
}

function Wait-EditorProfileReceipt {
    param([string]$Path, [System.Diagnostics.Process]$Process)
    $deadline = [datetime]::UtcNow.AddSeconds([Math]::Max(1, $ProfileWaitSeconds))
    while ([datetime]::UtcNow -lt $deadline) {
        $receipt = Get-EditorProfileReceipt $Path
        if ($null -ne $receipt) { return $receipt }
        if ($Process.HasExited) {
            return [pscustomobject]@{ error = "Editor exited before writing profile receipt." }
        }
        Start-Sleep -Milliseconds 200
    }
    return $null
}

function New-ProfileVerdict {
    param([bool]$Verified, [string]$RequestedProfile, [string]$ObservedQuality, [string]$Note)
    return [ordered]@{ verified = $Verified; requestedProfile = $RequestedProfile; observedQuality = $ObservedQuality; note = $Note }
}

function Test-EditorProfileReceipt {
    param([object]$Receipt, [string]$RequestedProfile)
    $expectedQuality = Get-EditorProfileQuality $RequestedProfile
    if ($null -eq $Receipt) { return New-ProfileVerdict $false $RequestedProfile "" "Timed out waiting for profile receipt." }
    $error = [string](Get-MemberValue $Receipt "error")
    if (-not [string]::IsNullOrWhiteSpace($error)) { return New-ProfileVerdict $false $RequestedProfile "" $error }
    $receiptProfile = [string](Get-MemberValue $Receipt "requestedProfile")
    $observedQuality = [string](Get-MemberValue $Receipt "observedQuality")
    if ($receiptProfile -ne $RequestedProfile -or $observedQuality -ne $expectedQuality) {
        return New-ProfileVerdict $false $RequestedProfile $observedQuality "Profile receipt expected $RequestedProfile/$expectedQuality but reported $receiptProfile/$observedQuality."
    }
    return New-ProfileVerdict $true $RequestedProfile $observedQuality ""
}

# ---- Owner liveness & records ----------------------------------------------
function Get-ProcessStartTime {
    param([int]$TargetProcessId)
    $process = Get-Process -Id $TargetProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) { return "" }
    return $process.StartTime.ToUniversalTime().ToString("o")
}

# A bare PID is not an identity: Windows recycles them, so a stranger wearing a dead holder's
# number kept a dead lease alive. Identity is the pid plus the start time recorded with it.
function Test-HolderAlive {
    param([int]$HolderProcessId, [object]$RecordedStartTime)
    $process = Get-Process -Id $HolderProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) { return $false }
    $recorded = [string]$RecordedStartTime
    # Records written before holderStartTime existed carry no identity to check; reading them as
    # live keeps live leases safe across the migration, at the pid-recycling risk they already ran.
    if ([string]::IsNullOrWhiteSpace($recorded)) { return $true }
    return ([Math]::Abs(((Get-DateValue $recorded) - $process.StartTime.ToUniversalTime()).TotalSeconds) -lt 2)
}

function Test-OwnerStale {
    param([object]$Owner)
    # A lease whose holding coordinator still runs is busy, whatever its opaque child is doing —
    # checked first so a child that exits just before the release does not open a reap window.
    $holder = [int](Get-MemberValue $Owner "holderProcessId")
    if ($holder -gt 0 -and (Test-HolderAlive $holder (Get-MemberValue $Owner "holderStartTime"))) { return $false }
    if ([int]$Owner.processId -gt 0) { return -not (Test-UnityProcessLive ([int]$Owner.processId)) }
    return ([datetime]::UtcNow - (Get-DateValue $Owner.updatedAt)).TotalSeconds -gt $OwnerTtlSeconds
}

function Get-OwnerDir {
    param([string]$RequestedKey)
    return Join-Path $OwnersRoot $RequestedKey
}

function Get-OwnerRecordPath {
    param([string]$RequestedKey)
    return Join-Path (Get-OwnerDir $RequestedKey) "owner.json"
}

function Get-ProjectOwner {
    param([string]$RequestedKey)
    return Get-RecordOrReap (Get-OwnerDir $RequestedKey) "owner.json" { param($r) Test-OwnerStale $r }
}

function Get-AllOwners {
    $owners = @()
    foreach ($dir in @(Get-ChildItem -LiteralPath $OwnersRoot -Directory -ErrorAction SilentlyContinue)) {
        $owner = Get-ProjectOwner $dir.Name
        if ($null -ne $owner) { $owners += $owner }
    }
    return $owners
}

function Find-OwnerByLease {
    param([string]$RequestedLease)
    $matches = @(Get-AllOwners | Where-Object { [string]$_.lease -eq $RequestedLease })
    if ($matches.Count -eq 0) { return $null }
    return $matches[0]
}

# Single-owner state written by pre-two-tier script copies still in live sessions; honored as machine-wide until it clears.
function Get-LegacyOwner {
    return Get-RecordOrReap $LegacyOwnerRoot "owner.json" { param($r) Test-OwnerStale $r }
}

# An undeletable leftover here surfaces as boot_lane_wedged at the next acquire, never as a throw.
function Get-BootOwner {
    return Get-RecordOrReap $BootRoot "boot.json" {
        param($boot)
        if (([datetime]::UtcNow - (Get-DateValue $boot.acquiredAt)).TotalSeconds -gt $BootTtlSeconds) { return $true }
        if ([int]$boot.processId -le 0) { return $false }
        return -not (Test-UnityProcessLive ([int]$boot.processId))
    }
}

function Get-TrackedPids {
    $pids = @(Get-AllOwners | ForEach-Object { [int]$_.processId } | Where-Object { $_ -gt 0 })
    $legacy = Get-LegacyOwner
    if ($null -ne $legacy -and [int]$legacy.processId -gt 0) { $pids += [int]$legacy.processId }
    return $pids
}

# ---- Blockers --------------------------------------------------------------
function Get-Blockers {
    param([string]$RequestedProject = "", [string]$RequestedMode = "")
    $mainProject = Normalize-Path (Join-Path $PrimaryRoot "src/Asteroids3D")
    $requested = Normalize-Path $RequestedProject
    $tracked = @(Get-TrackedPids)
    $blockers = @()
    foreach ($process in @(Get-RelevantUnityProcesses)) {
        if ($tracked -contains $process.processId) { continue }
        # Untracked batch processes may be mid-boot (the D6 hazard) so they block everywhere; untracked editors are long-lived and only contend on their own project. Editor-mode requests stay machine-wide strict.
        if ($RequestedMode -eq "batch" -and -not $process.batch -and -not [string]::IsNullOrWhiteSpace($requested) -and $process.normalizedProjectPath -ne $requested) { continue }
        $kind = "unmanaged_unity"
        if (-not $process.batch -and $process.normalizedProjectPath -eq $mainProject) { $kind = "user_editor" }
        $blockers += [ordered]@{
            kind = $kind
            processId = $process.processId
            projectPath = $process.projectPath
            batch = $process.batch
        }
    }
    return $blockers
}

function Get-BlockedStatus {
    param([object[]]$Blockers)
    if (@($Blockers | Where-Object { $_.kind -eq "user_editor" }).Count -gt 0) { return "blocked_user_editor" }
    return "blocked_unmanaged_unity"
}

# ---- Status & Contract -----------------------------------------------------
# normalizedProjectPath is THE comparison key for "is this owner on my project": the coordinator's own
# normalization, applied here so no consumer re-implements it (script-contracts.md sec.3). It is derived
# per call, so owner records written before this existed answer too.
function Add-NormalizedProjectPath {
    param([object]$Owner)
    if ($null -eq $Owner) { return $null }
    $normalized = Normalize-Path ([string]$Owner.projectPath)
    if ($Owner -is [System.Collections.IDictionary]) { $Owner["normalizedProjectPath"] = $normalized }
    else { $Owner | Add-Member -NotePropertyName normalizedProjectPath -NotePropertyValue $normalized -Force }
    return $Owner
}

function Get-StatusValue {
    $owners = @(Get-AllOwners | ForEach-Object { Add-NormalizedProjectPath $_ })
    $tickets = @(Get-Tickets)
    $queue = @()
    for ($i = 0; $i -lt $tickets.Count; $i++) {
        $queue += [ordered]@{
            position = $i + 1
            lease = [string]$tickets[$i].data.lease
            slot = [string]$tickets[$i].data.slot
            mode = [string]$tickets[$i].data.mode
            projectPath = [string]$tickets[$i].data.projectPath
            requestedAt = [string]$tickets[$i].data.requestedAt
        }
    }
    $boot = Get-BootOwner
    $state = [ordered]@{
        stateRoot = $AccessRoot
        owners = $owners
        legacyOwner = Add-NormalizedProjectPath (Get-LegacyOwner)
        boot = $boot
        # Get-BootOwner just tried to reap any unowned boot dir; one that survives is wedged.
        bootWedged = ($null -eq $boot -and (Test-Path -LiteralPath $BootRoot))
        queue = $queue
        blockers = @(Get-Blockers)
    }

    # "Who owns this path, and what Unity processes sit on it" - asked of the coordinator rather than
    # re-derived by each consumer from the process table.
    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
        $target = Normalize-Path $ResolvedProject
        $state.requestedProjectPath = $ResolvedProject
        $state.requestedNormalizedProjectPath = $target
        $state.projectOwner = @($owners | Where-Object { [string]$_.normalizedProjectPath -eq $target }) | Select-Object -First 1
        $state.projectProcesses = @(Get-RelevantUnityProcesses | Where-Object { $_.normalizedProjectPath -eq $target } | ForEach-Object {
            [ordered]@{ processId = $_.processId; projectPath = $_.projectPath; normalizedProjectPath = $_.normalizedProjectPath; batch = $_.batch }
        })
    }
    return $state
}

# The one home for constants a caller must match exactly. A caller that hard-codes any of these is
# keeping a copy that silently drifts - ask for them.
function Get-ContractValue {
    return [ordered]@{
        status = "contract"
        bootCompletePattern = $BootCompletePattern
        ticketTtlSeconds = $TicketTtlSeconds
        ownerTtlSeconds = $OwnerTtlSeconds
        bootTtlSeconds = $BootTtlSeconds
    }
}

# ---- Project lease ---------------------------------------------------------
function Get-QueuePosition {
    param([string]$RequestedLease, [string]$RequestedKey)
    $projectTickets = @(Get-Tickets | Where-Object { (Get-ProjectKey ([string]$_.data.projectPath)) -eq $RequestedKey })
    $position = 1 + [array]::IndexOf(@($projectTickets | ForEach-Object { [string]$_.data.lease }), $RequestedLease)
    # Callers only ask after seating their own ticket, so an absent one means the queue lost it
    # under us. Position 0 is not a wait state - reporting it as one queues the lease forever.
    if ($position -le 0) { throw "Queue invariant violated: lease '$RequestedLease' has no ticket for project $RequestedKey." }
    return $position
}

function Request-Access {
    [void](Ensure-Ticket $Lease $Slot $Mode $ResolvedProject)
    $position = Get-QueuePosition $Lease $ProjectKey
    return [ordered]@{ status = "queued"; lease = $Lease; slot = $Slot; mode = $Mode; projectPath = $ResolvedProject; position = $position }
}

function Try-AcquireAccess {
    $current = Get-ProjectOwner $ProjectKey
    if ($null -ne $current -and [string]$current.lease -eq $Lease) {
        Write-OwnerHeartbeat $current
        return [ordered]@{ status = "acquired"; owner = $current; renewed = $true }
    }

    [void](Request-Access)
    $position = Get-QueuePosition $Lease $ProjectKey
    $legacy = Get-LegacyOwner
    if ($null -ne $legacy) {
        return [ordered]@{ status = "waiting"; position = $position; owner = $legacy }
    }
    if ($null -ne $current) {
        return [ordered]@{ status = "waiting"; position = $position; owner = $current }
    }

    $blockers = @(Get-Blockers $ResolvedProject $Mode)
    if ($blockers.Count -gt 0) {
        $status = Get-BlockedStatus $blockers
        return [ordered]@{ status = $status; position = $position; blockers = $blockers }
    }

    if ($position -ne 1) { return [ordered]@{ status = "waiting"; position = $position; owner = $null } }

    $ownerDir = Get-OwnerDir $ProjectKey
    $now = [datetime]::UtcNow.ToString("o")
    $owner = [ordered]@{
        lease = $Lease
        slot = $Slot
        mode = $Mode
        projectPath = $ResolvedProject
        projectKey = $ProjectKey
        processId = 0
        holderProcessId = $PID
        holderStartTime = Get-ProcessStartTime $PID
        acquiredAt = $now
        updatedAt = $now
    }
    $claim = Move-RecordDirIntoPlace $ownerDir "owner.json" $owner
    if (-not $claim.moved) { return [ordered]@{ status = "waiting"; position = $position; owner = Get-ProjectOwner $ProjectKey } }

    $ownTicket = Find-Ticket $Lease
    if ($null -ne $ownTicket) { Remove-TicketFile $ownTicket.file }
    return [ordered]@{ status = "acquired"; owner = [pscustomobject]$owner; renewed = $false }
}

function Acquire-Access {
    $deadline = [datetime]::UtcNow.AddSeconds([Math]::Max(0, $WaitSeconds))
    do {
        $result = Try-AcquireAccess
        if ($result.status -eq "acquired") { return $result }
        if ([datetime]::UtcNow -ge $deadline) { return $result }
        Start-Sleep -Seconds ([Math]::Max(1, $PollSeconds))
    } while ($true)
}

# A pid-less owner ages out on OwnerTtlSeconds alone; anything longer-lived must keep it fresh.
function Write-OwnerHeartbeat {
    param([object]$Owner)
    # Renewing hands the lease to whoever renewed it; a dead holder falls back to the TTL.
    $Owner | Add-Member -NotePropertyName holderProcessId -NotePropertyValue $PID -Force
    $Owner | Add-Member -NotePropertyName holderStartTime -NotePropertyValue (Get-ProcessStartTime $PID) -Force
    $Owner.updatedAt = [datetime]::UtcNow.ToString("o")
    Write-JsonFile (Get-OwnerRecordPath ([string]$Owner.projectKey)) $Owner
}

# ---- Boot lane -------------------------------------------------------------
# Claims the child's Unity, then frees the machine-wide lane once the caller's declared log shows
# startup is past the contention window. Runs beside the blocking child and exits on its own.
# Both halves matter: an unclaimed child blocks every project no matter who holds the lane.
function Start-BootLaneSidecar {
    $settings = @{
        coordinator = $PSCommandPath
        client = Join-Path $PSScriptRoot "unity_access_client.ps1"
        lease = $Lease
        stateRoot = $AccessRoot
        snapshot = $ProcessSnapshotPath
        logPath = $BatchLogPath
        pattern = $BootCompletePattern
        pollSeconds = [Math]::Max(1, $PollSeconds)
        deadline = ([datetime]::UtcNow.AddSeconds($(if ($BatchBootSeconds -gt 0) { $BatchBootSeconds } else { $BootTtlSeconds }))).ToString("o")
    }
    return Start-Job -ScriptBlock {
        param($s)
        . $s.client
        $common = @("-Lease", $s.lease, "-StateRoot", $s.stateRoot)
        if (-not [string]::IsNullOrWhiteSpace($s.snapshot)) { $common += @("-ProcessSnapshotPath", $s.snapshot) }
        $attached = $false
        while ($true) {
            if (-not $attached) {
                $call = Invoke-UnityAccessCoordinator -Coordinator $s.coordinator -CoordinatorArgs (@("-Action", "AttachBatchChild") + $common)
                $attached = ($null -ne $call.result -and [string]$call.result.status -eq "attached")
            }
            $bootDone = [datetime]::UtcNow -ge [datetime]::Parse($s.deadline).ToUniversalTime()
            if (-not $bootDone -and -not [string]::IsNullOrWhiteSpace($s.logPath) -and (Test-Path -LiteralPath $s.logPath)) {
                $bootDone = [bool](Select-String -LiteralPath $s.logPath -Pattern $s.pattern -Quiet -ErrorAction SilentlyContinue)
            }
            if ($bootDone) { break }
            Start-Sleep -Seconds $s.pollSeconds
        }
        [void](Invoke-UnityAccessCoordinator -Coordinator $s.coordinator -CoordinatorArgs (@("-Action", "BootRelease") + $common))
    } -ArgumentList $settings
}

function Try-AcquireBoot {
    $owner = Find-OwnerByLease $Lease
    if ($null -eq $owner) { return [ordered]@{ status = "ownership_mismatch"; note = "BootAcquire requires holding a project owner lease." } }

    # A pid-less owner queued behind the boot lane must not age out while it waits.
    Write-OwnerHeartbeat $owner

    $boot = Get-BootOwner
    if ($null -ne $boot -and [string]$boot.lease -eq $Lease) {
        return [ordered]@{ status = "boot_acquired"; boot = $boot; renewed = $true }
    }
    if ($null -ne $boot) { return [ordered]@{ status = "boot_waiting"; boot = $boot } }

    $blockers = @(Get-Blockers ([string]$owner.projectPath) ([string]$owner.mode))
    if ($blockers.Count -gt 0) {
        $status = Get-BlockedStatus $blockers
        return [ordered]@{ status = $status; blockers = $blockers }
    }

    $record = [ordered]@{
        lease = $Lease
        projectPath = [string]$owner.projectPath
        processId = 0
        acquiredAt = [datetime]::UtcNow.ToString("o")
    }
    $claim = Move-RecordDirIntoPlace $BootRoot "boot.json" $record
    if (-not $claim.moved) {
        $boot = Get-BootOwner
        if ($null -ne $boot) { return [ordered]@{ status = "boot_waiting"; boot = $boot } }
        # The dir is unowned yet undeletable (stray handle/CWD holds it); waiting would never end.
        return [ordered]@{ status = "boot_lane_wedged"; error = $claim.error; bootRoot = $BootRoot }
    }
    return [ordered]@{ status = "boot_acquired"; boot = [pscustomobject]$record; renewed = $false }
}

function Acquire-Boot {
    $deadline = [datetime]::UtcNow.AddSeconds([Math]::Max(0, $WaitSeconds))
    do {
        # boot_lane_wedged retries too: only a wedge that outlives the wait reaches the caller.
        $result = Try-AcquireBoot
        if ($result.status -in @("boot_acquired", "ownership_mismatch")) { return $result }
        if ([datetime]::UtcNow -ge $deadline) { return $result }
        Start-Sleep -Seconds ([Math]::Max(1, $PollSeconds))
    } while ($true)
}

function Release-Boot {
    $boot = Get-BootOwner
    if ($null -eq $boot) { return [ordered]@{ status = "boot_released"; alreadyFree = $true } }
    if ([string]$boot.lease -ne $Lease) { return [ordered]@{ status = "ownership_mismatch"; boot = $boot } }
    try { Remove-Item -LiteralPath $BootRoot -Recurse -Force -ErrorAction Stop }
    catch {
        # Missing already (a concurrent reaper won) is a successful release; a still-present dir is not.
        if (Test-Path -LiteralPath $BootRoot) {
            return [ordered]@{ status = "boot_lane_wedged"; error = $_.Exception.Message; bootRoot = $BootRoot }
        }
    }
    return [ordered]@{ status = "boot_released"; alreadyFree = $false }
}

# ---- Attach / adopt / release ----------------------------------------------
function Attach-Process {
    $owner = Find-OwnerByLease $Lease
    if ($null -eq $owner) { return [ordered]@{ status = "ownership_mismatch" } }
    $owner.processId = $ProcessId
    $owner.updatedAt = [datetime]::UtcNow.ToString("o")
    Write-JsonFile (Get-OwnerRecordPath ([string]$owner.projectKey)) $owner
    $boot = Get-BootOwner
    if ($null -ne $boot -and [string]$boot.lease -eq $Lease) {
        $boot.processId = $ProcessId
        Write-JsonFile (Join-Path $BootRoot "boot.json") $boot
    }
    return [ordered]@{ status = "attached"; owner = $owner }
}

# A batch child's Unity names itself to nobody, so the owner stays pid-less and its own child reads
# as an unmanaged process that blocks every project. Claim it as soon as it appears.
function Attach-BatchChild {
    $owner = Find-OwnerByLease $Lease
    if ($null -eq $owner) { return [ordered]@{ status = "ownership_mismatch" } }
    if ([int]$owner.processId -gt 0) { return [ordered]@{ status = "attached"; owner = $owner } }
    $target = Normalize-Path ([string]$owner.projectPath)
    $tracked = @(Get-TrackedPids)
    $match = @(Get-RelevantUnityProcesses | Where-Object {
        $_.batch -and $_.normalizedProjectPath -eq $target -and $tracked -notcontains $_.processId })
    if ($match.Count -eq 0) { return [ordered]@{ status = "batch_child_absent" } }
    $script:ProcessId = [int]$match[0].processId
    return Attach-Process
}

# Operator-invoked recovery for an orphaned untracked editor; guards stay minimal because the PID is supplied explicitly.
function Adopt-Process {
    $target = @(Get-RelevantUnityProcesses | Where-Object { $_.processId -eq $ProcessId })
    if ($target.Count -eq 0) { return [ordered]@{ status = "adopt_no_process"; processId = $ProcessId } }
    if (@(Get-TrackedPids) -contains $ProcessId) { return [ordered]@{ status = "adopt_already_tracked"; processId = $ProcessId } }
    $found = $target[0]
    $mainProject = Normalize-Path (Join-Path $PrimaryRoot "src/Asteroids3D")
    if (-not $found.batch -and $found.normalizedProjectPath -eq $mainProject) {
        return [ordered]@{ status = "adopt_refused_user_editor"; processId = $ProcessId; projectPath = $found.projectPath }
    }
    $adoptKey = Get-ProjectKey $found.projectPath
    $existing = Get-ProjectOwner $adoptKey
    if ($null -ne $existing -and [string]$existing.lease -ne $Lease) {
        return [ordered]@{ status = "adopt_project_owned"; processId = $ProcessId; owner = $existing }
    }
    $ownerDir = Get-OwnerDir $adoptKey
    $now = [datetime]::UtcNow.ToString("o")
    $owner = [ordered]@{
        lease = $Lease
        slot = $Slot
        mode = if ($found.batch) { "batch" } else { "editor" }
        projectPath = $found.projectPath
        projectKey = $adoptKey
        processId = $ProcessId
        acquiredAt = $now
        updatedAt = $now
    }
    $claim = Move-RecordDirIntoPlace $ownerDir "owner.json" $owner
    if (-not $claim.moved) { return [ordered]@{ status = "adopt_project_owned"; processId = $ProcessId; owner = Get-ProjectOwner $adoptKey } }
    return [ordered]@{ status = "adopted"; owner = [pscustomobject]$owner }
}

function Cancel-Request {
    $ticket = Find-Ticket $Lease
    if ($null -ne $ticket) { Remove-TicketFile $ticket.file }
    return [ordered]@{ status = "cancelled"; lease = $Lease }
}

function Release-Access {
    $owner = Find-OwnerByLease $Lease
    if ($null -eq $owner) {
        [void](Release-Boot)
        # A caller that gave up while still queued holds no owner but does hold a ticket; leaving it
        # stranded the whole project's FIFO behind a dead lease until TicketTtlSeconds expired.
        [void](Cancel-Request)
        return [ordered]@{ status = "released"; alreadyFree = $true }
    }

    # Close or kill only a coordinator-relevant Unity PID — never a bare/recycled one. Checked once,
    # above both branches: CloseMainWindow on a stranger's window is as wrong as killing it.
    if ($CloseEditor.IsPresent -and [int]$owner.processId -gt 0 -and (Test-UnityProcessLive ([int]$owner.processId))) {
        $editorPid = [int]$owner.processId
        $process = Get-Process -Id $editorPid -ErrorAction SilentlyContinue
        if ($null -ne $process) {
            $closed = $false
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                [void]$process.CloseMainWindow()
                $closed = Wait-ProcessExit $editorPid $EditorCloseWaitSeconds
            }
            if (-not $closed) {
                Stop-ProcessTree -ProcessId $editorPid
                if (-not (Wait-ProcessExit $editorPid $EditorCloseWaitSeconds)) {
                    return [ordered]@{ status = "editor_did_not_exit"; owner = $owner }
                }
            }
        }
    }

    # After a CloseEditor kill our PID reads dead, so a concurrent reader may win this prune; already gone is the goal.
    Remove-Item -LiteralPath (Get-OwnerDir ([string]$owner.projectKey)) -Recurse -Force -ErrorAction SilentlyContinue
    [void](Release-Boot)
    [void](Cancel-Request)
    return [ordered]@{ status = "released"; alreadyFree = $false }
}

# ---- Composite actions -----------------------------------------------------
function Start-TrackedEditor {
    $acquired = Acquire-Access
    if ($acquired.status -ne "acquired") { return $acquired }
    $boot = Acquire-Boot
    if ($boot.status -ne "boot_acquired") {
        [void](Release-Access)
        return $boot
    }
    try {
        $exe = if ([string]::IsNullOrWhiteSpace($UnityPath)) { Resolve-UnityEditorPath -ProjectPath $ResolvedProject } else { Resolve-FullPath $UnityPath }
        if (-not (Test-Path -LiteralPath $exe)) { throw "Unity executable not found: $exe" }
        $receiptPath = New-EditorProfileReceiptPath
        $previousProfile = [Environment]::GetEnvironmentVariable("ASTRONOMICAL_EDITOR_PROFILE", "Process")
        $previousReceipt = [Environment]::GetEnvironmentVariable("ASTRONOMICAL_EDITOR_PROFILE_RECEIPT", "Process")
        # The editor must open the project whose lease it holds, so caller args compose after -projectPath.
        $launchArgs = @("-projectPath", $ResolvedProject) + $EditorArgs
        try {
            [Environment]::SetEnvironmentVariable("ASTRONOMICAL_EDITOR_PROFILE", $EditorProfile, "Process")
            [Environment]::SetEnvironmentVariable("ASTRONOMICAL_EDITOR_PROFILE_RECEIPT", $receiptPath, "Process")
            $process = Start-Process -FilePath $exe -ArgumentList $launchArgs -PassThru
        }
        finally {
            [Environment]::SetEnvironmentVariable("ASTRONOMICAL_EDITOR_PROFILE", $previousProfile, "Process")
            [Environment]::SetEnvironmentVariable("ASTRONOMICAL_EDITOR_PROFILE_RECEIPT", $previousReceipt, "Process")
        }
        try {
            $profile = Test-EditorProfileReceipt (Wait-EditorProfileReceipt $receiptPath $process) $EditorProfile
        }
        finally {
            Remove-Item -LiteralPath $receiptPath -Force -ErrorAction SilentlyContinue
        }
        if (-not $profile.verified) {
            Stop-ProcessTree -ProcessId $process.Id
            [void](Release-Access)
            return [ordered]@{ status = "editor_profile_failed"; profile = $profile }
        }
        $script:ProcessId = $process.Id
        $attached = Attach-Process
        $attached.profile = $profile
        return $attached
    }
    catch {
        [void](Release-Access)
        throw
    }
}

function Run-TrackedBatch {
    $acquired = Acquire-Access
    if ($acquired.status -ne "acquired") { return $acquired }
    try {
        if ([string]::IsNullOrWhiteSpace($BatchScript)) { throw "RunBatch requires -BatchScript." }
        $boot = Acquire-Boot
        if ($boot.status -ne "boot_acquired") { return $boot }
        # A Unity child launched through Start-Process reports HasExited long before Unity is done,
        # so the child stays a blocking call and the lane is freed from beside it.
        $sidecar = Start-BootLaneSidecar
        try {
            # The child's own chatter is prose, and under -Json stdout is reserved for the one
            # result line, so it is forwarded to stderr rather than corrupting the machine channel.
            # 2>&1 ENVELOPE HAZARD: under EAP=Stop a native command's stderr arrives as an
            # ErrorRecord and throws, so the merge only ever happens with EAP relaxed.
            $previousEap = $ErrorActionPreference
            $ErrorActionPreference = "Continue"
            try {
                if ($Json.IsPresent) {
                    & powershell -NoProfile -ExecutionPolicy Bypass -File (Resolve-FullPath $BatchScript) @BatchArguments 2>&1 |
                        ForEach-Object { [Console]::Error.WriteLine([string]$_) }
                }
                else {
                    & powershell -NoProfile -ExecutionPolicy Bypass -File (Resolve-FullPath $BatchScript) @BatchArguments
                }
                $code = $LASTEXITCODE
            }
            finally { $ErrorActionPreference = $previousEap }
        }
        finally {
            if ($null -ne $sidecar) {
                Stop-Job $sidecar -ErrorAction SilentlyContinue
                Remove-Job $sidecar -Force -ErrorAction SilentlyContinue
            }
            [void](Release-Boot)
        }
        return [ordered]@{ status = "batch_complete"; exitCode = $code }
    }
    finally { [void](Release-Access) }
}

# ---- Result channel & dispatch ---------------------------------------------
function Require-Lease {
    if ([string]::IsNullOrWhiteSpace($Lease)) { throw "$Action requires -Lease." }
}

function Write-Result {
    param([object]$Result)
    if ($Json.IsPresent) { Write-Output ($Result | ConvertTo-Json -Depth 8 -Compress); return }
    if ($Action -eq "Status" -and [string]$Result["status"] -eq "") {
        $owners = @($Result.owners)
        if ($owners.Count -gt 0) {
            foreach ($owner in $owners) { Write-Host "Unity owner: $($owner.slot) $($owner.mode) lease=$($owner.lease) pid=$($owner.processId) project=$($owner.projectPath)" }
        }
        else { Write-Host "Unity projects: all free" }
        if ($null -ne $Result.legacyOwner) { Write-Host "LEGACY machine-wide owner (old script copy): $($Result.legacyOwner.slot) lease=$($Result.legacyOwner.lease)" }
        if ($null -ne $Result.boot) { Write-Host "Boot lane: held by lease=$($Result.boot.lease)" }
        elseif ($Result.bootWedged) { Write-Host "Boot lane: WEDGED - unowned boot dir could not be removed; find and kill whatever holds $BootRoot" }
        else { Write-Host "Boot lane: free" }
        if (@($Result.queue).Count -gt 0) { Write-Host "Queue: $((@($Result.queue) | ForEach-Object { "$($_.position):$($_.slot)" }) -join ', ')" }
        else { Write-Host "Queue: empty" }
        foreach ($blocker in @($Result.blockers)) { Write-Host "Blocker: $($blocker.kind) pid=$($blocker.processId) project=$($blocker.projectPath)" }
        return
    }
    Write-Host "$($Action): $([string]$Result.status)"
    foreach ($entry in $Result.GetEnumerator()) {
        if ($entry.Key -eq "status" -or $null -eq $entry.Value) { continue }
        $rendered = if ($entry.Value -is [string] -or $entry.Value -is [int] -or $entry.Value -is [bool]) { [string]$entry.Value }
                    else { $entry.Value | ConvertTo-Json -Depth 8 -Compress }
        Write-Host "  $($entry.Key): $rendered"
    }
}

$PrimaryRoot = Get-PrimaryRoot
$AccessRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Join-Path $PrimaryRoot ".worktree-pool/unity-access" } else { Resolve-FullPath $StateRoot }
$ProfileReceiptRoot = Join-Path $AccessRoot "profile-receipts"
$LegacyOwnerRoot = Join-Path $AccessRoot "owner"
$OwnersRoot = Join-Path $AccessRoot "owners"
$BootRoot = Join-Path $AccessRoot "boot"
$QueueRoot = Join-Path $AccessRoot "queue"
$StagingRoot = Join-Path $AccessRoot "staging"
New-Item -ItemType Directory -Force -Path $QueueRoot | Out-Null
New-Item -ItemType Directory -Force -Path $OwnersRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($ProjectPath) -and -not [string]::IsNullOrWhiteSpace($Slot)) {
    $ProjectPath = Join-Path (Get-WorktreePath $Slot) "src/Asteroids3D"
}
$ResolvedProject = Resolve-FullPath $ProjectPath
$ProjectKey = Get-ProjectKey $ResolvedProject

# Every failure leaves through the same door: one result on the machine channel, its text on stderr.
# A caller that gets no parseable line got no answer at all, which is worse than a named failure.
$result = try {
    switch ($Action) {
    "Status" { Get-StatusValue }
    "Contract" { Get-ContractValue }
    "Request" { Require-Lease; Request-Access }
    "Acquire" { Require-Lease; Acquire-Access }
    "Wait" { Require-Lease; if ($WaitSeconds -le 0) { $WaitSeconds = 60 }; Acquire-Access }
    "Attach" { Require-Lease; if ($ProcessId -le 0) { throw "Attach requires -ProcessId." }; Attach-Process }
    "AttachBatchChild" { Require-Lease; Attach-BatchChild }
    "Adopt" { Require-Lease; if ($ProcessId -le 0) { throw "Adopt requires -ProcessId." }; Adopt-Process }
    "Release" { Require-Lease; Release-Access }
    "Cancel" { Require-Lease; Cancel-Request }
    "BootAcquire" { Require-Lease; if ($WaitSeconds -le 0) { $WaitSeconds = 300 }; Acquire-Boot }
    "BootRelease" { Require-Lease; Release-Boot }
    "StartEditor" { Require-Lease; $Mode = "editor"; Start-TrackedEditor }
    "RunBatch" { Require-Lease; $Mode = "batch"; Run-TrackedBatch }
    }
}
catch {
    $message = [string]$_.Exception.Message
    [Console]::Error.WriteLine("unity_access $($Action): $message")
    $parts = $message -split '\|', 3
    if ($parts[0] -eq $RecordUnreadableTag) {
        [ordered]@{ status = "record_unreadable"; path = $parts[1]; error = $parts[2] }
    }
    else { [ordered]@{ status = "coordinator_error"; error = $message } }
}

Write-Result $result
$resultStatus = if ($result -is [System.Collections.IDictionary]) { [string]$result["status"] } else { [string]$result.status }
exit ($(if ($statusExitCodes.ContainsKey($resultStatus)) { $statusExitCodes[$resultStatus] } else { 0 }))
