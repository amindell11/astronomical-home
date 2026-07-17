param(
    [Parameter(Mandatory=$true)][string]$OldPath,
    [Parameter(Mandatory=$true)][string]$NewPath
)

# Verdict for the merge gate's inert-diff fast path: exit 0 = full-line-comment/blank-line-only delta, 1 = code change, 2 = doubt (callers treat as code). Conservative line heuristic: constructs that can span lines or hide code shape (verbatim/raw strings, block comments) are doubt; false negatives merely cost a test run.
$ErrorActionPreference = "Stop"

function Get-SignificantLines {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw ("missing file: " + $Path) }
    $text = Get-Content -LiteralPath $Path -Raw
    if ($null -eq $text) { $text = "" }
    foreach ($marker in @('@"', '$@"', '@$"', '"""', '/*')) {
        if ($text.Contains($marker)) { throw ("unsupported construct: " + $marker) }
    }
    $lines = $text -split "`r?`n" | ForEach-Object { $_.Trim() } |
        Where-Object { $_ -ne "" -and -not $_.StartsWith("//") }
    return @($lines) -join "`n"
}

try {
    $old = Get-SignificantLines $OldPath
    $new = Get-SignificantLines $NewPath
}
catch {
    Write-Output ("doubt: " + $_.Exception.Message)
    exit 2
}

if ($old -ceq $new) { Write-Output "inert"; exit 0 }
Write-Output "different"
exit 1
