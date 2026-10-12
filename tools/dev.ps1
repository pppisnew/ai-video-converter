#Requires -Version 5.1
<#
.SYNOPSIS
    一条命令把项目跑起来：校验 .NET SDK -> 按需补 ffmpeg -> 构建 -> 启动。
.DESCRIPTION
    等价于手动执行 README「快速开始」里的四步，只是把"该跳过就跳过"的判断做了：

      1. dotnet --version                         -> 校验存在且主版本 >= 10，否则直接给出 winget 安装命令
      2. ./tools/fetch-ffmpeg.ps1                 -> 仅在 tools/ffmpeg/ 与 PATH 都没有 ffmpeg 时才执行
      3. dotnet run --project src/VideoConverter  -> dotnet run 自带构建，不需要先 dotnet build

    每一步做了什么都会打印出来；想看清或想自己控制，直接照 README 手动跑，语义完全一致。

.PARAMETER Test
    不启动应用，改为运行单元测试（dotnet test）。

.PARAMETER NoRun
    只构建，不启动；结束时会打印可以直接双击的 exe 路径。

.PARAMETER SkipFfmpeg
    跳过 ffmpeg 检查与获取。

.PARAMETER Force
    强制重新获取 ffmpeg（透传给 tools/fetch-ffmpeg.ps1 -Force）。

.EXAMPLE
    ./tools/dev.ps1
.EXAMPLE
    ./tools/dev.ps1 -Test
.EXAMPLE
    ./tools/dev.ps1 -NoRun
.EXAMPLE
    ./tools/dev.ps1 -Force
#>
[CmdletBinding()]
param(
    [switch]$Test,
    [switch]$NoRun,
    [switch]$SkipFfmpeg,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# dotnet 的告警 / 还原进度会写 stderr。PowerShell 7.4+ 默认会让 native command 的 stderr
# 触发 $ErrorActionPreference，把整个脚本打断；这里显式关掉，交给下方各自判断退出码。
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$appProject = 'src\VideoConverter\VideoConverter.csproj'
$appExe = 'src\VideoConverter\bin\Debug\net10.0-windows10.0.17763.0\VideoConverter.exe'

Push-Location $repoRoot
try {
    # ---------- 1/4 .NET SDK ----------
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host '[1/4] .NET SDK：找不到 dotnet 命令' -ForegroundColor Red
        Write-Host '      先装 .NET 10 SDK：'
        Write-Host '        winget install --id Microsoft.DotNet.SDK.10 --source winget'
        exit 1
    }

    $sdkVersion = (& dotnet --version).Trim()
    $sdkMajor = 0
    [void][int]::TryParse(($sdkVersion -split '\.')[0], [ref]$sdkMajor)
    Write-Host "[1/4] .NET SDK：$sdkVersion"
    if ($sdkMajor -lt 10) {
        Write-Host "      版本不够：项目 TFM 是 net10.0-windows10.0.17763.0，需要 .NET 10 SDK。" -ForegroundColor Red
        Write-Host '        winget install --id Microsoft.DotNet.SDK.10 --source winget'
        exit 1
    }

    # ---------- 2/4 ffmpeg ----------
    # FfmpegLocator 的查找顺序是「随包捆绑 -> 仓库 tools/ffmpeg -> 系统 PATH」，
    # 所以只有两处都没有时才真的需要下载；PATH 上已有就跳过这 ~280MB。
    if ($SkipFfmpeg) {
        Write-Host '[2/4] ffmpeg：按 -SkipFfmpeg 跳过检查'
    }
    elseif ((Test-Path 'tools\ffmpeg\ffmpeg.exe') -and (Test-Path 'tools\ffmpeg\ffprobe.exe')) {
        Write-Host '[2/4] ffmpeg：tools/ffmpeg/ 已有二进制，跳过下载'
    }
    elseif ((Get-Command ffmpeg -ErrorAction SilentlyContinue) -and (Get-Command ffprobe -ErrorAction SilentlyContinue)) {
        Write-Host '[2/4] ffmpeg：PATH 上已有 ffmpeg/ffprobe，跳过 ~280MB 下载（应用内会显示「系统 PATH」）' -ForegroundColor Yellow
        Write-Host '      注意：这样构建产物不带 ffmpeg，需要随包捆绑时重跑本脚本（别加 -SkipFfmpeg）'
    }
    else {
        Write-Host '[2/4] ffmpeg：tools/ffmpeg/ 与 PATH 都没有，开始获取（约 280MB；先试 winget，失败回退 GitHub 直链）...'
        if ($Force) {
            & "$PSScriptRoot\fetch-ffmpeg.ps1" -Force
        }
        else {
            & "$PSScriptRoot\fetch-ffmpeg.ps1"
        }
        if ($LASTEXITCODE -ne 0) { throw 'ffmpeg 获取失败，详见上方输出' }
    }

    # ---------- 3/4 + 4/4 构建与运行 ----------
    $code = 0
    if ($Test) {
        Write-Host '[3/4] 构建：由 dotnet test 一并完成'
        Write-Host '[4/4] 单元测试'
        & dotnet test
        $code = $LASTEXITCODE
    }
    elseif ($NoRun) {
        Write-Host '[3/4] 构建'
        & dotnet build $appProject
        $code = $LASTEXITCODE
        Write-Host '[4/4] 未启动（-NoRun）。下次可以直接双击：'
        Write-Host "      $appExe"
    }
    else {
        # dotnet run 自带构建，所以没有单独的 dotnet build 这一步
        Write-Host '[3/4] 构建：由 dotnet run 一并完成（不需要先 dotnet build）'
        Write-Host '[4/4] 启动应用'
        & dotnet run --project $appProject
        $code = $LASTEXITCODE
    }

    if ($code -ne 0) {
        Write-Host "失败，退出码 $code" -ForegroundColor Red
    }
    exit $code
}
finally {
    Pop-Location
}