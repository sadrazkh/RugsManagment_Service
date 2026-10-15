# ویرایش‌های دستیِ migration «Shipments» (M3)

اگر این migration طبق قاعدهٔ «regenerate» (spec B.15) حذف و با همین نام دوباره ساخته شد
(`dotnet ef migrations add Shipments --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Web --output-dir Persistence/Migrations`)،
این دو ویرایش را دوباره اعمال کنید. بعد:

- `dotnet ef migrations has-pending-model-changes` باید «No changes» بدهد (هیچ‌کدام از دو ویرایش در مدل نیست، پس snapshot دست نمی‌خورد)؛
- `dotnet ef migrations script RugModelV2AndCosting Shipments` نباید هیچ خطی با «xmin» داشته باشد و باید دقیقاً یک `ADD CONSTRAINT "UX_Bales_Shipment_Number"` داشته باشد.

## ۱. حذف ستون xmin از سه CreateTable

در `CreateTable` جدول‌های `Shipments`، `Bales` و `ShipmentLines` خط زیر را حذف کنید (به‌جایش یک توضیح «ویرایش دستی» مانده):

```csharp
xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
```

xmin ستون سیستمی پستگرس است؛ ساختنش خطا می‌دهد و CI اگر در اسکریپت migration، DDL مربوط به xmin ببیند شکست می‌خورد. مدل (snapshot) همچنان آن را توکن هم‌زمانی (`IsRowVersion`) می‌شناسد. `ShipmentLineValues` (مشتق‌شده) و `DeclaredValueRates` (مهر جدول در ترجیحات) اصلاً xmin ندارند.

## ۲. قید یکتای تعویق‌پذیرِ شمارهٔ نگله

بلافاصله بعد از `CreateTable("Bales")` و پیش از `CreateTable("ShipmentLines")`:

```csharp
migrationBuilder.Sql("""
    ALTER TABLE "Bales"
        ADD CONSTRAINT "UX_Bales_Shipment_Number" UNIQUE ("ShipmentId", "Number") DEFERRABLE INITIALLY DEFERRED;
    """);
```

چرا: شمارهٔ نگله در هر محموله یکتاست (B.9)، ولی «شماره‌گذاری دوباره» (۱، ۳، ۴ ← ۱، ۲، ۳) و جابه‌جایی دو شماره در **یک** flush انجام می‌شوند (`BaleService.RenumberAsync` داخل `IUnitOfWork.ExecuteInTransactionAsync`). ایندکس یکتای معمولی ردیف به ردیف سنجیده می‌شود و وقتی دو نگله موقتاً یک شماره دارند خطا می‌داد؛ قید `DEFERRABLE INITIALLY DEFERRED` در پایان تراکنش سنجیده می‌شود. پستگرس ایندکس یکتا را تعویق‌پذیر نمی‌سازد، فقط قید را؛ پس این قید در مدل EF نیست (`BaleConfiguration` فقط ایندکس غیریکتای `(TenantId, ShipmentId)` را دارد) و `has-pending-model-changes` آن را نمی‌بیند.

Down لازم ندارد: `DropTable("Bales")` قید را هم می‌برد.

## چیزهایی که عمداً **ویرایش نشده‌اند**

- کلیدهای خارجی `ShipmentLines → Rugs / Bales / Shipments` بی `onDelete` ساخته می‌شوند، یعنی `NO ACTION` پستگرس (B.13): در پایان دستور سنجیده می‌شود، پس حذف آبشاری کارگاه همچنان کار می‌کند ولی فرشِ خط‌دار حذف سخت نمی‌شود و نگلهٔ غیرخالی حذف نمی‌شود.
- هیچ دادهٔ موجودی جابه‌جا نمی‌شود و `[RequiresBackup]` ندارد (B.15).
