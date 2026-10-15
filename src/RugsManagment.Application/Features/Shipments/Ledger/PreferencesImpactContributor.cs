using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Shipments.Ledger;

/// <summary>
/// اثر تغییر «ارزش گمرکی بر اساس» (<c>DeclaredValueKeyField</c>) یا «ارزش گمرکی وقتی در جدول نیست»
/// (<c>DeclaredValueFallbackUsd</c>) در ترجیحات (spec D.5 #21): همهٔ خط‌های محموله‌های باز که نرخ دستی ندارند دوباره
/// ارزش‌گذاری می‌شوند (مرحله کلید تازه را بعد از flush می‌خواند). مهر جدول (<c>DeclaredValuesStamp</c>) و بقیهٔ ترجیحات
/// اثری ندارند (D.5 #23).
/// </summary>
public sealed class PreferencesImpactContributor(IShipmentLedgerRepository repository) : IChangeImpactContributor
{
    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(TenantPreferences)];

    public async Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var changed = context.Changes.Of<TenantPreferences>().Any(c => c.Kind switch
        {
            ChangeKind.Modified => c.HasAny(nameof(TenantPreferences.DeclaredValueKeyField), nameof(TenantPreferences.DeclaredValueFallbackUsd)),
            ChangeKind.Unknown => true,
            _ => false
        });
        if (!changed)
            return;

        // کلید فعلی دیتابیس فقط برای پیدا کردن خط‌هاست؛ کلید تازه را مرحله بعد از flush می‌خواند
        var settings = await repository.SettingsAsync(context.TenantId, cancellationToken);
        foreach (var line in await repository.OpenLinesWithoutOverrideAsync(context.TenantId, settings.KeyField, cancellationToken))
        {
            context.Impact.ShipmentLines.Add(line.LineId);
            context.Impact.Shipments.Add(line.ShipmentId);
        }
    }
}
