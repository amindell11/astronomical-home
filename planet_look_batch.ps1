param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][ValidateSet("capture", "tests")][string]$Task,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [Parameter(Mandatory = $true)][string]$LogPath
)
# Evidence-only, issue 1481. Run under the coordinator:
#   unity_access.ps1 -Action RunBatch -BatchScript <this> -BatchArguments @(...) -BatchLogPath <LogPath>
# capture: PlanetLookCapture.Run writes stills and bakes the owner's settings into the two planet materials.
# tests:   the graphics-tagged PlanetRenderTests fixture, EditMode, with a graphics device.
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $ProjectPath "../..")).Path
. (Join-Path $repoRoot "scripts/lib/repo_root.ps1")
. (Join-Path $repoRoot "scripts/lib/unity_editor.ps1")
$unity = Resolve-UnityEditorPath -ProjectPath $ProjectPath
if (-not (Test-Path -LiteralPath $unity)) { throw "Unity executable not found: $unity" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$arguments = @("-batchmode", "-projectPath", $ProjectPath, "-logFile", $LogPath)
if ($Task -eq "capture") {
    $arguments += @("-executeMethod", "PlanetLookCapture.Run", "-planetOut", $OutDir, "-quit")
} else {
    $arguments += @("-runTests", "-testPlatform", "EditMode",
        "-testFilter", "Tests.EditMode.Rendering.Locales.PlanetRenderTests",
        "-testResults", (Join-Path $OutDir "planet-render-tests.xml"))
}
Write-Host "Launching $unity $($arguments -join ' ')"
$process = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
Write-Host "Unity exit code $($process.ExitCode)"
exit $process.ExitCode
