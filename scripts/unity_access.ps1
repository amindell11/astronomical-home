param(
    [ValidateSet("Status", "Request", "Acquire", "Wait", "Attach", "AttachBatchChild", "Adopt", "Release", "Cancel", "BootAcquire", "BootRelease", "StartEditor", "RunBatch")]
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
    [string]$ProcessSnapshotPath = "",
    [string]$UnityPath = "D:\Programs\Unity\Editor\6000.1.8f1\Editor\Unity.exe",
    [switch]$CloseEditor,
    [string[]]$EditorArgs = @(),
    [string]$BatchScript = "",
    [string[]]$BatchArguments = @(),
    [string]$BatchLogPath = "",
    [int]$BatchBootSeconds = 0,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ExitWaiting = 20
$ExitUnmanaged = 21
$ExitOwnership = 22
$ExitIncomplete = 23
$ExitAdoptRefused = 24
$ExitBootWedged = 25
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
# Boot ends once licensing + global package-cache work gives way to per-project Library work
# (postmortem D6). Sibling copy in scripts/unity_test_agent.ps1, which drives the lane itself.
$BootCompletePattern = 'Application\.AssetDatabase Initial Refresh Start'

function Resolve-FullPath {
    param([string]$Path, [string]$Base = (Get-Location).Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path $Base $Path))
}

function Get-PrimaryRoot {
    $repo = Resolve-FullPath (Join-Path $PSScriptRoot "..")
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

function Read-JsonFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { return $null }
}

function Write-JsonFile {
    param([string]$Path, [object]$Value)
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $temp = Join-Path $parent (([System.IO.Path]::GetFileName($Path)) + "." + [guid]::NewGuid().ToString("N") + ".tmp")
    [System.IO.File]::WriteAllText($temp, ($Value | ConvertTo-Json -Depth 8), $Utf8NoBom)
    Move-Item -LiteralPath $temp -Destination $Path -Force
}

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
    if (-not [string]::IsNullOrWhiteSpace($ProcessSnapshotPath)) {
        $snapshot = Read-JsonFile (Resolve-FullPath $ProcessSnapshotPath)
        if ($null -eq $snapshot) { return @() }
        return @($snapshot)
    }
    try {
        return @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue | ForEach-Object {
            [pscustomobject]@{ processId = [int]$_.ProcessId; commandLine = [string]$_.CommandLine }
        })
    }
    catch { return @() }
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

function Get-DateValue {
    param([object]$Value)
    $parsed = [datetime]::MinValue
    if ($null -ne $Value) { [void][datetime]::TryParse([string]$Value, [ref]$parsed) }
    return $parsed.ToUniversalTime()
}

function Remove-TicketFile {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { throw "Ticket file path is required." }
    [System.IO.File]::Delete($Path)
}

function Remove-StaleTickets {
    $now = [datetime]::UtcNow
    foreach ($file in @(Get-ChildItem -LiteralPath $QueueRoot -Filter "*.json" -File -ErrorAction SilentlyContinue)) {
        $ticket = Read-JsonFile $file.FullName
        $updated = if ($null -ne $ticket) { Get-DateValue $ticket.updatedAt } else { [datetime]::MinValue }
        if (($now - $updated).TotalSeconds -gt $TicketTtlSeconds) { Remove-TicketFile $file.FullName }
    }
}

function Get-Tickets {
    Remove-StaleTickets
    $items = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $QueueRoot -Filter "*.json" -File -ErrorAction SilentlyContinue | Sort-Object Name)) {
        $ticket = Read-JsonFile $file.FullName
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
        $existing.data.updatedAt = $now
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

function Get-MemberValue {
    param([object]$Object, [string]$Name)
    if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name]) { return $null }
    return $Object.PSObject.Properties[$Name].Value
}

function Test-OwnerStale {
    param([object]$Owner)
    # A lease whose holding coordinator still runs is busy, whatever its opaque child is doing —
    # checked first so a child that exits just before the release does not open a reap window.
    $holder = [int](Get-MemberValue $Owner "holderProcessId")
    if ($holder -gt 0 -and $null -ne (Get-Process -Id $holder -ErrorAction SilentlyContinue)) { return $false }
    if ([int]$Owner.processId -gt 0) {
        return @(Get-RelevantUnityProcesses | Where-Object { $_.processId -eq [int]$Owner.processId }).Count -eq 0
    }
    return ([datetime]::UtcNow - (Get-DateValue $Owner.updatedAt)).TotalSeconds -gt $OwnerTtlSeconds
}

