param(
    [Parameter(Mandatory=$true)][string]$OldPath,
    [Parameter(Mandatory=$true)][string]$NewPath
)

# Verdict for the merge gate's inert-diff fast path: exit 0 = comment/whitespace-only, 1 = code change, 2 = doubt (callers treat as code).
$ErrorActionPreference = "Stop"

function Get-NormalizedCSharp {
    param([string]$Text)

    $sb = New-Object System.Text.StringBuilder
    $directiveMark = [string][char]1
    $n = $Text.Length
    $i = 0
    $mode = "code"
    $strVerbatim = $false
    $strInterp = $false
    $holeStack = New-Object System.Collections.Stack
    $holeDepth = 0
    $atLineStart = $true
    $pending = $false

    while ($i -lt $n) {
        $c = $Text[$i]

        if ($mode -eq "str") {
            if ($strVerbatim) {
                if ($c -eq '"') {
                    if ($i + 1 -lt $n -and $Text[$i + 1] -eq '"') {
                        [void]$sb.Append('"').Append('"')
                        $i += 2
                        continue
                    }
                    [void]$sb.Append('"')
                    $i++
                    $mode = "code"
                    $atLineStart = $false
                    continue
                }
            }
            else {
                if ($c -eq '\') {
                    if ($i + 1 -ge $n) { throw "unterminated string literal" }
                    [void]$sb.Append($c).Append($Text[$i + 1])
                    $i += 2
                    continue
                }
                if ($c -eq '"') {
                    [void]$sb.Append('"')
                    $i++
                    $mode = "code"
                    $atLineStart = $false
                    continue
                }
                if ($c -eq "`n" -or $c -eq "`r") { throw "unterminated string literal" }
            }
            if ($strInterp) {
                if ($c -eq '{') {
                    if ($i + 1 -lt $n -and $Text[$i + 1] -eq '{') {
                        [void]$sb.Append('{').Append('{')
                        $i += 2
                        continue
                    }
                    [void]$sb.Append('{')
                    $i++
                    $holeStack.Push(@{ Verbatim = $strVerbatim; Interp = $strInterp; SavedDepth = $holeDepth })
                    $holeDepth = 0
                    $mode = "code"
                    continue
                }
                if ($c -eq '}' -and $i + 1 -lt $n -and $Text[$i + 1] -eq '}') {
                    [void]$sb.Append('}').Append('}')
                    $i += 2
                    continue
                }
            }
            [void]$sb.Append($c)
            $i++
            continue
        }

        if ([char]::IsWhiteSpace($c)) {
            if ($c -eq "`n") { $atLineStart = $true }
            $pending = $true
            $i++
            continue
        }

        if ($c -eq '#' -and $atLineStart -and $holeStack.Count -eq 0) {
            if ($pending -and $sb.Length -gt 0) { [void]$sb.Append(' ') }
            $pending = $false
            [void]$sb.Append($directiveMark)
            while ($i -lt $n -and $Text[$i] -ne "`n" -and $Text[$i] -ne "`r") {
                [void]$sb.Append($Text[$i])
                $i++
            }
            [void]$sb.Append($directiveMark)
            $pending = $true
            continue
        }

        if ($c -eq '/' -and $i + 1 -lt $n -and $Text[$i + 1] -eq '/') {
            while ($i -lt $n -and $Text[$i] -ne "`n") { $i++ }
            $pending = $true
            continue
        }

        if ($c -eq '/' -and $i + 1 -lt $n -and $Text[$i + 1] -eq '*') {
            $end = $Text.IndexOf("*/", $i + 2)
            if ($end -lt 0) { throw "unterminated block comment" }
            $i = $end + 2
            $pending = $true
            continue
        }

        if ($c -eq '"' -and $i + 2 -lt $n -and $Text[$i + 1] -eq '"' -and $Text[$i + 2] -eq '"') {
            throw "raw string literal (unsupported)"
        }

        $isInterp = $false
        $isVerbatim = $false
        $prefixLen = 0
        if ($c -eq '"') { $prefixLen = 1 }
        elseif ($c -eq '@' -and $i + 1 -lt $n -and $Text[$i + 1] -eq '"') { $isVerbatim = $true; $prefixLen = 2 }
        elseif ($c -eq '@' -and $i + 2 -lt $n -and $Text[$i + 1] -eq '$' -and $Text[$i + 2] -eq '"') { $isVerbatim = $true; $isInterp = $true; $prefixLen = 3 }
        elseif ($c -eq '$' -and $i + 1 -lt $n -and $Text[$i + 1] -eq '"') { $isInterp = $true; $prefixLen = 2 }
        elseif ($c -eq '$' -and $i + 2 -lt $n -and $Text[$i + 1] -eq '@' -and $Text[$i + 2] -eq '"') { $isVerbatim = $true; $isInterp = $true; $prefixLen = 3 }

        if ($prefixLen -gt 0) {
            if ($pending -and $sb.Length -gt 0) { [void]$sb.Append(' ') }
            $pending = $false
            for ($k = 0; $k -lt $prefixLen; $k++) { [void]$sb.Append($Text[$i + $k]) }
            $i += $prefixLen
            $mode = "str"
            $strVerbatim = $isVerbatim
            $strInterp = $isInterp
            $atLineStart = $false
            continue
        }

        if ($c -eq "'") {
            if ($pending -and $sb.Length -gt 0) { [void]$sb.Append(' ') }
            $pending = $false
            [void]$sb.Append($c)
            $i++
            $closed = $false
            while ($i -lt $n) {
                $ch = $Text[$i]
                if ($ch -eq "`n" -or $ch -eq "`r") { break }
                if ($ch -eq '\') {
                    if ($i + 1 -ge $n) { break }
                    [void]$sb.Append($ch).Append($Text[$i + 1])
                    $i += 2
                    continue
                }
                [void]$sb.Append($ch)
                $i++
                if ($ch -eq "'") { $closed = $true; break }
            }
            if (-not $closed) { throw "unterminated char literal" }
            $atLineStart = $false
            continue
        }

        if ($holeStack.Count -gt 0 -and $c -eq '}' -and $holeDepth -eq 0) {
            if ($pending -and $sb.Length -gt 0) { [void]$sb.Append(' ') }
            $pending = $false
            [void]$sb.Append('}')
            $i++
            $frame = $holeStack.Pop()
            $mode = "str"
            $strVerbatim = $frame.Verbatim
            $strInterp = $frame.Interp
            $holeDepth = $frame.SavedDepth
            continue
        }
        if ($holeStack.Count -gt 0) {
            if ($c -eq '{') { $holeDepth++ }
            elseif ($c -eq '}') { $holeDepth-- }
        }

        if ($pending -and $sb.Length -gt 0) { [void]$sb.Append(' ') }
        $pending = $false
        [void]$sb.Append($c)
        $i++
        $atLineStart = $false
    }

    if ($mode -ne "code" -or $holeStack.Count -gt 0) { throw "unterminated construct at end of file" }
    return $sb.ToString()
}

function Read-FileText {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw ("missing file: " + $Path) }
    $raw = Get-Content -LiteralPath $Path -Raw
    if ($null -eq $raw) { return "" }
    return [string]$raw
}

try {
    $oldNorm = Get-NormalizedCSharp (Read-FileText $OldPath)
    $newNorm = Get-NormalizedCSharp (Read-FileText $NewPath)
}
catch {
    Write-Output ("doubt: " + $_.Exception.Message)
    exit 2
}

if ($oldNorm -ceq $newNorm) {
    Write-Output "inert"
    exit 0
}
Write-Output "different"
exit 1
