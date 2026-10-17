using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Intake;

/// <summary>یک سطر خرید فرش، همان‌طور که جدول نشانش می‌دهد (بی‌ردیابی).</summary>
public sealed record GridPurchaseLine(Guid Id, Guid? CategoryId, CostBasis Basis, decimal Rate, decimal Amount, int SortOrder);

/// <summary>
/// یک فرش برای ساختن ردیف جدول: همهٔ خانه‌ها، ورودی، سطرهای خرید و نسخه (xmin). از پرس‌وجوی بی‌ردیابی می‌آید، یا از
/// همان فرش ردیابی‌شده‌ای که همین حالا نوشته می‌شود (<see cref="FromTracked"/>) — پس «مقدار فعلی سرور» در ادغام
/// سه‌طرفه و ردیف GET یک سازنده دارند.
/// </summary>
public sealed record GridRugSnapshot(
    Guid Id,
    uint Version,
    string Sku,
    string? SerialNo,
    int? SerialFrom,
    int? SerialTo,
    long? SerialSort,
    int Quantity,
    string? Title,
    string? Origin,
    string? Pattern,
    string? Material,
    int? KnotDensity,
    decimal IntakeLength,
    decimal IntakeWidth,
    decimal? FinalLength,
    decimal? FinalWidth,
    decimal AreaM2,
    decimal TotalAreaM2,
    decimal IntakeAreaM2,
    string? Notes,
    string? MetadataJson,
    Guid? BatchId,
    int? BatchNumber,
    string? SellerName,
    DateOnly? ReceivedOn,
    DateTimeOffset CreatedAt,
    IReadOnlyList<GridPurchaseLine> PurchaseLines)
{
    /// <summary>
    /// از فرش ردیابی‌شده (با <see cref="Rug.Batch"/> و <see cref="Rug.PurchaseLines"/> بارگذاری‌شده). متراژها همان فرمول
    /// ستون‌های تولیدشده‌اند (<c>AreaMath</c>)، پس با عدد ذخیره‌شده یکی‌اند.
    /// </summary>
    public static GridRugSnapshot FromTracked(Rug rug, uint version, IEnumerable<RugPurchaseLine> lines)
    {
        ArgumentNullException.ThrowIfNull(rug);
        return new GridRugSnapshot(
            rug.Id, version, rug.Sku, rug.SerialNo, rug.SerialFrom, rug.SerialTo, rug.SerialSort, rug.Quantity,
            rug.Title, rug.Origin, rug.Pattern, rug.Material, rug.KnotDensity,
            rug.IntakeLength, rug.IntakeWidth, rug.FinalLength, rug.FinalWidth,
            rug.AreaM2, rug.TotalAreaM2, rug.IntakeAreaM2,
            rug.Notes, rug.MetadataJson, rug.BatchId, rug.Batch?.Number, rug.Batch?.SellerName, rug.Batch?.ReceivedOn,
            rug.CreatedAt,
            lines.Select(l => new GridPurchaseLine(l.Id, l.CategoryId, l.Basis, l.Rate, l.Amount, l.SortOrder)).ToList());
    }
}

/// <summary>
/// آنچه قاعدهٔ حذف انباردار لازم دارد (G.3، G.6، E6): فرشی که خودش در ۲۴ ساعت گذشته ساخته و هیچ مبلغی رویش نیست.
/// </summary>
/// <param name="CreatedByUser">رویداد «ثبت» همین فرش در تاریخچه به نام همین کاربر است.</param>
/// <param name="HasMoney">سطر خرید، فروش، یا هر مبلغی در جمع بهای فرش (کارهای کارگاه، سهم هزینه‌ها، خرید یکجا).</param>
public sealed record RugDeleteFacts(Guid RugId, DateTimeOffset CreatedAt, bool CreatedByUser, bool HasMoney, bool Sold);

/// <summary>یک ورودی با جمع فرش‌هایش (نوار «ورودی‌های اخیر»).</summary>
public sealed record IntakeBatchRow(RugBatch Batch, int Rows, int Pieces, decimal AreaM2);

/// <summary>ردیف چیدمان ستون‌های کارگاه با نسخه‌اش.</summary>
public sealed record VersionedGridLayout(TenantGridLayout Layout, uint Version);

