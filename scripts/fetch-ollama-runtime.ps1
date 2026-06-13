#Requires -Version 5.1
<#
.SYNOPSIS
    Downloads and verifies pinned Ollama Windows standalone CLI zips for local dev/CI testing.

.DESCRIPTION
    Fetches ollama-windows-{amd64,arm64}.zip from GitHub release v0.30.8 (by default),
    verifies SHA-256, writes sidecar files, and stores versioned copies under third_party/ollama/.
    Not used during Inno installer builds (settings-only runtime download as of v3.7.1).
#>
[CmdletBinding()]
param(
    [string]$Version = "0.30.8",
    [ValidateSet("all", "amd64", "arm64")]
    [string]$Architecture = "all",
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\third_party\ollama"),
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$assets = @{
    amd64 = @{
        DownloadName = "ollama-windows-amd64.zip"
        StagedName   = "ollama-windows-amd64-v$Version.zip"
        Url          = "https://github.com/ollama/ollama/releases/download/v$Version/ollama-windows-amd64.zip"
        Sha256       = "c2d26d97e698027329c252629d7113bbc05d874b49960cbb03e93a39ae9fd95c"
    }
    arm64 = @{
        DownloadName = "ollama-windows-arm64.zip"
        StagedName   = "ollama-windows-arm64-v$Version.zip"
        Url          = "https://github.com/ollama/ollama/releases/download/v$Version/ollama-windows-arm64.zip"
        Sha256       = "487fa170d6eedc3ce12fbf144a39970d8322c4c6efbaa9a366ad7aa8769f5713"
    }
}

function Write-Sha256Sidecar {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Hash
    )

    $fileName = Split-Path -Leaf $FilePath
    $sidecarPath = "$FilePath.sha256"
    Set-Content -Path $sidecarPath -Value "$Hash  $fileName" -Encoding ascii -NoNewline
    Write-Host "Wrote $sidecarPath"
}

function Test-StagedAsset {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$ExpectedSha256
    )

    if (-not (Test-Path $FilePath)) {
        return $false
    }

    $actual = (Get-FileHash -Path $FilePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $ExpectedSha256) {
        throw "Checksum mismatch for $(Split-Path -Leaf $FilePath): expected $ExpectedSha256, got $actual"
    }

    Write-Sha256Sidecar -FilePath $FilePath -Hash $actual
    return $true
}

function Get-StagedAsset {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Asset
    )

    $outputDir = [System.IO.Path]::GetFullPath($OutputDir)
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

    $stagedPath = Join-Path $outputDir $Asset.StagedName
    if (-not $Force -and (Test-StagedAsset -FilePath $stagedPath -ExpectedSha256 $Asset.Sha256)) {
        Write-Host "Already staged: $stagedPath"
        return $stagedPath
    }

    $tempPath = Join-Path ([System.IO.Path]::GetTempPath()) "$($Asset.DownloadName).$([Guid]::NewGuid().ToString('N')).download"
    Write-Host "Downloading $($Asset.Url) ..."
    Invoke-WebRequest -Uri $Asset.Url -OutFile $tempPath -UseBasicParsing

    $actual = (Get-FileHash -Path $tempPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Asset.Sha256) {
        Remove-Item -Force $tempPath -ErrorAction SilentlyContinue
        throw "Download checksum mismatch for $($Asset.DownloadName): expected $($Asset.Sha256), got $actual"
    }

    Move-Item -Path $tempPath -Destination $stagedPath -Force
    Write-Sha256Sidecar -FilePath $stagedPath -Hash $actual
    Write-Host "Staged $($Asset.StagedName) ($((Get-Item $stagedPath).Length) bytes)"
    return $stagedPath
}

$selected = switch ($Architecture) {
    "all" { @("amd64", "arm64") }
    default { @($Architecture) }
}

$staged = @()
foreach ($arch in $selected) {
    $staged += Get-StagedAsset -Asset $assets[$arch]
}

return $staged
