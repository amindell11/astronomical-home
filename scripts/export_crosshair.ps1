<#
.SYNOPSIS
Exports the crosshair SVG masters with Inkscape 1.4.2.
.DESCRIPTION
Each SVG in Assets/Visuals/Ui/Crosshair produces Generated/<name>.png at
256x256 pixels, preserving the full 64-unit artboard and transparent background.
Exit 0 means every export succeeded; exit 1 means export failed.
Stdout is one JSON object containing renderer, pixelSize and outputFiles.
Diagnostics and native renderer output go to stderr. Existing PNGs are replaced.
.PARAMETER InkscapePath
Path to Inkscape 1.4.2. Defaults to its standard Windows installation.
.PARAMETER AssetDirectory
Folder containing the SVG masters. Defaults to this checkout's Crosshair folder.
#>
param(
    [string]$InkscapePath = "$env:ProgramFiles/Inkscape/bin/inkscape.com",
    [string]$AssetDirectory = "$PSScriptRoot/../src/Asteroids3D/Assets/Visuals/Ui/Crosshair"
)
$ErrorActionPreference = 'Stop'
try {
    $renderer = (& $InkscapePath --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $renderer -notmatch '^Inkscape 1\.4\.2\b') {
        throw "Expected Inkscape 1.4.2, got '$renderer'."
    }
    $source = (Resolve-Path -LiteralPath $AssetDirectory).Path
    $output = Join-Path $source 'Generated'
    [System.IO.Directory]::CreateDirectory($output) | Out-Null
    $vectors = @(Get-ChildItem -LiteralPath $source -Filter '*.svg' | Sort-Object Name)
    if ($vectors.Count -eq 0) { throw "No SVG masters in $source." }
    $files = @()
    foreach ($vector in $vectors) {
        $png = Join-Path $output ($vector.BaseName + '.png')
        & $InkscapePath --export-area-page --export-width=256 --export-height=256 "--export-filename=$png" $vector.FullName 2>&1 |
            ForEach-Object { [Console]::Error.WriteLine($_) }
        if ($LASTEXITCODE -ne 0) { throw "Export failed for $($vector.Name)." }
        $files += $png
    }
    @{ renderer = $renderer; pixelSize = 256; outputFiles = $files } | ConvertTo-Json -Compress
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
