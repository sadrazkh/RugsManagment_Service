using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// SQL پاس سازگاری برای دفتر هزینه (پیاده‌سازی: <c>Infrastructure/Persistence/Repositories/CostLedgerRepository</c>، در فهرست
/// سفید «نوشتن خام» آزمون معماری). خواندن‌های پیش از flush را contributorها بعد از قفل کارگاه صدا می‌زنند (وضعیت «پیش از این
/// ذخیره»)؛ بقیه را مرحله‌ها بعد از flush. نوشتن فقط <see cref="WriteAsync"/> است و فقط در جدول‌های مشتق‌شدهٔ
/// <c>CostAllocations</c> و <c>CostEntryResults</c> — هرگز در <c>Rugs</c> یا <c>CostEntries</c> (و xmin آن‌ها). همهٔ متدها کارگاه
/// را می‌گیرند و بیرون از آن چیزی نمی‌بینند.
/// </summary>
public interface ICostLedgerRepository
{
    /// <summary>ثبت‌هایی که این معیارها به آن‌ها می‌رسند (بعد از flush؛ حذف‌شده‌ها هم).</summary>
    Task<IReadOnlyList<Guid>> FindEntriesAsync(Guid tenantId, EntryCriteria criteria, CancellationToken cancellationToken);

    /// <summary>
    /// ورودی تقسیم این ثبت‌ها (یا همهٔ ثبت‌های کارگاه وقتی null) با سهم‌ها و نتیجهٔ ذخیره‌شده برای diff — یک دستور.
    /// </summary>
    Task<IReadOnlyList<EntryInput>> LoadAsync(Guid tenantId, IReadOnlyCollection<Guid>? entryIds, CancellationToken cancellationToken);

    /// <summary>نوشتن diffها (حذف سهم‌های رفته، upsert سهم‌ها و نتیجه‌ها) — یک دستور. شمار ردیف‌های نوشته‌شده.</summary>
    Task<int> WriteAsync(Guid tenantId, IReadOnlyList<EntryWrite> writes, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// پیش از flush: ثبت‌هایی که با حذف این نگله‌ها، محموله‌ها (و نگله‌هایشان)، ورودی‌ها یا فرش‌ها (حذف سخت) هدف یا
    /// پیوندشان را از دست می‌دهند — cascade و SET NULL در flush رخ می‌دهند و بعد از آن دیگر پیدا نمی‌شوند (D.5 #6، #12، #17، #18).
    /// </summary>
    Task<IReadOnlyList<Guid>> EntriesLosingTargetsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> shipmentIds, IReadOnlyCollection<Guid> baleIds,
        IReadOnlyCollection<Guid> batchIds, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken);

    /// <summary>پیش از flush: فرش‌هایی که از این ثبت‌ها (که حذف سخت می‌شوند) سهم داشتند.</summary>
    Task<IReadOnlyList<Guid>> AllocatedRugsAsync(Guid tenantId, IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken);

    /// <summary>
    /// گروه ذخیره‌شده (پیش از flush) و «به کار رفته» (ثبت، حذف‌شده هم، یا سطر خرید، سطل زباله هم) این نوع‌ها؛ برای قفل گروه
    /// (D.5 #25).
    /// </summary>
    Task<IReadOnlyList<CategoryUse>> CategoryUsesAsync(Guid tenantId, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken);

    /// <summary>
    /// بعد از flush، برای سهم درآمد (D.6): فرش‌های فعلیِ محموله‌هایی که درآمدشان عوض شد یا درآمد دارند و یکی از
    /// <paramref name="lineUsdChangedRugs"/> را دارند، به‌اضافهٔ آن فرش‌هایی که پیش از این سهم درآمد داشتند (از محموله بیرون رفتند).
    /// </summary>
    Task<IReadOnlyList<Guid>> RevenueRugsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> revenueShipments, IReadOnlyCollection<Guid> lineUsdChangedRugs,
        CancellationToken cancellationToken);
}

/// <summary>یک نوع هزینه در دیتابیس: گروه ذخیره‌شده و اینکه به کار رفته است.</summary>
public sealed record CategoryUse(Guid Id, CostGroup Group, bool Used);

