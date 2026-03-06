param(
    [string]$PlayerPath = "build\\StandaloneWindows64\\Asteroids3D.exe",
    [ValidateSet("combat_baseline")]
    [string]$Scenario = "combat_baseline",
    [int]$WarmupSeconds = 5,
    [int]$SampleSeconds = 30,
    [string]$OutDir = "results/latency-profiling",
    [int]$Seed = 1337,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [string]$QualityLevel = "",
    [int]$TeamACount = 1,
    [int]$TeamBCount = 6,
    [int]$AsteroidCount = -1,
    [float]$SpawnRadius = 60,
    [float]$RespawnDelay = 3,
    [int]$StartupTimeoutSeconds = 90,
    [switch]$CaptureProfilerRaw
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

$playerExe = Resolve-FullPath $PlayerPath
if (-not (Test-Path -LiteralPath $playerExe)) {
    throw "Player executable not found: $playerExe"
}

$rootOutDir = Resolve-FullPath $OutDir
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDir = Join-Path $rootOutDir $timestamp
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

$logPath = Join-Path $runDir "run.log"
$profilerRawPath = Join-Path $runDir "unity-profiler.raw"

$args = @(
    "-screen-fullscreen", "0",
    "-screen-width", $Width,
    "-screen-height", $Height,
    "-latencyProfileScenario", $Scenario,
    "-latencyWarmupSeconds", $WarmupSeconds,
    "-latencySampleSeconds", $SampleSeconds,
    "-latencyOutDir", $runDir,
    "-latencySeed", $Seed,
    "-latencyWidth", $Width,
    "-latencyHeight", $Height,
    "-latencyTeamACount", $TeamACount,
    "-latencyTeamBCount", $TeamBCount,
    "-latencySpawnRadius", $SpawnRadius,
    "-latencyRespawnDelay", $RespawnDelay,
    "-latencyStartupTimeoutSeconds", $StartupTimeoutSeconds,
    "-latencyDisableDebugOverlay",
    "-latencyDisableVSync"
)

if ($AsteroidCount -ge 0) {
    $args += @("-latencyAsteroidCount", $AsteroidCount)
}

if (-not [string]::IsNullOrWhiteSpace($QualityLevel)) {
    $args += @("-latencyQuality", $QualityLevel)
}

if ($CaptureProfilerRaw) {
    $args += @(
        "-profiler-enable",
        "-profiler-log-file", $profilerRawPath,
        "-profiler-capture-frame-count", [Math]::Max(1, $SampleSeconds * 120)
    )
}

$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $playerExe
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.Arguments = (($args | ForEach-Object {
    $text = [string]$_
    if ($text -match '[\s"]') {
        '"' + ($text -replace '"', '\"') + '"'
    }
    else {
        $text
    }
}) -join ' ')

$process = [System.Diagnostics.Process]::Start($startInfo)
$stdoutTask = $process.StandardOutput.ReadToEndAsync()
$stderrTask = $process.StandardError.ReadToEndAsync()
$process.WaitForExit()

$combinedLog = $stdoutTask.GetAwaiter().GetResult()
$stderrText = $stderrTask.GetAwaiter().GetResult()
if (-not [string]::IsNullOrWhiteSpace($stderrText)) {
    if (-not [string]::IsNullOrWhiteSpace($combinedLog)) {
        $combinedLog += [Environment]::NewLine
    }

    $combinedLog += $stderrText
}

Set-Content -LiteralPath $logPath -Value $combinedLog

if ($process.ExitCode -ne 0) {
    throw "Latency profiling run failed with exit code $($process.ExitCode). See $logPath"
}

Write-Host "Artifacts written to $runDir"
