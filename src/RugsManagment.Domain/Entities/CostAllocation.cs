using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// سهم یک فرش از یک ثبت هزینه (spec B.10، مشتق‌شده) — فقط مرحله‌های پاس سازگاری (<c>PurchaseEntryStage</c>،
/// <c>ExpenseEntryStage</c>) می‌نویسند، با diff (فقط سهمی که عوض می‌شود). کلید (ثبت، فرش)؛ xmin ندارد و نوشتنش نسخهٔ فرش یا
/// ثبت را عوض نمی‌کند. جمع سهم‌های هر فرش (جدا برای گروه خرید و هزینه) در <c>RugCostTotal</c> می‌نشیند.
/// </summary>
public class CostAllocation : ITenantScoped
{
    public Guid EntryId { get; set; }

    public Guid RugId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>سهم به ارز کارگاه (numeric(18,2)).</summary>
    public decimal Amount { get; set; }

    /// <summary>وزن به کار رفته (صدم متر، تعداد، سنت، …؛ bigint) — برای نمایش «سهم از کل».</summary>
    public long WeightUnits { get; set; }

    /// <summary>این سهم مبلغ ثابت (pin) است.</summary>
    public bool IsPinned { get; set; }

    public CostEntry Entry { get; set; } = null!;

    public Rug Rug { get; set; } = null!;
}