/// <summary>
/// «کدام ثبت‌ها دوباره تقسیم شوند» (<see cref="AffectedEntryFinder"/>): هر مجموعه با شرط خودش.
/// </summary>
/// <param name="EntryIds">ثبت‌هایی که مستقیم عوض شدند.</param>
/// <param name="MemberRugs">هر ثبتی که این فرش‌ها را دارد (اندازه، تعداد، شماره، ورودی، سطل زباله).</param>
/// <param name="PurchaseGroupRugs">ثبت‌های گروه خرید که این فرش‌ها را دارند (سطر خرید عوض شد: <c>W_PURCHASE_DOUBLE</c>).</param>
/// <param name="StepRugs">ثبت‌هایی با نوع مرحلهٔ پیوندی که این فرش‌ها را دارند (<c>W_STEP_DOUBLE</c>).</param>
/// <param name="DeclaredRugs">ثبت‌های «به نسبت ارزش گمرکی» که این فرش‌ها را دارند (دلار خط عوض شد).</param>
/// <param name="PurchaseCostRugs">ثبت‌های «به نسبت قیمت خرید» که این فرش‌ها را دارند (قیمت خرید عوض شد).</param>
/// <param name="Batches">ثبت‌های این ورودی‌ها.</param>
/// <param name="Shipments">ثبت‌های این محموله‌ها و ثبت‌های نگله‌ای نگله‌هایشان.</param>
/// <param name="Bales">ثبت‌هایی که این نگله‌ها را هدف دارند.</param>
/// <param name="Categories">ثبت‌های این نوع‌ها (پیوند نوع مرحله عوض شد، D.5 #25b).</param>
public sealed record EntryCriteria(
    IReadOnlyCollection<Guid> EntryIds,
    IReadOnlyCollection<Guid> MemberRugs,
    IReadOnlyCollection<Guid> PurchaseGroupRugs,
    IReadOnlyCollection<Guid> StepRugs,
    IReadOnlyCollection<Guid> DeclaredRugs,
    IReadOnlyCollection<Guid> PurchaseCostRugs,
    IReadOnlyCollection<Guid> Batches,
    IReadOnlyCollection<Guid> Shipments,
    IReadOnlyCollection<Guid> Bales,
    IReadOnlyCollection<Guid> Categories)
{
    /// <summary>هیچ معیاری نیست؛ پرس‌وجویی لازم نیست.</summary>
    public bool IsEmpty
        => EntryIds.Count == 0 && MemberRugs.Count == 0 && PurchaseGroupRugs.Count == 0 && StepRugs.Count == 0
           && DeclaredRugs.Count == 0 && PurchaseCostRugs.Count == 0 && Batches.Count == 0 && Shipments.Count == 0
           && Bales.Count == 0 && Categories.Count == 0;
}

/// <summary>ورودی تقسیم یک ثبت (از وضعیتِ flushشده).</summary>
/// <param name="EntryId">ثبت.</param>
/// <param name="Number">شمارهٔ ثبت.</param>
/// <param name="Deleted">حذف نرم شده: هیچ سهمی ندارد.</param>
/// <param name="Method">روش ذخیره‌شده (برای «به نسبت قیمت خرید»).</param>
/// <param name="Entry">آنچه تقسیم‌کننده از ثبت لازم دارد.</param>
/// <param name="Members">اعضای فعلی (بی سطل زباله).</param>
/// <param name="Pins">مبلغ‌های ثابت با شماره.</param>
/// <param name="Stored">سهم‌های ذخیره‌شده.</param>
/// <param name="Result">نتیجهٔ ذخیره‌شده؛ null یعنی هنوز نیست.</param>
public sealed record EntryInput(
    Guid EntryId,
    int Number,
    bool Deleted,
    AllocationMethod Method,
    AllocationEntry Entry,
    IReadOnlyList<AllocationMember> Members,
    IReadOnlyList<AllocationPin> Pins,
    IReadOnlyList<StoredAllocation> Stored,
    StoredResult? Result);

/// <summary>یک سهم ذخیره‌شده.</summary>
public sealed record StoredAllocation(Guid RugId, decimal Amount, long WeightUnits, bool IsPinned);

/// <summary>نتیجهٔ ذخیره‌شده (هشدارها خوانده‌شده از jsonb).</summary>
public sealed record StoredResult(
    int Lines, int Pieces, decimal AreaM2, long TotalWeightUnits, decimal BaseAmount, decimal Allocated, decimal Unallocated,
    AllocationState State, IReadOnlyList<AllocationWarning> Warnings);

/// <summary>آنچه برای یک ثبت نوشته می‌شود: سهم‌های رفته، سهم‌های تازه یا عوض‌شده، و نتیجه (null یعنی عوض نشده).</summary>
public sealed record EntryWrite(
    Guid EntryId,
    IReadOnlyList<Guid> RemovedRugs,
    IReadOnlyList<AllocationShare> Upserts,
    StoredResult? Result);
