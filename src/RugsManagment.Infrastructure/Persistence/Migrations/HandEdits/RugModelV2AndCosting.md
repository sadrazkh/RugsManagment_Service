# ویرایش‌های دستیِ migration «RugModelV2AndCosting» (M2)

اگر این migration طبق قاعدهٔ «regenerate» (spec B.15) حذف و با همین نام دوباره ساخته شد (`dotnet ef migrations add RugModelV2AndCosting --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Web`)، همهٔ ویرایش‌های زیر را دوباره اعمال کنید. بعد:

- `dotnet ef migrations has-pending-model-changes` باید «No changes» بدهد؛
- `dotnet ef migrations script <M1> RugModelV2AndCosting` را با نسخهٔ تولیدیِ EF مقایسه کنید: همهٔ دستورهای schema باید عیناً همان باشند (فقط ترتیب و SQL داده فرق دارد) و هیچ خطی «xmin» نداشته باشد؛
- شناسهٔ migration (برچسب زمانی) عوض می‌شود؛ مقدار `Database:ConfirmedBackupFor` روی سرور باید شناسهٔ تازه باشد.

خودِ متن SQL هر ویرایش در فایل migration است؛ این‌جا جا و دلیل هر کدام آمده است.

## ۰. `[RequiresBackup("Rugs")]` روی کلاس

`using RugsManagment.Infrastructure.Persistence.Seed;` و `[RequiresBackup("Rugs")]` روی `partial class RugModelV2AndCosting` (در فایل اصلی، نه Designer). ابعاد قدیمی گرد می‌شوند؛ روی دیتابیس پُر استارت تا تأیید پشتیبان متوقف می‌شود (B.15).

## ۱. ترتیب ابعاد (B.4 «M2 dimension order»)

EF ستون‌ها را به ترتیب خودش می‌چیند؛ Up دستی به این ترتیب بازچیده شد، همه در یک تراکنش:

1. `CREATE TABLE IF NOT EXISTS "_m2_rug_dims_backup" ("RugId" uuid PK, "Length" numeric, "Width" numeric)` — بدون کلید خارجی (حذف سخت فرش در برگشت ورود را نبندد) و `IF NOT EXISTS` چون Down جدول را نگه می‌دارد.
2. `INSERT … SELECT "Id","LengthMeters","WidthMeters" FROM "Rugs" ON CONFLICT ("RugId") DO UPDATE` — بعد از یک Down و Up دوباره، پشتیبان ابعادِ درست پیش از همین اجرا را دارد.
3. یک `DO` که تعداد فرش‌هایی را که با گرد کردن عوض می‌شوند با `RAISE NOTICE` اعلام و در `COMMENT ON TABLE "_m2_rug_dims_backup"` ثبت می‌کند (لاگ ماندگار؛ EF نوتیس را لاگ نمی‌کند).
4. `UPDATE "Rugs" SET "LengthMeters" = round(…, 2), "WidthMeters" = round(…, 2)` (round پستگرس روی numeric نصفه را دور از صفر می‌برد).
5. دو `AlterColumn` به `numeric(9,2)`.
6. `AddColumn` `IntakeLength`/`IntakeWidth` (پیش‌فرض ۰) و بلافاصله `UPDATE "Rugs" SET "IntakeLength" = "LengthMeters", "IntakeWidth" = "WidthMeters"`.
7. ستون‌های نهایی و `Quantity` (پیش‌فرض ۱) — `Quantity` پیش از `TotalAreaM2` که به آن وابسته است.
8. سه ستون تولیدشده (`AreaM2`، `IntakeAreaM2`، `TotalAreaM2`) — بعد از ALTER نوع، چون پستگرس نوعِ ستونی را که ستون تولیدشده به آن وابسته است عوض نمی‌کند، و بعد از پر شدن اندازهٔ اول تا جدول دو بار بازنویسی نشود.

## ۲. شمارهٔ ورودی‌ها (RugBatch)

بین `AddColumn("Number")` و `CreateIndex("IX_RugBatches_TenantId_Number")`:

- `UPDATE "RugBatches" … SET "Number" = row_number() OVER (PARTITION BY "TenantId" ORDER BY "CreatedAt", "Id"), "SellerKey" = ''` — ورودی‌های موجود به ترتیب ساخت شماره می‌گیرند؛ فروشنده ندارند (کلید خالی). `ReceivedOn` عمداً خالی می‌ماند: چند ورودی قدیمی در یک روز با کلید خالی در ایندکس «یک ورودی برای هر فروشنده در هر روز» (NULLS NOT DISTINCT) به هم می‌خوردند.
- `INSERT INTO "TenantCounters" … 'batch', max("Number") … ON CONFLICT DO UPDATE SET "Value" = GREATEST(…)` — شمارنده از بزرگ‌ترین شماره ادامه می‌دهد.

شمارندهٔ `sku:*` دست نمی‌خورد (اولین استفاده از بیشینهٔ موجود seed می‌شود، M0)، پس SKUها همان‌طور کار می‌کنند.

