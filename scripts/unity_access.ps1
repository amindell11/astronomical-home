param(
    [ValidateSet("Status", "Request", "Acquire", "Wait", "Attach", "Release", "Cancel", "BootAcquire", "BootRelease", "StartEditor", "RunBatch")]
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
    [int]$McpPort = 8081,
    [switch]$CloseEditor,
    [string]$BatchScript = "",
    [string[]]$BatchArguments = @(),
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ExitWaiting = 20
$ExitUnmanaged = 21
$ExitOwnership = 22
$ExitIncomplete = 23
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

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

function Test-TcpPort {
    param([int]$Port)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $connection = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
        if (-not $connection.AsyncWaitHandle.WaitOne(800)) { return $false }
        $client.EndConnect($connection)
        return $true
    }
    catch { return $false }
    finally { $client.Close() }
}

function Ensure-McpServer {
    if (Test-TcpPort $McpPort) { return }
    $uvx = Get-Command uvx.exe -ErrorAction SilentlyContinue
    if ($null -eq $uvx) { throw "uvx.exe is required to start the shared Unity MCP server." }
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $stdout = Join-Path $env:TEMP "unity-mcp-$stamp.out.log"
    $stderr = Join-Path $env:TEMP "unity-mcp-$stamp.err.log"
    $arguments = @(
        "--offline", "--from", "mcpforunityserver==10.0.0", "mcp-for-unity",
        "--transport", "http", "--http-url", "http://127.0.0.1:$McpPort", "--project-scoped-tools"
    )
    Start-Process -FilePath $uvx.Source -ArgumentList $arguments -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr | Out-Null
    $deadline = [datetime]::UtcNow.AddSeconds(20)
    while ([datetime]::UtcNow -lt $deadline) {
        if (Test-TcpPort $McpPort) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Unity MCP server did not bind port $McpPort. Inspect $stderr"
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

function Remove-StaleTickets {
    $now = [datetime]::UtcNow
    foreach ($file in @(Get-ChildItem -LiteralPath $QueueRoot -Filter "*.json" -File -ErrorAction SilentlyContinue)) {
        $ticket = Read-JsonFile $file.FullName
        $updated = if ($null -ne $ticket) { Get-DateValue $ticket.updatedAt } else { [datetime]::MinValue }
        if (($now - $updated).TotalSeconds -gt $TicketTtlSeconds) { Remove-Item -LiteralPath $file.FullName -Force }
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

function Test-OwnerStale {
    param([object]$Owner)
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
        Remove-Item -LiteralPath $ownerDir -Recurse -Force
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
        Remove-Item -LiteralPath $LegacyOwnerRoot -Recurse -Force
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
        Remove-Item -LiteralPath $BootRoot -Recurse -Force
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
    return [ordered]@{
        stateRoot = $AccessRoot
        owners = $owners
        legacyOwner = Get-LegacyOwner
        boot = Get-BootOwner
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
        $current.updatedAt = [datetime]::UtcNow.ToString("o")
        Write-JsonFile (Join-Path (Join-Path $OwnersRoot $ProjectKey) "owner.json") $current
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
        $status = if (@($blockers | Where-Object { $_.kind -eq "user_editor" }).Count -gt 0) { "blocked_user_editor" } else { "blocked_unmanaged_unity" }
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
        acquiredAt = $now
        updatedAt = $now
    }
    Write-JsonFile (Join-Path $ownerDir "owner.json") $owner
    $ownTicket = Find-Ticket $Lease
    if ($null -ne $ownTicket) { Remove-Item -LiteralPath $ownTicket.file -Force }
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

function Try-AcquireBoot {
    $owner = Find-OwnerByLease $Lease
    if ($null -eq $owner) { return [ordered]@{ status = "ownership_mismatch"; note = "BootAcquire requires holding a project owner lease." } }

    $boot = Get-BootOwner
    if ($null -ne $boot -and [string]$boot.lease -eq $Lease) {
        return [ordered]@{ status = "boot_acquired"; boot = $boot; renewed = $true }
    }
    if ($null -ne $boot) { return [ordered]@{ status = "boot_waiting"; boot = $boot } }

    $blockers = @(Get-Blockers ([string]$owner.projectPath) ([string]$owner.mode))
    if ($blockers.Count -gt 0) {
        $status = if (@($blockers | Where-Object { $_.kind -eq "user_editor" }).Count -gt 0) { "blocked_user_editor" } else { "blocked_unmanaged_unity" }
        return [ordered]@{ status = $status; blockers = $blockers }
    }

    try { New-Item -ItemType Directory -Path $BootRoot -ErrorAction Stop | Out-Null }
    catch { return [ordered]@{ status = "boot_waiting"; boot = Get-BootOwner } }

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
    Remove-Item -LiteralPath $BootRoot -Recurse -Force
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

function Cancel-Request {
    $ticket = Find-Ticket $Lease
    if ($null -ne $ticket) { Remove-Item -LiteralPath $ticket.file -Force }
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
            [void]$process.CloseMainWindow()
            $deadline = [datetime]::UtcNow.AddSeconds([Math]::Max(0, $EditorCloseWaitSeconds))
            while ($null -ne (Get-Process -Id ([int]$owner.processId) -ErrorAction SilentlyContinue) -and [datetime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500 }
            if ($null -ne (Get-Process -Id ([int]$owner.processId) -ErrorAction SilentlyContinue)) {
                return [ordered]@{ status = "editor_did_not_exit"; owner = $owner }
            }
        }
    }

    Remove-Item -LiteralPath (Join-Path $OwnersRoot ([string]$owner.projectKey)) -Recurse -Force
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
        Ensure-McpServer
        $exe = Resolve-FullPath $UnityPath
        if (-not (Test-Path -LiteralPath $exe)) { throw "Unity executable not found: $exe" }
        $previousEndpoint = $env:ASTRONOMICAL_UNITY_MCP_ENDPOINT
        $env:ASTRONOMICAL_UNITY_MCP_ENDPOINT = "http://127.0.0.1:$McpPort"
        try { $process = Start-Process -FilePath $exe -ArgumentList @("-projectPath", $ResolvedProject) -PassThru }
        finally {
            if ($null -eq $previousEndpoint) { Remove-Item Env:\ASTRONOMICAL_UNITY_MCP_ENDPOINT -ErrorAction SilentlyContinue }
            else { $env:ASTRONOMICAL_UNITY_MCP_ENDPOINT = $previousEndpoint }
        }
        # Interactive editors emit no coordinator-visible boot signal; the boot lane self-expires after BootTtlSeconds.
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
        # The child script is opaque to the coordinator, so the boot lane stays held for its whole run.
        try {
            & powershell -NoProfile -ExecutionPolicy Bypass -File (Resolve-FullPath $BatchScript) @BatchArguments
            $code = $LASTEXITCODE
        }
        finally { [void](Release-Boot) }
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
        if ($null -ne $Result.boot) { Write-Host "Boot lane: held by lease=$($Result.boot.lease)" } else { Write-Host "Boot lane: free" }
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
    blocked_user_editor = $ExitWaiting
}
exit ($(if ($statusExitCodes.ContainsKey($resultStatus)) { $statusExitCodes[$resultStatus] } else { 0 }))
