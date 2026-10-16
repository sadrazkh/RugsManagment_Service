using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// قاعدهٔ انتشار «قیمت خرید» (spec D.4): جمع خرید (۵۰۰) فرش‌هایی را که قیمت خریدشان در این پاس عوض شد در
/// <see cref="LedgerContext.PurchaseCostChangedRugs"/> می‌گذارد (تغییر اندازهٔ اول روی سطر متری، CRUD سطر خرید، خرید یکجا)؛
/// هر ثبت حذف‌نشدهٔ «به نسبت قیمت خرید» که عضوهای فعلی‌اش با آن‌ها اشتراک دارد — با هر دامنه‌ای — باید دوباره تقسیم شود،
/// با قیمت تازه. ثبت‌های «به نسبت قیمت خرید» که مرحلهٔ ۴۰۰ پیش‌تر خوانده بود هم دوباره خوانده می‌شوند، چون وزنشان (قیمت
/// خرید) بعد از ۴۰۰ عوض شده است.
/// </summary>
public static class ByPurchaseCostExpander
{
    /// <summary>
    /// ورودی‌های گروه «هزینه» برای مرحلهٔ ۶۰۰: ورودی‌های بارشدهٔ ۴۰۰، که ثبت‌های «به نسبت قیمت خرید»شان (اگر قیمتی عوض
    /// شده) با ورودی تازه جایگزین و ثبت‌های تازه‌پیداشده به آن‌ها افزوده شده‌اند.
    /// </summary>
    public static async Task<IReadOnlyList<EntryInput>> ExpandAsync(
        LedgerContext context, ICostLedgerRepository repository, IReadOnlyList<EntryInput> loaded, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(loaded);

        var changed = context.PurchaseCostChangedRugs;
        if (changed.Count == 0)
            return loaded;

        var stale = loaded.Where(i => i.Method == AllocationMethod.ByPurchaseCost).Select(i => i.EntryId).ToHashSet();
        IReadOnlyCollection<Guid>? reload;
        if (context.IsRebuild)
        {
            // بازسازی: ۴۰۰ همه را خوانده بود؛ همهٔ «به نسبت قیمت خرید»ها با قیمت تازه
            reload = stale;
        }
        else
        {
            var found = await repository.FindEntriesAsync(
                context.TenantId, AffectedEntryFinder.PurchaseCostCriteria(changed.ToList()), cancellationToken);
            reload = stale.Union(found).ToHashSet();
        }

        if (reload.Count == 0)
            return loaded;

        var fresh = await repository.LoadAsync(context.TenantId, reload, cancellationToken);
        var freshIds = fresh.Select(i => i.EntryId).ToHashSet();
        return loaded.Where(i => !freshIds.Contains(i.EntryId))
            .Concat(fresh.Where(i => i.Entry.Group == CostGroup.Expense))
            .ToList();
    }
}
