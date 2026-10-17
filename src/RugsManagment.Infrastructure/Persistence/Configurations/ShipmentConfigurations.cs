using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Configurations;

// جدول‌های محموله (M3، spec B.9، B.13). DbSet ندارند (AppDbContext عمداً دست نمی‌خورد) و از راه db.Set<T>() خوانده
// می‌شوند، پس نام جدول صریح است. هر جدول کارگاه دارد (cascade از کارگاه) و ایندکس‌ها با TenantId شروع می‌شوند. هیچ ستون
// float نیست: ابعاد/متراژ numeric(12,2)، دلار numeric(14,2)، نرخ دلار و نرخ ارز numeric(18,6).

/// <summary>
/// محموله. شماره در کارگاه یکتا (شمارندهٔ <c>shipment</c>)؛ عکس‌ها متن‌اند نه jsonb (بایت‌هایی که هش می‌پوشاند باید بمانند).
/// xmin برای ذخیرهٔ هم‌زمان (PUT، قطعی، باز کردن دوباره، شماره‌گذاری نگله‌ها)؛ migration ستونش را نمی‌سازد.
/// </summary>
public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");

        builder.Property(s => s.Title).HasMaxLength(Shipment.TitleMaxLength).IsRequired();
        builder.Property(s => s.Destination).HasMaxLength(Shipment.DestinationMaxLength);
        builder.Property(s => s.BuyerName).HasMaxLength(Shipment.BuyerNameMaxLength);
        builder.Property(s => s.DeclarationNo).HasMaxLength(Shipment.DeclarationNoMaxLength);
        builder.Property(s => s.CustomsOffice).HasMaxLength(Shipment.CustomsOfficeMaxLength);
        builder.Property(s => s.Notes).HasMaxLength(Shipment.NotesMaxLength);
        builder.Property(s => s.DocumentDate).HasColumnType("date");
        builder.Property(s => s.SnapshotJson).HasColumnType("text");
        builder.Property(s => s.LastSnapshotJson).HasColumnType("text");
        builder.Property(s => s.SnapshotHash).HasColumnType("character(64)").HasMaxLength(Shipment.SnapshotHashLength);
        builder.Property(s => s.TargetBaleAreaM2).HasPrecision(6, 2);
        builder.Property(s => s.UsdRate).HasPrecision(18, 6);
        builder.Property(s => s.RevenueAmount).HasPrecision(18, 2);
        builder.Property(s => s.RevenueFx).HasPrecision(18, 6);

        builder.HasIndex(s => new { s.TenantId, s.Number }).IsUnique();
        // فهرست «محموله‌های باز» (خانه، انتخابگر نگله‌بندی)
        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

/// <summary>
/// نگله. یکتایی (ShipmentId, Number) با قید «تعویق‌پذیر» <c>UX_Bales_Shipment_Number</c> است که migration با SQL دستی
/// می‌سازد (HandEdits/Shipments.md)، نه ایندکس یکتای مدل: شماره‌گذاری دوباره و جابه‌جایی دو شماره در یک flush انجام
/// می‌شوند و قید معمولی وسط کار خطا می‌داد. با محموله می‌رود (cascade). xmin دارد.
/// </summary>
public class BaleConfiguration : IEntityTypeConfiguration<Bale>
{
    public void Configure(EntityTypeBuilder<Bale> builder)
    {
        builder.ToTable("Bales");

        builder.Property(b => b.WeightKg).HasPrecision(8, 2);
        builder.Property(b => b.Notes).HasMaxLength(Bale.NotesMaxLength);

        builder.HasIndex(b => new { b.TenantId, b.ShipmentId });

        builder.HasOne(b => b.Shipment).WithMany(s => s.Bales)
            .HasForeignKey(b => b.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(b => b.TenantId).OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

/// <summary>
/// خط (عضویت فرش در نگله). <see cref="ShipmentLine.RugId"/> یکتاست (فرش حداکثر در یک خط). کلیدهای خارجی به فرش، نگله و
/// محموله NO ACTION‌اند (حذف فرشِ خط‌دار و نگلهٔ غیرخالی ممنوع؛ در پایان دستور سنجیده می‌شود، پس cascade کارگاه کار می‌کند).
/// فیلتر حذف نرمِ فرش رویش هست. xmin دارد.
/// </summary>
public class ShipmentLineConfiguration : IEntityTypeConfiguration<ShipmentLine>
{
    public void Configure(EntityTypeBuilder<ShipmentLine> builder)
    {
        builder.ToTable("ShipmentLines");

        builder.Property(l => l.UsdPerM2Override).HasPrecision(14, 2);

        builder.HasIndex(l => l.RugId).IsUnique();
        builder.HasIndex(l => new { l.BaleId, l.Seq });
        builder.HasIndex(l => new { l.TenantId, l.ShipmentId });

        builder.HasOne(l => l.Rug).WithMany()
            .HasForeignKey(l => l.RugId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(l => l.Bale).WithMany(b => b.Lines)
            .HasForeignKey(l => l.BaleId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(l => l.Shipment).WithMany()
            .HasForeignKey(l => l.ShipmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(l => l.Rug.DeletedAt == null);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

/// <summary>
/// ارزش مشتق‌شدهٔ خط (کلید همان LineId، با خط می‌رود). فقط مرحلهٔ ارزش خط می‌نویسد؛ xmin ندارد. فیلتر حذف نرم از راه خط.
/// </summary>
public class ShipmentLineValueConfiguration : IEntityTypeConfiguration<ShipmentLineValue>
{
    public void Configure(EntityTypeBuilder<ShipmentLineValue> builder)
    {
        builder.ToTable("ShipmentLineValues");
        builder.HasKey(v => v.LineId);
        builder.Property(v => v.LineId).ValueGeneratedNever();

        builder.Property(v => v.UsdPerM2).HasPrecision(14, 2);
        builder.Property(v => v.PieceUsd).HasPrecision(14, 2);
        builder.Property(v => v.LineAreaM2).HasPrecision(12, 2);
        builder.Property(v => v.LineUsd).HasPrecision(14, 2);

        builder.HasIndex(v => new { v.TenantId, v.ShipmentId });

        builder.HasOne(v => v.Line).WithOne(l => l.Value)
            .HasForeignKey<ShipmentLineValue>(v => v.LineId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(v => v.TenantId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(v => v.Line.Rug.DeletedAt == null);
    }
}

/// <summary>جدول ارزش گمرکی. کلید یکسان‌شده در کارگاه یکتاست؛ xmin ندارد (مهر جدول در ترجیحات).</summary>
public class DeclaredValueRateConfiguration : IEntityTypeConfiguration<DeclaredValueRate>
{
    public void Configure(EntityTypeBuilder<DeclaredValueRate> builder)
    {
        builder.ToTable("DeclaredValueRates");

        builder.Property(r => r.KeyValue).HasMaxLength(DeclaredValueRate.KeyMaxLength).IsRequired();
        builder.Property(r => r.KeyNormalized).HasMaxLength(DeclaredValueRate.KeyMaxLength).IsRequired();
        builder.Property(r => r.UsdPerM2).HasPrecision(14, 2);

        builder.HasIndex(r => new { r.TenantId, r.KeyNormalized }).IsUnique();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
