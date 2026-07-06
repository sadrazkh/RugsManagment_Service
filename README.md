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

یا از PostgreSQL محلی خود استفاده کنید و رشتهٔ اتصال را در
`src/RugsManagment.Web/appsettings.json` (کلید `ConnectionStrings:DefaultConnection`) تنظیم کنید.

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

هنگام استارت، migration و دادهٔ اولیه (ادمین سیستم، کارگاه دمو، قالب‌ها و — روی دیتابیس تازه — چند فرش نمونه) اجرا می‌شود.
سپس آدرس نمایش‌داده‌شده (مثلاً `http://localhost:5xxx`) را باز کنید.

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

- Migration جدید:
  `dotnet ef migrations add Name --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Web`
- افزودن جزیرهٔ Vue: کامپوننت را در `ClientApp/src/islands/` بسازید و یک سطر در `registry.ts` اضافه کنید.
