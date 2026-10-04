using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Ledger.Contributors;

/// <summary>
/// اثر CRUD سطرهای خرید (spec D.5 #9): مبلغ همهٔ سطرهای همان فرش (سطر درصدی به جمع بقیه بسته است) و جمع هزینه‌اش.
/// ثبت‌های «به نسبت قیمت خرید» با قاعدهٔ انتشار D.4 از <c>PurchaseRollupStage</c> دنبال می‌شوند، نه این‌جا. برچسب،
/// یادداشت، ترتیب و نوع پولی نیستند و اثری ندارند.
/// </summary>
public sealed class PurchaseLineImpactContributor : IChangeImpactContributor
{
    private static readonly string[] MoneyProperties =
        [nameof(RugPurchaseLine.Basis), nameof(RugPurchaseLine.Rate), nameof(RugPurchaseLine.Amount), nameof(RugPurchaseLine.RugId)];

    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(RugPurchaseLine)];

    public Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var impact = context.Impact;
        foreach (var change in context.Changes.Of<RugPurchaseLine>())
        {
            if (change.Kind == ChangeKind.Modified && !change.HasAny(MoneyProperties))
                continue;

            var line = change.As<RugPurchaseLine>();
            Add(impact, line.RugId);

            // سطری که به فرش دیگری رفت (یا حذف شد): فرش قبلی هم
            if (change.Original<Guid>(nameof(RugPurchaseLine.RugId)) is var oldRug && oldRug != Guid.Empty && oldRug != line.RugId)
                Add(impact, oldRug);
        }

        return Task.CompletedTask;
    }

    private static void Add(LedgerImpact impact, Guid rugId)
    {
        impact.PurchaseLineRugs.Add(rugId);
        impact.CostTotalRugs.Add(rugId);
    }
}
