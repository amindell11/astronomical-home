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

Assert-Verdict "line comment added" `
    "class A { int x = 1; }" `
    "class A { int x = 1; } // init" $INERT

Assert-Verdict "line comment removed" `
    "class A {`n    // remove me`n    int x = 1;`n}" `
    "class A {`n    int x = 1;`n}" $INERT

Assert-Verdict "line comment text changed" `
    "class A {`n    // old wording`n    int x = 1;`n}" `
    "class A {`n    // new wording`n    int x = 1;`n}" $INERT

Assert-Verdict "block comment removed mid-token-stream" `
    "class A { int/*mid*/x = 1; }" `
    "class A { int x = 1; }" $INERT

Assert-Verdict "block comment spanning lines removed" `
    "class A {`n/* a`n   b */`n    int x = 1;`n}" `
    "class A {`n    int x = 1;`n}" $INERT

Assert-Verdict "code change" `
    "class A { int x = 1; }" `
    "class A { int x = 2; }" $DIFFERENT

Assert-Verdict "double-slash inside string is code, not comment" `
    'class A { string u = "http://a.example"; }' `
    'class A { string u = "http://b.example"; }' $DIFFERENT

Assert-Verdict "double-slash inside string survives comment edit" `
    ('class A { string u = "http://a.example"; }' + "`n// note") `
    ('class A { string u = "http://a.example"; }' + "`n// other note") $INERT

Assert-Verdict "escaped quote in string then comment change" `
    ('class A { string s = "say \"hi\" // x"; } // one') `
    ('class A { string s = "say \"hi\" // x"; } // two') $INERT

Assert-Verdict "verbatim string content change" `
    'class A { string p = @"C:\a // x"; }' `
    'class A { string p = @"C:\b // x"; }' $DIFFERENT

Assert-Verdict "verbatim string with doubled quotes survives comment edit" `
    ('class A { string s = @"say ""hi"" now"; } // one') `
    ('class A { string s = @"say ""hi"" now"; } // two') $INERT

Assert-Verdict "multiline verbatim string whitespace is significant" `
    ("class A { string s = @`"line1`n  line2`"; }") `
    ("class A { string s = @`"line1`n line2`"; }") $DIFFERENT

Assert-Verdict "interpolated string hole code change" `
    'class A { string s = $"v={x}"; }' `
    'class A { string s = $"v={y}"; }' $DIFFERENT

Assert-Verdict "interpolated string with nested literal survives comment edit" `
    ('class A { string s = $"v={(ok ? "y" : "n")}"; } // one') `
    ('class A { string s = $"v={(ok ? "y" : "n")}"; } // two') $INERT

Assert-Verdict "verbatim interpolated string content change" `
    'class A { string s = $@"p={x} \raw"; }' `
    'class A { string s = $@"q={x} \raw"; }' $DIFFERENT

Assert-Verdict "whitespace-only reformat" `
    "class A {`n    void M() { int x = 1;`n        Run(x); }`n}" `
    "class A`n{`n    void M()`n    {`n        int x = 1;`n        Run(x);`n    }`n}" $INERT

Assert-Verdict "whitespace removal that joins tokens" `
    "class A { int y = a - -b; }" `
    "class A { int y = a --b; }" $DIFFERENT

Assert-Verdict "string-internal whitespace change" `
    'class A { string s = "a  b"; }' `
    'class A { string s = "a b"; }' $DIFFERENT

Assert-Verdict "char literal change" `
    "class A { char c = 'a'; }" `
    "class A { char c = 'b'; }" $DIFFERENT

Assert-Verdict "escaped-quote char literal survives reformat" `
    "class A { char q = '\'';  char s = '\\'; }" `
    "class A {`n    char q = '\'';`n    char s = '\\';`n}" $INERT

Assert-Verdict "comment edit in file with preprocessor directives" `
    "#if DEBUG`nclass A { int x = 1; } // one`n#endif" `
    "#if DEBUG`nclass A { int x = 1; } // two`n#endif" $INERT

Assert-Verdict "preprocessor directive change" `
    "#if DEBUG`nclass A { int x = 1; }`n#endif" `
    "#if UNITY_EDITOR`nclass A { int x = 1; }`n#endif" $DIFFERENT

Assert-Verdict "directive merged onto one line is not inert" `
    "#if DEBUG`nclass A { }`n#endif" `
    "#if DEBUG class A { } #endif" $DIFFERENT

Assert-Verdict "interpolation format-clause whitespace is significant" `
    'class A { string s = $"{x:00  00}"; }' `
    'class A { string s = $"{x:00 00}"; }' $DIFFERENT

Assert-Verdict "verbatim interpolation format-clause whitespace is significant" `
    'class A { string s = $@"{x:00  00}"; }' `
    'class A { string s = $@"{x:00 00}"; }' $DIFFERENT

Assert-Verdict "interpolation hole whitespace is significant" `
    'class A { string s = $"{x + 1}"; }' `
    'class A { string s = $"{x +  1}"; }' $DIFFERENT

Assert-Verdict "comment inside interpolation hole is doubt" `
    'class A { string s = $"{x /* c */}"; }' `
    'class A { string s = $"{x}"; }' $DOUBT

Assert-Verdict "bare CR line terminator is doubt" `
    ("class A { int x = 1; }`r// tail") `
    ("class A { int x = 1; }`r// other") $DOUBT

Assert-Verdict "U+2028 line terminator is doubt" `
    ("class A { int x = 1; }" + [string][char]0x2028 + "// tail") `
    ("class A { int x = 1; }" + [string][char]0x2028 + "// other") $DOUBT

Assert-Verdict "CRLF file with comment edit is inert" `
    ("class A {`r`n    // one`r`n    int x = 1;`r`n}`r`n") `
    ("class A {`r`n    // two`r`n    int x = 1;`r`n}`r`n") $INERT

Assert-Verdict "unterminated block comment is doubt" `
    "class A { int x = 1; }" `
    "class A { int x = 1; } /* oops" $DOUBT

Assert-Verdict "raw string literal is doubt" `
    "class A { int x = 1; }" `
    ('class A { string s = """raw"""; }') $DOUBT

Assert-Verdict "trailing comment without newline at EOF" `
    "class A { int x = 1; }" `
    "class A { int x = 1; } // tail" $INERT

Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue

if ($script:failures -gt 0) {
    Write-Host ("{0} failing case(s)" -f $script:failures)
    exit 1
}
Write-Host "PASS: inert_diff normalizer"
exit 0