## ۳. قیمت خرید قدیمی ← سطر خرید

بعد از `CreateTable("RugPurchaseLines")` و ایندکس‌هایش: برای هر فرش (حتی سطل‌زباله) با `PurchaseCost IS NOT NULL` یک سطر `Basis = 0` (Total)، برچسب «قیمت خرید»، `Rate = round(PurchaseCost, 4)`، `Amount = round(Rate, MoneyScale کارگاه)` (پیش‌فرض ۰ اگر ردیف ترجیحات نیست)، `CategoryId = NULL` (نوع‌های seed بعد از migration ساخته می‌شوند)، `CreatedAt = now()`. شناسه با `gen_random_uuid()`. برچسب عمداً ثابت نوشته شده، نه از ثابت کد، تا migration تاریخی عوض نشود. ستون `PurchaseCost` دست نمی‌خورد (M7 حذفش می‌کند).

## ۴. بررسی خودکار (self-verify) و عکس بهای فروش

در پایان Up، در همان تراکنش:

1. `CREATE TEMP TABLE "_m2_totals" ON COMMIT DROP AS …`: برای هر فرش، جمع قدیم = `COALESCE(PurchaseCost,0)` + Σ `GREATEST(0, COALESCE(ManualCostOverride, CalculatedCost, 0) + COALESCE(Adjustment, 0))` روی مراحل `Status IN (1, 2)` (InProgress، Completed)؛ جمع تازه = Σ `Amount` سطرهای خرید + همان مراحل. فرمول مرحله عمداً در migration تکرار شده (migration تاریخی است و نباید با تغییر CostSql عوض شود؛ آزمون معماری «یک جای فرمول» باید پوشهٔ Migrations را استثنا کند).
2. یک `DO`: اگر `LengthMeters <> COALESCE(FinalLength, IntakeLength)` یا مشابهش برای عرض روی هر ردیفی برقرار باشد، یا جمع تازه با جمع قدیم حتی یک ریال فرق کند → `RAISE EXCEPTION` با تعداد و تا پنج SKU نمونه (و HINT: معمولاً قیمت خریدی با رقم اعشار بیش از MoneyScale). کل migration برمی‌گردد.
3. `UPDATE "RugSales" SET "CostAtSale" = new_total, "CostAtSaleAt" = "SoldAt"` برای فروش‌های موجود (D.6).

ردیف‌های `RugCostTotals` ساخته نمی‌شوند؛ `LedgerStartupCheck` (بررسی استارت، Order 200) آن‌ها را می‌سازد.

## ۵. xmin

ستون سیستمی xmin در سه جا حذف شد (Npgsql 10 خودش DDL آن را نمی‌نویسد، ولی قاعدهٔ پروژه حذف دستی است): `AddColumn<uint>("xmin", "RugBatches", …)` در Up، ستون `xmin` در `CreateTable("CostCategories")`، و `DropColumn("xmin", "RugBatches")` در Down. مدل (snapshot) همچنان آن را توکن هم‌زمانی می‌شناسد. در هیچ متن SQL این migration کلمهٔ «xmin» نیامده (CI روی اسکریپت grep می‌کند).

## ۶. Down

- اول: `UPDATE "Rugs" r SET "PurchaseCost" = (SELECT sum("Amount") … WHERE "RugId" = r."Id")` برای **همهٔ** فرش‌ها (ویرایش‌های بعد از M2 با حذف جدول گم نشود). فرش بی‌سطر NULL می‌گیرد: Up هر قیمت غیر NULL را سطر کرد و بعد از M2 هیچ کدی ستون قدیمی را نمی‌نویسد (`[Obsolete]`)، پس فرش بی‌سطر یا پیش از M2 هم NULL بود یا کاربر همهٔ سطرهایش را پاک کرده؛ نسخهٔ اول این بند ستون فرش بی‌سطر را نگه می‌داشت و قیمت پاک‌شده را برمی‌گرداند (بازبینی فاز ۲، یافتهٔ M2.Down). ویرایش بعد از اجرای M2 روی دیتابیس واقعی است و فقط Down را عوض می‌کند؛ Up دست نخورده.
- `DELETE FROM "TenantCounters" WHERE "Key" = 'batch'` (شماره‌ها با ستونشان می‌روند).
- ستون‌های تولیدشده پیش از `AlterColumn` ابعاد حذف می‌شوند.
- بعد از برگرداندن ابعاد به `numeric`: `UPDATE "Rugs" … FROM "_m2_rug_dims_backup"` فقط برای فرشی که جفت فعلی‌اش هنوز همان گردشدهٔ اصل است — اندازه‌ای که کاربر بعد از M2 نوشته (از جمله اندازهٔ نهایی، که حالا جفت تکِ قدیمی می‌شود) نگه داشته می‌شود.
- جدول `_m2_rug_dims_backup` حذف نمی‌شود؛ فقط M7 حذفش می‌کند (B.15).
