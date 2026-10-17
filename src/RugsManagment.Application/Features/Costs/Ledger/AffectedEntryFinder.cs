using RugsManagment.Application.Ledger;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// «کدام ثبت‌های هزینه با این ذخیره دوباره تقسیم شوند؟» (spec D.5، D.4 قاعدهٔ انتشار). از اثرِ پاس معیار می‌سازد و
/// <see cref="ICostLedgerRepository.FindEntriesAsync"/> با ایندکس‌های <c>CostEntryTarget.RugId</c>، <c>CostEntryTarget.BaleId</c>،
/// <c>CostEntry.ShipmentId</c> و <c>CostEntry.BatchId</c> پیدایشان می‌کند — روی وضعیتِ flushشده، پس عضویت فعلی را می‌بیند.
///
/// <list type="bullet">
/// <item>عضویت (اندازه، تعداد، شماره، ورودی، سطل زباله، خط): هر ثبتی که فرش را دارد، با هر دامنه‌ای؛ ورودی و نگله و محمولهٔ
/// قدیمی را contributorها در اثر گذاشته‌اند.</item>
/// <item>سطر خرید: فقط ثبت‌های گروه خرید (هشدار «دوبار حساب شده»).</item>
/// <item>هزینهٔ مرحله (هر فرشی که جمعش باید از نو حساب شود): فقط ثبت‌هایی که نوعشان به نوع مرحله‌ای وصل است.</item>
/// <item>دلار خط (مرحلهٔ ۳۰۰): ثبت‌های «به نسبت ارزش گمرکی».</item>
/// <item>قیمت خرید (مرحلهٔ ۵۰۰): ثبت‌های «به نسبت قیمت خرید» — این یکی را <see cref="ByPurchaseCostExpander"/> در مرحلهٔ ۶۰۰ می‌پرسد.</item>
/// </list>
/// ثبت‌هایی که نگله، محموله یا ورودی‌شان حذف می‌شود پیش از flush پیدا شده‌اند و مستقیم در <see cref="LedgerImpact.Entries"/> اند.
/// </summary>
public static class AffectedEntryFinder
{
    /// <summary>معیار مرحلهٔ ۴۰۰ (همهٔ گروه‌ها، بی «قیمت خرید» که هنوز جمع نشده).</summary>
    public static EntryCriteria Criteria(LedgerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var impact = context.Impact;
        var state = CostPassState.Of(context);
        return new EntryCriteria(
            impact.Entries.ToList(),
            impact.MemberRugs.ToList(),
            impact.PurchaseLineRugs.ToList(),
            impact.CostTotalRugs.ToList(),
            context.LineUsdChangedRugs.ToList(),
            [],
            impact.Batches.ToList(),
            impact.Shipments.ToList(),
            impact.Bales.ToList(),
            state.Categories.ToList());
    }

    /// <summary>معیار «به نسبت قیمت خرید» برای فرش‌هایی که قیمت خریدشان در این پاس عوض شد (مرحلهٔ ۶۰۰).</summary>
    public static EntryCriteria PurchaseCostCriteria(IReadOnlyCollection<Guid> rugs)
        => new([], [], [], [], [], rugs, [], [], [], []);

    /// <summary>ثبت‌های مربوط به این پاس (یا همهٔ ثبت‌ها در بازسازی: null).</summary>
    public static async Task<IReadOnlyCollection<Guid>?> FindAsync(
        LedgerContext context, ICostLedgerRepository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(repository);
        if (context.IsRebuild)
            return null;

        var criteria = Criteria(context);
        if (criteria.IsEmpty)
            return [];

        return await repository.FindEntriesAsync(context.TenantId, criteria, cancellationToken);
    }
}

/// <summary>
/// وضع دفتر هزینه در یک پاس (در <see cref="LedgerContext.Items"/>، با پاس می‌رود): نوع‌هایی که پیوند نوع مرحله‌شان عوض شد
/// (contributor پر می‌کند) و ورودی‌های ثبت‌های گروه «هزینه» که مرحلهٔ ۴۰۰ خوانده و مرحلهٔ ۶۰۰ تقسیم می‌کند (یک پرس‌وجو برای هر دو گروه).
/// </summary>
public sealed class CostPassState
{
    /// <summary>نوع‌های هزینه‌ای که <c>StepTypeId</c>شان در این ذخیره عوض شد.</summary>
    public HashSet<Guid> Categories { get; } = [];

    /// <summary>ثبت‌های گروه «هزینه» که مرحلهٔ ۴۰۰ بار کرد؛ null یعنی مرحلهٔ ۴۰۰ چیزی نخواند.</summary>
    public IReadOnlyList<EntryInput>? Expense { get; set; }

    public static CostPassState Of(LedgerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Items.TryGetValue(typeof(CostPassState), out var existing) && existing is CostPassState state)
            return state;

        state = new CostPassState();
        context.Items[typeof(CostPassState)] = state;
        return state;
    }
}
