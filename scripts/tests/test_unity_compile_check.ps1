# covers: scripts/unity_compile_check.ps1
# A fake Unity project with old-style project files compiles under the real dotnet build. Save-Snapshot
# stands in for the Unity boot. The check compiles the .cs files on disk (a nested asmdef's folder stays
# out of its parent; the generated Compile list is ignored), reports errors in Unity's form, writes nothing
# into the project, and calls the snapshot stale on a changed asmdef, a new asmdef or a gone reference.
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$checkScript = Join-Path $PSScriptRoot "..\unity_compile_check.ps1"
. $checkScript -ProjectPath unused -StateDir unused

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "FAIL: $Message" }
}

function Write-Text {
    param([string]$Path, [string]$Text)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

function Get-NetStandardReference {
    $sdks = @(& dotnet --list-sdks)
    if ($LASTEXITCODE -ne 0 -or $sdks.Count -eq 0) { throw "this test needs the .NET SDK (dotnet --list-sdks)" }
    $sdkRoot = ($sdks[-1] -replace '^.*\[(.*)\]\s*$', '$1')
    $reference = @(Get-ChildItem -Path (Join-Path (Split-Path -Parent $sdkRoot) "packs\NETStandard.Library.Ref\*\ref\netstandard2.1\netstandard.dll") -ErrorAction SilentlyContinue)
    if ($reference.Count -eq 0) { throw "this test needs the SDK's NETStandard.Library.Ref pack beside $sdkRoot" }
    return $reference[-1].FullName
}

# The Rider package's old-style shape: framework probing stubbed out, the standard library only by HintPath.
function Write-OldStyleProject {
    param([string]$Path, [string]$Name, [string]$Defines, [string[]]$HintPaths, [string[]]$ProjectReferences)
    $references = ($HintPaths | ForEach-Object { "    <Reference Include=`"$([System.IO.Path]::GetFileNameWithoutExtension($_))`"><HintPath>$_</HintPath></Reference>" }) -join "`n"
    $projects = ($ProjectReferences | Where-Object { $_ } | ForEach-Object { "    <ProjectReference Include=`"$_.csproj`"><Name>$_</Name></ProjectReference>" }) -join "`n"
    Write-Text $Path @"
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <LangVersion>9.0</LangVersion>
    <_TargetFrameworkDirectories>non_empty_path_generated_by_unity.rider.package</_TargetFrameworkDirectories>
    <_FullFrameworkReferenceAssemblyPaths>non_empty_path_generated_by_unity.rider.package</_FullFrameworkReferenceAssemblyPaths>
    <DisableHandlePackageFileConflicts>true</DisableHandlePackageFileConflicts>
    <Configuration Condition=" '`$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '`$(Platform)' == '' ">AnyCPU</Platform>
    <OutputType>Library</OutputType>
    <AssemblyName>$Name</AssemblyName>
    <TargetFrameworkVersion>v4.7.1</TargetFrameworkVersion>
    <OutputPath>Temp\Bin\Debug\$Name\</OutputPath>
    <DefineConstants>$Defines</DefineConstants>
    <NoConfig>true</NoConfig>
    <NoStdLib>true</NoStdLib>
    <AddAdditionalExplicitAssemblyReferences>false</AddAdditionalExplicitAssemblyReferences>
    <ImplicitlyExpandNETStandardFacades>false</ImplicitlyExpandNETStandardFacades>
    <ImplicitlyExpandDesignTimeFacades>false</ImplicitlyExpandDesignTimeFacades>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Assets\Scripts\GeneratedButDeleted.cs" />
  </ItemGroup>
  <ItemGroup>
$references
  </ItemGroup>
  <ItemGroup>
$projects
  </ItemGroup>
  <Import Project="`$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
"@
}

function Invoke-CheckScript {
    param([string]$ProjectPath, [string]$StateDir)
    $out = Join-Path $StateDir "..\stdout.txt"
    $err = Join-Path $StateDir "..\stderr.txt"
    $process = Start-Process powershell -Wait -PassThru -NoNewWindow -RedirectStandardOutput $out -RedirectStandardError $err -ArgumentList @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$checkScript`"", "-ProjectPath", "`"$ProjectPath`"", "-StateDir", "`"$StateDir`"")
    return [pscustomobject]@{
        exitCode = $process.ExitCode
        stdout = ([System.IO.File]::ReadAllText($out)).Trim()
        stderr = [System.IO.File]::ReadAllText($err)
    }
}

function Get-TreeListing {
    param([string]$Path)
    return (@(Get-ChildItem -LiteralPath $Path -Recurse -File | ForEach-Object { "$($_.FullName)|$($_.Length)|$($_.LastWriteTimeUtc.Ticks)" }) -join "`n")
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("compile-check-" + [guid]::NewGuid().ToString("N"))
$project = Join-Path $temp "Project"
$state = Join-Path $temp "state"
try {
    $netstandard = Get-NetStandardReference
    Write-Text (Join-Path $project "Assets\Scripts\Main.asmdef") '{ "name": "Main", "references": [ "Lib" ] }'
    Write-Text (Join-Path $project "Assets\Scripts\Lib\Lib.asmdef") '{ "name": "Lib" }'
    Write-Text (Join-Path $project "Assets\Scripts\Main.cs") "namespace Game { public static class Entry { public static string Run() => Lib.Greeter.Hi(); } }`n"
    Write-Text (Join-Path $project "Assets\Scripts\Lib\Greeter.cs") "namespace Lib { public static class Greeter { public static string Hi() => `"hi`"; } }`n"
    # Compiled into Main as well, this file would hit the #error: the nested-folder exclusion is what keeps it out.
    Write-Text (Join-Path $project "Assets\Scripts\Lib\OnlyInLib.cs") "#if !LIB_ONLY`n#error compiled outside Lib`n#endif`nnamespace Lib { internal static class OnlyInLib { } }`n"
    foreach ($file in $Script:FingerprintFiles) { Write-Text (Join-Path $project $file) "$file`n" }
    Write-OldStyleProject (Join-Path $project "Main.csproj") "Main" "DEBUG" @($netstandard) @("Lib")
    Write-OldStyleProject (Join-Path $project "Lib.csproj") "Lib" "DEBUG;LIB_ONLY" @($netstandard) @()
    $project = [System.IO.Path]::GetFullPath($project)

    $none = Invoke-CheckScript $project $state
    Assert-True ($none.exitCode -eq 3 -and $none.stdout -eq "stale") "no snapshot is stale, exit 3 (got $($none.exitCode) '$($none.stdout)')"
    Assert-True ($none.stderr -match "no project-file snapshot yet") "the stale summary says why (got: $($none.stderr))"

    $asmdefs = @(Get-Asmdefs $project)
    New-Item -ItemType Directory -Force -Path $state | Out-Null
    Save-Snapshot $project $state $asmdefs @(Get-InputFingerprint $project $asmdefs)
    $projectBefore = Get-TreeListing $project

    $pass = Invoke-CheckScript $project $state
    Assert-True ($pass.exitCode -eq 0 -and $pass.stdout -eq "pass") "the fresh snapshot compiles: exit 0 (got $($pass.exitCode) '$($pass.stdout)'; $($pass.stderr))"
    Assert-True (Test-Path -LiteralPath (Join-Path $state "bin\Main.dll")) "build output lands under the state dir"

    $broken = Join-Path $project "Assets\Scripts\Broken.cs"
    Write-Text $broken "namespace Game`n{`n    public static class Broken { public static int Value => missingThing; }`n}`n"
    $fail = Invoke-CheckScript $project $state
    Assert-True ($fail.exitCode -eq 1 -and $fail.stdout -eq "fail") "a new file with an error fails, exit 1 (got $($fail.exitCode) '$($fail.stdout)'; $($fail.stderr))"
    $errorLines = @($fail.stderr -split "`r?`n" | Where-Object { $_ -match 'error CS' })
    Assert-True ($errorLines.Count -eq 1) "each error prints once (got: $($errorLines -join ' | '))"
    Assert-True ($errorLines[0] -match '^Assets/Scripts/Broken\.cs\(3,\d+\): error CS0103: ') "errors print in Unity's form, project-relative (got: $($errorLines[0]))"

    Remove-Item -LiteralPath $broken
    $result = Invoke-CompileCheck $project $state
    Assert-True ($result.verdict -eq "pass") "a deleted file drops out of the build (got $($result.verdict): $($result.lines -join ' | '))"
    Assert-True ((Get-TreeListing $project) -eq $projectBefore) "the check writes nothing into the project"

    $mainAsmdef = Join-Path $project "Assets\Scripts\Main.asmdef"
    Write-Text $mainAsmdef '{ "name": "Main", "references": [ "Lib" ], "autoReferenced": false }'
    $result = Invoke-CompileCheck $project $state
    Assert-True ($result.verdict -eq "stale" -and ($result.lines -join "`n") -match "Assets/Scripts/Main\.asmdef") "an edited asmdef makes the snapshot stale and is named (got $($result.verdict): $($result.lines -join ' | '))"
    Write-Text $mainAsmdef '{ "name": "Main", "references": [ "Lib" ] }'

    $extra = Join-Path $project "Assets\Scripts\Extra\Extra.asmdef"
    Write-Text $extra '{ "name": "Extra" }'
    $result = Invoke-CompileCheck $project $state
    Assert-True ($result.verdict -eq "stale" -and ($result.lines -join "`n") -match "Assets/Scripts/Extra/Extra\.asmdef") "a new asmdef makes the snapshot stale and is named (got $($result.verdict))"
    $refused = $null
    try { Save-Snapshot $project $state @(Get-Asmdefs $project) @() } catch { $refused = $_.Exception.Message }
    Assert-True ($refused -match "no Extra\.csproj was generated") "a refresh refuses an asmdef with no generated project (got: $refused)"
    Remove-Item -LiteralPath (Split-Path -Parent $extra) -Recurse

    $snapshotMain = Join-Path $state "snapshot\Main.csproj"
    $gone = Join-Path $temp "Library\ScriptAssemblies\Gone.dll"
    $original = [System.IO.File]::ReadAllText($snapshotMain)
    Write-Text $snapshotMain ($original.Replace("<HintPath>$netstandard</HintPath>", "<HintPath>$netstandard</HintPath></Reference><Reference Include=`"Gone`"><HintPath>$gone</HintPath>"))
    $result = Invoke-CompileCheck $project $state
    Assert-True ($result.verdict -eq "stale" -and ($result.lines -join "`n").Contains($gone)) "a referenced file that is gone makes the snapshot stale and is named (got $($result.verdict): $($result.lines -join ' | '))"
    Write-Text $snapshotMain $original

    Write-Text (Join-Path $project "Lib.csproj") "<Project>`n  <Import Project=`"Sdk.props`" Sdk=`"Microsoft.NET.Sdk`" />`n</Project>`n"
    $refused = $null
    try { Save-Snapshot $project $state @(Get-Asmdefs $project) @() } catch { $refused = $_.Exception.Message }
    Assert-True ($refused -match "SDK-style") "a refresh refuses SDK-style project files (got: $refused)"
    Assert-True ((Invoke-CompileCheck $project $state).verdict -eq "pass") "a refused refresh leaves the previous snapshot in place"

    Write-Host "PASS: Unity compile check"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
