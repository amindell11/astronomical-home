Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot "../unity_test_agent.ps1"), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw $errors[0] }
$function = $ast.Find({ param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
    $node.Name -eq 'Get-ProcessTreeMemory'
}, $true)
Invoke-Expression $function.Extent.Text
. (Join-Path $PSScriptRoot "../lib/memory_reading.ps1")

function Assert-Equal($Expected, $Actual, $What) {
    if ($Expected -ne $Actual) { throw "$What : expected $Expected, got $Actual" }
}
$gb = 1048576.0  # KB per GB - sample records carry KB, as Win32_Process reports them.
$born = [datetime]'2026-09-20T00:00:00Z'
function New-Record($ProcessId, $ParentProcessId, $PrivateGB, $PeakGB, $WorkingSetGB, $BornOffsetSec = 0) {
    return [ordered]@{
        processId = $ProcessId
        parentProcessId = $ParentProcessId
        creationDate = $born.AddSeconds($BornOffsetSec)
        privateKb = $PrivateGB * $gb
        peakPrivateKb = $PeakGB * $gb
        workingSetBytes = $WorkingSetGB * 1073741824.0
    }
}
function New-Sample($AtSec, $Processes, $Headroom = 20.0, $Available = 8.0) {
    return [ordered]@{ atSec = $AtSec; processes = $Processes
        commitHeadroomGB = $Headroom; availablePhysicalGB = $Available }
}
$launch = [pscustomobject]@{ commitHeadroomGB = 25.0; availablePhysicalGB = 9.0 }
$base = @{ RootProcessId = 100; RootPeakPrivateBytes = 3221225472; RootPeakWorkingSetBytes = 2147483648
    SamplesFailed = 0; LaunchReading = $launch; BootLaneReleasedAtSec = 42.5 }

# Tree walk: child and grandchild counted, an unrelated process is not.
$samples = @(
    (New-Sample 5 @((New-Record 100 4 1.0 1.5 0.8), (New-Record 200 100 0.5 0.9 0.4 1),
                    (New-Record 300 200 0.25 0.25 0.2 2), (New-Record 900 4 6.0 6.0 6.0)) 12.0 5.0),
    (New-Sample 10 @((New-Record 100 4 2.0 2.5 1.0), (New-Record 200 100 0.5 1.0 0.4 1)) 9.0 3.0)
)
$m = Get-ProcessTreeMemory @base -Samples $samples
Assert-Equal 100 $m.pid 'pid'
Assert-Equal 3.0 $m.rootPeakPrivateGB 'root peak private'
Assert-Equal 2.0 $m.rootPeakWorkingSetGB 'root peak working set'
Assert-Equal 2.5 $m.treePeakSampledPrivateGB 'sampled tree peak (lower bracket)'
Assert-Equal 1.4 $m.treePeakSampledWorkingSetGB 'sampled tree working set'
Assert-Equal 4.25 $m.treeSumOfPeaksPrivateGB 'summed kernel peaks (upper bracket)'
Assert-Equal 10 $m.treePeakAtSec 'seconds from launch to tree peak'
Assert-Equal 42.5 $m.bootLaneReleasedAtSec 'seconds from launch to boot-lane release'
Assert-Equal 25.0 $m.commitHeadroomAtLaunchGB 'headroom at launch'
Assert-Equal 9.0 $m.commitHeadroomMinGB 'minimum headroom'
Assert-Equal 9.0 $m.availablePhysicalAtLaunchGB 'available physical at launch'
Assert-Equal 3.0 $m.availablePhysicalMinGB 'minimum available physical'
Assert-Equal 2 $m.samples 'good samples'
Assert-Equal '' $m.unavailableReason 'no unavailable reason'

# A pid recycled onto a process older than its claimed parent is a different lineage.
$m = Get-ProcessTreeMemory @base -Samples @(
    (New-Sample 5 @((New-Record 100 4 1.0 1.0 1.0 10), (New-Record 200 100 4.0 4.0 4.0 -5))))
Assert-Equal 1.0 $m.treePeakSampledPrivateGB 'pid reuse excluded from the tree'
Assert-Equal 3.0 $m.treeSumOfPeaksPrivateGB 'pid reuse excluded from the peak sum'

