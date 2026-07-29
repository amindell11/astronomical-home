# Opaque batch child for unity_access.ps1 -Action RunBatch (which renews the owner lease for the
# whole run and releases the boot lane once EVAL_LOG shows startup is past the contention window).
# Reads EVAL_* + RL_EVAL_* from the inherited environment (eval_gate.py sets them); Unity exits itself.
$ErrorActionPreference = "Stop"
& $env:EVAL_UNITY -projectPath $env:EVAL_PROJ -batchmode -nographics `
    -executeMethod Game.RLHarness.TrainingBootstrap.RunEval -logFile $env:EVAL_LOG
exit $LASTEXITCODE
