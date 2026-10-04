using Microsoft.AspNetCore.Mvc.Infrastructure;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Navigation;

/// <summary>
/// آیتم‌های پایهٔ منو: خانه، فرش‌ها، صفحه‌های موجودِ پیش از بازطراحی (گزارش‌ها، فروش، کارگاه‌ها، کارهای
/// کارگاه، ورودی‌ها، برچسب‌ها، داشبورد) و «بیشتر» — مطابق جدول ناوبری A.4.
///
/// قاعدهٔ جایگاه‌ها:
/// <list type="bullet">
/// <item>اصلی (خانه، فرش‌ها): نوار کناری + نوار پایین در هر دو حالت.</item>
/// <item>فرعی (گزارش‌ها تا برچسب‌ها): در حالت کامل در نوار کناری؛ در حالت ساده فقط در «بیشتر». در «بیشتر»
/// همیشه هستند تا روی گوشی (که نوار کناری‌اش کشویی است) هم یک‌لمسی برسند.</item>
/// <item>کمکی (داشبورد، کارت راهنما، حساب کاربری): فقط «بیشتر» و Ctrl+K.</item>
/// <item>«بیشتر»: همیشه در نوار پایین؛ در حالت ساده در نوار کناری هم (بازرگان پنج دکمه می‌بیند).</item>
/// </list>
///
/// هر آیتم فقط وقتی برمی‌گردد که بیننده اجازهٔ باز کردنش را دارد (همان سیاستِ کنترلرِ آن صفحه)، پس منو
/// هیچ‌وقت دری نشان نمی‌دهد که پشتش «دسترسی ندارید» است.
///
/// ورود فرش، محموله‌ها، هزینه‌ها و ورود از اکسل صفحه‌های فازهای بعدند. جایشان در جدول A.4 همین‌جا نوشته
/// شده ولی هر کدام فقط وقتی دیده می‌شود که کنترلرش در برنامه باشد (<see cref="AppShell.HasController"/>):
/// تا آن روز منو پیوندِ مرده (۴۰۴) نشان نمی‌دهد، و روزی که صفحه آمد بی‌ویرایش این فایل سر جای خودش در
/// نوار کناری و نوار پایین می‌نشیند (بازرگان: خانه · فرش‌ها · محموله‌ها · هزینه‌ها · بیشتر). فازی که بخواهد
/// برچسب یا جایگاه دیگری بدهد، آیتمی با همان کلید و Order کمتر در contributor خودش برمی‌گرداند.
/// </summary>
public sealed class CoreNavContributor(IActionDescriptorCollectionProvider actions) : INavContributor
{
    /// <summary>کلید فرمان «ثبت فرش تازه» — نشانی‌اش با آمدن صفحهٔ ورود فرش خودکار به آن‌جا می‌رود.</summary>
    public const string NewRugKey = "new-rug";

    private const string MoneyGroup = "حساب و فروش";
    private const string WorkshopGroup = "کارگاه";

