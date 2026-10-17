using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// سلامت حساب‌ها (D.4 «Recovery») — فقط تنظیم‌کننده (Configure):
/// <list type="bullet">
/// <item><c>GET /api/diagnostics/cost-consistency</c>: همه‌چیز را با همان قاعده‌ها از نو حساب و با ذخیره‌شده می‌سنجد؛
/// <c>{checked, mismatches[]}</c>، فهرست خالی یعنی سالم.</item>
/// <item><c>POST /api/diagnostics/ledger-rebuild</c>: همهٔ ردیف‌های مشتق‌شدهٔ کارگاه را از نو می‌نویسد؛
/// <c>{rugs, entries, lines}</c>. در تاریخچه ثبت می‌شود.</item>
/// </list>
/// صفحهٔ <c>/settings/ledger-health</c> (جزیرهٔ <c>ledger-health</c>) همین دو را صدا می‌زند.
/// </summary>
[Route("api/diagnostics")]
[Authorize(Policy = Policies.Configure)]
public class DiagnosticsApiController(
    ILedgerVerifier verifier,
    ILedgerMaintenance maintenance,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : ApiControllerBase
{
    [HttpGet("cost-consistency")]
    public async Task<IActionResult> CostConsistency(CancellationToken ct)
        => Ok(await verifier.VerifyAsync(User.RequireTenantId(), sample: null, ct));

    [HttpPost("ledger-rebuild")]
    public async Task<IActionResult> LedgerRebuild(CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        var result = await maintenance.RebuildAsync(tenantId, ct);

        // تاریخچه در تراکنش بازسازی نیست (بازسازی خودش ذخیره کرده)؛ پس جدا و بعد از موفقیت ثبت می‌شود
        audit.Record(AuditAction.LedgerRebuilt, "Ledger", null,
            $"حساب‌های کارگاه از نو ساخته شد ({Application.Common.PersianText.ToPersianDigits(result.Rugs.ToString())} فرش).",
            details: new { result.Rugs, result.Entries, result.Lines });
        await unitOfWork.SaveChangesAsync(ct);
        return Ok(result);
    }
}
