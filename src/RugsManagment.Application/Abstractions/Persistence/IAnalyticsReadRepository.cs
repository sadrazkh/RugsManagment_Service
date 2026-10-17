using RugsManagment.Application.DTOs.Analytics;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>گزارش تجمیعی در دیتابیس؛ نمودار نباید همهٔ فرش‌ها و عکس‌ها و مراحل را در حافظه بار کند.</summary>
public interface IAnalyticsReadRepository
{
    Task<AgingReportDto> AgingAsync(Guid tenantId, CancellationToken ct = default);
    Task<AnalyticsReportDto> ReadAsync(Guid tenantId, int months, CancellationToken ct = default);
}
