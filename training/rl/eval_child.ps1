# Opaque batch child for unity_access.ps1 -Action RunBatch; EVAL_LOG is the lane's boot-progress signal.
# Reads EVAL_* + RL_EVAL_* from the inherited environment (eval_gate.py sets them); Unity exits itself.
$ErrorActionPreference = "Stop"
& $env:EVAL_UNITY -projectPath $env:EVAL_PROJ -batchmode -nographics `
    -executeMethod Game.RLHarness.TrainingBootstrap.RunEval -logFile $env:EVAL_LOG
exit $LASTEXITCODE
