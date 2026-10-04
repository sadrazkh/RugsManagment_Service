namespace RugsManagment.Application.Ledger.Stages;

/// <summary>
/// مبلغ سطرهای خرید (ترتیب ۲۰۰، spec B.6 و قاعدهٔ دستهٔ D.3) برای فرش‌هایی که اندازهٔ اول، تعداد یا خودِ سطرهایشان عوض
/// شده: «کل» ROUND(rate, s)؛ «هر متر» ROUND(rate × مساحت یک تخته در اندازهٔ اول، s) × تعداد؛ «هر تخته» ROUND(rate, s) ×
/// تعداد؛ «درصد» یک بار ROUND(جمع سطرهای غیردرصدی × p / 100, s). فرمول فقط در CostSql (و همتای C#ش RugCostMath) است و
/// فقط سطرهایی که مبلغشان واقعاً عوض می‌شود نوشته می‌شوند.
/// </summary>
public sealed class PurchaseLineStage(ILedgerRepository repository) : ILedgerStage
{
    public int Order => LedgerStageOrder.PurchaseLine;

    public async Task RunAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        if (context.Impact.PurchaseLineRugs.Count == 0)
            return;

        await repository.RecomputePurchaseLinesAsync(
            context.TenantId, context.Impact.PurchaseLineRugs, context.MoneyScale, cancellationToken);
    }
}
