$ErrorActionPreference = "Stop"
$unityArgs = @('-projectPath', $env:HARNESS_PROJ, '-batchmode', '-nographics',
    '-executeMethod', 'Game.RLHarness.RLEvalModelConvert.Convert', '-logFile', $env:HARNESS_LOG)
& $env:HARNESS_UNITY @unityArgs
exit $LASTEXITCODE
