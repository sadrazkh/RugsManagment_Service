using System.Security.Claims;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Support;

/// <summary>
/// پیاده‌سازی <see cref="IUserPreferencesAccessor"/>: ترجیحات کاربر جاری را یک بار در هر درخواست از
/// <see cref="IUserPreferencesService"/> می‌خواند و برای layout، viewها و منو نگه می‌دارد.
///
/// چرا همگام: layout و viewها خاصیت می‌خوانند و Razor جای await کردن در هر خاصیت ندارد؛ مثل
/// <see cref="TenantSettingsAccessor"/> نتیجه کش می‌شود، پس فقط یک کوئری در کل صفحه است. ASP.NET Core
/// SynchronizationContext ندارد، پس GetResult بن‌بست نمی‌سازد.
///
/// پیش‌نمایش «نمای ساده» فقط از کوکی تنظیم‌کننده خوانده می‌شود؛ کوکیِ همین نام در مرورگر بازرگان یا انباردار
/// بی‌اثر است (آن‌ها خودشان ساده‌اند) و هیچ دسترسی‌ای را عوض نمی‌کند.
/// </summary>
public sealed class UserPreferencesAccessor(
    IUserPreferencesService preferences,
    IHttpContextAccessor accessor,
    ILogger<UserPreferencesAccessor> logger) : IUserPreferencesAccessor
{
    private UserPreferencesDto? _cached;
    private bool _loaded;

    public UserPreferencesDto? Current
    {
        get
        {
            if (_loaded) return _cached;
            _loaded = true;

            // مدیر سامانه و کاربر واردنشده ترجیحات کارگاهی ندارند
            var user = accessor.HttpContext?.User;
            if (user is null || !Policies.IsFloor(user)) return _cached = null;
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return _cached = null;

            try
            {
                _cached = preferences.GetAsync(userId, accessor.HttpContext!.RequestAborted).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // درخواست رها شد؛ چیزی برای لاگ نیست
                _cached = null;
            }
            catch (Exception ex)
            {
                // نبود ترجیحات (کاربر حذف‌شده، دیتابیس در دسترس نیست) نباید هیچ صفحه‌ای را بشکند؛ پوسته با پیش‌فرض‌ها می‌آید
                logger.LogWarning(ex, "خواندن ترجیحات کاربر {UserId} ناموفق بود؛ پیش‌فرض‌ها به کار رفت.", userId);
                _cached = null;
            }

            return _cached;
        }
    }

    public UiMode UiMode => Current?.UiMode ?? UiMode.Full;

    public bool PreviewSimple
    {
        get
        {
            var http = accessor.HttpContext;
            return http is not null
                   && Policies.IsConfigure(http.User)
                   && string.Equals(http.Request.Cookies[UiPreview.CookieName], UiPreview.OnValue, StringComparison.Ordinal);
        }
    }

    public bool IsSimple => UiMode == UiMode.Simple || PreviewSimple;

    public TextScale TextScale => Current?.TextScale ?? TextScale.Normal;

    public GridEnterDirection GridEnter => Current?.GridEnter ?? GridEnterDirection.Down;
}
