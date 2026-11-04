param([Parameter(Mandatory=$true)][string]$ConfigPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
. (Join-Path $config.repoRoot 'scripts/lib/unity_editor.ps1')
. (Join-Path $config.repoRoot 'scripts/lib/process_tree.ps1')
$unity = Resolve-UnityEditorPath -ProjectPath $config.projectPath
if ($config.PSObject.Properties.Name -contains 'testFilter') {
    $arguments = @('-batchmode', '-projectPath', $config.projectPath, '-runTests', '-testPlatform', 'EditMode', '-testFilter', $config.testFilter, '-testResults', $config.testResults, '-logFile', $config.logPath)
} else {
    $arguments = @('-batchmode', '-quit', '-projectPath', $config.projectPath, '-executeMethod', $config.method, '-logFile', $config.logPath)
}
$quoted = $arguments | ForEach-Object { '"' + $_ + '"' }
$process = Start-Process -FilePath $unity -ArgumentList $quoted -WindowStyle Hidden -PassThru
[void]$process.Handle
if (-not $process.WaitForExit(600000)) {
    Stop-ProcessTree -ProcessId $process.Id
    throw "Locale batch timed out. See $($config.logPath)"
}
$process.WaitForExit()
$process.Refresh()
exit $process.ExitCode

