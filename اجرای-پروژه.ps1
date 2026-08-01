# ═══════════════════════════════════════════════════════════════
# اسکریپت راه‌اندازی سامانه مدیریت فرش (Windows PowerShell)
# ═══════════════════════════════════════════════════════════════
# نحوه اجرا: راست‌کلیک → Run with PowerShell
# یا در ترمینال:  .\اجرای-پروژه.ps1
#
# معماری: یک اپلیکیشن یکپارچه (ASP.NET MVC + جزیره‌های Vue) روی یک پورت.
# فرانت فقط زمان build اجرا می‌شود؛ در تولید سرور جداگانه‌ای لازم نیست.

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$webPath = Join-Path $root "src\RugsManagment.Web"
$clientAppPath = Join-Path $webPath "ClientApp"
$port = 5299

Write-Host ""
Write-Host "=== سامانه مدیریت فرش ===" -ForegroundColor Cyan
Write-Host ""

# بستن نمونهٔ قبلی که فایل‌های build را قفل می‌کند
Get-Process -Name "RugsManagment.Web" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# ── ۱) PostgreSQL (اختیاری با Docker) ─────────────────────────
Write-Host "[1] PostgreSQL..." -ForegroundColor Yellow
try {
    docker info 2>$null | Out-Null
    Set-Location $root
    docker compose up -d 2>$null
    if ($LASTEXITCODE -eq 0) { Write-Host "    Docker Postgres بالا آمد." -ForegroundColor Green }
} catch {
    Write-Host "    Docker خاموش است — اگر Postgres محلی نصب دارید همان استفاده می‌شود." -ForegroundColor DarkYellow
}

# ── ۲) build جزیره‌های Vue ────────────────────────────────────
Write-Host "[2] build فرانت (جزیره‌های Vue)..." -ForegroundColor Yellow
Set-Location $clientAppPath
if (-not (Test-Path (Join-Path $clientAppPath "node_modules"))) {
    Write-Host "    نصب وابستگی‌های npm (فقط بار اول)..." -ForegroundColor DarkGray
    npm install
}
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "    build فرانت ناموفق بود." -ForegroundColor Red
    Set-Location $root
    exit 1
}
Write-Host "    خروجی در wwwroot/dist آماده شد." -ForegroundColor Green

# ── ۳) اجرای اپلیکیشن ─────────────────────────────────────────
Write-Host "[3] اجرای اپلیکیشن (پورت $port)..." -ForegroundColor Yellow
Write-Host ""
Write-Host "مرورگر را باز کنید:" -ForegroundColor Cyan
Write-Host "  >>>  http://localhost:$port  <<<" -ForegroundColor White -BackgroundColor DarkGreen
Write-Host ""
Write-Host "ورود دمو:     demo@rugsystem.local / Demo@12345" -ForegroundColor Gray
Write-Host "ادمین سیستم:  admin@rugsystem.local / Admin@12345" -ForegroundColor Gray
Write-Host ""
Write-Host "برای توسعهٔ فرانت با HMR: در ترمینالی جدا داخل ClientApp دستور 'npm run dev' را بزنید" -ForegroundColor DarkGray
Write-Host "و در appsettings.Development.json کلید Vite:DevServer را روی http://localhost:5174 بگذارید." -ForegroundColor DarkGray
Write-Host ""

Set-Location $webPath
dotnet run --no-launch-profile --urls "http://localhost:$port"

Set-Location $root
