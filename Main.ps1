#requires -Version 5.1
# ColorCurver v2.0.1. Standard .cube LUT processing plus lossless floating HDR JXR.
# No application name or specific LUT name is hard-coded.
param(
    [Parameter(Position=0,ValueFromRemainingArguments=$true)]
    [string[]] $InputPaths
)
$ErrorActionPreference = 'Stop'
$AppName = Split-Path -Path $PSScriptRoot -Leaf
if ([string]::IsNullOrWhiteSpace($AppName)) { $AppName = 'ImageProcessor' }
try { $Host.UI.RawUI.WindowTitle = $AppName } catch { }

function Get-CubePresets {
    $directory = Join-Path $PSScriptRoot 'Curves'
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Missing Curves directory: $directory"
    }
    $files = @(Get-ChildItem -LiteralPath $directory -File |
        Where-Object { $_.Extension -ieq '.cube' } | Sort-Object Name)
    if ($files.Count -eq 0) { throw 'Curves has no .cube files.' }
    return $files
}

function Get-InverseFile {
    param([IO.FileInfo]$Forward, $AllFiles)
    if ($Forward.BaseName -match '(?i)_Inverse$') { return $null }
    $stem = $Forward.BaseName
    if ($stem -match '(?i)_Forward$') {
        $stem = $stem.Substring(0,$stem.Length - '_Forward'.Length)
    }
    $candidateName = $stem + '_Inverse.cube'
    foreach ($file in $AllFiles) {
        if ($file.Name -ieq $candidateName) { return $file }
    }
    return $null
}

function Get-DefaultIndex {
    param($Files,$AllFiles)
    $config = Join-Path (Join-Path $PSScriptRoot 'Curves') 'Default.txt'
    if (-not (Test-Path -LiteralPath $config -PathType Leaf)) { return 0 }
    $lines = @(Get-Content -LiteralPath $config -Encoding UTF8 |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_.Length -gt 0 -and -not $_.StartsWith('#') })
    if ($lines.Count -eq 0) { return 0 }
    if ($lines.Count -ne 1) { throw 'Default.txt must contain exactly one .cube filename.' }
    $filename = $lines[0]
    if ([IO.Path]::GetFileName($filename) -cne $filename -or
        [IO.Path]::GetExtension($filename) -ine '.cube') {
        throw 'Default.txt must specify a .cube filename in Curves, without a path.'
    }
    $found = $false
    foreach ($item in $AllFiles) {
        if ($item.Name -ieq $filename) { $found = $true; break }
    }
    if (-not $found) { throw "The default LUT '$filename' is missing from Curves." }
    for ($i=0;$i -lt $Files.Count;$i++) {
        if ($Files[$i].Name -ieq $filename) { return $i }
    }
    return 0 # a forward-only default is absent from the reverse-mode choices
}

function Confirm-InversePair {
    param([IO.FileInfo]$Forward,[IO.FileInfo]$Inverse)
    $lines = @(Get-Content -LiteralPath $Inverse.FullName -TotalCount 16 -Encoding UTF8)
    $expected = $null
    foreach ($line in $lines) {
        if ($line -match '^#\s*SOURCE_FORWARD_SHA256\s+([a-fA-F0-9]{64})\s*$') {
            $expected = $Matches[1]; break
        }
    }
    if ($null -ne $expected) {
        $actual = (Get-FileHash -LiteralPath $Forward.FullName -Algorithm SHA256).Hash
        if ($actual -ine $expected) { throw 'The inverse LUT was made for a different forward LUT.' }
        Write-Host 'Forward/inverse SHA-256 fingerprint: verified.' -ForegroundColor Green
    }
    else {
        Write-Host 'WARNING: Inverse has no SHA-256 pairing information.' -ForegroundColor Yellow
    }
}

function New-OutputName {
    param([string]$Directory,[string]$Stem,[string]$Extension)
    $output = Join-Path $Directory ($Stem+$Extension)
    $number = 2
    while (Test-Path -LiteralPath $output) {
        $output = Join-Path $Directory ('{0}_{1}{2}' -f $Stem,$number,$Extension)
        ++$number
    }
    return $output
}

