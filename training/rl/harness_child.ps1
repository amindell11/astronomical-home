# Opaque batch child for unity_access.ps1 -Action RunBatch; HARNESS_LOG is the lane's boot-progress signal.
# Reads HARNESS_* + RL_HARNESS_* from the inherited environment (eval_lane.py sets them); Unity exits itself.
# Recording launches stamp RL_HARNESS_CAPTURE_COMMIT from the project's Git HEAD before starting Unity.
$ErrorActionPreference = "Stop"
# A recording run films through the Game View, which needs a rendering windowed editor:
# WaitForEndOfFrame never resumes under -batchmode. Non-recording runs stay headless.
$unityArgs = @('-projectPath', $env:HARNESS_PROJ)
if ([string]::IsNullOrEmpty($env:RL_HARNESS_RECORD)) { $unityArgs += @('-batchmode', '-nographics') }
else {
    $captureCommit = (& git -C $env:HARNESS_PROJ rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $captureCommit -notmatch '^[0-9a-fA-F]{40}$') {
        throw "Cannot read the recording project's Git HEAD for capture provenance."
    }
    $env:RL_HARNESS_CAPTURE_COMMIT = $captureCommit
}
$unityArgs += @('-executeMethod', 'RL.Hosts.TrainingBootstrap.RunHarness', '-logFile', $env:HARNESS_LOG)
& $env:HARNESS_UNITY @unityArgs
exit $LASTEXITCODE
