#requires -Version 5.1
# Standalone 3D .cube LUT runner. No color presets are hard-coded.
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $InputPaths
)

$ErrorActionPreference = 'Stop'
$AppName = Split-Path -Path $PSScriptRoot -Leaf
if ([string]::IsNullOrWhiteSpace($AppName)) { $AppName = 'ImageProcessor' }
try { $Host.UI.RawUI.WindowTitle = $AppName } catch { }

function Get-CubePresets {
    $directory = Join-Path $PSScriptRoot 'Curves'
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Missing Curves folder: $directory"
    }
    $files = @(Get-ChildItem -LiteralPath $directory -File |
               Where-Object { $_.Extension -ieq '.cube' } | Sort-Object Name)
    if ($files.Count -eq 0) { throw 'No .cube files found in Curves.' }
    return $files
}

function Get-DefaultPresetIndex {
    param($Files)
    $config = Join-Path (Join-Path $PSScriptRoot 'Curves') 'Default.txt'
    if (-not (Test-Path -LiteralPath $config -PathType Leaf)) { return 0 }
    $lines = @(Get-Content -LiteralPath $config -Encoding UTF8 |
               ForEach-Object { $_.Trim() } |
               Where-Object { $_.Length -gt 0 -and -not $_.StartsWith('#') })
    if ($lines.Count -eq 0) { return 0 }
    if ($lines.Count -ne 1) {
        throw 'Curves/Default.txt must contain exactly one .cube filename.'
    }
    $fileName = $lines[0]
    if ([IO.Path]::GetFileName($fileName) -ne $fileName -or
        [IO.Path]::GetExtension($fileName) -ine '.cube') {
        throw 'Default.txt must name a .cube file in the same Curves folder.'
    }
    for ($i = 0; $i -lt $Files.Count; ++$i) {
        if ($Files[$i].Name -ieq $fileName) { return $i }
    }
    throw "Default filter '$fileName' not found in Curves. Update or delete Default.txt."
}

try {
    Write-Host ''
    Write-Host ('=== ' + $AppName + ' ===') -ForegroundColor Cyan
    Write-Host 'Static 3D .cube LUT presets. No strength or level parameter.'
    Write-Host 'Input images are never overwritten.'
    Write-Host ''

    $files = @(Get-CubePresets)
    $defaultIndex = Get-DefaultPresetIndex -Files $files
    $selectedIndex = $defaultIndex
    if ($files.Count -gt 1) {
        Write-Host 'Available LUTs:'
        for ($i = 0; $i -lt $files.Count; ++$i) {
            $tag = if ($i -eq $defaultIndex) { ' [default]' } else { '' }
            Write-Host ('  {0}. {1}{2}' -f ($i + 1), $files[$i].Name, $tag)
        }
        while ($true) {
            $response = Read-Host ('Choose LUT [Enter = {0}]' -f ($defaultIndex + 1))
            if ([string]::IsNullOrWhiteSpace($response)) { break }
            $candidate = 0
            if ([int]::TryParse($response.Trim(), [ref]$candidate) -and
                $candidate -ge 1 -and $candidate -le $files.Count) {
                $selectedIndex = $candidate - 1
                break
            }
            Write-Host 'Enter a number from the list.' -ForegroundColor Yellow
        }
    }
    $selection = $files[$selectedIndex]
    Write-Host ('Selected: ' + $selection.Name) -ForegroundColor Cyan

    Add-Type -AssemblyName System.Drawing
    $library = Join-Path $PSScriptRoot 'Library.cs'
    if (-not (Test-Path -LiteralPath $library -PathType Leaf)) {
        throw "Missing library: $library"
    }
    Add-Type -LiteralPath $library -ReferencedAssemblies 'System.Drawing.dll'

    Write-Host ('Loading 3D LUT: ' + $selection.Name)
    $lut = [CubeLut]::Load($selection.FullName)
    Write-Host ('Loaded: {0}x{0}x{0}' -f $lut.Size) -ForegroundColor Cyan
    Write-Host ''

    if ($null -eq $InputPaths -or $InputPaths.Count -eq 0) {
        Add-Type -AssemblyName System.Windows.Forms
        $dialog = New-Object System.Windows.Forms.OpenFileDialog
        $dialog.Title = 'Select images - ' + $AppName
        $dialog.Filter = 'Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif|All files|*.*'
        $dialog.Multiselect = $true
        try {
            if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
                Write-Host 'No images selected. Cancelled.'
                return
            }
            $InputPaths = @($dialog.FileNames)
        }
        finally { $dialog.Dispose() }
    }

    $paths = @($InputPaths | Select-Object -Unique)
    $success = 0
    $failed = 0
    for ($index = 0; $index -lt $paths.Count; ++$index) {
        $path = $paths[$index]
        try {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Input file not found: $path"
            }
            $file = Get-Item -LiteralPath $path
            if ($file.Extension.ToLowerInvariant() -notin @('.png','.jpg','.jpeg','.bmp','.tif','.tiff','.gif')) {
                throw 'Unsupported image format. Use PNG, JPG, BMP, TIFF, or GIF.'
            }
            $stem = [IO.Path]::GetFileNameWithoutExtension($file.Name)
            $base = '{0}_{1}_{2}' -f $stem, $AppName, $selection.BaseName
            $output = Join-Path $file.DirectoryName ($base + '.png')
            $counter = 2
            while (Test-Path -LiteralPath $output) {
                $output = Join-Path $file.DirectoryName ('{0}_{1}.png' -f $base, $counter)
                ++$counter
            }
            [ImageEngine]::RenderFile($file.FullName, $output, $lut,
                                      $index + 1, $paths.Count)
            Write-Host ('OK: ' + $output) -ForegroundColor Green
            ++$success
        }
        catch {
            Write-Host ('FAILED: ' + $path + ' :: ' + $_.Exception.Message) -ForegroundColor Red
            ++$failed
        }
    }
    Write-Host ''
    Write-Host ('Finished. Success: {0} | Failed: {1}' -f $success, $failed)
    if ($failed -gt 0) { exit 1 }
}
catch {
    Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
