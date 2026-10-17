using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Batches;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// API «ورودی‌ها» (<c>RugBatch</c>: فرش‌های یک فروشنده در یک روز) — محدود به کارگاه کاربر، برای هر کاربر کارگاه.
/// واژهٔ «محموله» مال اظهار صادراتی است و این‌جا به کار نمی‌رود.
/// </summary>
[Route("api/batches")]
public class BatchesApiController(IRugBatchService batches) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await batches.ListAsync(User.RequireTenantId(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRugBatchRequest request, CancellationToken ct)
        => Ok(await batches.CreateAsync(User.RequireTenantId(), request, ct));

    /// <summary>
    /// «ورودی امروز» (B.5، H.1): ورودیِ (کارگاه، روز ایرانِ <c>receivedAt</c> یا امروز، فروشندهٔ یکسان‌شده) را پیدا می‌کند یا
    /// می‌سازد → ورودی با شماره، فروشنده، روز و جمع تخته‌ها.
    /// </summary>
    [HttpPost("today")]
    public async Task<IActionResult> Today([FromBody] TodayBatchRequest request, CancellationToken ct)
        => Ok(await batches.TodayAsync(User.RequireTenantId(), request, ct));

    /// <summary>
    /// نوار «ورودی‌های اخیر»: ورودی‌های روزدارِ <c>days</c> روز گذشته (پیش‌فرض ۷، امروز هم) که فرشی دارند، با جمع تخته‌ها.
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> Recent([FromQuery] int days = 7, CancellationToken ct = default)
        => Ok(await batches.RecentAsync(User.RequireTenantId(), days, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRugBatchRequest request, CancellationToken ct)
        => Ok(await batches.UpdateAsync(User.RequireTenantId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await batches.DeleteAsync(User.RequireTenantId(), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/rugs")]
    public async Task<IActionResult> AssignRugs(Guid id, [FromBody] BatchRugIdsRequest request, CancellationToken ct)
    {
        await batches.AssignRugsAsync(User.RequireTenantId(), id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/rugs")]
    public async Task<IActionResult> RemoveRugs(Guid id, [FromBody] BatchRugIdsRequest request, CancellationToken ct)
    {
        await batches.RemoveRugsAsync(User.RequireTenantId(), id, request, ct);
        return NoContent();
    }
}
