namespace RugsManagment.Web.Settings;

/// <summary>
/// کاشی «ارزش گمرکی» در مرکز تنظیمات (گروه فیلدها و فهرست‌ها) → <c>/settings/declared-values</c>؛ Program.cs خودکار ثبتش
/// می‌کند. متن کاشی از صفحهٔ تنظیمات است و واژهٔ اظهار صادراتی را نمی‌آورد (واژه‌نامهٔ A.2).
/// </summary>
public sealed class DeclaredValuesSettingsLink() : StaticSettingsLink(
    "declared-values", "ارزش گمرکی",
    "دلار هر متر برای هر محل بافت. فرش‌هایی که هنوز قطعی نشده‌اند با همین جدول ارزش‌گذاری می‌شوند.",
    "/settings/declared-values", "dollar", SettingsGroup.Catalog, 40);
