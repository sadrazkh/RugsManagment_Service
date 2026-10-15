using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Navigation;
using RugsManagment.Web.Support;

namespace RugsManagment.Web.Shipments;

/// <summary>یک زبانه در نوار زبانه‌ها.</summary>
/// <param name="Key">کلید (<c>?tab=</c>)</param>
/// <param name="Title">عنوان</param>
/// <param name="Icon">نماد</param>
/// <param name="Href">پیوند همین زبانه</param>
/// <param name="Active">زبانهٔ باز</param>
public sealed record ShipmentTabLink(string Key, string Title, string Icon, string Href, bool Active);

/// <summary>مدل view نوار زبانه‌ها و محتوای زبانهٔ باز.</summary>
/// <param name="Context">بیننده و محموله</param>
/// <param name="Tabs">زبانه‌هایی که بیننده می‌بیند، به ترتیب</param>
/// <param name="Active">محتوای زبانهٔ باز؛ null وقتی هیچ زبانه‌ای نیست یا زبانه چیزی نداد</param>
public sealed record ShipmentTabsModel(
    ShipmentTabContext Context,
    IReadOnlyList<ShipmentTabLink> Tabs,
    ShipmentTabView? Active);

/// <summary>
/// نوار زبانه‌های کارگاه محموله و محتوای زبانهٔ باز:
/// <c>@await Component.InvokeAsync("ShipmentTabs", new { shipment = Model.Shipment, tab = Model.Tab })</c>.
///
/// زبانه‌ها از اسمبلی وب پیدا می‌شوند (هر کلاس غیرانتزاعی که <see cref="IShipmentTab"/> است) و با سرویس‌های همان درخواست
/// ساخته می‌شوند؛ پس فاز تازه فقط یک فایل اضافه می‌کند. هر زبانه یک پیوند معمولی است (<c>?tab=bales</c>)، نه جابه‌جایی با
/// اسکریپت: بی‌جاوااسکریپت هم کار می‌کند، دکمهٔ «برگشت» مرورگر درست است و فقط زبانهٔ باز ساخته می‌شود. کلید ناشناخته یا
/// زبانه‌ای که بیننده نمی‌بیند → اولین زبانه.
///
/// خطای یک زبانه صفحه را نمی‌شکند: آن زبانه کنار می‌رود و خطا در لاگ می‌ماند (سر و بقیهٔ محموله مهم‌ترند). تا وقتی هیچ
/// زبانه‌ای ثبت نشده (پیش از فاز ۴b)، view خلاصهٔ فقط‌خواندنی نگله‌ها را از خودِ <see cref="ShipmentDto"/> نشان می‌دهد.
/// </summary>
public sealed class ShipmentTabsViewComponent(
    IServiceProvider services,
    IUserPreferencesAccessor preferences,
    ILogger<ShipmentTabsViewComponent> logger) : ViewComponent
{
    /// <summary>همهٔ نوع‌های زبانهٔ اسمبلی وب، یک بار.</summary>
    private static readonly Lazy<IReadOnlyList<Type>> TabTypes = new(() =>
        typeof(IShipmentTab).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                        && typeof(IShipmentTab).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList());

    public async Task<IViewComponentResult> InvokeAsync(ShipmentDto shipment, string? tab)
    {
        var http = HttpContext;
        var user = http.User;
        var context = new ShipmentTabContext(
            Shipment: shipment,
            User: user,
            TenantId: user.RequireTenantId(),
            Simple: preferences.IsSimple,
            CanBusiness: Policies.IsBusiness(user),
            CanConfigure: Policies.IsConfigure(user),
            Terms: await AppShell.TermsAsync(http),
            Preferences: await AppShell.TenantPreferencesAsync(http));

        // زبانه‌های دیدنی برای این بیننده، به ترتیب؛ کلید تکراری (دو کلاس با یک کلید) فقط اولی
        var visible = new List<(IShipmentTab Tab, ShipmentTabHeader Header)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in CreateTabs().OrderBy(t => t.Order).ThenBy(t => t.Key, StringComparer.Ordinal))
        {
            if (!seen.Add(candidate.Key)) continue;
            try
            {
                if (candidate.Header(context) is { } header)
                    visible.Add((candidate, header));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "سرِ زبانهٔ «{Tab}» محموله ساخته نشد (محموله {ShipmentId}).", candidate.Key, shipment.Id);
            }
        }

        var activeKey = visible
            .Select(v => v.Tab.Key)
            .FirstOrDefault(k => string.Equals(k, tab, StringComparison.OrdinalIgnoreCase))
            ?? visible.Select(v => v.Tab.Key).FirstOrDefault();

        ShipmentTabView? view = null;
        if (activeKey is not null)
        {
            var active = visible.First(v => v.Tab.Key == activeKey).Tab;
            try
            {
                view = await active.RenderAsync(context, http.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "زبانهٔ «{Tab}» محموله رندر نشد (محموله {ShipmentId}).", active.Key, shipment.Id);
            }
        }

        var links = visible
            .Select(v => new ShipmentTabLink(
                v.Tab.Key,
                v.Header.Title,
                v.Header.Icon,
                NavBuilder.Resolve(http, ShipmentRoutes.Tab(shipment.Id, v.Tab.Key)),
                v.Tab.Key == activeKey))
            .ToList();

        return View(new ShipmentTabsModel(context, links, view));
    }

    private IEnumerable<IShipmentTab> CreateTabs()
    {
        foreach (var type in TabTypes.Value)
        {
            IShipmentTab? tab = null;
            try
            {
                tab = (IShipmentTab)ActivatorUtilities.CreateInstance(services, type);
            }
            catch (Exception ex) when (ex is InvalidOperationException or MissingMethodException)
            {
                // وابستگیِ ثبت‌نشده (مثلاً سرویس فاز بعد هنوز نیامده): همان زبانه کنار می‌رود
                logger.LogError(ex, "زبانهٔ محمولهٔ {Type} ساخته نشد.", type.FullName);
            }

            if (tab is not null)
                yield return tab;
        }
    }
}

