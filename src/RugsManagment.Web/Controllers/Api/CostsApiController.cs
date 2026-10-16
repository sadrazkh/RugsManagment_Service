using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Application.Features.Costs;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// API دفتر «هزینه‌ها» (spec I.2، D.2، D.3، H.2): فهرست، ثبت، ویرایش، حذف نرم و «برگردان»، پیش‌نمایش تقسیم و تبدیل
/// «فرش‌های دیگر…» به فهرست صریح فرش.
///
/// کل کلاس Business است: هر عددِ این دفتر پول تومانی است و انباردار هیچ مبلغی نمی‌بیند (A.5)؛ پس سرویس‌ها پنهان‌سازی ندارند.
///
/// چند قاعده که پنل به آن‌ها تکیه دارد:
/// <list type="bullet">
/// <item><c>POST /api/costs</c> با همان <c>clientKey</c> همان ثبت را برمی‌گرداند؛ «دوباره بفرست» پس از قطع اینترنت ثبت دوم
/// نمی‌سازد.</item>
/// <item>سهم‌ها در همان درخواست (پاس سازگاری) نوشته می‌شوند، پس پاسخ نتیجهٔ تقسیم و جملهٔ پنل را دارد.</item>
/// <item>ویرایش و «به‌روزرسانی مبلغ» نسخه می‌خواهند؛ نسخهٔ کهنه ۴۰۹. حذف نسخهٔ اختیاری دارد (<c>?version=</c>).</item>
/// <item>شناسهٔ کارگاه دیگر درست مثل شناسهٔ ناموجود ۴۰۴ است (<see cref="KeyNotFoundException"/>)؛ مقدار نادرست ۴۰۰ با پیام فارسی
/// هر خانه (<c>E_PINS_EXCEED</c>، <c>E_FX_REQUIRED</c>، …).</item>
/// </list>
/// </summary>
[Route("api/costs")]
[Authorize(Policy = Policies.Business)]
public sealed class CostsApiController(ICostEntryService costs, ITargetResolver targets) : ApiControllerBase
{
    /// <summary>
    /// فهرست صفحه‌بندی‌شده: <c>?shipmentId&amp;batchId&amp;categoryId&amp;state&amp;notFullyAllocated&amp;rugId&amp;from&amp;to&amp;page&amp;pageSize</c>.
    /// «تقسیم‌نشده» = <c>notFullyAllocated=true</c>؛ <c>from</c>/<c>to</c> روز ایران (<c>yyyy-MM-dd</c>).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CostEntryQuery query, CancellationToken ct)
        => Ok(await costs.ListAsync(User.RequireTenantId(), query.Sanitized(), ct));

    /// <summary>یک ثبت با درخواست ذخیره‌شده (برای پر کردن دوبارهٔ پنل) و نتیجهٔ تقسیم.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await costs.GetAsync(User.RequireTenantId(), id, ct));

    /// <summary>ثبت تازه (تکرارناپذیر با <c>clientKey</c>).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCostEntryRequest request, CancellationToken ct)
        => Ok(await costs.CreateAsync(User.RequireTenantId(), request, ct));

    /// <summary>ویرایش با <c>version</c>.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCostEntryRequest request, CancellationToken ct)
        => Ok(await costs.UpdateAsync(User.RequireTenantId(), id, request, ct));

    /// <summary>حذف نرم: «هزینهٔ شمارهٔ ۱۲ حذف شد [برگردان]». پاسخ همان ثبت (حذف‌شده) است تا toast شماره‌اش را بداند.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] uint? version, CancellationToken ct)
        => Ok(await costs.DeleteAsync(User.RequireTenantId(), id, version, ct));

    /// <summary>«برگردان» یک ثبت حذف‌شده؛ سهم‌ها در همان درخواست دوباره حساب می‌شوند.</summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
        => Ok(await costs.RestoreAsync(User.RequireTenantId(), id, ct));

    /// <summary>پیش‌نمایش تقسیم بی ذخیره (جملهٔ پنل، سهم هر فرش، هشدارها).</summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] SaveCostEntryRequest request, CancellationToken ct)
        => Ok(await costs.PreviewAsync(User.RequireTenantId(), request, ct));

    /// <summary>
    /// «فرش‌های دیگر…»: <c>{serialsText?, filter?, inStock?}</c> → <c>{rugIds, lines, pieces, areaM2, unresolved, scopeNote}</c>.
    /// فهرست همین حالا صریح می‌شود؛ هیچ‌وقت فیلتر زنده ذخیره نمی‌شود (D.2).
    /// </summary>
    [HttpPost("resolve-targets")]
    public async Task<IActionResult> ResolveTargets([FromBody] ResolveTargetsRequest request, CancellationToken ct)
        => Ok(await targets.ResolveAsync(User.RequireTenantId(), request, ct));

    /// <summary>«به‌روزرسانی مبلغ» رسید ثابت‌شده با اندازه‌های امروز (هشدار <c>W_RATE_DRIFT</c>، D.3)؛ <c>{version}</c>.</summary>
    [HttpPost("{id:guid}/refreeze")]
    public async Task<IActionResult> Refreeze(Guid id, [FromBody] CostVersionRequest request, CancellationToken ct)
        => Ok(await costs.RefreezeAsync(User.RequireTenantId(), id, request.Version, ct));

    /// <summary>«فرش‌های این ورودی را دوباره بگیر» برای خرید یکجای یک ورودی (D.2)؛ <c>{version}</c>.</summary>
    [HttpPost("{id:guid}/resnapshot")]
    public async Task<IActionResult> Resnapshot(Guid id, [FromBody] CostVersionRequest request, CancellationToken ct)
        => Ok(await costs.ResnapshotAsync(User.RequireTenantId(), id, request.Version, ct));
}

/// <summary>بدنهٔ کارهای یک‌نسخه‌ای روی یک ثبت («به‌روزرسانی مبلغ»، «دوباره بگیر»).</summary>
/// <param name="Version">نسخهٔ ثبت (xmin)</param>
public sealed record CostVersionRequest(uint Version);
