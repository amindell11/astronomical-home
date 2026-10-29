<#
.SYNOPSIS
    Compile every Unity assembly in a project from the .cs files on disk with dotnet build,
    without booting Unity. Front door: agent_worktree_pool.sh compile-check <slot>.

.DESCRIPTION
    unity_compile_check.ps1 -ProjectPath <unity-project> -StateDir <dir> [-Refresh]

    Builds a snapshot of the project's old-style (Rider package) .csproj files, kept under
    <StateDir>\snapshot, after swapping each project's Compile items for a glob of its asmdef
    folder minus nested asmdef folders, so new and deleted files count. Build outputs go under
    <StateDir>. Only -Refresh writes under -ProjectPath.

    -Refresh first regenerates the project files with one coordinated Unity batch boot
    (lib/unity_solution_sync.ps1, which refuses a worktree with tracked changes), then copies
    each asmdef's .csproj into the snapshot beside a fingerprint of the inputs that shape them:
    every asmdef, Packages/manifest.json, Packages/packages-lock.json,
    ProjectSettings/ProjectVersion.txt and ProjectSettings/ProjectSettings.asset.

    Verdict: one word on stdout, mirrored by the exit code; stderr ends in one summary line.
      pass   0  every assembly compiled
      fail   1  compile errors, each on stderr in Unity's own form:
                Assets/<path>(<line>,<col>): error CS<n>: <message>
      error  2  anything else: a failed refresh, no dotnet, a build failure with no CS error
      stale  3  no snapshot, a changed fingerprint input, or a referenced file that is gone
                (a wiped Library); stderr names it, and -Refresh rebuilds the snapshot
#>
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$StateDir,
    [switch]$Refresh,
    # Empty resolves from the project's own ProjectVersion.txt (scripts/lib/unity_editor.ps1).
    [string]$UnityPath = "",
    [int]$UnityAccessWaitSec = 900,
    [int]$UnitySyncTimeoutSec = 600
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity_access_client.ps1")
. (Join-Path $PSScriptRoot "lib/repo_root.ps1")
. (Join-Path $PSScriptRoot "lib/unity_editor.ps1")
. (Join-Path $PSScriptRoot "lib/unity_solution_sync.ps1")

$Script:FingerprintFiles = @(
    "Packages/manifest.json",
    "Packages/packages-lock.json",
    "ProjectSettings/ProjectVersion.txt",
    "ProjectSettings/ProjectSettings.asset"
)
$Script:ExitCodes = @{ pass = 0; fail = 1; error = 2; stale = 3 }

function Get-Asmdefs {
    param([string]$ProjectPath)
    $assets = Join-Path $ProjectPath "Assets"
    foreach ($file in [System.IO.Directory]::EnumerateFiles($assets, "*.asmdef", [System.IO.SearchOption]::AllDirectories)) {
        $name = [string](Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json).name
        if ([string]::IsNullOrWhiteSpace($name)) { throw "asmdef has no name: $file" }
        [pscustomobject]@{ name = $name; dir = [System.IO.Path]::GetDirectoryName($file); path = $file }
    }
}

