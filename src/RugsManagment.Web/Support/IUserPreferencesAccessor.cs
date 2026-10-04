using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Support;

/// <summary>
/// ترجیحات کاربر جاری برای layout و viewها (<c>data-ui</c>، <c>data-text-scale</c>، app-context، منو).
///
/// مثل <c>TenantSettingsAccessor</c> در طول هر درخواست فقط یک بار از سرویس می‌خواند و کش می‌کند؛ برای
/// کاربر واردنشده یا مدیر سامانه و اگر خواندن خطا داد، پیش‌فرض‌های بی‌خطر برمی‌گرداند و صفحه را نمی‌شکند.
/// ثبت scoped در Program.cs.
/// </summary>
public interface IUserPreferencesAccessor
{
    /// <summary>ترجیحات ذخیره‌شدهٔ کاربر؛ null برای کاربر واردنشده، مدیر سامانه یا خطای خواندن.</summary>
    UserPreferencesDto? Current { get; }

    /// <summary>حالت ذخیره‌شده؛ بدون ترجیحات «کامل».</summary>
    UiMode UiMode { get; }

    /// <summary>
    /// پیش‌نمایش «نمای ساده»ی تنظیم‌کننده (کوکی <see cref="UiPreview.CookieName"/>). برای هر نقش دیگر
    /// همیشه false. فقط پوسته را ساده می‌کند؛ دسترسی همان می‌ماند.
    /// </summary>
    bool PreviewSimple { get; }

    /// <summary>حالت مؤثر پوسته: <c>UiMode == Simple || PreviewSimple</c>.</summary>
    bool IsSimple { get; }

    /// <summary>اندازهٔ نوشته؛ بدون ترجیحات «معمولی».</summary>
    TextScale TextScale { get; }

    /// <summary>جهت Enter در جدول‌ها؛ بدون ترجیحات «پایین».</summary>
    GridEnterDirection GridEnter { get; }
}

/// <summary>پیش‌نمایش «نمای ساده»ی تنظیم‌کننده.</summary>
public static class UiPreview
{
    /// <summary>
    /// نام کوکی پیش‌نمایش؛ مقدار «1» یعنی روشن. فقط برای تنظیم‌کننده خوانده می‌شود و هیچ دسترسی‌ای را
    /// عوض نمی‌کند، پس امضا یا HttpOnly لازم ندارد.
    /// </summary>
    public const string CookieName = "rugs.preview-simple";

    /// <summary>مقدار روشن.</summary>
    public const string OnValue = "1";
}
