using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Platform;

/// <summary>
/// خلاصهٔ صفحهٔ خانه از چهار منبع جدا: محموله‌های باز و آمادگی (<see cref="IHomeShipmentSource"/>)، انبار
/// (<see cref="IHomeStockSource"/>)، کارت پولی (<see cref="IHomeMoneySource"/>، فقط Business) و فعالیت‌های اخیر.
///
/// امن برای انباردار: منبع پولی برای او اصلاً صدا زده نمی‌شود (عضوهایش null می‌مانند) و رویدادهای پولی
/// (فروش، پرداخت، هزینه) از فعالیت‌های اخیر حذف می‌شوند. هر فاز منبع خودش را جایگزین می‌کند و این سرویس
/// ویرایش نمی‌شود.
/// </summary>
public sealed class HomeSummaryService(
    IHomeShipmentSource shipments,
    IHomeStockSource stock,
    IHomeMoneySource moneySource,
    IAuditEntryRepository activity,
    ITenantPreferencesRepository preferences,
    IMoneyVisibility money) : IHomeSummaryService
{
    /// <summary>تعداد ردیف «فعالیت‌های اخیر».</summary>
    public const int RecentCount = 8;

    public async Task<HomeSummaryDto> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var open = await shipments.ListOpenAsync(tenantId, cancellationToken);

        // کاشی‌ها اولین محمولهٔ باز را هدف می‌گیرند (تازه‌ترین فعالیت اول)؛ آمادگی هم مال همان است
        var target = open.Count > 0 ? open[0] : null;
        var readiness = target is null
            ? []
            : await shipments.ReadinessAsync(tenantId, target.Id, cancellationToken);

        var stockSummary = await stock.GetAsync(tenantId, cancellationToken);

        var homeMoney = money.CanSeeMoney
            ? await moneySource.GetAsync(tenantId, target?.Id, cancellationToken)
            : HomeMoney.Unknown;

        var excluded = money.CanSeeMoney ? [] : ActivityLogService.MoneyActions;
        var recentEntries = await activity.ListRecentAsync(tenantId, RecentCount, excluded, cancellationToken);
        var baleTerm = (await preferences.GetAsync(tenantId, cancellationToken))?.Preferences.BaleTerm ?? BaleTerm.Negleh;
        var terms = BaleTerms.For(baleTerm);

        return new HomeSummaryDto(
            open,
            readiness,
            stockSummary.Pieces,
            stockSummary.AreaM2,
            money.Money(homeMoney.OpenShipmentCostPerM2),
            money.CanSeeMoney ? homeMoney.UnallocatedEntries : null,
            recentEntries.Select(e => ActivityLogService.ToDto(e, terms)).ToList());
    }
}
