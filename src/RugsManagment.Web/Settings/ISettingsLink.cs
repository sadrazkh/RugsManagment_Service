namespace RugsManagment.Web.Settings;

/// <summary>
/// یک کاشی در مرکز تنظیمات (<c>/Settings</c>، فقط Configure).
///
/// چرا: هر فاز صفحهٔ تنظیم خودش را دارد (ستون‌های جدول، دسته‌های هزینه، ارزش گمرکی، قالب اسناد)؛ با این
/// درز هر فاز یک کلاس لینک اضافه می‌کند و نه view مرکز تنظیمات را ویرایش می‌کند نه منوی تنظیمات را.
/// ثبت در DI؛ پیدا کردن خودکار همهٔ پیاده‌سازی‌های اسمبلی وب کار Program.cs است. همهٔ لینک‌ها پشت
/// Configure‌اند، پس شرط دیده‌شدن ندارند.
/// </summary>
public interface ISettingsLink
{
    /// <summary>شناسهٔ پایدار و یکتا (مثل «preferences»)</summary>
    string Key { get; }

    /// <summary>عنوان فارسی کاشی</summary>
    string Title { get; }

    /// <summary>یک جملهٔ کوتاه دربارهٔ اینکه این صفحه چه چیزی را تنظیم می‌کند</summary>
    string Description { get; }

    /// <summary>آدرس صفحه</summary>
    string Href { get; }

    /// <summary>نام نماد در <c>icons.svg</c></summary>
    string Icon { get; }

    /// <summary>گروه کاشی در مرکز تنظیمات</summary>
    SettingsGroup Group { get; }

    /// <summary>ترتیب درون گروه؛ کوچک‌تر جلوتر</summary>
    int Order { get; }
}

/// <summary>گروه‌های مرکز تنظیمات، به ترتیب نمایش.</summary>
public enum SettingsGroup
{
    /// <summary>کارگاه: مشخصات، ترجیحات، آماده‌سازی</summary>
    Workshop = 0,

    /// <summary>کاربران</summary>
    People = 1,

    /// <summary>فیلدها و فهرست‌ها: فیلدهای سفارشی، انواع مرحله، دسته‌های هزینه، ارزش گمرکی</summary>
    Catalog = 2,

    /// <summary>جدول‌ها و خروجی‌ها: ستون‌های جدول، قالب اسناد، برچسب‌ها</summary>
    Documents = 3,

    /// <summary>نگهداری: تاریخچهٔ فعالیت، سلامت حساب‌ها</summary>
    System = 4
}

/// <summary>برچسب فارسی گروه‌های مرکز تنظیمات.</summary>
public static class SettingsGroups
{
    public static string Label(SettingsGroup group) => group switch
    {
        SettingsGroup.Workshop => "کارگاه",
        SettingsGroup.People => "کاربران",
        SettingsGroup.Catalog => "فیلدها و فهرست‌ها",
        SettingsGroup.Documents => "جدول‌ها و خروجی‌ها",
        SettingsGroup.System => "نگهداری",
        _ => "سایر"
    };
}
