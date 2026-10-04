using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Configurations;

/// <summary>
/// شمارنده‌های اتمیک هر کارگاه. کلید اصلی ترکیبی (کارگاه، نام شمارنده) است، پس هر شمارنده
/// فقط یک ردیف دارد و UPSERT روی همین کلید قفل ردیفی می‌گیرد؛ دو درخواست هم‌زمان پشت هم صف
/// می‌کشند و هرگز عدد تکراری نمی‌گیرند.
///
/// DbSet ندارد (AppDbContext عمداً دست نمی‌خورد)؛ از طریق <c>db.Set&lt;TenantCounter&gt;()</c>
/// خوانده می‌شود، پس نام جدول باید صریح باشد وگرنه EF نام کلاس («TenantCounter») را می‌گذاشت.
/// با حذف کارگاه، شمارنده‌هایش هم می‌روند.
/// </summary>
public class TenantCounterConfiguration : IEntityTypeConfiguration<TenantCounter>
{
    public void Configure(EntityTypeBuilder<TenantCounter> builder)
    {
        builder.ToTable("TenantCounters");
        builder.HasKey(c => new { c.TenantId, c.Key });
        builder.Property(c => c.Key).HasMaxLength(40);
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// ترجیحات کارگاه — یک‌به‌یک با کارگاه (کلید اصلی همان TenantId، با حذف کارگاه می‌رود).
///
/// دقت ستون‌ها از B.1: ضلع و مساحت numeric(…,2)، نرخ ارز numeric(18,6)، دلار numeric(14,2). هیچ ستون
/// float ندارد. xmin برای ذخیرهٔ هم‌زمانِ دو تنظیم‌کننده؛ migration ستونِ xmin را نمی‌سازد (ستون سیستمی است).
/// ردیف هر کارگاه را migration برای کارگاه‌های قدیمی و TenantSeedRunner برای کارگاه‌های تازه می‌سازد.
/// </summary>
public class TenantPreferencesConfiguration : IEntityTypeConfiguration<TenantPreferences>
{
    public void Configure(EntityTypeBuilder<TenantPreferences> builder)
    {
        builder.ToTable("TenantPreferences");
        builder.HasKey(p => p.TenantId);
        builder.HasOne<Tenant>().WithOne()
            .HasForeignKey<TenantPreferences>(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.SkuPrefix).HasMaxLength(TenantPreferences.SkuPrefixMaxLength).IsRequired();
        builder.Property(p => p.MoneyScale).HasColumnType("smallint");
        builder.Property(p => p.MaxPlausibleSideM).HasPrecision(4, 2);
        builder.Property(p => p.DefaultUsdFx).HasPrecision(18, 6);
        builder.Property(p => p.DeclaredValueKeyField)
            .HasMaxLength(TenantPreferences.DeclaredValueKeyFieldMaxLength).IsRequired();
        builder.Property(p => p.DeclaredValueFallbackUsd).HasPrecision(14, 2);
        builder.Property(p => p.DefaultBaleTargetAreaM2).HasPrecision(6, 2);
        builder.Property(p => p.BaleTargetTolerancePercent).HasColumnType("smallint");

        builder.Property(p => p.ExporterName).HasColumnType("text");
        builder.Property(p => p.ExporterCode).HasColumnType("text");
        builder.Property(p => p.ExporterAddress).HasColumnType("text");
        builder.Property(p => p.ExporterPhone).HasColumnType("text");
        builder.Property(p => p.CustomsOffice).HasColumnType("text");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

/// <summary>
/// چیدمان ستون‌های جدول‌ها برای هر کارگاه — کلید اصلی (کارگاه، شناسهٔ جدول). نبودِ ردیف یعنی «پیش‌فرض
/// برنامه». xmin برای ذخیرهٔ هم‌زمان؛ migration ستونِ xmin را نمی‌سازد.
/// </summary>
public class TenantGridLayoutConfiguration : IEntityTypeConfiguration<TenantGridLayout>
{
    public void Configure(EntityTypeBuilder<TenantGridLayout> builder)
    {
        builder.ToTable("TenantGridLayouts");
        builder.HasKey(l => new { l.TenantId, l.GridId });
        builder.Property(l => l.GridId).HasMaxLength(TenantGridLayout.GridIdMaxLength);
        builder.Property(l => l.ColumnsJson).HasColumnType("jsonb").IsRequired();
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
