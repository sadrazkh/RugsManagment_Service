namespace RugsManagment.Web.Settings;

/// <summary>
/// کاشی «نوع‌های هزینه» در مرکز تنظیمات (گروه فیلدها و فهرست‌ها) → <c>/settings/cost-categories</c>؛ Program.cs خودکار ثبتش
/// می‌کند. بازرگان خودش با «+ نوع تازه» در پنل ثبت هزینه نوع می‌سازد؛ این‌جا جای پیش‌فرض‌ها، نماد، ترتیب و غیرفعال کردن است.
/// </summary>
public sealed class CostCategoriesSettingsLink() : StaticSettingsLink(
    "cost-categories", "نوع‌های هزینه",
    "شستشو، کرایه، گمرک، قیمت خرید و دلالی: نماد، مبلغ پیش‌فرض و اینکه هر کدام چطور بین فرش‌ها تقسیم شود.",
    "/settings/cost-categories", "coins", SettingsGroup.Catalog, 35);
