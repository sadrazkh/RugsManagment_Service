# دیپلوی روی CapRover

این پروژه یک اپلیکیشن یکپارچه است (بک‌اند + فرانت در یک ایمیج). با `Dockerfile` و `captain-definition`
موجود در ریشهٔ ریپو، روی CapRover به‌سادگی دیپلوی می‌شود. SSL و پراکسی را CapRover مدیریت می‌کند؛
اپ داخل کانتینر روی پورت **80** گوش می‌دهد.

## ۱) دیتابیس

از همان PostgreSQL که روی CapRover دارید استفاده کنید. یک دیتابیس برای این اپ بسازید
(مثلاً `rugs_management`)؛ اگر کاربر اتصال اجازهٔ `CREATEDB` داشته باشد، اپ در اولین استارت خودش دیتابیسِ خالی را می‌سازد.
جدول‌ها را هرگز دستی نسازید — کار migrationهای خودکار است (بخش ۴). نام میزبان داخلی PostgreSQL در CapRover به‌شکل زیر است:

```
srv-captain--<نام-اپ-پستگرس>
```

رشتهٔ اتصال نهایی (Npgsql):

```
Host=srv-captain--postgres;Port=5432;Database=rugs_management;Username=postgres;Password=<رمز>
```

## ۲) ساخت اپ و تنظیمات

در پنل CapRover یک اپ جدید بسازید (مثلاً `rugs`)، سپس در تب **App Configs**:

### متغیرهای محیطی (Environmental Variables)

| کلید | مقدار |
|------|-------|
| `ConnectionStrings__DefaultConnection` | رشتهٔ اتصال بالا |
| `DataProtection__KeyPath` | `/data/protection-keys` |
| `Storage__ImagePath` | `/data/uploads` — محل ذخیرهٔ عکس فرش‌ها |
| `ASPNETCORE_ENVIRONMENT` | `Production` (پیش‌فرض ایمیج همین است) |
| `Database__MigrateOnStartup` *(اختیاری)* | پیش‌فرض `true`؛ با `false` اپ به schema دست نمی‌زند (بخش ۵) |
| `Database__ConfirmedBackupFor` *(فقط وقتی لازم شد)* | شناسهٔ migration پشتیبان‌خواهی که پشتیبانش را گرفته‌اید (بخش ۵) |

> نکته: دو زیرخط (`__`) در نام متغیرها معادل نقطه در پیکربندی .NET است — دقیقاً همین‌طور وارد کنید.

### دایرکتوری ماندگار (Persistent Directories)

یک ردیف اضافه کنید:

- **Path in App:** `/data`
- **Label:** `rugs-data`

این دو چیز را بین دیپلوی‌ها حفظ می‌کند:

- **کلیدهای Data Protection** (`/data/protection-keys`) — وگرنه هر بار همهٔ کاربران از حساب خارج می‌شوند.
- **عکس‌های فرش** (`/data/uploads`) — وگرنه با هر دیپلوی همهٔ عکس‌ها پاک می‌شوند.

> ⚠️ اگر این دایرکتوری ماندگار را تنظیم نکنید، عکس‌های آپلودشده با اولین redeploy از بین می‌روند
> (رکورد در دیتابیس می‌ماند ولی فایل نیست و تصویر شکسته نمایش داده می‌شود).

### پورت و HTTPS

- **Container HTTP Port:** `80` (پیش‌فرض).
- بعد از اولین دیپلوی موفق: **Enable HTTPS** و سپس **Force HTTPS** را روشن کنید.

## ۳) دیپلوی

با CLI کاپروور از ریشهٔ ریپو:

```bash
npm i -g caprover      # اگر نصب نیست
caprover deploy        # captain-definition و Dockerfile را می‌خواند
```

یا از طریق آپلود tarball / اتصال به Git در پنل CapRover.

> ساخت ایمیج چند دقیقه طول می‌کشد (نصب Node و build فرانت + publish دات‌نت). این‌ها همه داخل ایمیج انجام می‌شوند؛ نیازی به Node/دات‌نت روی سرور نیست.

## ۴) اولین اجرا

هنگام هر استارت، پیش از پذیرفتن اولین درخواست، این‌ها به‌صورت خودکار و زیر یک **قفل advisory پستگرس** اجرا می‌شوند:
ساخت دیتابیس خالی اگر نباشد ← migrationهای در انتظار ← دادهٔ اولیه ← کارهای استارت (`IStartupTask`).
قفل یعنی اگر دو نمونهٔ اپ هم‌زمان بالا بیایند، دومی صبر می‌کند و همه‌چیز را انجام‌شده می‌بیند؛ استارت‌های بعدی تغییری نمی‌دهند.

