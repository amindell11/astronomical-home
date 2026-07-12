Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Coordinator = Join-Path $PSScriptRoot "unity_access.ps1"
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
        [int]$TicketTtlSeconds = 900
    )
    $arguments = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $Coordinator,
        "-Action", $Action,
        "-StateRoot", $State,
        "-ProcessSnapshotPath", $Snapshot,
        "-TicketTtlSeconds", $TicketTtlSeconds,
        "-Json"
    )
    if (-not [string]::IsNullOrWhiteSpace($Lease)) { $arguments += @("-Lease", $Lease, "-Slot", $Slot, "-Mode", $Mode) }
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

try {
    Write-Snapshot @()

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
    Assert-Equal $waited.value.status "acquired" "wait acquires free lane"
    [void](Invoke-Coordinator -Action Release -Lease waited)

    [void](Invoke-Coordinator -Action Request -Lease alpha)
    [void](Invoke-Coordinator -Action Request -Lease beta)
    $beta = Invoke-Coordinator -Action Acquire -Lease beta
    Assert-Equal $beta.value.position 2 "FIFO preserves request order"
    $alpha = Invoke-Coordinator -Action Acquire -Lease alpha
    Assert-Equal $alpha.value.status "acquired" "queue head acquires"
    [void](Invoke-Coordinator -Action Release -Lease alpha)
    [void](Invoke-Coordinator -Action Cancel -Lease beta)

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    $commonGit = (& git -C $repoRoot rev-parse --path-format=absolute --git-common-dir | Select-Object -First 1)
    $mainProject = Join-Path (Split-Path -Parent $commonGit) "src\Asteroids3D"
    Write-Snapshot @([ordered]@{ processId = 41001; commandLine = "Unity.exe -projectPath `"$mainProject`"" })
    $userBlocked = Invoke-Coordinator -Action Acquire -Lease user-blocked
    Assert-Equal $userBlocked.code 20 "user editor block exit"
    Assert-Equal $userBlocked.value.status "blocked_user_editor" "user editor classification"
    Assert-Equal $userBlocked.value.blockers[0].processId 41001 "user editor pid"
    [void](Invoke-Coordinator -Action Cancel -Lease user-blocked)

    $agentProject = Join-Path $repoRoot "src\Asteroids3D"
    Write-Snapshot @([ordered]@{ processId = 41002; commandLine = "Unity.exe -projectPath `"$agentProject`"" })
    $unmanaged = Invoke-Coordinator -Action Acquire -Lease unmanaged
    Assert-Equal $unmanaged.code 21 "unmanaged editor exit"
    Assert-Equal $unmanaged.value.status "blocked_unmanaged_unity" "unmanaged editor classification"
    [void](Invoke-Coordinator -Action Cancel -Lease unmanaged)

    Write-Snapshot @()
    [void](Invoke-Coordinator -Action Request -Lease stale -TicketTtlSeconds 1)
    $ticketFile = Get-ChildItem (Join-Path $State "queue") -Filter "*.json" | Select-Object -First 1
    $ticket = Get-Content $ticketFile.FullName -Raw | ConvertFrom-Json
    $ticket.updatedAt = [datetime]::UtcNow.AddMinutes(-5).ToString("o")
    [System.IO.File]::WriteAllText($ticketFile.FullName, ($ticket | ConvertTo-Json), $Utf8NoBom)
    $staleStatus = Invoke-Coordinator -Action Status -TicketTtlSeconds 1
    Assert-Equal @($staleStatus.value.queue).Count 0 "stale ticket cleanup"

    $attachAcquire = Invoke-Coordinator -Action Acquire -Lease attach
    Assert-Equal $attachAcquire.value.status "acquired" "attach owner acquire"
    Write-Snapshot @([ordered]@{ processId = 41003; commandLine = "Unity.exe -batchMode -projectPath `"$agentProject`"" })
    $attachOutput = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Attach -Lease attach -Slot agent-1 -Mode batch -ProcessId 41003 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    Assert-Equal $LASTEXITCODE 0 "attach exit"
    $attached = [string](@($attachOutput | Select-Object -Last 1)[0]) | ConvertFrom-Json
    Assert-Equal $attached.status "attached" "attach status"
    $status = Invoke-Coordinator -Action Status
    Assert-Equal $status.value.owner.processId 41003 "tracked process pid"

    Write-Snapshot @()
    [void](Invoke-Coordinator -Action Release -Lease attach)
    $batchProbe = Join-Path $Root "batch-probe.ps1"
    [System.IO.File]::WriteAllText($batchProbe, "exit 7", $Utf8NoBom)
    $batchOutput = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action RunBatch -Lease batch-probe -Slot agent-1 -Mode batch -BatchScript $batchProbe -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
    Assert-Equal $LASTEXITCODE 0 "run batch coordinator exit"
    $batchResult = [string](@($batchOutput | Select-Object -Last 1)[0]) | ConvertFrom-Json
    Assert-Equal $batchResult.status "batch_complete" "run batch status"
    Assert-Equal $batchResult.exitCode 7 "run batch child exit"
    $afterBatch = Invoke-Coordinator -Action Status
    Assert-True ($null -eq $afterBatch.value.owner) "run batch releases owner"

    try {
        $stubbornAcquire = Invoke-Coordinator -Action Acquire -Lease stubborn-editor -Mode editor
        Assert-Equal $stubbornAcquire.value.status "acquired" "stubborn editor acquire"
        $stubbornAttach = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Attach -Lease stubborn-editor -Slot agent-1 -Mode editor -ProcessId $PID -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
        Assert-Equal $LASTEXITCODE 0 "stubborn editor attach exit"
        Write-Snapshot @([ordered]@{ processId = $PID; commandLine = "Unity.exe -projectPath `"$agentProject`"" })
        $stubbornRelease = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $Coordinator -Action Release -Lease stubborn-editor -CloseEditor -EditorCloseWaitSeconds 0 -StateRoot $State -ProcessSnapshotPath $Snapshot -Json 2>&1)
        Assert-Equal $LASTEXITCODE 23 "incomplete editor release exit"
        $stubbornResult = [string](@($stubbornRelease | Select-Object -Last 1)[0]) | ConvertFrom-Json
        Assert-Equal $stubbornResult.status "editor_did_not_exit" "incomplete editor release status"
        $stubbornStatus = Invoke-Coordinator -Action Status
        Assert-Equal $stubbornStatus.value.owner.lease "stubborn-editor" "incomplete editor release retains owner"
    }
    finally {
        Write-Snapshot @()
        [void](Invoke-Coordinator -Action Release -Lease stubborn-editor)
    }

    Write-Host "UNITY_ACCESS_TESTS_PASSED assertions=$Assertions"
}
finally {
    if (Test-Path -LiteralPath $Root) { Remove-Item -LiteralPath $Root -Recurse -Force }
}
