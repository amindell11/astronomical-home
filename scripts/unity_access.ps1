<#
.SYNOPSIS
    Coordinates access to this machine's shared Unity editors: a per-project owner lease plus a
    machine-wide boot lane, with a FIFO ticket queue per project.

.DESCRIPTION
    The published interface of this module is: -Action x its statuses x exit codes, the machine
    channel, and the three state-file schemas below. Nothing else is contract. Consumers must not
    parse this script's state files, output layout, or the process table - ask through an -Action.
    Law: doc/agents/script-contracts.md.
    This file is the CLI; the implementation is scripts/unity_access_lib.ps1, which adds no interface.

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
        20 waiting / boot_waiting / blocked_user_editor   21 blocked_unmanaged_unity / blocked_zombie_unity
        22 ownership_mismatch                             23 editor_did_not_exit
        24 adopt_* (all four refusals)                    25 boot_lane_wedged
        26 editor_profile_failed                          27 record_unreadable
        28 boot_refused_low_memory                        29 reap_refused_not_zombie / reap_did_not_exit
         1 coordinator_error (see FAILURE below)

      Status        (needs no -Lease) -> the state object, no "status" field, always exit 0.
                    Fields: stateRoot, owners[], legacyOwner, boot, bootWedged, queue[], blockers[].
                    A blocker carries kind, processId, projectPath, batch. Kinds: unmanaged_unity
                    (an untracked live Unity), user_editor (an untracked windowed editor on the
                    primary tree - the user's), zombie_unity (dead-but-running: a non-batch
                    editor with no main window, whose project has no Temp/UnityLockfile - Unity
                    deletes it on shutdown - and which is older than zombieBootWindowSeconds; a
                    hung Unity 6 teardown never exits on its own). All three signals are required
                    together; a zombie blocker also carries windowless, lockfileMissing and
                    ageSeconds. ageSeconds is process lifetime, not time in that state: the last
                    seconds of an ordinary shutdown look the same, so the verdict is a snapshot
                    and only Reap, which reconfirms it, acts on it.
                    Every owner carries normalizedProjectPath - THE key for "is this owner on my
                    project" (this script's own normalization; compare against it, never re-derive).
                    With -ProjectPath, Status also answers "who owns this path":
                      requestedProjectPath, requestedNormalizedProjectPath,
                      projectOwner        - the owner record for that path, or null,
                      projectProcesses[]  - live Unity processes on it, each with processId,
                                            projectPath, normalizedProjectPath and batch (false =
                                            an interactive editor, which makes a batch run
                                            infra_error and is where CLI commands route), plus
                                            privateGB / peakPrivateGB (that process, kernel-
                                            maintained for its whole lifetime, so one late read is
                                            its exact peak) and workersPrivateGB /
                                            workersPeakPrivateGB (its AssetImportWorker children,
                                            summed - they come and go, so they stay a separate
                                            number). This is where the editor boot demand constant
                                            is measured: doc/agents/environment.md.
      Contract      (needs no -Lease) -> status "contract" plus the constants a caller must match
                    exactly: bootCompletePattern, ticketTtlSeconds, ownerTtlSeconds,
                    bootTtlSeconds, bootDemandBatchGB, bootDemandEditorGB, bootMemoryMarginGB,
                    zombieBootWindowSeconds.
                    Hard-coding any of them keeps a copy that drifts.
      BootAdmission (needs no -Lease) -Mode -> boot_admitted | boot_not_admitted, both exit 0 (a
                    "no" from a query is not a failure; branch on status). Read-only memory
                    admission: may a boot of that mode start now? Fields: mode, commitHeadroomGB,
                    requiredHeadroomGB (= demand for the mode + margin), availablePhysicalGB.
                    Only commit headroom decides; available physical RAM is reported, never blocks.
                    This action ignores -AllowLowMemory.
      Request       -Lease [-Slot|-ProjectPath] [-Mode] -> queued.
      Acquire       -Lease [-Slot|-ProjectPath] [-Mode] [-WaitSeconds] ->
                    acquired | waiting | blocked_user_editor | blocked_unmanaged_unity |
                    blocked_zombie_unity (a zombie is among the blockers and no user editor is;
                    it never clears by waiting - Reap it).
      Wait          as Acquire, but -WaitSeconds defaults to 60.
      Attach        -Lease -ProcessId -> attached | ownership_mismatch.
      AttachBatchChild -Lease -> attached | batch_child_absent | ownership_mismatch.
      Adopt         -Lease -ProcessId -> adopted | adopt_no_process | adopt_already_tracked |
                    adopt_refused_user_editor | adopt_project_owned.
      Reap          (needs no -Lease) -ProcessId [-ReapConfirmSeconds] [-EditorCloseWaitSeconds] ->
                    reaped | reap_refused_not_zombie | reap_did_not_exit.
                    Refuses any pid Status does not classify zombie_unity, tracked or not - a live
                    editor is closed through Release -CloseEditor or by its owner. Otherwise waits
                    -ReapConfirmSeconds (default 15) and classifies again: an editor that was
                    merely finishing an ordinary shutdown has exited by then and returns reaped
                    with exitedUnaided true; one still windowless, lockfile-less and running is
                    killed with its tree (UnityCrashHandler64, UnityPackageManager and
                    AssetImportWorker children) and waited for. A pid that stops classifying
                    zombie_unity on the second look is refused. Operator-invoked only: Acquire
                    reports a zombie, it never reaps one.
      Release       -Lease [-CloseEditor [-EditorCloseWaitSeconds]] -> released | editor_did_not_exit.
                    Also frees this lease's boot lane and cancels its queued ticket.
      Cancel        -Lease -> cancelled.
      BootAcquire   -Lease [-WaitSeconds] [-AllowLowMemory] -> boot_acquired | boot_waiting |
                    boot_lane_wedged | boot_refused_low_memory | ownership_mismatch | blocked_*.
                    -WaitSeconds defaults to 300. Memory admission is enforced here, on the owner
                    record's mode, once the lane is free and unblocked; a renew is not re-checked.
                    boot_refused_low_memory returns immediately - memory is not a queue.
                    -AllowLowMemory admits anyway: the boot record and the boot_acquired result
                    carry memoryOverride plus the readings, and one line goes to stderr. Pass it
                    only after the user approved that specific boot.
      BootRelease   -Lease -> boot_released | ownership_mismatch | boot_lane_wedged.
      StartEditor   -Lease -Slot|-ProjectPath [-EditorArgs] [-EditorProfile] [-UnityPath]
                    [-AllowLowMemory] ->
                    attached (carrying a .profile receipt) | editor_profile_failed |
                    any Acquire or BootAcquire status. -UnityPath overrides the
                    editor resolved from the project's own ProjectVersion.txt
                    (scripts/lib/unity_editor.ps1).
      RunBatch      -Lease -BatchScript [-BatchArguments] [-BatchLogPath] [-BatchBootSeconds]
                    [-AllowLowMemory] ->
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
        acquiredAt, updatedAt, and editorProfile on a StartEditor lease. Read it back through
        Status.owners[].
      <StateRoot>/queue/<timestamp>-<guid>.json - lease, slot, mode, projectPath, requestedAt,
        updatedAt. Read it back through Status.queue[] (position is 1-based, per project).
      <StateRoot>/boot/boot.json - lease, projectPath, processId, acquiredAt, plus memoryOverride
        and the three readings when -AllowLowMemory admitted a boot that would have been refused.
        Read it back through Status.boot; an unowned dir that cannot be removed surfaces as
        Status.bootWedged.
      <StateRoot>/owner/owner.json is the retired single-owner record, honored until it clears.

