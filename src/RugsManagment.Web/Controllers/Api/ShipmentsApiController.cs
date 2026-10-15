using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// API محموله (I.2): فهرست، سربرگ، آمادگی، «قطعی کن» و «باز کردن دوباره»، و کارهای ردیف (افزودن با شماره، انتقال، خارج
/// کردن، اندازهٔ نهایی، نرخ دستی، ترتیب). نگله‌ها در <see cref="BalesApiController"/> و جدول ارزش گمرکی در
/// <see cref="DeclaredValuesApiController"/>.
///
/// دسترسی (A.1، I.1): خواندن و کار روی نگله و ردیف Floor است (انباردار نگله‌بندی می‌کند و اندازهٔ نهایی می‌نویسد)؛ ساختن،
/// ویرایش و حذف محموله، «قطعی کن» و «باز کردن دوباره» Business. نرخ دستی یک ردیف هم Business است: سرور آن عمل را برای غیر
/// Business با پیام فارسی همان عمل (<c>E_LINE_RATE_FORBIDDEN</c>) رد می‌کند و بقیهٔ عمل‌های همان درخواست انجام می‌شوند.
/// محموله مبلغ تومانی ندارد، پس پاسخ‌ها برای همه یکی است.
///
/// خطاها از <see cref="ApiExceptionFilter"/>: محمولهٔ کارگاه دیگر ۴۰۴، نسخهٔ کهنه و قفل «قطعی» (<see cref="ShipmentLockedException"/>)
/// ۴۰۹ با پیام فارسی، مقدار نادرست ۴۰۰.
/// </summary>
[Route("api/shipments")]
public sealed class ShipmentsApiController(
    IShipmentService shipments,
    IReadinessService readiness,
    IFinalizeService finalizer,
    ILineService lines) : ApiControllerBase
{
    /// <summary>محموله‌های کارگاه، تازه‌ترین اول؛ <c>?status=0|1</c> (یا Open/Finalized) فقط همان وضعیت.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ShipmentStatus? status, CancellationToken ct)
        => Ok(await shipments.ListAsync(User.RequireTenantId(), status, ct));

    /// <summary>
    /// «محمولهٔ تازه» با یک لمس: بدنه اختیاری است (<c>{documentDate}</c> یا هیچ)؛ عنوان «اظهار {شماره}»، تاریخ امروزِ ایران و
    /// متراژ هدف و نرخ دلار از ترجیحات کارگاه. پاسخ <see cref="ShipmentDto"/> با <c>id</c>، که خانه و فهرست با آن به
    /// زبانهٔ نگله‌ها می‌روند.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Business)]
    public async Task<IActionResult> Create(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateShipmentRequest? request,
        CancellationToken ct)
        => Ok(await shipments.CreateAsync(User.RequireTenantId(), request ?? EmptyCreate, ct));

    /// <summary>محمولهٔ کامل: سربرگ، خلاصهٔ نگله‌ها، جمع‌ها و آمادگی.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await shipments.GetAsync(User.RequireTenantId(), id, ct));

    /// <summary>ویرایش سربرگ با <c>version</c>؛ روی محمولهٔ قطعی جز یادداشت و متراژ هدف ۴۰۹ (C.8).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Business)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShipmentRequest request, CancellationToken ct)
        => Ok(await shipments.UpdateAsync(User.RequireTenantId(), id, request, ct));

    /// <summary>
    /// حذف: فقط محمولهٔ باز و بی‌فرش (نگله‌های خالی با آن می‌روند)؛ <c>?version=</c> اختیاری و اگر هست باید تازه باشد.
    /// هزینه‌هایی که به آن وصل بودند «تقسیم‌نشده» می‌شوند و خروجی‌های گرفته‌شده می‌مانند (C.1).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Business)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] uint? version, CancellationToken ct)
    {
        await shipments.DeleteAsync(User.RequireTenantId(), id, version, ct);
        return NoContent();
    }

    /// <summary>تراشه‌های آمادگی (بازدارنده‌ها اول)؛ با <c>?templateId=</c> پیش‌پرواز همان قالب هم می‌آید.</summary>
    [HttpGet("{id:guid}/readiness")]
    public async Task<IActionResult> Readiness(Guid id, [FromQuery] Guid? templateId, CancellationToken ct)
        => Ok(await readiness.GetAsync(User.RequireTenantId(), id, templateId, ct));

    /// <summary>
    /// «قطعی کن» با <c>{version}</c> → <c>{ok, issues, shipment}</c>. با کمبود بازدارنده <c>ok</c> = false و فهرست کامل
    /// کمبودها، و هیچ چیز عوض نمی‌شود؛ وگرنه فرش‌ها «صادر شده» و محموله قفل.
    /// </summary>
    [HttpPost("{id:guid}/finalize")]
    [Authorize(Policy = Policies.Business)]
    public async Task<IActionResult> Finalize(Guid id, [FromBody] FinalizeShipmentRequest request, CancellationToken ct)
        => Ok(await finalizer.FinalizeAsync(User.RequireTenantId(), id, request, ct));

    /// <summary>«باز کردن دوباره» با <c>{version, reason?}</c> → محمولهٔ باز با ارزش‌های تازه از جدول.</summary>
    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = Policies.Business)]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenShipmentRequest request, CancellationToken ct)
        => Ok(await finalizer.ReopenAsync(User.RequireTenantId(), id, request, ct));

    /// <summary>
    /// چند کار روی ردیف‌ها در یک درخواست (<c>add</c>، <c>move</c>، <c>remove</c>، <c>measure</c>، <c>rate</c>، <c>order</c>)
    /// → نتیجهٔ هر کار، خلاصهٔ تازهٔ نگله‌ها و جمع محموله. «افزودن» با شمارهٔ نگله‌ای که نیست همان نگله را می‌سازد (C.2).
    /// </summary>
    [HttpPost("{id:guid}/lines/batch")]
    public async Task<IActionResult> LinesBatch(Guid id, [FromBody] LinesBatchRequest request, CancellationToken ct)
        => Ok(await lines.BatchAsync(User.RequireTenantId(), id, request, Policies.IsBusiness(User), ct));

    /// <summary>
    /// پیدا کردن فرش با شماره برای جعبهٔ «شماره را بزنید»: تطبیق دقیق یا «داخل دسته» و اینکه الان در کدام نگله است.
    /// پیدا نشد → ۴۰۴ با پیام فارسی.
    /// </summary>
    [HttpGet("{id:guid}/find-rug")]
    public async Task<IActionResult> FindRug(Guid id, [FromQuery] string? serial, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { message = Messages.LineRugRequired.Text });

        var hit = await lines.FindRugAsync(User.RequireTenantId(), id, serial.Trim(), ct);
        return hit is null
            ? NotFound(new { message = Messages.RugSerialNotFound.Format(("serial", serial.Trim())) })
            : Ok(hit);
    }

    /// <summary>«محمولهٔ تازه» بی بدنه: همه‌چیز پیش‌فرض (تاریخ خالی یعنی امروزِ ایران).</summary>
    private static readonly CreateShipmentRequest EmptyCreate = new(null, default, null, null, null, null, null, null, null);
}
