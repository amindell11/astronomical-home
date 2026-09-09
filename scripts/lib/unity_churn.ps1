<#
.SYNOPSIS
    Classify a worktree's tracked changes as the known Unity analytics-define churn.

.DESCRIPTION
    The single owner of the restore allowlist. Unity rewrites
    src/Asteroids3D/ProjectSettings/ProjectSettings.asset's Standalone define line
    (SENTIS_ANALYTICS_ENABLED appears/disappears) on almost every batch run; that
    one-line flip is expected and gets restored. Anything else is a real edit the
    caller must surface, not silently discard.

    Dot-source (PowerShell):
        . (Join-Path $PSScriptRoot "lib/unity_churn.ps1")
        Test-UnityAnalyticsChurnOnly -RepoRoot <path>   # -> @{ knownChurn = <bool>; changes = @(...) }

    Invoke (bash and other callers):
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/lib/unity_churn.ps1 -WorktreePath <path>
        Machine channel: exactly one compressed JSON line on stdout,
        {"knownChurn":<bool>,"changes":[<porcelain lines>]}; prose to stderr.
        Exit 0 = classified (read knownChurn), exit 1 = could not inspect the worktree.

.NOTES
    Classification only - it never restores. Each caller does its own restore, and
    must capture this verdict BEFORE restoring: the restore destroys the evidence
    an unexpected-changes error needs to report.
#>

param(
    # Not named $RepoRoot: dot-sourcing binds param variables into the caller's scope, where that name is already taken.
    [string]$WorktreePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Script:UnityChurnFile = "src/Asteroids3D/ProjectSettings/ProjectSettings.asset"
# The whole allowlist: exactly this define line, with or without the analytics define.
$Script:UnityChurnLinePattern = '^[-+]\s+Standalone: UNITY_POST_PROCESSING_STACK_V2(?:;SENTIS_ANALYTICS_ENABLED)?$'

function Get-TrackedChanges {
    param([string]$RepoRoot)
    $changes = @(& git -c core.excludesFile= -C $RepoRoot status --porcelain --untracked-files=no)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect tracked files in $RepoRoot" }
    return $changes
}

function Test-UnityAnalyticsChurnOnly {
    param([string]$RepoRoot)

    $changes = @(Get-TrackedChanges $RepoRoot)
    if ($changes.Count -eq 0) { return [ordered]@{ knownChurn = $true; changes = @() } }

    $names = @(& git -c core.excludesFile= -C $RepoRoot diff --name-only)
    $numstat = @(& git -c core.excludesFile= -C $RepoRoot diff --numstat -- $Script:UnityChurnFile)
    $diff = @(& git -c core.excludesFile= -C $RepoRoot diff --unified=0 -- $Script:UnityChurnFile)
    $content = @($diff | Where-Object { $_ -match '^[+-]\s+Standalone:' -and $_ -notmatch '^[+-]{3}' })

    $known = $names.Count -eq 1 -and
        $names[0] -eq $Script:UnityChurnFile -and
        $numstat.Count -eq 1 -and
        $numstat[0] -eq "1`t1`t$Script:UnityChurnFile" -and
        $content.Count -eq 2 -and
        @($content | Where-Object { $_ -notmatch $Script:UnityChurnLinePattern }).Count -eq 0

    return [ordered]@{ knownChurn = [bool]$known; changes = $changes }
}

if ($MyInvocation.InvocationName -eq '.') { return }

if ([string]::IsNullOrWhiteSpace($WorktreePath)) { $WorktreePath = (Get-Location).Path }
try {
    $verdict = Test-UnityAnalyticsChurnOnly -RepoRoot $WorktreePath
}
catch {
    [Console]::Error.WriteLine([string]$_)
    exit 1
}
Write-Output ($verdict | ConvertTo-Json -Depth 4 -Compress)
exit 0
