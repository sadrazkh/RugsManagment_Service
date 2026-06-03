# ═══════════════════════════════════════════════════════════════
# اسکript راه‌اندازی سامانه مدیریت فرش (Windows PowerShell)
# ═══════════════════════════════════════════════════════════════
# نحوه اجرا: راست‌کلیک → Run with PowerShell
# یا در ترمینال:  .\اجرای-پروژه.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host ""
Write-Host "=== سامانه مدیریت فرش ===" -ForegroundColor Cyan
Write-Host ""

# بستن نمونهٔ قبلی API که باعث خطای build می‌شود
Get-Process -Name "RugsManagment.Api" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# 1) PostgreSQL (اختیاری با Docker)
Write-Host "[1] PostgreSQL..." -ForegroundColor Yellow
try {
    docker info 2>$null | Out-Null
    Set-Location $root
    docker compose up -d 2>$null
    if ($LASTEXITCODE -eq 0) { Write-Host "    Docker Postgres بالا آمد." -ForegroundColor Green }
} catch {
    Write-Host "    Docker خاموش است — اگر Postgres نصب دارید همان را استفاده کنید." -ForegroundColor DarkYellow
}

# 2) API
Write-Host "[2] API (پورت 5280)..." -ForegroundColor Yellow
$apiPath = Join-Path $root "src\RugsManagment.Api"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$apiPath'; Write-Host 'API: http://localhost:5280/swagger' -ForegroundColor Green; dotnet run --launch-profile http"

Start-Sleep -Seconds 6

# 3) فرانت Vue
Write-Host "[3] فرانت (پورت 5173)..." -ForegroundColor Yellow
$clientPath = Join-Path $root "client"
if (-not (Test-Path (Join-Path $clientPath "node_modules"))) {
    Set-Location $clientPath
    npm install
}
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$clientPath'; Write-Host 'اپ: http://localhost:5173' -ForegroundColor Green; npm run dev"

Write-Host ""
Write-Host "مرورگر را باز کنید:" -ForegroundColor Cyan
Write-Host "  >>>  http://localhost:5173  <<<" -ForegroundColor White -BackgroundColor DarkGreen
Write-Host ""
Write-Host "ورود دمو: demo@rugsystem.local / Demo@12345" -ForegroundColor Gray
Write-Host "Swagger API: http://localhost:5280/swagger" -ForegroundColor Gray
Write-Host ""
Write-Host "توجه: آدرس http://localhost:5280 فقط API است — صفحه اپ نیست!" -ForegroundColor DarkYellow
Write-Host ""

Set-Location $root
