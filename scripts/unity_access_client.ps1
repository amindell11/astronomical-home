<#
.SYNOPSIS
    The sanctioned client for scripts/unity_access.ps1 - dot-source it and call
    Invoke-UnityAccessCoordinator instead of invoking and scraping the coordinator by hand.

.DESCRIPTION
    Dot-source: . (Join-Path $PSScriptRoot "unity_access_client.ps1")

    Invoke-UnityAccessCoordinator -CoordinatorArgs @("-Action", "Status") [-Coordinator <path>]
    returns [pscustomobject]@{ exitCode; result; stdout; stderr }, where `result` is the
    coordinator's single JSON line already parsed. -Json is supplied here; callers must not pass it.

    Why this exists: the coordinator's machine channel guarantees ONE compressed JSON line on
    stdout with everything else on stderr, so the parse is ConvertFrom-Json over the whole stdout
    stream - no line-sniffing for '^\s*{'. Every consumer that re-derived that was reading a layout
    the coordinator owns. See doc/agents/script-contracts.md.
#>

function Invoke-UnityAccessCoordinator {
    param(
        [Parameter(Mandatory = $true)][string[]]$CoordinatorArgs,
        [string]$Coordinator = ""
    )

    if ([string]::IsNullOrWhiteSpace($Coordinator)) { $Coordinator = Join-Path $PSScriptRoot "unity_access.ps1" }
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $Coordinator) + $CoordinatorArgs + @("-Json")

    # 2>&1 ENVELOPE HAZARD: under EAP=Stop a native command's stderr arrives as an ErrorRecord and
    # throws mid-capture, so the merge only ever happens with EAP relaxed. The merge is what lets
    # stderr be separated back out below - ErrorRecords are the coordinator's prose channel.
    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $raw = @(& powershell @arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousEap }

    $stdout = @($raw | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } | ForEach-Object { [string]$_ })
    $stderr = @($raw | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { [string]$_ })
    $text = ($stdout -join "`n").Trim()
    $result = $null
    if (-not [string]::IsNullOrWhiteSpace($text)) { $result = $text | ConvertFrom-Json }

    return [pscustomobject]@{
        exitCode = $exitCode
        result = $result
        stdout = $text
        stderr = ($stderr -join "`n")
    }
}
