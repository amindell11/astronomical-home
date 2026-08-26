<#
.SYNOPSIS
  Preflight for the Unity CLI + batch-test workflow.

.DESCRIPTION
  Reports the live environment state that agents otherwise reconstruct (often
  wrongly) from memory before doing live-editor (unity CLI) or batch-test work:

    - Whether a live editor answers `unity command editor_status` for this
      project (the only reliable readiness gate — `unity status` discovery
      is broken both directions).
    - Which interactive Unity editor (if any) holds the project. A non-batch
      editor on the project makes the batch runner infra_error, and is the
      instance CLI commands route to via the per-project lockfile.
    - EnterPlayModeOptions: the domain-reload trap. In-Editor PlayMode test
      runs need domain reload ON; the DisableDomainReload bit leaks statics
      (GamePlane "already configured", ships won't move).
    - BurstCache presence: a cold cache makes the first batch run slow and can
      push perf-probe tests past their timeout.

  Diagnostic only: exits 0 unless -FailOnWarn is set. Use -Json for a
  machine-readable object.

.EXAMPLE
  powershell -File scripts/unity_doctor.ps1
.EXAMPLE
  powershell -File scripts/unity_doctor.ps1 -Json
#>
param(
    [string]$ProjectPath = "src/Asteroids3D",
    [string]$UnityCliPath = "$env:LOCALAPPDATA\Unity\bin\unity.exe",
    [switch]$Json,
    [switch]$FailOnWarn
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Find-InteractiveEditor {
    param([string]$ProjectFullPath)
    $normalizedProject = $ProjectFullPath.Replace('\', '/').ToLowerInvariant()
    try {
        $procs = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue
    }
    catch { return $null }
    if ($null -eq $procs) { return $null }
    foreach ($proc in $procs) {
        $cmd = [string]$proc.CommandLine
        if ([string]::IsNullOrWhiteSpace($cmd)) { continue }
        $cmdLower = $cmd.ToLowerInvariant()
        if ($cmdLower -notlike "*-projectpath*") { continue }
        $batch = ($cmdLower -like "*-batchmode*")
        $cmdNorm = $cmdLower.Replace('\', '/')
        if ($cmdNorm.Contains($normalizedProject)) {
            return [ordered]@{ pid = [int]$proc.ProcessId; batch = $batch }
        }
    }
    return $null
}

function Get-EnterPlayModeState {
    param([string]$ProjectFullPath)
    $path = Join-Path $ProjectFullPath "ProjectSettings/EditorSettings.asset"
    $result = [ordered]@{ found = $false; enabled = $null; options = $null; domainReloadDisabled = $null }
    if (-not (Test-Path -LiteralPath $path)) { return $result }
    $result.found = $true
    foreach ($line in (Get-Content -LiteralPath $path)) {
        if ($line -match 'm_EnterPlayModeOptionsEnabled:\s*(\d+)') { $result.enabled = [int]$Matches[1] }
        elseif ($line -match 'm_EnterPlayModeOptions:\s*(\d+)') { $result.options = [int]$Matches[1] }
    }
    if ($null -ne $result.enabled) {
        $disableDomainReloadBit = 1
        $fastEnterEnabled = ($result.enabled -eq 1)
        $result.domainReloadDisabled = ($fastEnterEnabled -and $null -ne $result.options -and (($result.options -band $disableDomainReloadBit) -ne 0))
    }
    return $result
}

$project = Resolve-FullPath $ProjectPath
$checks = New-Object System.Collections.Generic.List[object]
$unityAccess = $null

function Add-Check {
    param([string]$Name, [string]$Level, [string]$Detail)
    $checks.Add([ordered]@{ name = $Name; level = $Level; detail = $Detail })
}

$unityAccessScript = Join-Path $PSScriptRoot "unity_access.ps1"
if (Test-Path -LiteralPath $unityAccessScript) {
    try {
        $accessJson = & powershell -NoProfile -ExecutionPolicy Bypass -File $unityAccessScript -Action Status -Json
        $unityAccess = $accessJson | ConvertFrom-Json
        $owners = @($unityAccess.owners)
        foreach ($owner in $owners) {
            Add-Check "unity-access" "INFO" "Project owned: slot=$($owner.slot) mode=$($owner.mode) lease=$($owner.lease) pid=$($owner.processId) project=$($owner.projectPath)"
        }
        if ($null -ne $unityAccess.legacyOwner) {
            Add-Check "unity-access" "WARN" "Legacy machine-wide owner from an old script copy (slot=$($unityAccess.legacyOwner.slot) lease=$($unityAccess.legacyOwner.lease)); blocks all projects until it clears -- that session should pull main."
        }
        if ($null -ne $unityAccess.boot) {
            Add-Check "unity-access" "INFO" "Boot lane held by lease=$($unityAccess.boot.lease) (a Unity process is starting up)."
        }
        if (@($unityAccess.blockers | Where-Object { $_.kind -eq "user_editor" }).Count -gt 0) {
            $userEditor = @($unityAccess.blockers | Where-Object { $_.kind -eq "user_editor" })[0]
            Add-Check "unity-access" "WARN" "Untracked main-worktree editor is user-owned (pid=$($userEditor.processId)); main-project and editor-mode requests queue behind it -- ask the user to close it."
        }
        elseif (@($unityAccess.blockers).Count -gt 0) {
            Add-Check "unity-access" "WARN" "Untracked Unity process(es) present: $((@($unityAccess.blockers | ForEach-Object { $_.processId })) -join ','). Batch requests block on untracked batch processes and same-project editors."
        }
        elseif ($owners.Count -eq 0 -and $null -eq $unityAccess.legacyOwner) {
            Add-Check "unity-access" "OK" "All Unity projects free."
        }
        if (@($unityAccess.queue).Count -gt 0) {
            Add-Check "unity-queue" "INFO" "$(@($unityAccess.queue).Count) queued request(s): $((@($unityAccess.queue | ForEach-Object { "$($_.position):$($_.slot)" })) -join ', ')"
        }
    }
    catch {
        Add-Check "unity-access" "WARN" "Could not read Unity access coordinator state: $($_.Exception.Message)"
    }
}

$cliStatus = $null
if (-not (Test-Path -LiteralPath $UnityCliPath)) {
    Add-Check "cli-editor" "INFO" "unity CLI not found at $UnityCliPath; live-editor checks skipped."
}
else {
    try {
        $raw = & $UnityCliPath command editor_status --project-path $project --format json --no-banner --quiet
        $parsed = ($raw -join "`n") | ConvertFrom-Json
        if ($parsed.success) {
            $cliStatus = $parsed.data.result
            Add-Check "cli-editor" "OK" "Live editor answers editor_status (status=$($cliStatus.status) playMode=$($cliStatus.playMode) compiling=$($cliStatus.compiling))"
        }
        else {
            Add-Check "cli-editor" "INFO" "No live editor answers editor_status for this project (CLI reported failure)."
        }
    }
    catch {
        Add-Check "cli-editor" "INFO" "No live editor answers editor_status for this project."
    }
}

$editor = Find-InteractiveEditor -ProjectFullPath $project
if ($null -eq $editor) {
    Add-Check "project-editor" "OK" "No interactive Unity editor holds this project; batch runner can run."
}
elseif ($editor.batch) {
    Add-Check "project-editor" "INFO" "A batch Unity is running on this project (pid $($editor.pid)) -- likely an in-progress test run."
}
else {
    Add-Check "project-editor" "WARN" "Interactive Unity editor holds this project (pid $($editor.pid)). Batch runner will infra_error; close it before batch tests. CLI commands route to this instance."
}

$epm = Get-EnterPlayModeState -ProjectFullPath $project
if (-not $epm.found) {
    Add-Check "enter-playmode" "WARN" "EditorSettings.asset not found under $project"
}
elseif ($epm.domainReloadDisabled) {
    Add-Check "enter-playmode" "WARN" "DisableDomainReload is set (enabled=$($epm.enabled), options=$($epm.options)). In-Editor PlayMode suite leaks statics -> bogus failures. Set EnterPlayModeOptions to None before test runs, restore after."
}
else {
    Add-Check "enter-playmode" "OK" "Domain reload active (enabled=$($epm.enabled), options=$($epm.options)); safe for PlayMode tests."
}

$burst = Join-Path $project "Library/BurstCache"
if (Test-Path -LiteralPath $burst) {
    Add-Check "burst-cache" "OK" "Library/BurstCache present (warm); first batch run won't pay full Burst compile."
}
else {
    Add-Check "burst-cache" "INFO" "No Library/BurstCache (cold). First batch run is slow and may time out perf-probe tests."
}

if ($Json.IsPresent) {
    $out = [ordered]@{
        projectPath   = $project
        cliEditor     = $cliStatus
        enterPlayMode = $epm
        unityAccess    = $unityAccess
        checks        = $checks
    }
    $out | ConvertTo-Json -Depth 6
}
else {
    Write-Host "unity doctor  ($project)"
    Write-Host ("-" * 60)
    foreach ($c in $checks) {
        $tag = switch ($c.level) {
            "OK"   { "[ OK ]" }
            "WARN" { "[WARN]" }
            "FAIL" { "[FAIL]" }
            default { "[INFO]" }
        }
        Write-Host ("{0} {1,-16} {2}" -f $tag, $c.name, $c.detail)
    }
}

$hasWarn = @($checks | Where-Object { $_.level -eq "WARN" -or $_.level -eq "FAIL" }).Count -gt 0
if ($FailOnWarn.IsPresent -and $hasWarn) { exit 1 }
exit 0
