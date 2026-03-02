#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates Unity test file/class naming conventions.

.DESCRIPTION
    Ensures all test files follow *Tests.cs naming and that the primary
    public class name matches the file name. Utility classes (like TestSceneBuilder)
    and files under PlayMode/Common are excluded from test-file naming checks.

.PARAMETER ProjectPath
    Path to Unity project root (default: src/Asteroids3D)

.PARAMETER Fix
    Show suggested fixes for violations (does not auto-fix)

.EXAMPLE
    .\scripts\check_test_naming.ps1
    .\scripts\check_test_naming.ps1 -Fix

.OUTPUTS
    Exit code 0: All tests pass conventions
    Exit code 1: One or more violations found
#>

param(
    [string]$ProjectPath = "src/Asteroids3D",
    [switch]$Fix
)

$ErrorActionPreference = "Stop"

# Resolve project path
$projectRoot = Resolve-Path $ProjectPath -ErrorAction Stop
$testsRoot = Join-Path $projectRoot "Assets\Scripts\Editor\Tests"

if (-not (Test-Path $testsRoot)) {
    Write-Error "Tests directory not found: $testsRoot"
    exit 2
}

Write-Host "Checking test naming conventions in: $testsRoot" -ForegroundColor Cyan
Write-Host ""

# Find all .cs files in Tests/
$testFiles = Get-ChildItem -Path $testsRoot -Filter "*.cs" -Recurse -File

# Utility classes that don't need *Tests naming
$utilityClasses = @(
    "TestSceneBuilder"
)

# Utility directories that are intentionally not test classes
$utilityDirPatterns = @(
    "*\Assets\Scripts\Editor\Tests\PlayMode\Common\*"
)

$violations = @()

foreach ($file in $testFiles) {
    $fileName = $file.BaseName
    $filePath = $file.FullName
    $relativePath = $filePath.Substring($projectRoot.Path.Length + 1)

    # Skip utility classes and utility directories (non-test helpers)
    $isUtilityDir = $false
    foreach ($pattern in $utilityDirPatterns) {
        if ($filePath -like $pattern) {
            $isUtilityDir = $true
            break
        }
    }

    if (($fileName -in $utilityClasses) -or $isUtilityDir) {
        Write-Host "  OK $relativePath (utility file, exempt)" -ForegroundColor DarkGray
        continue
    }

    # Rule 1: Test files should end with Tests
    if ($fileName -notmatch 'Tests$') {
        $violation = New-Object PSObject -Property @{
            Type = "FileNaming"
            File = $relativePath
            Issue = "File name does not end with Tests"
            Current = "$fileName.cs"
            Expected = "${fileName}Tests.cs"
        }
        $violations += $violation
        continue
    }

    # Rule 2: Primary public class name should match file name
    $content = Get-Content $filePath -Raw
    
    # Extract primary public class name (ignore nested/private classes)
    if ($content -match '(?m)^\s*public\s+class\s+(\w+)') {
        $className = $Matches[1]
        
        if ($className -ne $fileName) {
            $violation = New-Object PSObject -Property @{
                Type = "ClassNaming"
                File = $relativePath
                Issue = "Class name does not match file name"
                Current = $className
                Expected = $fileName
            }
            $violations += $violation
        } else {
            Write-Host "  OK $relativePath" -ForegroundColor Green
        }
    } else {
        # No public class found - might be an interface or all internal
        Write-Host "  WARN $relativePath (no public class found)" -ForegroundColor Yellow
    }
}

# Report results
Write-Host ""
if ($violations.Count -eq 0) {
    Write-Host "All test files follow naming conventions!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Found $($violations.Count) naming violation(s):" -ForegroundColor Red
    Write-Host ""
    
    foreach ($v in $violations) {
        Write-Host "  File: $($v.File)" -ForegroundColor Yellow
        Write-Host "  Issue: $($v.Issue)" -ForegroundColor Red
        Write-Host "  Current: $($v.Current)" -ForegroundColor DarkYellow
        Write-Host "  Expected: $($v.Expected)" -ForegroundColor Green
        
        if ($Fix) {
            if ($v.Type -eq "FileNaming") {
                $dirname = Split-Path $v.File -Parent
                Write-Host "  Suggested fix: git mv '$($v.File)' '$dirname/$($v.Expected)'" -ForegroundColor Cyan
            } else {
                Write-Host "  Suggested fix: Rename class '$($v.Current)' to '$($v.Expected)'" -ForegroundColor Cyan
            }
        }
        Write-Host ""
    }
    
    if (-not $Fix) {
        Write-Host "Run with -Fix flag to see suggested fixes." -ForegroundColor Cyan
    }
    
    exit 1
}
