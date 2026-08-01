using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Support;

/// <summary>
/// دسترسی viewها به تنظیمات کارگاه جاری (واحد پول، لوگو، وضعیت اشتراک).
///
/// در طول هر درخواست فقط یک بار از دیتابیس می‌خواند و کش می‌کند؛ چون viewها ممکن است
/// ده‌ها بار واحد پول را بخواهند و هر بار یک کوئری، صفحه را کند می‌کند.
/// </summary>
public sealed class TenantSettingsAccessor(
    ITenantSettingsService settings,
    IHttpContextAccessor accessor)
{
    private TenantSettingsDto? _cached;
    private bool _loaded;

    /// <summary>تنظیمات کارگاه؛ null برای ادمین سیستم یا کاربر واردنشده.</summary>
    public TenantSettingsDto? Current
    {
        get
        {
            if (_loaded) return _cached;
            _loaded = true;

            var tenantId = accessor.HttpContext?.User.GetTenantId();
            if (tenantId is null) return _cached = null;

            try
            {
                // فراخوانی همگام در view اجتناب‌ناپذیر است؛ نتیجه کش می‌شود
                _cached = settings.GetAsync(tenantId.Value).GetAwaiter().GetResult();
            }
            catch
            {
                // نبود تنظیمات نباید کل صفحه را بشکند
                _cached = null;
            }

            return _cached;
        }
    }

    /// <summary>برچسب واحد پول: «تومان» یا «ریال».</summary>
    public string Currency => Current?.Currency == CurrencyUnit.Rial ? "ریال" : "تومان";

    public string? LogoUrl => Current?.LogoUrl;

    /// <summary>
    /// اگر اشتراک نزدیک پایان است، چند روز مانده — برای نوار هشدار.
    /// null یعنی هشداری لازم نیست.
    /// </summary>
    public int? ExpiryWarningDays
    {
        get
        {
            var days = Current?.DaysUntilExpiry;
            return days is >= 0 and <= 14 ? days : null;
        }
    }

    /// <summary>مبلغ همراه واحد پول کارگاه — جایگزین متن ثابت «تومان» در viewها.</summary>
    public string Money(decimal? value)
        => value.HasValue ? $"{PersianFormat.Money(value)} {Currency}" : "—";
}
