using System.Security.Claims;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Shipments;

/// <summary>
/// یک زبانهٔ کارگاه محموله (<c>/Shipments/{id}?tab=…</c>) — درز فاز ۴a (spec H.1، K.1).
///
/// چرا درز: زبانه‌ها را فازهای مختلف می‌سازند — «نگله‌ها» (فاز ۴b)، «هزینه‌ها» و «حساب محموله» (فاز ۵)، «اکسل و چاپ»
/// (فاز ۶). اگر هر کدام <c>Workspace.cshtml</c> را ویرایش می‌کرد، فازهای موازی روی یک فایل به هم می‌خوردند. حالا هر فاز یک
/// کلاس در <c>Web/Shipments/</c> می‌گذارد و <see cref="ShipmentTabsViewComponent"/> خودکار پیدا و رندرش می‌کند (بی ثبت در
/// DI و بی ویرایش Program.cs)، با سرویس‌های همان درخواست. کلاس‌های Tailwind این پوشه را Tailwind می‌بیند
/// (<c>@source</c> در app.css).
///
/// قرارداد:
/// <list type="bullet">
/// <item><see cref="Header"/> برای بیننده‌ای که نباید زبانه را ببیند null برمی‌گرداند (زبانه‌های پولی برای انباردار)؛ زبانه
/// فقط آن‌چه بیننده اجازه دارد نشان می‌دهد، دسترسی واقعی همیشه روی سرور سنجیده می‌شود.</item>
/// <item><see cref="RenderAsync"/> فقط برای زبانهٔ باز صدا زده می‌شود (هر زبانه یک پیوند است، نه همهٔ زبانه‌ها در یک
/// صفحه)، پس زبانهٔ سنگین هزینهٔ زبانه‌های دیگر را نمی‌دهد.</item>
/// <item>کلید همان <c>?tab=</c> است (<see cref="ShipmentTabKeys"/>)؛ خانه با <c>?tab=bales</c> و <c>?tab=output</c> به
/// این‌جا می‌آید (<c>HomeRoutes</c>).</item>
/// </list>
/// </summary>
public interface IShipmentTab
{
    /// <summary>شناسهٔ پایدار و یکتا، همان مقدار <c>?tab=</c> (از <see cref="ShipmentTabKeys"/>).</summary>
    string Key { get; }

    /// <summary>ترتیب زبانه‌ها؛ کوچک‌تر جلوتر (از <see cref="ShipmentTabOrder"/>).</summary>
    int Order { get; }

    /// <summary>عنوان و نماد زبانه برای این بیننده و این محموله، یا null یعنی «این زبانه را نشان نده».</summary>
    ShipmentTabHeader? Header(ShipmentTabContext context);

    /// <summary>محتوای زبانهٔ باز: یک partial یا یک جزیره؛ null یعنی «چیزی برای نشان دادن نیست».</summary>
    Task<ShipmentTabView?> RenderAsync(ShipmentTabContext context, CancellationToken cancellationToken);
}

/// <summary>سرِ یک زبانه.</summary>
/// <param name="Title">عنوان فارسی («نگله‌ها» از <see cref="ShipmentTabContext.Terms"/>، نه متن ثابت)</param>
/// <param name="Icon">نام نماد در <c>icons.svg</c></param>
public sealed record ShipmentTabHeader(string Title, string Icon);

