namespace RugsManagment.Web.Settings;

/// <summary>
/// پایهٔ یک کاشی ثابت مرکز تنظیمات: همهٔ مقدارها در سازنده. فازهای بعد هم برای کاشی خودشان از همین ارث
/// می‌برند (یک کلاس یک‌خطی در فایل خودشان)؛ Program.cs هر کلاس غیرانتزاعیِ <see cref="ISettingsLink"/> را خودکار
/// ثبت می‌کند.
/// </summary>
public abstract class StaticSettingsLink(
    string key, string title, string description, string href, string icon, SettingsGroup group, int order)
    : ISettingsLink
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public string Description { get; } = description;
    public string Href { get; } = href;
    public string Icon { get; } = icon;
    public SettingsGroup Group { get; } = group;
    public int Order { get; } = order;
}

// ── کاشی‌های پایه (صفحه‌هایی که پیش از این فاز هم بودند، به‌اضافهٔ «ترجیحات») ─────────────────
// متن‌ها برای تنظیم‌کننده است (نمای کامل)، ولی همچنان فارسی ساده و بدون اصطلاح فنی.

/// <summary>مشخصات کارگاه: نام، تماس، لوگو، واحد پول.</summary>
public sealed class TenantProfileSettingsLink() : StaticSettingsLink(
    "tenant", "مشخصات کارگاه", "نام، تلفن، لوگو و واحد پول (تومان یا ریال).",
    "/Settings/Tenant", "building", SettingsGroup.Workshop, 10);

/// <summary>ترجیحات کارگاه (<c>/settings/preferences</c>).</summary>
public sealed class PreferencesSettingsLink() : StaticSettingsLink(
    "preferences", "ترجیحات",
    "شمارهٔ فرش، «نگله» یا «عدل»، متراژ هدف هر نگله، نرخ دلار و مشخصات صادرکننده.",
    "/settings/preferences", "sliders", SettingsGroup.Workshop, 20);

/// <summary>کاربران و نقش‌ها.</summary>
public sealed class UsersSettingsLink() : StaticSettingsLink(
    "users", "کاربران", "حساب بازرگان و انباردار، نقش هر کس و فعال یا غیرفعال بودن.",
    "/Users", "users", SettingsGroup.People, 10);

/// <summary>فیلدهای سفارشی فرش.</summary>
public sealed class CustomFieldsSettingsLink() : StaticSettingsLink(
    "custom-fields", "فیلدهای سفارشی", "خانه‌های اضافهٔ هر فرش، مثل رنگ متن و رنگ حاشیه.",
    "/Settings/CustomFields", "settings", SettingsGroup.Catalog, 10);

/// <summary>انواع مرحلهٔ کارگاه.</summary>
public sealed class StepTypesSettingsLink() : StaticSettingsLink(
    "step-types", "انواع مرحله", "کارهای کارگاه مثل شستشو و رفوگری، با نرخ پیش‌فرض هر کدام.",
    "/Settings/StepTypes", "workflow", SettingsGroup.Catalog, 20);

/// <summary>قالب‌های گردش کار.</summary>
public sealed class WorkflowTemplatesSettingsLink() : StaticSettingsLink(
    "workflows", "قالب‌های کارهای کارگاه", "ترتیب کارهای کارگاه که به فرش تازه داده می‌شود.",
    "/Workflows", "workflow", SettingsGroup.Catalog, 30);

/// <summary>طراحی برچسب.</summary>
public sealed class LabelsSettingsLink() : StaticSettingsLink(
    "labels", "برچسب‌ها", "طراحی برچسب چاپیِ فرش، با شماره و کد QR.",
    "/Labels", "tag", SettingsGroup.Documents, 10);

/// <summary>تاریخچهٔ فعالیت.</summary>
public sealed class ActivitySettingsLink() : StaticSettingsLink(
    "activity", "تاریخچهٔ فعالیت", "چه کسی، کِی، چه کاری کرد — فقط خواندنی.",
    "/Settings/Activity", "info", SettingsGroup.System, 10);
