param(
    [string]$Exe,
    [string]$Locale,
    [string]$OutDir,
    [int]$TimeoutSec = 1500
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$out = Join-Path $OutDir "flight_$Locale.jsonl"
$log = Join-Path $OutDir "player_$Locale.log"
if (Test-Path $out) { Remove-Item $out }
$arguments = @(
    "-flightPerfProbe", "`"$out`"",
    "-probeLocale", $Locale,
    "-probeRes", "1920x1080,3840x2160",
    "-probeQuality", "0,2",
    "-probeFrames", "150",
    "-probeSettle", "8",
    "-logFile", "`"$log`""
)
$started = Get-Date
$process = Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru
[void]$process.Handle
if (-not $process.WaitForExit($TimeoutSec * 1000)) {
    Stop-Process -Id $process.Id -Force
    throw "player timed out after $TimeoutSec s"
}
"locale=$Locale exit=$($process.ExitCode) seconds=$([int]((Get-Date) - $started).TotalSeconds) rows=$((Get-Content $out -ErrorAction SilentlyContinue | Measure-Object -Line).Lines)"
