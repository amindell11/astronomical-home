# covers: training/rl/harness_child.ps1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$captureTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('capture-launch-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $captureTestRoot | Out-Null
$captureTestRoot = (Resolve-Path -LiteralPath $captureTestRoot).Path
if (-not $captureTestRoot.StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Scratch directory escaped the temporary root.'
}
$names = @('HARNESS_UNITY', 'HARNESS_PROJ', 'HARNESS_LOG', 'RL_HARNESS_RECORD', 'RL_HARNESS_CAPTURE_COMMIT', 'CAPTURE_TEST_RESULT')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    & git -C $captureTestRoot init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize fixture repository.' }
    & git -C $captureTestRoot -c user.name=CaptureTest -c user.email=capture-test@example.invalid commit --quiet --allow-empty -m fixture
    if ($LASTEXITCODE -ne 0) { throw 'Could not create fixture commit.' }
    $expected = (& git -C $captureTestRoot rev-parse HEAD).Trim()
    $fakeUnity = Join-Path $captureTestRoot 'unity.cmd'
    @'
@echo off
echo %RL_HARNESS_CAPTURE_COMMIT%> "%CAPTURE_TEST_RESULT%"
echo %*>> "%CAPTURE_TEST_RESULT%"
exit /b 0
'@ | Set-Content -LiteralPath $fakeUnity -Encoding ASCII
    $env:HARNESS_UNITY = $fakeUnity
    $env:HARNESS_PROJ = $captureTestRoot
    $env:HARNESS_LOG = Join-Path $captureTestRoot 'editor.log'
    $env:CAPTURE_TEST_RESULT = Join-Path $captureTestRoot 'launch.txt'
    $env:RL_HARNESS_RECORD = 'all'
    $env:RL_HARNESS_CAPTURE_COMMIT = 'stale-value'
    $child = Join-Path $PSScriptRoot '../../training/rl/harness_child.ps1'
    & powershell.exe -NoProfile -File $child
    if ($LASTEXITCODE -ne 0) { throw 'Recording launcher failed.' }
    $result = Get-Content -LiteralPath $env:CAPTURE_TEST_RESULT
    if ($result[0] -ne $expected) { throw 'Launcher did not replace stale provenance with the selected project HEAD.' }
    if ($result[1] -match '-batchmode|-nographics') { throw 'Recording child must render a windowed Game View.' }
    $env:RL_HARNESS_RECORD = ''
    & powershell.exe -NoProfile -File $child
    if ($LASTEXITCODE -ne 0) { throw 'Headless launcher failed.' }
    $result = Get-Content -LiteralPath $env:CAPTURE_TEST_RESULT
    if ($result[1] -notmatch '-batchmode' -or $result[1] -notmatch '-nographics') { throw 'Non-recording launch lost its headless flags.' }
    Write-Output 'PASS: launcher stamps recording provenance and preserves headless non-recording mode'
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    Remove-Item -LiteralPath $captureTestRoot -Recurse -Force
}
