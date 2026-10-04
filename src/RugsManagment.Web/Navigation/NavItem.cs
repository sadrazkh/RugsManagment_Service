using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Navigation;

/// <summary>یک آیتم کجا دیده شود — ترکیب‌پذیر.</summary>
[Flags]
public enum NavSlot
{
    None = 0,

    /// <summary>نوار کناری (دسکتاپ) و کشوی منو (گوشی)</summary>
    Sidebar = 1,

    /// <summary>نوار پایین گوشی — صریح، نه «پنج تای اول»</summary>
    Bottom = 2,

    /// <summary>فهرست صفحهٔ «بیشتر» (<c>/More</c>)</summary>
    More = 4,

    /// <summary>فرمان‌های جعبهٔ جست‌وجوی Ctrl+K (<c>commands</c> در app-context)</summary>
    Command = 8
}

/// <summary>
/// یک آیتم منو. هر <see cref="INavContributor"/> برای بینندهٔ جاری فقط آیتم‌هایی را برمی‌گرداند که او
/// اجازهٔ باز کردنشان را دارد، پس منو هیچ‌وقت دری نشان نمی‌دهد که پشتش ۴۰۳ است.
/// </summary>
/// <param name="Key">شناسهٔ پایدار و یکتا (مثل «home»، «rugs»)؛ تکراری‌ها را NavBuilder یکی می‌کند (کمترین Order می‌ماند)</param>
/// <param name="Label">برچسب فارسی، با واژه‌های فرهنگ بازرگان</param>
/// <param name="Href">آدرس</param>
/// <param name="Icon">نام نماد در <c>icons.svg</c></param>
/// <param name="Order">ترتیب؛ از <see cref="NavOrder"/> استفاده کنید تا آیتم‌های فازهای بعد سر جای درست بنشینند</param>
/// <param name="Slots">کجاها دیده شود</param>
/// <param name="Hint">توضیح یک‌خطی زیر برچسب در صفحهٔ «بیشتر»</param>
/// <param name="ActivePrefix">پیشوند مسیری که آیتم را «فعال» نشان می‌دهد (بی‌حساسیت به بزرگی حروف)؛ null یعنی خودِ <paramref name="Href"/></param>
/// <param name="Group">سرتیتر گروه در نوار کناری حالت کامل؛ null یعنی بی‌گروه</param>
public sealed record NavItem(
    string Key,
    string Label,
    string Href,
    string Icon,
    int Order,
    NavSlot Slots = NavSlot.Sidebar,
    string? Hint = null,
    string? ActivePrefix = null,
    string? Group = null);

/// <summary>
/// بینندهٔ منو. همه‌چیز از پیش حساب شده (سیاست‌ها، حالت مؤثر) تا contributorها ساده و قابل‌آزمون بمانند.
/// </summary>
/// <param name="Role">نقش کاربر</param>
/// <param name="CanFloor">سیاست Floor</param>
/// <param name="CanBusiness">سیاست Business</param>
/// <param name="CanConfigure">سیاست Configure</param>
/// <param name="IsSimple">حالت مؤثرِ پوسته: حالت ساده، یا پیش‌نمایش «نمای ساده»ی تنظیم‌کننده</param>
/// <param name="PreviewSimple">آیا ساده بودن فقط از پیش‌نمایش تنظیم‌کننده است (دسترسی همان کامل می‌ماند)</param>
/// <param name="CurrentPath">مسیر درخواست جاری (برای آیتم فعال)</param>
public sealed record NavContext(
    UserRole Role,
    bool CanFloor,
    bool CanBusiness,
    bool CanConfigure,
    bool IsSimple,
    bool PreviewSimple,
    string CurrentPath);

/// <summary>
/// ترتیب آیتم‌های منو مطابق جدول ناوبری (A.4) — هر فاز آیتم خودش را با همین عددها اضافه می‌کند.
/// فاصلهٔ ده‌تایی برای آیتم‌هایی است که بعداً میان این‌ها می‌آیند.
/// </summary>
public static class NavOrder
{
    public const int Home = 10;
    public const int Intake = 20;
    public const int Rugs = 30;
    public const int Shipments = 40;
    public const int Costs = 50;
    public const int Import = 60;
    public const int Reports = 70;
    public const int Sales = 80;
    public const int Providers = 90;
    public const int Workflows = 100;
    public const int Batches = 110;
    public const int Labels = 120;
    public const int Dashboard = 130;
    public const int Settings = 900;
    public const int Help = 950;
    public const int Profile = 960;
    public const int More = 1000;
}