try {
    Write-Host ''
    Write-Host ('=== ' + $AppName + ' | 2.0.1 ===') -ForegroundColor Cyan
    Write-Host 'Static .cube LUTs | SDR images -> PNG | floating HDR JPEG XR -> lossless JXR'
    Write-Host 'Original images and existing outputs are preserved.'
    Write-Host ''
    Write-Host 'Operation:'
    Write-Host '  1. Apply one selected LUT (default; also supports HDR JXR)'
    Write-Host '  2. Inverse estimate and forward replay (SDR only)'
    $reverseMode = $false
    while ($true) {
        $choice = Read-Host 'Choose operation [Enter = 1]'
        if ([string]::IsNullOrWhiteSpace($choice) -or $choice.Trim() -eq '1') { break }
        if ($choice.Trim() -eq '2') { $reverseMode = $true; break }
        Write-Host 'Please enter 1 or 2.' -ForegroundColor Yellow
    }
    Write-Host ''

    $allFiles = @(Get-CubePresets)
    if ($reverseMode) {
        $files = @($allFiles | Where-Object { $null -ne (Get-InverseFile -Forward $_ -AllFiles $allFiles) })
        if ($files.Count -eq 0) { throw 'No paired inverse LUT is available.' }
    }
    else { $files = $allFiles }
    $defaultIndex = Get-DefaultIndex -Files $files -AllFiles $allFiles
    $selectedIndex = $defaultIndex
    if ($files.Count -gt 1) {
        Write-Host 'Available LUTs:'
        for ($i=0;$i -lt $files.Count;$i++) {
            $tag = if ($i -eq $defaultIndex) { ' [default]' } else { '' }
            Write-Host ('  {0}. {1}{2}' -f ($i+1),$files[$i].Name,$tag)
        }
        while ($true) {
            $answer = Read-Host ('Choose LUT [Enter = {0}]' -f ($defaultIndex+1))
            if ([string]::IsNullOrWhiteSpace($answer)) { break }
            $requested = 0
            if ([int]::TryParse($answer.Trim(),[ref]$requested) -and
                $requested -ge 1 -and $requested -le $files.Count) {
                $selectedIndex = $requested-1; break
            }
            Write-Host 'Choose a listed number.' -ForegroundColor Yellow
        }
    }
    $selected = $files[$selectedIndex]
    Write-Host ('Selected: ' + $selected.Name) -ForegroundColor Cyan

    $inverseFile = $null
    if ($reverseMode) {
        $inverseFile = Get-InverseFile -Forward $selected -AllFiles $allFiles
        if ($null -eq $inverseFile) { throw 'Missing inverse LUT.' }
        Confirm-InversePair -Forward $selected -Inverse $inverseFile
        Write-Host ('Inverse LUT: ' + $inverseFile.Name) -ForegroundColor Cyan
    }

    if ($null -eq $InputPaths -or $InputPaths.Count -eq 0) {
        Add-Type -AssemblyName System.Windows.Forms
        $dialog = New-Object System.Windows.Forms.OpenFileDialog
        $dialog.Title = 'Choose image files - ' + $AppName
        $dialog.Filter = 'Images|*.jxr;*.wdp;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif|All files|*.*'
        $dialog.Multiselect = $true
        try {
            if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
                Write-Host 'No images selected.'; return
            }
            $InputPaths = @($dialog.FileNames)
        }
        finally { $dialog.Dispose() }
    }
    $paths = @($InputPaths | Select-Object -Unique)
    $hasJxr = @($paths | Where-Object { [IO.Path]::GetExtension($_) -in @('.jxr','.JXR','.wdp','.WDP') }).Count -gt 0

    $library = Join-Path $PSScriptRoot 'Library.cs'
    if (-not (Test-Path -LiteralPath $library)) { throw 'Missing Library.cs.' }
    Add-Type -AssemblyName System.Drawing
    if ($hasJxr -and -not $reverseMode) {
        $hdr = Join-Path $PSScriptRoot 'HDR.cs'
        if (-not (Test-Path -LiteralPath $hdr)) { throw 'Missing HDR.cs.' }
        Add-Type -AssemblyName PresentationCore
        Add-Type -AssemblyName WindowsBase
        Add-Type -LiteralPath @($library,$hdr) -ReferencedAssemblies @(
            'System.Drawing.dll','PresentationCore.dll','WindowsBase.dll')
    }
    else {
        Add-Type -LiteralPath $library -ReferencedAssemblies 'System.Drawing.dll'
    }
    if ($reverseMode) {
        $comparison = Join-Path $PSScriptRoot 'Comparison.cs'
        if (-not (Test-Path -LiteralPath $comparison)) { throw 'Missing Comparison.cs.' }
        Add-Type -LiteralPath $comparison -ReferencedAssemblies 'System.Drawing.dll'
    }

    Write-Host ('Loading LUT: ' + $selected.Name)
    $lut = [CubeLut]::Load($selected.FullName)
    Write-Host ('LUT size: {0}x{0}x{0}' -f $lut.Size) -ForegroundColor Cyan
    if ($reverseMode) { $inverseLut = [CubeLut]::Load($inverseFile.FullName) }
    Write-Host ''

    $success = 0; $failed = 0
    for ($index=0;$index -lt $paths.Count;$index++) {
        $path = $paths[$index]
        $created = @()
        try {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "File not found: $path" }
            $item = Get-Item -LiteralPath $path
            $ext = $item.Extension.ToLowerInvariant()
            if ($ext -notin @('.png','.jpg','.jpeg','.bmp','.tif','.tiff','.gif','.jxr','.wdp')) {
                throw 'Supported images: PNG, JPG, BMP, TIFF, GIF, HDR JXR/WDP.'
            }
            if ($reverseMode -and ($ext -eq '.jxr' -or $ext -eq '.wdp')) {
                throw 'Inverse preview is SDR only. For HDR JXR, choose operation 1.'
            }
            $stem = [IO.Path]::GetFileNameWithoutExtension($item.Name)
            $prefix = '{0}_{1}_{2}' -f $stem,$AppName,$selected.BaseName
            if (-not $reverseMode) {
                $isHdr = ($ext -eq '.jxr' -or $ext -eq '.wdp')
                $resultExt = if ($isHdr) { '.jxr' } else { '.png' }
                $output = New-OutputName -Directory $item.DirectoryName -Stem $prefix -Extension $resultExt
                if ($isHdr) {
                    Write-Host 'HDR mode: linear scRGB / SDR-encoded LUT / protected highlights / lossless JXR'
                    [HdrJxrEngine]::RenderFile($item.FullName,$output,$lut,$index+1,$paths.Count)
                }
                else {
                    [ImageEngine]::RenderFile($item.FullName,$output,$lut,$index+1,$paths.Count)
                }
                Write-Host ('OK: ' + $output) -ForegroundColor Green
            }
            else {
                $counter=1
                while ($true) {
                    $suffix = if ($counter -eq 1) { '' } else { '_'+$counter }
                    $base = $prefix + $suffix
                    $virtual = Join-Path $item.DirectoryName ($base+'_VirtualOriginal.png')
                    $replay = Join-Path $item.DirectoryName ($base+'_ForwardReplay.png')
                    $report = Join-Path $item.DirectoryName ($base+'_Comparison.txt')
                    if (-not (Test-Path -LiteralPath $virtual) -and
                        -not (Test-Path -LiteralPath $replay) -and
                        -not (Test-Path -LiteralPath $report)) { break }
                    ++$counter
                }
                Write-Host 'Inverse mode: creating estimated source image...'
                [ImageEngine]::RenderFile($item.FullName,$virtual,$inverseLut,
                                           2*$index+1,2*$paths.Count)
                $created += $virtual
                Write-Host 'Inverse mode: forward replay...'
                [ImageEngine]::RenderFile($virtual,$replay,$lut,
                                           2*$index+2,2*$paths.Count)
                $created += $replay
                Write-Host 'Inverse mode: computing comparison report...'
                $score = [ImageComparison]::CompareAndReport($item.FullName,$replay,$report,
                    $virtual,$selected.Name,$inverseFile.Name)
                $created += $report
                Write-Host ('OK: ' + $virtual) -ForegroundColor Green
                Write-Host ('OK: ' + $replay) -ForegroundColor Green
                Write-Host ('OK: ' + $report) -ForegroundColor Green
                Write-Host $score
            }
            ++$success
        }
        catch {
            foreach ($newFile in $created) {
                try { Remove-Item -LiteralPath $newFile -Force } catch { }
            }
            Write-Host ('FAILED: ' + $path + ' :: ' + $_.Exception.Message) -ForegroundColor Red
            Write-Host ($_.Exception.ToString()) -ForegroundColor DarkGray
            ++$failed
        }
    }
    Write-Host ''
    Write-Host ('Finished | Success: {0} | Failed: {1}' -f $success,$failed)
    if ($failed -gt 0) { exit 1 }
}
catch {
    Write-Host ('ERROR: '+$_.Exception.Message) -ForegroundColor Red
    exit 1
}
