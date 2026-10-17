# E.6 #3 / #5 / #6 check in real Excel (COM, invisible, read-only, nothing saved). Throwaway; not in the repo.
# usage: powershell -NoProfile -File excel-check.ps1 <dir with full-sample.xlsx, golden-bales-1-10.xlsx, full-sample-draft.xlsx>
param([string]$Dir)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Out($s) { Write-Output $s }

# A tampered copy: one cached <v> changed to 9.99 — if Excel shows 9.99 on open, it displayed the cached values and did not
# recalculate on load (manual mode), which proves what Excel shows comes from our <v>.
$full = Join-Path $Dir 'full-sample.xlsx'
$tampered = Join-Path $Dir 'tampered-check.xlsx'
Copy-Item $full $tampered -Force
$zip = [System.IO.Compression.ZipFile]::Open($tampered, 'Update')
$entry = $zip.GetEntry('xl/worksheets/sheet1.xml')
$reader = New-Object System.IO.StreamReader($entry.Open())
$xml = $reader.ReadToEnd(); $reader.Close()
$m = [regex]::Match($xml, '<x:c r="K3"[^>]*><x:f>[^<]*</x:f><x:v>([^<]*)</x:v>')
if (-not $m.Success) { $m = [regex]::Match($xml, '<c r="K3"[^>]*><f>[^<]*</f><v>([^<]*)</v>') }
Out ("tamper: K3 original cached <v> = " + $m.Groups[1].Value)
$xml2 = $xml.Substring(0, $m.Groups[1].Index) + '9.99' + $xml.Substring($m.Groups[1].Index + $m.Groups[1].Length)
$entry.Delete()
$new = $zip.CreateEntry('xl/worksheets/sheet1.xml')
$w = New-Object System.IO.StreamWriter($new.Open(), (New-Object System.Text.UTF8Encoding($false)))
$w.Write($xml2); $w.Close(); $zip.Dispose()

