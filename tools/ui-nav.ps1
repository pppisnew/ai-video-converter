# 通过 UI Automation 点击/选中指定名称的控件（Name = AutomationProperties.Name）
param([string]$ElementName, [int]$WaitMs = 800)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$proc = Get-Process -Name VideoConverter -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { Write-Error "app not running"; exit 1 }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $ElementName)
$el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
if (-not $el) { Write-Error "element not found: $ElementName"; exit 1 }
$done = $false
try {
    $sel = $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern) -as [System.Windows.Automation.SelectionItemPattern]
    if ($sel) { $sel.Select(); Write-Output "selected: $ElementName"; $done = $true }
} catch {}
if (-not $done) {
    try {
        $inv = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
        if ($inv) { $inv.Invoke(); Write-Output "invoked: $ElementName"; $done = $true }
    } catch {}
}
if (-not $done) { Write-Error "no usable pattern for: $ElementName"; exit 1 }
Start-Sleep -Milliseconds $WaitMs