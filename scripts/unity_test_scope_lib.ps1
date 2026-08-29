Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "lib/repo_root.ps1")

# Speed/selector overlays, never domains: they must not seed a scope, or a Slow/graphics tag would pull every such test across domains.
$Script:AutoScopeOverlayCategories = @('Smoke', 'Slow', 'RequiresGraphics', 'ChaseBenchmark')

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

    return ""
}

# The scope map authors one thing - a '|'-alternation of literal fixture-name substrings - and the two
# transports need different readings of it: cold Unity takes the alternation verbatim as a -testFilter
# regex, while the routed pipeline's run_tests takes ONE literal substring per call. This is the single
# place that reads the authored format, so neither transport re-parses it.
#   kind          "none" (no name selection) or "names"
#   names         the literal substrings, one per routed call
#   testFilter    the alternation, for the cold -testFilter argument
#   representable $false when the filter is a regex the routed transport cannot reproduce exactly;
#                 routed must REFUSE such a selection, never approximate it (reason says why).
function ConvertTo-TestNameSelection {
    param([string]$TestFilter)

    if ([string]::IsNullOrWhiteSpace($TestFilter)) {
        return [pscustomobject]@{ kind = "none"; names = @(); testFilter = ""; representable = $true; reason = "" }
    }

    $names = @($TestFilter -split '\|' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
    # Anything beyond '|' alternation of literals is regex the pipeline's substring filter cannot honor.
    $regexMetacharacters = @($names | Where-Object { $_ -match '[\\\[\](){}.*+?^$]' })
    if ($regexMetacharacters.Count -gt 0) {
        return [pscustomobject]@{
            kind = "names"
            names = $names
            testFilter = $TestFilter
            representable = $false
            reason = "regex construct(s) beyond '|'-alternation of literal names: $($regexMetacharacters -join ', ')"
        }
    }

    return [pscustomobject]@{ kind = "names"; names = $names; testFilter = ($names -join '|'); representable = $true; reason = "" }
}

function Resolve-ScopeSelection {
    param([object]$ScopeMap, [string]$ScopeType, [string]$ScopeName)

    return ConvertTo-TestNameSelection -TestFilter (Resolve-ScopeFilter -ScopeMap $ScopeMap -ScopeType $ScopeType -ScopeName $ScopeName)
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
    return ($Path -like '*.md' -or $Path -like 'doc/*' -or $Path -like '.claude/*' -or $Path -like '*.gitignore')
}

function Get-AutoChangedFiles {
    param([string]$RepoProbePath, [string]$BaseRef)

    $repoRoot = Get-RepoRoot -ProbePath $RepoProbePath

    $mergeBaseLines = @(& git -C $repoRoot merge-base $BaseRef HEAD)
    $mergeBase = [string]$mergeBaseLines[0]
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
        repoRoot = $repoRoot
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

function Get-CategoriesFromContent {
    param([string]$Content)

    $cats = New-Object System.Collections.Generic.List[string]
    if ([string]::IsNullOrWhiteSpace($Content)) { return @($cats) }

    foreach ($match in [regex]::Matches($Content, '\[\s*Category\s*\(\s*"([^"]+)"\s*\)\s*\]')) {
        $cat = $match.Groups[1].Value
        if (-not [string]::IsNullOrWhiteSpace($cat) -and -not $cats.Contains($cat)) {
            $cats.Add($cat)
        }
    }

    return @($cats)
}

function Get-TestFileCategoryIndex {
    param([string]$TestsRoot, [string]$RepoRoot)

    $index = [ordered]@{}
    if ([string]::IsNullOrWhiteSpace($TestsRoot) -or -not (Test-Path -LiteralPath $TestsRoot)) {
        return $index
    }

    $repoPrefix = ""
    if (-not [string]::IsNullOrWhiteSpace($RepoRoot)) {
        $repoPrefix = (ConvertTo-RepoSlashPath (Resolve-FullPath $RepoRoot)).TrimEnd('/') + '/'
    }

    foreach ($file in @(Get-ChildItem -LiteralPath $TestsRoot -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue)) {
        $cats = Get-CategoriesFromContent -Content (Get-Content -LiteralPath $file.FullName -Raw)
        if (@($cats).Count -eq 0) { continue }

        $rel = ConvertTo-RepoSlashPath $file.FullName
        # PS 5.1 has no Path.GetRelativePath; the tests tree always sits under the repo, so a prefix strip is exact.
        if ($repoPrefix -ne "" -and $rel.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $rel = $rel.Substring($repoPrefix.Length)
        }
        $index[$rel] = @($cats)
    }

    return $index
}

function Add-UniqueDomainCategories {
    param([System.Collections.Generic.List[string]]$Domains, [string[]]$Categories)

    foreach ($cat in @($Categories)) {
        if ([string]::IsNullOrWhiteSpace($cat)) { continue }
        if ($Script:AutoScopeOverlayCategories -contains $cat) { continue }
        if (-not $Domains.Contains($cat)) { $Domains.Add($cat) }
    }
}

function Test-PathMatchesGlobs {
    param([string]$Path, [string[]]$Globs)

    foreach ($glob in @($Globs)) {
        if ($Path -like $glob) { return $true }
    }
    return $false
}

function Get-ModuleDerivedCategories {
    param([object]$ScopeMap, [string]$ModuleName, [System.Collections.IDictionary]$FileCategoryIndex)

    $globsByModule = Get-ModulePathGlobs -ScopeMap $ScopeMap
    if (-not $globsByModule.Contains($ModuleName)) { return @() }
    $globs = @($globsByModule[$ModuleName])
    if ($globs.Count -eq 0 -or $null -eq $FileCategoryIndex) { return @() }

    $domains = New-Object System.Collections.Generic.List[string]
    foreach ($file in @($FileCategoryIndex.Keys)) {
        if (-not (Test-PathMatchesGlobs -Path $file -Globs $globs)) { continue }
        Add-UniqueDomainCategories -Domains $domains -Categories $FileCategoryIndex[$file]
    }

    return @($domains)
}

function Resolve-AutoSelection {
    param(
        [object]$ScopeMap,
        [string[]]$ChangedFiles,
        [System.Collections.IDictionary]$FileCategoryIndex
    )

    $selection = [ordered]@{
        mode = ""
        consideredFiles = @()
        ignoredFiles = @()
        matchedModules = @()
        unmatchedFiles = @()
        emptyCategoryModules = @()
        categories = @()
        testCategory = ""
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
        $selection.categories = @("Smoke")
        $selection.testCategory = "Smoke"
        return [pscustomobject]$selection
    }

    $globsByModule = Get-ModulePathGlobs -ScopeMap $ScopeMap
    $matchedModules = New-Object System.Collections.Generic.List[string]
    $unmatched = @()

    foreach ($path in $considered) {
        $modulesForFile = @()
        foreach ($moduleName in @($globsByModule.Keys)) {
            if (Test-PathMatchesGlobs -Path $path -Globs $globsByModule[$moduleName]) {
                $modulesForFile += $moduleName
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
        return [pscustomobject]$selection
    }

    $domains = New-Object System.Collections.Generic.List[string]
    $emptyCategoryModules = @()
    foreach ($moduleName in $matchedModules) {
        $moduleCategories = Get-ModuleDerivedCategories -ScopeMap $ScopeMap -ModuleName $moduleName -FileCategoryIndex $FileCategoryIndex
        if (@($moduleCategories).Count -eq 0) {
            $emptyCategoryModules += $moduleName
            continue
        }
        Add-UniqueDomainCategories -Domains $domains -Categories $moduleCategories
    }

    if ($emptyCategoryModules.Count -gt 0) {
        # A matched module whose paths cover no tagged fixture cannot be scope-tested; never under-test.
        $selection.mode = "fallback"
        $selection.emptyCategoryModules = $emptyCategoryModules
        return [pscustomobject]$selection
    }

    if (-not $domains.Contains("Smoke")) { $domains.Add("Smoke") }
    $sorted = @($domains | Sort-Object)

    $selection.mode = "modules"
    $selection.categories = $sorted
    $selection.testCategory = ($sorted -join ';')
    return [pscustomobject]$selection
}
