using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Application.Features.Intake.Grid;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// جدول‌ها (spec G.1، G.6، I.2): داده‌های هر جدول (<c>GET /api/grid/{gridId}</c>، با منبعی که شناسه‌اش همین است؛
/// ناشناخته ۴۰۴)، ذخیرهٔ دسته‌ای ردیف‌های فرش (<c>POST /api/grid/rugs/batch</c>) و «برگردان» فرشی که از جدول حذف شد
/// (<c>POST /api/grid/rugs/{rugId}/restore</c>، E6).
///
/// همه Floor‌اند (کار روزانهٔ انبار). پول را سرویس نگه می‌دارد: ستون و خانهٔ پولی به غیر Business نمی‌رسد و کلید پولی از او
/// کل درخواست را ۴۰۳ می‌کند؛ حذف و برگرداندن قاعدهٔ «فرش خودش در ۲۴ ساعت، بی مبلغ» را دارند.
/// </summary>
[Route("api/grid")]
public class GridApiController(IRugGridService grids) : ApiControllerBase
{
    /// <summary>
    /// داده‌های یک جدول. <c>intake</c>: <c>?date=yyyy-MM-dd</c> (روز ایران؛ پیش‌فرض امروز) و اختیاری <c>batchId</c>.
    /// <c>rugs</c>: همان پارامترهای فهرست فرش‌ها (<see cref="RugQuery"/>) و <c>skip</c>/<c>take</c>.
    /// </summary>
    [HttpGet("{gridId}")]
    public async Task<IActionResult> Get(
        string gridId,
        [FromQuery] DateOnly? date,
        [FromQuery] Guid? batchId,
        [FromQuery] Guid? shipmentId,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        [FromQuery] RugQuery query,
        CancellationToken ct)
        => Ok(await grids.LoadAsync(User.RequireTenantId(), gridId,
            new GridLoadRequest(date, batchId, shipmentId, skip, take, query), ct));

    /// <summary>ذخیرهٔ دسته‌ای (G.6) → <c>{results, totals, nextSerial}</c>.</summary>
    [HttpPost("rugs/batch")]
    public async Task<IActionResult> Batch([FromBody] GridBatchRequest request, CancellationToken ct)
        => Ok(await grids.SaveAsync(User.RequireTenantId(), request, ct));

    /// <summary>
    /// «حذف شد [برگردان]»: فرش از سطل زباله برمی‌گردد → ردیف جدول (<c>?gridId=</c> جدولی که ردیف به آن برمی‌گردد).
    /// شمارهٔ فرش که حالا مال فرش دیگری است → ۴۰۹.
    /// </summary>
    [HttpPost("rugs/{rugId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid rugId, [FromQuery] string? gridId, CancellationToken ct)
        => Ok(await grids.RestoreAsync(User.RequireTenantId(), rugId, gridId, ct));
}
