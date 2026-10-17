using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Rugs;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// «این عدد از کجا آمد؟» برای سهم ثبت‌های هزینه (spec I.3 <c>RugCostBreakdownDto.Allocations</c>): فاز ۲ ریز هزینه را با فهرست
/// خالیِ سهم‌ها می‌سازد (<c>RugPurchaseService</c>، فاز ۲)؛ این لایه همان پاسخ را با سهم این فرش از هر ثبت حذف‌نشده (خرید
/// یکجا و هزینه‌ها) پر می‌کند. بقیهٔ کارهای سرویس (سطرهای خرید، نوع‌ها) دست‌نخورده عبور می‌کنند.
/// </summary>
public sealed class CostBreakdownDecorator(IRugPurchaseService inner, ICostEntryRepository entries) : IRugPurchaseService
{
    public Task<PurchaseLinesDto> GetAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
        => inner.GetAsync(tenantId, rugId, cancellationToken);

    public Task<PurchaseLinesDto> ReplaceAsync(
        Guid tenantId, Guid rugId, ReplacePurchaseLinesRequest request, CancellationToken cancellationToken = default)
        => inner.ReplaceAsync(tenantId, rugId, request, cancellationToken);

    public async Task<RugCostBreakdownDto> BreakdownAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
    {
        var breakdown = await inner.BreakdownAsync(tenantId, rugId, cancellationToken);
        var allocations = await entries.AllocationsOfRugAsync(tenantId, rugId, cancellationToken);
        return breakdown with { Allocations = allocations };
    }

    public Task<IReadOnlyList<PurchaseCategoryOptionDto>> CategoriesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => inner.CategoriesAsync(tenantId, cancellationToken);
}
