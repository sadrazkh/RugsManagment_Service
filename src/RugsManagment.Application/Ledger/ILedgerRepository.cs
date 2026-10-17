using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Ledger;

/// <summary>تنظیم‌های کارگاه که پاس لازم دارد (بی ردیف ترجیحات: رقم اعشار ۰ و بی شمارهٔ شروع).</summary>
public sealed record LedgerTenantSettings(int MoneyScale, CurrencyUnit Currency, int? SerialStart);

/// <summary>یک همپوشانی شماره: فرش این درخواست و فرش فعالِ دیگری که بازه‌اش را دارد.</summary>
public sealed record SerialConflict(Guid RugId, string Serial, Guid OtherRugId, string OtherSerial);

/// <summary>شمار فرش‌ها و ردیف‌های جمع برای بررسی استارت.</summary>
/// <param name="Rugs">همهٔ فرش‌های کارگاه، سطل زباله هم.</param>
/// <param name="Totals">ردیف‌های <c>RugCostTotal</c> موجود.</param>
/// <param name="Stale">ردیف‌هایی که با نسخهٔ قاعدهٔ دیگری ساخته شده‌اند.</param>
public sealed record LedgerHealthCounts(int Rugs, int Totals, int Stale);

/// <summary>یک ناهمخوانی که <c>LedgerVerifier</c> پیدا کرد (کدهای <c>CostMismatchCheck</c>).</summary>
public sealed record LedgerMismatchRow(
    Guid RugId, string Sku, string? SerialNo, string Check, decimal? Stored, decimal? Expected, string? Detail);

/// <summary>
/// تنها جای SQL پاس سازگاری (پیاده‌سازی: <c>Infrastructure/Persistence/Ledger/LedgerRepository</c>، با فرمول‌های
/// <c>CostSql</c>). همه‌چیز مجموعه‌ای و روی وضعیتِ flushشده است؛ همهٔ متدها کارگاه را می‌گیرند و بیرون از آن چیزی
/// نمی‌بینند. نوشتن‌ها فقط در جدول‌های مشتق‌شده (و ستون‌های مشتق‌شدهٔ <c>RugPurchaseLine.Amount</c> و
/// <c>RugSale.CostAtSale</c>) است و هرگز به <c>Rugs</c> (و xmin آن) دست نمی‌زند.
/// </summary>
public interface ILedgerRepository
{
    /// <summary>رقم اعشار، واحد پول و شمارهٔ شروع کارگاه.</summary>
    Task<LedgerTenantSettings> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>همهٔ کارگاه‌ها، قدیمی‌ترین اول (بررسی استارت).</summary>
    Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// شماره‌های این فرش‌ها (فقط فعال‌ها و فقط شمارهٔ دار) که با شمارهٔ فرش فعال دیگری همپوشانی دارند: بازهٔ عددی با
    /// بازهٔ عددی، یا متن یکسان. نتیجهٔ خالی یعنی سالم.
    /// </summary>
    Task<IReadOnlyList<SerialConflict>> FindSerialOverlapsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken);

    /// <summary>بزرگ‌ترین شمارهٔ عددیِ فرش‌های فعال کارگاه، بی این فرش‌ها (برای «شمارهٔ آزاد بعدی»).</summary>
    Task<int?> MaxSerialAsync(Guid tenantId, IReadOnlyCollection<Guid> excludingRugIds, CancellationToken cancellationToken);

    /// <summary>مبلغ همهٔ سطرهای خرید این فرش‌ها با قاعدهٔ دسته؛ فقط سطرهایی که عوض می‌شوند نوشته می‌شوند. شمار نوشته‌ها.</summary>
    Task<int> RecomputePurchaseLinesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, int moneyScale, CancellationToken cancellationToken);

    /// <summary>
    /// «قیمت خرید» هر فرش (سطرها + سهم ثبت‌های گروه خرید) را با مقدار ذخیره‌شده در <c>RugCostTotal.PurchaseCost</c>
    /// می‌سنجد، فرق‌دارها (و فرش‌های بی‌ردیف) را می‌نویسد و شناسه‌شان را برمی‌گرداند (انتشار D.4).
    /// </summary>
    Task<IReadOnlyList<Guid>> RollupPurchaseCostAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources,
        DateTimeOffset now, long ledgerVersion, CancellationToken cancellationToken);

    /// <summary>ردیف کامل <c>RugCostTotal</c> این فرش‌ها (فقط اگر عوض می‌شود). شمار نوشته‌ها.</summary>
    Task<int> UpsertCostTotalsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources,
        int moneyScale, DateTimeOffset now, long ledgerVersion, CancellationToken cancellationToken);

    /// <summary>عکس بها (<c>CostAtSale</c> = Total تازه، <c>CostAtSaleAt</c>) برای فروش این فرش‌ها. شمار نوشته‌ها.</summary>
    Task<int> SnapshotCostAtSaleAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// شناسهٔ فرش‌های کارگاه (سطل زباله هم)؛ با <paramref name="sample"/> فقط آن تعداد به‌تصادف (بررسی استارت).
    /// </summary>
    Task<IReadOnlyList<Guid>> ListRugIdsAsync(Guid tenantId, int? sample, CancellationToken cancellationToken);

    /// <summary>شمار فرش‌ها، ردیف‌های جمع و ردیف‌های کهنه.</summary>
    Task<LedgerHealthCounts> GetHealthCountsAsync(Guid tenantId, long ledgerVersion, CancellationToken cancellationToken);

    /// <summary>
    /// همه‌چیزِ این فرش‌ها را از نو حساب می‌کند (همان CostSql) و با ذخیره‌شده می‌سنجد: مبلغ هر سطر خرید، هر ستون جمع،
    /// نبودِ ردیف، نسخهٔ کهنه، و هم‌خوانی جفت مؤثر با COALESCE(نهایی، اول).
    /// </summary>
    Task<IReadOnlyList<LedgerMismatchRow>> VerifyAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources,
        int moneyScale, long ledgerVersion, CancellationToken cancellationToken);
}
