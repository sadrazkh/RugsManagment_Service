namespace RugsManagment.Web.RugDetails;

/// <summary>مدل partial بخش «هزینه‌ها»ی صفحهٔ فرش.</summary>
/// <param name="RugId">فرش</param>
/// <param name="IslandProps">props جزیرهٔ <c>cost-ledger</c> در نمای فشرده</param>
public sealed record RugCostsPanelModel(Guid RugId, object IslandProps);

/// <summary>
/// بخش «هزینه‌ها»ی صفحهٔ فرش (فاز ۵، H.1 «Rug details» ردیف ۴؛ فقط Business): هزینه‌هایی که این فرش در آن‌ها سهم دارد (همان
/// دفتر هزینه‌ها با فیلتر فرش، فشرده) و «ثبت هزینه برای این فرش» که پنل را با «برای: این فرش» باز می‌کند. پس از ذخیره صفحه
/// تازه می‌شود تا کارت «بهای تمام‌شده» عدد تازه را نشان دهد (سرور در همان درخواست حساب کرده است).
///
/// انباردار این بخش را نمی‌بیند؛ پول تومانی برای او نیست (A.5). خودِ API هم Business است.
/// </summary>
public sealed class CostsPanel : IRugDetailPanel
{
    public string Key => "costs";

    public RugDetailSlot Slot => RugDetailSlot.Side;

    public int Order => 20;

    public Task<RugDetailPanelView?> RenderAsync(RugDetailPanelContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.CanSeeMoney)
            return Task.FromResult<RugDetailPanelView?>(null);

        var rugId = context.Rug.Id;
        var props = new
        {
            filters = new { rugId },
            openNew = false,
            compact = true,
            // scope 0 = Selection (این فرش)؛ پس از ذخیره صفحه تازه شود تا جمع‌های سرور دیده شوند
            preset = new { scope = 0, rugIds = new[] { rugId }, reloadOnSave = true }
        };

        return Task.FromResult<RugDetailPanelView?>(
            RugDetailPanelView.ForPartial("~/Views/Costs/_RugCosts.cshtml", new RugCostsPanelModel(rugId, props)));
    }
}
