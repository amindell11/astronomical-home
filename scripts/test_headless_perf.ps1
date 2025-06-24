param(
    [string]$Build = "./LightingTest.exe",
    [int]$ArenaCount = 4,
    [int]$Duration = 30,
    [int]$MinFps = 200,
    [string]$Log = "headless.log",
    [switch]$Help
)

function Show-Help {
    @"
Usage: pwsh -File test_headless_perf.ps1 [options]

Options (name=value):
    Build        Path to the Unity executable (default: ./LightingTest.exe)
    ArenaCount   Number of arenas to spawn      (default: 4)
    Duration     Seconds to run before quitting (default: 30)
    MinFps       Minimum acceptable avg FPS     (default: 200)
    Log          Log file path                  (default: headless.log)
    Help         Display this help text
"@
}

if ($Help) {
    Show-Help
    exit 0
}

if (-not (Test-Path $Build)) {
    Write-Error "Build executable '$Build' not found."
    exit 1
}

Write-Host "Running $Build for $Duration s with $ArenaCount arenas…"

& $Build -batchmode -nographics --arena-count $ArenaCount --duration $Duration -logFile $Log
if ($LASTEXITCODE -ne 0) {
    Write-Error "Unity build exited with code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$perfLine = Select-String -Path $Log -Pattern "PERF_RESULT" | Select-Object -Last 1
if (-not $perfLine) {
    Write-Error "PERF_RESULT line not found in log ($Log)."
    exit 1
}

if ($perfLine.Line -match 'avg_fps=([0-9.]+)') {
    $fps = [double]$Matches[1]
} else {
    Write-Error "Could not parse FPS value from PERF_RESULT line."
    exit 1
}

Write-Host "Average FPS reported: $fps"

if ($fps -ge $MinFps) {
    Write-Host "PASS: avg_fps ($fps) ≥ $MinFps."
    exit 0
} else {
    Write-Error "FAIL: avg_fps ($fps) < $MinFps."
    exit 1
} 