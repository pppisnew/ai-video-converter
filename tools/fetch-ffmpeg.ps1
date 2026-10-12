#Requires -Version 5.1
<#
.SYNOPSIS
    获取 ffmpeg / ffprobe 静态二进制并放置到 tools/ffmpeg/（供应用捆绑分发）。
.DESCRIPTION
    优先使用 winget 安装 BtbN 的 GPL 静态构建（国内网络下 GitHub 直链常不可达），
    失败时回退到 GitHub Releases 直链下载。已存在二进制时默认跳过，加 -Force 重新获取。
.EXAMPLE
    ./tools/fetch-ffmpeg.ps1
    ./tools/fetch-ffmpeg.ps1 -Force
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [string]$WingetId = "BtbN.FFmpeg.GPL.9.0",
    [string]$DownloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-win64-gpl-9.0.zip"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$destDir = Join-Path $repoRoot "tools\ffmpeg"
$destExe = Join-Path $destDir "ffmpeg.exe"
$destProbe = Join-Path $destDir "ffprobe.exe"

if ((Test-Path $destExe) -and (Test-Path $destProbe) -and -not $Force) {
    Write-Host "tools/ffmpeg 已存在，跳过获取（-Force 可强制刷新）。"
    & $destExe -version | Select-Object -First 1
    exit 0
}

function Install-ViaWinget {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { return $false }
    Write-Host "尝试通过 winget 安装 $WingetId ..."
    winget install --id $WingetId --source winget --accept-source-agreements --accept-package-agreements --silent | Out-Null
    if ($LASTEXITCODE -ne 0) { return $false }

    $candidates = @()
    $linksDir = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links"
    if (Test-Path $linksDir) { $candidates += (Get-ChildItem $linksDir -Filter "ffmpeg.exe" -Recurse -ErrorAction SilentlyContinue) }
    $pkgRoot = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    if (Test-Path $pkgRoot) { $candidates += (Get-ChildItem $pkgRoot -Recurse -Filter "ffmpeg.exe" -ErrorAction SilentlyContinue) }

    $src = $candidates | Select-Object -First 1
    if (-not $src) { return $false }

    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    Copy-Item $src.FullName $destExe -Force
    $probe = Join-Path $src.DirectoryName "ffprobe.exe"
    if (Test-Path $probe) { Copy-Item $probe $destProbe -Force }
    return (Test-Path $destProbe)
}

function Install-ViaDownload {
    Write-Host "回退到 GitHub Releases 直链下载（约 190MB）..."
    $tmp = Join-Path $env:TEMP ("ffmpeg-fetch-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    try {
        $zip = Join-Path $tmp "ffmpeg.zip"
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $zip -UseBasicParsing
        Expand-Archive -Path $zip -DestinationPath $tmp -Force
        $exe = Get-ChildItem $tmp -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
        if (-not $exe) { throw "压缩包内未找到 ffmpeg.exe" }
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
        Copy-Item $exe.FullName $destExe -Force
        $probe = Join-Path $exe.DirectoryName "ffprobe.exe"
        if (Test-Path $probe) { Copy-Item $probe $destProbe -Force }
        return (Test-Path $destProbe)
    }
    finally {
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$ok = Install-ViaWinget
if (-not $ok) { $ok = Install-ViaDownload }

if (-not $ok) {
    Write-Error "ffmpeg 获取失败。请手动下载 BtbN 静态构建（win64-gpl），将 ffmpeg.exe 与 ffprobe.exe 放入 tools/ffmpeg/。"
    exit 1
}

Write-Host "完成："
& $destExe -version | Select-Object -First 1
