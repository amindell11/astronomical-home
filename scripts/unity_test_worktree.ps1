param(
    [string]$WorktreePath = "../astronomical-home-wt-tests",
    [string]$Ref = "HEAD",
    [string]$ProjectSubPath = "src/Asteroids3D",
    [string]$OutDir = "results/unity-tests-agent",
    [string]$UnityPath = "D:\Programs\Unity\Editor\6000.1.8f1\Editor\Unity.exe",
    [ValidateSet("Both", "EditMode", "PlayMode")]
    [string]$Mode = "Both",
    [ValidateSet("Workspace", "Feature", "Module", "Smoke")]
    [string]$ScopeType = "Workspace",
    [string]$ScopeName = "",
    [string]$TestFilter = "",
    [string]$TestCategory = "",
    [string]$AssemblyNames = "",
    [switch]$IncludeStackTrace,
    [switch]$Recreate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

$repoRoot = (& git rev-parse --show-toplevel).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    throw "Could not resolve repo root."
}

$worktreeFullPath = Resolve-FullPath $WorktreePath
$projectFullPath = Join-Path $worktreeFullPath $ProjectSubPath
$outFullPath = Join-Path $worktreeFullPath $OutDir
$agentScript = Join-Path $repoRoot "scripts/unity_test_agent.ps1"

if ($Recreate -and (Test-Path -LiteralPath $worktreeFullPath)) {
    Write-Host "Removing existing worktree: $worktreeFullPath"
    & git worktree remove --force $worktreeFullPath
}

if (-not (Test-Path -LiteralPath $worktreeFullPath)) {
    Write-Host "Creating worktree at $worktreeFullPath (ref: $Ref)"
    & git worktree add --detach $worktreeFullPath $Ref
}
else {
    Write-Host "Using existing worktree: $worktreeFullPath"
}

if (-not (Test-Path -LiteralPath $projectFullPath)) {
    throw "Unity project path not found in worktree: $projectFullPath"
}

$args = @{
    UnityPath = $UnityPath
    ProjectPath = $projectFullPath
    OutDir = $outFullPath
    Mode = $Mode
    ScopeType = $ScopeType
    ScopeName = $ScopeName
    TestFilter = $TestFilter
    TestCategory = $TestCategory
    AssemblyNames = $AssemblyNames
}

if ($IncludeStackTrace) {
    $args["IncludeStackTrace"] = $true
}

Write-Host "Running Unity tests against worktree project: $projectFullPath"
& $agentScript @args
exit $LASTEXITCODE
