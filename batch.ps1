param(
    [string]$Unity,
    [string]$Project,
    [string]$Method,
    [string]$Log,
    [string]$Extra = ""
)
$ErrorActionPreference = "Stop"
$arguments = @(
    "-batchmode",
    "-quit",
    "-projectPath", "`"$Project`"",
    "-executeMethod", $Method,
    "-logFile", "`"$Log`""
)
if ($Extra) { $arguments += ($Extra -split ' ') }
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -NoNewWindow -PassThru
[void]$process.Handle
if (-not $process.WaitForExit(3600000)) { throw "batch run timed out" }
$process.WaitForExit()
exit $process.ExitCode
