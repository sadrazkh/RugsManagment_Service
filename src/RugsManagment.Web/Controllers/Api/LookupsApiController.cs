using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Fields;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// دادهٔ کمکی فرم‌ها: قالب‌های گردش کار، انواع مرحله، طرف‌های خدمات، فیلدهای سفارشی؛ از فاز ۲ کاتالوگ فیلدها
/// (<c>/api/fields</c>) و پیشنهاد مقدارهای قبلی (<c>/api/rugs/values</c>). همه Floor؛ مبلغ‌ها (فیلدهای پولی کاتالوگ،
/// نرخ‌ها) برای غیر Business در سرویس‌ها پنهان می‌شوند.
/// </summary>
[Route("api/lookups")]
public class LookupsApiController(
    ILookupService lookups,
    ICustomFieldService customFields,
    IFieldCatalog fieldCatalog,
    IRugManagementService rugs) : ApiControllerBase
{
    [HttpGet("workflow-templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
        => Ok(await lookups.WorkflowTemplatesAsync(User.RequireTenantId(), ct));

    [HttpGet("step-types")]
    public async Task<IActionResult> StepTypes(CancellationToken ct)
        => Ok(await lookups.StepTypesAsync(User.RequireTenantId(), ct));

    [HttpGet("service-providers")]
    public async Task<IActionResult> ServiceProviders(CancellationToken ct)
        => Ok(await lookups.ServiceProvidersAsync(User.RequireTenantId(), ct));

    [HttpGet("custom-fields")]
    public async Task<IActionResult> CustomFields(CancellationToken ct)
        => Ok(await customFields.ListAsync(User.RequireTenantId(), onlyActive: true, ct));

    /// <summary>
    /// کاتالوگ فیلدها (E.3): <c>?purpose=grid|document&amp;kind=</c> → <c>FieldDefDto[]</c>. فیلدهای پولی فقط برای Business.
    /// هدف ناشناخته → ۴۰۰.
    /// </summary>
    [HttpGet("/api/fields")]
    public async Task<IActionResult> Fields([FromQuery] string? purpose, [FromQuery] string? kind, CancellationToken ct)
    {
        var effective = string.IsNullOrWhiteSpace(purpose) ? FieldPurpose.Grid : purpose.Trim().ToLowerInvariant();
        if (!FieldPurpose.IsKnown(effective))
            return BadRequest(new { message = Messages.InvalidValue.Format(("label", "هدف")) });

        return Ok(await fieldCatalog.ListAsync(User.RequireTenantId(), effective, kind, ct));
    }

    /// <summary>
    /// پیشنهاد مقدارهای قبلیِ یک خانهٔ combo: <c>?field=rug.origin&amp;q=کا</c> → <c>string[]</c> (پرتکرارتر اول). کلیدی که
    /// پیشنهاد ندارد → ۴۰۰.
    /// </summary>
    [HttpGet("/api/rugs/values")]
    public async Task<IActionResult> Values([FromQuery] string? field, [FromQuery] string? q, [FromQuery] int? take, CancellationToken ct)
    {
        if (!RugValuesQuery.TryFromFieldKey(field, q, out var query))
            return BadRequest(new { message = Messages.ValuesFieldInvalid.Text });

        if (take is { } limit)
            query = (query with { Take = limit }).Sanitized();

        return Ok(await rugs.ListValuesAsync(User.RequireTenantId(), query, ct));
    }
}
