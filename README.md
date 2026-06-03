# سامانه مدیریت فرش (Rugs Management)

سیستم چندمستاجری (Multi-tenant) برای مدیریت گردش کار فرش از تولید تا آماده‌سازی برای فروش.

## راهنمای فارسی (برای غیربرنامه‌نویس)

توضیح کامل «چه چیزی کجا و چرا» در فایل [docs/راهنمای-کد-فارسی.md](docs/راهنمای-کد-فارسی.md) و **کامنت‌های فارسی داخل هر فایل منبع** (`///` در C# و `/** */` در Vue/TS).

## معماری

```
src/
  RugsManagment.Domain/          موجودیت‌ها و قوانین دامنه
  RugsManagment.Application/     سرویس‌ها، DTO، موتور گردش کار و محاسبات
  RugsManagment.Infrastructure/  EF Core + PostgreSQL، JWT، Repository
  RugsManagment.Api/             REST API
client/                          Vue 3 + Vite + PWA + Tailwind (RTL)
```

- **موتور گردش کار (`IWorkflowEngine`)**: مسیر داینامیک هر فرش — قالب از پیش‌تعریف‌شده، مراحل اختیاری (مثل دارکشی)، یا مسیر سفارشی.
- **محاسبات (`ICostCalculationService`)**: هزینه ثابت، متری مربع، و override دستی — بدون تکرار در فرانت.
- **چندمستاجری**: ادمین سیستم کارگاه (Tenant) می‌سازد؛ هر کارگاه قالب‌ها و فرش‌های خود را دارد.

## پیش‌نیاز

- .NET 10 SDK
- Node.js 20+
- Docker (برای PostgreSQL)

## راه‌اندازی

### 1. دیتابیس

```powershell
docker compose up -d
```

### 2. API

```powershell
cd src/RugsManagment.Api
dotnet run
```

Swagger: `http://localhost:5280/swagger`

### 3. فرانت‌اند

```powershell
cd client
npm install
npm run dev
```

مرورگر: `http://localhost:5173` (پروکسی به API)

## حساب‌های پیش‌فرض (Seed)

| نقش | ایمیل | رمز |
|-----|--------|-----|
| ادمین سیستم | admin@rugsystem.local | Admin@12345 |
| کارگاه دمو | demo@rugsystem.local | Demo@12345 |

## API اصلی

- `POST /api/auth/login`
- `GET /api/dashboard` (نیاز به Tenant)
- `GET/POST /api/rugs`
- `POST /api/rugs/{id}/steps/{stepId}/advance` | `skip`
- `GET/POST /api/workflows/templates`
- `GET/POST /api/tenants` (فقط SystemAdmin)

## PWA

اپلیکیشن به‌صورت PWA نصب‌پذیر است (موبایل و تبلت). Service Worker با `vite-plugin-pwa` فعال است.

## توسعه

- Migration جدید: `dotnet ef migrations add Name --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Api`
- لایه Application قابل استخراج به میکروسرویس جدا است؛ وابستگی فقط به Domain و قراردادهای Persistence.