/// <summary>
/// دسترسی دیتابیس جدول ورود و انبار، ورودی‌های (روز، فروشنده) و چیدمان ستون‌ها — همیشه با کارگاه.
///
/// چرا جدا از <c>IRugRepository</c>: جدول ردیف‌های زیادی را یک‌جا می‌خواند و می‌نویسد (همهٔ ردیف‌های یک روز، صد ردیف
/// انبار، پنجاه ردیف چسبانده‌شده)؛ این‌جا هر کار یک پرس‌وجو برای همه است، نه یک بارگذاری کامل گراف (مراحل، عکس‌ها) برای
/// هر ردیف. نوشتن‌ها همه از <c>IUnitOfWork</c> و پاس سازگاری می‌گذرند؛ این کلاس هیچ SQL نوشتنی ندارد.
/// </summary>
public interface IIntakeGridRepository
{
    // ── خواندن ردیف‌ها (بی‌ردیابی) ─────────────────────────────────────

    /// <summary>
    /// فرش‌های فعال ورودی‌های یک روز ایران (<see cref="RugBatch.ReceivedOn"/>)، یا فقط یک ورودی؛ به ترتیب ثبت.
    /// </summary>
    Task<IReadOnlyList<GridRugSnapshot>> ListIntakeDayAsync(
        Guid tenantId, DateOnly day, Guid? batchId, CancellationToken cancellationToken = default);

    /// <summary>فرش‌های فعال با این شناسه‌ها (ترتیب نتیجه همان ترتیب شناسه‌ها؛ نبودها کنار می‌روند).</summary>
    Task<IReadOnlyList<GridRugSnapshot>> ListByIdsAsync(
        Guid tenantId, IReadOnlyList<Guid> rugIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// فرش‌هایی که از این ردیف‌های جدول ساخته شده‌اند (حتی در سطل زباله): شناسهٔ ردیف ← (فرش، حذف شده؟).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, (Guid RugId, bool Deleted)>> FindByClientRowIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> clientRowIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// فرش‌های فعال برای نوشتن: ردیابی‌شده، با ورودی، فروش و سطرهای خرید (نه مراحل و عکس‌ها). کارگاه دیگر یا سطل زباله
    /// در نتیجه نیست.
    /// </summary>
    Task<IReadOnlyList<Rug>> LoadForWriteAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// دانسته‌های قاعدهٔ حذف/برگرداندن برای این فرش‌ها (حتی در سطل زباله) نسبت به کاربر <paramref name="userId"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, RugDeleteFacts>> DeleteFactsAsync(
        Guid tenantId, Guid? userId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default);

    // ── ورودی‌ها ──────────────────────────────────────────────────────────

    /// <summary>ورودیِ (روز، کلید فروشنده) — ردیابی‌شده؛ null اگر نیست. ورودی تازهٔ همین درخواست (هنوز ذخیره‌نشده) هم دیده می‌شود.</summary>
    Task<RugBatch?> FindBatchAsync(Guid tenantId, DateOnly day, string sellerKey, CancellationToken cancellationToken = default);

    /// <summary>یک ورودی کارگاه (ردیابی‌شده)؛ null اگر نیست یا مال کارگاه دیگر است.</summary>
    Task<RugBatch?> GetBatchAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ورودی‌های روزدار از <paramref name="fromDay"/> تا <paramref name="toDay"/> (هر دو شامل)، با جمع فرش‌های فعالشان؛
    /// تازه‌ترین روز اول، بعد شمارهٔ بزرگ‌تر.
    /// </summary>
    Task<IReadOnlyList<IntakeBatchRow>> ListBatchesAsync(
        Guid tenantId, DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default);

    /// <summary>همین ورودی‌ها با جمع فرش‌های فعالشان.</summary>
    Task<IReadOnlyList<IntakeBatchRow>> SummarizeBatchesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken = default);

    // ── چیدمان ستون‌ها ────────────────────────────────────────────────────

    /// <summary>چیدمان کارگاه برای یک جدول با نسخه‌اش (بی‌ردیابی)؛ null یعنی پیش‌فرض برنامه.</summary>
    Task<VersionedGridLayout?> GetLayoutAsync(Guid tenantId, string gridId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ردیف چیدمان ردیابی‌شده با نسخهٔ مسلح (<c>WHERE xmin = expected</c>)؛ null اگر نیست.
    /// </summary>
    Task<TenantGridLayout?> GetLayoutForUpdateAsync(
        Guid tenantId, string gridId, uint expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>ردیف تازه در UnitOfWork (بی ذخیره).</summary>
    Task AddLayoutAsync(TenantGridLayout layout, CancellationToken cancellationToken = default);

    /// <summary>حذف ردیف ردیابی‌شده (برگشت به پیش‌فرض).</summary>
    void RemoveLayout(TenantGridLayout layout);

    /// <summary>نسخهٔ فعلیِ ردیف ردیابی‌شده — بعد از ذخیره همان xmin تازه.</summary>
    uint LayoutVersion(TenantGridLayout layout);
}
