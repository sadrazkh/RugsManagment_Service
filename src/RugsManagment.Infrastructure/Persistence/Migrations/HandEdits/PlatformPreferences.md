# ویرایش‌های دستیِ migration «PlatformPreferences» (M1)

اگر این migration طبق قاعدهٔ «regenerate» (spec B.15) حذف و با همین نام دوباره ساخته شد، این چهار ویرایش را دوباره اعمال کنید و بعد `dotnet ef migrations has-pending-model-changes` باید خالی باشد.

## ۱. حذف ستون xmin از هر دو CreateTable

در `CreateTable` جدول‌های `TenantGridLayouts` و `TenantPreferences` خط زیر را حذف کنید (xmin ستون سیستمی پستگرس است و ساختنش خطا می‌دهد؛ CI هم اگر در اسکریپت migration، DDL مربوط به xmin ببیند شکست می‌خورد):

```csharp
xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
```

## ۲. پیش‌فرض نقش برای کاربران موجود

بلافاصله بعد از `AddColumn`های `GridEnter`، `TextScale` و `UiMode` روی `Users`:

```csharp
migrationBuilder.Sql("""
    UPDATE "Users" SET
        "UiMode" = CASE WHEN "Role" IN (2, 3) THEN 0 ELSE 1 END,
        "TextScale" = CASE WHEN "Role" = 3 THEN 1 ELSE 0 END;
    """);
```

باید دقیقاً همان `User.ApplyRoleDefaultPreferences` باشد (آزمون یکپارچهٔ `PlatformPreferencesMigrationTests` بررسی می‌کند).

## ۳. ردیف ترجیحات برای هر کارگاه موجود

بعد از `CreateTable("TenantPreferences")`، یک `INSERT … SELECT … FROM "Tenants" ON CONFLICT ("TenantId") DO NOTHING` با پیش‌فرض‌های `TenantPreferences.CreateDefault` (متن کامل در خود migration). مقدارها ثابت نوشته می‌شوند، نه از ثابت‌های کد، تا migration تاریخی با تغییر بعدی کد عوض نشود.

## ۴. Exportable

`AddColumn<bool>("Exportable", …)` باید `defaultValue: true` داشته باشد. EF خودش همین را از `HasDefaultValue(true)` مدل می‌سازد؛ فقط بررسی کنید که `false` نشده باشد.
