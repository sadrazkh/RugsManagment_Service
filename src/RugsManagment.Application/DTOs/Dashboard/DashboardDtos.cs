namespace RugsManagment.Application.DTOs.Dashboard;

/// <summary>پاسخ GET /api/dashboard — همهٔ اعداد در Application محاسبه شده</summary>
public record DashboardStatsDto(
    int TotalRugs,
    int InProgress,
    int ReadyForSale,
    int Sold,
    decimal TotalInvestment,
    decimal PipelineValue,
    /// <summary>سود تخمینیِ فرش‌های فروخته‌نشده — بر پایهٔ قیمت هدف</summary>
    decimal ProfitEstimate,
    decimal ReadyForSaleValue,
    int BatchCount,
    int PendingCostCount,
    /// <summary>مجموع فروش خالص واقعی</summary>
    decimal ActualSalesTotal,
    /// <summary>سود واقعیِ محقق‌شده روی فرش‌های فروخته‌شده</summary>
    decimal ActualProfitTotal,
    /// <summary>طلب باقی‌مانده از خریداران (فروش اقساطی/چکی)</summary>
    decimal OutstandingReceivable,
    IReadOnlyList<RecentRugDto> RecentRugs,
    IReadOnlyList<StepDistributionDto> StepDistribution);

public record RecentRugDto(
    Guid Id,
    string Sku,
    string? Title,
    string Status,
    string? CurrentStepName,
    decimal TotalInvestment);

public record StepDistributionDto(
    string StepName,
    int Count);