/// <summary>جای متراژ یک نگله نسبت به هدف (نوار هدف، C.2).</summary>
public enum BaleTargetState
{
    /// <summary>هدفی تعیین نشده.</summary>
    NoTarget = 0,

    /// <summary>کمتر از هدف × (۱ − t): خنثی.</summary>
    Below = 1,

    /// <summary>در بازهٔ ± t: سبز.</summary>
    Within = 2,

    /// <summary>بیشتر از هدف × (۱ + t): نارنجی (هدف ۱۳ متر با ۱۰٪ → بالای ۱۴٫۳).</summary>
    Over = 3
}

/// <summary>قاعدهٔ نوار هدف نگله، یک جا (C.2)؛ با decimal، بی گرد کردن.</summary>
public static class BaleTarget
{
    public static BaleTargetState State(decimal areaM2, decimal? targetM2, int tolerancePercent)
    {
        if (targetM2 is not { } target || target <= 0m)
            return BaleTargetState.NoTarget;

        var tolerance = Math.Clamp(tolerancePercent, 0, 100) / 100m;
        if (areaM2 > target * (1m + tolerance)) return BaleTargetState.Over;
        if (areaM2 < target * (1m - tolerance)) return BaleTargetState.Below;
        return BaleTargetState.Within;
    }
}

/// <summary>نشانی‌های صفحه‌های محموله (همان قراردادی که خانه با <c>HomeRoutes</c> به کار می‌برد).</summary>
public static class ShipmentRoutes
{
    /// <summary>فهرست محموله‌ها.</summary>
    public const string Index = "/Shipments";

    /// <summary>کارگاه یک محموله.</summary>
    public static string Workspace(Guid id) => $"/Shipments/{id}";

    /// <summary>یک زبانهٔ کارگاه محموله.</summary>
    public static string Tab(Guid id, string key) => $"/Shipments/{id}?tab={Uri.EscapeDataString(key)}";
}
