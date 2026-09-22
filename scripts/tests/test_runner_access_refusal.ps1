Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# A Unity access refusal on the direct (cold) path lands as an infra_error summary + STATUS= trailer
# + exit 2; a coordinator failure stays a wrapper failure. The agent runs from a copy of scripts/
# whose unity_access.ps1 is a stub speaking the coordinator's published channel, so no coordinator,
# Unity or machine state is consulted; the fake Unity executable proves nothing was launched.

$Root = Join-Path $env:TEMP ("runner-refusal-" + [guid]::NewGuid().ToString("N"))
$Scripts = Join-Path $Root "scripts"
$Source = Join-Path $PSScriptRoot ".."
New-Item -ItemType Directory -Force -Path (Join-Path $Scripts "lib") | Out-Null
foreach ($file in @("unity_test_agent.ps1", "unity_test_scope_lib.ps1", "unity_access_client.ps1", "unity_test_scopes.json")) {
    Copy-Item -LiteralPath (Join-Path $Source $file) -Destination $Scripts
}
Copy-Item -Path (Join-Path $Source "lib\*.ps1") -Destination (Join-Path $Scripts "lib")

$StubLog = Join-Path $Root "stub.log"
@'
param([string]$Action = "Status", [Parameter(ValueFromRemainingArguments = $true)][object[]]$Rest)
Add-Content -LiteralPath $env:RUNNER_REFUSAL_LOG -Value $Action
$answer = [ordered]@{ status = "ok" }
$exit = 0
switch ($Action) {
    "Acquire" {
        if ($env:RUNNER_REFUSAL_STUB -eq "acquire-waiting") { $answer = [ordered]@{ status = "waiting" }; $exit = 20 }
        else { $answer = [ordered]@{ status = "acquired" } }
    }
    "BootAcquire" {
        switch ($env:RUNNER_REFUSAL_STUB) {
            "boot-low-memory" { $answer = [ordered]@{ status = "boot_refused_low_memory"; commitHeadroomGB = 2.5; requiredHeadroomGB = 4 }; $exit = 28 }
            "coordinator-error" { $answer = [ordered]@{ status = "coordinator_error"; error = "state dir reaped" }; $exit = 1 }
            default { $answer = [ordered]@{ status = "boot_acquired" } }
        }
    }
}
if ($null -ne $answer) { Write-Output ($answer | ConvertTo-Json -Compress) }
exit $exit
'@ | Set-Content -LiteralPath (Join-Path $Scripts "unity_access.ps1") -Encoding UTF8

$Launched = Join-Path $Root "launched.txt"
$Unity = Join-Path $Root "Unity.cmd"
"@echo launched > `"$Launched`"" | Set-Content -LiteralPath $Unity -Encoding ASCII

$Repo = Join-Path $Root "repo"
$Project = Join-Path $Repo "src\Asteroids3D"
New-Item -ItemType Directory -Force -Path $Project | Out-Null
& git init -q -b agent-1 $Repo
& git -C $Repo config user.email refusal-test@example.test
& git -C $Repo config user.name "Refusal Test"
"base" | Set-Content -LiteralPath (Join-Path $Repo "file.txt")
& git -C $Repo add file.txt
& git -C $Repo commit -qm init

$OrderedList = Join-Path $Root "ordered.txt"
"Probe.One" | Set-Content -LiteralPath $OrderedList

function Invoke-Agent {
    param([string]$Stub, [string[]]$AgentArgs)
    $env:RUNNER_REFUSAL_STUB = $Stub
    $env:RUNNER_REFUSAL_LOG = $StubLog
    Set-Content -LiteralPath $StubLog -Value ""
    $outDir = Join-Path $Root ("out-" + [guid]::NewGuid().ToString("N"))
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $Scripts "unity_test_agent.ps1"),
        "-UnityPath", $Unity, "-ProjectPath", $Project, "-OutDir", $outDir,
        "-UnityAccessStateRoot", (Join-Path $Root "state")) + $AgentArgs
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $raw = @(& powershell @arguments 2>&1)
        $exit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previous }
    return [pscustomobject]@{
        exit = $exit
        text = (($raw | ForEach-Object { [string]$_ }) -join "`n")
        summary = Join-Path $outDir "latest-summary.json"
        calls = @(Get-Content -LiteralPath $StubLog | Where-Object { $_ })
    }
}

