using RugsManagment.Application.Features.Platform.DTOs;

namespace RugsManagment.Application.Features.Platform;

/// <summary>
/// منبع محموله‌های باز برای صفحهٔ خانه — درزی که فاز محموله پر می‌کند (erratum E3).
///
/// چرا درز و نه کوئری مستقیم: جدول محموله‌ها تا فاز محموله وجود ندارد، ولی خانه و <c>/api/home/summary</c>
/// از همین فاز کار می‌کنند. پس فاز سکو <see cref="EmptyHomeShipmentSource"/> را ثبت می‌کند (خانه حالتِ
/// «هیچ محمولهٔ بازی نیست» را نشان می‌دهد) و فاز محموله پیاده‌سازی خودش را جایگزین می‌کند:
/// <c>services.Replace(ServiceDescriptor.Scoped&lt;IHomeShipmentSource, ShipmentHomeSource&gt;())</c>
/// — بی‌آنکه سرویس خلاصهٔ خانه را ویرایش کند.
///
/// همهٔ کوئری‌ها با <c>tenantId</c> محدود می‌شوند و هیچ مبلغ تومانی برنمی‌گردانند.
/// </summary>
public interface IHomeShipmentSource
{
    /// <summary>همهٔ محموله‌های باز کارگاه، تازه‌ترین فعالیتِ نگله اول.</summary>
    Task<IReadOnlyList<HomeShipmentDto>> ListOpenAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>کمبودهای آمادگیِ یک محمولهٔ باز (محمولهٔ کارگاه دیگر → فهرست خالی).</summary>
    Task<IReadOnlyList<HomeReadinessDto>> ReadinessAsync(
        Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);
}

/// <summary>پیاده‌سازی فاز سکو: هنوز محموله‌ای وجود ندارد.</summary>
public sealed class EmptyHomeShipmentSource : IHomeShipmentSource
{
    public Task<IReadOnlyList<HomeShipmentDto>> ListOpenAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<HomeShipmentDto>>([]);

    public Task<IReadOnlyList<HomeReadinessDto>> ReadinessAsync(
        Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<HomeReadinessDto>>([]);
}
