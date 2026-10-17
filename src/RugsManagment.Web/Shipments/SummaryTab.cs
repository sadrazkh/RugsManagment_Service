namespace RugsManagment.Web.Shipments;

/// <summary>مدل partial زبانهٔ «حساب محموله».</summary>
/// <param name="ShipmentId">محموله</param>
/// <param name="Finalized">محموله قطعی است</param>
/// <param name="IslandProps">props جزیرهٔ <c>shipment-summary</c> (I.5)</param>
public sealed record ShipmentSummaryTabModel(Guid ShipmentId, bool Finalized, object IslandProps);

/// <summary>
/// زبانهٔ «حساب محموله» (فاز ۵، H.1، D.6؛ فقط Business؛ نام قدیمی «خلاصه» که واژه‌نامه عوضش کرد، A.2): میزبان جزیرهٔ
/// <c>shipment-summary</c> — «فروش این محموله: [__] دلار × نرخ [__] = … تومان» (<c>PUT …/revenue</c>)، «قیمت تمام‌شدهٔ هر
/// متر» و «سود» از <c>ShipmentProfitDto</c>.
///
/// انباردار این زبانه را نمی‌بیند؛ سود و بها پول‌اند (A.5).
/// </summary>
public sealed class SummaryTab : IShipmentTab
{
    public string Key => ShipmentTabKeys.Summary;

    public int Order => ShipmentTabOrder.Summary;

    public ShipmentTabHeader? Header(ShipmentTabContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.CanBusiness ? new ShipmentTabHeader("حساب محموله", "calculator") : null;
    }

    public Task<ShipmentTabView?> RenderAsync(ShipmentTabContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.CanBusiness)
            return Task.FromResult<ShipmentTabView?>(null);

        var id = context.Shipment.Id;
        var model = new ShipmentSummaryTabModel(id, context.Locked, new { shipmentId = id });
        return Task.FromResult<ShipmentTabView?>(ShipmentTabView.ForPartial("~/Views/Shipments/_TabSummary.cshtml", model));
    }
}
