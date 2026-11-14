<#
.SYNOPSIS
    RunBatch child for build_game_player.ps1. Not a front door.

.DESCRIPTION
    Takes the config path that build_game_player.ps1 writes. The JSON object has
    projectPath, buildDir, and logPath, plus buildLabel when the front door was
    given -BuildLabel. buildLabel is forwarded to Unity as -gameBuildLabel,
    beside -gameBuildDir.

    Runs Game.GamePlayerBuild.Build in one Unity batch boot, then exits with Unity's exit code.

    A missing ConfigPath or a missing Unity executable throws, which exits 1.

.PARAMETER ConfigPath
    Path to the JSON config build_game_player.ps1 writes.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File <worktree>/scripts/build_game_player_batch.ps1 -ConfigPath <worktree>/build/game-build.json
#>
param(
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConfigPath)) { throw "ConfigPath is required." }

. (Join-Path $PSScriptRoot "lib/unity_editor.ps1")

$config = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
$unity = Resolve-UnityEditorPath -ProjectPath $config.projectPath
if (-not (Test-Path -LiteralPath $unity -PathType Leaf)) { throw "Unity executable not found: $unity" }

$arguments = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", [string]$config.projectPath,
    "-executeMethod", "Game.GamePlayerBuild.Build",
    "-gameBuildDir", [string]$config.buildDir,
    "-logFile", [string]$config.logPath
)
if ($null -ne $config.PSObject.Properties["buildLabel"]) {
    # Quoted so a space or an empty label arrives whole and the stamp refuses it.
    $arguments += @("-gameBuildLabel", ('"{0}"' -f $config.buildLabel))
}
$process = Start-Process -FilePath $unity -ArgumentList $arguments -NoNewWindow -PassThru
[void]$process.Handle
$process.WaitForExit()
exit $process.ExitCode
