using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// بخش‌های پولیِ ذاتیِ یک فرش (I.2): سطرهای خرید، ریز هزینه و گزینه‌های نوع خرید — همه Business (A.5).
///
/// چرا جدا از <see cref="RugsApiController"/>: آن کنترلر یکسره Floor است (نقشهٔ دسترسی J.2) و هر اکشنش برای انباردار باز
/// است؛ این‌ها خودِ مبلغ‌اند، نه چیزی که بشود پنهانش کرد. مسیرها همان <c>/api/rugs/…</c>ِ قرارداد I.2 است.
/// </summary>
[Route("api/rugs")]
[Authorize(Policy = Policies.Business)]
public class RugMoneyApiController(IRugPurchaseService purchases) : ApiControllerBase
{
    /// <summary>سطرهای خرید فرش: <c>{lines, purchaseCost, version, costs}</c>.</summary>
    [HttpGet("{id:guid}/purchase-lines")]
    public async Task<IActionResult> PurchaseLines(Guid id, CancellationToken ct)
        => Ok(await purchases.GetAsync(User.RequireTenantId(), id, ct));

    /// <summary>
    /// جایگزینی همهٔ سطرها با نسخهٔ فرش. مبلغ‌ها همان پاس حساب می‌کند و در پاسخ می‌آیند؛ نسخهٔ کهنه → ۴۰۹.
    /// </summary>
    [HttpPut("{id:guid}/purchase-lines")]
    public async Task<IActionResult> ReplacePurchaseLines(
        Guid id, [FromBody] ReplacePurchaseLinesRequest request, CancellationToken ct)
        => Ok(await purchases.ReplaceAsync(User.RequireTenantId(), id, request, ct));

    /// <summary>«این عدد از کجا آمد؟»: سطرهای خرید، مراحل، سهم ثبت‌های هزینه و خلاصه.</summary>
    [HttpGet("{id:guid}/cost-breakdown")]
    public async Task<IActionResult> CostBreakdown(Guid id, CancellationToken ct)
        => Ok(await purchases.BreakdownAsync(User.RequireTenantId(), id, ct));

    /// <summary>نوع‌های فعال گروه «خرید» برای ویرایشگر سطرها (قیمت خرید، دلالی، کرایه تا انبار، …).</summary>
    [HttpGet("purchase-categories")]
    public async Task<IActionResult> PurchaseCategories(CancellationToken ct)
        => Ok(await purchases.CategoriesAsync(User.RequireTenantId(), ct));
}
