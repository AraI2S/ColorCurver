#requires -Version 5.1
# Image processing entry point. The application's public name is its folder name.
# Keep all internal file/class names neutral so the folder can be renamed freely.

param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $InputPaths
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot always points to the directory containing this script,
# independent of the current working directory or the folder's current name.
$AppName = Split-Path -Path $PSScriptRoot -Leaf
if ([string]::IsNullOrWhiteSpace($AppName)) { $AppName = 'ImageTool' }
try { $Host.UI.RawUI.WindowTitle = "$AppName - Image Processor" } catch { }

Write-Host ''
Write-Host ('=== ' + $AppName + ' ===') -ForegroundColor Cyan
Write-Host 'Local tone adjustment to a new, lossless PNG.'
Write-Host 'Original images are never overwritten.'
Write-Host ''

$Level = 600
while ($true) {
    $text = Read-Host 'Tone level 0-1000 [Enter = 600]'
    if ([string]::IsNullOrWhiteSpace($text)) { break }
    $number = 0
    if ([int]::TryParse($text.Trim(), [ref] $number) -and
        $number -ge 0 -and $number -le 1000) {
        $Level = $number
        break
    }
    Write-Host 'Please enter an integer from 0 to 1000.' -ForegroundColor Yellow
}

Add-Type -AssemblyName System.Drawing
$libraryPath = Join-Path $PSScriptRoot 'Library.cs'
if (-not (Test-Path -LiteralPath $libraryPath -PathType Leaf)) {
    throw "Missing required library: $libraryPath"
}
Add-Type -LiteralPath $libraryPath -ReferencedAssemblies 'System.Drawing.dll'

if (-not $InputPaths -or $InputPaths.Count -eq 0) {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = 'Select images - ' + $AppName
    $dialog.Filter = 'Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif|All files|*.*'
    $dialog.Multiselect = $true
    try {
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
            Write-Host 'No image selected. Cancelled.'
            return
        }
        $InputPaths = @($dialog.FileNames)
    }
    finally {
        $dialog.Dispose()
    }
}

Write-Host ''
Write-Host ([ImageEngine]::Describe($Level))
Write-Host 'Processing images...'
Write-Host ''
$success = 0
$failed = 0

foreach ($path in $InputPaths) {
    try {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Input file does not exist: $path"
        }
        $file = Get-Item -LiteralPath $path
        if ($file.Extension.ToLowerInvariant() -notin @('.png','.jpg','.jpeg','.bmp','.tif','.tiff','.gif')) {
            throw 'Unsupported file type. Please select an image file, not a ZIP.'
        }

        $stem = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $folder = $file.DirectoryName
        $nameBase = '{0}_{1}{2}' -f $stem, $AppName, $Level
        $output = Join-Path $folder ($nameBase + '.png')
        $counter = 2
        while (Test-Path -LiteralPath $output) {
            $output = Join-Path $folder ('{0}_{1}.png' -f $nameBase, $counter)
            $counter++
        }

        [ImageEngine]::RenderFile($file.FullName, $output, $Level)
        Write-Host ('OK: ' + $output) -ForegroundColor Green
        $success++
    }
    catch {
        Write-Host ('FAILED: ' + $path + ' :: ' + $_.Exception.Message) -ForegroundColor Red
        $failed++
    }
}

Write-Host ''
Write-Host ("Finished. Success: $success | Failed: $failed")
if ($failed -gt 0) { exit 1 }