/// <summary>آن‌چه یک زبانه برای سر و محتوایش لازم دارد.</summary>
/// <param name="Shipment">محموله، با نگله‌ها، جمع‌ها و آمادگی (بی هیچ مبلغ تومانی)</param>
/// <param name="User">بیننده</param>
/// <param name="TenantId">کارگاه بیننده</param>
/// <param name="Simple">نمای ساده (فقط پوسته؛ دسترسی همان می‌ماند)</param>
/// <param name="CanBusiness">بیننده سیاست Business دارد (مبلغ تومانی، ساختن و قطعی کردن محموله)</param>
/// <param name="CanConfigure">بیننده سیاست Configure دارد</param>
/// <param name="Terms">واژه‌های نگله/عدل کارگاه</param>
/// <param name="Preferences">ترجیحات کارگاه (متراژ هدف، بازهٔ مجاز، …)؛ null اگر خوانده نشد</param>
public sealed record ShipmentTabContext(
    ShipmentDto Shipment,
    ClaimsPrincipal User,
    Guid TenantId,
    bool Simple,
    bool CanBusiness,
    bool CanConfigure,
    BaleTermWords Terms,
    TenantPreferencesDto? Preferences)
{
    /// <summary>محموله قطعی شده و اندازه‌ها، نگله‌ها و ارزش گمرکی‌اش قفل است (C.8).</summary>
    public bool Locked => Shipment.Status == ShipmentStatus.Finalized;

    /// <summary>متراژ هدف هر نگله: مال خودِ محموله، وگرنه ترجیح کارگاه (C.2)؛ null یعنی بی‌هدف.</summary>
    public decimal? TargetBaleAreaM2 =>
        Shipment.EffectiveTargetAreaM2 ?? Shipment.TargetBaleAreaM2 ?? Preferences?.DefaultBaleTargetAreaM2;

    /// <summary>بازهٔ مجاز اطراف هدف به درصد (ترجیح کارگاه؛ همان که سرور در محموله فرستاد).</summary>
    public int TolerancePercent => Shipment.BaleTargetTolerancePercent;
}

/// <summary>
/// محتوای یک زبانه: یک partial (با مدلش) یا یک جزیرهٔ Vue (با props). دقیقاً یکی؛ همان شکل
/// <c>RugDetailPanelView</c>.
/// </summary>
/// <param name="Partial">مسیر partial، مثل «~/Views/Shipments/_TabBales.cshtml»</param>
/// <param name="Model">مدل partial</param>
/// <param name="Island">نام جزیره (kebab-case)، مثل «baling-board»</param>
/// <param name="Props">props جزیره (به JSON سریال می‌شود)</param>
/// <param name="Skeleton">ارتفاع اسکلت بارگذاری جزیره، یا «off»</param>
public sealed record ShipmentTabView(
    string? Partial = null,
    object? Model = null,
    string? Island = null,
    object? Props = null,
    string Skeleton = "24rem")
{
    /// <summary>یک partial.</summary>
    public static ShipmentTabView ForPartial(string partial, object? model) => new(Partial: partial, Model: model);

    /// <summary>یک جزیره.</summary>
    public static ShipmentTabView ForIsland(string island, object props, string skeleton = "24rem")
        => new(Island: island, Props: props, Skeleton: skeleton);
}

/// <summary>
/// کلید زبانه‌ها (همان <c>?tab=</c>). خانه با <see cref="Bales"/> و <see cref="Output"/> پیوند می‌دهد، پس این دو عوض
/// نمی‌شوند.
/// </summary>
public static class ShipmentTabKeys
{
    /// <summary>«نگله‌ها» (فاز ۴b)</summary>
    public const string Bales = "bales";

    /// <summary>«هزینه‌ها» (فاز ۵، Business)</summary>
    public const string Costs = "costs";

    /// <summary>«حساب محموله» (فاز ۵، Business)</summary>
    public const string Summary = "summary";

    /// <summary>«اکسل و چاپ» (فاز ۶)</summary>
    public const string Output = "output";
}

/// <summary>ترتیب زبانه‌ها مطابق H.1: نگله‌ها · هزینه‌ها · حساب محموله · اکسل و چاپ. فاصله‌ها برای زبانه‌های بعدی است.</summary>
public static class ShipmentTabOrder
{
    public const int Bales = 10;
    public const int Costs = 20;
    public const int Summary = 30;
    public const int Output = 40;
}
