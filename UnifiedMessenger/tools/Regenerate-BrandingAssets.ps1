#Requires -Version 5.1
<#
.SYNOPSIS
    Regenerates Unified Messenger branding assets from master icon and logo PNGs.
.PARAMETER IconPath
    Square-ish app icon source (used for tiles, title bar, taskbar).
.PARAMETER LogoPath
    Wide logo source (used for splash screen and wide Start tile).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$IconPath,

    [Parameter(Mandatory = $true)]
    [string]$LogoPath,

    [string]$AssetsRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrWhiteSpace($AssetsRoot)) {
    $AssetsRoot = Join-Path (Split-Path -Parent $PSScriptRoot) "Assets"
}

[string]$TileBackground = "#1E293B"

function Convert-HexToColor([string]$Hex) {
    $hex = $Hex.TrimStart('#')
    return [System.Drawing.Color]::FromArgb(
        255,
        [Convert]::ToInt32($hex.Substring(0, 2), 16),
        [Convert]::ToInt32($hex.Substring(2, 2), 16),
        [Convert]::ToInt32($hex.Substring(4, 2), 16))
}

function New-SquareBitmap([System.Drawing.Image]$Source, [int]$CanvasSize, [double]$ContentRatio = 0.82) {
    $bitmap = New-Object System.Drawing.Bitmap $CanvasSize, $CanvasSize, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $maxSide = [int][Math]::Floor($CanvasSize * $ContentRatio)
        $scale = [Math]::Min($maxSide / $Source.Width, $maxSide / $Source.Height)
        $width = [int][Math]::Round($Source.Width * $scale)
        $height = [int][Math]::Round($Source.Height * $scale)
        $x = ($CanvasSize - $width) / 2
        $y = ($CanvasSize - $height) / 2
        $graphics.DrawImage($Source, $x, $y, $width, $height)
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function New-WideBitmap([System.Drawing.Image]$Source, [int]$Width, [int]$Height, [System.Drawing.Color]$Background) {
    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear($Background)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $padding = [Math]::Min($Width, $Height) * 0.08
        $maxWidth = $Width - (2 * $padding)
        $maxHeight = $Height - (2 * $padding)
        $scale = [Math]::Min($maxWidth / $Source.Width, $maxHeight / $Source.Height)
        $drawWidth = [int][Math]::Round($Source.Width * $scale)
        $drawHeight = [int][Math]::Round($Source.Height * $scale)
        $x = ($Width - $drawWidth) / 2
        $y = ($Height - $drawHeight) / 2
        $graphics.DrawImage($Source, $x, $y, $drawWidth, $drawHeight)
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function Resize-Bitmap([System.Drawing.Bitmap]$Source, [int]$Size) {
    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($Source, 0, 0, $Size, $Size)
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function Save-Png([System.Drawing.Bitmap]$Bitmap, [string]$Path) {
    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Save-Icon([System.Drawing.Bitmap]$SquareMaster, [string]$Path, [int[]]$Sizes = @(16, 24, 32, 48, 256)) {
    $pngEntries = New-Object System.Collections.Generic.List[byte[]]
    foreach ($size in $Sizes) {
        $resized = Resize-Bitmap -Source $SquareMaster -Size $size
        try {
            $stream = New-Object System.IO.MemoryStream
            try {
                $resized.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $pngEntries.Add($stream.ToArray())
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            $resized.Dispose()
        }
    }

    $stream = New-Object System.IO.MemoryStream
    try {
        $writer = New-Object System.IO.BinaryWriter $stream
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$Sizes.Count)

        $offset = 6 + (16 * $Sizes.Count)
        for ($i = 0; $i -lt $Sizes.Count; $i++) {
            $size = $Sizes[$i]
            $pngBytes = $pngEntries[$i]
            $writer.Write([byte][Math]::Min($size, 255))
            $writer.Write([byte][Math]::Min($size, 255))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$pngBytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $pngBytes.Length
        }

        foreach ($pngBytes in $pngEntries) {
            $writer.Write($pngBytes)
        }

        [System.IO.File]::WriteAllBytes($Path, $stream.ToArray())
    }
    finally {
        $stream.Dispose()
    }
}

$iconSourcePath = (Resolve-Path -LiteralPath $IconPath).Path
$logoSourcePath = (Resolve-Path -LiteralPath $LogoPath).Path
$assetsRootPath = (Resolve-Path -LiteralPath $AssetsRoot).Path
$brandingPath = Join-Path $assetsRootPath "Branding"
$background = Convert-HexToColor $TileBackground

$iconSource = [System.Drawing.Image]::FromFile($iconSourcePath)
$logoSource = [System.Drawing.Image]::FromFile($logoSourcePath)
try {
    $iconMaster = New-SquareBitmap -Source $iconSource -CanvasSize 1024
    $wideMaster = New-WideBitmap -Source $logoSource -Width 1240 -Height 600 -Background $background
    $wideTile = New-WideBitmap -Source $logoSource -Width 620 -Height 300 -Background $background
    $splashTile = New-WideBitmap -Source $logoSource -Width 620 -Height 300 -Background $background

    Save-Png $iconMaster (Join-Path $brandingPath "icon-master.png")
    Save-Png $wideMaster (Join-Path $brandingPath "wide-master.png")

    $outputs = @(
        @{ Path = "StoreLogo.png"; Bitmap = (Resize-Bitmap -Source $iconMaster -Size 50) },
        @{ Path = "Square44x44Logo.scale-200.png"; Bitmap = (Resize-Bitmap -Source $iconMaster -Size 88) },
        @{ Path = "Square44x44Logo.targetsize-24_altform-unplated.png"; Bitmap = (Resize-Bitmap -Source $iconMaster -Size 24) },
        @{ Path = "Square44x44Logo.targetsize-48_altform-lightunplated.png"; Bitmap = (Resize-Bitmap -Source $iconMaster -Size 48) },
        @{ Path = "Square150x150Logo.scale-200.png"; Bitmap = (Resize-Bitmap -Source $iconMaster -Size 300) },
        @{ Path = "LockScreenLogo.scale-200.png"; Bitmap = (Resize-Bitmap -Source $iconMaster -Size 96) },
        @{ Path = "Wide310x150Logo.scale-200.png"; Bitmap = $wideTile },
        @{ Path = "SplashScreen.scale-200.png"; Bitmap = $splashTile }
    )

    foreach ($output in $outputs) {
        $targetPath = Join-Path $assetsRootPath $output.Path
        Save-Png $output.Bitmap $targetPath
        $output.Bitmap.Dispose()
        Write-Host "Wrote $targetPath"
    }

    $icoPath = Join-Path $assetsRootPath "AppIcon.ico"
    Save-Icon -SquareMaster $iconMaster -Path $icoPath
    Write-Host "Wrote $icoPath"
}
finally {
    if ($null -ne $iconSource) { $iconSource.Dispose() }
    if ($null -ne $logoSource) { $logoSource.Dispose() }
    if ($null -ne $iconMaster) { $iconMaster.Dispose() }
    if ($null -ne $wideMaster) { $wideMaster.Dispose() }
}

Write-Host "Branding assets regenerated successfully."