$xl = New-Object -ComObject Excel.Application
try {
    $xl.Visible = $false
    $xl.DisplayAlerts = $false
    $xl.ScreenUpdating = $false
    $xl.AskToUpdateLinks = $false
    $blank = $xl.Workbooks.Add()
    $xl.Calculation = -4135   # xlCalculationManual
    $xl.CalculateBeforeSave = $false
    Out ("excel version " + $xl.Version + "; calculation mode " + $xl.Calculation)

    # 1) tampered file: cached value shown?
    $wb = $xl.Workbooks.Open($tampered, 0, $true)
    $ws = $wb.Worksheets.Item(1)
    Out ("tampered K3 shown on open (manual calc) = " + $ws.Range('K3').Value2 + "  (9.99 => Excel shows the cached <v>, no recalculation on load)")
    $xl.CalculateFull()
    Out ("tampered K3 after CalculateFull = " + $ws.Range('K3').Value2)
    $wb.Close($false)

    foreach ($name in @('full-sample.xlsx', 'golden-bales-1-10.xlsx', 'full-sample-draft.xlsx')) {
        $path = Join-Path $Dir $name
        Out ""
        Out ("=== " + $name)
        $wb = $xl.Workbooks.Open($path, 0, $true)
        $main = $wb.Worksheets.Item(1)
        $summary = $wb.Worksheets.Item(2)
        $last = $main.UsedRange.Rows.Count
        $lots = ($main.Range('M2').Value2 -ne $null)
        $areaCol = if ($lots) { 11 } else { 10 }
        $usdCol = if ($lots) { 13 } else { 12 }
        $footArea = if ($lots) { 10 } else { 9 }
        $footUsd = if ($lots) { 12 } else { 11 }

        function Snapshot() {
            $vals = @{}
            for ($r = 3; $r -le $last; $r++) {
                $e = $main.Cells.Item($r, 5)
                if ($e.HasFormula -and ($e.Formula -like '=SUM(H*' -or $e.Formula -like '=ROWS(*')) {
                    $b = $main.Cells.Item($r, 3).Value2
                    $vals["footer$b"] = "" + $e.Value2 + " / " + $main.Cells.Item($r, $footArea).Value2 + " / " + $main.Cells.Item($r, $footUsd).Value2
                }
                if ($main.Cells.Item($r, 2).Value2 -eq 3816) { $vals['3816area'] = "" + $main.Cells.Item($r, $areaCol).Value2 }
            }
            $sl = $summary.UsedRange.Rows.Count + $summary.UsedRange.Row - 1
            $vals['summaryTotals'] = "" + $summary.Cells.Item($sl, 5).Value2 + " / " + $summary.Cells.Item($sl, 6).Value2 + " / " + $summary.Cells.Item($sl, 7).Value2
            $vals['summaryRow3'] = "" + $summary.Cells.Item(3, 4).Value2 + " / " + $summary.Cells.Item(3, 5).Value2 + " / " + $summary.Cells.Item(3, 6).Value2 + " / " + $summary.Cells.Item(3, 7).Value2
            return $vals
        }

        $before = Snapshot
        Out ("lots=" + $lots + " usedRows=" + $last)
        foreach ($k in @('3816area', 'footer1', 'footer10', 'footer29', 'footer30', 'footer33', 'summaryRow3', 'summaryTotals')) {
            if ($before.ContainsKey($k)) { Out ("  before recalc " + $k + " = " + $before[$k]) }
        }
        $xl.CalculateFull()
        $after = Snapshot
        $diff = @($before.Keys | Where-Object { $before[$_] -ne $after[$_] })
        Out ("  after CalculateFull: " + $diff.Count + " differences among " + $before.Count + " checked values" + $(if ($diff.Count) { ": " + ($diff -join ',') } else { "" }))

        # page setup as Excel reads it
        $ps = $main.PageSetup
        Out ("  PageSetup: Zoom=" + $ps.Zoom + " FitToPagesWide=" + $ps.FitToPagesWide + " FitToPagesTall=" + $ps.FitToPagesTall + " PrintTitleRows=" + $ps.PrintTitleRows + " PaperSize=" + $ps.PaperSize + " Orientation=" + $ps.Orientation + " CenterHeader='" + $ps.CenterHeader + "'")
        Out ("  Summary PrintTitleRows='" + $summary.PageSetup.PrintTitleRows + "'")
        Out ("  A1 font colour (BGR long)=" + $main.Range('A1').Font.Color + " (0x767171 => 7434614); A1 fill=" + $main.Range('A1').Interior.Color + " (yellow => 65535); A1 font=" + $main.Range('A1').Font.Name + " " + $main.Range('A1').Font.Size)
        Out ("  RTL=" + $main.DisplayRightToLeft + " B3 type=" + $main.Range('B3').Value2.GetType().Name + " NumberFormat(K3)=" + $main.Range('K3').NumberFormat)

        # pagination as Excel computes it for printing (manual + automatic breaks)
        $starts = @()
        $count = $main.HPageBreaks.Count
        for ($i = 1; $i -le $count; $i++) {
            $row = $main.HPageBreaks.Item($i).Location.Row
            $type = $main.HPageBreaks.Item($i).Type
            $bale = $main.Cells.Item($row, 1).Value2
            $starts += ("row " + $row + " (bale " + $bale + ", type " + $type + ")")
        }
        Out ("  HPageBreaks (" + $count + "): " + ($starts -join '; '))
        try { Out ("  Pages.Count=" + $ps.Pages.Count) } catch { Out ("  Pages.Count unavailable: " + $_.Exception.Message) }
        $pdf = Join-Path $Dir ($name -replace '\.xlsx$', '.pdf')
        $main.ExportAsFixedFormat(0, $pdf)
        Out ("  exported main sheet print to " + $pdf)
        $wb.Close($false)
    }
    $blank.Close($false)
}
finally {
    try { $xl.Calculation = -4105 } catch { }
    $xl.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($xl) | Out-Null
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    Remove-Item $tampered -ErrorAction SilentlyContinue
}
