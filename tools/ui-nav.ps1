# 通过 UI Automation 操作指定控件：优先按 ASCII AutomationId 定位，
# 未给 -AutomationId 时退回按可见名（AutomationProperties.Name）匹配。
#
# 用法:
#   powershell -ExecutionPolicy Bypass -File tools/ui-nav.ps1 -AutomationId NavQueue
#   powershell -ExecutionPolicy Bypass -File tools/ui-nav.ps1 -ElementName '队列'
#
# 注意：-ElementName 传中文时，命令行经 exec 层会被转码损坏（项目规范要求按
# AutomationId 定位）；窗口头部标题与导航项同名（转换 / 队列 / 设置），因此按名匹配
# 不能只取第一个元素——必须遍历所有同名元素，挑出支持 SelectionItem / Invoke 模式的
# 那个。该脚本含中文字面量，必须以带 BOM 的 UTF-8 保存，否则 Windows PowerShell 5.1
# 会按 ANSI 解码导致匹配失败。

param([string]$ElementName, [int]$WaitMs = 800, [string]$AutomationId)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process -Name VideoConverter -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { Write-Error 'app not running'; exit 1 }

$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
# -AutomationId 收到非 ASCII（中文名或经 exec 层转码的乱码）时按名匹配，避免静默失败
$byId = $AutomationId
if ($byId -and $byId -match '[^\x00-\x7F]') {
    if (-not $ElementName) { $ElementName = $byId }
    $byId = ''
}
if (-not $byId -and -not $ElementName) { Write-Error 'pass -AutomationId (preferred) or -ElementName'; exit 1 }

if ($byId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $byId)
    $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $found) { Write-Error "element not found by AutomationId: $byId"; exit 1 }
    $matches = @($found)
    $label = $byId
} else {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $ElementName)
    $matches = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($matches.Count -eq 0) { Write-Error "element not found: $ElementName"; exit 1 }
    $label = $ElementName
}

foreach ($el in $matches) {
    try {
        $sel = $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern) -as [System.Windows.Automation.SelectionItemPattern]
        if ($sel) {
            $sel.Select()
            Write-Output "selected: $label"
            Start-Sleep -Milliseconds $WaitMs
            exit 0
        }
    } catch { }
}

foreach ($el in $matches) {
    try {
        $inv = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
        if ($inv) {
            $inv.Invoke()
            Write-Output "invoked: $label"
            Start-Sleep -Milliseconds $WaitMs
            exit 0
        }
    } catch { }
}

# Expander / ToggleButton 走 ExpandCollapse（媒体信息、高级选项属此类）
foreach ($el in $matches) {
    try {
        $ec = $el.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern) -as [System.Windows.Automation.ExpandCollapsePattern]
        if ($ec) {
            $ec.Expand()
            Write-Output "expanded: $label"
            Start-Sleep -Milliseconds $WaitMs
            exit 0
        }
    } catch { }
}

Write-Error "no usable pattern for: $label"
exit 1
