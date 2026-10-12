# UI 截图工具：对指定进程的主窗口截图（供自动化视觉验证用）
# 注意：必须先将本进程设为 Per-Monitor-V2 DPI 感知，否则 GetWindowRect 返回的
# 是被虚拟化缩放的坐标（150% DPI 下会得到 2/3 尺寸的错误区域）。
param([string]$ProcessName = "VideoConverter", [string]$Out = "shot.png", [int]$SettleMs = 0)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ShotDpi {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out ShotRect r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
}
public struct ShotRect {
  public int Left; public int Top; public int Right; public int Bottom;
}
"@
[ShotDpi]::SetProcessDpiAwarenessContext(-3) | Out-Null   # DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
if ($SettleMs -gt 0) { Start-Sleep -Milliseconds $SettleMs }
$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { Write-Error "process $ProcessName not found"; exit 1 }
$rect = New-Object ShotRect
[ShotDpi]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    # PW_RENDERFULLCONTENT = 2：WinUI 3 必须带该标志才能抓到内容，同时避免被上层窗口遮挡
    $ok = [ShotDpi]::PrintWindow($proc.MainWindowHandle, $hdc, 2)
    $g.ReleaseHdc($hdc)
    if (-not $ok) {
        $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    }
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved $Out ${w}x${h}"