    public IEnumerable<NavItem> GetItems(NavContext context)
    {
        if (context.Role == UserRole.SystemAdmin)
        {
            // مدیر سامانه فقط /Admin دارد (بدون کارگاه)
            yield return new NavItem("admin", "کارگاه‌ها", "/Admin", "building", NavOrder.Home,
                NavSlot.Sidebar | NavSlot.Bottom | NavSlot.Command, ActivePrefix: "/Admin");
            yield return new NavItem("profile", "حساب کاربری", "/Account/Profile", "user", NavOrder.Profile,
                NavSlot.Bottom | NavSlot.Command, ActivePrefix: "/Account/Profile");
            yield break;
        }

        if (!context.CanFloor) yield break;

        // فرعی‌ها در حالت ساده از نوار کناری بیرون می‌روند و فقط در «بیشتر» می‌مانند
        var secondary = (context.IsSimple ? NavSlot.None : NavSlot.Sidebar) | NavSlot.More | NavSlot.Command;
        var primary = NavSlot.Sidebar | NavSlot.Bottom | NavSlot.Command;

        yield return new NavItem("home", "خانه", "/", "home", NavOrder.Home, primary,
            ActivePrefix: "/|/Home");

        var hasIntake = AppShell.HasController(actions, ShellFeatures.IntakeController);

        yield return new NavItem(NewRugKey, "ثبت فرش تازه", hasIntake ? "/Intake" : "/Rugs/Create", "plus",
            NavOrder.Home + 5, NavSlot.Command, Hint: "فرش‌هایی که از فروشنده رسید");

        // «ورود فرش»: در حالت ساده کاشیِ «ثبت فرش تازه» در خانه همین کار را می‌کند، پس فقط در نوار کامل
        if (hasIntake)
        {
            yield return new NavItem("intake", "ورود فرش", "/Intake", "plus", NavOrder.Intake,
                (context.IsSimple ? NavSlot.None : NavSlot.Sidebar) | NavSlot.Command,
                Hint: "فرش‌های رسیده از فروشنده، در جدول");
        }

        yield return new NavItem("rugs", "فرش‌ها", "/Rugs", "rug", NavOrder.Rugs, primary,
            Hint: "همهٔ فرش‌ها؛ جست‌وجو با شماره");

        if (AppShell.HasController(actions, ShellFeatures.ShipmentsController))
        {
            yield return new NavItem("shipments", "محموله‌ها", "/Shipments", "truck", NavOrder.Shipments, primary,
                Hint: "اظهارهای صادراتی و نگله‌بندی");
        }

        if (context.CanBusiness && AppShell.HasController(actions, ShellFeatures.CostsController))
        {
            yield return new NavItem("costs", "هزینه‌ها", "/Costs", "coins", NavOrder.Costs, primary,
                Hint: "رسیدها و تقسیمشان بین فرش‌ها");
        }

        if (context.CanBusiness && AppShell.HasController(actions, ShellFeatures.ImportController))
        {
            yield return new NavItem("import", "ورود از اکسل", "/Import", "upload", NavOrder.Import, secondary,
                Hint: "اظهار یا فهرست فرش از فایل اکسل");
        }

        if (context.CanBusiness)
        {
            yield return new NavItem("reports", "گزارش‌ها", "/Reports", "chart", NavOrder.Reports, secondary,
                Hint: "فروش، سود و فرش‌هایی که مانده‌اند", Group: MoneyGroup);
            yield return new NavItem("sales", "فروش", "/Sales", "receipt", NavOrder.Sales, secondary,
                Hint: "فروش‌های ثبت‌شده و طلب‌ها", Group: MoneyGroup);
            yield return new NavItem("providers", "کارگاه‌ها", "/Providers", "handshake", NavOrder.Providers, secondary,
                Hint: "شست‌وشو، رفو و طرف‌حساب‌ها؛ حساب و پرداخت", Group: MoneyGroup);
        }

        yield return new NavItem("workflows", "کارهای کارگاه", "/Workflows", "workflow", NavOrder.Workflows, secondary,
            Hint: "مرحله‌های کار روی فرش‌ها", Group: WorkshopGroup);
        yield return new NavItem("batches", "ورودی‌ها", "/Groups", "package", NavOrder.Batches, secondary,
            Hint: "فرش‌های هر فروشنده در هر روز", Group: WorkshopGroup);
        yield return new NavItem("labels", "برچسب‌ها", "/Labels", "tag", NavOrder.Labels, secondary,
            Hint: "چاپ برچسب برای فرش‌ها", Group: WorkshopGroup);

        yield return new NavItem("dashboard", "داشبورد", "/Dashboard", "dashboard", NavOrder.Dashboard,
            NavSlot.More | NavSlot.Command, Hint: "خلاصهٔ عددی فرش‌ها و کارهای مانده");
        yield return new NavItem("help-card", "کارت راهنما", "/Help/Card", "help", NavOrder.Help,
            NavSlot.More | NavSlot.Command, Hint: "یک برگ برای چاپ و گذاشتن کنار کامپیوتر");
        yield return new NavItem("profile", "حساب کاربری", "/Account/Profile", "user", NavOrder.Profile,
            NavSlot.More | NavSlot.Command, Hint: "نام و رمز عبور", ActivePrefix: "/Account/Profile");

        yield return new NavItem(NavBuilder.MoreKey, "بیشتر", "/More", "more", NavOrder.More,
            NavSlot.Bottom | (context.IsSimple ? NavSlot.Sidebar : NavSlot.None));
    }
}

/// <summary>
/// نام کنترلرِ صفحه‌هایی که فازهای بعد می‌سازند. منو و کاشی‌های خانه با <see cref="AppShell.HasController"/>
/// می‌سنجند که صفحه آمده یا نه (نه با شمارهٔ فاز)، پس هیچ‌کدام برای روشن شدن ویرایش نمی‌خواهند.
/// </summary>
public static class ShellFeatures
{
    /// <summary><c>/Intake</c> — جدول ورود فرش (فاز ۳)</summary>
    public const string IntakeController = "Intake";

    /// <summary><c>/Shipments</c> — محموله‌ها و نگله‌بندی (فاز ۴)</summary>
    public const string ShipmentsController = "Shipments";

    /// <summary><c>/Costs</c> — هزینه‌ها و پنل ثبت هزینه (فاز ۵)</summary>
    public const string CostsController = "Costs";

    /// <summary><c>/Import</c> — ورود از اکسل (فاز ۸)</summary>
    public const string ImportController = "Import";
}
