param(
    [Parameter(Mandatory)]
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$config = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
$unity = [System.IO.Path]::GetFullPath([string]$config.unityPath)
$project = [System.IO.Path]::GetFullPath([string]$config.projectPath)
$log = [System.IO.Path]::GetFullPath([string]$config.logPath)
$timeoutMs = [Math]::Max(1, [int]$config.timeoutSec) * 1000

if (-not (Test-Path -LiteralPath $unity -PathType Leaf)) { throw "Unity executable not found: $unity" }
if (-not (Test-Path -LiteralPath $project -PathType Container)) { throw "Unity project not found: $project" }

$logParent = Split-Path -Parent $log
New-Item -ItemType Directory -Force -Path $logParent | Out-Null

$arguments = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $project,
    "-executeMethod", "Packages.Rider.Editor.RiderScriptEditor.SyncSolution",
    "-logFile", $log
)
$process = Start-Process -FilePath $unity -ArgumentList $arguments -NoNewWindow -PassThru
[void]$process.Handle
if (-not $process.WaitForExit($timeoutMs)) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw "Unity solution synchronization timed out. See $log"
}
$process.WaitForExit()
$process.Refresh()

if ($process.ExitCode -ne 0) { throw "Unity solution synchronization failed with exit code $($process.ExitCode). See $log" }
