using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Intake.Grid;

/// <summary>
/// بینندهٔ جدول برای درخواست جاری: کارگاه، کاربر، دیدن پول (سیاست Business) و واژه‌های نگله/عدل کارگاه — همان چیزی که
/// ستون‌های پیش‌فرض و ردیف‌ها با آن ساخته می‌شوند.
/// </summary>
public sealed class GridViewerFactory(
    ICurrentUser currentUser,
    IMoneyVisibility money,
    ITenantPreferencesRepository preferences)
{
    public async Task<GridViewer> CreateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var prefs = (await preferences.GetAsync(tenantId, cancellationToken))?.Preferences
                    ?? TenantPreferences.CreateDefault(tenantId);
        return new GridViewer(tenantId, currentUser.UserId, money.CanSeeMoney, BaleTerms.For(prefs.BaleTerm));
    }
}
