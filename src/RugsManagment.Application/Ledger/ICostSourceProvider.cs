namespace RugsManagment.Application.Ledger;

/// <summary>
/// سهم یک فرش از منبع‌های هزینهٔ بیرون از خودِ فرش (به ارز پایهٔ کارگاه): ثبت‌های گروه «خرید» (خرید یکجا)، ثبت‌های
/// گروه «هزینه» (شستشو، کرایه، گمرک…) و سهم درآمد صادرات. همان سه ورودی که <c>RugCostMath.Compose</c>/CostSql کنار
/// سطرهای خرید و مراحل جمع می‌زنند (D.1).
/// </summary>
/// <param name="PurchaseAllocations">جمع سهم ثبت‌های گروه خرید.</param>
/// <param name="ExpenseAllocations">جمع سهم ثبت‌های گروه هزینه.</param>
/// <param name="ExportRevenueShare">سهم درآمد صادرات؛ null وقتی محموله درآمدی ندارد.</param>
public readonly record struct RugCostSources(decimal PurchaseAllocations, decimal ExpenseAllocations, decimal? ExportRevenueShare)
{
    public static readonly RugCostSources None = new(0m, 0m, null);
}

/// <summary>
/// منبع سهم‌های ثبت هزینه برای جمع بهای فرش (spec D.4 «One formula site»). فاز ۲ هنوز ثبت هزینه ندارد و
/// <see cref="NoCostSources"/> را ثبت می‌کند؛ فاز هزینه‌ها (P5) پیاده‌سازی واقعی را با <c>Replace</c> جایش می‌گذارد، بی‌آنکه
/// مرحلهٔ جمع یا CostSql عوض شوند. سهم‌ها از جدول مشتق‌شدهٔ <c>CostAllocation</c> خوانده می‌شوند که مرحله‌های
/// پیشین همین پاس (۴۰۰ و ۶۰۰) به‌روز کرده‌اند.
/// </summary>
public interface ICostSourceProvider
{
    /// <summary>سهم‌های فرش‌های داده‌شده؛ فرشی که در نتیجه نیست سهمی ندارد (<see cref="RugCostSources.None"/>).</summary>
    Task<IReadOnlyDictionary<Guid, RugCostSources>> GetAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken);

    /// <summary>آیا کارگاه هیچ ثبت هزینه‌ای دارد (برای قفل رقم اعشار و بررسی‌های سلامت).</summary>
    Task<bool> AnyEntriesAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>پیش‌فرض فاز ۲: هیچ ثبت هزینه‌ای نیست، پس هیچ سهمی نیست.</summary>
public sealed class NoCostSources : ICostSourceProvider
{
    private static readonly IReadOnlyDictionary<Guid, RugCostSources> Empty = new Dictionary<Guid, RugCostSources>();

    public Task<IReadOnlyDictionary<Guid, RugCostSources>> GetAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
        => Task.FromResult(Empty);

    public Task<bool> AnyEntriesAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(false);
}

/// <summary>
/// «آیا این کارگاه هیچ مبلغی ذخیره کرده؟» (D.5 #22) — یک پرسش برای هر گروه جدول. <c>PreferencesLockGuard</c> همهٔ
/// پیاده‌سازی‌های ثبت‌شده را می‌پرسد؛ فاز ۲ جدول‌های خودش را می‌پرسد (سطر خرید، مرحلهٔ پول‌دار، فروش، پرداخت، نرخ
/// توافقی، نرخ پیش‌فرض نوع مرحلهٔ کارگاه، قیمت هدف، نرخ پیش‌فرض نوع هزینه) و فازهای محموله و هزینه‌ها پرسش خودشان
/// (درآمد محموله، ثبت هزینه) را به‌صورت فایل تازه اضافه می‌کنند.
/// </summary>
public interface IStoredMoneyProbe
{
    Task<bool> AnyAsync(Guid tenantId, CancellationToken cancellationToken);
}
