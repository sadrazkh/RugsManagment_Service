using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>
/// ترجیحات کارگاه که کار محموله لازم دارد، یک‌جا: فیلد کلید و نرخ پیش‌فرض ارزش گمرکی، هدف و تلورانس نگله، شرط اندازهٔ
/// نهایی، واژهٔ نگله/عدل، نرخ دلار پیش‌فرض و سربرگ صادرکننده. کارگاهِ بی‌ردیف ترجیحات همان پیش‌فرض‌های
/// <see cref="TenantPreferences.CreateDefault"/> را دارد.
/// </summary>
public sealed record ShipmentSettings(TenantPreferences Preferences, uint Version)
{
    public string KeyField => string.IsNullOrWhiteSpace(Preferences.DeclaredValueKeyField)
        ? TenantPreferences.DefaultDeclaredValueKeyField
        : Preferences.DeclaredValueKeyField;

    public decimal? FallbackUsd => Preferences.DeclaredValueFallbackUsd;

    public bool RequireFinalSize => Preferences.RequireFinalSizeToFinalize;

    public short TolerancePercent => Preferences.BaleTargetTolerancePercent;

    public BaleTermWords Terms => BaleTerms.For(Preferences.BaleTerm);

    /// <summary>متراژ هدف مؤثر: خودِ محموله ?? ترجیح کارگاه.</summary>
    public decimal? TargetFor(decimal? shipmentTarget) => shipmentTarget ?? Preferences.DefaultBaleTargetAreaM2;
}

/// <summary>خواندن <see cref="ShipmentSettings"/> (بی‌ردیابی).</summary>
public interface IShipmentSettingsReader
{
    Task<ShipmentSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ShipmentSettingsReader(ITenantPreferencesRepository preferences) : IShipmentSettingsReader
{
    public async Task<ShipmentSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var row = await preferences.GetAsync(tenantId, cancellationToken);
        return row is null
            ? new ShipmentSettings(TenantPreferences.CreateDefault(tenantId), 0)
            : new ShipmentSettings(row.Preferences, row.Version);
    }
}
