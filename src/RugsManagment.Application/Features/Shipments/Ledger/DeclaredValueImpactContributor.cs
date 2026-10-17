using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Shipments.Ledger;

/// <summary>
/// اثر تغییر جدول ارزش گمرکی (spec D.5 #20، و افزودن بازرگان C.5): خط‌های محموله‌های باز که نرخ دستی ندارند و مقدار
/// یکسان‌شدهٔ فیلد کلیدشان یکی از کلیدهای افزوده، ویرایش‌شده یا حذف‌شده است، دوباره ارزش‌گذاری می‌شوند. خط‌های محمولهٔ قطعی
/// منجمدند و دست نمی‌خورند. ثبت‌های «به نسبت ارزش اظهاری» و سهم درآمد از قاعدهٔ انتشار D.4 پیروی می‌کنند.
/// </summary>
public sealed class DeclaredValueImpactContributor(IShipmentLedgerRepository repository) : IChangeImpactContributor
{
    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(DeclaredValueRate)];

    public async Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var everything = false;
        foreach (var change in context.Changes.Of<DeclaredValueRate>())
        {
            var rate = change.As<DeclaredValueRate>();
            switch (change.Kind)
            {
                case ChangeKind.Added:
                    keys.Add(rate.KeyNormalized);
                    break;
                case ChangeKind.Deleted:
                    keys.Add(change.Original<string>(nameof(DeclaredValueRate.KeyNormalized)) ?? rate.KeyNormalized);
                    break;
                case ChangeKind.Modified:
                    if (change.HasAny(nameof(DeclaredValueRate.UsdPerM2), nameof(DeclaredValueRate.KeyNormalized)))
                    {
                        keys.Add(rate.KeyNormalized);
                        if (change.Original<string>(nameof(DeclaredValueRate.KeyNormalized)) is { } old) keys.Add(old);
                    }

                    break;
                case ChangeKind.Unknown:
                    everything = true;
                    break;
            }
        }

        if (keys.Count == 0 && !everything)
            return;

        var settings = await repository.SettingsAsync(context.TenantId, cancellationToken);
        foreach (var line in await repository.OpenLinesWithoutOverrideAsync(context.TenantId, settings.KeyField, cancellationToken))
        {
            if (!everything && !keys.Contains(DeclaredValueMath.KeyNormalize(line.KeyValue)))
                continue;
            context.Impact.ShipmentLines.Add(line.LineId);
            context.Impact.Shipments.Add(line.ShipmentId);
        }
    }
}