> تا پیش از فاز ۰ همین سند می‌گفت migrationها خودکار اجرا می‌شوند، ولی در عمل اجرا نمی‌شدند و روی دیتابیس تازه اپ بدون جدول
> بالا می‌آمد. از این نسخه واقعاً اجرا می‌شوند.

اگر دیتابیس هنوز آماده نباشد، ۱۰ بار با فاصلهٔ ۳ ثانیه دوباره تلاش می‌شود. اما اگر در لاگ پیام `Critical` با «استارت متوقف شد» دیدید،
تلاش دوباره کمکی نمی‌کند (کانتینر مدام ری‌استارت می‌شود) — متن پیام می‌گوید چه باید کرد (معمولاً بخش ۵).

ورود اولیه:

| نقش | ایمیل | رمز |
|-----|--------|-----|
| ادمین سیستم | `admin@rugsystem.local` | `Admin@12345` |

بعد از ورود، به‌عنوان ادمین سیستم یک «کارگاه» (فروشنده) بسازید و با حساب مدیر آن وارد شوید.
**توصیه:** رمزهای پیش‌فرض را بعد از اولین ورود تغییر دهید.

## ۵) به‌روزرسانی، پشتیبان و migrationهای حساس

### پشتیبان‌گیری

اسکریپت‌های `scripts/db-backup.sh` (لینوکس، روی سرور) و `scripts/db-backup.ps1` (ویندوز) با `pg_dump` یک پشتیبان کامل در قالب custom
در `backups/rugs_<زمان>.dump` می‌سازند و خوانا بودن آرشیو را هم بررسی می‌کنند. روی سرور CapRover، که pg_dump معمولاً فقط داخل کانتینر
پستگرس هست (اسکریپت را به سرور کپی کنید):

```bash
bash scripts/db-backup.sh --container "$(docker ps -q -f name=srv-captain--postgres)" --db rugs_management --password '<رمز>'
```

بازگردانی: `pg_restore --clean --if-exists --no-owner -d rugs_management backups/rugs_<زمان>.dump`
(در حالت کانتینر: `docker exec -i <کانتینر> pg_restore --clean --if-exists --no-owner -U postgres -d rugs_management < backups/rugs_<زمان>.dump`).

### دروازهٔ پشتیبان (`[RequiresBackup]`)

migration‌ای که دادهٔ موجود را تغییر می‌دهد یا دور می‌ریزد با `[RequiresBackup("جدول")]` علامت خورده است. اگر چنین migration‌ای در
انتظار باشد و آن جدول خالی نباشد، اپ بالا نمی‌آید و در لاگ شناسهٔ migration و نام جدول را می‌نویسد. آنگاه:

1. با اسکریپت بالا پشتیبان بگیرید و مطمئن شوید فایل ساخته شده است.
2. در **App Configs** متغیر `Database__ConfirmedBackupFor` را برابر همان شناسه بگذارید (مثلاً `20261101090000_SplitRugDimensions`؛
   چند شناسه را با ویرگول جدا کنید) و **Save & Update** بزنید.
3. اپ بالا می‌آید و migration اجرا می‌شود. ماندن این متغیر بی‌خطر است چون فقط همان شناسه را تأیید می‌کند؛ ولی بهتر است بعداً پاکش کنید.

### اجرای دستی migration (اختیاری)

اگر می‌خواهید schema را خودتان (مثلاً DBA) به‌روز کنید، `Database__MigrateOnStartup=false` بگذارید و اسکریپت idempotent بسازید:

```bash
dotnet tool restore
dotnet ef migrations script --idempotent -o migrate.sql --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Web
sed -i '1s/^\xEF\xBB\xBF//' migrate.sql   # EF فایل را با BOM می‌نویسد و psql آن را نمی‌فهمد
psql -v ON_ERROR_STOP=1 -d rugs_management -f migrate.sql
```

این اسکریپت را می‌توان بارها اجرا کرد؛ هر migration فقط اگر هنوز اجرا نشده باشد اعمال می‌شود. دروازهٔ پشتیبان در این حالت فعال نیست،
پس پیش از اجرا خودتان پشتیبان بگیرید.

## نکات فنی

- اپ پشت پراکسی، هدرهای `X-Forwarded-Proto` را می‌خواند تا کوکی‌ها روی HTTPS امن (Secure) شوند.
- ری‌دایرکت HTTPS داخل اپ خاموش است (CapRover در لبه HTTPS را اجبار می‌کند). برای اجرای مستقیم HTTPS بدون پراکسی، متغیر `Hosting__UseHttpsRedirection=true` را ست کنید.
- برای مقیاس‌پذیری چندنمونه‌ای، همان دایرکتوری ماندگار کلیدها بین نمونه‌ها مشترک است و مشکلی پیش نمی‌آید؛
  migration و دادهٔ اولیه هم به لطف قفل advisory فقط یک بار اجرا می‌شوند.
