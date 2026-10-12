# 通过 UIA 选中 ComboBox 中的指定项：展开后按名称选中
param([Parameter(Mandatory=$true)][string]$ComboId, [Parameter(Mandatory=$true)][string]$ItemName, [int]$WaitMs = 600)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$p = Get-Process -Name VideoConverter -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Error 'app not running'; exit 1 }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $ComboId)
$combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
if (-not $combo) { Write-Error "combo not found: $ComboId"; exit 1 }
$ec = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern) -as [System.Windows.Automation.ExpandCollapsePattern]
if ($ec) { $ec.Expand(); Start-Sleep -Milliseconds 500 }
$nameCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $ItemName)
$item = $combo.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
if (-not $item) {
    $item = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
}
if (-not $item) { Write-Error "item not found: $ItemName"; exit 1 }
$si = $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern) -as [System.Windows.Automation.SelectionItemPattern]
if (-not $si) { Write-Error "item not selectable: $ItemName"; exit 1 }
$si.Select()
Start-Sleep -Milliseconds $WaitMs
Write-Output "selected '$ItemName' in $ComboId"