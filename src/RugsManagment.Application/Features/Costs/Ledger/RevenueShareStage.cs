using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// سهم درآمد صادرات (ترتیب ۷۰۰، spec D.6): درآمد محموله به ارز کارگاه (ROUND(مبلغ × نرخ، s) یک بار) با «بزرگ‌ترین باقی‌مانده»
/// به نسبت سنت دلار هر خط بین فرش‌هایش تقسیم می‌شود. خودِ تقسیم در منبع سهم‌ها (<see cref="ICostSourceProvider"/>، همان که
/// مرحلهٔ ۱۰۰۰ و سنجش سلامت می‌خوانند) است؛ این مرحله فقط فرش‌هایی را که سهمشان ممکن است عوض شود به جمع هزینه می‌رساند:
/// <list type="bullet">
/// <item>همهٔ فرش‌های محموله‌ای که درآمد، ارز یا نرخش در این ذخیره عوض شد (پاک شدن درآمد هم: سهم‌ها null می‌شوند)؛</item>
/// <item>فرش‌های محموله‌های درآمددار که دلار یکی از خط‌هایشان عوض شد (وزن‌ها عوض شد)، و فرشی که از چنین محموله‌ای بیرون رفت.</item>
/// </list>
/// </summary>
public sealed class RevenueShareStage(ICostLedgerRepository repository) : ILedgerStage
{
    private static readonly string[] RevenueFields =
        [nameof(Shipment.RevenueAmount), nameof(Shipment.RevenueCurrency), nameof(Shipment.RevenueFx)];

    public int Order => LedgerStageOrder.RevenueShare;

    public async Task RunAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        // بازسازی همهٔ فرش‌ها را در جمع دارد
        if (context.IsRebuild)
            return;

        var shipments = context.Changes.Of<Shipment>()
            .Where(c => c.Kind != ChangeKind.Deleted && c.HasAny(RevenueFields))
            .Select(c => c.As<Shipment>().Id)
            .ToHashSet();
        var lineRugs = context.LineUsdChangedRugs;
        if (shipments.Count == 0 && lineRugs.Count == 0)
            return;

        var rugs = await repository.RevenueRugsAsync(context.TenantId, shipments, lineRugs.ToList(), cancellationToken);
        context.Impact.CostTotalRugs.UnionWith(rugs);
    }
}
