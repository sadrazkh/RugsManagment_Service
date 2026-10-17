using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Configurations;

/// <summary>قوانین جدول کارگاه — ایندکس یکتا روی Slug</summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        // مهر امنیتی نشست؛ مقدار فعلی ۳۲ نویسهٔ hex است و ۶۴ جا برای قالب دیگری در آینده می‌ماند
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();

        // ترجیحات کاربر: UiMode، TextScale و GridEnter مثل بقیهٔ enumها int ذخیره می‌شوند. پیش‌فرض هر نقش را
        // کد می‌گذارد (User.ApplyRoleDefaultPreferences) و migration همان را برای کاربران قدیمی پر کرده است.
        builder.Property(u => u.CoachDismissedJson).HasColumnType("jsonb");

        builder.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId);
    }
}

public class ProcessStepTypeConfiguration : IEntityTypeConfiguration<ProcessStepType>
{
    public void Configure(EntityTypeBuilder<ProcessStepType> builder)
    {
        // کد در محدودهٔ هر کارگاه یکتاست، نه در کل سامانه — وگرنه دو کارگاه
        // نمی‌توانستند هر دو مرحله‌ای به نام «رنگرزی» داشته باشند.
        // مرحله‌های سیستمی (TenantId = null) هم بین خودشان یکتا می‌مانند.
        builder.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();

        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.Property(s => s.NameFa).HasMaxLength(100).IsRequired();
        builder.Property(s => s.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(s => s.FieldSchemaJson).HasColumnType("jsonb");
    }
}

public class WorkflowTemplateConfiguration : IEntityTypeConfiguration<WorkflowTemplate>
{
    public void Configure(EntityTypeBuilder<WorkflowTemplate> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(t => t.Tenant).WithMany(x => x.WorkflowTemplates).HasForeignKey(t => t.TenantId);
        builder.HasMany(t => t.Steps).WithOne(s => s.WorkflowTemplate).HasForeignKey(s => s.WorkflowTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// فرش (spec B.4): شماره و تعداد، سه جفت اندازه، مساحت‌های تولیدشده در دیتابیس، شناسهٔ ردیف جدول ورود.
/// قیدهای CHECK همان قاعده‌های دامنه را در دیتابیس هم نگه می‌دارند تا SQL خام (migration، ورود از اکسل) هم
/// نتواند دادهٔ ناسازگار بنویسد.
/// </summary>
public class RugConfiguration : IEntityTypeConfiguration<Rug>
{
    /// <summary>مساحت یک تخته در اندازهٔ مؤثر — همان <c>AreaMath.PieceArea</c> (round در پستگرس نصفه را دور از صفر می‌برد).</summary>
    public const string AreaM2Sql = "round(\"LengthMeters\" * \"WidthMeters\", 2)";

    /// <summary>مساحت ردیف: مساحت تخته × تعداد (قاعدهٔ دسته: اول گرد، بعد ضرب).</summary>
    public const string TotalAreaM2Sql = "round(\"LengthMeters\" * \"WidthMeters\", 2) * \"Quantity\"";

    /// <summary>مساحت یک تخته در اندازهٔ اول.</summary>
    public const string IntakeAreaM2Sql = "round(\"IntakeLength\" * \"IntakeWidth\", 2)";

    public void Configure(EntityTypeBuilder<Rug> builder)
    {
        builder.HasIndex(r => new { r.TenantId, r.Sku }).IsUnique();
        builder.Property(r => r.Sku).HasMaxLength(50).IsRequired();
        builder.HasOne(r => r.Tenant).WithMany(t => t.Rugs).HasForeignKey(r => r.TenantId);
        builder.HasOne(r => r.Batch).WithMany(b => b.Rugs).HasForeignKey(r => r.BatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(r => r.WorkflowSteps).WithOne(s => s.Rug).HasForeignKey(s => s.RugId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(r => r.MetadataJson).HasColumnType("jsonb"); // متادیتای انعطاف‌پذیر
        builder.Ignore(r => r.AreaSquareMeters); // فقط در کد محاسبه می‌شود
        builder.Ignore(r => r.HasFinalSize);
        builder.Ignore(r => r.Serial);

        // ── شماره و تعداد ─────────────────────────────────────────────
        builder.Property(r => r.SerialNo).HasMaxLength(SerialNumber.MaxLength);
        // پیش‌فرض ۱ برای ردیف‌های موجود و INSERTهای خام؛ کد همیشه مقدار را می‌فرستد (SetQuantity کمتر از ۱ نمی‌پذیرد)
        builder.Property(r => r.Quantity).HasDefaultValue(1);

        // شمارهٔ یکسان بین فرش‌های فعال یک کارگاه ممنوع است؛ فرش سطل‌زباله شماره‌اش را آزاد می‌کند. همپوشانی
        // دسته‌ها را این ایندکس نمی‌بیند — SerialOverlapStage زیر قفل کارگاه می‌سنجد.
        builder.HasIndex(r => new { r.TenantId, r.SerialNo })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL AND \"SerialNo\" IS NOT NULL");

        // همپوشانی بازه‌ها، «شماره داخل دسته» و «شمارهٔ بعدی» روی همین ایندکس می‌خوانند
        builder.HasIndex(r => new { r.TenantId, r.SerialFrom, r.SerialTo })
            .HasFilter("\"DeletedAt\" IS NULL AND \"SerialFrom\" IS NOT NULL");

        // تلاش دوبارهٔ همان ردیف جدول ورود همان فرش را پیدا می‌کند و فرش دوم نمی‌سازد
        builder.HasIndex(r => new { r.TenantId, r.ClientRowId })
            .IsUnique()
            .HasFilter("\"ClientRowId\" IS NOT NULL");

        // ── اندازه‌ها: numeric(9,2)، و جفت مؤثر همان LengthMeters/WidthMeters قدیمی ─────────
        builder.Property(r => r.LengthMeters).HasPrecision(9, 2);
        builder.Property(r => r.WidthMeters).HasPrecision(9, 2);
        builder.Property(r => r.IntakeLength).HasPrecision(9, 2);
        builder.Property(r => r.IntakeWidth).HasPrecision(9, 2);
        builder.Property(r => r.FinalLength).HasPrecision(9, 2);
        builder.Property(r => r.FinalWidth).HasPrecision(9, 2);

        // ستون‌های تولیدشده (stored): هر SQL همان عدد C# را می‌بیند. EF فقط فیلد پشتیبان را پر می‌کند؛ getter
        // همان فرمول را روی اضلاع فعلی حساب می‌کند تا پیش از ذخیره هم درست باشد.
        builder.Property(r => r.AreaM2)
            .HasPrecision(12, 2)
            .HasComputedColumnSql(AreaM2Sql, stored: true)
            .HasField("_storedAreaM2")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(r => r.TotalAreaM2)
            .HasPrecision(12, 2)
            .HasComputedColumnSql(TotalAreaM2Sql, stored: true)
            .HasField("_storedTotalAreaM2")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(r => r.IntakeAreaM2)
            .HasPrecision(12, 2)
            .HasComputedColumnSql(IntakeAreaM2Sql, stored: true)
            .HasField("_storedIntakeAreaM2")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Rugs_Quantity", "\"Quantity\" >= 1");
            // بازه می‌تواند از تعداد بلندتر باشد، هرگز کوتاه‌تر؛ شمارهٔ تک SerialTo = SerialFrom دارد و متنی هیچ‌کدام
            t.HasCheckConstraint(
                "CK_Rugs_SerialRange",
                "(\"SerialFrom\" IS NULL AND \"SerialTo\" IS NULL) OR " +
                "(\"SerialFrom\" IS NOT NULL AND \"SerialTo\" IS NOT NULL AND \"SerialTo\"::bigint - \"SerialFrom\" + 1 >= \"Quantity\")");
            // اندازهٔ نهایی: هر دو ضلع یا هیچ‌کدام
            t.HasCheckConstraint("CK_Rugs_FinalSize", "(\"FinalLength\" IS NULL) = (\"FinalWidth\" IS NULL)");
        });
        // رابطه‌های سطر خرید و جمع هزینه در CostingConfigurations (سمت کلید خارجی) پیکربندی شده‌اند

        // ── حذف نرم ────────────────────────────────────────────────
        // فیلتر سراسری: فرش‌های سطل‌زباله از هر کوئری‌ای (فهرست، داشبورد، گزارش، فروش)
        // خودبه‌خود کنار می‌روند. فقط صفحهٔ سطل زباله با IgnoreQueryFilters آن‌ها را می‌بیند.
        builder.HasQueryFilter(r => r.DeletedAt == null);
        // ایندکس جزئی: ردیف‌های فعال (اکثریت قاطع) ایندکس سبکی می‌گیرند
        builder.HasIndex(r => new { r.TenantId, r.DeletedAt });

        // کنترل هم‌زمانی خوش‌بینانه با ستون سیستمی xmin پستگرس (بدون ستون اضافه در جدول).
        // اگر دو اپراتور هم‌زمان یک فرش را تغییر دهند، دومی خطای هم‌زمانی می‌گیرد
        // به‌جای اینکه تغییر اولی را بی‌صدا بازنویسی کند.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

/// <summary>فیلدهای سفارشی هر کارگاه — کلید یکتا در محدودهٔ همان کارگاه</summary>
public class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.HasIndex(f => new { f.TenantId, f.Key }).IsUnique();
        builder.Property(f => f.Key).HasMaxLength(60).IsRequired();
        builder.Property(f => f.Label).HasMaxLength(120).IsRequired();
        builder.Property(f => f.OptionsJson).HasColumnType("jsonb");

        // پیش‌فرض دیتابیسی true تا فیلدهای موجود هنگام migration «قابل خروجی» بمانند. Sentinel = true یعنی EF
        // فقط وقتی مقدار را نمی‌فرستد که true است (همان پیش‌فرض)؛ false صریح همیشه نوشته می‌شود — بدون آن،
        // false (پیش‌فرض CLR) با پیش‌فرض دیتابیس جایگزین و بی‌صدا true می‌شد.
        builder.Property(f => f.Exportable).HasDefaultValue(true).HasSentinel(true);

        builder.HasOne(f => f.Tenant).WithMany().HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>قالب‌های برچسب هر کارگاه — طراحی بصری در jsonb</summary>
public class LabelTemplateConfiguration : IEntityTypeConfiguration<LabelTemplate>
{
    public void Configure(EntityTypeBuilder<LabelTemplate> builder)
    {
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.ElementsJson).HasColumnType("jsonb");
        builder.HasOne(l => l.Tenant).WithMany().HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// «ورودی» (spec B.5): شمارهٔ یکتا در کارگاه، فروشنده و روز ایران، و کلید «یک ورودی برای هر فروشنده در هر روز».
/// </summary>
public class RugBatchConfiguration : IEntityTypeConfiguration<RugBatch>
{
    public void Configure(EntityTypeBuilder<RugBatch> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.SellerName).HasMaxLength(RugBatch.SellerMaxLength);
        builder.Property(b => b.SellerKey).HasMaxLength(RugBatch.SellerMaxLength);
        builder.HasOne(b => b.Tenant).WithMany().HasForeignKey(b => b.TenantId).OnDelete(DeleteBehavior.Cascade);

        // شمارهٔ ورودی از شمارندهٔ «batch» می‌آید؛ این ایندکس پشتوانهٔ یکتایی است
        builder.HasIndex(b => new { b.TenantId, b.Number }).IsUnique();

        // کلید «ورودی»: یک ورودی برای هر فروشنده در هر روز ایران. NULLها برابر حساب می‌شوند (NULLS NOT DISTINCT) تا
        // کلید خالی هم یکتا بماند؛ ورودی‌های قدیمیِ بی‌روز (ReceivedOn = NULL) بیرون از این قاعده‌اند.
        builder.HasIndex(b => new { b.TenantId, b.ReceivedOn, b.SellerKey })
            .IsUnique()
            .HasFilter("\"ReceivedOn\" IS NOT NULL")
            .AreNullsDistinct(false);

        // دو ویرایش هم‌زمانِ یک ورودی همدیگر را بی‌صدا پاک نکنند
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

public class ServiceProviderConfiguration : IEntityTypeConfiguration<ServiceProvider>
{
    public void Configure(EntityTypeBuilder<ServiceProvider> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Specialty).HasMaxLength(200);
        builder.Property(p => p.Phone).HasMaxLength(40);
        builder.HasIndex(p => new { p.TenantId, p.Name });
        builder.HasOne(p => p.Tenant).WithMany().HasForeignKey(p => p.TenantId);

        builder.HasMany(p => p.Rates).WithOne(r => r.ServiceProvider)
            .HasForeignKey(r => r.ServiceProviderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Payments).WithOne(x => x.ServiceProvider)
            .HasForeignKey(x => x.ServiceProviderId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>نرخ توافقی هر طرف برای هر نوع مرحله — حداکثر یک ردیف برای هر ترکیب.</summary>
public class ServiceProviderRateConfiguration : IEntityTypeConfiguration<ServiceProviderRate>
{
    public void Configure(EntityTypeBuilder<ServiceProviderRate> builder)
    {
        builder.HasIndex(r => new { r.ServiceProviderId, r.ProcessStepTypeId }).IsUnique();
        builder.Property(r => r.UnitRate).HasPrecision(18, 2);

        builder.HasOne(r => r.ProcessStepType).WithMany()
            .HasForeignKey(r => r.ProcessStepTypeId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>پرداخت‌ها به طرف خدمات — پرکاربردترین کوئری «پرداخت‌های یک طرف به ترتیب تاریخ» است.</summary>
public class ProviderPaymentConfiguration : IEntityTypeConfiguration<ProviderPayment>
{
    public void Configure(EntityTypeBuilder<ProviderPayment> builder)
    {
        builder.HasIndex(p => new { p.ServiceProviderId, p.PaidAt });
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Reference).HasMaxLength(120);

        builder.HasOne(p => p.Tenant).WithMany()
            .HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// تاریخچهٔ فعالیت — فقط افزودنی. ایندکس روی (کارگاه، زمان نزولی) چون فهرست
/// همیشه «آخرین رویدادهای یک کارگاه» است.
/// </summary>
public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.HasIndex(a => new { a.TenantId, a.CreatedAt });
        builder.HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId });

        builder.Property(a => a.UserName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(60).IsRequired();
        builder.Property(a => a.EntityLabel).HasMaxLength(200);
        builder.Property(a => a.Summary).HasMaxLength(500).IsRequired();
        // جزئیات ساخت‌یافته (چه از چه به چه شد)؛ بدون مبلغ تومانی
        builder.Property(a => a.DetailsJson).HasColumnType("jsonb");

        builder.HasOne(a => a.Tenant).WithMany()
            .HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// فروش فرش — هر فرش حداکثر یک فروش دارد، پس رابطه یک‌به‌یک با ایندکس یکتاست.
/// </summary>
public class RugSaleConfiguration : IEntityTypeConfiguration<RugSale>
{
    public void Configure(EntityTypeBuilder<RugSale> builder)
    {
        builder.HasIndex(s => s.RugId).IsUnique();
        // گزارش فروش تقریباً همیشه «بازهٔ تاریخ در یک کارگاه» است
        builder.HasIndex(s => new { s.TenantId, s.SoldAt });

        builder.Property(s => s.BuyerName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.BuyerPhone).HasMaxLength(40);
        builder.Property(s => s.Reference).HasMaxLength(120);
        builder.Property(s => s.SalePrice).HasPrecision(18, 2);
        builder.Property(s => s.Discount).HasPrecision(18, 2);
        builder.Property(s => s.ReceivedAmount).HasPrecision(18, 2);
        // عکس بهای تمام‌شده در لحظهٔ فروش (D.6) — نوشتنش کار RugCostTotalStage است
        builder.Property(s => s.CostAtSale).HasPrecision(18, 2);

        // فقط در کد محاسبه می‌شوند
        builder.Ignore(s => s.NetAmount);
        builder.Ignore(s => s.OutstandingAmount);

        builder.HasOne(s => s.Rug).WithOne(r => r.Sale)
            .HasForeignKey<RugSale>(s => s.RugId).OnDelete(DeleteBehavior.Cascade);

        // هم‌راستا با فیلتر حذف نرمِ فرش (وگرنه EF هشدار «سر اجباری رابطه فیلتر شده» می‌دهد).
        // در عمل نباید فعال شود چون حذف فرشِ فروخته‌شده در سرویس مسدود است.
        builder.HasQueryFilter(s => s.Rug.DeletedAt == null);
        builder.HasOne(s => s.Tenant).WithMany()
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// عکس‌های فرش — با حذف فرش، رکوردها هم می‌روند (فایل‌ها را سرویس پاک می‌کند).
/// ایندکس ترکیبی چون گالری همیشه «عکس‌های یک فرش به ترتیب» خوانده می‌شود.
/// </summary>
public class RugImageConfiguration : IEntityTypeConfiguration<RugImage>
{
    public void Configure(EntityTypeBuilder<RugImage> builder)
    {
        builder.HasIndex(i => new { i.RugId, i.SortOrder });
        builder.Property(i => i.FileName).HasMaxLength(120).IsRequired();
        builder.Property(i => i.ThumbnailFileName).HasMaxLength(120);
        builder.Property(i => i.ContentType).HasMaxLength(60).IsRequired();

        builder.HasOne(i => i.Rug).WithMany(r => r.Images)
            .HasForeignKey(i => i.RugId).OnDelete(DeleteBehavior.Cascade);

        // هم‌راستا با فیلتر حذف نرمِ فرش: عکس فرشِ سطل‌زباله‌شده هم سرو نمی‌شود
        builder.HasQueryFilter(i => i.Rug.DeletedAt == null);
        builder.HasOne(i => i.Tenant).WithMany()
            .HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// مرحلهٔ فرش — پرترافیک‌ترین موجودیت برای ویرایش هم‌زمان (دو اپراتور، یک فرش).
/// کنترل هم‌زمانی با xmin تا ثبت هزینهٔ یکی، ثبت دیگری را بی‌صدا پاک نکند.
/// </summary>
public class RugWorkflowStepConfiguration : IEntityTypeConfiguration<RugWorkflowStep>
{
    public void Configure(EntityTypeBuilder<RugWorkflowStep> builder)
    {
        builder.HasIndex(s => new { s.RugId, s.OrderIndex });
        builder.Property(s => s.CompletedByName).HasMaxLength(200);
        builder.Ignore(s => s.EffectiveCost); // فقط در کد محاسبه می‌شود
        builder.Ignore(s => s.HasPricingSnapshot);
        builder.Property<uint>("xmin").IsRowVersion();

        // عکس اندازه و تعداد هنگام قیمت‌گذاری (B.5)؛ هم‌دقت با اضلاع فرش
        builder.Property(s => s.PricedLength).HasPrecision(9, 2);
        builder.Property(s => s.PricedWidth).HasPrecision(9, 2);

        // هم‌راستا با فیلتر حذف نرمِ فرش: تسویهٔ استادکار نباید مرحلهٔ فرشِ
        // سطل‌زباله‌شده را بشمارد، وگرنه بدهی‌ای گزارش می‌شود که وجود ندارد.
        builder.HasQueryFilter(s => s.Rug.DeletedAt == null);
    }
}
