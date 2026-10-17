using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Support;

/// <summary>
/// دسترسی viewها به تنظیمات کارگاه جاری (واحد پول، لوگو، وضعیت اشتراک) و به اینکه بیننده مبلغ ببیند یا نه.
///
/// در طول هر درخواست فقط یک بار از دیتابیس می‌خواند و کش می‌کند؛ چون viewها ممکن است
/// ده‌ها بار واحد پول را بخواهند و هر بار یک کوئری، صفحه را کند می‌کند.
///
/// چرا دید پول هم این‌جاست: viewها مبلغ را با <see cref="Money"/> همین کلاس قالب می‌کنند و این کلاس در همهٔ
/// viewها با نام <c>Tenant</c> تزریق شده است. اگر view جایی شرط <see cref="CanSeeMoney"/> را جا انداخت، خودِ
/// <see cref="Money"/> برای انباردار «—» می‌نویسد؛ لایهٔ دوم پشت null کردن مبلغ‌ها در سرویس‌ها.
/// </summary>
public sealed class TenantSettingsAccessor(
    ITenantSettingsService settings,
    IHttpContextAccessor accessor,
    IMoneyVisibility moneyVisibility)
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

    /// <summary>
    /// واحد پول کارگاه: فقط تومان یا ریال. هر مقدار دیگری (مثلاً دلار که هرگز واحد کارگاه نیست) تومان خوانده
    /// می‌شود تا برچسب و کد همیشه یکی از این دو باشند.
    /// </summary>
    public CurrencyUnit CurrencyUnit => Current?.Currency == CurrencyUnit.Rial ? CurrencyUnit.Rial : CurrencyUnit.Toman;

    /// <summary>برچسب واحد پول: «تومان» یا «ریال».</summary>
    public string Currency => CurrencyUnit == CurrencyUnit.Rial ? "ریال" : "تومان";

    /// <summary>کد واحد برای app-context جزیره‌ها: «IRT» (تومان) یا «IRR» (ریال).</summary>
    public string CurrencyCode => CurrencyUnit == CurrencyUnit.Rial ? "IRR" : "IRT";

    /// <summary>
    /// آیا بینندهٔ این صفحه مبلغ تومانی می‌بیند (سیاست Business). viewها بخش‌های پولی را با همین پنهان می‌کنند.
    /// </summary>
    public bool CanSeeMoney => moneyVisibility.CanSeeMoney;

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

    /// <summary>
    /// مبلغ همراه واحد پول کارگاه — جایگزین متن ثابت «تومان» در viewها. برای بیننده‌ای که مبلغ نمی‌بیند همیشه «—».
    /// </summary>
    public string Money(decimal? value)
        => value.HasValue && CanSeeMoney ? $"{PersianFormat.Money(value)} {Currency}" : "—";
}
