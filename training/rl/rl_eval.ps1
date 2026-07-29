# Coordinated checkpoint-eval launcher for the teacher-tuning loop.
# Runs Game.RLHarness.TrainingBootstrap.RunEval against a frozen checkpoint under
# unity_access RunBatch (owner + boot-lane acquire/release, no stale leases), then
# prints the newest summary JSON. Env RL_EVAL_* is inherited by the batch child -> Unity.
param(
    [Parameter(Mandatory)][string]$Onnx,          # absolute path to the .onnx checkpoint
    [Parameter(Mandatory)][string]$Seeds,         # e.g. "2001,2002,...,2020" (keep disjoint from held-out 1001-1020)
    [int]$EpisodesPerSeed = 5,
    [string]$Density = "",                          # "" = canonical eval env (density 2.0); "3.0" for stretch
    [string]$Slot = "agent-3",
    [string]$Lease = "archetype-eval",
    [string]$Tag = ""
)
$ErrorActionPreference = "Stop"
$repo = "D:\amind\git\astronomical-home"
$proj = "D:\amind\git\$Slot\src\Asteroids3D"
$ts   = Get-Date -Format "yyyyMMdd-HHmmss"
$log  = "D:\amind\git\$Slot\results\rl-eval\eval-$ts$Tag.editor.log"
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null

$env:EVAL_UNITY = "D:\Programs\Unity\Editor\6000.1.8f1\Editor\Unity.exe"
$env:EVAL_PROJ  = $proj
$env:EVAL_LOG   = $log
$env:RL_EVAL_ONNX = $Onnx
$env:RL_EVAL_SEEDS = $Seeds
$env:RL_EVAL_EPISODES_PER_SEED = "$EpisodesPerSeed"
if ($Density) { $env:RL_EVAL_DENSITY = $Density } else { Remove-Item Env:\RL_EVAL_DENSITY -ErrorAction SilentlyContinue }

$densLabel = if ($Density) { $Density } else { "canonical(2.0)" }
Write-Host "Eval: onnx=$Onnx"
Write-Host "      seeds=$Seeds ep/seed=$EpisodesPerSeed density=$densLabel"
Write-Host "      log=$log"

& "$repo\scripts\unity_access.ps1" -Action RunBatch -Lease $Lease -Slot $Slot `
    -BatchScript "D:\amind\git\$Slot\training\rl\eval_child.ps1" -Json

$summary = Get-ChildItem "D:\amind\git\$Slot\results\rl-eval\*-summary.json" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if ($summary) { Write-Host "SUMMARY=$($summary.FullName)" } else { Write-Host "SUMMARY=(none written - check $log)" }
