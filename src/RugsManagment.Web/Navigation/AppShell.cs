using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Support;

namespace RugsManagment.Web.Navigation;

/// <summary>
/// بدنهٔ <c>&lt;script type="application/json" id="app-context"&gt;</c> در layout — همان شکلی که
/// <c>ClientApp/src/lib/context.ts</c> مستند کرده (کلیدها camelCase، همه همیشه حاضر). enumها به نام متنی
/// («simple»، «large»، «down») نوشته می‌شوند، نه عدد، چون جزیره‌ها آن‌ها را مستقیم در <c>data-*</c> و CSS
/// به کار می‌برند.
/// </summary>
public sealed record AppContextPayload(
    string? TenantId,
    string? UserId,
    string? Role,
    AppPoliciesPayload Policies,
    bool MoneyVisible,
    string UiMode,
    bool PreviewSimple,
    string TextScale,
    string GridEnter,
    AppCurrencyPayload Currency,
    int MoneyScale,
    AppTermsPayload Terms,
    string Today,
    string TodayJalali,
    IReadOnlyList<AppCommandPayload> Commands);

/// <summary>سیاست‌های بیننده (فقط برای پنهان کردن کنترل‌ها؛ دسترسی واقعی همیشه روی سرور سنجیده می‌شود).</summary>
public sealed record AppPoliciesPayload(bool Floor, bool Business, bool Configure);

/// <summary>واحد پول کارگاه: <c>code</c> «IRT» (تومان) یا «IRR» (ریال)، برچسب فارسی و تعداد رقم اعشار.</summary>
public sealed record AppCurrencyPayload(string Code, string Label, int Scale);

/// <summary>واژه‌های نگله/عدل کارگاه.</summary>
public sealed record AppTermsPayload(string Bale, string Bales, string BaleOf, string BaleFooter);

/// <summary>یک فرمان Ctrl+K.</summary>
public sealed record AppCommandPayload(string Label, string Href, string Icon);

/// <summary>
/// داده‌های مشترک پوسته در طول یک درخواست: ترجیحات کارگاه (یک کوئری برای layout، کارت‌های خانه و کارت
/// راهنما)، «امروز» به وقت ایران و ساختن app-context.
/// </summary>
public static class AppShell
{
    private const string TenantPreferencesKey = "rugs.tenant-preferences";

    private static readonly TimeZoneInfo IranTimeZone = ResolveIranTimeZone();
    private static readonly PersianCalendar Jalali = new();

    private sealed record ControllerSet(int Version, HashSet<string> Names);

    private static ControllerSet? _controllers;

