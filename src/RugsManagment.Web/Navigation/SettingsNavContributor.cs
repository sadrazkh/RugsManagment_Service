using RugsManagment.Web.Settings;

namespace RugsManagment.Web.Navigation;

/// <summary>
/// «تنظیمات» در منو (فقط Configure) و هر کاشی مرکز تنظیمات به‌صورت فرمان Ctrl+K.
///
/// چرا کاشی‌ها فرمان می‌شوند و آیتم منو نه: نوار کناری تنظیم‌کننده یک «تنظیمات» دارد که به مرکز تنظیمات
/// می‌رود (جدول A.4)؛ ولی کسی که می‌داند دنبال «کاربران» است با Ctrl+K مستقیم به همان صفحه می‌رسد. هر فاز
/// که یک <see cref="ISettingsLink"/> اضافه کند (ستون‌های جدول، دسته‌های هزینه، ارزش گمرکی، …) هم در مرکز
/// تنظیمات و هم در Ctrl+K دیده می‌شود، بی‌آنکه این فایل ویرایش شود.
/// </summary>
public sealed class SettingsNavContributor(IEnumerable<ISettingsLink> links) : INavContributor
{
    public const string SettingsKey = "settings";

    public IEnumerable<NavItem> GetItems(NavContext context)
    {
        if (!context.CanConfigure) yield break;

        // در پیش‌نمایش «نمای ساده» تنظیمات از نوار کناری بیرون می‌رود (منو همان منوی بازرگان است)
        // ولی در «بیشتر» می‌ماند تا تنظیم‌کننده راه برگشت را گم نکند.
        var slots = (context.IsSimple ? NavSlot.None : NavSlot.Sidebar) | NavSlot.More | NavSlot.Command;

        yield return new NavItem(SettingsKey, "تنظیمات", "/Settings", "settings", NavOrder.Settings, slots,
            Hint: "کاربران، فیلدها و ترجیحات کارگاه",
            ActivePrefix: "/Settings|/Users");

        var order = NavOrder.Settings + 1;
        foreach (var link in links
                     .OrderBy(l => l.Group)
                     .ThenBy(l => l.Order)
                     .ThenBy(l => l.Key, StringComparer.Ordinal))
        {
            yield return new NavItem($"{SettingsKey}:{link.Key}", link.Title, link.Href, link.Icon, order++,
                NavSlot.Command, Hint: link.Description);
        }
    }
}
