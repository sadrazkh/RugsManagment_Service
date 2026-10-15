namespace RugsManagment.Web.Settings;

/// <summary>کاشی «ستون‌های جدول» در مرکز تنظیمات (گروه جدول‌ها و خروجی‌ها)؛ Program.cs خودکار ثبتش می‌کند.</summary>
public sealed class GridSettingsLink() : StaticSettingsLink(
    "grid", "ستون‌های جدول",
    "ترتیب، نام و پهنای ستون‌های جدول ورود فرش و جدول فرش‌ها؛ ستون‌هایی که لازم ندارید پنهان شوند.",
    "/settings/grid", "spreadsheet", SettingsGroup.Documents, 5);
