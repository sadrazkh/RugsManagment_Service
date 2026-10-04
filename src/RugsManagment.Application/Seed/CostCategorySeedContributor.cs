using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Seed;

/// <summary>
/// سه نوع خرید پایهٔ هر کارگاه (spec K.2 فاز ۲، H.8): «قیمت خرید» (هر متر، ستون جدول ورود)، «دلالی» (درصد ۲، ستون
/// جدول ورود) و «کرایه تا انبار» (هر تخته). با <see cref="CostCategory.SeedKey"/> پیدا می‌شوند، پس اجرای دوباره چیزی
/// اضافه نمی‌کند و نامی که تنظیم‌کننده عوض کرده دست نمی‌خورد؛ بستهٔ «صادرات فرش» (فاز ۷) همین‌ها را «موجود بود»
/// گزارش می‌کند.
///
/// <para><b>سطرهای قدیمی M2.</b> M2 قیمت خرید قدیمی هر فرش را یک سطر «کل» با برچسب «قیمت خرید» و بی نوع ساخت
/// (<see cref="RugPurchaseLine.LegacyPurchaseLabel"/>). این‌جا همان سطرها به نوع <c>purchase.price</c> وصل می‌شوند، چون
/// خانهٔ «قیمت خرید» جدول ورود سطرِ همان نوع را نشان می‌دهد (G.2): فرش قدیمی باید «کل ۴۰٬۰۰۰٬۰۰۰» ببیند، نه خانهٔ خالی
/// که با نوشتنش سطر دوم ساخته شود و خرید دوبار حساب شود. فقط نوع عوض می‌شود؛ مبنا، نرخ و مبلغ همان می‌ماند. سطری که
/// یک بار وصل شد دیگر پیدا نمی‌شود، پس این کار در استارت‌های بعدی هزینه‌ای ندارد.</para>
///
/// <para>نرخ پیش‌فرض «دلالی» درصد است، نه مبلغ؛ قفل تعداد رقم اعشار مبلغ‌ها (D.5 #22) نباید آن را «مبلغ ذخیره‌شده»
/// بشمارد، وگرنه هر کارگاه تازه از روز اول قفل می‌شد.</para>
/// </summary>
public sealed class CostCategorySeedContributor(
    IRepository<CostCategory> categories,
    IRepository<RugPurchaseLine> purchaseLines) : ITenantSeedContributor
{
    public const string PriceKey = "purchase.price";
    public const string BrokerKey = "purchase.broker";
    public const string FreightInKey = "purchase.freightIn";

    /// <summary>بعد از ردیف ترجیحات (۱۰۰).</summary>
    public int Order => 200;

    /// <summary>سه نوع پایه، با همان ترتیب ستون‌های جدول ورود.</summary>
    public static IReadOnlyList<CostCategory> Defaults(Guid tenantId) =>
    [
        new()
        {
            TenantId = tenantId,
            SeedKey = PriceKey,
            Name = RugPurchaseLine.LegacyPurchaseLabel, // «قیمت خرید»
            Group = CostGroup.Purchase,
            Icon = "receipt",
            SortOrder = 10,
            DefaultBasis = CostBasis.PerM2,
            DefaultAreaBasis = AreaBasis.Intake,
            DefaultMethod = AllocationMethod.ByArea,
            ShowInIntake = true
        },
        new()
        {
            TenantId = tenantId,
            SeedKey = BrokerKey,
            Name = "دلالی",
            Group = CostGroup.Purchase,
            Icon = "receipt",
            SortOrder = 20,
            DefaultBasis = CostBasis.Percent,
            DefaultRate = 2m,
            DefaultAreaBasis = AreaBasis.Intake,
            DefaultMethod = AllocationMethod.ByArea,
            ShowInIntake = true
        },
        new()
        {
            TenantId = tenantId,
            SeedKey = FreightInKey,
            Name = "کرایه تا انبار",
            Group = CostGroup.Purchase,
            Icon = "truck",
            SortOrder = 30,
            DefaultBasis = CostBasis.PerPiece,
            DefaultAreaBasis = AreaBasis.Intake,
            DefaultMethod = AllocationMethod.ByCount,
            ShowInIntake = false
        }
    ];

    public async Task SeedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var seedKeys = new[] { PriceKey, BrokerKey, FreightInKey };
        var existing = await categories.ListAsync(
            c => c.TenantId == tenantId && c.SeedKey != null && seedKeys.Contains(c.SeedKey), cancellationToken);
        var bySeedKey = existing.ToDictionary(c => c.SeedKey!, StringComparer.Ordinal);

        foreach (var category in Defaults(tenantId))
        {
            if (bySeedKey.ContainsKey(category.SeedKey!))
                continue;

            await categories.AddAsync(category, cancellationToken);
            bySeedKey[category.SeedKey!] = category;
        }

        await LinkLegacyLinesAsync(tenantId, bySeedKey[PriceKey].Id, cancellationToken);
    }

    /// <summary>سطرهای «قیمت خرید» بی‌نوع (ساختهٔ M2) را به نوع <c>purchase.price</c> وصل می‌کند.</summary>
    private async Task LinkLegacyLinesAsync(Guid tenantId, Guid priceCategoryId, CancellationToken cancellationToken)
    {
        const string label = RugPurchaseLine.LegacyPurchaseLabel;
        var legacy = await purchaseLines.ListAsync(
            l => l.TenantId == tenantId && l.CategoryId == null && l.Label == label, cancellationToken);

        foreach (var snapshot in legacy)
        {
            // نسخهٔ ردیابی‌شده، تا پاس سازگاری فقط تغییر واقعی (CategoryId) را ببیند، نه کل سطر
            var line = await purchaseLines.GetByIdAsync(snapshot.Id, cancellationToken);
            if (line is null || line.TenantId != tenantId || line.CategoryId is not null)
                continue;

            line.CategoryId = priceCategoryId;
            line.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
