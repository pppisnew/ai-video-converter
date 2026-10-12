param([string]$ChipName)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$p = Get-Process -Name VideoConverter | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $ChipName)
$and = New-Object System.Windows.Automation.AndCondition($c1, $c2)
$btn = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $and)
if (-not $btn) { Write-Error "chip button not found: $ChipName"; exit 1 }
$inv = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
$inv.Invoke()
Start-Sleep -Milliseconds 900
$textCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
$texts = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCond)
$rows = 0
$empty = $false
foreach ($e in $texts) {
    if ($e.Current.Name -match '^1a989|^4b9b') { $rows++ }
    if ($e.Current.Name -eq '暂无任务') { $empty = $true }
}
Write-Output ("chip={0} rows={1} emptyState={2}" -f $ChipName, $rows, $empty)