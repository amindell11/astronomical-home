$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath 'D:/amind/git/agent-4'
. ./scripts/lib/unity_editor.ps1
$editor = Resolve-UnityEditorPath -ProjectPath 'D:/amind/git/agent-4/src/Asteroids3D'
$unityArgs = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', 'D:/amind/git/agent-4/src/Asteroids3D',
    '-executeMethod', 'Combat.Projectiles.Visual.LaserPlaytestBuild.Build',
    '-laserPlaytestOutput', 'C:/Users/amind/.codex/visualizations/2026/10/21/01a15fa8-130c-7a20-af5e-62d067bb20db/turbo-ripper-playtest/Astronomical.exe',
    '-logFile', 'D:/amind/git/agent-4/results/turbo-ripper-player-build.log'
)
& $editor @unityArgs
exit $LASTEXITCODE
