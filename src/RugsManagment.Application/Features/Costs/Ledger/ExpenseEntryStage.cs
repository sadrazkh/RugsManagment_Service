using RugsManagment.Application.Ledger;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// تقسیم ثبت‌های گروه «هزینه» (ترتیب ۶۰۰، spec D.1، D.4): شستشو، کرایه، گمرک، انبارداری، … بعد از جمع خرید (۵۰۰) اجرا
/// می‌شود، چون «به نسبت قیمت خرید» قیمت تازه را لازم دارد (<see cref="ByPurchaseCostExpander"/>)، و بعد از ارزش خط (۳۰۰)،
/// چون «به نسبت ارزش گمرکی» دلار تازهٔ خط را لازم دارد (مرحلهٔ ۴۰۰ آن ثبت‌ها را پیدا کرده است). سهم‌ها در
/// <c>RugCostTotal.ExpenseCost</c> با مرحلهٔ ۱۰۰۰ می‌نشینند.
/// </summary>
public sealed class ExpenseEntryStage(ICostLedgerRepository repository) : ILedgerStage
{
    public int Order => LedgerStageOrder.ExpenseEntry;

    public async Task RunAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var state = CostPassState.Of(context);
        var inputs = await ByPurchaseCostExpander.ExpandAsync(context, repository, state.Expense ?? [], cancellationToken);
        await EntryRunner.RunAsync(context, repository, inputs, cancellationToken);
    }
}
