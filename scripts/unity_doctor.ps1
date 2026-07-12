<#
.SYNOPSIS
  Preflight for the Unity-MCP + batch-test workflow.

.DESCRIPTION
  Reports the live environment state that agents otherwise reconstruct (often
  wrongly) from memory before doing Unity-MCP or batch-test work:

    - CoplayDev UnityMCP HTTP server (default port 8081) reachable or not
    - mcp-for-unity server process present or not
    - Which interactive Unity editor (if any) holds the project. A non-batch
      editor on the project makes the batch runner infra_error, and is the
      instance the MCP tools attach to.
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
    [int]$McpPort = 8081,
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

function Test-TcpPort {
    param([string]$TargetHost = "127.0.0.1", [int]$Port, [int]$TimeoutMs = 800)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect($TargetHost, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMs)) { return $false }
        $client.EndConnect($async)
        return $true
    }
    catch { return $false }
    finally { $client.Close() }
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
        # DisableDomainReload = bit 0. Only a concern when fast-enter is enabled.
        $result.domainReloadDisabled = ($result.enabled -eq 1 -and $null -ne $result.options -and (($result.options -band 1) -ne 0))
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
        if ($null -ne $unityAccess.owner) {
            Add-Check "unity-access" "INFO" "Lane owned by slot=$($unityAccess.owner.slot) mode=$($unityAccess.owner.mode) lease=$($unityAccess.owner.lease) pid=$($unityAccess.owner.processId)"
        }
        elseif (@($unityAccess.blockers | Where-Object { $_.kind -eq "user_editor" }).Count -gt 0) {
            $userEditor = @($unityAccess.blockers | Where-Object { $_.kind -eq "user_editor" })[0]
            Add-Check "unity-access" "WARN" "Untracked main-worktree editor is user-owned (pid=$($userEditor.processId)); agents must queue and ask the user to close it."
        }
        elseif (@($unityAccess.blockers).Count -gt 0) {
            Add-Check "unity-access" "WARN" "Unity lane is blocked by unmanaged Unity process(es): $((@($unityAccess.blockers | ForEach-Object { $_.processId })) -join ',')"
        }
        else {
            Add-Check "unity-access" "OK" "Unity lane is free."
        }
        if (@($unityAccess.queue).Count -gt 0) {
            Add-Check "unity-queue" "INFO" "$(@($unityAccess.queue).Count) queued request(s): $((@($unityAccess.queue | ForEach-Object { "$($_.position):$($_.slot)" })) -join ', ')"
        }
    }
    catch {
        Add-Check "unity-access" "WARN" "Could not read Unity access coordinator state: $($_.Exception.Message)"
    }
}

# --- MCP HTTP server ---
$mcpUp = Test-TcpPort -Port $McpPort
if ($mcpUp) {
    Add-Check "mcp-http" "OK" "CoplayDev UnityMCP reachable at 127.0.0.1:$McpPort"
}
else {
    Add-Check "mcp-http" "WARN" "No listener on 127.0.0.1:$McpPort. Server down, on a different port, or bound to another editor instance. If it came up after session start, run /mcp to reconnect."
}

# --- mcp-for-unity server process ---
try { $mcpProcs = @(Get-Process -Name "mcp-for-unity" -ErrorAction SilentlyContinue) } catch { $mcpProcs = @() }
if ($mcpProcs.Count -gt 0) {
    Add-Check "mcp-server-proc" "INFO" "mcp-for-unity process running (pid $(( $mcpProcs | ForEach-Object { $_.Id }) -join ','))"
}
else {
    Add-Check "mcp-server-proc" "INFO" "No mcp-for-unity process. Plugin-spawned server dies on domain reload; start it manually to survive reloads (uvx --offline --from mcpforunityserver==10.0.0 ...)."
}

# --- interactive editor holding the project ---
$editor = Find-InteractiveEditor -ProjectFullPath $project
if ($null -eq $editor) {
    Add-Check "project-editor" "OK" "No interactive Unity editor holds this project; batch runner can run."
}
elseif ($editor.batch) {
    Add-Check "project-editor" "INFO" "A batch Unity is running on this project (pid $($editor.pid)) -- likely an in-progress test run."
}
else {
    Add-Check "project-editor" "WARN" "Interactive Unity editor holds this project (pid $($editor.pid)). Batch runner will infra_error; close it before batch tests. MCP tools attach to this instance."
}

# --- EnterPlayModeOptions ---
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

# --- BurstCache ---
$burst = Join-Path $project "Library/BurstCache"
if (Test-Path -LiteralPath $burst) {
    Add-Check "burst-cache" "OK" "Library/BurstCache present (warm); first batch run won't pay full Burst compile."
}
else {
    Add-Check "burst-cache" "INFO" "No Library/BurstCache (cold). First batch run is slow and may time out perf-probe tests."
}

# --- output ---
if ($Json.IsPresent) {
    $out = [ordered]@{
        projectPath   = $project
        mcpPort       = $McpPort
        mcpReachable  = $mcpUp
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
