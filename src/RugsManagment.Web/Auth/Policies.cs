using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Auth;

/// <summary>
/// سه سیاست دسترسی کارگاه — جای <c>[Authorize(Roles = …)]</c>های پراکنده.
///
/// <list type="table">
/// <item><term><see cref="Floor"/></term><description>تنظیم‌کننده، بازرگان، انباردار: ورود فرش، انبار، محموله و نگله، اندازهٔ نهایی، خروجی بدون مبلغ، برچسب، راهنما.</description></item>
/// <item><term><see cref="Business"/></term><description>تنظیم‌کننده، بازرگان: هر مبلغ تومانی، فروش، طرف‌های خدمات، ساخت و قطعی‌کردن محموله، ورود از اکسل، سطل زباله، گزارش‌ها.</description></item>
/// <item><term><see cref="Configure"/></term><description>فقط تنظیم‌کننده: تنظیمات، کاربران، فیلدها، قالب‌ها، ترجیحات.</description></item>
/// </list>
///
/// چرا سیاست و نه نقش: وقتی «بازرگان» اضافه شد، هر <c>Roles = "TenantAdmin,Operator"</c> باید جداگانه
/// بازبینی می‌شد و یکی جا می‌ماند. حالا هر درِ برنامه یکی از سه نام را دارد و نقش‌های هر نام فقط این‌جاست.
/// مدیر سامانه در هیچ‌کدام نیست (او فقط <c>/Admin</c> دارد). نقش از claim کوکی می‌آید و تغییرش با مهر امنیتی
/// حداکثر تا ۵ دقیقه به همهٔ نشست‌ها می‌رسد.
/// </summary>
public static class Policies
{
    /// <summary>کار روزانهٔ انبار — هر کاربر کارگاه.</summary>
    public const string Floor = "Floor";

    /// <summary>پول و تصمیم‌های تجاری — بازرگان و تنظیم‌کننده.</summary>
    public const string Business = "Business";

    /// <summary>پیکربندی کارگاه — فقط تنظیم‌کننده.</summary>
    public const string Configure = "Configure";

    /// <summary>نقش‌های <see cref="Floor"/>.</summary>
    public static IReadOnlyList<UserRole> FloorRoles { get; } =
        [UserRole.TenantAdmin, UserRole.Merchant, UserRole.Operator];

    /// <summary>نقش‌های <see cref="Business"/>.</summary>
    public static IReadOnlyList<UserRole> BusinessRoles { get; } =
        [UserRole.TenantAdmin, UserRole.Merchant];

    /// <summary>نقش‌های <see cref="Configure"/>.</summary>
    public static IReadOnlyList<UserRole> ConfigureRoles { get; } =
        [UserRole.TenantAdmin];

    /// <summary>
    /// ثبت سه سیاست. در Program.cs: <c>builder.Services.AddAuthorization(o =&gt; o.AddRugsPolicies());</c>
    /// </summary>
    public static AuthorizationOptions AddRugsPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Floor, policy => policy.RequireAuthenticatedUser().RequireRole(Names(FloorRoles)));
        options.AddPolicy(Business, policy => policy.RequireAuthenticatedUser().RequireRole(Names(BusinessRoles)));
        options.AddPolicy(Configure, policy => policy.RequireAuthenticatedUser().RequireRole(Names(ConfigureRoles)));
        return options;
    }

    /// <summary>آیا کاربر سیاست <see cref="Floor"/> را دارد (برای view و منو، بدون IAuthorizationService).</summary>
    public static bool IsFloor(ClaimsPrincipal? user) => HasAnyRole(user, FloorRoles);

    /// <summary>آیا کاربر سیاست <see cref="Business"/> را دارد — یعنی مبلغ‌ها را می‌بیند.</summary>
    public static bool IsBusiness(ClaimsPrincipal? user) => HasAnyRole(user, BusinessRoles);

    /// <summary>آیا کاربر سیاست <see cref="Configure"/> را دارد.</summary>
    public static bool IsConfigure(ClaimsPrincipal? user) => HasAnyRole(user, ConfigureRoles);

    private static string[] Names(IReadOnlyList<UserRole> roles) => roles.Select(r => r.ToString()).ToArray();

    private static bool HasAnyRole(ClaimsPrincipal? user, IReadOnlyList<UserRole> roles)
        => user?.Identity?.IsAuthenticated == true && roles.Any(r => user.IsInRole(r.ToString()));
}

/// <summary>
/// نام سیاست‌های محدودیت نرخ (<c>[EnableRateLimiting(RateLimitPolicies.Heavy)]</c>). خودِ سیاست‌ها در Program.cs
/// ثبت شده‌اند و Program.cs بعد از این فاز دست نمی‌خورد؛ فازهای بعد فقط همین نام‌ها را به کار می‌برند.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>صفحهٔ ورود: ۱۰ تلاش در ۵ دقیقه از هر آی‌پی — دفاع در برابر حدس رمز.</summary>
    public const string Login = "login";

    /// <summary>
    /// کارهای سنگین (ساخت اکسل، صفحهٔ چاپ، ورود از اکسل): برای هر کاربر ۱۰ درخواست پشت سر هم و بعد
    /// ۱۰ تا هر ۳۰ ثانیه. جلوی کلیک‌های پیاپی را می‌گیرد که سرور را برای بقیه کند می‌کرد.
    /// </summary>
    public const string Heavy = "heavy";
}
