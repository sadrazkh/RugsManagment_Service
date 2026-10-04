namespace RugsManagment.Domain.Enums;

/// <summary>
/// روش تقسیم یک هزینهٔ «کل» بین فرش‌ها.
///
/// در فاز سکو فقط به‌عنوان پیش‌فرض کارگاه (<c>TenantPreferences.DefaultMethod</c>) ذخیره می‌شود؛ خودِ
/// تقسیم‌کننده در فاز هزینه‌ها می‌آید. فقط افزودنی با عدد صریح.
/// </summary>
public enum AllocationMethod
{
    /// <summary>به نسبت متراژ</summary>
    ByArea = 0,

    /// <summary>به تعداد تخته</summary>
    ByCount = 1,

    /// <summary>مساوی برای هر ردیف (یک دسته یک بار حساب می‌شود)</summary>
    Equal = 2,

    /// <summary>به نسبت ارزش گمرکی</summary>
    ByDeclaredValue = 3,

    /// <summary>به نسبت قیمت خرید (فقط برای هزینه‌های غیرخرید)</summary>
    ByPurchaseCost = 4,

    /// <summary>دستی: سهم هر فرش را خود کاربر می‌نویسد</summary>
    Manual = 5
}
