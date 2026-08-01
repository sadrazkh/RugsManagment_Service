using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Sales;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Support;

namespace RugsManagment.Web.Controllers;

/// <summary>گزارش فروش و سود واقعی — اطلاعات مالی کارگاه، فقط مدیر.</summary>
[Authorize(Roles = nameof(UserRole.TenantAdmin))]
public class SalesController(IRugSaleService sales) : Controller
{
    public IActionResult Index() => View();

    /// <summary>
    /// خروجی CSV گزارش فروش — با BOM و اعلام جداکننده تا اکسل فارسی درست بازش کند.
    /// </summary>
    public async Task<IActionResult> ExportCsv(
        DateTimeOffset? from, DateTimeOffset? to, bool onlyOutstanding, CancellationToken ct)
    {
        var report = await sales.GetReportAsync(User.RequireTenantId(), BuildQuery(from, to, onlyOutstanding), ct);

        var csv = new CsvWriter();
        csv.AddRow("کد فرش", "عنوان", "خریدار", "تلفن خریدار", "تاریخ فروش",
                   "مبلغ فروش", "تخفیف", "فروش خالص", "دریافتی", "باقی‌مانده",
                   "نحوهٔ پرداخت", "سرمایه‌گذاری", "سود واقعی", "شمارهٔ فاکتور", "توضیح");

        foreach (var s in report.Sales)
        {
            csv.AddRow(s.RugSku, s.RugTitle, s.BuyerName, s.BuyerPhone, s.SoldAt,
                       s.SalePrice, s.Discount, s.NetAmount, s.ReceivedAmount, s.OutstandingAmount,
                       DisplayHelpers.PaymentMethod(s.PaymentMethod), s.TotalInvestment, s.ActualProfit,
                       s.Reference, s.Notes);
        }

        // سطر جمع، تا کاربر بدون فرمول‌نویسی هم اعداد کل را ببیند
        var sum = report.Summary;
        csv.AddRow("جمع", null, null, null, null,
                   sum.GrossTotal, sum.DiscountTotal, sum.NetTotal, sum.ReceivedTotal, sum.OutstandingTotal,
                   null, sum.InvestmentTotal, sum.ProfitTotal, null, null);

        var fileName = $"sales-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
        return File(csv.ToBytes(), "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// نسخهٔ چاپی گزارش. برای PDF کاربر از «چاپ ← ذخیره به‌صورت PDF» مرورگر
    /// استفاده می‌کند — بدون وابستگی به کتابخانهٔ تولید PDF.
    /// </summary>
    public async Task<IActionResult> Print(
        DateTimeOffset? from, DateTimeOffset? to, bool onlyOutstanding, CancellationToken ct)
    {
        var report = await sales.GetReportAsync(User.RequireTenantId(), BuildQuery(from, to, onlyOutstanding), ct);

        ViewData["From"] = from;
        ViewData["To"] = to;
        ViewData["TenantName"] = User.GetTenantName();
        return View(report);
    }

    /// <summary>
    /// تاریخ‌های ورودی «روز» هستند؛ به ابتدا و انتهای همان روز به وقت ایران تبدیل می‌شوند
    /// تا «تا تاریخ» شامل خود آن روز باشد.
    /// </summary>
    private static SalesQuery BuildQuery(DateTimeOffset? from, DateTimeOffset? to, bool onlyOutstanding) => new()
    {
        From = PersianFormat.IranDayStartUtc(from),
        To = PersianFormat.IranDayEndUtc(to),
        OnlyOutstanding = onlyOutstanding
    };
}
