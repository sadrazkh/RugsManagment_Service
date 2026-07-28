using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>API فرش‌ها که جزیره‌های Vue مصرف می‌کنند — tenantId از کوکی کاربر (جداسازی کارگاه).</summary>
[Route("api/rugs")]
public class RugsApiController(IRugManagementService rugs) : ApiControllerBase
{
    /// <summary>
    /// فهرست صفحه‌بندی‌شده با جستجو، فیلتر و مرتب‌سازی — همان کوئریِ صفحهٔ فرش‌ها.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] RugQuery query, CancellationToken ct)
        => Ok(await rugs.SearchAsync(User.RequireTenantId(), query, ct));

    /// <summary>
    /// چند فرش کامل (با مراحل و متادیتا) برای پیش‌نمایش — مثلاً در طراح برچسب.
    /// عمداً محدود است تا هرگز کل انبار لود نشود.
    /// </summary>
    [HttpGet("samples")]
    public async Task<IActionResult> Samples([FromQuery] int count, CancellationToken ct)
    {
        var all = await rugs.ListAsync(User.RequireTenantId(), null, ct);
        return Ok(all.Take(Math.Clamp(count <= 0 ? 20 : count, 1, 50)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var rug = await rugs.GetAsync(User.RequireTenantId(), id, ct);
        return rug is null ? NotFound(new { message = "فرش یافت نشد." }) : Ok(rug);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRugRequest request, CancellationToken ct)
        => Ok(await rugs.CreateAsync(User.RequireTenantId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRugRequest request, CancellationToken ct)
        => Ok(await rugs.UpdateAsync(User.RequireTenantId(), id, request, ct));

    // ── حرکت مرحله و ثبت هزینه (استفاده در صفحهٔ جزئیات) ──

    [HttpPost("{rugId:guid}/steps/{stepId:guid}/advance")]
    public async Task<IActionResult> Advance(Guid rugId, Guid stepId, [FromBody] AdvanceRugStepRequest request, CancellationToken ct)
        => Ok(await rugs.AdvanceStepAsync(User.RequireTenantId(), rugId, stepId, request, ct));

    [HttpPut("{rugId:guid}/steps/{stepId:guid}/pricing")]
    public async Task<IActionResult> UpdatePricing(Guid rugId, Guid stepId, [FromBody] AdvanceRugStepRequest request, CancellationToken ct)
        => Ok(await rugs.UpdateStepPricingAsync(User.RequireTenantId(), rugId, stepId, request, ct));

    [HttpPost("{rugId:guid}/steps/{stepId:guid}/skip")]
    public async Task<IActionResult> Skip(Guid rugId, Guid stepId, CancellationToken ct)
        => Ok(await rugs.SkipStepAsync(User.RequireTenantId(), rugId, stepId, ct));

    [HttpPost("{rugId:guid}/steps/{stepId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid rugId, Guid stepId, CancellationToken ct)
        => Ok(await rugs.ActivateStepAsync(User.RequireTenantId(), rugId, stepId, ct));

    [HttpPost("{rugId:guid}/workflow/back")]
    public async Task<IActionResult> GoBack(Guid rugId, CancellationToken ct)
        => Ok(await rugs.GoBackStepAsync(User.RequireTenantId(), rugId, ct));

    /// <summary>اعمال یک قالب گردش کار روی فرشی که هنوز مسیر ندارد.</summary>
    [HttpPost("{rugId:guid}/workflow/apply-template")]
    public async Task<IActionResult> ApplyTemplate(Guid rugId, [FromBody] ApplyTemplateRequest request, CancellationToken ct)
        => Ok(await rugs.ApplyTemplateAsync(User.RequireTenantId(), rugId, request.TemplateId, request.SkippedOptionalStepIds, ct));

    /// <summary>پیشبرد گروهی مرحلهٔ جاریِ چند فرش با هم.</summary>
    [HttpPost("bulk/advance")]
    public async Task<IActionResult> BulkAdvance([FromBody] BulkAdvanceRequest request, CancellationToken ct)
        => Ok(await rugs.BulkAdvanceAsync(User.RequireTenantId(), request, ct));

    /// <summary>بازگشت گروهی چند فرش به مرحلهٔ قبل.</summary>
    [HttpPost("bulk/back")]
    public async Task<IActionResult> BulkBack([FromBody] BulkRugIdsRequest request, CancellationToken ct)
        => Ok(await rugs.BulkGoBackAsync(User.RequireTenantId(), request, ct));

    /// <summary>ویرایش گروهی مشخصات چند فرش — فیلدهای null دست‌نخورده می‌مانند.</summary>
    [HttpPut("bulk/fields")]
    public async Task<IActionResult> BulkFields([FromBody] BulkUpdateFieldsRequest request, CancellationToken ct)
        => Ok(await rugs.BulkUpdateFieldsAsync(User.RequireTenantId(), request, ct));
}
