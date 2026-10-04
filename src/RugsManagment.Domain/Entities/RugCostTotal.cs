using RugsManagment.Domain.Common;
using RugsManagment.Domain.Costing;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// بهای تمام‌شدهٔ مشتق‌شدهٔ یک فرش (spec B.8، D.1) — یک ردیف برای هر فرش، کلید همان <see cref="RugId"/>.
///
/// چرا ذخیره‌شده: پیش از این «جمع سرمایه» در هفت جای کد (فهرست، مرتب‌سازی، سطل زباله، داشبورد، تحلیل، فروش، seed) هر
/// بار از نو و هر جا کمی متفاوت حساب می‌شد. حالا فقط پاس سازگاری (<c>RugCostTotalStage</c>) این ردیف را با
/// <see cref="RugCostMath"/>/CostSql می‌نویسد و همهٔ آن هفت جا همین را می‌خوانند؛ پس فهرست و جزئیات و گزارش یک عدد
/// می‌بینند. هیچ کد دیگری در این جدول نمی‌نویسد، xmin ندارد و نوشتنش نسخهٔ فرش را عوض نمی‌کند.
/// <see cref="LedgerVersion"/> نسخهٔ قاعده‌هاست؛ با عوض شدنش، بررسی استارت همه را از نو می‌سازد.
/// </summary>
public class RugCostTotal : ITenantScoped
{
    public Guid RugId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>سطرهای خرید + سهم هزینه‌های گروه خرید.</summary>
    public decimal PurchaseCost { get; set; }

    /// <summary>جمع هزینهٔ مؤثر مراحل انجام‌شده و در حال انجام (هر کدام کف صفر).</summary>
    public decimal StepCost { get; set; }

    /// <summary>سهم هزینه‌های گروه «هزینه».</summary>
    public decimal ExpenseCost { get; set; }

    /// <summary>بهای تمام‌شده = خرید + مراحل + هزینه‌ها.</summary>
    public decimal Total { get; set; }

    /// <summary>بها برای هر متر مربع (کل متراژ ردیف)؛ null وقتی متراژ صفر است.</summary>
    public decimal? CostPerM2 { get; set; }

    /// <summary>بها برای هر تخته.</summary>
    public decimal CostPerPiece { get; set; }

    /// <summary>سهم این فرش از درآمد صادرات محموله (به ارز پایه)؛ null وقتی محموله درآمد ثبت‌شده ندارد.</summary>
    public decimal? ExportRevenueShare { get; set; }

    public DateTimeOffset ComputedAt { get; set; }

    /// <summary>نسخهٔ قاعده‌های محاسبه که این ردیف با آن ساخته شد.</summary>
    public long LedgerVersion { get; set; }

    public Rug Rug { get; set; } = null!;

    /// <summary>ستون‌های ترکیب را از یک <see cref="RugCostBreakdown"/> می‌نویسد.</summary>
    public void Apply(RugCostBreakdown breakdown, DateTimeOffset computedAt, long ledgerVersion)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        PurchaseCost = breakdown.PurchaseCost;
        StepCost = breakdown.StepCost;
        ExpenseCost = breakdown.ExpenseCost;
        Total = breakdown.Total;
        CostPerM2 = breakdown.CostPerM2;
        CostPerPiece = breakdown.CostPerPiece;
        ComputedAt = computedAt;
        LedgerVersion = ledgerVersion;
    }
}
