# سامانه مدیریت فرش (Rugs Management)

سیستم چندمستأجری (Multi-tenant) برای مدیریت گردش کار فرش از پذیرش/تولید تا آماده‌سازی برای فروش.

## معماری

یک اپلیکیشن یکپارچه ASP.NET MVC/Razor که **Vue را به‌صورت «جزیره» (island)** — مانند jQuery — درون viewها استفاده می‌کند. **هیچ سرور جداگانه‌ای برای فرانت در تولید لازم نیست.**

```
src/
  RugsManagment.Domain/          موجودیت‌ها و قوانین دامنه
  RugsManagment.Application/     سرویس‌ها، DTO، موتور گردش کار و محاسبهٔ هزینه
  RugsManagment.Infrastructure/  EF Core + PostgreSQL، Repository، Seed
  RugsManagment.Web/             ← میزبان: MVC + Razor + کنترلرهای /api + جزیره‌های Vue
      ClientApp/                 پروژهٔ Vite (فقط زمان build؛ در تولید اجرا نمی‌شود)
      wwwroot/dist/              خروجی build جزیره‌ها (توسط Razor از manifest خوانده می‌شود)
tests/
  RugsManagment.Tests/             تست‌های واحد (بدون دیتابیس)
  RugsManagment.IntegrationTests/  تست‌های یکپارچه روی PostgreSQL واقعی (migration، شمارنده‌ها)
scripts/
  db-backup.ps1 / db-backup.sh     پشتیبان کامل دیتابیس با pg_dump
```

- **جزیره‌های Vue:** در هر view کافی است `<div data-island="نام" data-props='{...}'>` بگذارید؛ فایل `ClientApp/src/main.ts` آن را پیدا و mount می‌کند. کلاس `Frontend/ViteAssets.cs` فایل‌های هش‌دار را از `manifest.json` می‌خواند.
- **احراز هویت:** مبتنی بر Cookie (طبیعی برای MVC). نقش‌ها: `SystemAdmin`، `TenantAdmin`، `Operator`. جداسازی کامل مستأجر.
- **APIها:** با هدر `X-CSRF-TOKEN` محافظت می‌شوند و همیشه به کارگاه کاربر محدودند.
- **موتور هزینه (`ICostCalculationService`):** ثابت، ×طول، ×عرض، ×مساحت، ترکیبی، دستی + تخفیف/اضافه — همه در بک‌اند.
- **موتور گردش کار (`IWorkflowEngine`):** مسیر داینامیک هر فرش، قالب‌های قابل‌ویرایش، حرکت جلو/عقب.
- **PWA:** نصب‌پذیر روی موبایل/تبلت (`manifest.webmanifest` + `sw.js`).

## پیش‌نیاز

- .NET 10 SDK
- Node.js 20+ (فقط برای build فرانت)
- PostgreSQL (لوکال یا Docker)

## راه‌اندازی

### ۱) دیتابیس

با Docker:

```bash
docker compose up -d
```

رمز Postgres در `docker-compose.yml` با رمز رشتهٔ اتصال `appsettings.json` یکی است؛ اگر یکی را عوض کردید دیگری را هم عوض کنید.

یا از PostgreSQL محلی خود استفاده کنید و رشتهٔ اتصال را در
`src/RugsManagment.Web/appsettings.json` (کلید `ConnectionStrings:DefaultConnection`) تنظیم کنید.
لازم نیست جدولی بسازید؛ بخش «دیتابیس و migration» را ببینید.

### ۲) build فرانت (جزیره‌های Vue)

```bash
cd src/RugsManagment.Web/ClientApp
npm install
npm run build          # خروجی در ../wwwroot/dist
```

> در حین توسعهٔ فرانت با HMR: `npm run dev` و در `appsettings.Development.json`
> کلید `Vite:DevServer` را روی `http://localhost:5174` بگذارید. برای تولید فقط `npm run build` کافی است.

