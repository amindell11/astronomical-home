# Test scope resolution functions
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Helper functions (minimal versions needed for testing)
function Resolve-FullPath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Load-ScopeMap {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        $scriptDir = Split-Path -Parent $PSCommandPath
        $Path = Join-Path $scriptDir "unity_test_scopes.json"
    }

    $fullPath = Resolve-FullPath $Path

    if (-not (Test-Path -LiteralPath $fullPath)) {
        Write-Warning "Scope map not found at: $fullPath"
        return $null
    }

    try {
        $raw = Get-Content -LiteralPath $fullPath -Raw
        $map = $raw | ConvertFrom-Json
        return $map
    }
    catch {
        Write-Warning "Failed to parse scope map at $fullPath : $_"
        return $null
    }
}

function Resolve-ScopeFilter {
    param(
        [object]$ScopeMap,
        [string]$ScopeType,
        [string]$ScopeName
    )

    if ($null -eq $ScopeMap) {
        return ""
    }

    $lowerType = $ScopeType.ToLower()
    $lowerName = $ScopeName.ToLower()

    # Special case: "Smoke" scope type
    if ($lowerType -eq "smoke") {
        if ($null -ne $ScopeMap.smoke -and $null -ne $ScopeMap.smoke.testFilter) {
            return [string]$ScopeMap.smoke.testFilter
        }
        return ""
    }

    # "Workspace" scope type (empty filter = all tests)
    if ($lowerType -eq "workspace") {
        if ($null -ne $ScopeMap.modules -and $null -ne $ScopeMap.modules.workspace -and $null -ne $ScopeMap.modules.workspace.testFilter) {
            return [string]$ScopeMap.modules.workspace.testFilter
        }
        return ""
    }

    # "Feature" scope type
    if ($lowerType -eq "feature") {
        if ([string]::IsNullOrWhiteSpace($lowerName)) {
            Write-Warning "ScopeType=Feature requires -ScopeName to be specified"
            return ""
        }

        if ($null -ne $ScopeMap.features) {
            $featuresObj = $ScopeMap.features
            $members = $featuresObj | Get-Member -MemberType NoteProperty | Where-Object { $_.Name -eq $lowerName }
            if ($members) {
                $entry = $featuresObj.$lowerName
                if ($null -ne $entry.testFilter) {
                    return [string]$entry.testFilter
                }
            }
        }

        Write-Warning "Feature '$lowerName' not found in scope map"
        return ""
    }

    # "Module" scope type
    if ($lowerType -eq "module") {
        if ([string]::IsNullOrWhiteSpace($lowerName)) {
            Write-Warning "ScopeType=Module requires -ScopeName to be specified"
            return ""
        }

        if ($null -ne $ScopeMap.modules) {
            $modulesObj = $ScopeMap.modules
            $members = $modulesObj | Get-Member -MemberType NoteProperty | Where-Object { $_.Name -eq $lowerName }
            if ($members) {
                $entry = $modulesObj.$lowerName
                if ($null -ne $entry.testFilter) {
                    return [string]$entry.testFilter
                }
            }
        }

        Write-Warning "Module '$lowerName' not found in scope map"
        return ""
    }

    return ""
}

# Test 1: Load scope map
Write-Host "Test 1: Loading scope map..."
$scopeMap = Load-ScopeMap -Path (Join-Path $PSScriptRoot "unity_test_scopes.json")
if ($null -eq $scopeMap) {
    Write-Host "  FAILED: Could not load scope map"
    exit 1
}
Write-Host "  PASSED: Scope map loaded"

# Test 2: Resolve Smoke scope
Write-Host ""
Write-Host "Test 2: Resolving Smoke scope..."
$filter = Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Smoke" -ScopeName ""
if ([string]::IsNullOrWhiteSpace($filter)) {
    Write-Host "  FAILED: Smoke scope returned empty filter"
    exit 1
}
Write-Host "  PASSED: Smoke filter = $filter"

# Test 3: Resolve Feature scope (camera)
Write-Host ""
Write-Host "Test 3: Resolving Feature/camera scope..."
$filter = Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Feature" -ScopeName "camera"
if ([string]::IsNullOrWhiteSpace($filter)) {
    Write-Host "  FAILED: Feature/camera scope returned empty filter"
    exit 1
}
Write-Host "  PASSED: Feature/camera filter = $filter"

# Test 4: Resolve Module scope (ai)
Write-Host ""
Write-Host "Test 4: Resolving Module/ai scope..."
$filter = Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Module" -ScopeName "ai"
if ([string]::IsNullOrWhiteSpace($filter)) {
    Write-Host "  FAILED: Module/ai scope returned empty filter"
    exit 1
}
Write-Host "  PASSED: Module/ai filter = $filter"

# Test 5: Resolve Workspace scope (empty filter expected)
Write-Host ""
Write-Host "Test 5: Resolving Workspace scope..."
$filter = Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Workspace" -ScopeName ""
Write-Host "  PASSED: Workspace filter = '$filter' (empty is expected)"

# Test 6: Invalid feature should warn
Write-Host ""
Write-Host "Test 6: Resolving invalid Feature scope..."
$filter = Resolve-ScopeFilter -ScopeMap $scopeMap -ScopeType "Feature" -ScopeName "nonexistent"
if (-not [string]::IsNullOrWhiteSpace($filter)) {
    Write-Host "  FAILED: Invalid feature returned non-empty filter"
    exit 1
}
Write-Host "  PASSED: Invalid feature returned empty filter (warning expected above)"

Write-Host ""
Write-Host "========================================"
Write-Host "All scope resolution tests PASSED"
Write-Host "========================================"
