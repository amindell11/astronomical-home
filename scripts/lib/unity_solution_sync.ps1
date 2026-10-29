<#
.SYNOPSIS
    Regenerate a Unity project's old-style (Rider package) .csproj/.sln files through one
    coordinated batch boot - the single producer of those files for scripts/.

.DESCRIPTION
    Dot-source: . (Join-Path $PSScriptRoot "lib/unity_solution_sync.ps1")
    Requires unity_access_client.ps1 (dot-source it first).

    Sync-UnitySolution <RepoRoot> <SolutionRoot> <OutputRoot> <UnityExe> <WaitSeconds> <TimeoutSeconds>
    runs scripts/sync_unity_solution.ps1 (RiderScriptEditor.SyncSolution, then quit) as a
    RunBatch under the Unity access coordinator, leased per call. It writes unity-sync.json and
    unity-sync.log under OutputRoot, and throws unless the child exited 0. The child refuses a
    worktree with tracked changes, because it restores any tracked file Unity touches.
#>

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
    $lease = "solution-sync-$slot-$([guid]::NewGuid().ToString('N'))"
    $logPath = Join-Path $OutputRoot "unity-sync.log"
    $configPath = Join-Path $OutputRoot "unity-sync.json"
    $config = [ordered]@{ unityPath = $UnityExe; projectPath = $SolutionRoot; repoRoot = $RepoRoot; logPath = $logPath; timeoutSec = $TimeoutSeconds }
    [System.IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))

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
