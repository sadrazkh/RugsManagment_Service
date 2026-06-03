namespace RugsManagment.Application.DTOs.Dashboard;

/// <summary>پاسخ GET /api/dashboard — همهٔ اعداد در Application محاسبه شده</summary>
public record DashboardStatsDto(
    int TotalRugs,
    int InProgress,
    int ReadyForSale,
    int Sold,
    decimal TotalInvestment,
    decimal PipelineValue,
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
