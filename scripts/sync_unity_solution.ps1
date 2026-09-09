param(
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "lib/unity_churn.ps1")
. (Join-Path $PSScriptRoot "lib/process_tree.ps1")

function Assert-CleanTrackedWorktree {
    param([string]$RepoRoot)
    $changes = @(Get-TrackedChanges $RepoRoot)
    if ($changes.Count -gt 0) { throw "Unity solution sync requires a clean tracked worktree: $($changes -join ', ')" }
}

function Restore-UnityTrackedChanges {
    param([string]$RepoRoot)
    # Classify BEFORE restoring: the restore is what destroys the evidence the unexpected-changes throw reports.
    $verdict = Test-UnityAnalyticsChurnOnly -RepoRoot $RepoRoot
    $changes = @($verdict.changes)
    if ($changes.Count -eq 0) { return }
    & git -c core.excludesFile= -C $RepoRoot restore --worktree --source=HEAD -- . | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not restore tracked files changed by Unity solution sync." }
    $remaining = @(Get-TrackedChanges $RepoRoot)
    if ($remaining.Count -gt 0) { throw "Unity solution sync left tracked changes: $($remaining -join ', ')" }
    if (-not $verdict.knownChurn) { throw "Unity solution sync changed unexpected tracked files: $($changes -join ', ')" }
}

if ($MyInvocation.InvocationName -eq '.') { return }
if ([string]::IsNullOrWhiteSpace($ConfigPath)) { throw "ConfigPath is required." }

$config = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
$unity = [System.IO.Path]::GetFullPath([string]$config.unityPath)
$project = [System.IO.Path]::GetFullPath([string]$config.projectPath)
$repo = [System.IO.Path]::GetFullPath([string]$config.repoRoot)
$log = [System.IO.Path]::GetFullPath([string]$config.logPath)
$timeoutMs = [Math]::Max(1, [int]$config.timeoutSec) * 1000

if (-not (Test-Path -LiteralPath $unity -PathType Leaf)) { throw "Unity executable not found: $unity" }
if (-not (Test-Path -LiteralPath $project -PathType Container)) { throw "Unity project not found: $project" }
if (-not (Test-Path -LiteralPath (Join-Path $repo ".git"))) { throw "Git worktree not found: $repo" }
Assert-CleanTrackedWorktree $repo

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
try {
    $process = Start-Process -FilePath $unity -ArgumentList $arguments -NoNewWindow -PassThru
    [void]$process.Handle
    if (-not $process.WaitForExit($timeoutMs)) {
        Stop-ProcessTree -ProcessId $process.Id
        throw "Unity solution synchronization timed out. See $log"
    }
    $process.WaitForExit()
    $process.Refresh()

    if ($process.ExitCode -ne 0) { throw "Unity solution synchronization failed with exit code $($process.ExitCode). See $log" }
}
finally {
    Restore-UnityTrackedChanges $repo
}
