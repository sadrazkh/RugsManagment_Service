using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

public class RugConfiguration : IEntityTypeConfiguration<Rug>
{
    public void Configure(EntityTypeBuilder<Rug> builder)
    {
        builder.HasIndex(r => new { r.TenantId, r.Sku }).IsUnique();
        builder.Property(r => r.Sku).HasMaxLength(50).IsRequired();
        builder.HasOne(r => r.Tenant).WithMany(t => t.Rugs).HasForeignKey(r => r.TenantId);
        builder.HasOne(r => r.Batch).WithMany(b => b.Rugs).HasForeignKey(r => r.BatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(r => r.WorkflowSteps).WithOne(s => s.Rug).HasForeignKey(s => s.RugId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(r => r.MetadataJson).HasColumnType("jsonb"); // متادیتای انعطاف‌پذیر
        builder.Ignore(r => r.AreaSquareMeters); // فقط در کد محاسبه می‌شود

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

public class RugBatchConfiguration : IEntityTypeConfiguration<RugBatch>
{
    public void Configure(EntityTypeBuilder<RugBatch> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(b => b.Tenant).WithMany().HasForeignKey(b => b.TenantId).OnDelete(DeleteBehavior.Cascade);
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
        builder.Property<uint>("xmin").IsRowVersion();

        // هم‌راستا با فیلتر حذف نرمِ فرش: تسویهٔ استادکار نباید مرحلهٔ فرشِ
        // سطل‌زباله‌شده را بشمارد، وگرنه بدهی‌ای گزارش می‌شود که وجود ندارد.
        builder.HasQueryFilter(s => s.Rug.DeletedAt == null);
    }
}
