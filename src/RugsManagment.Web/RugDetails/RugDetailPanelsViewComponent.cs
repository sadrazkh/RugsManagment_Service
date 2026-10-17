using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Support;

namespace RugsManagment.Web.RugDetails;

/// <summary>
/// رندر همهٔ <see cref="IRugDetailPanel"/>های یک ستون صفحهٔ جزئیات فرش:
/// <c>@await Component.InvokeAsync("RugDetailPanels", new { rug = Model, slot = RugDetailSlot.Side })</c>.
///
/// پنل‌ها از اسمبلی وب پیدا می‌شوند (هر کلاس غیرانتزاعی که <see cref="IRugDetailPanel"/> است) و با سرویس‌های همان
/// درخواست ساخته می‌شوند؛ پس فاز تازه فقط یک فایل اضافه می‌کند. خطای یک پنل صفحه را نمی‌شکند: آن پنل نشان داده نمی‌شود و
/// خطا در لاگ می‌ماند (صفحهٔ فرش مهم‌تر از یک بخش اضافه است).
/// </summary>
public sealed class RugDetailPanelsViewComponent(
    IServiceProvider services,
    IUserPreferencesAccessor preferences,
    ILogger<RugDetailPanelsViewComponent> logger) : ViewComponent
{
    /// <summary>همهٔ نوع‌های پنل اسمبلی وب، یک بار.</summary>
    private static readonly Lazy<IReadOnlyList<Type>> PanelTypes = new(() =>
        typeof(IRugDetailPanel).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                        && typeof(IRugDetailPanel).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList());

    public async Task<IViewComponentResult> InvokeAsync(RugDto rug, RugDetailSlot slot)
    {
        var user = HttpContext.User;
        var context = new RugDetailPanelContext(
            rug, user, preferences.IsSimple, Policies.IsBusiness(user), user.RequireTenantId());

        var panels = PanelTypes.Value
            .Select(t => (IRugDetailPanel)ActivatorUtilities.CreateInstance(services, t))
            .Where(p => p.Slot == slot)
            .OrderBy(p => p.Order)
            .ThenBy(p => p.Key, StringComparer.Ordinal);

        var views = new List<RugDetailPanelView>();
        foreach (var panel in panels)
        {
            try
            {
                if (await panel.RenderAsync(context, HttpContext.RequestAborted) is { } view)
                    views.Add(view);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "پنل صفحهٔ فرش «{Panel}» رندر نشد (فرش {RugId}).", panel.Key, rug.Id);
            }
        }

        return View("~/Views/Rugs/_Details.Panels.cshtml", views);
    }
}
