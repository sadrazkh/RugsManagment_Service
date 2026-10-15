using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>
/// تکمیل <see cref="RugDto"/> با محموله، نگله و ارزش دلاری خط (درز فاز ۲، spec I.3) — یک پرس‌وجو با RugId برای همهٔ DTOها.
/// جای فرش (C.10): «سطل زباله» و «فروخته‌شده» را سازندهٔ DTO پیش‌تر گذاشته و دست نمی‌خورند؛ بقیه با خط در محمولهٔ قطعی
/// «صادر شده»، با خط در محمولهٔ باز «در نگله»، و بی خط همان «در انبار». دلار گمرکی هزینهٔ داخلی نیست و پنهان نمی‌شود.
/// </summary>
public sealed class ShipmentRugDtoEnricher(IShipmentRepository shipments, ICurrentUser currentUser) : IRugDtoEnricher
{
    public async Task<IReadOnlyList<RugDto>> EnrichAsync(IReadOnlyList<RugDto> rugs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rugs);
        if (rugs.Count == 0) return rugs;

        // کارگاه کاربر جاری؛ بیرون از درخواست (seed، کار استارت) کارگاهِ خودِ فرش‌ها
        var ids = rugs.Select(r => r.Id).Distinct().ToList();
        var placements = new Dictionary<Guid, RugPlacementData>();
        foreach (var tenantId in await TenantsAsync(ids, cancellationToken))
        {
            foreach (var p in await shipments.PlacementsAsync(tenantId, ids, cancellationToken))
                placements[p.RugId] = p;
        }

        return rugs.Select(r => placements.TryGetValue(r.Id, out var p) ? Apply(r, p) : r).ToList();
    }

    /// <summary>عضوهای محموله/نگله/دلار و جای فرش روی یک DTO.</summary>
    public static RugDto Apply(RugDto rug, RugPlacementData placement)
    {
        ArgumentNullException.ThrowIfNull(rug);
        ArgumentNullException.ThrowIfNull(placement);
        var location = rug.Location is RugLocation.Trashed or RugLocation.Sold
            ? rug.Location
            : placement.ShipmentStatus == ShipmentStatus.Finalized ? RugLocation.Exported : RugLocation.InBale;

        return rug with
        {
            ShipmentId = placement.ShipmentId,
            ShipmentNumber = placement.ShipmentNumber,
            BaleId = placement.BaleId,
            BaleNumber = placement.BaleNumber,
            UsdPerM2 = placement.UsdPerM2,
            LineUsd = placement.LineUsd,
            Location = location
        };
    }

    /// <summary>
    /// کارگاهی که پرس‌وجو به آن محدود می‌شود: کارگاه کاربر جاری؛ بی کاربر (seed، کار استارت) کارگاه(های) خودِ این فرش‌ها،
    /// چون DTO فرش کارگاه ندارد.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> TenantsAsync(IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
        => currentUser.TenantId is { } tenantId ? [tenantId] : await shipments.TenantsOfRugsAsync(rugIds, cancellationToken);
}
