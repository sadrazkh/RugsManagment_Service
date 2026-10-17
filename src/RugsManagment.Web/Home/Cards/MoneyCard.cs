using RugsManagment.Application.Common;

namespace RugsManagment.Web.Home.Cards;

/// <summary>مدل کارت پول خانه.</summary>
/// <param name="ShipmentId">اولین محمولهٔ باز (همان که کاشی‌ها هدف می‌گیرند)؛ null وقتی محمولهٔ بازی نیست</param>
/// <param name="ShipmentName">«محمولهٔ ۸»</param>
/// <param name="CostPerM2">قیمت تمام‌شدهٔ هر متر همان محموله؛ null یعنی هنوز معلوم نیست (فرشی یا متراژی ندارد)</param>
/// <param name="UnallocatedEntries">چند هزینه هنوز بین فرش‌ها تقسیم نشده؛ null یعنی دفتر هزینه در دسترس نبود</param>
/// <param name="PanelProps">«برای» پنل ثبت هزینه از خانه (A.4): محمولهٔ هدف، وگرنه خالی (پنل خودش «ورودی امروز» را می‌گذارد)</param>
/// <param name="CostsHref">صفحهٔ «هزینه‌ها»</param>
/// <param name="UnallocatedHref">همان صفحه، فقط «تقسیم‌نشده»‌ها</param>
public sealed record HomeMoneyModel(
    Guid? ShipmentId,
    string? ShipmentName,
    decimal? CostPerM2,
    int? UnallocatedEntries,
    object PanelProps,
    string CostsHref,
    string UnallocatedHref);

/// <summary>
/// کارت «پول» خانه (A.4؛ فقط Business): قیمت تمام‌شدهٔ هر متر محمولهٔ باز و شمار هزینه‌های «تقسیم‌نشده»، با «ثبت هزینه» و
/// «دیدن هزینه‌ها». عددها از خلاصهٔ خانه می‌آیند که منبع پولی‌اش (<c>IHomeMoneySource</c>) را فاز هزینه پر می‌کند و برای
/// غیر Business از پیش null است؛ این کارت هم برای انباردار اصلاً ساخته نمی‌شود (A.5).
/// </summary>
public sealed class MoneyCard : IHomeCard
{
    public string Key => "money";

    public int Order => HomeCardOrder.Money;

    public Task<HomeCardView?> BuildAsync(HomeCardContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.CanBusiness)
            return Task.FromResult<HomeCardView?>(null);

        var summary = context.Summary;
        var target = summary.OpenShipments.Count > 0 ? summary.OpenShipments[0] : null;
        object panel = target is null
            ? new { reloadOnSave = true }
            : new { scope = HomeRoutes.CostScopeShipment, shipmentId = target.Id, reloadOnSave = true };

        var model = new HomeMoneyModel(
            target?.Id,
            target is null ? null : $"محمولهٔ {Messages.FormatNumber(target.Number)}",
            summary.OpenShipmentCostPerM2,
            summary.UnallocatedEntries,
            panel,
            "/Costs",
            "/Costs?unallocated=1");

        return Task.FromResult<HomeCardView?>(
            new HomeCardView("~/Views/Shared/Components/HomeCards/Money.cshtml", model));
    }
}
