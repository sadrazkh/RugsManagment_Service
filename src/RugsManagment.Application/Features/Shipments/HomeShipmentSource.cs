using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Platform.DTOs;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>
/// محموله‌های باز برای خانه (erratum E3؛ جای <see cref="EmptyHomeShipmentSource"/> فاز سکو): همهٔ محموله‌های باز، تازه‌ترین
/// فعالیتِ نگله‌ها اول (A.4)، با جمع‌ها و شمار تراشه‌های بازدارنده از همان قاعده‌های فهرست. هیچ مبلغ تومانی ندارد.
/// </summary>
public sealed class HomeShipmentSource(
    IShipmentRepository shipments,
    IShipmentSettingsReader settingsReader,
    IReadinessService readiness) : IHomeShipmentSource
{
    public async Task<IReadOnlyList<HomeShipmentDto>> ListOpenAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsReader.GetAsync(tenantId, cancellationToken);
        var rows = await shipments.ListOpenForHomeAsync(tenantId, settings.KeyField, cancellationToken);
        return rows.Select(r =>
        {
            var item = ShipmentService.ToListItem(r, settings);
            return new HomeShipmentDto(r.Id, r.Number, r.Title, r.DocumentDate, r.Bales, r.Lines, r.Pieces, r.AreaM2,
                r.UsdTotal, item.ReadinessBlocking, r.LastActivityAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<HomeReadinessDto>> ReadinessAsync(
        Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var issues = await readiness.GetAsync(tenantId, shipmentId, cancellationToken: cancellationToken);
            return issues.Select(i => new HomeReadinessDto(i.Code, i.Message, i.Count, i.Blocking)).ToList();
        }
        catch (KeyNotFoundException)
        {
            // محمولهٔ کارگاه دیگر: قرارداد درز «فهرست خالی» است، نه ۴۰۴
            return [];
        }
    }
}

/// <summary>
/// «انبار» خانه از فاز محموله (جای منبع فاز سکو): فرش‌های فعالی که نه فروخته شده‌اند و نه در هیچ نگله‌ای‌اند؛ تعداد تخته
/// (Σ تعداد) و متراژ کل با قاعدهٔ دسته.
/// </summary>
public sealed class ShipmentHomeStockSource(IShipmentRepository shipments) : IHomeStockSource
{
    public async Task<HomeStock> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var (pieces, area) = await shipments.StockAsync(tenantId, cancellationToken);
        return new HomeStock(pieces, area);
    }
}