    /// <summary>
    /// آیا کنترلری با این نام (بدون پسوند Controller) در برنامه هست — برای مقصدهایی که فاز بعد می‌سازد
    /// (ورود فرش، محموله‌ها، هزینه‌ها، ورود از اکسل): تا نیامده‌اند به جایگزین موجود اشاره می‌کنیم و روزی که
    /// آمدند بی‌ویرایش همین کد به آن‌ها. از فهرست اکشن‌های MVC خوانده می‌شود، نه از تولید پیوند (مسیر
    /// قراردادی «{controller}/{action}» برای کنترلرِ ناموجود هم پیوند می‌سازد).
    /// </summary>
    public static bool HasController(IActionDescriptorCollectionProvider actions, string controller)
    {
        var collection = actions.ActionDescriptors;
        var cached = _controllers;
        if (cached is null || cached.Version != collection.Version)
        {
            cached = new ControllerSet(
                collection.Version,
                collection.Items
                    .OfType<ControllerActionDescriptor>()
                    .Select(a => a.ControllerName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
            _controllers = cached;
        }

        return cached.Names.Contains(controller);
    }

    /// <summary>
    /// ترجیحات کارگاه کاربر جاری (یک بار در هر درخواست)؛ null برای کاربر بی‌کارگاه یا اگر خواندن خطا داد —
    /// چون layout هر صفحه این را می‌خواند و نبودِ ترجیحات نباید هیچ صفحه‌ای را بشکند.
    /// </summary>
    public static async Task<TenantPreferencesDto?> TenantPreferencesAsync(HttpContext http)
    {
        if (http.Items.TryGetValue(TenantPreferencesKey, out var cached))
            return cached as TenantPreferencesDto;

        TenantPreferencesDto? result = null;
        if (http.User.GetTenantId() is Guid tenantId)
        {
            try
            {
                var service = http.RequestServices.GetRequiredService<ITenantPreferencesService>();
                result = await service.GetAsync(tenantId, http.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                http.RequestServices.GetService<ILoggerFactory>()?
                    .CreateLogger(typeof(AppShell))
                    .LogWarning(ex, "ترجیحات کارگاه {TenantId} خوانده نشد؛ پیش‌فرض‌ها به کار می‌روند.", tenantId);
            }
        }

        http.Items[TenantPreferencesKey] = result;
        return result;
    }

    /// <summary>واژه‌های نگله/عدل کارگاه جاری (پیش‌فرض «نگله»).</summary>
    public static async Task<BaleTermWords> TermsAsync(HttpContext http)
        => BaleTerms.For((await TenantPreferencesAsync(http))?.BaleTerm ?? BaleTerm.Negleh);

    /// <summary>امروز (تاریخ میلادی) به وقت ایران — نه تاریخ UTC سرور که از ۰۰:۰۰ تا ۰۳:۳۰ یک روز عقب است.</summary>
    public static DateOnly IranToday(DateTimeOffset? now = null)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now ?? DateTimeOffset.UtcNow, IranTimeZone).DateTime);

    /// <summary>تاریخ شمسی با ارقام لاتین: «1405/06/15» (نمایش، ارقام را فارسی می‌کند).</summary>
    public static string JalaliLatin(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        return string.Create(CultureInfo.InvariantCulture,
            $"{Jalali.GetYear(dt):0000}/{Jalali.GetMonth(dt):00}/{Jalali.GetDayOfMonth(dt):00}");
    }

    /// <summary>تاریخ شمسی با ارقام فارسی: «۱۴۰۵/۰۶/۱۵».</summary>
    public static string JalaliPersian(DateOnly date) => PersianFormat.ToPersianDigits(JalaliLatin(date));

    /// <summary>نام متنی حالت پوسته برای <c>data-ui</c> و app-context.</summary>
    public static string UiModeName(UiMode mode) => mode == Domain.Enums.UiMode.Simple ? "simple" : "full";

    /// <summary>نام متنی اندازهٔ نوشته برای <c>data-text-scale</c> و app-context.</summary>
    public static string TextScaleName(TextScale scale) => scale switch
    {
        Domain.Enums.TextScale.Large => "large",
        Domain.Enums.TextScale.XLarge => "xlarge",
        _ => "normal"
    };

    /// <summary>نام متنی جهت Enter جدول‌ها.</summary>
    public static string GridEnterName(GridEnterDirection direction)
        => direction == GridEnterDirection.Right ? "right" : "down";

    /// <summary>
    /// app-context این درخواست. واحد پول از تنظیمات کارگاه (تومان/ریال)، تعداد رقم اعشار و واژهٔ نگله از
    /// ترجیحات کارگاه، فرمان‌ها از آیتم‌های <see cref="NavSlot.Command"/> منو.
    /// </summary>
    public static async Task<AppContextPayload> BuildPayloadAsync(
        HttpContext http,
        NavModel? nav,
        IUserPreferencesAccessor preferences,
        CurrencyUnit? currency)
    {
        var user = http.User;
        var authed = user.Identity?.IsAuthenticated == true;
        var tenantPreferences = authed ? await TenantPreferencesAsync(http) : null;
        var terms = BaleTerms.For(tenantPreferences?.BaleTerm ?? BaleTerm.Negleh);
        var moneyScale = Math.Clamp((int)(tenantPreferences?.MoneyScale ?? 0), 0, 2);
        var isRial = currency == CurrencyUnit.Rial;
        var today = IranToday();
        var business = authed && Policies.IsBusiness(user);

        return new AppContextPayload(
            TenantId: authed ? user.GetTenantId()?.ToString() : null,
            UserId: authed ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            Role: authed ? RoleName(user) : null,
            Policies: new AppPoliciesPayload(
                Floor: authed && Policies.IsFloor(user),
                Business: business,
                Configure: authed && Policies.IsConfigure(user)),
            MoneyVisible: business,
            UiMode: authed ? UiModeName(preferences.UiMode) : "full",
            PreviewSimple: authed && preferences.PreviewSimple,
            TextScale: authed ? TextScaleName(preferences.TextScale) : "normal",
            GridEnter: authed ? GridEnterName(preferences.GridEnter) : "down",
            Currency: new AppCurrencyPayload(isRial ? "IRR" : "IRT", isRial ? "ریال" : "تومان", moneyScale),
            MoneyScale: moneyScale,
            Terms: new AppTermsPayload(terms.Bale, terms.Bales, terms.BaleOf, terms.BaleFooter),
            Today: today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TodayJalali: JalaliLatin(today),
            Commands: nav?.Commands
                .Select(c => new AppCommandPayload(c.Label, NavBuilder.Resolve(http, c.Href), c.Icon))
                .ToList() ?? []);
    }

    private static string? RoleName(ClaimsPrincipal user)
        => Enum.TryParse<UserRole>(user.FindFirstValue(ClaimTypes.Role), out var role) && Enum.IsDefined(role)
            ? role.ToString()
            : null;

    /// <summary>شناسهٔ منطقهٔ زمانی روی ویندوز و لینوکس فرق دارد؛ اگر هیچ‌کدام نبود UTC+3:30 ثابت.</summary>
    private static TimeZoneInfo ResolveIranTimeZone()
    {
        foreach (var id in new[] { "Iran Standard Time", "Asia/Tehran" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Iran-Fallback", TimeSpan.FromMinutes(210), "Iran", "Iran");
    }
}