function Get-ProjectOwner {
    param([string]$RequestedKey)
    $ownerDir = Join-Path $OwnersRoot $RequestedKey
    $owner = Read-JsonFile (Join-Path $ownerDir "owner.json")
    if ($null -eq $owner) {
        if (Test-Path -LiteralPath $ownerDir) { Remove-Item -LiteralPath $ownerDir -Recurse -Force -ErrorAction SilentlyContinue }
        return $null
    }
    if (Test-OwnerStale $owner) {
        # Concurrent coordinators race this prune; a lost race left the dir already gone, which is the goal.
        Remove-Item -LiteralPath $ownerDir -Recurse -Force -ErrorAction SilentlyContinue
        return $null
    }
    return $owner
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
    $owner = Read-JsonFile (Join-Path $LegacyOwnerRoot "owner.json")
    if ($null -eq $owner) {
        if (Test-Path -LiteralPath $LegacyOwnerRoot) { Remove-Item -LiteralPath $LegacyOwnerRoot -Recurse -Force -ErrorAction SilentlyContinue }
        return $null
    }
    if (Test-OwnerStale $owner) {
        # Concurrent coordinators race this prune; a lost race left the dir already gone, which is the goal.
        Remove-Item -LiteralPath $LegacyOwnerRoot -Recurse -Force -ErrorAction SilentlyContinue
        return $null
    }
    return $owner
}

function Get-BootOwner {
    $boot = Read-JsonFile (Join-Path $BootRoot "boot.json")
    if ($null -eq $boot) {
        if (Test-Path -LiteralPath $BootRoot) { Remove-Item -LiteralPath $BootRoot -Recurse -Force -ErrorAction SilentlyContinue }
        return $null
    }
    $stale = ([datetime]::UtcNow - (Get-DateValue $boot.acquiredAt)).TotalSeconds -gt $BootTtlSeconds
    if (-not $stale -and [int]$boot.processId -gt 0) {
        $stale = @(Get-RelevantUnityProcesses | Where-Object { $_.processId -eq [int]$boot.processId }).Count -eq 0
    }
    if ($stale) {
        # A concurrent reader may reap the same stale record first; an undeletable leftover
        # surfaces as boot_lane_wedged at the next acquire rather than killing this call.
        Remove-Item -LiteralPath $BootRoot -Recurse -Force -ErrorAction SilentlyContinue
        return $null
    }
    return $boot
}

function Get-TrackedPids {
    $pids = @(Get-AllOwners | ForEach-Object { [int]$_.processId } | Where-Object { $_ -gt 0 })
    $legacy = Get-LegacyOwner
    if ($null -ne $legacy -and [int]$legacy.processId -gt 0) { $pids += [int]$legacy.processId }
    return $pids
}

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

function Get-StatusValue {
    $owners = @(Get-AllOwners)
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
    return [ordered]@{
        stateRoot = $AccessRoot
        owners = $owners
        legacyOwner = Get-LegacyOwner
        boot = $boot
        # Get-BootOwner just tried to reap any unowned boot dir; one that survives is wedged.
        bootWedged = ($null -eq $boot -and (Test-Path -LiteralPath $BootRoot))
        queue = $queue
        blockers = @(Get-Blockers)
    }
}

function Get-QueuePosition {
    param([string]$RequestedLease, [string]$RequestedKey)
    $projectTickets = @(Get-Tickets | Where-Object { (Get-ProjectKey ([string]$_.data.projectPath)) -eq $RequestedKey })
    return 1 + [array]::IndexOf(@($projectTickets | ForEach-Object { [string]$_.data.lease }), $RequestedLease)
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

    $ownerDir = Join-Path $OwnersRoot $ProjectKey
    try { New-Item -ItemType Directory -Path $ownerDir -ErrorAction Stop | Out-Null }
    catch { return [ordered]@{ status = "waiting"; position = $position; owner = Get-ProjectOwner $ProjectKey } }

    $now = [datetime]::UtcNow.ToString("o")
    $owner = [ordered]@{
        lease = $Lease
        slot = $Slot
        mode = $Mode
        projectPath = $ResolvedProject
        projectKey = $ProjectKey
        processId = 0
        holderProcessId = $PID
        acquiredAt = $now
        updatedAt = $now
    }
    Write-JsonFile (Join-Path $ownerDir "owner.json") $owner
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
    $Owner.updatedAt = [datetime]::UtcNow.ToString("o")
    Write-JsonFile (Join-Path (Join-Path $OwnersRoot ([string]$Owner.projectKey)) "owner.json") $Owner
}

