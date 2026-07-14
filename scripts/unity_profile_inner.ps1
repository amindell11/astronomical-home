param(
    [string]$UnityPath = "D:\Programs\Unity\Editor\6000.1.8f1\Editor\Unity.exe",
    [string]$ProjectPath = "src/Asteroids3D"
)

$ErrorActionPreference = "Stop"
$action = $env:ASTRO_PROFILE_ACTION
$method = switch ($action) {
    "build" { "Tools.Editor.PerformanceProfileBuild.Build" }
    "report" { "Tools.Editor.PerformanceProfileReport.Generate" }
    default { throw "Unknown ASTRO_PROFILE_ACTION '$action'." }
}
$logPath = if ($action -eq "build") { $env:ASTRO_PROFILE_BUILD_LOG } else { $env:ASTRO_PROFILE_REPORT_LOG }
& $UnityPath -batchmode -quit -projectPath $ProjectPath -executeMethod $method -logFile $logPath 2>&1 | Out-Host
$unityExitCode = $LASTEXITCODE
exit $unityExitCode
