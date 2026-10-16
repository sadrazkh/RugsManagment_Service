using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Costs;

/// <summary>
/// «حساب محموله» (I.3، D.6؛ <c>GET /api/shipments/{id}/profit</c> و پاسخ <c>PUT /revenue</c>، Business): جمع بهای فرش‌های
/// محموله در سه سطل D.1، قیمت تمام‌شدهٔ هر متر، درآمد به ارز کارگاه و سود = درآمد − Σ بها. عضوهای افزوده بعد از
/// <paramref name="Finalized"/> فرم «فروش این محموله» را پر می‌کنند.
/// </summary>
/// <param name="Pieces">جمع تخته‌ها.</param>
/// <param name="AreaM2">جمع متراژ خط‌ها (مؤثر).</param>
/// <param name="UsdTotal">جمع دلار اظهاری.</param>
/// <param name="Purchase">جمع قیمت خرید (سطرها + خرید یکجا).</param>
/// <param name="Steps">جمع کارهای کارگاه.</param>
/// <param name="Expenses">جمع سهم هزینه‌ها.</param>
/// <param name="Total">جمع بهای تمام‌شده.</param>
/// <param name="CostPerM2">ROUND(Total ÷ متراژ، MoneyScale)؛ null بی متراژ.</param>
/// <param name="RevenueBase">درآمد به ارز کارگاه = ROUND(مبلغ × نرخ، s)؛ null وقتی درآمد ثبت نشده.</param>
/// <param name="Profit">درآمد − بها؛ null بی درآمد.</param>
/// <param name="Finalized">محموله قطعی است.</param>
/// <param name="RevenueAmount">مبلغ درآمد در ارز خودش («فروش این محموله: [__] دلار»).</param>
/// <param name="RevenueCurrency">ارز درآمد.</param>
/// <param name="RevenueFx">نرخ تبدیل درآمد به ارز کارگاه.</param>
/// <param name="Version">نسخهٔ محموله برای <c>PUT /revenue</c> بعدی.</param>
/// <param name="UnallocatedEntries">ثبت‌های این محموله که همهٔ مبلغشان تقسیم نشده (تراشهٔ «تقسیم‌نشده»).</param>
public record ShipmentProfitDto(
    int Pieces,
    decimal AreaM2,
    decimal UsdTotal,
    decimal Purchase,
    decimal Steps,
    decimal Expenses,
    decimal Total,
    decimal? CostPerM2,
    decimal? RevenueBase,
    decimal? Profit,
    bool Finalized,
    decimal? RevenueAmount = null,
    CurrencyUnit? RevenueCurrency = null,
    decimal? RevenueFx = null,
    uint Version = 0,
    int UnallocatedEntries = 0);

/// <summary>
/// <c>PUT /api/shipments/{id}/revenue</c> (I.2): «فروش این محموله: [__] دلار × نرخ [__] = … تومان». مبلغ null درآمد را پاک
/// می‌کند. نرخ برای دلار الزامی است (<c>E_FX_REQUIRED</c>)؛ ریال↔تومان نرخ ثابت دارد.
/// </summary>
public record SaveRevenueRequest(decimal? Amount, CurrencyUnit Currency, decimal? Fx, uint Version);

/// <summary>سود یک فرش در محموله (D.6): سهم درآمد − بهای تمام‌شده.</summary>
public record RugProfitDto(Guid RugId, string? Serial, int Quantity, decimal Total, decimal? RevenueShare, decimal? Profit);