# Claims the child's Unity, then frees the machine-wide lane once the caller's declared log shows
# startup is past the contention window. Runs beside the blocking child and exits on its own.
# Both halves matter: an unclaimed child blocks every project no matter who holds the lane.
function Start-BootLaneSidecar {
    $settings = @{
        coordinator = $PSCommandPath
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
        $common = @("-Lease", $s.lease, "-StateRoot", $s.stateRoot, "-Json")
        if (-not [string]::IsNullOrWhiteSpace($s.snapshot)) { $common += @("-ProcessSnapshotPath", $s.snapshot) }
        $attached = $false
        while ($true) {
            if (-not $attached) {
                $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $s.coordinator `
                    -Action AttachBatchChild @common 2>&1
                $attached = [bool]([string]$out -match '"status":"attached"')
            }
            $bootDone = [datetime]::UtcNow -ge [datetime]::Parse($s.deadline).ToUniversalTime()
            if (-not $bootDone -and -not [string]::IsNullOrWhiteSpace($s.logPath) -and (Test-Path -LiteralPath $s.logPath)) {
                $bootDone = [bool](Select-String -LiteralPath $s.logPath -Pattern $s.pattern -Quiet -ErrorAction SilentlyContinue)
            }
            if ($bootDone) { break }
            Start-Sleep -Seconds $s.pollSeconds
        }
        & powershell -NoProfile -ExecutionPolicy Bypass -File $s.coordinator -Action BootRelease @common | Out-Null
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

    try { New-Item -ItemType Directory -Path $BootRoot -ErrorAction Stop | Out-Null }
    catch {
        $reason = $_.Exception.Message
        $boot = Get-BootOwner
        if ($null -ne $boot) { return [ordered]@{ status = "boot_waiting"; boot = $boot } }
        # The dir is unowned yet undeletable (stray handle/CWD holds it); waiting would never end.
        return [ordered]@{ status = "boot_lane_wedged"; error = $reason; bootRoot = $BootRoot }
    }

    $record = [ordered]@{
        lease = $Lease
        projectPath = [string]$owner.projectPath
        processId = 0
        acquiredAt = [datetime]::UtcNow.ToString("o")
    }
    Write-JsonFile (Join-Path $BootRoot "boot.json") $record
    return [ordered]@{ status = "boot_acquired"; boot = [pscustomobject]$record; renewed = $false }
}

function Acquire-Boot {
    $deadline = [datetime]::UtcNow.AddSeconds([Math]::Max(0, $WaitSeconds))
    do {
        # boot_lane_wedged retries too: the empty-dir window between a winner's mkdir and its
        # boot.json write reads as wedged for one poll; only a persistent wedge reaches the caller.
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

function Attach-Process {
    $owner = Find-OwnerByLease $Lease
    if ($null -eq $owner) { return [ordered]@{ status = "ownership_mismatch" } }
    $owner.processId = $ProcessId
    $owner.updatedAt = [datetime]::UtcNow.ToString("o")
    Write-JsonFile (Join-Path (Join-Path $OwnersRoot ([string]$owner.projectKey)) "owner.json") $owner
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
    $ownerDir = Join-Path $OwnersRoot $adoptKey
    New-Item -ItemType Directory -Force -Path $ownerDir | Out-Null
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
    Write-JsonFile (Join-Path $ownerDir "owner.json") $owner
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
        return [ordered]@{ status = "released"; alreadyFree = $true }
    }

    if ($CloseEditor.IsPresent -and [int]$owner.processId -gt 0) {
        $process = Get-Process -Id ([int]$owner.processId) -ErrorAction SilentlyContinue
        if ($null -ne $process) {
            $closed = $false
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                [void]$process.CloseMainWindow()
                $deadline = [datetime]::UtcNow.AddSeconds([Math]::Max(0, $EditorCloseWaitSeconds))
                while ($null -ne (Get-Process -Id ([int]$owner.processId) -ErrorAction SilentlyContinue) -and [datetime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500 }
                $closed = $null -eq (Get-Process -Id ([int]$owner.processId) -ErrorAction SilentlyContinue)
            }
            # Kill only a still-live, coordinator-relevant Unity PID — never a bare/recycled one.
            if (-not $closed) {
                if (@(Get-RelevantUnityProcesses | Where-Object { $_.processId -eq [int]$owner.processId }).Count -gt 0) {
                    Stop-Process -Id ([int]$owner.processId) -Force -ErrorAction SilentlyContinue
                    $deadline = [datetime]::UtcNow.AddSeconds([Math]::Max(0, $EditorCloseWaitSeconds))
                    while ($null -ne (Get-Process -Id ([int]$owner.processId) -ErrorAction SilentlyContinue) -and [datetime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500 }
                }
                if ($null -ne (Get-Process -Id ([int]$owner.processId) -ErrorAction SilentlyContinue)) {
                    return [ordered]@{ status = "editor_did_not_exit"; owner = $owner }
                }
            }
        }
    }

    # After a CloseEditor kill our PID reads dead, so a concurrent reader may win this prune; already gone is the goal.
    Remove-Item -LiteralPath (Join-Path $OwnersRoot ([string]$owner.projectKey)) -Recurse -Force -ErrorAction SilentlyContinue
    [void](Release-Boot)
    [void](Cancel-Request)
    return [ordered]@{ status = "released"; alreadyFree = $false }
}

function Start-TrackedEditor {
    $acquired = Acquire-Access
    if ($acquired.status -ne "acquired") { return $acquired }
    $boot = Acquire-Boot
    if ($boot.status -ne "boot_acquired") {
        [void](Release-Access)
        return $boot
    }
    try {
        $exe = Resolve-FullPath $UnityPath
        if (-not (Test-Path -LiteralPath $exe)) { throw "Unity executable not found: $exe" }
        # The editor must open the project whose lease it holds, so caller args compose after -projectPath.
        $launchArgs = @("-projectPath", $ResolvedProject) + $EditorArgs
        $process = Start-Process -FilePath $exe -ArgumentList $launchArgs -PassThru
        # Editors emit no coordinator-visible boot signal; the boot lane self-expires after BootTtlSeconds.
        $script:ProcessId = $process.Id
        return Attach-Process
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
            & powershell -NoProfile -ExecutionPolicy Bypass -File (Resolve-FullPath $BatchScript) @BatchArguments
            $code = $LASTEXITCODE
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

function Require-Lease {
    if ([string]::IsNullOrWhiteSpace($Lease)) { throw "$Action requires -Lease." }
}

function Write-Result {
    param([object]$Result)
    if ($Json.IsPresent) { Write-Output ($Result | ConvertTo-Json -Depth 8 -Compress); return }
    if ($Action -eq "Status") {
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
    Write-Host ($Result | ConvertTo-Json -Depth 8)
}

$PrimaryRoot = Get-PrimaryRoot
$AccessRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Join-Path $PrimaryRoot ".worktree-pool/unity-access" } else { Resolve-FullPath $StateRoot }
$LegacyOwnerRoot = Join-Path $AccessRoot "owner"
$OwnersRoot = Join-Path $AccessRoot "owners"
$BootRoot = Join-Path $AccessRoot "boot"
$QueueRoot = Join-Path $AccessRoot "queue"
New-Item -ItemType Directory -Force -Path $QueueRoot | Out-Null
New-Item -ItemType Directory -Force -Path $OwnersRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($ProjectPath) -and -not [string]::IsNullOrWhiteSpace($Slot)) {
    $ProjectPath = Join-Path (Get-WorktreePath $Slot) "src/Asteroids3D"
}
$ResolvedProject = Resolve-FullPath $ProjectPath
$ProjectKey = Get-ProjectKey $ResolvedProject

$result = switch ($Action) {
    "Status" { Get-StatusValue }
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

Write-Result $result
$resultStatus = if ($Action -eq "Status") { "" } else { [string]$result.status }
$statusExitCodes = @{
    ownership_mismatch = $ExitOwnership
    editor_did_not_exit = $ExitIncomplete
    blocked_unmanaged_unity = $ExitUnmanaged
    waiting = $ExitWaiting
    boot_waiting = $ExitWaiting
    boot_lane_wedged = $ExitBootWedged
    blocked_user_editor = $ExitWaiting
    adopt_no_process = $ExitAdoptRefused
    adopt_already_tracked = $ExitAdoptRefused
    adopt_refused_user_editor = $ExitAdoptRefused
    adopt_project_owned = $ExitAdoptRefused
}
exit ($(if ($statusExitCodes.ContainsKey($resultStatus)) { $statusExitCodes[$resultStatus] } else { 0 }))
