# Exports the summary sheet (sheet 2) of a workbook to PDF with Excel (read-only, nothing saved). Throwaway.
param([string]$Xlsx, [string]$Pdf)
$ErrorActionPreference = 'Stop'
$xl = New-Object -ComObject Excel.Application
try {
    $xl.Visible = $false; $xl.DisplayAlerts = $false; $xl.ScreenUpdating = $false
    $wb = $xl.Workbooks.Open($Xlsx, 0, $true)
    $wb.Worksheets.Item(2).ExportAsFixedFormat(0, $Pdf)
    "summary pages=" + $wb.Worksheets.Item(2).PageSetup.Pages.Count
    $wb.Close($false)
}
finally {
    $xl.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($xl) | Out-Null
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
}
