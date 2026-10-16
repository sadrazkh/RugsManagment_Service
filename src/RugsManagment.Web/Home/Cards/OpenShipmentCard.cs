using Microsoft.AspNetCore.Mvc.Infrastructure;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Web.Navigation;

namespace RugsManagment.Web.Home.Cards;

/// <summary>یک محمولهٔ باز در کارت خانه.</summary>
/// <param name="Id">شناسه</param>
/// <param name="Name">«محمولهٔ ۸»</param>
/// <param name="Date">تاریخ اظهار با ارقام فارسی («۱۴۰۵/۰۶/۱۵»)</param>
/// <param name="Bales">تعداد نگله‌ها</param>
/// <param name="Pieces">تخته‌ها (Σ تعداد)</param>
/// <param name="Lines">ردیف‌ها (برای «۵۲ تخته (۳ ردیف)»)</param>
/// <param name="AreaM2">متراژ</param>
/// <param name="UsdTotal">ارزش گمرکی (دلار؛ پول داخلی نیست، برای همه)</param>
/// <param name="ReadinessBlocking">کمبودهای بازدارنده</param>
/// <param name="BalesHref">زبانهٔ نگله‌های همین محموله</param>
public sealed record HomeOpenShipment(
    Guid Id,
    string Name,
    string Date,
    int Bales,
    int Pieces,
    int Lines,
    decimal AreaM2,
    decimal UsdTotal,
    int ReadinessBlocking,
    string BalesHref);

/// <summary>مدل کارت «محموله‌های باز».</summary>
/// <param name="Target">محمولهٔ هدف (تازه‌ترین فعالیت نگله‌ها، A.4)</param>
/// <param name="Others">بقیهٔ محموله‌های باز، به همان ترتیب</param>
/// <param name="Choices">دکمه‌های برگهٔ انتخاب وقتی دو محمولهٔ باز یا بیشتر هست؛ null با یک محموله</param>
/// <param name="Terms">واژه‌های نگله/عدل کارگاه</param>
public sealed record HomeOpenShipmentsModel(
    HomeOpenShipment Target,
    IReadOnlyList<HomeOpenShipment> Others,
    IReadOnlyList<HomeTileChoice>? Choices,
    BaleTermWords Terms);

/// <summary>
/// کارت «محموله‌های باز» خانه (فاز ۴b، A.4): محمولهٔ هدف با تخته، متر، دلار و نگله‌ها، و پیوند مستقیم به نگله‌بندی‌اش.
///
/// <para><b>محمولهٔ هدف</b> همان قاعدهٔ کاشی «نگله‌بندی» است (<see cref="TilesCard"/>): اولین محمولهٔ
/// <see cref="HomeSummaryDto.OpenShipments"/>، یعنی تازه‌ترین فعالیت نگله‌ها. با یک محمولهٔ باز همان باز می‌شود؛ با دو یا
/// بیشتر، بقیه زیرش فهرست می‌شوند و دکمهٔ «محمولهٔ دیگر» همان برگهٔ انتخاب ۵۶ پیکسلی کاشی‌ها را باز می‌کند (تازه‌ترین
/// فوکوس‌شده؛ <c>data-sheet-open</c> در site.js، بی اسکریپت درون‌خطی).</para>
///
/// <para>بی محمولهٔ باز کارتی نیست: کاشی «نگله‌بندی» همان‌جا «محمولهٔ تازه» (بازرگان) یا «هنوز محمولهٔ بازی نیست» (انباردار)
/// را می‌گوید و کارت خالی فقط تکرارش بود. هیچ مبلغ تومانی ندارد، پس برای همهٔ نقش‌ها یکی است.</para>
/// </summary>
public sealed class OpenShipmentCard(IActionDescriptorCollectionProvider actions, IHttpContextAccessor httpContextAccessor)
    : IHomeCard
{
    public string Key => "open-shipments";

    public int Order => HomeCardOrder.OpenShipments;

    public async Task<HomeCardView?> BuildAsync(HomeCardContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var open = context.Summary.OpenShipments;
        if (open.Count == 0 || !HomeRoutes.Detect(actions).Shipments)
            return null;

        var http = httpContextAccessor.HttpContext;
        var terms = http is null ? BaleTerms.Negleh : await AppShell.TermsAsync(http);
        var items = open.Select(ToItem).ToList();

        IReadOnlyList<HomeTileChoice>? choices = null;
        if (items.Count > 1)
        {
            choices = open
                .Select((s, i) => new HomeTileChoice(ChoiceLabel(s, terms), items[i].BalesHref, IsLatest: i == 0))
                .ToList();
        }

        return new HomeCardView("~/Views/Shared/Components/HomeCards/OpenShipments.cshtml",
            new HomeOpenShipmentsModel(items[0], items.Skip(1).ToList(), choices, terms));
    }

    /// <summary>متن دکمهٔ برگهٔ انتخاب، همان شکل کاشی‌ها: «محمولهٔ ۸ (۱۴۰۵/۰۶/۱۵) — ۱۲ نگله».</summary>
    public static string ChoiceLabel(HomeShipmentDto shipment, BaleTermWords terms)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        ArgumentNullException.ThrowIfNull(terms);
        return $"{Name(shipment)} ({AppShell.JalaliPersian(shipment.DocumentDate)}) — {Messages.FormatNumber(shipment.Bales)} {terms.Bale}";
    }

    private static HomeOpenShipment ToItem(HomeShipmentDto s)
        => new(s.Id, Name(s), AppShell.JalaliPersian(s.DocumentDate), s.Bales, s.Pieces, s.Lines, s.AreaM2, s.UsdTotal,
            s.ReadinessBlocking, HomeRoutes.ShipmentTab(s.Id.ToString(), HomeRoutes.BalesTab));

    private static string Name(HomeShipmentDto shipment) => $"محمولهٔ {Messages.FormatNumber(shipment.Number)}";
}
