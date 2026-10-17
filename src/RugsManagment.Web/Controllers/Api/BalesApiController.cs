using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// API نگله‌ها (I.2) — همه Floor: انباردار هم نگله می‌سازد، شماره و وزنش را می‌نویسد، نگلهٔ خالی را حذف می‌کند و شماره‌ها را
/// پشت سر هم می‌کند (A.1). نگله مبلغ تومانی ندارد.
///
/// قاعده‌ها در سرویس و پاس سازگاری‌اند، نه این‌جا: نگلهٔ پُر حذف نمی‌شود (۴۰۹)، شمارهٔ تکراری ۴۰۹، روی محمولهٔ قطعی
/// ساختن، حذف، شماره‌گذاری دوباره و وزن ۴۰۹ با پیام فارسی قفل (C.8)، نسخهٔ کهنه ۴۰۹، و محموله یا نگلهٔ کارگاه دیگر ۴۰۴.
/// معمولاً نگله را اولین فرشش می‌سازد (عمل «افزودن» با شمارهٔ تازه در <c>lines/batch</c>، C.2)؛ ساختن دستی برای نگلهٔ
/// خالیِ از پیش است.
/// </summary>
[Route("api")]
public sealed class BalesApiController(IBaleService bales) : ApiControllerBase
{
    /// <summary>نگله‌های یک محموله با ردیف‌هایشان، به ترتیب شماره.</summary>
    [HttpGet("shipments/{shipmentId:guid}/bales")]
    public async Task<IActionResult> List(Guid shipmentId, CancellationToken ct)
        => Ok(await bales.ListAsync(User.RequireTenantId(), shipmentId, ct));

    /// <summary>نگلهٔ تازه: <c>{number?}</c>؛ بی بدنه یا بی شماره یعنی «بزرگ‌ترین + ۱».</summary>
    [HttpPost("shipments/{shipmentId:guid}/bales")]
    public async Task<IActionResult> Create(
        Guid shipmentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateBaleRequest? request,
        CancellationToken ct)
        => Ok(await bales.CreateAsync(User.RequireTenantId(), shipmentId, request ?? new CreateBaleRequest(null), ct));

    /// <summary>
    /// شماره‌گذاری دوباره به ۱..n به ترتیب فعلی، یکجا: <c>{version}</c> نسخهٔ محموله → همهٔ نگله‌ها با شمارهٔ تازه.
    /// </summary>
    [HttpPost("shipments/{shipmentId:guid}/bales/renumber")]
    public async Task<IActionResult> Renumber(Guid shipmentId, [FromBody] RenumberBalesRequest request, CancellationToken ct)
        => Ok(await bales.RenumberAsync(User.RequireTenantId(), shipmentId, request, ct));

    /// <summary>ویرایش نگله: <c>{number?, weightKg?, notes?, version}</c> → نگلهٔ تازه.</summary>
    [HttpPut("bales/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBaleRequest request, CancellationToken ct)
        => Ok(await bales.UpdateAsync(User.RequireTenantId(), id, request, ct));

    /// <summary>حذف نگلهٔ خالی: <c>?version=</c> لازم است (بی نسخه ۴۰۰)؛ نگلهٔ پُر ۴۰۹.</summary>
    [HttpDelete("bales/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery, BindRequired] uint version, CancellationToken ct)
    {
        await bales.DeleteAsync(User.RequireTenantId(), id, version, ct);
        return NoContent();
    }
}
