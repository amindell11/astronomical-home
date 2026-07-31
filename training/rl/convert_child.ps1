# Opaque batch child for unity_access.ps1 -Action RunBatch: the player eval lane's convert tollbooth.
# Reads HARNESS_* + RL_HARNESS_* from the inherited environment (eval_lane.py sets them); Unity exits itself.
$ErrorActionPreference = "Stop"
$unityArgs = @('-projectPath', $env:HARNESS_PROJ, '-batchmode', '-nographics',
    '-executeMethod', 'Game.RLHarness.RLEvalModelConvert.Convert', '-logFile', $env:HARNESS_LOG)
& $env:HARNESS_UNITY @unityArgs
exit $LASTEXITCODE
