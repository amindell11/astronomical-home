Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Coordinator = Join-Path $PSScriptRoot "..\unity_access.ps1"
$Root = Join-Path $env:TEMP ("unity-access-tests-" + [guid]::NewGuid().ToString("N"))
$State = Join-Path $Root "state"
$Snapshot = Join-Path $Root "processes.json"
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$Assertions = 0

function Write-Snapshot {
    param([object[]]$Processes)
    New-Item -ItemType Directory -Force -Path $Root | Out-Null
    [System.IO.File]::WriteAllText($Snapshot, ($Processes | ConvertTo-Json -Depth 5), $Utf8NoBom)
}

function Invoke-Coordinator {
    param(
        [string]$Action,
        [string]$Lease = "",
        [string]$Slot = "agent-1",
        [string]$Mode = "batch",
        [string]$ProjectPath = "",
        [int]$WaitSeconds = 0,
        [int]$TicketTtlSeconds = 900,
        [int]$BootTtlSeconds = 180,
        [int]$OwnerTtlSeconds = 300
    )
    $arguments = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $Coordinator,
        "-Action", $Action,
        "-StateRoot", $State,
        "-ProcessSnapshotPath", $Snapshot,
        "-TicketTtlSeconds", $TicketTtlSeconds,
        "-BootTtlSeconds", $BootTtlSeconds,
        "-OwnerTtlSeconds", $OwnerTtlSeconds,
        "-Json"
    )
    if (-not [string]::IsNullOrWhiteSpace($Lease)) { $arguments += @("-Lease", $Lease, "-Slot", $Slot, "-Mode", $Mode) }
    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) { $arguments += @("-ProjectPath", $ProjectPath) }
    if ($WaitSeconds -gt 0) { $arguments += @("-WaitSeconds", $WaitSeconds) }
    $output = @(& powershell @arguments 2>&1)
    $code = $LASTEXITCODE
    $jsonLine = @($output | Where-Object { [string]$_ -match '^\s*[\{\[]' } | Select-Object -Last 1)
    if ($jsonLine.Count -eq 0) { throw "No JSON from coordinator: $($output -join [Environment]::NewLine)" }
    return [pscustomobject]@{ code = $code; value = ([string]$jsonLine[0] | ConvertFrom-Json); output = $output }
}

function Assert-Equal {
    param([object]$Actual, [object]$Expected, [string]$Name)
    $script:Assertions++
    if ([string]$Actual -ne [string]$Expected) { throw "$Name expected '$Expected' but got '$Actual'." }
}

function Assert-True {
    param([bool]$Value, [string]$Name)
    $script:Assertions++
    if (-not $Value) { throw "$Name expected true." }
}

function Get-OwnerByLease {
    param([object]$Status, [string]$Lease)
    $matches = @($Status.value.owners | Where-Object { [string]$_.lease -eq $Lease })
    if ($matches.Count -eq 0) { return $null }
    return $matches[0]
}

