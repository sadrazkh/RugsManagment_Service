using System.Globalization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Web.Navigation;

namespace RugsManagment.Web.Home.Cards;

/// <summary>کاری که یک کاشی خانه با لمس انجام می‌دهد.</summary>
public enum HomeTileAction
{
    /// <summary>فقط متن (مثلاً «هنوز محمولهٔ بازی نیست»)؛ دکمه نیست.</summary>
    None = 0,

    /// <summary>رفتن به یک صفحه.</summary>
    Link = 1,

    /// <summary>باز کردن یک پنل (<c>data-open-panel</c>).</summary>
    Panel = 2,

    /// <summary>یک لمس، یک ساختن: POST به API و رفتن به نتیجه (<c>data-post-json</c>).</summary>
    Post = 3,

    /// <summary>چند مقصد: برگهٔ انتخاب با دکمه‌های بزرگ (<c>data-sheet-open</c>).</summary>
    Choose = 4
}

/// <summary>یک دکمهٔ برگهٔ انتخاب.</summary>
/// <param name="Label">«محمولهٔ ۸ (۱۴۰۵/۰۶/۱۵) — ۱۲ نگله»</param>
/// <param name="Href">مقصد</param>
/// <param name="IsLatest">تازه‌ترین، که هنگام باز شدن برگه فوکوس می‌گیرد</param>
public sealed record HomeTileChoice(string Label, string Href, bool IsLatest);

/// <summary>یک کاشی بزرگ خانه.</summary>
public sealed record HomeTile(
    string Key,
    string Title,
    string Icon,
    string? Subtitle,
    HomeTileAction Action,
    string? Href = null,
    string? PanelName = null,
    object? PanelProps = null,
    string? PostUrl = null,
    object? PostBody = null,
    string? PostRedirect = null,
    string? ActionLabel = null,
    IReadOnlyList<HomeTileChoice>? Choices = null,
    string? ChoicesTitle = null);

/// <summary>مدل partial کاشی‌ها.</summary>
public sealed record HomeTilesModel(IReadOnlyList<HomeTile> Tiles);

