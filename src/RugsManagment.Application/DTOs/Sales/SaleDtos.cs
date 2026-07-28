using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Sales;

/// <summary>فروش ثبت‌شدهٔ یک فرش، همراه سود واقعی.</summary>
public record RugSaleDto(
    Guid Id,
    Guid RugId,
    string RugSku,
    string? RugTitle,
    string BuyerName,
    string? BuyerPhone,
    decimal SalePrice,
    decimal Discount,
    decimal NetAmount,
    decimal ReceivedAmount,
    decimal OutstandingAmount,
    SalePaymentMethod PaymentMethod,
    DateTimeOffset SoldAt,
    string? Reference,
    string? Notes,
    /// <summary>سرمایه‌گذاری کل روی فرش (خرید + مراحل) در لحظهٔ گزارش</summary>
    decimal TotalInvestment,
    /// <summary>سود واقعی = مبلغ خالص فروش − سرمایه‌گذاری کل</summary>
    decimal ActualProfit);

public record SaveRugSaleRequest(
    string BuyerName,
    string? BuyerPhone,
    decimal SalePrice,
    decimal Discount,
    decimal? ReceivedAmount,
    SalePaymentMethod PaymentMethod,
    DateTimeOffset? SoldAt,
    string? Reference,
    string? Notes);

/// <summary>فیلتر گزارش فروش.</summary>
public record SalesQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    /// <summary>فقط فروش‌هایی که هنوز کامل تسویه نشده‌اند</summary>
    public bool OnlyOutstanding { get; init; }

    public SalesQuery Sanitized() => this with
    {
        From = From?.ToUniversalTime(),
        To = To?.ToUniversalTime()
    };
}

/// <summary>
/// خلاصهٔ گزارش فروش برای یک بازه.
/// همهٔ اعداد از رکوردهای فروش می‌آیند، نه از تخمین.
/// </summary>
public record SalesSummaryDto(
    int SaleCount,
    decimal GrossTotal,
    decimal DiscountTotal,
    decimal NetTotal,
    decimal ReceivedTotal,
    decimal OutstandingTotal,
    decimal InvestmentTotal,
    decimal ProfitTotal)
{
    /// <summary>حاشیهٔ سود بر حسب درصد از فروش خالص؛ null وقتی فروشی نبوده.</summary>
    public decimal? MarginPercent => NetTotal == 0 ? null : Math.Round(ProfitTotal / NetTotal * 100, 1);
}

public record SalesReportDto(
    SalesSummaryDto Summary,
    IReadOnlyList<RugSaleDto> Sales);
