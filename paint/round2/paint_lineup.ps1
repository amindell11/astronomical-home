param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [Parameter(Mandatory = $true)][string]$LogPath,
    [Parameter(Mandatory = $true)][string]$MaskRoot
)
# Evidence-only: renders the #1014 paint lineup through PaintPreviewLineup.Render in a batch editor.
# Run under the coordinator: unity_access.ps1 -Action RunBatch -BatchScript <this> -BatchArguments @(...)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $ProjectPath "../..")).Path
. (Join-Path $repoRoot "scripts/lib/repo_root.ps1")
. (Join-Path $repoRoot "scripts/lib/unity_editor.ps1")
$unity = Resolve-UnityEditorPath -ProjectPath $ProjectPath
if (-not (Test-Path -LiteralPath $unity)) { throw "Unity executable not found: $unity" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$arguments = @(
    "-batchmode",
    "-projectPath", $ProjectPath,
    "-executeMethod", "PaintPreviewLineup.Render",
    "-paintPreviewOut", $OutDir,
    "-paintPreviewMasks", $MaskRoot,
    "-logFile", $LogPath,
    "-quit"
)
Write-Host "Launching $unity $($arguments -join ' ')"
$process = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
Write-Host "Unity exit code $($process.ExitCode)"
exit $process.ExitCode
