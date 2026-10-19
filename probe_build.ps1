param(
    [string]$Unity,
    [string]$Project,
    [string]$OutDir,
    [string]$Log
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$arguments = @(
    "-batchmode",
    "-quit",
    "-projectPath", "`"$Project`"",
    "-executeMethod", "HangarPerfProbeBuild.Build",
    "-probeOut", "`"$OutDir`"",
    "-logFile", "`"$Log`""
)
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -NoNewWindow -PassThru
[void]$process.Handle
if (-not $process.WaitForExit(2400000)) { throw "build timed out" }
$process.WaitForExit()
exit $process.ExitCode