/// <summary>
/// چهار کاشی بزرگ خانه (A.4): «ثبت فرش تازه»، «نگله‌بندی»، «ثبت هزینه» (Business) و «اکسل و چاپ».
///
/// محمولهٔ هدف: از <see cref="HomeSummaryDto.OpenShipments"/> (تازه‌ترین فعالیت اول).
/// <list type="bullet">
/// <item>صفر محمولهٔ باز: بازرگان «محمولهٔ تازه» را با یک لمس می‌سازد؛ انباردار می‌خواند «هنوز محمولهٔ بازی
/// نیست؛ از بازرگان بخواهید یکی بسازد.»</item>
/// <item>یک محموله: کاشی مستقیم همان را باز می‌کند و زیر برچسبش «محمولهٔ ۸ · ۱۲ نگله · ۱۸۵ متر» است.</item>
/// <item>دو یا بیشتر: برگه‌ای از دکمه‌های ۵۶ پیکسلی، تازه‌ترین فوکوس‌شده.</item>
/// </list>
///
/// هر مقصدِ فاز بعدی (صفحهٔ ورود فرش، محموله‌ها، پنل هزینه) با وجودِ کنترلرش سنجیده می‌شود نه با شمارهٔ
/// فاز: تا وقتی نیست کاشی یا به جایگزین موجود می‌رود (فرم تک‌فرش) یا فقط وضعیت را می‌گوید، و روزی که آمد
/// بی‌ویرایش این فایل فعال می‌شود. قرارداد نشانی‌ها در <see cref="HomeRoutes"/> است.
/// </summary>
public sealed class TilesCard(IActionDescriptorCollectionProvider actions, IHttpContextAccessor httpContextAccessor)
    : IHomeCard
{
    public string Key => "tiles";

    public int Order => HomeCardOrder.Tiles;

    public async Task<HomeCardView?> BuildAsync(HomeCardContext context, CancellationToken cancellationToken)
    {
        var http = httpContextAccessor.HttpContext;
        var terms = http is null ? BaleTerms.Negleh : await AppShell.TermsAsync(http);
        var features = HomeRoutes.Detect(actions);
        var open = context.Summary.OpenShipments;
        var tiles = new List<HomeTile>
        {
            new("intake", "ثبت فرش تازه", "plus", "فرش‌هایی که از فروشنده رسید", HomeTileAction.Link,
                Href: features.Intake ? HomeRoutes.Intake : HomeRoutes.LegacyRugCreate),
            ShipmentTile("baling", $"{terms.Bale}‌بندی", "bale", HomeRoutes.BalesTab, context, features, terms, open,
                allowCreate: true)
        };

        if (context.CanBusiness && features.Costs)
        {
            // «برای» از خانه: محمولهٔ هدف؛ بدون محموله خودِ پنل «ورودی امروز» یا خالی را می‌گذارد (A.4)
            object preset = open.Count > 0
                ? new { scope = HomeRoutes.CostScopeShipment, shipmentId = open[0].Id }
                : new { };
            tiles.Add(new HomeTile("costs", "ثبت هزینه", "coins",
                open.Count > 0 ? $"برای {ShipmentName(open[0])}" : "رسید کرایه، شست‌وشو و هزینه‌های دیگر",
                HomeTileAction.Panel, PanelName: HomeRoutes.CostPanel, PanelProps: preset));
        }

        tiles.Add(ShipmentTile("output", "اکسل و چاپ", "printer", HomeRoutes.OutputTab, context, features, terms, open,
            allowCreate: false));

        return new HomeCardView("~/Views/Shared/Components/HomeCards/Tiles.cshtml", new HomeTilesModel(tiles), Wide: true);
    }

    private static HomeTile ShipmentTile(
        string key,
        string title,
        string icon,
        string tab,
        HomeCardContext context,
        HomeFeatures features,
        BaleTermWords terms,
        IReadOnlyList<HomeShipmentDto> open,
        bool allowCreate)
    {
        if (open.Count == 0)
        {
            if (!context.CanBusiness)
                return new HomeTile(key, title, icon, "هنوز محمولهٔ بازی نیست؛ از بازرگان بخواهید یکی بسازد.",
                    HomeTileAction.None);

            if (allowCreate && features.Shipments)
            {
                // یک لمس، بی‌دیالوگ (C.1): تاریخ امروزِ ایران، بقیه از ترجیحات کارگاه روی سرور
                return new HomeTile(key, title, icon, "هنوز محمولهٔ بازی نیست", HomeTileAction.Post,
                    PostUrl: HomeRoutes.CreateShipmentApi,
                    PostBody: new { documentDate = AppShell.IranToday().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                    PostRedirect: HomeRoutes.ShipmentTab("{id}", tab),
                    ActionLabel: "محمولهٔ تازه");
            }

            return new HomeTile(key, title, icon, "هنوز محمولهٔ بازی نیست", HomeTileAction.None);
        }

        var latest = open[0];
        var subtitle = $"{ShipmentName(latest)} · {Messages.FormatNumber(latest.Bales)} {terms.Bale} · {Messages.FormatNumber(latest.AreaM2)} متر";

        if (open.Count == 1)
            return new HomeTile(key, title, icon, subtitle, HomeTileAction.Link,
                Href: HomeRoutes.ShipmentTab(latest.Id.ToString(), tab));

        var choices = open
            .Select((s, i) => new HomeTileChoice(
                $"{ShipmentName(s)} ({AppShell.JalaliPersian(s.DocumentDate)}) — {Messages.FormatNumber(s.Bales)} {terms.Bale}",
                HomeRoutes.ShipmentTab(s.Id.ToString(), tab),
                IsLatest: i == 0))
            .ToList();

        return new HomeTile(key, title, icon,
            $"{subtitle} · {Messages.FormatNumber(open.Count)} محمولهٔ باز",
            HomeTileAction.Choose,
            Choices: choices,
            ChoicesTitle: $"{title}: کدام محموله؟");
    }

    private static string ShipmentName(HomeShipmentDto shipment) => $"محمولهٔ {Messages.FormatNumber(shipment.Number)}";
}

/// <summary>کدام صفحه‌های فازهای بعد همین حالا در برنامه هستند.</summary>
/// <param name="Intake">صفحهٔ ورود فرش (جدول)</param>
/// <param name="Shipments">محموله‌ها (صفحه و API ساختن)</param>
/// <param name="Costs">هزینه‌ها (صفحه و پنل ثبت هزینه)</param>
/// <param name="Import">ورود از اکسل</param>
public sealed record HomeFeatures(bool Intake, bool Shipments, bool Costs, bool Import);

/// <summary>
/// قرارداد نشانی‌هایی که خانه به فازهای بعد می‌دهد. فاز محموله زبانهٔ کارگاه محموله را با
/// <c>?tab=bales</c> و <c>?tab=output</c> انتخاب می‌کند و <c>POST /api/shipments</c> پاسخی با <c>id</c>
/// برمی‌گرداند؛ فاز هزینه پنلی به نام <c>cost-entry</c> با props <c>{scope, shipmentId?}</c> دارد.
/// </summary>
public static class HomeRoutes
{
    public const string Intake = "/Intake";
    public const string LegacyRugCreate = "/Rugs/Create";
    public const string Import = "/Import";
    public const string CreateShipmentApi = "/api/shipments";
    public const string BalesTab = "bales";
    public const string OutputTab = "output";
    public const string CostPanel = "cost-entry";

    /// <summary><c>CostScope.Shipment</c> (B.14) — enum فاز هزینه هنوز در کد نیست.</summary>
    public const int CostScopeShipment = 2;

    /// <summary>زبانهٔ یک محموله؛ <paramref name="id"/> می‌تواند «{id}» باشد تا site.js پس از ساختن پرش کند.</summary>
    public static string ShipmentTab(string id, string tab) => $"/Shipments/{id}?tab={tab}";

    /// <summary>کدام کنترلرهای فازهای بعد همین حالا در برنامه هستند.</summary>
    public static HomeFeatures Detect(IActionDescriptorCollectionProvider actions)
        => new(
            Intake: AppShell.HasController(actions, ShellFeatures.IntakeController),
            Shipments: AppShell.HasController(actions, ShellFeatures.ShipmentsController),
            Costs: AppShell.HasController(actions, ShellFeatures.CostsController),
            Import: AppShell.HasController(actions, ShellFeatures.ImportController));
}
