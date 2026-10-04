using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Common;

/// <summary>
/// برچسب فارسی نقش‌ها — تنها منبع. پیش از این در سرویس کاربران، کنترلر حساب و دو view جدا نوشته شده
/// بود و با افزودن «بازرگان» از هم جدا می‌شدند. لایهٔ وب (<c>Web/Support/RoleLabels</c>) فقط به این
/// کلاس ارجاع می‌دهد.
/// </summary>
public static class RoleLabels
{
    /// <summary>نقش‌هایی که مدیر کارگاه می‌تواند به کاربر کارگاه بدهد، به ترتیب نمایش در فرم.</summary>
    public static IReadOnlyList<UserRole> TenantRoles { get; } =
        [UserRole.Merchant, UserRole.Operator, UserRole.TenantAdmin];

    private const string Unknown = "نقش ناشناخته";

    /// <summary>برچسب فارسی یک نقش.</summary>
    public static string For(UserRole role) => role switch
    {
        UserRole.SystemAdmin => "مدیر سامانه",
        UserRole.TenantAdmin => "تنظیم‌کننده (مدیر)",
        UserRole.Merchant => "بازرگان",
        UserRole.Operator => "کاربر / انباردار",
        _ => Unknown
    };

    /// <summary>
    /// برچسب از نام نقش (همان متنی که در claim نقش و <c>UserDto.Role</c> نشسته، مثل «Merchant»).
    /// نام ناشناخته همان «نقش ناشناخته» می‌شود، نه خطا، تا یک کوکی کهنه صفحه را نشکند.
    /// </summary>
    public static string For(string? roleName)
        => Enum.TryParse<UserRole>(roleName, ignoreCase: false, out var role) && Enum.IsDefined(role)
            ? For(role)
            : Unknown;

    /// <summary>آیا این نقش مال کاربر کارگاه است (نه مدیر سامانه).</summary>
    public static bool IsTenantRole(UserRole role) => TenantRoles.Contains(role);
}
