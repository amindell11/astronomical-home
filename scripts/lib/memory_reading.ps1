<#
.SYNOPSIS
    The machine's memory reading - commit headroom and available physical RAM.

.DESCRIPTION
    Dot-source: . (Join-Path $PSScriptRoot "lib/memory_reading.ps1")

    Get-SystemMemoryReading reads Win32_OperatingSystem live; ConvertTo-MemoryReading
    parses any object carrying FreeVirtualMemory / FreePhysicalMemory in KB (the
    coordinator's test-injected snapshot takes that door). Both return an object with
    commitHeadroomGB and availablePhysicalGB, rounded to 2 decimals.

    Commit headroom (commit limit minus commit charge) is what a dying Unity boot runs
    out of. A reading that cannot be parsed throws: callers decide what "unavailable"
    means, and admitting by default on an unreadable reading is the failure mode the
    coordinator's memory admission exists to stop.
#>

function ConvertTo-MemoryReading {
    param([object]$Source)

    $headroomKb = 0.0
    $availableKb = 0.0
    $headroom = if ($null -eq $Source -or $null -eq $Source.PSObject.Properties["FreeVirtualMemory"]) { $null } else { $Source.PSObject.Properties["FreeVirtualMemory"].Value }
    $available = if ($null -eq $Source -or $null -eq $Source.PSObject.Properties["FreePhysicalMemory"]) { $null } else { $Source.PSObject.Properties["FreePhysicalMemory"].Value }
    if (-not [double]::TryParse([string]$headroom, [ref]$headroomKb)) {
        throw "Memory reading has no usable FreeVirtualMemory value."
    }
    if (-not [double]::TryParse([string]$available, [ref]$availableKb)) {
        throw "Memory reading has no usable FreePhysicalMemory value."
    }
    return [pscustomobject]@{
        commitHeadroomGB = [Math]::Round($headroomKb / 1048576.0, 2)
        availablePhysicalGB = [Math]::Round($availableKb / 1048576.0, 2)
    }
}

function Get-SystemMemoryReading {
    try { $source = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop }
    catch { throw "Memory reading failed: $($_.Exception.Message)" }
    return ConvertTo-MemoryReading -Source $source
}
