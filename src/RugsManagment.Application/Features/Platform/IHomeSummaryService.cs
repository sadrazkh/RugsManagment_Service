using RugsManagment.Application.Features.Platform.DTOs;

namespace RugsManagment.Application.Features.Platform;

/// <summary>
/// خلاصهٔ صفحهٔ خانه (<c>/api/home/summary</c> و کارت‌های خانه).
///
/// برای انباردار امن است: پیاده‌سازی <c>IMoneyVisibility</c> را می‌گیرد و عضوهای پولی را null و رویدادهای
/// پولی را از «فعالیت‌های اخیر» حذف می‌کند. محموله‌ها از <see cref="IHomeShipmentSource"/> می‌آیند.
/// </summary>
public interface IHomeSummaryService
{
    Task<HomeSummaryDto> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
