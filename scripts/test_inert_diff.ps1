$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $PSCommandPath
$inertDiff = Join-Path $scriptDir "inert_diff.ps1"
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("inert-diff-tests-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
$script:failures = 0
$script:caseIndex = 0

function Assert-Verdict {
    param([string]$Name, [string]$Old, [string]$New, [int]$Expected)
    $script:caseIndex++
    $oldPath = Join-Path $tmp ("case{0}-old.cs" -f $script:caseIndex)
    $newPath = Join-Path $tmp ("case{0}-new.cs" -f $script:caseIndex)
    Set-Content -LiteralPath $oldPath -Value $Old -Encoding UTF8 -NoNewline
    Set-Content -LiteralPath $newPath -Value $New -Encoding UTF8 -NoNewline
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $inertDiff -OldPath $oldPath -NewPath $newPath | Out-Null
    $actual = $LASTEXITCODE
    if ($actual -eq $Expected) {
        Write-Host ("PASS: " + $Name)
    }
    else {
        Write-Host ("FAIL: {0} (expected exit {1}, got {2})" -f $Name, $Expected, $actual)
        $script:failures++
    }
}

$INERT = 0
$DIFFERENT = 1
$DOUBT = 2

Assert-Verdict "identical files" `
    "class A { int x = 1; }" `
    "class A { int x = 1; }" $INERT

Assert-Verdict "full-line comment added" `
    "class A {`n    int x = 1;`n}" `
    "class A {`n    // init`n    int x = 1;`n}" $INERT

Assert-Verdict "full-line comment removed" `
    "class A {`n    // remove me`n    int x = 1;`n}" `
    "class A {`n    int x = 1;`n}" $INERT

Assert-Verdict "full-line comment reworded" `
    "class A {`n    // old wording`n    int x = 1;`n}" `
    "class A {`n    // new wording`n    int x = 1;`n}" $INERT

Assert-Verdict "blank line inserted and removed" `
    "class A {`n`n    int x = 1;`n}" `
    "class A {`n    int x = 1;`n`n}" $INERT

Assert-Verdict "CRLF file with full-line comment edit" `
    ("class A {`r`n    // one`r`n    int x = 1;`r`n}`r`n") `
    ("class A {`r`n    // two`r`n    int x = 1;`r`n}`r`n") $INERT

Assert-Verdict "indentation-only change on comment line" `
    "class A {`n    // note`n    int x = 1;`n}" `
    "class A {`n        // note`n    int x = 1;`n}" $INERT

Assert-Verdict "code change" `
    "class A { int x = 1; }" `
    "class A { int x = 2; }" $DIFFERENT

Assert-Verdict "double-slash inside string is code, not comment" `
    'class A { string u = "http://a.example"; }' `
    'class A { string u = "http://b.example"; }' $DIFFERENT

Assert-Verdict "trailing comment appended to code line (false negative by design)" `
    "class A { int x = 1; }" `
    "class A { int x = 1; } // note" $DIFFERENT

Assert-Verdict "trailing comment reworded (false negative by design)" `
    "class A { int x = 1; } // one" `
    "class A { int x = 1; } // two" $DIFFERENT

Assert-Verdict "whitespace reformat that splits a line (false negative by design)" `
    "class A { void M() { Run(); } }" `
    "class A {`n    void M() { Run(); }`n}" $DIFFERENT

Assert-Verdict "preprocessor directive change" `
    "#if DEBUG`nclass A { int x = 1; }`n#endif" `
    "#if UNITY_EDITOR`nclass A { int x = 1; }`n#endif" $DIFFERENT

Assert-Verdict "verbatim string is doubt" `
    'class A { string p = @"C:\a"; }' `
    'class A { string p = @"C:\a"; }' $DOUBT

Assert-Verdict "interpolated verbatim string is doubt" `
    'class A { string s = $@"v={x}"; }' `
    'class A { string s = $@"v={x}"; }' $DOUBT

Assert-Verdict "verbatim interpolated (at-dollar) string is doubt" `
    'class A { string s = @$"v={x}"; }' `
    'class A { string s = @$"v={x}"; }' $DOUBT

Assert-Verdict "raw string literal is doubt" `
    "class A { int x = 1; }" `
    ('class A { string s = """raw"""; }') $DOUBT

Assert-Verdict "block comment is doubt even when inert-looking" `
    "class A { /* a */ int x = 1; }" `
    "class A { int x = 1; }" $DOUBT

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $inertDiff -OldPath (Join-Path $tmp "absent-old.cs") -NewPath (Join-Path $tmp "absent-new.cs") | Out-Null
if ($LASTEXITCODE -eq $DOUBT) {
    Write-Host "PASS: missing file is doubt"
}
else {
    Write-Host ("FAIL: missing file is doubt (expected exit {0}, got {1})" -f $DOUBT, $LASTEXITCODE)
    $script:failures++
}

Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue

if ($script:failures -gt 0) {
    Write-Host ("{0} failing case(s)" -f $script:failures)
    exit 1
}
Write-Host "PASS: inert_diff line heuristic"
exit 0
