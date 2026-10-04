namespace RugsManagment.Application.Ledger.Stages;

/// <summary>
/// جمع بهای تمام‌شدهٔ هر فرش (ترتیب ۱۰۰۰، <b>همیشه آخر</b>؛ spec D.1، D.4، D.6): ردیف کامل <c>RugCostTotal</c> با CostSql —
/// خرید (سطرها + سهم خرید یکجا) + مراحل (فقط «انجام‌شده» و «در حال انجام»، هر کدام کف صفر) + سهم هزینه‌ها، و بها برای هر
/// متر مربع (ROUND(کل ÷ متراژ کل ردیف، s)، null وقتی متراژ صفر است) و برای هر تخته. فقط ردیف‌هایی که عوض می‌شوند نوشته
/// می‌شوند و نوشتن هرگز به <c>Rugs.xmin</c> نمی‌رسد.
///
/// بعد از آن، برای فروش‌هایی که در همین ذخیره ثبت شدند، عکس بها (<c>RugSale.CostAtSale</c> = همین کلِ تازه) گرفته
/// می‌شود؛ پس عکس، تغییرهای همین پاس را هم دارد (D.6). در بازسازی عکس گرفته نمی‌شود: عکس فروش‌های قدیمی دست نمی‌خورد.
/// </summary>
public sealed class RugCostTotalStage(ILedgerRepository repository, ICostSourceProvider costSources) : ILedgerStage
{
    public int Order => LedgerStageOrder.RugCostTotal;

    public async Task RunAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var impact = context.Impact;
        var rugs = impact.CostTotalRugs
            .Union(context.PurchaseCostChangedRugs)
            .Union(impact.SaleAddedRugs)
            .ToList();
        if (rugs.Count == 0)
            return;

        // سهم‌ها دوباره خوانده می‌شوند: مرحله‌های ۶۰۰ و ۷۰۰ (فاز هزینه‌ها) بعد از جمع خرید تقسیم کرده‌اند
        var sources = await costSources.GetAsync(context.TenantId, rugs, cancellationToken);
        await repository.UpsertCostTotalsAsync(
            context.TenantId, rugs, sources, context.MoneyScale, context.Now, LedgerVersion.Current, cancellationToken);

        if (!context.IsRebuild && impact.SaleAddedRugs.Count > 0)
            await repository.SnapshotCostAtSaleAsync(context.TenantId, impact.SaleAddedRugs, context.Now, cancellationToken);
    }
}
