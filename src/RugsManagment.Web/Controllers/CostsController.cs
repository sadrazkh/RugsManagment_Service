using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// صفحهٔ «هزینه‌ها» (<c>/Costs</c>، Business؛ H.1، I.4): دفتر همهٔ ثبت‌های هزینه با فیلتر «تقسیم‌نشده» و پنل «ثبت هزینه».
/// خودِ فهرست و پنل جزیره‌اند (<c>cost-ledger</c>، <c>cost-entry</c>) و از <c>/api/costs</c> می‌خوانند؛ این اکشن فقط قاب و
/// فیلترهای نشانی را می‌دهد.
///
/// نشانی‌ها: <c>/Costs?shipmentId=…</c>، <c>?batchId=…</c>، <c>?categoryId=…</c> فهرست را از همان اول محدود می‌کنند و
/// <c>?unallocated=1</c> فقط «تقسیم‌نشده»‌ها را نشان می‌دهد و <c>?new=1</c> پنل ثبت را همان لحظه باز می‌کند (فرمان Ctrl+K «ثبت هزینه»). انباردار (Floor) پول تومانی نمی‌بیند، پس این
/// صفحه برایش ۴۰۳ است و منو هم آن را برایش نشان نمی‌دهد (A.5).
/// </summary>
[Authorize(Policy = Policies.Business)]
[Route("Costs")]
public sealed class CostsController : Controller
{
    [HttpGet("")]
    public IActionResult Index(
        [FromQuery] Guid? shipmentId,
        [FromQuery] Guid? batchId,
        [FromQuery] Guid? categoryId,
        [FromQuery(Name = "new")] string? openNew = null,
        [FromQuery] string? unallocated = null)
        => View("~/Views/Costs/Index.cshtml",
            new CostsPageModel(shipmentId, batchId, categoryId, Flag(openNew), Flag(unallocated)));

    /// <summary>«1»، «true» یا «on» یعنی روشن (پیوندهای منو و خانه <c>?new=1</c> می‌نویسند).</summary>
    private static bool Flag(string? value)
        => value is not null && (value == "1"
                                 || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                                 || value.Equals("on", StringComparison.OrdinalIgnoreCase));
}

/// <summary>مدل قاب صفحهٔ «هزینه‌ها» (props جزیرهٔ <c>cost-ledger</c>).</summary>
/// <param name="ShipmentId">فقط هزینه‌های این محموله</param>
/// <param name="BatchId">فقط هزینه‌های این ورودی</param>
/// <param name="CategoryId">فقط این نوع</param>
/// <param name="OpenNew">پنل ثبت تازه با باز شدن صفحه باز شود</param>
/// <param name="UnallocatedOnly">فقط «تقسیم‌نشده»‌ها (پیوند کارت پول خانه)</param>
public sealed record CostsPageModel(Guid? ShipmentId, Guid? BatchId, Guid? CategoryId, bool OpenNew, bool UnallocatedOnly = false)
{
    /// <summary>props جزیره به شکل I.5: <c>{ filters: { shipmentId?, batchId?, categoryId? }, openNew }</c>.</summary>
    public object IslandProps => new
    {
        filters = new { shipmentId = ShipmentId, batchId = BatchId, categoryId = CategoryId },
        openNew = OpenNew,
        unallocatedOnly = UnallocatedOnly
    };
}
