namespace RugsManagment.Domain.Enums;

/// <summary>
/// نقش کاربر در سیستم — تعیین می‌کند چه منو و APIهایی در دسترس است.
/// </summary>
public enum UserRole
{
    /// <summary>مدیر کل سامانه؛ کارگاه می‌سازد، به Tenant وابسته نیست</summary>
    SystemAdmin = 0,

    /// <summary>مدیر یک کارگاه؛ قالب فرایند، فرش و کاربران همان کارگاه</summary>
    TenantAdmin = 1,

    /// <summary>اپراتور کارگاه؛ معمولاً ثبت و پیش بردن مراحل فرش</summary>
    Operator = 2
}
