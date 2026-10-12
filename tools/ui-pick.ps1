# 通过文件选择对话框把测试视频加入队列（自动化不稳定，仅作辅助）
#
# 背景：新版 Shell 对话框的遗留控件（Edit aid=1148 / Button aid=1）不响应
# WM_SETTEXT / BM_CLICK，UIA 的 ValuePattern / InvokePattern 也不支持。
# 目前可行的路径是：点击文件名编辑框 → 键盘输入带引号的完整路径 → Escape x2。
# 实测成功率不稳定，失败时请人工选择文件。
#
# 用法:
#   powershell -ExecutionPolicy Bypass -File tools/ui-pick.ps1 -Paths '"C:\a.mp4" "C:\b.mp4"'

param([Parameter(Mandatory = $true)][string]$Paths, [int]$TimeoutSec = 10)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class PickDlg {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern IntPtr PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  public const uint MOUSEEVENTF_LEFTUP = 0x0004;
  public const uint WM_CLOSE = 0x0010;
  public static List<IntPtr> Dialogs() {
    var res = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      if (IsWindowVisible(h)) {
        var c = new System.Text.StringBuilder(256); GetClassName(h, c, 256);
        if (c.ToString() == "#32770") { res.Add(h); }
      }
      return true;
    }, IntPtr.Zero);
    return res;
  }
  public static void CloseAll() { foreach (var h in Dialogs()) { PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); } }
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(150);
    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(50);
    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
  }
}
"@

# 清掉上一次失败留下的对话框，避免选错窗口
[PickDlg]::CloseAll()
Start-Sleep -Milliseconds 800

$proc = Get-Process -Name VideoConverter -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { Write-Error 'app not running'; exit 1 }
# 入口查找顺序（项目规范：优先 AutomationId，中文字面量仅作兜底）：
#   1. AddFilesButton  文件态，文件卡片右上角
#   2. PickFileButton  空态，页面中央
#   3. Name '添加文件'   兜底；脚本内的中文字面量在无 BOM 的 .ps1 里会被
#                      Windows PowerShell 5.1 按 ANSI 解码成乱码，匹配会静默失败
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$pick = $null
foreach ($id in @('AddFilesButton', 'PickFileButton')) {
    $idCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    $pick = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCond)
    if ($pick) { break }
}
if (-not $pick) {
    $nameCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, '添加文件')
    $pick = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
}
if (-not $pick) { Write-Error 'no pick entry point found (is the Convert page showing?)'; exit 1 }

# 元素在查找与调用之间消失（或缺少 InvokePattern）时不要中断脚本，
# 交给下面的对话框轮询判定成功与否。
try {
    ($pick.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]).Invoke()
}
catch {
    Write-Warning ('invoke failed: ' + $_.Exception.Message)
}

$dlg = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline -and $dlg -eq [IntPtr]::Zero) {
    Start-Sleep -Milliseconds 300
    foreach ($h in [PickDlg]::Dialogs()) { $dlg = $h }
}
if ($dlg -eq [IntPtr]::Zero) { Write-Error 'file dialog did not appear'; exit 1 }

$droot = [System.Windows.Automation.AutomationElement]::FromHandle($dlg)
$els = $droot.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
$edit = $null
foreach ($e in $els) {
    if ($e.Current.ClassName -eq 'Edit' -and $e.Current.AutomationId -eq '1148') { $edit = $e; break }
}
if (-not $edit) { Write-Error 'filename edit not found'; exit 1 }

$r = $edit.Current.BoundingRectangle
[PickDlg]::Click([int]($r.Left + 40), [int]($r.Top + $r.Height / 2))
Start-Sleep -Milliseconds 500
# 逐字输入会被输入框的自动补全吞掉/改写，改用剪贴板粘贴
# （文件名框很窄，光标在末尾时只显示尾部，别误判为内容被截断）
Set-Clipboard -Value $Paths
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait('^a')
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait('^v')
Start-Sleep -Milliseconds 900
[System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
Start-Sleep -Milliseconds 3000

$left = [PickDlg]::Dialogs().Count
if ($left -gt 0) { [PickDlg]::CloseAll(); Start-Sleep -Milliseconds 800 }
Write-Output "picker finished, dialogs left=$left"
