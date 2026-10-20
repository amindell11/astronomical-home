param(
    [string]$Unity,
    [string]$Project,
    [string]$Filter,
    [string]$Results,
    [string]$Log
)
$ErrorActionPreference = "Stop"
$arguments = @(
    "-batchmode",
    "-projectPath", "`"$Project`"",
    "-runTests",
    "-testPlatform", "EditMode",
    "-testFilter", "`"$Filter`"",
    "-testResults", "`"$Results`"",
    "-logFile", "`"$Log`""
)
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -NoNewWindow -PassThru
[void]$process.Handle
if (-not $process.WaitForExit(2400000)) { throw "test run timed out" }
$process.WaitForExit()
exit $process.ExitCode
