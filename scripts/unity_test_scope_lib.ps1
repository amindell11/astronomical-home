Set-StrictMode -Version Latest

function Resolve-FullPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

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
        return $raw | ConvertFrom-Json
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

    if ($lowerType -eq "smoke") {
        if ($null -ne $ScopeMap.smoke -and $null -ne $ScopeMap.smoke.testFilter) {
            return [string]$ScopeMap.smoke.testFilter
        }
        return ""
    }

    if ($lowerType -eq "workspace") {
        if ($null -ne $ScopeMap.modules -and $null -ne $ScopeMap.modules.workspace -and $null -ne $ScopeMap.modules.workspace.testFilter) {
            return [string]$ScopeMap.modules.workspace.testFilter
        }
        return ""
    }

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

function ConvertTo-RepoSlashPath {
    param([string]$Path)

    $normalized = ($Path -replace '\\', '/').Trim()
    while ($normalized.StartsWith('./')) {
        $normalized = $normalized.Substring(2)
    }
    return $normalized.TrimStart('/')
}

function Test-AutoScopeIgnoredFile {
    param([string]$Path)

    # -like '*' spans '/', so 'doc/*' deliberately covers the whole doc/ tree; these paths cannot affect Unity test outcomes.
    return ($Path -like '*.md' -or $Path -like 'doc/*' -or $Path -like '.claude/*')
}

function Get-AutoChangedFiles {
    param([string]$RepoProbePath, [string]$BaseRef)

    $repoRoot = [string](& git -C $RepoProbePath rev-parse --show-toplevel | Select-Object -First 1)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
        throw "git rev-parse --show-toplevel failed under '$RepoProbePath'"
    }

    $mergeBase = [string](& git -C $repoRoot merge-base $BaseRef HEAD | Select-Object -First 1)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($mergeBase)) {
        throw "git merge-base $BaseRef HEAD failed"
    }

    # --no-renames: a rename must surface BOTH paths, or moving a file into a mapped dir could dodge the unmatched-file fallback.
    $diffFiles = @(& git -C $repoRoot diff --no-renames --name-only $mergeBase)
    if ($LASTEXITCODE -ne 0) {
        throw "git diff --no-renames --name-only $mergeBase failed"
    }

    $untrackedFiles = @(& git -C $repoRoot ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files --others failed"
    }

    return [pscustomobject]@{
        mergeBase = $mergeBase
        files = @(@($diffFiles) + @($untrackedFiles) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    }
}

function Get-ModulePathGlobs {
    param([object]$ScopeMap)

    $globsByModule = [ordered]@{}

    if ($null -eq $ScopeMap -or $null -eq $ScopeMap.PSObject.Properties['modules']) {
        return $globsByModule
    }

    foreach ($module in $ScopeMap.modules.PSObject.Properties) {
        $pathsProperty = $module.Value.PSObject.Properties['paths']
        if ($null -eq $pathsProperty) { continue }

        $globs = @($pathsProperty.Value | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($globs.Count -gt 0) {
            $globsByModule[$module.Name] = $globs
        }
    }

    return $globsByModule
}

function Add-UniqueFilterTerms {
    param([System.Collections.Generic.List[string]]$Terms, [string]$Filter)

    foreach ($term in ($Filter -split '\|')) {
        if (-not [string]::IsNullOrWhiteSpace($term) -and -not $Terms.Contains($term)) {
            $Terms.Add($term)
        }
    }
}

function Resolve-AutoSelection {
    param(
        [object]$ScopeMap,
        [string[]]$ChangedFiles
    )

    $selection = [ordered]@{
        mode = ""
        consideredFiles = @()
        ignoredFiles = @()
        matchedModules = @()
        unmatchedFiles = @()
        emptyFilterModules = @()
        testFilter = ""
    }

    $considered = @()
    $ignored = @()
    foreach ($file in @($ChangedFiles)) {
        if ([string]::IsNullOrWhiteSpace($file)) { continue }
        $path = ConvertTo-RepoSlashPath $file
        if (Test-AutoScopeIgnoredFile $path) {
            $ignored += $path
            continue
        }
        $considered += $path
    }
    $selection.consideredFiles = $considered
    $selection.ignoredFiles = $ignored

    if ($considered.Count -eq 0) {
        $selection.mode = "smoke"
        $selection.testFilter = Resolve-ScopeFilter -ScopeMap $ScopeMap -ScopeType "Smoke" -ScopeName ""
        return [pscustomobject]$selection
    }

    $globsByModule = Get-ModulePathGlobs -ScopeMap $ScopeMap
    $matchedModules = New-Object System.Collections.Generic.List[string]
    $unmatched = @()

    foreach ($path in $considered) {
        $modulesForFile = @()
        foreach ($moduleName in @($globsByModule.Keys)) {
            foreach ($glob in $globsByModule[$moduleName]) {
                if ($path -like $glob) {
                    $modulesForFile += $moduleName
                    break
                }
            }
        }

        if ($modulesForFile.Count -eq 0) {
            $unmatched += $path
            continue
        }

        foreach ($moduleName in $modulesForFile) {
            if (-not $matchedModules.Contains($moduleName)) {
                $matchedModules.Add($moduleName)
            }
        }
    }

    $selection.matchedModules = @($matchedModules)
    $selection.unmatchedFiles = $unmatched

    if ($unmatched.Count -gt 0) {
        $selection.mode = "fallback"
        $selection.testFilter = Resolve-ScopeFilter -ScopeMap $ScopeMap -ScopeType "Workspace" -ScopeName ""
        return [pscustomobject]$selection
    }

    $terms = New-Object System.Collections.Generic.List[string]
    $emptyFilterModules = @()
    foreach ($moduleName in $matchedModules) {
        $moduleFilter = Resolve-ScopeFilter -ScopeMap $ScopeMap -ScopeType "Module" -ScopeName $moduleName
        if ([string]::IsNullOrWhiteSpace($moduleFilter)) {
            $emptyFilterModules += $moduleName
            continue
        }
        Add-UniqueFilterTerms -Terms $terms -Filter $moduleFilter
    }

    if ($emptyFilterModules.Count -gt 0) {
        # A matched module with no filter cannot be scope-tested; never under-test.
        $selection.mode = "fallback"
        $selection.emptyFilterModules = $emptyFilterModules
        $selection.testFilter = Resolve-ScopeFilter -ScopeMap $ScopeMap -ScopeType "Workspace" -ScopeName ""
        return [pscustomobject]$selection
    }

    Add-UniqueFilterTerms -Terms $terms -Filter (Resolve-ScopeFilter -ScopeMap $ScopeMap -ScopeType "Smoke" -ScopeName "")

    $selection.mode = "modules"
    $selection.testFilter = ($terms -join '|')
    return [pscustomobject]$selection
}
