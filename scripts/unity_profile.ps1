param(
    [ValidateSet("baseline", "candidate")]
    [string]$Label = "baseline",
    [int]$WarmupFrames = 300,
    [int]$SampleFrames = 1200,
    [int]$ExtraShips = 0,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [string]$UnityPath = "D:\Programs\Unity\Editor\6000.1.8f1\Editor\Unity.exe",
    [string]$OutDir = "results/profiling"
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "src/Asteroids3D"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot (Join-Path $OutDir "$stamp-$Label")))
$playerPath = Join-Path $runDir "player/AstronomicalProfile.exe"
$rawPath = Join-Path $runDir "$Label.raw"
$summaryPath = Join-Path $runDir "$Label-summary.json"
$innerScript = Join-Path $PSScriptRoot "unity_profile_inner.ps1"
$accessScript = Join-Path $PSScriptRoot "unity_access.ps1"
$branch = (& git -C $repoRoot branch --show-current | Select-Object -First 1)

New-Item -ItemType Directory -Force -Path $runDir | Out-Null

function Invoke-CoordinatedBatch {
    param([string]$Action)
    $env:ASTRO_PROFILE_ACTION = $Action
    $lease = "unity-profile-$branch-$Action-$stamp"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $accessScript `
        -Action RunBatch -Lease $lease -Slot $branch -Mode batch -ProjectPath $projectPath `
        -WaitSeconds 300 -BatchScript $innerScript -Json
    if ($LASTEXITCODE -ne 0) { throw "Unity profile $Action batch failed with exit $LASTEXITCODE." }
}

$env:ASTRO_PROFILE_BUILD = $playerPath
$env:ASTRO_PROFILE_BUILD_LOG = Join-Path $runDir "build.log"
Invoke-CoordinatedBatch "build"
if (-not (Test-Path -LiteralPath $playerPath)) { throw "Profiling player was not built at $playerPath." }

$playerArguments = @(
    "-screen-fullscreen", "0",
    "-screen-width", $Width,
    "-screen-height", $Height,
    "-astronomical-profile-output", $rawPath,
    "-astronomical-profile-warmup-frames", $WarmupFrames,
    "-astronomical-profile-sample-frames", $SampleFrames,
    "-astronomical-profile-extra-ships", $ExtraShips,
    "-logFile", (Join-Path $runDir "player.log")
)
$player = Start-Process -FilePath $playerPath -ArgumentList $playerArguments -PassThru -Wait
if ($player.ExitCode -ne 0) { throw "Profiling player exited with code $($player.ExitCode)." }
if (-not (Test-Path -LiteralPath $rawPath)) { throw "Unity Profiler capture was not written to $rawPath." }

$env:ASTRO_PROFILE_RAW = $rawPath
$env:ASTRO_PROFILE_SUMMARY = $summaryPath
$env:ASTRO_PROFILE_FRAME_HISTORY = [Math]::Max(300, $SampleFrames + 16)
$env:ASTRO_PROFILE_REPORT_LOG = Join-Path $runDir "report.log"
Invoke-CoordinatedBatch "report"
if (-not (Test-Path -LiteralPath $summaryPath)) { throw "Profiler summary was not written to $summaryPath." }

Write-Host "UNITY_PROFILE_RUN=$runDir"
Write-Host "UNITY_PROFILE_RAW=$rawPath"
Write-Host "UNITY_PROFILE_SUMMARY=$summaryPath"
