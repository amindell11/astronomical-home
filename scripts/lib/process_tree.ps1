<#
.SYNOPSIS
    Kill a process AND its children - the single owner of "stop this Unity".

.DESCRIPTION
    Dot-source: . (Join-Path $PSScriptRoot "lib/process_tree.ps1")

    Stop-ProcessTree -ProcessId <int> kills the whole tree (taskkill /T /F on
    Windows, Stop-Process elsewhere). Root-only kills orphan Unity's bee/shader
    child processes, which keep the files they opened locked - the WinError 32
    pathology on the next delete or rename.

    Always succeeds: an already-dead process is the goal state, not an error.
#>

$Script:StopProcessTreeIsWindows = ($null -eq (Get-Variable -Name IsWindows -ErrorAction SilentlyContinue)) -or $IsWindows

function Stop-ProcessTree {
    param([int]$ProcessId)

    if ($ProcessId -le 0) { return }
    if ($Script:StopProcessTreeIsWindows) {
        # cmd swallows both streams inside itself. PowerShell's own `*> $null` does not help here:
        # redirecting a native command's stderr wraps each line in a NativeCommandError, which an
        # $ErrorActionPreference='Stop' caller then throws on - for an already-dead process.
        $null = & cmd.exe /c "taskkill /PID $ProcessId /T /F >nul 2>&1"
        return
    }
    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}
