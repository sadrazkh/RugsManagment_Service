using Microsoft.AspNetCore.Mvc.Infrastructure;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Platform.DTOs;

namespace RugsManagment.Web.Home.Cards;

/// <summary>یک تراشهٔ آمادگی روی خانه.</summary>
/// <param name="Code">کد (E_NO_FINAL_SIZE، …)</param>
/// <param name="Message">پیام فارسی کامل از <c>Messages.fa</c></param>
/// <param name="Blocking">جلوی «قطعی کن» را می‌گیرد (نماد خطا) یا فقط هشدار است (نماد هشدار)</param>
public sealed record HomeReadinessChip(string Code, string Message, bool Blocking);

/// <summary>مدل کارت «آمادگی».</summary>
/// <param name="ShipmentName">«محمولهٔ ۸»</param>
/// <param name="FixHref">زبانهٔ نگله‌های همان محموله (جای درست کردن)</param>
/// <param name="Chips">تراشه‌ها، بازدارنده‌ها اول</param>
/// <param name="Ready">چیزی جلوی «قطعی کن» را نگرفته</param>
public sealed record HomeReadinessModel(string ShipmentName, string FixHref, IReadOnlyList<HomeReadinessChip> Chips, bool Ready);

/// <summary>
/// کارت «آمادگی» خانه (فاز ۴b، A.4): تراشه‌های آمادگیِ محمولهٔ هدف (اولین محمولهٔ باز،
/// <see cref="HomeSummaryDto.Readiness"/>) — «اندازهٔ نهایی ۱۲ فرش ثبت نشده»، «ارزش گمرکی ۳ فرش «کاشان» معلوم نیست»، … — هر کدام
/// با نماد و متن (هرگز فقط رنگ، H.7) و پیوند به زبانهٔ نگله‌ها که همان‌جا درست می‌شوند. وقتی چیزی جلوی «قطعی کن» را نگرفته
/// همان «آمادهٔ قطعی شدن» فهرست محموله‌ها را می‌گوید.
///
/// بی محمولهٔ باز کارتی نیست (کاشی «نگله‌بندی» همان را می‌گوید). پیام‌ها از سرور آماده می‌آیند (<c>Messages.fa</c>، واژهٔ
/// نگله/عدل کارگاه در خودشان)؛ بی‌پول، برای همه.
/// </summary>
public sealed class ReadinessCard(IActionDescriptorCollectionProvider actions) : IHomeCard
{
    public string Key => "readiness";

    public int Order => HomeCardOrder.Readiness;

    public Task<HomeCardView?> BuildAsync(HomeCardContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var open = context.Summary.OpenShipments;
        if (open.Count == 0 || !HomeRoutes.Detect(actions).Shipments)
            return Task.FromResult<HomeCardView?>(null);

        var target = open[0];
        var chips = context.Summary.Readiness
            .OrderByDescending(r => r.Blocking)
            .Select(r => new HomeReadinessChip(r.Code, r.Message, r.Blocking))
            .ToList();
        var ready = target.Lines > 0 && !chips.Any(c => c.Blocking);

        var model = new HomeReadinessModel(
            $"محمولهٔ {Messages.FormatNumber(target.Number)}",
            HomeRoutes.ShipmentTab(target.Id.ToString(), HomeRoutes.BalesTab),
            chips,
            ready);

        return Task.FromResult<HomeCardView?>(
            new HomeCardView("~/Views/Shared/Components/HomeCards/Readiness.cshtml", model));
    }
}
