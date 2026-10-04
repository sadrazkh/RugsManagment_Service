namespace RugsManagment.Application.Ledger;

/// <summary>
/// «اثر» یک ذخیره روی حساب‌ها (spec D.4 گام ۴): کدام ردیف‌های مشتق‌شده باید از نو حساب شوند. contributorها پرش
/// می‌کنند و مرحله‌ها (stage) از آن می‌خوانند. هر مجموعه فقط شناسه است؛ خودِ محاسبه در SQL روی وضعیتِ flushشده است.
///
/// فازهای بعد مجموعه‌های خودشان را این‌جا دارند (ثبت‌ها، خط‌های محموله، …) تا contributor یک فاز بتواند برای مرحلهٔ
/// فاز دیگر کار بگذارد بی‌آنکه این کلاس را دوباره شکل دهد؛ فاز ۲ فقط چهار مجموعهٔ اول را پر و مصرف می‌کند.
/// </summary>
public sealed class LedgerImpact
{
    /// <summary>فرش‌هایی که ردیف <c>RugCostTotal</c>شان باید از نو نوشته شود (مرحلهٔ آخر).</summary>
    public HashSet<Guid> CostTotalRugs { get; } = [];

    /// <summary>فرش‌هایی که مبلغ سطرهای خریدشان باید از نو حساب شود (اندازهٔ اول، تعداد، CRUD سطر).</summary>
    public HashSet<Guid> PurchaseLineRugs { get; } = [];

    /// <summary>فرش‌هایی که شماره‌شان باید زیر قفل با بقیه برای همپوشانی سنجیده شود (تازه، تغییر شماره، بازگردانی؛ E1).</summary>
    public HashSet<Guid> SerialCheckRugs { get; } = [];

    /// <summary>فرش‌هایی که فروششان در همین ذخیره ثبت شد؛ <c>RugCostTotalStage</c> عکس بها (CostAtSale) را می‌گیرد (D.6).</summary>
    public HashSet<Guid> SaleAddedRugs { get; } = [];

    /// <summary>
    /// فرش‌هایی که چیزی از «عضویت»شان در یک ثبت هزینه عوض شد (اندازه، تعداد، شماره، ورودی، سطل زباله): ثبت‌هایی که
    /// آن‌ها را دارند باید دوباره تقسیم شوند (فاز هزینه‌ها، <c>AffectedEntryFinder</c>).
    /// </summary>
    public HashSet<Guid> MemberRugs { get; } = [];

    /// <summary>ثبت‌های هزینه‌ای که مستقیم عوض شدند (فاز هزینه‌ها).</summary>
    public HashSet<Guid> Entries { get; } = [];

    /// <summary>ورودی‌هایی (RugBatch) که عضویتشان عوض شد یا حذف شدند (فاز هزینه‌ها: ثبت‌های «این ورودی»).</summary>
    public HashSet<Guid> Batches { get; } = [];

    /// <summary>محموله‌هایی که خط، نگله یا درآمدشان عوض شد (فاز محموله).</summary>
    public HashSet<Guid> Shipments { get; } = [];

    /// <summary>نگله‌هایی که عضویتشان عوض شد (فاز محموله).</summary>
    public HashSet<Guid> Bales { get; } = [];

    /// <summary>خط‌های محموله‌ای که ارزش دلاری‌شان باید از نو حساب شود (فاز محموله).</summary>
    public HashSet<Guid> ShipmentLines { get; } = [];

    /// <summary>
    /// اثری نیست: ذخیره فقط به چیزهایی رسیده که قفل‌ها می‌سنجند (توصیف، وضعیت، …)؛ هیچ مرحله‌ای اجرا نمی‌شود (D.4 گام ۷).
    /// </summary>
    public bool IsEmpty
        => CostTotalRugs.Count == 0 && PurchaseLineRugs.Count == 0 && SerialCheckRugs.Count == 0
           && SaleAddedRugs.Count == 0 && MemberRugs.Count == 0 && Entries.Count == 0 && Batches.Count == 0
           && Shipments.Count == 0 && Bales.Count == 0 && ShipmentLines.Count == 0;

    /// <summary>
    /// «همه‌چیزِ این فرش»: برای ردیف تازه، ردیف با مقدار اصلیِ نامعلوم و بازسازی. سطرهای خرید، جمع، شماره و عضویت.
    /// </summary>
    public void AddFullRug(Guid rugId)
    {
        CostTotalRugs.Add(rugId);
        PurchaseLineRugs.Add(rugId);
        SerialCheckRugs.Add(rugId);
        MemberRugs.Add(rugId);
    }

    public override string ToString()
        => $"totals={CostTotalRugs.Count} lines={PurchaseLineRugs.Count} serial={SerialCheckRugs.Count} " +
           $"sales={SaleAddedRugs.Count} members={MemberRugs.Count} entries={Entries.Count}";
}
