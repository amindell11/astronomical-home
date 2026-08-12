Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "..\resharper_ratchet.ps1")
. (Join-Path $PSScriptRoot "..\sync_unity_solution.ps1")

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "FAIL: $Message" }
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("resharper-ratchet-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
try {
    & git -C $temp init -q
    & git -C $temp config user.email ratchet-test@example.test
    & git -C $temp config user.name "Ratchet Test"
    & git -C $temp config core.autocrlf false
    $source = Join-Path $temp "src/Asteroids3D/Assets/Scripts/Ship.cs"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $source) | Out-Null
    [System.IO.File]::WriteAllText($source, "line1`nline2`nline3`nline4`n", (New-Object System.Text.UTF8Encoding($false)))
    & git -C $temp add .
    & git -C $temp commit -qm init
    [System.IO.File]::WriteAllText($source, "line1`nchanged2`nline3`nchanged4`n", (New-Object System.Text.UTF8Encoding($false)))

    $changed = Get-ChangedLineMap $temp HEAD
    Assert-True ($changed.ContainsKey("src/Asteroids3D/Assets/Scripts/Ship.cs")) "changed file is discovered"
    Assert-True ($changed["src/Asteroids3D/Assets/Scripts/Ship.cs"].Contains(2)) "first changed line is recorded"
    Assert-True ($changed["src/Asteroids3D/Assets/Scripts/Ship.cs"].Contains(4)) "second changed line is recorded"
    Assert-True (-not $changed["src/Asteroids3D/Assets/Scripts/Ship.cs"].Contains(3)) "unchanged line is excluded"

    $finding = [pscustomobject]@{ path = "src/Asteroids3D/Assets/Scripts/Ship.cs"; startLine = 2; endLine = 2 }
    Assert-True (Test-FindingTouchesChangedLine $finding $changed) "finding on a changed line blocks"
    $finding.startLine = 3
    $finding.endLine = 3
    Assert-True (-not (Test-FindingTouchesChangedLine $finding $changed)) "finding on an unchanged line is report-only"
    Assert-True (Test-ReportOnlyRule "Unity.PerformanceCriticalCodeInvocation") "performance indicator stays report-only"
    Assert-True (Test-ReportOnlyRule "Unity.InefficientMultiplicationOrder") "performance warning stays report-only"
    Assert-True (Test-ReportOnlyRule "Unity.PreferAddressByIdToGraphicsParams") "graphics lookup warning stays report-only"
    Assert-True (-not (Test-ReportOnlyRule "Unity.IncorrectMonoBehaviourInstantiation")) "Unity correctness finding can block"

    $sarifPath = Join-Path $temp "sample.sarif.json"
    $sarif = @{
        runs = @(@{
            results = @(
                @{ ruleId = "Unity.IncorrectMonoBehaviourInstantiation"; level = "warning"; message = @{ text = "bad new" }; locations = @(@{ physicalLocation = @{ artifactLocation = @{ uri = "Assets/Scripts/Ship.cs" }; region = @{ startLine = 2; endLine = 2 } } }) },
                @{ ruleId = "CSharpWarnings::CS0168"; level = "warning"; message = @{ text = "generic" }; locations = @(@{ physicalLocation = @{ artifactLocation = @{ uri = "Assets/Scripts/Ship.cs" }; region = @{ startLine = 3; endLine = 3 } } }) }
            )
        })
    }
    [System.IO.File]::WriteAllText($sarifPath, ($sarif | ConvertTo-Json -Depth 12), (New-Object System.Text.UTF8Encoding($false)))
    $parsed = @(Read-UnityFindings $sarifPath $temp (Join-Path $temp "src/Asteroids3D"))
    Assert-True ($parsed.Count -eq 1) "only Unity inspection results are retained"
    Assert-True ($parsed[0].path -eq "src/Asteroids3D/Assets/Scripts/Ship.cs") "SARIF path maps to the repository"
    Assert-True (Test-FindingTouchesChangedLine $parsed[0] $changed) "parsed finding overlaps the changed line"

    & git -C $temp restore $source
    $settingsPath = Join-Path $temp "src/Asteroids3D/ProjectSettings/ProjectSettings.asset"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $settingsPath) | Out-Null
    [System.IO.File]::WriteAllText($settingsPath, "    Standalone: UNITY_POST_PROCESSING_STACK_V2`n", (New-Object System.Text.UTF8Encoding($false)))
    & git -C $temp add $settingsPath
    & git -C $temp commit -qm settings
    [System.IO.File]::WriteAllText($settingsPath, "    Standalone: UNITY_POST_PROCESSING_STACK_V2;SENTIS_ANALYTICS_ENABLED`n", (New-Object System.Text.UTF8Encoding($false)))
    $cleanRejected = $false
    try { Assert-CleanTrackedWorktree $temp } catch { $cleanRejected = $true }
    Assert-True $cleanRejected "solution sync rejects a dirty tracked tree"
    Restore-UnityTrackedChanges $temp
    Assert-True (@(Get-TrackedChanges $temp).Count -eq 0) "solution sync restores tracked Unity mutations"

    Write-Host "PASS: ReSharper changed-line ratchet"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
