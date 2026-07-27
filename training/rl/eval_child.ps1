# Opaque batch child for unity_access.ps1 -Action RunBatch (the boot lane stays held for its whole run).
# Reads EVAL_* + RL_EVAL_* from the inherited environment (eval_gate.py sets them); Unity exits itself.
$ErrorActionPreference = "Stop"
& $env:EVAL_UNITY -projectPath $env:EVAL_PROJ -batchmode -nographics `
    -executeMethod Game.RLHarness.TrainingBootstrap.RunEval -logFile $env:EVAL_LOG
exit $LASTEXITCODE