# A process that only ever appeared in an early sample still counts toward the upper bracket.
$m = Get-ProcessTreeMemory @base -Samples @(
    (New-Sample 5 @((New-Record 100 4 1.0 1.0 1.0), (New-Record 200 100 1.0 2.0 1.0 1))),
    (New-Sample 10 @((New-Record 100 4 1.0 1.0 1.0))))
Assert-Equal 5.0 $m.treeSumOfPeaksPrivateGB 'departed child retained in the peak sum'

# A short run whose last seconds went unsampled: the root's tick-exact peak carries the bracket.
$shortRun = @{} + $base
$shortRun.RootPeakPrivateBytes = 2147483648
$m = Get-ProcessTreeMemory @shortRun -Samples @(
    (New-Sample 5 @((New-Record 100 4 0.5 0.6 0.4), (New-Record 200 100 0.5 0.5 0.4 1))))
Assert-Equal 2.0 $m.rootPeakPrivateGB 'root tick-exact peak'
Assert-Equal 2.5 $m.treeSumOfPeaksPrivateGB 'the upper bracket takes the root tick-exact peak over its sampled one'
if ($m.treeSumOfPeaksPrivateGB -lt $m.rootPeakPrivateGB) { throw 'Upper bracket fell below the root peak' }

# Failed and zero-sample runs: nulls with a reason, never a fabricated number.
$failedOnly = @{} + $base
$failedOnly.SamplesFailed = 3
$m = Get-ProcessTreeMemory @failedOnly -Samples @()
Assert-Equal $null $m.treePeakSampledPrivateGB 'no samples leaves the tree peak null'
Assert-Equal $null $m.treeSumOfPeaksPrivateGB 'no samples leaves the peak sum null'
Assert-Equal $null $m.commitHeadroomMinGB 'no samples leaves the headroom minimum null'
Assert-Equal 0 $m.samples 'no good samples'
Assert-Equal 3 $m.samplesFailed 'failed samples counted'
if (-not $m.unavailableReason) { throw 'Missing unavailable reason' }
Assert-Equal 3.0 $m.rootPeakPrivateGB 'root peak survives zero tree samples'

# A sample that caught no root process is not a tree sample.
$m = Get-ProcessTreeMemory @base -Samples @((New-Sample 5 @((New-Record 900 4 1.0 1.0 1.0))))
Assert-Equal 0 $m.samples 'sample without the root is discarded'

# Routed runs launch nothing; unreadable root peaks stay null rather than reading zero.
$m = Get-ProcessTreeMemory -RootProcessId 100 -RootPeakPrivateBytes $null -RootPeakWorkingSetBytes $null `
    -Samples @() -SamplesFailed 0 -LaunchReading $null -BootLaneReleasedAtSec $null
Assert-Equal $null $m.rootPeakPrivateGB 'unread root peak stays null'
Assert-Equal $null $m.commitHeadroomAtLaunchGB 'unread launch reading stays null'
Assert-Equal $null $m.bootLaneReleasedAtSec 'no boot lane held'

# The shared memory reading: KB in, GB out, and an unusable reading throws rather than admitting.
$reading = ConvertTo-MemoryReading -Source ([pscustomobject]@{ FreeVirtualMemory = '5242880'; FreePhysicalMemory = '2097152' })
Assert-Equal 5.0 $reading.commitHeadroomGB 'commit headroom parsed from KB'
Assert-Equal 2.0 $reading.availablePhysicalGB 'available physical parsed from KB'
foreach ($bad in @(([pscustomobject]@{ FreePhysicalMemory = '1' }), ([pscustomobject]@{ FreeVirtualMemory = 'x'; FreePhysicalMemory = '1' }), $null)) {
    $threw = $false
    try { [void](ConvertTo-MemoryReading -Source $bad) } catch { $threw = $true }
    if (-not $threw) { throw 'Unusable memory reading did not throw' }
}

Write-Host 'PASS: process-tree memory brackets, pid-reuse rejection, minima, missing samples and the shared memory reading'
