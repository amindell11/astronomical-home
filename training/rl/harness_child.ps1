# Opaque batch child for unity_access.ps1 -Action RunBatch; HARNESS_LOG is the lane's boot-progress signal.
# Reads HARNESS_* + RL_HARNESS_* from the inherited environment (eval_lane.py sets them); Unity exits itself.
$ErrorActionPreference = "Stop"
# A recording run films through the Game View, which needs a rendering windowed editor:
# Recorder's WaitForEndOfFrame never resumes under -batchmode. Non-recording runs stay headless.
$unityArgs = @('-projectPath', $env:HARNESS_PROJ)
if ([string]::IsNullOrEmpty($env:RL_HARNESS_RECORD)) { $unityArgs += @('-batchmode', '-nographics') }
$unityArgs += @('-executeMethod', 'Game.RLHarness.TrainingBootstrap.RunHarnessSession', '-logFile', $env:HARNESS_LOG)
& $env:HARNESS_UNITY @unityArgs
exit $LASTEXITCODE
