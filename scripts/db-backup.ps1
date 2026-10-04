<#
.SYNOPSIS
    پشتیبان کامل دیتابیس سامانهٔ فرش با pg_dump (قالب custom) در backups/rugs_<زمان>.dump

.DESCRIPTION
    پیش از هر دیپلوی که migration‌اش [RequiresBackup] دارد اجرا کنید؛ بعد شناسهٔ همان migration را
    در Database:ConfirmedBackupFor (متغیر محیطی Database__ConfirmedBackupFor) بگذارید تا استارت ادامه دهد.

    مقادیر اتصال به ترتیب از پارامترها، متغیرهای محیطی استاندارد پستگرس (PGHOST، PGPORT، PGUSER،
    PGDATABASE، PGPASSWORD) و در آخر پیش‌فرض‌های محلی (localhost:5432، postgres، rugs_management) خوانده می‌شوند.
    اگر رمز داده نشود، pg_dump خودش از ~/.pgpass (pgpass.conf در ویندوز) می‌خواند یا می‌پرسد.

    pg_dump از -PgDump، متغیر PG_DUMP، PATH یا پوشهٔ نصب پیش‌فرض ویندوز (C:\Program Files\PostgreSQL\*\bin) پیدا می‌شود.
    با -Container، pg_dump داخل کانتینر Docker پستگرس اجرا می‌شود (وقتی روی میزبان نصب نیست).

    بازگردانی: pg_restore --clean --if-exists --no-owner -d <دیتابیس> backups\rugs_....dump

.EXAMPLE
    .\scripts\db-backup.ps1
.EXAMPLE
    .\scripts\db-backup.ps1 -DbHost 10.0.0.5 -Database rugs_management -User postgres -Password '...'
.EXAMPLE
    .\scripts\db-backup.ps1 -Container rugs_postgres
#>
[CmdletBinding()]
param(
    # «Host» نام متغیر خودکار PowerShell است، پس DbHost
    [string]$DbHost = $(if ($env:PGHOST) { $env:PGHOST } else { 'localhost' }),
    [int]$Port = $(if ($env:PGPORT) { [int]$env:PGPORT } else { 5432 }),
    [string]$User = $(if ($env:PGUSER) { $env:PGUSER } else { 'postgres' }),
    [string]$Database = $(if ($env:PGDATABASE) { $env:PGDATABASE } else { 'rugs_management' }),
    [string]$Password = $env:PGPASSWORD,
    [string]$OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'backups'),
    [string]$PgDump = $env:PG_DUMP,
    [string]$Container = $env:RUGS_PG_CONTAINER
)

$ErrorActionPreference = 'Stop'

function Find-PgTool([string]$name, [string]$explicit) {
    if ($explicit) { return $explicit }
    $onPath = Get-Command $name -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    # نصب پیش‌فرض ویندوز؛ جدیدترین نسخه اول (pg_dump قدیمی‌تر از سرور کار نمی‌کند)
    $installed = Get-ChildItem "C:\Program Files\PostgreSQL\*\bin\$name.exe" -ErrorAction SilentlyContinue |
        Sort-Object { [int]($_.Directory.Parent.Name -replace '\D', '') } -Descending |
        Select-Object -First 1
    if ($installed) { return $installed.FullName }
    throw "$name not found. Install the PostgreSQL client tools, pass -PgDump, or use -Container <docker-container>."
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
# فرهنگ ثابت: با فرهنگ fa سال شمسی و رقم غیر ASCII در نام فایل می‌آمد
$stamp = [DateTime]::Now.ToString('yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)
$outFile = Join-Path (Resolve-Path $OutDir) "rugs_$stamp.dump"
# هرگز روی پشتیبان قبلی (اجرای دوم در همان ثانیه) نمی‌نویسیم
if (Test-Path $outFile) { throw "Backup file already exists, not overwriting: $outFile" }

if ($Container) {
    # خروجی باینری را از stdout نمی‌گیریم (PowerShell 5 آن را متن فرض می‌کند و خراب می‌کند)؛
    # داخل کانتینر در فایل نوشته و با docker cp بیرون آورده می‌شود
    $inside = "/tmp/rugs_$stamp.dump"
    Write-Host "pg_dump inside container '$Container' ($Database)..."
    $envArgs = @()
    if ($Password) { $envArgs = @('-e', "PGPASSWORD=$Password") }
    & docker exec @envArgs $Container pg_dump --format=custom --username=$User --dbname=$Database --file=$inside
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed inside container (exit $LASTEXITCODE)." }
    & docker cp "${Container}:$inside" $outFile
    if ($LASTEXITCODE -ne 0) { throw "docker cp failed (exit $LASTEXITCODE)." }
    & docker exec $Container rm -f $inside | Out-Null
}
else {
    $pgDumpExe = Find-PgTool 'pg_dump' $PgDump
    Write-Host "pg_dump $User@${DbHost}:$Port/$Database -> $outFile"
    $previousPassword = $env:PGPASSWORD
    try {
        if ($Password) { $env:PGPASSWORD = $Password }
        & $pgDumpExe --format=custom --host=$DbHost --port=$Port --username=$User --dbname=$Database --file=$outFile
        if ($LASTEXITCODE -ne 0) { throw "pg_dump failed (exit $LASTEXITCODE)." }
    }
    finally {
        $env:PGPASSWORD = $previousPassword
    }

    # فایل نیمه‌کاره نباید «پشتیبان سالم» حساب شود: فهرست محتوای آرشیو باید خوانا باشد
    $pgRestoreExe = Join-Path (Split-Path -Parent $pgDumpExe) 'pg_restore'
    if (Test-Path "$pgRestoreExe*") {
        & $pgRestoreExe --list $outFile | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Backup archive is not readable: $outFile" }
    }
}

$size = (Get-Item $outFile).Length
if ($size -le 0) { throw "Backup file is empty: $outFile" }
Write-Host ("Backup written: {0} ({1:N0} bytes)" -f $outFile, $size)
Write-Host "Restore with: pg_restore --clean --if-exists --no-owner -d <database> `"$outFile`""
