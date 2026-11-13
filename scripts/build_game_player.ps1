<#
.SYNOPSIS
    Build this worktree's Windows game player into build/game/, through one coordinated Unity batch boot.

.DESCRIPTION
    Run the copy inside the worktree. That copy builds that worktree:

        powershell -NoProfile -ExecutionPolicy Bypass -File <worktree>/scripts/build_game_player.ps1

    Runs Game.GamePlayerBuild.Build as one unity_access RunBatch, leased per call.
    The player comes from the project-wide settings (Build Settings scenes and PlayerSettings).
    An active custom build profile (such as Main) is switched to the platform profile first, and
    the worktree's Library keeps that switch: the profile's player-settings copy would replace those settings.

    OUTPUT
      <worktree>/build/game/           Player folder. Emptied first, so a renamed product leaves
                                       no stale exe. The exe is PlayerSettings.productName + ".exe".
      <worktree>/build/game-build.log  Unity log.
      <worktree>/build/game-build.json Config for the RunBatch child, UTF-8 without BOM, with
                                       projectPath, buildDir, and logPath. The child is
                                       build_game_player_batch.ps1.

    MACHINE CHANNEL
      Success writes exactly one compressed JSON line on stdout:
        {"status":"built","buildDir":"<abs path>","log":"<abs path>"}
      Everything else goes to stderr.

    EXIT CODES
      0  built. The coordinator status is batch_complete and the Unity child exited 0.
      1  anything else. The stderr message carries the coordinator exit, the coordinator status,
         the Unity exit code, and the log path.

.PARAMETER WaitSeconds
    Seconds to wait for this project's Unity lease and the boot lane. Default 300.

.PARAMETER AllowLowMemory
    Passed through to the RunBatch boot. Pass it only after the user approved that specific boot.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File <worktree>/scripts/build_game_player.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File <worktree>/scripts/build_game_player.ps1 -WaitSeconds 600
#>
param(
    [int]$WaitSeconds = 300,
    [switch]$AllowLowMemory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "lib/repo_root.ps1")
. (Join-Path $PSScriptRoot "unity_access_client.ps1")

$repo = Get-RepoRoot -ProbePath $PSScriptRoot
$project = Join-Path $repo "src/Asteroids3D"
$buildRoot = Join-Path $repo "build"
$buildDir = Join-Path $buildRoot "game"
$log = Join-Path $buildRoot "game-build.log"
$configPath = Join-Path $buildRoot "game-build.json"

if (Test-Path -LiteralPath $buildDir) {
    Remove-Item -Recurse -Force -LiteralPath $buildDir
}
New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null

$config = [ordered]@{ projectPath = $project; buildDir = $buildDir; logPath = $log }
[System.IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))

# Collect then index: a git pipe can kill the command mid-exit.
$branchLines = @(& git -C $repo branch --show-current)
$slot = if ($branchLines.Count -gt 0) { [string]$branchLines[0] } else { "" }
if ([string]::IsNullOrWhiteSpace($slot)) { $slot = "main" }
$lease = "game-build-$slot-$([guid]::NewGuid().ToString('N'))"

$arguments = @(
    "-Action", "RunBatch",
    "-Lease", $lease,
    "-Slot", $slot,
    "-Mode", "batch",
    "-ProjectPath", $project,
    "-WaitSeconds", "$WaitSeconds",
    "-BatchLogPath", $log,
    "-BatchScript", (Join-Path $PSScriptRoot "build_game_player_batch.ps1"),
    "-BatchArguments", $configPath
)
if ($AllowLowMemory.IsPresent) { $arguments += "-AllowLowMemory" }

$call = Invoke-UnityAccessCoordinator -CoordinatorArgs $arguments
$result = $call.result
$status = $null
$unityExit = $null
if ($null -ne $result) {
    $status = [string]$result.status
    $exitProp = $result.PSObject.Properties["exitCode"]
    if ($null -ne $exitProp) { $unityExit = $exitProp.Value }
}
# batch_complete exits 0 even when the child failed, so the Unity exit is checked too.
if ($call.exitCode -ne 0 -or $null -eq $result -or $status -ne "batch_complete" -or $null -eq $unityExit -or [int]$unityExit -ne 0) {
    throw "Game player build failed: coordinator exit=$($call.exitCode) status=$status unity exit=$unityExit log=$log $($call.stderr)"
}

[Console]::Out.WriteLine(([ordered]@{ status = "built"; buildDir = $buildDir; log = $log } | ConvertTo-Json -Compress))
