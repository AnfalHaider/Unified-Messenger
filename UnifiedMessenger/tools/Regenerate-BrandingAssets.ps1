#Requires -Version 5.1
<#
.SYNOPSIS
    Regenerates Unified Messenger branding assets from master icon and logo sources.
.PARAMETER IconPath
    Square app icon PNG (window icon, installer, tray, toasts).
.PARAMETER LogoSvgPath
    Full wordmark SVG (icon + UNIFIED MESSENGER text). Exported via Inkscape when available.
.PARAMETER LogoPath
    Optional wide logo PNG fallback when SVG export is unavailable.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$IconPath,

    [string]$LogoSvgPath = "",

    [string]$LogoPath = "",

    [string]$AssetsRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrWhiteSpace($AssetsRoot)) {
    $AssetsRoot = Join-Path (Split-Path -Parent $PSScriptRoot) "Assets"
}

[string]$TileBackground = "#1B75BB"
[string[]]$InkscapeCandidates = @(
    "${env:ProgramFiles}\Inkscape\bin\inkscape.exe",
    "${env:ProgramFiles(x86)}\Inkscape\bin\inkscape.exe",
    "$env:LOCALAPPDATA\Programs\Inkscape\bin\inkscape.exe"
)

function Convert-HexToColor([string]$Hex) {
    $hex = $Hex.TrimStart('#')
    return [System.Drawing.Color]::FromArgb(
        255,
        [Convert]::ToInt32($hex.Substring(0, 2), 16),
        [Convert]::ToInt32($hex.Substring(2, 2), 16),
        [Convert]::ToInt32($hex.Substring(4, 2), 16))
}

function Resolve-InkscapePath() {
    foreach ($candidate in $InkscapeCandidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

function Export-SvgToPng([string]$SvgPath, [string]$OutputPath, [int]$Width) {
    $inkscape = Resolve-InkscapePath
    if ($null -eq $inkscape) {
        throw "Inkscape is required to export wordmark SVG. Install from https://inkscape.org or pass -LogoPath PNG."
    }

    $directory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    & $inkscape $SvgPath --export-type=png --export-filename=$OutputPath -w $Width | Out-Null
    if (-not (Test-Path -LiteralPath $OutputPath)) {
        throw "Inkscape failed to export $SvgPath"
    }
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

        $padding = [Math]::Min($Width, $Height) * 0.06
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

function New-InlineWordmark([System.Drawing.Image]$IconSource, [int]$CanvasWidth = 280, [int]$CanvasHeight = 44) {
    $bitmap = New-Object System.Drawing.Bitmap $CanvasWidth, $CanvasHeight, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        $iconSize = $CanvasHeight - 4
        $graphics.DrawImage($IconSource, 0, 2, $iconSize, $iconSize)

        $titleFont = New-Object System.Drawing.Font ("Segoe UI", 13, [System.Drawing.FontStyle]::Bold)
        $subtitleFont = New-Object System.Drawing.Font ("Segoe UI", 8, [System.Drawing.FontStyle]::Regular)
        try {
            $textX = $iconSize + 10
            $graphics.DrawString("UNIFIED", $titleFont, [System.Drawing.Brushes]::White, $textX, 4)
            $graphics.DrawString("MESSENGER", $subtitleFont, [System.Drawing.Brushes]::White, $textX + 1, 22)
        }
        finally {
            $titleFont.Dispose()
            $subtitleFont.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function New-InlineWordmarkTheme([System.Drawing.Image]$IconSource, [System.Drawing.Color]$TextColor, [string]$OutputPath, [int]$CanvasWidth = 280, [int]$CanvasHeight = 44) {
    $bitmap = New-Object System.Drawing.Bitmap $CanvasWidth, $CanvasHeight, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        $iconSize = $CanvasHeight - 4
        $graphics.DrawImage($IconSource, 0, 2, $iconSize, $iconSize)

        $brush = New-Object System.Drawing.SolidBrush $TextColor
        $titleFont = New-Object System.Drawing.Font ("Segoe UI", 13, [System.Drawing.FontStyle]::Bold)
        $subtitleFont = New-Object System.Drawing.Font ("Segoe UI", 8, [System.Drawing.FontStyle]::Regular)
        try {
            $textX = $iconSize + 10
            $graphics.DrawString("UNIFIED", $titleFont, $brush, $textX, 4)
            $graphics.DrawString("MESSENGER", $subtitleFont, $brush, $textX + 1, 22)
        }
        finally {
            $brush.Dispose()
            $titleFont.Dispose()
            $subtitleFont.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    Save-Png $bitmap $OutputPath
    $bitmap.Dispose()
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
$assetsRootPath = (Resolve-Path -LiteralPath $AssetsRoot).Path
$brandingPath = Join-Path $assetsRootPath "Branding"
$background = Convert-HexToColor $TileBackground
$wordmarkHeroPath = Join-Path $brandingPath "wordmark-hero.png"
$wordmarkInlineDarkPath = Join-Path $brandingPath "wordmark-inline-dark.png"
$wordmarkInlineLightPath = Join-Path $brandingPath "wordmark-inline-light.png"

$iconSource = [System.Drawing.Image]::FromFile($iconSourcePath)
$logoSource = $null
$iconMaster = $null
$wideMaster = $null
try {
    $iconMaster = New-SquareBitmap -Source $iconSource -CanvasSize 1024

    if (-not [string]::IsNullOrWhiteSpace($LogoSvgPath) -and (Test-Path -LiteralPath $LogoSvgPath)) {
        Export-SvgToPng -SvgPath (Resolve-Path -LiteralPath $LogoSvgPath).Path -OutputPath $wordmarkHeroPath -Width 720
        $logoSource = [System.Drawing.Image]::FromFile($wordmarkHeroPath)
    }
    elseif (-not [string]::IsNullOrWhiteSpace($LogoPath) -and (Test-Path -LiteralPath $LogoPath)) {
        $logoSource = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $LogoPath).Path)
        Save-Png ([System.Drawing.Bitmap]$logoSource.Clone()) $wordmarkHeroPath
    }
    else {
        throw "Provide -LogoSvgPath or -LogoPath for the UNIFIED MESSENGER wordmark."
    }

    $wideMaster = New-WideBitmap -Source $logoSource -Width 1240 -Height 600 -Background $background
    Save-Png $iconMaster (Join-Path $brandingPath "icon-master.png")
    Save-Png $wideMaster (Join-Path $brandingPath "wide-master.png")

    New-InlineWordmarkTheme -IconSource $iconSource -TextColor ([System.Drawing.Color]::White) -OutputPath $wordmarkInlineDarkPath
    New-InlineWordmarkTheme -IconSource $iconSource -TextColor ([System.Drawing.Color]::FromArgb(255, 30, 41, 59)) -OutputPath $wordmarkInlineLightPath

    $icoPath = Join-Path $assetsRootPath "AppIcon.ico"
    Save-Icon -SquareMaster $iconMaster -Path $icoPath
    Write-Host "Wrote $icoPath"
    Write-Host "Wrote $wordmarkHeroPath"
    Write-Host "Wrote $wordmarkInlineDarkPath"
    Write-Host "Wrote $wordmarkInlineLightPath"
}
finally {
    if ($null -ne $iconSource) { $iconSource.Dispose() }
    if ($null -ne $logoSource) { $logoSource.Dispose() }
    if ($null -ne $iconMaster) { $iconMaster.Dispose() }
    if ($null -ne $wideMaster) { $wideMaster.Dispose() }
}

Write-Host "Branding assets regenerated successfully."