function Get-InputFingerprint {
    param([string]$ProjectPath, [object[]]$Asmdefs)
    $relative = @($Asmdefs | ForEach-Object { $_.path.Substring($ProjectPath.Length + 1).Replace('\', '/') }) + $Script:FingerprintFiles
    [Array]::Sort($relative, [StringComparer]::Ordinal)
    foreach ($path in $relative) {
        $full = Join-Path $ProjectPath $path
        $hash = if (Test-Path -LiteralPath $full -PathType Leaf) { (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash } else { "missing" }
        "$hash $path"
    }
}

function Get-ChangedInputs {
    param([string[]]$Recorded, [string[]]$Current)
    $old = @{}
    foreach ($line in $Recorded) { $hash, $path = $line -split ' ', 2; $old[$path] = $hash }
    $new = @{}
    foreach ($line in $Current) { $hash, $path = $line -split ' ', 2; $new[$path] = $hash }
    foreach ($path in @($old.Keys) + @($new.Keys) | Sort-Object -Unique) {
        if ($old[$path] -ne $new[$path]) { $path }
    }
}

function Get-MissingReferences {
    param([string]$SnapshotDir)
    $namespace = @{ m = "http://schemas.microsoft.com/developer/msbuild/2003" }
    $seen = @{}
    foreach ($project in Get-ChildItem -LiteralPath $SnapshotDir -Filter "*.csproj") {
        $xml = [xml](Get-Content -LiteralPath $project.FullName -Raw -Encoding UTF8)
        $paths = @(Select-Xml -Xml $xml -XPath "//m:HintPath" -Namespace $namespace | ForEach-Object { $_.Node.InnerText }) +
            @(Select-Xml -Xml $xml -XPath "//m:Analyzer/@Include" -Namespace $namespace | ForEach-Object { $_.Node.Value })
        foreach ($path in $paths) {
            if ($seen.ContainsKey($path)) { continue }
            $seen[$path] = $true
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $path }
        }
    }
}

function Save-Snapshot {
    param([string]$ProjectPath, [string]$StateDir, [object[]]$Asmdefs, [string[]]$Fingerprint)
    $staging = Join-Path $StateDir "snapshot.new"
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    foreach ($asmdef in $Asmdefs) {
        $source = Join-Path $ProjectPath "$($asmdef.name).csproj"
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "no $($asmdef.name).csproj was generated for $($asmdef.path)" }
        if ((Get-Content -LiteralPath $source -Raw -Encoding UTF8) -match '\bSdk="') { throw "$source is SDK-style; the snapshot takes only the Rider package's old-style projects" }
        Copy-Item -LiteralPath $source -Destination $staging
    }
    [System.IO.File]::WriteAllLines((Join-Path $staging "inputs.txt"), [string[]]$Fingerprint)
    $snapshot = Join-Path $StateDir "snapshot"
    if (Test-Path -LiteralPath $snapshot) { Remove-Item -LiteralPath $snapshot -Recurse -Force }
    Rename-Item -LiteralPath $staging -NewName "snapshot"
}

function Invoke-Refresh {
    param([string]$ProjectPath, [string]$StateDir, [string]$UnityPath, [int]$WaitSeconds, [int]$TimeoutSeconds)
    $asmdefs = @(Get-Asmdefs $ProjectPath)
    # Taken before the boot: an input edited mid-boot then reads as stale, never as fresh.
    $fingerprint = @(Get-InputFingerprint $ProjectPath $asmdefs)
    if ([string]::IsNullOrWhiteSpace($UnityPath)) { $UnityPath = Resolve-UnityEditorPath -ProjectPath $ProjectPath }
    New-Item -ItemType Directory -Force -Path $StateDir | Out-Null
    Sync-UnitySolution (Get-RepoRoot -ProbePath $ProjectPath) $ProjectPath $StateDir $UnityPath $WaitSeconds $TimeoutSeconds
    Save-Snapshot $ProjectPath $StateDir $asmdefs $fingerprint
}

# MSBuild counts imported project files as compile inputs, so rewriting identical content recompiles everything.
function Write-FileIfChanged {
    param([string]$Path, [string]$Content)
    if ((Test-Path -LiteralPath $Path -PathType Leaf) -and [System.IO.File]::ReadAllText($Path) -ceq $Content) { return }
    [System.IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
}

function Write-BuildFiles {
    param([string]$StateDir, [string]$SnapshotDir, [object[]]$Asmdefs)
    $groups = foreach ($asmdef in $Asmdefs) {
        $nested = @($Asmdefs | Where-Object { $_.dir.StartsWith($asmdef.dir + '\', [StringComparison]::OrdinalIgnoreCase) } | ForEach-Object { "$($_.dir)\**" })
        $include = [System.Security.SecurityElement]::Escape("$($asmdef.dir)\**\*.cs")
        $exclude = [System.Security.SecurityElement]::Escape($nested -join ';')
        "  <ItemGroup Condition=`"'`$(MSBuildProjectName)' == '$($asmdef.name)'`">`n" +
        "    <Compile Remove=`"@(Compile)`" />`n" +
        "    <Compile Include=`"$include`" Exclude=`"$exclude`" />`n" +
        "  </ItemGroup>"
    }
    $targets = Join-Path $StateDir "compile-check.targets"
    Write-FileIfChanged $targets "<Project>`n$($groups -join "`n")`n</Project>`n"

    $projects = [System.Security.SecurityElement]::Escape(($Asmdefs | ForEach-Object { Join-Path $SnapshotDir "$($_.name).csproj" }) -join ';')
    $properties = [System.Security.SecurityElement]::Escape("CustomAfterMicrosoftCommonTargets=$targets;OutputPath=$StateDir\bin\;BaseIntermediateOutputPath=$StateDir\obj\;IntermediateOutputPath=$StateDir\obj\")
    $proj = Join-Path $StateDir "compile-check.proj"
    Write-FileIfChanged $proj @"
<Project DefaultTargets="Build">
  <Target Name="Build">
    <MSBuild Projects="$projects" Properties="$properties" BuildInParallel="true" />
  </Target>
</Project>

"@
    return $proj
}

function ConvertTo-UnityErrorLines {
    param([string[]]$BuildOutput, [string]$ProjectPath)
    $pattern = '^\s*(?<file>.+?)\((?<line>\d+),(?<col>\d+)\): error (?<code>CS\d+): (?<message>.*?)(?: \[[^\]]+\])?\s*$'
    $prefix = $ProjectPath + '\'
    foreach ($line in $BuildOutput) {
        if ($line -notmatch $pattern) { continue }
        $file = $Matches.file
        if ($file.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { $file = $file.Substring($prefix.Length).Replace('\', '/') }
        "$file($($Matches.line),$($Matches.col)): error $($Matches.code): $($Matches.message)"
    }
}

function Invoke-CompileCheck {
    param([string]$ProjectPath, [string]$StateDir)
    $snapshot = Join-Path $StateDir "snapshot"
    $recorded = Join-Path $snapshot "inputs.txt"
    if (-not (Test-Path -LiteralPath $recorded -PathType Leaf)) {
        return [pscustomobject]@{ verdict = "stale"; lines = @(); summary = "no project-file snapshot yet" }
    }
    $asmdefs = @(Get-Asmdefs $ProjectPath)
    $changed = @(Get-ChangedInputs @(Get-Content -LiteralPath $recorded) @(Get-InputFingerprint $ProjectPath $asmdefs))
    if ($changed.Count -gt 0) {
        return [pscustomobject]@{ verdict = "stale"; lines = @($changed | ForEach-Object { "changed since the snapshot: $_" }); summary = "project-file inputs changed since the snapshot" }
    }
    $missing = @(Get-MissingReferences $snapshot)
    if ($missing.Count -gt 0) {
        return [pscustomobject]@{ verdict = "stale"; lines = @($missing | ForEach-Object { "referenced file is gone: $_" }); summary = "files the snapshot references are gone" }
    }

    $proj = Write-BuildFiles $StateDir $snapshot $asmdefs
    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = @(& dotnet build $proj --no-restore -nologo -v:q -tl:off "-clp:ErrorsOnly;NoSummary" -m 2>&1 | ForEach-Object { [string]$_ })
        $exitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousEap }

    $errors = @(ConvertTo-UnityErrorLines $output $ProjectPath)
    if ($exitCode -eq 0) {
        return [pscustomobject]@{ verdict = "pass"; lines = @(); summary = "$($asmdefs.Count) assemblies compiled" }
    }
    if ($errors.Count -gt 0) {
        return [pscustomobject]@{ verdict = "fail"; lines = $errors; summary = "$($errors.Count) compile error(s)" }
    }
    return [pscustomobject]@{ verdict = "error"; lines = @($output | Select-Object -Last 20); summary = "dotnet build exited $exitCode with no CS error" }
}

if ($MyInvocation.InvocationName -eq '.') { return }

$clock = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd('\')
    $StateDir = [System.IO.Path]::GetFullPath($StateDir).TrimEnd('\')
    if ($Refresh.IsPresent) { Invoke-Refresh $ProjectPath $StateDir $UnityPath $UnityAccessWaitSec $UnitySyncTimeoutSec }
    $result = Invoke-CompileCheck $ProjectPath $StateDir
}
catch {
    $result = [pscustomobject]@{ verdict = "error"; lines = @(); summary = $_.Exception.Message }
}
foreach ($line in $result.lines) { [Console]::Error.WriteLine($line) }
$hint = if ($result.verdict -eq "stale") { "; rebuild it with compile-check <slot> --refresh" } else { "" }
[Console]::Error.WriteLine("compile check $($result.verdict): $($result.summary)$hint ($([int]$clock.Elapsed.TotalSeconds)s)")
[Console]::Out.WriteLine($result.verdict)
exit $Script:ExitCodes[$result.verdict]
