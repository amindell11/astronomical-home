$ErrorActionPreference = "Stop"
$unityArgs = @('-projectPath', $env:HARNESS_PROJ, '-batchmode', '-nographics',
    '-executeMethod', 'RL.Hosts.PlayerEval.RLEvalModelConvert.Convert', '-logFile', $env:HARNESS_LOG)
& $env:HARNESS_UNITY @unityArgs
exit $LASTEXITCODE