### ۳) اجرای برنامه

```bash
cd src/RugsManagment.Web
dotnet run
```

هنگام استارت، migrationها و دادهٔ اولیه (ادمین سیستم، کارگاه دمو، قالب‌ها و — روی دیتابیس تازه — چند فرش نمونه) خودکار اجرا می‌شوند.
سپس آدرس نمایش‌داده‌شده (مثلاً `http://localhost:5xxx`) را باز کنید.

## دیتابیس و migration

> تا پیش از فاز ۰ این README می‌گفت migrationها هنگام استارت اجرا می‌شوند، ولی در عمل اجرا نمی‌شدند
> (`MigrateAsync` کامنت شده بود) و زنجیرهٔ migration روی دیتابیس خالی با خطای 42701 می‌شکست.
> هر دو درست شده‌اند؛ آنچه در ادامه آمده رفتار فعلی است.

**استارت برنامه** (`Infrastructure/Persistence/Seed/StartupPipeline.cs`) به این ترتیب کار می‌کند:

1. اگر خود دیتابیس وجود ندارد، یک دیتابیس خالی ساخته می‌شود.
2. یک **قفل advisory پستگرس** گرفته می‌شود، تا اگر دو نمونهٔ برنامه هم‌زمان بالا بیایند (مثلاً دیپلوی rolling) فقط یکی migration بزند و دادهٔ اولیه دو بار کاشته نشود. نمونهٔ دوم پشت قفل صبر می‌کند.
3. migrationهای در انتظار اجرا می‌شوند (`Migrator`)، سپس دادهٔ اولیه (`DatabaseSeeder`)، سپس هر `IStartupTask` ثبت‌شده در DI به ترتیب `Order`.
4. قفل آزاد می‌شود. استارت دوم هیچ چیزی را تغییر نمی‌دهد.

خطای گذرا (مثلاً دیتابیسی که هنوز بالا نیامده) ۱۰ بار با فاصلهٔ ۳ ثانیه دوباره امتحان می‌شود. سه حالت اما **فوراً** استارت را متوقف می‌کنند
(لاگ `Critical`) چون با تلاش دوباره درست نمی‌شوند: migration پشتیبان‌خواهِ تأییدنشده (پایین‌تر)، مدل EF که migration‌اش ساخته نشده،
و مقدار نامعتبر برای `Database:MigrateOnStartup`.

| تنظیم | پیش‌فرض | کاربرد |
|-------|---------|--------|
| `Database:MigrateOnStartup` | `true` | با `false` برنامه به schema دست نمی‌زند (مثلاً وقتی DBA اسکریپت را دستی اجرا می‌کند). |
| `Database:ConfirmedBackupFor` | خالی | شناسهٔ migration پشتیبان‌خواهی که پشتیبانش گرفته شده (چند شناسه با ویرگول). |

**دروازهٔ پشتیبان (`[RequiresBackup]`).** migration‌ای که دادهٔ موجود را تغییر می‌دهد یا دور می‌ریزد (تبدیل نوع ستون، ادغام
یا حذف ستونِ پُر و …) باید روی کلاسش `[RequiresBackup("نام‌جدول")]` بگیرد (برای هر جدول یک بار). اگر چنین migration‌ای در انتظار
باشد و آن جدول حداقل یک ردیف داشته باشد، برنامه بالا نمی‌آید تا وقتی که:

1. پشتیبان بگیرید: `./scripts/db-backup.ps1` (ویندوز) یا `bash scripts/db-backup.sh` (لینوکس/مک). خروجی در `backups/rugs_<زمان>.dump`
   (قالب custom؛ بازگردانی با `pg_restore --clean --if-exists --no-owner -d <دیتابیس> <فایل>`). پارامترها: میزبان/پورت/کاربر/دیتابیس/رمز
   یا متغیرهای `PGHOST`، `PGPORT`، `PGUSER`، `PGDATABASE`، `PGPASSWORD`؛ با `-Container`/`--container` داخل کانتینر Docker پستگرس اجرا می‌شود.