.NOTES
    Routed leaselessness (fork 4, RULED accepted): a -Routed unity_test_agent run attaches to an
    editor someone else already leased and takes no lease of its own. It verifies through Status
    that the project has a live editor owner, then runs beside it. Accepted as dev-loop behavior;
    a read-style co-lease is not planned.

    -ProcessSnapshotPath replaces live process enumeration with a JSON file (tests only); its
    records carry the same fields the live query reads - processId, parentProcessId, commandLine,
    privateKb, peakPrivateKb, mainWindowHandle (0 = no window), startTime (ISO 8601; absent =
    age unknown, which never classifies a zombie).
    -MemorySnapshotPath replaces the live memory reading with a JSON file carrying
    FreeVirtualMemory and FreePhysicalMemory in KB, as Win32_OperatingSystem reports them
    (tests only).
    -PrimaryRoot overrides the git-derived primary worktree - the thing that makes "the user's main
    editor" and slot-to-project resolution machine-dependent; tests inject it to stay hermetic.
#>
param(
    [ValidateSet("Status", "Contract", "BootAdmission", "Request", "Acquire", "Wait", "Attach", "AttachBatchChild", "Adopt", "Reap", "Release", "Cancel", "BootAcquire", "BootRelease", "StartEditor", "RunBatch")]
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
    [int]$ReapConfirmSeconds = 15,
    [string]$StateRoot = "",
    [string]$PrimaryRoot = "",
    [string]$ProcessSnapshotPath = "",
    [string]$MemorySnapshotPath = "",
    [switch]$AllowLowMemory,
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

. (Join-Path $PSScriptRoot "unity_access_lib.ps1")

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
        elseif ($Result.bootWedged) { Write-Host "Boot lane: WEDGED - unowned boot dir could not be removed; find and kill whatever holds $(Join-Path $Result.stateRoot "boot")" }
        else { Write-Host "Boot lane: free" }
        if (@($Result.queue).Count -gt 0) { Write-Host "Queue: $((@($Result.queue) | ForEach-Object { "$($_.position):$($_.slot)" }) -join ', ')" }
        else { Write-Host "Queue: empty" }
        foreach ($blocker in @($Result.blockers)) {
            $line = "Blocker: $($blocker.kind) pid=$($blocker.processId) project=$($blocker.projectPath)"
            if ($blocker.kind -eq "zombie_unity") { $line += " (windowless, lockfile gone, age=$($blocker.ageSeconds)s; reap: unity_access.ps1 -Action Reap -ProcessId $($blocker.processId))" }
            Write-Host $line
        }
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

$result = Invoke-UnityAccessAction @PSBoundParameters
Write-Result $result
exit (Get-UnityAccessExitCode $result)
