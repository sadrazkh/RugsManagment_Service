using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Configurations;

// جدول‌های هزینهٔ فرش (M2، spec B.6–B.8). DbSet ندارند (AppDbContext عمداً دست نمی‌خورد) و از راه
// db.Set<T>() خوانده می‌شوند، پس نام جدول صریح است. دقت پول numeric(18,2)، نرخ numeric(18,4)؛ هیچ ستون float نیست.

/// <summary>
/// سطرهای خرید فرش. با فرش (و کارگاه) می‌روند؛ فیلتر حذف نرمِ فرش رویشان هم هست. نوعِ هزینه‌ای که سطری به آن
/// اشاره می‌کند حذف نمی‌شود: NO ACTION (نه RESTRICT)، چون RESTRICT در لحظه سنجیده می‌شود و حذف آبشاری کارگاه را
/// بسته به ترتیب تریگرها می‌شکست؛ NO ACTION همان «حذف ممنوع» را در پایان دستور می‌سنجد (مثل ShipmentLine → Rug در B.13).
/// </summary>
public class RugPurchaseLineConfiguration : IEntityTypeConfiguration<RugPurchaseLine>
{
    public void Configure(EntityTypeBuilder<RugPurchaseLine> builder)
    {
        builder.ToTable("RugPurchaseLines");

        builder.Property(l => l.Label).HasMaxLength(RugPurchaseLine.LabelMaxLength).IsRequired();
        builder.Property(l => l.Rate).HasPrecision(18, 4);
        builder.Property(l => l.Amount).HasPrecision(18, 2);

        // سطرهای یک فرش همیشه به ترتیب خوانده می‌شوند؛ «نوع به کار رفته؟» با ایندکس دوم
        builder.HasIndex(l => new { l.RugId, l.SortOrder });
        builder.HasIndex(l => new { l.TenantId, l.CategoryId });

        builder.HasOne(l => l.Rug).WithMany(r => r.PurchaseLines)
            .HasForeignKey(l => l.RugId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.Category).WithMany()
            .HasForeignKey(l => l.CategoryId).OnDelete(DeleteBehavior.ClientNoAction);
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(l => l.Rug.DeletedAt == null);
    }
}

/// <summary>
/// نوع‌های هزینهٔ هر کارگاه. کلید seed در کارگاه یکتاست (وقتی پر است) تا seed تکرار نشود؛ با حذف نوع مرحله، پیوند
/// <c>StepTypeId</c> خالی می‌شود (فقط برای هشدار دوبار شمردن است). xmin برای ذخیرهٔ هم‌زمان؛ migration ستونش را نمی‌سازد.
/// </summary>
public class CostCategoryConfiguration : IEntityTypeConfiguration<CostCategory>
{
    public void Configure(EntityTypeBuilder<CostCategory> builder)
    {
        builder.ToTable("CostCategories");

        builder.Property(c => c.Name).HasMaxLength(CostCategory.NameMaxLength).IsRequired();
        builder.Property(c => c.SeedKey).HasMaxLength(CostCategory.SeedKeyMaxLength);
        builder.Property(c => c.Icon).HasMaxLength(CostCategory.IconMaxLength);
        builder.Property(c => c.DefaultRate).HasPrecision(18, 4);

        builder.HasIndex(c => new { c.TenantId, c.SeedKey })
            .IsUnique()
            .HasFilter("\"SeedKey\" IS NOT NULL");
        // کاشی‌های «نوع هزینه» به ترتیب و جدا برای هر گروه نشان داده می‌شوند
        builder.HasIndex(c => new { c.TenantId, c.Group, c.SortOrder });

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessStepType>().WithMany()
            .HasForeignKey(c => c.StepTypeId).OnDelete(DeleteBehavior.SetNull);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

/// <summary>
/// جمع هزینهٔ مشتق‌شدهٔ هر فرش (یک‌به‌یک، کلید همان RugId). فقط مرحلهٔ آخر پاس سازگاری می‌نویسد؛ xmin ندارد و نوشتنش
/// نسخهٔ فرش را عوض نمی‌کند. فیلتر حذف نرمِ فرش رویش هست (صفحهٔ سطل زباله با IgnoreQueryFilters می‌خواند).
/// </summary>
public class RugCostTotalConfiguration : IEntityTypeConfiguration<RugCostTotal>
{
    public void Configure(EntityTypeBuilder<RugCostTotal> builder)
    {
        builder.ToTable("RugCostTotals");
        builder.HasKey(t => t.RugId);
        builder.Property(t => t.RugId).ValueGeneratedNever();

        builder.Property(t => t.PurchaseCost).HasPrecision(18, 2);
        builder.Property(t => t.StepCost).HasPrecision(18, 2);
        builder.Property(t => t.ExpenseCost).HasPrecision(18, 2);
        builder.Property(t => t.Total).HasPrecision(18, 2);
        builder.Property(t => t.CostPerM2).HasPrecision(18, 2);
        builder.Property(t => t.CostPerPiece).HasPrecision(18, 2);
        builder.Property(t => t.ExportRevenueShare).HasPrecision(18, 2);

        builder.HasOne(t => t.Rug).WithOne(r => r.CostTotal)
            .HasForeignKey<RugCostTotal>(t => t.RugId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => t.Rug.DeletedAt == null);
    }
}
