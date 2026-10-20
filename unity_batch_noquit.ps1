param(
    [string]$Unity,
    [string]$Project,
    [string]$Method,
    [string]$Log
)
$ErrorActionPreference = "Stop"
$arguments = @(
    "-batchmode",
    "-projectPath", "`"$Project`"",
    "-executeMethod", $Method,
    "-logFile", "`"$Log`""
)
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -NoNewWindow -PassThru
[void]$process.Handle
if (-not $process.WaitForExit(420000)) { Stop-Process -Id $process.Id -Force; throw "batch run timed out" }
$process.WaitForExit()
exit $process.ExitCode
