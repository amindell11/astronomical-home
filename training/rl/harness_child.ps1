# Opaque batch child for unity_access.ps1 -Action RunBatch; HARNESS_LOG is the lane's boot-progress signal.
# Reads HARNESS_* + RL_HARNESS_* from the inherited environment (eval_lane.py sets them); Unity exits itself.
$ErrorActionPreference = "Stop"
& $env:HARNESS_UNITY -projectPath $env:HARNESS_PROJ -batchmode -nographics `
    -executeMethod Game.RLHarness.TrainingBootstrap.RunHarnessSession -logFile $env:HARNESS_LOG
exit $LASTEXITCODE
