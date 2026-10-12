# 多尺寸 / 多页面 UI 截图验证：按 DIP 指定窗口尺寸，逐页截图
#
# 用法:
#   powershell -ExecutionPolicy Bypass -File tools/ui-verify.ps1 -OutDir shots
#
# 说明:
# - 窗口尺寸以 DIP 给出，脚本按窗口 DPI 换算成物理像素后再 SetWindowPos
#   （Win32 的 SetWindowPos 用物理像素，WinUI 的 PreferredMinimum* 用 DIP，两者单位不同）
# - 截图用 PrintWindow(PW_RENDERFULLCONTENT) 抓窗口内容，避免被其它窗口遮挡
# - 页面切换走 UI Automation：先按 ASCII AutomationId（NavConvert/NavQueue/NavSettings）
#   定位导航项，失败再按可见名、最后按导航项序号回退

param(
    [string[]]$Sizes = @('800x600', '1180x800', '1600x1000'),
    [string[]]$Pages = @('NavConvert', 'NavQueue', 'NavSettings'),
    [string]$OutDir = 'shots',
    [string]$ProcessName = 'VideoConverter'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class UiVerify {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  public struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

# 必须先声明 DPI 感知，否则 GetWindowRect / SetWindowPos 的坐标会被虚拟化缩放
[UiVerify]::SetProcessDpiAwarenessContext(-3) | Out-Null

function Get-AppWindow {
    $p = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $p) { throw "process $ProcessName not found" }
    return $p
}

function Set-AppSize([IntPtr]$hwnd, [int]$dipW, [int]$dipH, [double]$scale) {
    $pxW = [int][Math]::Round($dipW * $scale)
    $pxH = [int][Math]::Round($dipH * $scale)
    [UiVerify]::SetWindowPos($hwnd, [IntPtr]::Zero, 40, 40, $pxW, $pxH, 0x0040) | Out-Null
    [UiVerify]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 900
}

function Select-NavItem($root, [string]$name, [int]$index) {
    # 导航项是 ControlType.ListItem（ClassName 为全限定的 Microsoft.UI.Xaml.Controls.NavigationViewItem）。
    # 定位顺序：AutomationId（ASCII，项目规范）→ 可见名（限定 ControlType.ListItem，
    # 避开同名的窗口头部标题）→ 序号。中文参数从命令行传入会被 exec 层转码损坏，
    # 所以 Id 路径优先；-Pages 默认值直接给的就是 Id。
    $navId = switch ($name) {
        'NavConvert' { 'NavConvert' }
        'NavQueue' { 'NavQueue' }
        'NavSettings' { 'NavSettings' }
        '转换' { 'NavConvert' }
        '队列' { 'NavQueue' }
        '设置' { 'NavSettings' }
        default { $null }
    }
    $el = $null
    if ($navId) {
        $idCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $navId)
        $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCond)
    }

    $listCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $listCond)
    if (-not $el) {
        foreach ($candidate in $items) {
            if ($candidate.Current.Name -eq $name) { $el = $candidate; break }
        }
    }
    if (-not $el -and $items.Count -gt $index) { $el = $items[$index] }
    if (-not $el) { throw "nav item not found: $name" }
    $pattern = $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern) -as [System.Windows.Automation.SelectionItemPattern]
    if (-not $pattern) { throw "nav item has no SelectionItem pattern: $name" }
    $pattern.Select()
    Start-Sleep -Milliseconds 700
}

function Save-WindowShot([IntPtr]$hwnd, [string]$path) {
    $r = New-Object UiVerify+Rect
    [UiVerify]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left
    $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    # PW_RENDERFULLCONTENT = 2：WinUI 3 窗口必须带该标志才能抓到内容
    $ok = [UiVerify]::PrintWindow($hwnd, $hdc, 2)
    $g.ReleaseHdc($hdc)
    if (-not $ok) {
        $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return "${w}x${h}"
}

# -File 传参时数组会被拼成一个字符串，这里统一拆开
if ($Sizes.Count -eq 1 -and $Sizes[0] -like "*,*") { $Sizes = $Sizes[0] -split "," }
if ($Pages.Count -eq 1 -and $Pages[0] -like "*,*") { $Pages = $Pages[0] -split "," }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$proc = Get-AppWindow
$hwnd = $proc.MainWindowHandle
$dpi = [UiVerify]::GetDpiForWindow($hwnd)
if ($dpi -le 0) { $dpi = 96 }
$scale = $dpi / 96.0
Write-Output "window dpi=$dpi scale=$scale"

foreach ($size in $Sizes) {
    $parts = $size -split 'x'
    $dipW = [int]$parts[0]; $dipH = [int]$parts[1]
    Set-AppSize $hwnd $dipW $dipH $scale
    for ($i = 0; $i -lt $Pages.Count; $i++) {
        Select-NavItem ([System.Windows.Automation.AutomationElement]::FromHandle($hwnd)) $Pages[$i] $i
        $file = Join-Path $OutDir ("{0}x{1}-{2}.png" -f $dipW, $dipH, $i)
        $actual = Save-WindowShot $hwnd $file
        Write-Output "$size page=$($Pages[$i]) -> $file ($actual px)"
    }
}
Write-Output 'verify done'
