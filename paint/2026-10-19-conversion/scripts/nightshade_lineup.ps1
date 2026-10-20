param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [Parameter(Mandatory = $true)][string]$LogPath,
    [switch]$Variants,
    [string]$MasksDir = ""
)
# Evidence-only: renders the Nightshade conversion lineup through NightshadeLineup.Capture in a batch
# editor. Run under the coordinator: unity_access.ps1 -Action RunBatch -BatchScript <this> -BatchArguments @(...)
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
    "-executeMethod", "NightshadeLineup.Capture",
    "-lineupOut", $OutDir,
    "-logFile", $LogPath,
    "-quit"
)
if ($Variants) { $arguments += "-lineupVariants" }
if ($MasksDir -ne "") { $arguments += @("-lineupMasks", $MasksDir) }
Write-Host "Launching $unity $($arguments -join ' ')"
$process = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
Write-Host "Unity exit code $($process.ExitCode)"
exit $process.ExitCode
