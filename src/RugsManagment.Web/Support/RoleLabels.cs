using RugsManagment.Domain.Enums;
using AppRoleLabels = RugsManagment.Application.Common.RoleLabels;

namespace RugsManagment.Web.Support;

/// <summary>
/// نقش‌ها در صفحه‌ها: برچسب (از <see cref="AppRoleLabels"/>، تنها منبع)، رنگ چیپ و یک جملهٔ توضیح برای فرم
/// کاربر. متن برچسب این‌جا تکرار نمی‌شود تا «بازرگان» یا «تنظیم‌کننده» فقط یک جا نوشته شده باشد.
///
/// کلاس‌های Tailwind این فایل در <c>Web/Support/**</c> هستند که منبع Tailwind است.
/// </summary>
public static class RoleLabels
{
    /// <summary>نقش‌هایی که در فرم کاربر کارگاه پیشنهاد می‌شوند، به ترتیب نمایش.</summary>
    public static IReadOnlyList<UserRole> TenantRoles => AppRoleLabels.TenantRoles;

    /// <summary>برچسب فارسی نقش.</summary>
    public static string For(UserRole role) => AppRoleLabels.For(role);

    /// <summary>برچسب فارسی از نام نقش (claim یا <c>UserDto.Role</c>).</summary>
    public static string For(string? roleName) => AppRoleLabels.For(roleName);

    /// <summary>کلاس‌های چیپ نقش (سازگار با حالت تاریک).</summary>
    public static string ChipCss(UserRole role) => role switch
    {
        UserRole.TenantAdmin => "bg-primary/12 text-primary",
        UserRole.Merchant => "bg-secondary-container text-on-secondary-container",
        UserRole.Operator => "bg-surface-container-high text-on-surface-variant",
        _ => "bg-surface-container-high text-on-surface-variant"
    };

    /// <summary>کلاس‌های چیپ از نام نقش.</summary>
    public static string ChipCss(string? roleName)
        => Enum.TryParse<UserRole>(roleName, ignoreCase: false, out var role) && Enum.IsDefined(role)
            ? ChipCss(role)
            : ChipCss(UserRole.Operator);

    /// <summary>
    /// این نقش چه می‌کند — یک جمله زیر انتخاب نقش، تا تنظیم‌کننده بداند به پدر کدام را بدهد.
    /// </summary>
    public static string Hint(UserRole role) => role switch
    {
        UserRole.Merchant => "همهٔ مبلغ‌ها، محموله‌ها و فروش را می‌بیند و ثبت می‌کند؛ تنظیمات را نه. نمای ساده با نوشتهٔ بزرگ.",
        UserRole.Operator => "ورود فرش، نگله‌بندی و چاپ برچسب؛ هیچ مبلغی نمی‌بیند. نمای ساده.",
        UserRole.TenantAdmin => "همه‌چیز، به‌اضافهٔ تنظیمات کارگاه و کاربران. نمای کامل.",
        _ => string.Empty
    };
}
