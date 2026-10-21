<#
.SYNOPSIS
    Unity editor executable resolution - the single owner of "which Unity.exe".

.DESCRIPTION
    Dot-source: . (Join-Path $PSScriptRoot "lib/unity_editor.ps1")

    Resolve-UnityEditorPath returns the editor matching the project's own
    ProjectSettings/ProjectVersion.txt, probing the known install roots. The
    version is never hardcoded: an editor upgrade edits ProjectVersion.txt and
    every script follows. -UnityPath parameters remain overrides of this default.

    Requires lib/repo_root.ps1 (dot-source it first) when -ProjectPath is omitted.

.NOTES
    When no candidate exists on this machine the first candidate path is returned
    unchanged, so the caller's own "Unity executable not found: <path>" throw
    names a real, diagnosable path instead of an empty string.
#>

# Install roots in probe order: this project's dev boxes, then the Hub default.
$Script:UnityEditorInstallRoots = @(
    "D:\Programs\Unity\Editor",
    "C:\Program Files\Unity\Hub\Editor"
)

$Script:UnityProjectRelativePath = "src/Asteroids3D"

function Get-UnityProjectVersion {
    param([string]$ProjectPath)

    $versionFile = Join-Path $ProjectPath "ProjectSettings/ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        throw "Unity project version file not found: $versionFile"
    }
    foreach ($line in @(Get-Content -LiteralPath $versionFile)) {
        if ($line -match '^\s*m_EditorVersion:\s*(\S+)\s*$') { return $Matches[1] }
    }
    throw "No m_EditorVersion line in $versionFile"
}

function Resolve-UnityEditorPath {
    param([string]$ProjectPath = "")

    if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
        $ProjectPath = Join-Path (Get-RepoRoot -ProbePath $PSScriptRoot) $Script:UnityProjectRelativePath
    }
    $version = Get-UnityProjectVersion -ProjectPath $ProjectPath
    $candidates = @($Script:UnityEditorInstallRoots | ForEach-Object { Join-Path $_ (Join-Path $version "Editor\Unity.exe") })
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    return $candidates[0]
}