try {
    Write-Snapshot @()

    $projA = Join-Path $Root "projA\src\Asteroids3D"
    $projB = Join-Path $Root "projB\src\Asteroids3D"

    # Leases with no -ProjectPath share the "unknown" project bucket, so they serialize FIFO.
    $first = Invoke-Coordinator -Action Acquire -Lease first
    Assert-Equal $first.code 0 "first acquire exit"
    Assert-Equal $first.value.status "acquired" "first acquire status"

    $second = Invoke-Coordinator -Action Acquire -Lease second
    Assert-Equal $second.code 20 "second acquire exit"
    Assert-Equal $second.value.status "waiting" "second acquire status"
    Assert-Equal $second.value.position 1 "second queue position"

    $released = Invoke-Coordinator -Action Release -Lease first
    Assert-Equal $released.value.status "released" "first release"
    $secondAcquired = Invoke-Coordinator -Action Acquire -Lease second
    Assert-Equal $secondAcquired.value.status "acquired" "second FIFO acquire"
    [void](Invoke-Coordinator -Action Release -Lease second)

    $waited = Invoke-Coordinator -Action Wait -Lease waited
    Assert-Equal $waited.value.status "acquired" "wait acquires free project"
    [void](Invoke-Coordinator -Action Release -Lease waited)

    [void](Invoke-Coordinator -Action Request -Lease alpha)
    [void](Invoke-Coordinator -Action Request -Lease beta)
    $beta = Invoke-Coordinator -Action Acquire -Lease beta
    Assert-Equal $beta.value.position 2 "FIFO preserves request order"
    $alpha = Invoke-Coordinator -Action Acquire -Lease alpha
    Assert-Equal $alpha.value.status "acquired" "queue head acquires"
    [void](Invoke-Coordinator -Action Release -Lease alpha)
    [void](Invoke-Coordinator -Action Cancel -Lease beta)

    $ownA = Invoke-Coordinator -Action Acquire -Lease own-a -ProjectPath $projA
    Assert-Equal $ownA.value.status "acquired" "project A acquire"
    $ownB = Invoke-Coordinator -Action Acquire -Lease own-b -ProjectPath $projB
    Assert-Equal $ownB.code 0 "project B concurrent acquire exit"
    Assert-Equal $ownB.value.status "acquired" "project B acquires while A is owned"
    $ownA2 = Invoke-Coordinator -Action Acquire -Lease own-a2 -ProjectPath $projA
    Assert-Equal $ownA2.code 20 "same-project second acquire exit"
    Assert-Equal $ownA2.value.status "waiting" "same-project second acquire waits"
    [void](Invoke-Coordinator -Action Cancel -Lease own-a2)
    $statusBoth = Invoke-Coordinator -Action Status
    Assert-Equal @($statusBoth.value.owners).Count 2 "two concurrent project owners visible"

    $bootA = Invoke-Coordinator -Action BootAcquire -Lease own-a -WaitSeconds 1
    Assert-Equal $bootA.code 0 "boot acquire exit"
    Assert-Equal $bootA.value.status "boot_acquired" "boot acquire status"
    $bootB = Invoke-Coordinator -Action BootAcquire -Lease own-b -WaitSeconds 1
    Assert-Equal $bootB.code 20 "boot contention exit"
    Assert-Equal $bootB.value.status "boot_waiting" "boot lane is machine-wide exclusive"
    $bootARelease = Invoke-Coordinator -Action BootRelease -Lease own-a
    Assert-Equal $bootARelease.value.status "boot_released" "boot release status"
    $bootB2 = Invoke-Coordinator -Action BootAcquire -Lease own-b -WaitSeconds 1
    Assert-Equal $bootB2.value.status "boot_acquired" "boot lane handoff"

    $ghostBoot = Invoke-Coordinator -Action BootAcquire -Lease ghost -WaitSeconds 1
    Assert-Equal $ghostBoot.code 22 "ownerless boot acquire exit"
    Assert-Equal $ghostBoot.value.status "ownership_mismatch" "boot lane requires a project owner"

    $releaseB = Invoke-Coordinator -Action Release -Lease own-b
    Assert-Equal $releaseB.value.status "released" "release B"
    $afterReleaseB = Invoke-Coordinator -Action Status
    Assert-True ($null -eq $afterReleaseB.value.boot) "release frees a held boot lane"
    [void](Invoke-Coordinator -Action Release -Lease own-a)

    function Backdate-Owner {
        param([string]$Lease)
        $ownerFile = @(Get-ChildItem (Join-Path $State "owners") -Recurse -Filter "owner.json" | Where-Object { (Get-Content $_.FullName -Raw | ConvertFrom-Json).lease -eq $Lease })[0]
        $ownerJson = Get-Content $ownerFile.FullName -Raw | ConvertFrom-Json
        $ownerJson.updatedAt = [datetime]::UtcNow.AddMinutes(-2).ToString("o")
        [System.IO.File]::WriteAllText($ownerFile.FullName, ($ownerJson | ConvertTo-Json), $Utf8NoBom)
    }
    [void](Invoke-Coordinator -Action Acquire -Lease hb-holder -ProjectPath $projA)
    [void](Invoke-Coordinator -Action BootAcquire -Lease hb-holder -WaitSeconds 1)
    [void](Invoke-Coordinator -Action Acquire -Lease hb-waiter -ProjectPath $projB)
    Backdate-Owner "hb-waiter"
    $hbWait = Invoke-Coordinator -Action BootAcquire -Lease hb-waiter -WaitSeconds 1
    Assert-Equal $hbWait.value.status "boot_waiting" "heartbeat waiter still queued"
    $hbStatus = Invoke-Coordinator -Action Status
    $hbOwner = Get-OwnerByLease $hbStatus "hb-waiter"
    $hbAge = ([datetime]::UtcNow - [datetime]::Parse([string]$hbOwner.updatedAt).ToUniversalTime()).TotalSeconds
    Assert-True ($hbAge -lt 60) "boot wait renews owner updatedAt"
    Backdate-Owner "hb-holder"
    $hbRenew = Invoke-Coordinator -Action BootAcquire -Lease hb-holder -WaitSeconds 1
    Assert-Equal $hbRenew.value.status "boot_acquired" "heartbeat holder renews on re-acquire"
    $hbStatus2 = Invoke-Coordinator -Action Status
    $hbOwner2 = Get-OwnerByLease $hbStatus2 "hb-holder"
    $hbAge2 = ([datetime]::UtcNow - [datetime]::Parse([string]$hbOwner2.updatedAt).ToUniversalTime()).TotalSeconds
    Assert-True ($hbAge2 -lt 60) "boot re-acquire renews owner updatedAt"
    [void](Invoke-Coordinator -Action Release -Lease hb-holder)
    [void](Invoke-Coordinator -Action Release -Lease hb-waiter)

    $staleOwn = Invoke-Coordinator -Action Acquire -Lease stale-boot -ProjectPath $projA
    Assert-Equal $staleOwn.value.status "acquired" "stale-boot owner acquire"
    [void](Invoke-Coordinator -Action BootAcquire -Lease stale-boot -WaitSeconds 1)
    $bootFile = Join-Path $State "boot\boot.json"
    $bootJson = Get-Content $bootFile -Raw | ConvertFrom-Json
    $bootJson.acquiredAt = [datetime]::UtcNow.AddMinutes(-10).ToString("o")
    [System.IO.File]::WriteAllText($bootFile, ($bootJson | ConvertTo-Json), $Utf8NoBom)
    $reclaimOwn = Invoke-Coordinator -Action Acquire -Lease reclaim -ProjectPath $projB
    Assert-Equal $reclaimOwn.value.status "acquired" "reclaim owner acquire"
    $reclaimBoot = Invoke-Coordinator -Action BootAcquire -Lease reclaim -WaitSeconds 1 -BootTtlSeconds 60
    Assert-Equal $reclaimBoot.value.status "boot_acquired" "stale boot hold reclaimed"
    [void](Invoke-Coordinator -Action Release -Lease reclaim)
    [void](Invoke-Coordinator -Action Release -Lease stale-boot)

    $editorA = Invoke-Coordinator -Action Acquire -Lease editor-a -Mode editor -ProjectPath $projA
    Assert-Equal $editorA.value.status "acquired" "tracked editor acquire"
    $batchB = Invoke-Coordinator -Action Acquire -Lease batch-b -ProjectPath $projB
    Assert-Equal $batchB.code 0 "batch beside tracked editor exit"
    Assert-Equal $batchB.value.status "acquired" "tracked editor does not block cross-project batch"
    $batchA = Invoke-Coordinator -Action Acquire -Lease batch-a -ProjectPath $projA
    Assert-Equal $batchA.value.status "waiting" "tracked editor still blocks same-project batch"
    [void](Invoke-Coordinator -Action Cancel -Lease batch-a)
    [void](Invoke-Coordinator -Action Release -Lease batch-b)
    [void](Invoke-Coordinator -Action Release -Lease editor-a)

    $legacyDir = Join-Path $State "owner"
    New-Item -ItemType Directory -Force -Path $legacyDir | Out-Null
    $legacyOwner = [ordered]@{ lease = "old-script"; slot = "agent-9"; mode = "batch"; projectPath = $projB; processId = 0; acquiredAt = [datetime]::UtcNow.ToString("o"); updatedAt = [datetime]::UtcNow.ToString("o") }
    [System.IO.File]::WriteAllText((Join-Path $legacyDir "owner.json"), ($legacyOwner | ConvertTo-Json), $Utf8NoBom)
    $legacyBlocked = Invoke-Coordinator -Action Acquire -Lease legacy-blocked -ProjectPath $projA
    Assert-Equal $legacyBlocked.code 20 "legacy owner blocks exit"
    Assert-Equal $legacyBlocked.value.status "waiting" "legacy owner blocks all projects"
    Assert-Equal $legacyBlocked.value.owner.lease "old-script" "legacy owner surfaced"
    Remove-Item -LiteralPath $legacyDir -Recurse -Force
    $legacyCleared = Invoke-Coordinator -Action Acquire -Lease legacy-blocked -ProjectPath $projA
    Assert-Equal $legacyCleared.value.status "acquired" "acquire proceeds once legacy owner clears"
    [void](Invoke-Coordinator -Action Release -Lease legacy-blocked)

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
    $commonGit = (& git -C $repoRoot rev-parse --path-format=absolute --git-common-dir | Select-Object -First 1)
    $mainProject = Join-Path (Split-Path -Parent $commonGit) "src\Asteroids3D"
    $agentProject = Join-Path $repoRoot "src\Asteroids3D"

    Write-Snapshot @([ordered]@{ processId = 41001; commandLine = "Unity.exe -projectPath `"$mainProject`"" })
    $crossProject = Invoke-Coordinator -Action Acquire -Lease cross-project -ProjectPath $agentProject
    Assert-Equal $crossProject.code 0 "cross-project editor batch exit"
    Assert-Equal $crossProject.value.status "acquired" "cross-project editor does not block batch"
    [void](Invoke-Coordinator -Action Release -Lease cross-project)

    $userBlocked = Invoke-Coordinator -Action Acquire -Lease user-blocked -ProjectPath $mainProject
    Assert-Equal $userBlocked.code 20 "user editor same-project block exit"
    Assert-Equal $userBlocked.value.status "blocked_user_editor" "user editor classification"
    Assert-Equal $userBlocked.value.blockers[0].processId 41001 "user editor pid"
    [void](Invoke-Coordinator -Action Cancel -Lease user-blocked)

    $editorStrict = Invoke-Coordinator -Action Acquire -Lease editor-strict -Mode editor -ProjectPath $agentProject
    Assert-Equal $editorStrict.code 20 "editor-mode request keeps machine-wide block"
    Assert-Equal $editorStrict.value.status "blocked_user_editor" "editor-mode block classification"
    [void](Invoke-Coordinator -Action Cancel -Lease editor-strict)

    Write-Snapshot @([ordered]@{ processId = 41002; commandLine = "Unity.exe -batchMode -projectPath `"$mainProject`"" })
    $unmanaged = Invoke-Coordinator -Action Acquire -Lease unmanaged -ProjectPath $agentProject
    Assert-Equal $unmanaged.code 21 "unmanaged batch process exit"
    Assert-Equal $unmanaged.value.status "blocked_unmanaged_unity" "cross-project batch process still blocks"
    [void](Invoke-Coordinator -Action Cancel -Lease unmanaged)

    Write-Snapshot @([ordered]@{ processId = 41005; commandLine = "Unity.exe -projectPath `"$agentProject`"" })
    $sameProject = Invoke-Coordinator -Action Acquire -Lease same-project -ProjectPath $agentProject
    Assert-Equal $sameProject.code 21 "same-project editor exit"
    Assert-Equal $sameProject.value.status "blocked_unmanaged_unity" "editor on the requested project blocks batch"
    [void](Invoke-Coordinator -Action Cancel -Lease same-project)

    Write-Snapshot @()
    $bootBlockedOwn = Invoke-Coordinator -Action Acquire -Lease boot-blocked -ProjectPath $projA
    Assert-Equal $bootBlockedOwn.value.status "acquired" "boot-blocked owner acquire"
    Write-Snapshot @([ordered]@{ processId = 41006; commandLine = "Unity.exe -batchMode -projectPath `"$mainProject`"" })
    $bootBlocked = Invoke-Coordinator -Action BootAcquire -Lease boot-blocked -WaitSeconds 1
    Assert-Equal $bootBlocked.code 21 "boot acquire blocked exit"
    Assert-Equal $bootBlocked.value.status "blocked_unmanaged_unity" "untracked batch blocks boot lane"
    Write-Snapshot @()
    [void](Invoke-Coordinator -Action Release -Lease boot-blocked)

    [void](Invoke-Coordinator -Action Request -Lease stale -TicketTtlSeconds 1)
    $ticketFile = Get-ChildItem (Join-Path $State "queue") -Filter "*.json" | Select-Object -First 1
    $ticket = Get-Content $ticketFile.FullName -Raw | ConvertFrom-Json
    $ticket.updatedAt = [datetime]::UtcNow.AddMinutes(-5).ToString("o")
    [System.IO.File]::WriteAllText($ticketFile.FullName, ($ticket | ConvertTo-Json), $Utf8NoBom)
    $staleStatus = Invoke-Coordinator -Action Status -TicketTtlSeconds 1
    Assert-Equal @($staleStatus.value.queue).Count 0 "stale ticket cleanup"

    $attachAcquire = Invoke-Coordinator -Action Acquire -Lease attach -ProjectPath $agentProject
    Assert-Equal $attachAcquire.value.status "acquired" "attach owner acquire"
    Write-Snapshot @([ordered]@{ processId = 41003; commandLine = "Unity.exe -batchMode -projectPath `"$agentProject`"" })
    $attachOutput = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Attach -Lease attach -Slot agent-1 -Mode batch -ProcessId 41003 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    Assert-Equal $LASTEXITCODE 0 "attach exit"
    $attached = [string](@($attachOutput | Select-Object -Last 1)[0]) | ConvertFrom-Json
    Assert-Equal $attached.status "attached" "attach status"
    $status = Invoke-Coordinator -Action Status
    $attachOwner = Get-OwnerByLease $status "attach"
    Assert-Equal $attachOwner.processId 41003 "tracked process pid"

    Write-Snapshot @()
    [void](Invoke-Coordinator -Action Release -Lease attach)

    $batchProbe = Join-Path $Root "batch-probe.ps1"
    [System.IO.File]::WriteAllText($batchProbe, "exit 7", $Utf8NoBom)
    $batchOutput = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action RunBatch -Lease batch-probe -Slot agent-1 -Mode batch -ProjectPath $agentProject -BatchScript $batchProbe -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    Assert-Equal $LASTEXITCODE 0 "run batch coordinator exit"
    $batchResult = [string](@($batchOutput | Select-Object -Last 1)[0]) | ConvertFrom-Json
    Assert-Equal $batchResult.status "batch_complete" "run batch status"
    Assert-Equal $batchResult.exitCode 7 "run batch child exit"
    $afterBatch = Invoke-Coordinator -Action Status
    Assert-Equal @($afterBatch.value.owners).Count 0 "run batch releases owner"
    Assert-True ($null -eq $afterBatch.value.boot) "run batch releases boot lane"

    # RunBatch renews the owner lease for the child's whole run but frees the boot lane at startup.
    function Read-OwnerJson {
        param([string]$Lease)
        $dir = Join-Path $State "owners"
        if (-not (Test-Path -LiteralPath $dir)) { return $null }
        foreach ($file in @(Get-ChildItem $dir -Recurse -Filter "owner.json" -ErrorAction SilentlyContinue)) {
            try { $json = Get-Content $file.FullName -Raw | ConvertFrom-Json } catch { continue }
            if ([string]$json.lease -eq $Lease) { return $json }
        }
        return $null
    }
    Write-Snapshot @()
    $sleepProbe = Join-Path $Root "batch-sleep.ps1"
    [System.IO.File]::WriteAllText($sleepProbe, "Start-Sleep -Seconds 25`r`nexit 3`r`n", $Utf8NoBom)
    $liveOut = Join-Path $Root "batch-live.out"
    # OwnerTtl well below the child's run and BootTtl well above the boot window, so a surviving
    # owner can only mean holder-backing and a freed lane can only mean an explicit release.
    $liveRunner = Start-Process powershell -PassThru -NoNewWindow -RedirectStandardOutput $liveOut -ArgumentList @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $Coordinator,
        "-Action", "RunBatch", "-Lease", "batch-live", "-Slot", "agent-1", "-Mode", "batch",
        "-ProjectPath", $projA, "-BatchScript", $sleepProbe,
        "-StateRoot", $State, "-ProcessSnapshotPath", $Snapshot,
        "-OwnerTtlSeconds", "5", "-BootTtlSeconds", "600", "-BatchBootSeconds", "2",
        "-PollSeconds", "1", "-Json")
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while ($null -eq (Read-OwnerJson "batch-live") -and $sw.Elapsed.TotalSeconds -lt 30) { Start-Sleep -Milliseconds 200 }
        Assert-True ($null -ne (Read-OwnerJson "batch-live")) "RunBatch takes a project owner lease"
        Assert-True ([int](Read-OwnerJson "batch-live").holderProcessId -gt 0) "the batch owner records its holding coordinator"

        $sw.Restart()
        $laneFree = $false
        while (-not $laneFree -and -not $liveRunner.HasExited -and $sw.Elapsed.TotalSeconds -lt 30) {
            Start-Sleep -Milliseconds 500
            $laneFree = $null -eq (Invoke-Coordinator -Action Status -BootTtlSeconds 600 -OwnerTtlSeconds 5).value.boot
        }
        Assert-True $laneFree "RunBatch releases the boot lane once the startup window closes"
        Assert-True (-not $liveRunner.HasExited) "the boot lane is freed while the child is still running"

        # Past OwnerTtl: a pid-less owner would be reaped by any reader; a holder-backed one is not.
        Start-Sleep -Seconds 8
        Assert-True (-not $liveRunner.HasExited) "the child is still running past the owner TTL"
        $aged = Invoke-Coordinator -Action Status -OwnerTtlSeconds 5 -BootTtlSeconds 600
        Assert-Equal @($aged.value.owners | Where-Object { $_.lease -eq "batch-live" }).Count 1 `
            "the owner lease outlives OwnerTtlSeconds while its coordinator runs"
        $contend = Invoke-Coordinator -Action Acquire -Lease batch-thief -ProjectPath $projA -TicketTtlSeconds 900
        Assert-Equal $contend.value.status "waiting" "a running batch child still owns its project past the TTL"
        [void](Invoke-Coordinator -Action Cancel -Lease batch-thief)
    }
    finally {
        if (-not $liveRunner.HasExited) { [void]$liveRunner.WaitForExit(120000) }
        Write-Snapshot @()
    }
    $liveJson = [string](@(Get-Content -LiteralPath $liveOut | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
    Assert-Equal $liveJson.status "batch_complete" "long-running RunBatch completes"
    Assert-Equal $liveJson.exitCode 3 "long-running RunBatch returns the child exit code"
    $afterLive = Invoke-Coordinator -Action Status
    Assert-Equal @($afterLive.value.owners).Count 0 "long-running RunBatch releases its owner"

    # AttachBatchChild claims the child's Unity, so a batch owner's own child stops reading as an
    # unmanaged process blocking every other project (codex P1 on #224).
    Write-Snapshot @()
    [void](Invoke-Coordinator -Action Acquire -Lease child-claim -ProjectPath $agentProject)
    $absent = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action AttachBatchChild -Lease child-claim -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    $absentResult = [string](@($absent | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
    Assert-Equal $absentResult.status "batch_child_absent" "no child yet is not an error"

    Write-Snapshot @([ordered]@{ processId = 43001; commandLine = "Unity.exe -batchMode -projectPath `"$agentProject`"" })
    $blockedBefore = Invoke-Coordinator -Action Acquire -Lease child-rival -ProjectPath $projB
    Assert-Equal $blockedBefore.value.status "blocked_unmanaged_unity" "an unclaimed batch child blocks other projects"
    [void](Invoke-Coordinator -Action Cancel -Lease child-rival)

    $claimed = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action AttachBatchChild -Lease child-claim -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    $claimResult = [string](@($claimed | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
    Assert-Equal $claimResult.status "attached" "AttachBatchChild claims the project's batch Unity"
    Assert-Equal $claimResult.owner.processId 43001 "the claimed pid lands on the owner record"
    $freeAfter = Invoke-Coordinator -Action Acquire -Lease child-rival2 -ProjectPath $projB
    Assert-Equal $freeAfter.value.status "acquired" "a claimed child no longer blocks other projects"
    [void](Invoke-Coordinator -Action Release -Lease child-rival2)
    Write-Snapshot @()
    [void](Invoke-Coordinator -Action Release -Lease child-claim)

    # StartEditor composes caller args after the injected -projectPath, so owner and editor can't diverge.
    Write-Snapshot @()
    $edSentinel = Join-Path $Root "editorargs-sentinel.txt"
    if (Test-Path -LiteralPath $edSentinel) { Remove-Item -LiteralPath $edSentinel -Force }
    $recorder = Join-Path $Root "launch-recorder.cmd"
    [System.IO.File]::WriteAllText($recorder, "@echo off`r`necho %* > `"%UA_TEST_SENTINEL%`"`r`n", $Utf8NoBom)
    $env:UA_TEST_SENTINEL = $edSentinel
    try {
        $startInner = "& '$Coordinator' -Action StartEditor -Lease edargs -Slot agent-1 -ProjectPath '$projA' -UnityPath '$recorder' -SkipMcp -StateRoot '$State' -ProcessSnapshotPath '$Snapshot' -WaitSeconds 1 -Json -EditorArgs @('-batchmode','-nographics')"
        $startOut = @(& powershell -NoProfile -ExecutionPolicy Bypass -Command $startInner 2>&1)
        Assert-Equal $LASTEXITCODE 0 "StartEditor -EditorArgs exit"
        $startResult = [string](@($startOut | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
        Assert-Equal $startResult.status "attached" "StartEditor -EditorArgs attaches"
        Assert-True ([int]$startResult.owner.processId -gt 0) "StartEditor -EditorArgs records a pid"
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path -LiteralPath $edSentinel) -and $sw.Elapsed.TotalSeconds -lt 15) { Start-Sleep -Milliseconds 200 }
        Assert-True (Test-Path -LiteralPath $edSentinel) "StartEditor launches the exe with the passed EditorArgs"
        $launched = [string](Get-Content -LiteralPath $edSentinel -Raw)
        Assert-True ($launched -match [regex]::Escape([System.IO.Path]::GetFullPath($projA))) "the launch carries the coordinator's leased project path"
        Assert-True ($launched.IndexOf("-projectPath") -ge 0 -and $launched.IndexOf("-projectPath") -lt $launched.IndexOf("-batchmode")) "caller args compose after the injected -projectPath"
    }
    finally {
        Remove-Item Env:\UA_TEST_SENTINEL -ErrorAction SilentlyContinue
        Write-Snapshot @()
        [void](Invoke-Coordinator -Action Release -Lease edargs)
    }

    # Release -CloseEditor kills a live, coordinator-relevant, windowless (batch) owner.
    $killProc = Start-Process powershell -ArgumentList @("-NoProfile", "-Command", "Start-Sleep -Seconds 120") -WindowStyle Hidden -PassThru
    try {
        $killAcq = Invoke-Coordinator -Action Acquire -Lease closekill -ProjectPath $projA
        Assert-Equal $killAcq.value.status "acquired" "closekill acquire"
        [void](& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Attach -Lease closekill -Slot agent-1 -Mode editor -ProcessId $killProc.Id -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
        Assert-Equal $LASTEXITCODE 0 "closekill attach exit"
        Write-Snapshot @([ordered]@{ processId = $killProc.Id; commandLine = "Unity.exe -batchMode -projectPath `"$projA`"" })
        $killRel = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Release -Lease closekill -Slot agent-1 -CloseEditor -EditorCloseWaitSeconds 20 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
        Assert-Equal $LASTEXITCODE 0 "closekill release exit"
        $killResult = [string](@($killRel | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
        Assert-Equal $killResult.status "released" "Release -CloseEditor releases the owner"
        Assert-True ($null -eq (Get-Process -Id $killProc.Id -ErrorAction SilentlyContinue)) "Release -CloseEditor kills the windowless owner process"
    }
    finally {
        if ($null -ne (Get-Process -Id $killProc.Id -ErrorAction SilentlyContinue)) { Stop-Process -Id $killProc.Id -Force -ErrorAction SilentlyContinue }
        Write-Snapshot @()
    }

    # Release -CloseEditor never kills a live owner PID that is not a coordinator-relevant Unity process.
    $safeProc = Start-Process powershell -ArgumentList @("-NoProfile", "-Command", "Start-Sleep -Seconds 120") -WindowStyle Hidden -PassThru
    try {
        [void](Invoke-Coordinator -Action Acquire -Lease closesafe -ProjectPath $projA)
        [void](& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Attach -Lease closesafe -Slot agent-1 -Mode editor -ProcessId $safeProc.Id -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
        Write-Snapshot @()
        $safeRel = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Release -Lease closesafe -Slot agent-1 -CloseEditor -EditorCloseWaitSeconds 5 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
        Assert-Equal $LASTEXITCODE 0 "closesafe release exit"
        $safeResult = [string](@($safeRel | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
        Assert-Equal $safeResult.status "released" "Release -CloseEditor releases a stale-owner lease"
        Assert-True ($null -ne (Get-Process -Id $safeProc.Id -ErrorAction SilentlyContinue)) "Release -CloseEditor leaves a non-Unity PID alive"
    }
    finally {
        Stop-Process -Id $safeProc.Id -Force -ErrorAction SilentlyContinue
        Write-Snapshot @()
    }

    # Adopt seizes an untracked live batch editor (the RL orphan) into pid-backed ownership; batch is NOT refused.
    Write-Snapshot @([ordered]@{ processId = 42001; commandLine = "Unity.exe -batchMode -projectPath `"$projB`"" })
    $adopt = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Adopt -Lease adopt-orphan -Slot agent-1 -ProcessId 42001 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    Assert-Equal $LASTEXITCODE 0 "adopt untracked batch exit"
    $adoptResult = [string](@($adopt | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
    Assert-Equal $adoptResult.status "adopted" "adopt untracked batch status"
    Assert-Equal $adoptResult.owner.processId 42001 "adopted owner pid"
    Assert-Equal $adoptResult.owner.mode "batch" "adopted batch orphan keeps batch mode"
    $adoptStatus = Invoke-Coordinator -Action Status
    $adoptOwner = Get-OwnerByLease $adoptStatus "adopt-orphan"
    Assert-Equal $adoptOwner.processId 42001 "adopted owner visible in status"
    [void](Invoke-Coordinator -Action Release -Lease adopt-orphan)

    # Adopt refuses the hand-opened dev editor (non-batch on the primary project).
    Write-Snapshot @([ordered]@{ processId = 42002; commandLine = "Unity.exe -projectPath `"$mainProject`"" })
    $adoptUser = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Adopt -Lease adopt-user -Slot agent-1 -ProcessId 42002 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    Assert-Equal $LASTEXITCODE 24 "adopt user_editor refused exit"
    $adoptUserResult = [string](@($adoptUser | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
    Assert-Equal $adoptUserResult.status "adopt_refused_user_editor" "adopt refuses the user editor"

    # Adopt refuses to clobber a project that already has a different owner (lease-theft guard).
    [void](Invoke-Coordinator -Action Acquire -Lease adopt-incumbent -ProjectPath $projA)
    Write-Snapshot @([ordered]@{ processId = 42010; commandLine = "Unity.exe -batchMode -projectPath `"$projA`"" })
    $adoptOwned = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Adopt -Lease adopt-thief -Slot agent-1 -ProcessId 42010 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    Assert-Equal $LASTEXITCODE 24 "adopt project-owned refused exit"
    $adoptOwnedResult = [string](@($adoptOwned | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
    Assert-Equal $adoptOwnedResult.status "adopt_project_owned" "adopt refuses to clobber an owned project"
    $ownedStatus = Invoke-Coordinator -Action Status
    $incumbent = Get-OwnerByLease $ownedStatus "adopt-incumbent"
    Assert-Equal $incumbent.lease "adopt-incumbent" "incumbent owner lease survives an adopt attempt"
    [void](Invoke-Coordinator -Action Release -Lease adopt-incumbent)
    Write-Snapshot @()

    # Adopt refuses a PID the coordinator already tracks.
    [void](Invoke-Coordinator -Action Acquire -Lease adopt-track-owner -ProjectPath $agentProject)
    Write-Snapshot @([ordered]@{ processId = 42003; commandLine = "Unity.exe -batchMode -projectPath `"$agentProject`"" })
    [void](& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Attach -Lease adopt-track-owner -Slot agent-1 -Mode batch -ProcessId 42003 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    $adoptTracked = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Adopt -Lease adopt-steal -Slot agent-1 -ProcessId 42003 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    Assert-Equal $LASTEXITCODE 24 "adopt already-tracked refused exit"
    $adoptTrackedResult = [string](@($adoptTracked | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
    Assert-Equal $adoptTrackedResult.status "adopt_already_tracked" "adopt refuses a tracked pid"
    [void](Invoke-Coordinator -Action Release -Lease adopt-track-owner)

    # Adopt errors on a PID with no matching live Unity process.
    Write-Snapshot @()
    $adoptGhost = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Adopt -Lease adopt-ghost -Slot agent-1 -ProcessId 49999 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    Assert-Equal $LASTEXITCODE 24 "adopt non-existent pid exit"
    $adoptGhostResult = [string](@($adoptGhost | Where-Object { [string]$_ -match '^\s*[\{]' } | Select-Object -Last 1)) | ConvertFrom-Json
    Assert-Equal $adoptGhostResult.status "adopt_no_process" "adopt errors on unknown pid"

    Write-Host "UNITY_ACCESS_TESTS_PASSED assertions=$Assertions"
}
finally {
    if (Test-Path -LiteralPath $Root) { Remove-Item -LiteralPath $Root -Recurse -Force }
}
