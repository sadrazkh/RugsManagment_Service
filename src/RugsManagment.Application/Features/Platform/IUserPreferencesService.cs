using RugsManagment.Application.Features.Platform.DTOs;

namespace RugsManagment.Application.Features.Platform;

/// <summary>
/// ترجیحات خودِ کاربر (<c>/api/me</c>، <c>/api/me/preferences</c>، پوستهٔ هر صفحه).
///
/// همیشه با شناسهٔ کاربرِ همان درخواست (از کوکی) صدا زده می‌شود، نه شناسه‌ای از بدنه یا آدرس؛ پس کسی
/// نمی‌تواند ترجیحات دیگری را بخواند یا عوض کند.
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>ترجیحات کاربر؛ کاربر ناموجود → <c>KeyNotFoundException</c> (۴۰۴).</summary>
    Task<UserPreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// عضوهای پرشدهٔ درخواست را می‌نویسد (null = دست نزن؛ UiMode فقط برای تنظیم‌کننده — بقیه نادیده) و
    /// ترجیحات تازه را برمی‌گرداند. مقدار عددیِ بیرون از enum → ۴۰۰ (<c>AppValidationException</c>).
    /// </summary>
    Task<UserPreferencesDto> UpdateAsync(
        Guid userId, UpdateMyPreferencesRequest request, CancellationToken cancellationToken = default);

    /// <summary>کاربر جاری با برچسب نقش و ترجیحاتش.</summary>
    Task<MeDto> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);
}
