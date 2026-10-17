using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// تقسیم ثبت‌های گروه «خرید» (ترتیب ۴۰۰، spec D.1، D.4): خرید یکجای ورودی (عکس فرش‌ها، E4) و هر ثبت خرید دیگر. پیش از
/// جمع خرید (۵۰۰) اجرا می‌شود تا سهم خرید یکجا در <c>RugCostTotal.PurchaseCost</c> بنشیند و ثبت‌های «به نسبت قیمت خرید» در
/// ۶۰۰ همان قیمت تازه را ببینند.
///
/// <para>یک پرس‌وجو همهٔ ثبت‌های اثر را پیدا می‌کند (<see cref="AffectedEntryFinder"/>) و یک دستور ورودی هر دو گروه را می‌خواند؛
/// ورودی‌های گروه «هزینه» در <see cref="CostPassState"/> برای مرحلهٔ ۶۰۰ می‌مانند (ورودی‌شان تا آن‌جا عوض نمی‌شود، جز
/// قیمت خرید که <see cref="ByPurchaseCostExpander"/> دوباره می‌خواند). در بازسازی همهٔ ثبت‌های کارگاه.</para>
/// </summary>
public sealed class PurchaseEntryStage(ICostLedgerRepository repository) : ILedgerStage
{
    public int Order => LedgerStageOrder.PurchaseEntry;

    public async Task RunAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var state = CostPassState.Of(context);
        var ids = await AffectedEntryFinder.FindAsync(context, repository, cancellationToken);
        if (ids is { Count: 0 })
        {
            state.Expense = [];
            return;
        }

        var inputs = await repository.LoadAsync(context.TenantId, ids, cancellationToken);
        state.Expense = inputs.Where(i => i.Entry.Group == CostGroup.Expense).ToList();
        await EntryRunner.RunAsync(context, repository, inputs.Where(i => i.Entry.Group == CostGroup.Purchase).ToList(), cancellationToken);
    }
}
