param(
    [string]$Exe,
    [string]$OutDir,
    [string]$Name,
    [string]$Extra = "",
    [int]$TimeoutSec = 1500
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$out = Join-Path $OutDir "$Name.jsonl"
$log = Join-Path $OutDir "player_$Name.log"
if (Test-Path $out) { Remove-Item $out }
$arguments = @(
    "-perfPassProbe", "`"$out`"",
    "-probeRes", "1920x1080,3840x2160",
    "-probeQuality", "2",
    "-logFile", "`"$log`""
)
if ($Extra) { $arguments += ($Extra -split ' ') }
$started = Get-Date
$process = Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru
[void]$process.Handle
if (-not $process.WaitForExit($TimeoutSec * 1000)) {
    Stop-Process -Id $process.Id -Force
    throw "player timed out after $TimeoutSec s"
}
"name=$Name exit=$($process.ExitCode) seconds=$([int]((Get-Date) - $started).TotalSeconds) rows=$((Get-Content $out -ErrorAction SilentlyContinue | Measure-Object -Line).Lines)"
