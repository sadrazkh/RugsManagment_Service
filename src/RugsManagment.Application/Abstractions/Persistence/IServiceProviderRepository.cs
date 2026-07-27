using RugsManagment.Application.DTOs.Providers;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>قالیشوی‌ها و رفوگرهای ثبت‌شده توسط کارگاه، و حساب مالی‌شان.</summary>
public interface IServiceProviderRepository : IRepository<ServiceProvider>
{
    /// <summary>فقط طرف‌های فعال — برای انتخابگرها هنگام ثبت مرحله.</summary>
    Task<IReadOnlyList<ServiceProvider>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>همهٔ طرف‌ها (فعال و غیرفعال) همراه نرخ‌ها — برای صفحهٔ مدیریت.</summary>
    Task<IReadOnlyList<ServiceProvider>> ListAllWithRatesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<ServiceProvider?> GetWithRatesAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// مانده‌حساب همهٔ طرف‌ها. جمع‌ها در SQL محاسبه می‌شوند تا با زیاد شدن
    /// مراحل و پرداخت‌ها کند نشود.
    /// </summary>
    Task<IReadOnlyList<ProviderBalanceDto>> ListBalancesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>کارهای انجام‌شده (و در جریان) یک طرف — ردیف‌های صورت‌حساب.</summary>
    Task<IReadOnlyList<ProviderWorkItemDto>> ListWorkAsync(Guid tenantId, Guid providerId, CancellationToken cancellationToken = default);

    /// <summary>آیا این طرف به مرحله‌ای نسبت داده شده؟ (برای جلوگیری از حذفِ دارای سابقه)</summary>
    Task<bool> HasWorkHistoryAsync(Guid providerId, CancellationToken cancellationToken = default);
}
