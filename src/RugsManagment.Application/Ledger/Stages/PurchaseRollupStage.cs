namespace RugsManagment.Application.Ledger.Stages;

/// <summary>
/// جمع «قیمت خرید» هر فرش (ترتیب ۵۰۰، spec D.1 و قاعدهٔ انتشار D.4): سطرهای خرید (که مرحلهٔ ۲۰۰ تازه کرده) + سهم
/// ثبت‌های گروه خرید (که مرحلهٔ ۴۰۰ فاز هزینه‌ها تقسیم کرده). مقدار تازه در <c>RugCostTotal.PurchaseCost</c> نوشته می‌شود
/// و فرش‌هایی که قیمت خریدشان واقعاً عوض شد در <see cref="LedgerContext.PurchaseCostChangedRugs"/> می‌روند تا مرحلهٔ ۶۰۰
/// (ثبت‌های «به نسبت قیمت خرید») هر ثبتی را که با آن‌ها اشتراک دارد دوباره تقسیم کند — پس هیچ ردیف D.5ای لازم نیست این
/// اثر درجهٔ دوم را خودش بشمارد. همان فرش‌ها به جمع نهایی (مرحلهٔ ۱۰۰۰) هم اضافه می‌شوند.
/// </summary>
public sealed class PurchaseRollupStage(ILedgerRepository repository, ICostSourceProvider costSources) : ILedgerStage
{
    public int Order => LedgerStageOrder.PurchaseRollup;

    public async Task RunAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var candidates = context.Impact.CostTotalRugs.Union(context.Impact.PurchaseLineRugs).ToList();
        if (candidates.Count == 0)
            return;

        var sources = await costSources.GetAsync(context.TenantId, candidates, cancellationToken);
        var changed = await repository.RollupPurchaseCostAsync(
            context.TenantId, candidates, sources, context.Now, LedgerVersion.Current, cancellationToken);

        context.PurchaseCostChangedRugs.UnionWith(changed);
        context.Impact.CostTotalRugs.UnionWith(changed);
    }
}
