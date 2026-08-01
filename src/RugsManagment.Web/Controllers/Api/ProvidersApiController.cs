using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Providers;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// طرف‌های خدمات: مشخصات، نرخ‌های توافقی، صورت‌حساب و پرداخت.
/// همهٔ عملیات مالی فقط برای مدیر کارگاه؛ اپراتور نباید بتواند پرداخت ثبت کند.
/// </summary>
[Route("api/providers")]
[Authorize(Roles = nameof(UserRole.TenantAdmin))]
public class ProvidersApiController(IServiceProviderService providers) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await providers.ListAsync(User.RequireTenantId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var provider = await providers.GetAsync(User.RequireTenantId(), id, ct);
        return provider is null ? NotFound(new { message = "طرف خدمات یافت نشد." }) : Ok(provider);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveServiceProviderRequest request, CancellationToken ct)
        => Ok(await providers.CreateAsync(User.RequireTenantId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveServiceProviderRequest request, CancellationToken ct)
        => Ok(await providers.UpdateAsync(User.RequireTenantId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await providers.DeleteAsync(User.RequireTenantId(), id, ct);
        return NoContent();
    }

    // ── حساب مالی ──

    /// <summary>مانده‌حساب همهٔ طرف‌ها — صفحهٔ تسویه.</summary>
    [HttpGet("balances")]
    public async Task<IActionResult> Balances(CancellationToken ct)
        => Ok(await providers.ListBalancesAsync(User.RequireTenantId(), ct));

    /// <summary>صورت‌حساب یک طرف: مانده + کارهای انجام‌شده + پرداخت‌ها.</summary>
    [HttpGet("{id:guid}/statement")]
    public async Task<IActionResult> Statement(Guid id, CancellationToken ct)
        => Ok(await providers.GetStatementAsync(User.RequireTenantId(), id, ct));

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> AddPayment(
        Guid id, [FromBody] CreateProviderPaymentRequest request, CancellationToken ct)
        => Ok(await providers.AddPaymentAsync(User.RequireTenantId(), id, request, ct));

    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    public async Task<IActionResult> DeletePayment(Guid id, Guid paymentId, CancellationToken ct)
    {
        await providers.DeletePaymentAsync(User.RequireTenantId(), id, paymentId, ct);
        return NoContent();
    }
}
