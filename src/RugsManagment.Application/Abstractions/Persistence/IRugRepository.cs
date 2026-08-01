using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>دسترسی دیتابیس به فرش — همیشه همراه TenantId برای امنیت</summary>
public interface IRugRepository : IRepository<Rug>
{
    Task<Rug?> GetWithWorkflowAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// بارگذاری کامل همهٔ فرش‌های کارگاه همراه مراحل — پرهزینه است.
    /// فقط برای محاسبات آماری (داشبورد) مناسب است؛ برای فهرست از
    /// <see cref="SearchAsync"/> استفاده کنید.
    /// </summary>
    Task<IReadOnlyList<Rug>> ListByTenantAsync(Guid tenantId, RugStatus? status, CancellationToken cancellationToken = default);

    /// <summary>
    /// فهرست صفحه‌بندی‌شده با جستجو، فیلتر و مرتب‌سازی.
    /// شمارش، هزینه و مرحلهٔ جاری همه در SQL محاسبه می‌شوند و موجودیت‌ها
    /// اصلاً materialize نمی‌شوند — مستقل از تعداد کل فرش‌های کارگاه.
    /// </summary>
    Task<PagedResult<RugListItemDto>> SearchAsync(Guid tenantId, RugQuery query, CancellationToken cancellationToken = default);

    Task<string> GenerateNextSkuAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // ── سطل زباله ─────────────────────────────────────────────────
    // فیلتر سراسری EF فرش‌های حذف‌شده را از همهٔ کوئری‌های بالا بیرون می‌گذارد،
    // پس این دو متد تنها راه دیدن و بازگرداندن آن‌ها هستند.

    /// <summary>فرش‌های سطل زباله، تازه‌ترین اول.</summary>
    Task<IReadOnlyList<DeletedRugDto>> ListDeletedAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>یک فرش حذف‌شده برای بازگردانی؛ null یعنی وجود ندارد یا مال کارگاه دیگری است.</summary>
    Task<Rug?> GetDeletedAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
