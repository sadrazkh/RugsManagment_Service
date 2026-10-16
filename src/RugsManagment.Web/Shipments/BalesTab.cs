using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Web.Navigation;

namespace RugsManagment.Web.Shipments;

/// <summary>نمای زبانهٔ نگله‌ها: جدول (پیش‌فرض) یا کارت‌ها (تبلت، H.6).</summary>
public static class BalesTabViews
{
    public const string Table = "table";
    public const string Cards = "cards";

    /// <summary><c>?view=cards</c> کارت‌ها، هر چیز دیگر جدول.</summary>
    public static string Parse(string? value)
        => string.Equals(value?.Trim(), Cards, StringComparison.OrdinalIgnoreCase) ? Cards : Table;
}

/// <summary>مدل partial زبانهٔ نگله‌ها.</summary>
/// <param name="Shipment">محموله با خلاصهٔ نگله‌ها و جمع‌ها (برای نمای بی‌اسکریپت پیش از سوار شدن جزیره)</param>
/// <param name="Terms">واژه‌های نگله/عدل کارگاه</param>
/// <param name="Locked">محموله قطعی است: فقط‌خواندنی (C.8)</param>
/// <param name="View">«table» یا «cards»</param>
/// <param name="TargetAreaM2">متراژ هدف هر نگله (محموله ?? کارگاه)؛ null بی‌هدف</param>
/// <param name="TolerancePercent">بازهٔ مجاز اطراف هدف (درصد)</param>
/// <param name="TableHref">پیوند نمای جدول</param>
/// <param name="CardsHref">پیوند نمای کارت‌ها</param>
/// <param name="PackHref">پیوند «حالت اسکن (گوشی)»</param>
/// <param name="IslandProps">props جزیرهٔ <c>baling-board</c> (I.5)</param>
public sealed record BalesTabModel(
    ShipmentDto Shipment,
    BaleTermWords Terms,
    bool Locked,
    string View,
    decimal? TargetAreaM2,
    int TolerancePercent,
    string TableHref,
    string CardsHref,
    string PackHref,
    object IslandProps);

/// <summary>
/// زبانهٔ «نگله‌ها» (فاز ۴b، H.1): میزبان جزیرهٔ <c>baling-board</c> — جعبهٔ «شماره را بزنید یا اسکن کنید»، «نگلهٔ جاری» و
/// «نگلهٔ بعد»، جدول نگله‌ها با پانویس هر نگله یا کارت‌ها با «انتقال» و «اندازه». عنوان زبانه از واژهٔ کارگاه است
/// («عدل‌ها» برای کارگاهی که عدل می‌گوید).
///
/// چرا partial و نه فقط جزیره: پیوند «حالت اسکن (گوشی)» و دو نمای «جدول / کارت‌ها» پیوندهای معمولی‌اند که بی‌اسکریپت هم
/// کار می‌کنند، و تا جزیره سوار شود (یا اگر نشد) خلاصهٔ فقط‌خواندنی نگله‌ها همان‌جا دیده می‌شود، نه یک جای خالی.
///
/// props جزیره همان I.5 است: <c>{ shipmentId, view, targetAreaM2, tolerancePercent, locked }</c>. نمای اولیه از
/// <c>?view=</c> می‌آید تا پیوند هر نما قابل نشانک‌گذاری باشد. خانه با <c>?tab=bales</c> به همین‌جا می‌آید.
/// </summary>
public sealed class BalesTab(IHttpContextAccessor httpContextAccessor) : IShipmentTab
{
    public string Key => ShipmentTabKeys.Bales;

    public int Order => ShipmentTabOrder.Bales;

    public ShipmentTabHeader? Header(ShipmentTabContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ShipmentTabHeader(context.Terms.Bales, "bale");
    }

    public Task<ShipmentTabView?> RenderAsync(ShipmentTabContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = httpContextAccessor.HttpContext;
        var view = BalesTabViews.Parse(http?.Request.Query["view"].ToString());
        var id = context.Shipment.Id;

        string Resolve(string path) => http is null ? path : NavBuilder.Resolve(http, path);

        var packHref = Resolve(ShipmentPackRoutes.Pack(id));

        // پنج عضو اول همان I.5؛ شمارهٔ محموله (برای متن قفل و پیام‌ها) و نشانی حالت اسکن عضوهای اختیاری خودِ جزیره‌اند
        var props = new
        {
            shipmentId = id,
            view,
            targetAreaM2 = context.TargetBaleAreaM2,
            tolerancePercent = context.TolerancePercent,
            locked = context.Locked,
            shipmentNumber = context.Shipment.Number,
            packUrl = packHref
        };

        var model = new BalesTabModel(
            context.Shipment,
            context.Terms,
            context.Locked,
            view,
            context.TargetBaleAreaM2,
            context.TolerancePercent,
            Resolve(ShipmentRoutes.Tab(id, ShipmentTabKeys.Bales)),
            Resolve(ShipmentRoutes.Tab(id, ShipmentTabKeys.Bales) + "&view=" + BalesTabViews.Cards),
            packHref,
            props);

        return Task.FromResult<ShipmentTabView?>(ShipmentTabView.ForPartial("~/Views/Shipments/_TabBales.cshtml", model));
    }
}

/// <summary>نشانی «حالت اسکن (گوشی)» (G.5).</summary>
public static class ShipmentPackRoutes
{
    /// <summary><c>/Shipments/{id}/Pack</c>.</summary>
    public static string Pack(Guid shipmentId) => $"/Shipments/{shipmentId}/Pack";

    /// <summary>همان، با نگلهٔ آغازین.</summary>
    public static string Pack(Guid shipmentId, Guid baleId) => $"{Pack(shipmentId)}?baleId={baleId}";
}
