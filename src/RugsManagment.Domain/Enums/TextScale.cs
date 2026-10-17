namespace RugsManagment.Domain.Enums;

/// <summary>
/// اندازهٔ نوشتهٔ هر کاربر — اندازهٔ ریشهٔ صفحه ۱۶ / ۱۸ / ۲۰ پیکسل (<c>data-text-scale</c>).
///
/// چون پهنای ستون‌های جدول به <c>ch</c> است، با بزرگ شدن نوشته ستون‌ها هم بزرگ می‌شوند و چیزی
/// بریده نمی‌شود. پیش‌فرض بازرگان «بزرگ» است.
/// </summary>
public enum TextScale
{
    /// <summary>معمولی (۱۶ پیکسل)</summary>
    Normal = 0,

    /// <summary>بزرگ (۱۸ پیکسل)</summary>
    Large = 1,

    /// <summary>خیلی بزرگ (۲۰ پیکسل)</summary>
    XLarge = 2
}
