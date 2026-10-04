using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Ledger.Contributors;

/// <summary>
/// اثر فروش (spec D.5 #11، D.6): فروش تازه → جمع هزینهٔ فرش و عکس بهای تمام‌شده (<c>CostAtSale</c>) که
/// <c>RugCostTotalStage</c> بعد از حساب کردن جمعِ همین پاس می‌گیرد — پس فروشی که در همان درخواستِ یک تغییر هزینه ثبت شود
/// بهای بعد از آن تغییر را می‌گیرد. لغو فروش → جمع. ویرایش فروش عکس را دست نمی‌زند (D.6).
/// </summary>
public sealed class SaleImpactContributor : IChangeImpactContributor
{
    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(RugSale)];

    public Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var impact = context.Impact;
        foreach (var change in context.Changes.Of<RugSale>())
        {
            var sale = change.As<RugSale>();
            switch (change.Kind)
            {
                case ChangeKind.Added:
                    impact.CostTotalRugs.Add(sale.RugId);
                    impact.SaleAddedRugs.Add(sale.RugId);
                    break;

                case ChangeKind.Deleted:
                case ChangeKind.Unknown:
                    impact.CostTotalRugs.Add(sale.RugId);
                    break;

                case ChangeKind.Modified when change.Has(nameof(RugSale.RugId)):
                    impact.CostTotalRugs.Add(sale.RugId);
                    if (change.Original<Guid>(nameof(RugSale.RugId)) is var oldRug && oldRug != Guid.Empty)
                        impact.CostTotalRugs.Add(oldRug);
                    break;
            }
        }

        return Task.CompletedTask;
    }
}