2. شناسهٔ همان migration (مثلاً `20261101090000_SplitRugDimensions`) را در `Database:ConfirmedBackupFor` بگذارید
   (متغیر محیطی `Database__ConfirmedBackupFor`) و دوباره اجرا کنید.

روی دیتابیس خالی (نصب تازه، CI) چیزی برای از دست رفتن نیست و دروازه مانعی ایجاد نمی‌کند.

## حساب‌های پیش‌فرض

| نقش | ایمیل | رمز |
|-----|--------|-----|
| ادمین سیستم | admin@rugsystem.local | Admin@12345 |
| مدیر کارگاه دمو | demo@rugsystem.local | Demo@12345 |

- **ادمین سیستم** کارگاه (فروشنده) می‌سازد.
- **مدیر کارگاه** فرش‌ها، گردش کار، قیمت‌گذاری، برچسب‌ها، فیلدهای سفارشی و کاربران کارگاه خود را مدیریت می‌کند.

## قابلیت‌ها

- مدیریت فرش (ثبت/ویرایش، ابعاد متری با محاسبهٔ خودکار مساحت، فیلدهای سفارشی JSONB، خرید/فروش/سود).
- گردش کار داینامیک (قالب‌ها با drag & drop، مسیر هر فرش، حرکت جلو/عقب با مودال، تاریخچه).
- موتور قیمت‌گذاری با پیش‌نمایش زندهٔ بک‌اند و تخفیف/اضافه.
- گروه‌ها/محموله‌ها + عملیات دسته‌ای (انتخاب چند فرش و پیشبرد هم‌زمان).
- طراح برچسب (بصری drag & drop + QR/بارکد + حالت HTML + چاپ).
- داشبورد (فرش بر اساس مرحله، ارزش موجودی، سود، فعالیت اخیر).

## توسعه

- ابزار EF با نسخهٔ ثابت در `.config/dotnet-tools.json` است؛ یک بار `dotnet tool restore` بزنید.
- Migration جدید:
  `dotnet ef migrations add Name --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Web`
  - اگر دادهٔ موجود را تغییر می‌دهد، `[RequiresBackup("Table")]` بگذارید (بالا را ببینید).
  - برای ستون همزمانی `xmin` هرگز `AddColumn`/`DropColumn` نگه ندارید؛ ستون سیستمی پستگرس است (`ConcurrencyAndStepIndex` را ببینید). CI روی اسکریپت migration این را بررسی می‌کند.
  - لازم نیست دستی `database update` بزنید؛ استارت بعدی برنامه اجرایش می‌کند.
- تست‌ها:
  - واحد: `dotnet test tests/RugsManagment.Tests`
  - یکپارچه (PostgreSQL واقعی): رشتهٔ اتصال پایه **بدون Database** را در `RUGS_TEST_PG` بگذارید؛ هر کلاس تست دیتابیس دورریختنی
    `rugs_it_xxxxxxxx` خودش را می‌سازد و در پایان پاک می‌کند (کاربر باید اجازهٔ CREATE DATABASE داشته باشد). بدون این متغیر تست‌ها رد
    (skip) می‌شوند، ولی در CI (`CI=true`) شکست می‌خورند.

    ```powershell
    $env:RUGS_TEST_PG = "Host=localhost;Port=5432;Username=postgres;Password=7082"
    dotnet test tests/RugsManagment.IntegrationTests
    ```
- CI (`.github/workflows/ci.yml`) کل زنجیرهٔ migration را روی Postgres خالی اجرا می‌کند، مدلِ بدون migration و DDL روی `xmin` را رد می‌کند و تست‌های یکپارچه را اجرا می‌کند.
- افزودن جزیرهٔ Vue: کامپوننت را در `ClientApp/src/islands/` بسازید و یک سطر در `registry.ts` اضافه کنید.
