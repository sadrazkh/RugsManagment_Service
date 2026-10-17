using RugsManagment.Web.Navigation;

namespace RugsManagment.Web.Shipments;

/// <summary>مدل partial زبانهٔ «هزینه‌ها»ی محموله.</summary>
/// <param name="ShipmentId">محموله</param>
/// <param name="Locked">محموله قطعی است (هزینهٔ دیر مجاز است؛ فقط نوار «فقط سود را تغییر می‌دهد»)</param>
/// <param name="LedgerHref">«همه در صفحهٔ هزینه‌ها» با فیلتر همین محموله</param>
/// <param name="IslandProps">props جزیرهٔ <c>shipment-costs</c> (I.5)</param>
public sealed record ShipmentCostsTabModel(Guid ShipmentId, bool Locked, string LedgerHref, object IslandProps);

/// <summary>
/// زبانهٔ «هزینه‌ها»ی کارگاه محموله (فاز ۵، H.1؛ فقط Business): میزبان جزیرهٔ <c>shipment-costs</c> — هزینه‌هایی که به این
/// محموله وصل‌اند یا فرشی از آن را دارند، با «ثبت هزینه» که پنل را با «برای: این محموله» باز می‌کند، تراشهٔ «تقسیم‌نشده» و
/// حذف با «برگردان». محمولهٔ قطعی هم هزینهٔ دیر می‌پذیرد (D.7 #12).
///
/// انباردار این زبانه را نمی‌بیند (<see cref="Header"/> null)؛ پول تومانی برای او نیست و خودِ API هم Business است.
/// </summary>
public sealed class CostsTab(IHttpContextAccessor httpContextAccessor) : IShipmentTab
{
    public string Key => ShipmentTabKeys.Costs;

    public int Order => ShipmentTabOrder.Costs;

    public ShipmentTabHeader? Header(ShipmentTabContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.CanBusiness ? new ShipmentTabHeader("هزینه‌ها", "coins") : null;
    }

    public Task<ShipmentTabView?> RenderAsync(ShipmentTabContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.CanBusiness)
            return Task.FromResult<ShipmentTabView?>(null);

        var id = context.Shipment.Id;
        var ledger = $"/Costs?shipmentId={id}";
        var http = httpContextAccessor.HttpContext;
        var model = new ShipmentCostsTabModel(
            id,
            context.Locked,
            http is null ? ledger : NavBuilder.Resolve(http, ledger),
            new { shipmentId = id, locked = context.Locked });

        return Task.FromResult<ShipmentTabView?>(ShipmentTabView.ForPartial("~/Views/Shipments/_TabCosts.cshtml", model));
    }
}
