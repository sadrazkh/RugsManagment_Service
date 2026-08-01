using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>
/// کاتالوگ انواع مرحله — ترکیبی از مرحله‌های سیستمی (مشترک بین همهٔ کارگاه‌ها)
/// و مرحله‌های اختصاصیِ هر کارگاه.
/// </summary>
public interface IProcessStepTypeRepository : IRepository<ProcessStepType>
{
    /// <summary>همهٔ مرحله‌های سیستمی — بدون در نظر گرفتن کارگاه (برای seed و کارهای سیستمی).</summary>
    Task<IReadOnlyList<ProcessStepType>> ListAllOrderedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// مرحله‌هایی که یک کارگاه می‌بیند: سیستمی + اختصاصی خودش.
    /// </summary>
    Task<IReadOnlyList<ProcessStepType>> ListForTenantAsync(
        Guid tenantId, bool onlyActive = true, CancellationToken cancellationToken = default);

    /// <summary>یک مرحله، فقط اگر برای این کارگاه قابل‌دسترس باشد.</summary>
    Task<ProcessStepType?> GetForTenantAsync(
        Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    Task<ProcessStepType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
