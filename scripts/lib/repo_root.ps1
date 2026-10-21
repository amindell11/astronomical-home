<#
.SYNOPSIS
    Repo-root resolution for scripts/ - the single owner.

.DESCRIPTION
    Dot-source: . (Join-Path $PSScriptRoot "lib/repo_root.ps1")

    Get-RepoRoot -ProbePath <path> returns the absolute worktree root containing
    ProbePath, asking git rather than counting '..' segments: a structural guess
    silently mis-resolves when a script moves or a project nests differently.

    Contract: throws when git cannot answer. Callers that want a fallback must
    choose one deliberately, in the open - never inside this helper.
#>

function Get-RepoRoot {
    param([string]$ProbePath)

    # Collect full output THEN take [0]: piping git into Select-Object -First 1 stops the pipeline early, which can kill git mid-exit and leave $LASTEXITCODE -1 despite good output.
    $lines = @(& git -C $ProbePath rev-parse --show-toplevel)
    $root = [string]$lines[0]
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
        throw "git rev-parse --show-toplevel failed under '$ProbePath'"
    }
    return [System.IO.Path]::GetFullPath($root)
}
