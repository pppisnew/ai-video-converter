# 校验关键 AutomationId 在三页都可达（跨页扫描）
param([string]$Ids)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$p = Get-Process -Name VideoConverter | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$list = $Ids -split ','
foreach ($id in $list) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($el) { Write-Output ("{0} OK" -f $id) } else { Write-Output ("{0} MISSING" -f $id) }
}