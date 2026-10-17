namespace RugsManagment.Web.Shipments;
public sealed class OutputTab : IShipmentTab
{
    public string Key => ShipmentTabKeys.Output;
    public int Order => ShipmentTabOrder.Output;
    public ShipmentTabHeader? Header(ShipmentTabContext context) => new("اکسل و چاپ","spreadsheet");
    public Task<ShipmentTabView?> RenderAsync(ShipmentTabContext context, CancellationToken cancellationToken)
        => Task.FromResult<ShipmentTabView?>(ShipmentTabView.ForIsland("shipment-output",new { shipmentId = context.Shipment.Id,locked = context.Locked }));
}