function Assert-Refusal {
    param([string]$Case, [object]$Run, [string[]]$Platforms, [string]$NoteFragment)
    if ($Run.exit -ne 2) { throw "${Case}: expected exit 2, got $($Run.exit)`n$($Run.text)" }
    if ($Run.text -notmatch '(?m)^STATUS=infra_error total=0 ') { throw "${Case}: missing the infra_error STATUS trailer`n$($Run.text)" }
    if (-not (Test-Path -LiteralPath $Run.summary)) { throw "${Case}: no summary written" }
    $summary = Get-Content -LiteralPath $Run.summary -Raw | ConvertFrom-Json
    if ($summary.status -ne "infra_error") { throw "${Case}: summary status $($summary.status)" }
    $runs = @($summary.runs)
    if ((@($runs | ForEach-Object { $_.platform }) -join ",") -ne ($Platforms -join ",")) {
        throw "${Case}: expected runs for $($Platforms -join ','), got $(@($runs | ForEach-Object { $_.platform }) -join ',')"
    }
    foreach ($record in $runs) {
        if ($record.status -ne "infra_error") { throw "${Case}: run $($record.platform) status $($record.status)" }
        if ($record.note -notlike "*$NoteFragment*") { throw "${Case}: run $($record.platform) note lacks '$NoteFragment': $($record.note)" }
    }
    if ($Run.text -notlike "*$NoteFragment*") { throw "${Case}: the refusal must be printed for the caller`n$($Run.text)" }
    if (Test-Path -LiteralPath $Launched) { throw "${Case}: Unity was launched despite the refusal" }
}

# Boot lane refused on the single-boot (gate) shape: both platforms carry the verdict.
$run = Invoke-Agent -Stub "boot-low-memory" -AgentArgs @("-Mode", "Both")
Assert-Refusal -Case "boot refusal (single boot)" -Run $run -Platforms @("EditMode", "PlayMode") -NoteFragment "2.5 GB commit headroom"

# Project acquire refused (a wait that expired): cancelled, and every planned platform carries it.
$run = Invoke-Agent -Stub "acquire-waiting" -AgentArgs @("-Mode", "Both")
Assert-Refusal -Case "acquire refusal" -Run $run -Platforms @("EditMode", "PlayMode") -NoteFragment "status=waiting"
if (($run.calls -join ",") -ne "Acquire,Cancel") { throw "a refused acquire must be cancelled and hold nothing (calls: $($run.calls -join ','))" }

# Per-platform loop with two platforms: the first refusal covers the rest, the coordinator is asked
# once, and the project lease it held is released.
$run = Invoke-Agent -Stub "boot-low-memory" -AgentArgs @("-Mode", "Both", "-OrderedTestListFile", $OrderedList)
Assert-Refusal -Case "boot refusal (two-platform loop)" -Run $run -Platforms @("EditMode", "PlayMode") -NoteFragment "2.5 GB commit headroom"
if (($run.calls -join ",") -ne "Acquire,BootAcquire,Release") { throw "the loop must stop at the first refusal and release the lease (calls: $($run.calls -join ','))" }

# coordinator_error is the coordinator's own failure, not a verdict: no summary and no trailer (an
# unhandled throw under -File exits 1, so the summary's absence is what tells it from red tests).
$run = Invoke-Agent -Stub "coordinator-error" -AgentArgs @("-Mode", "EditMode", "-TestFilter", "Probe")
if ($run.exit -eq 0) { throw "a coordinator failure must not exit 0`n$($run.text)" }
if ($run.text -match '(?m)^STATUS=') { throw "a coordinator failure must not print a STATUS trailer`n$($run.text)" }
if ($run.text -notlike "*Unity access BootAcquire failed (exit=1)*") { throw "a coordinator failure must name the action and exit`n$($run.text)" }
if (Test-Path -LiteralPath $run.summary) { throw "a coordinator failure must not write a summary" }

Remove-Item -LiteralPath $Root -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "PASS: direct-path access refusals land as infra_error summaries with exit 2; coordinator_error stays a wrapper failure"
