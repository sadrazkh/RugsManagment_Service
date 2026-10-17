using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="ITrashAwareQueries"/>: همان پرسش‌ها با <c>IgnoreQueryFilters</c> (مثل
/// <c>ServiceProviderRepository.HasWorkHistoryAsync</c>)، تا مرحله و سطر خریدِ فرش سطل زباله هم دیده شود. کارگاه همیشه
/// صریح در شرط است، چون فیلتر سراسری برداشته شده.
/// </summary>
public sealed class TrashAwareQueries(AppDbContext db) : ITrashAwareQueries
{
    public Task<bool> IsStepTypeUsedAsync(Guid processStepTypeId, CancellationToken cancellationToken = default)
        => db.RugWorkflowSteps
            .IgnoreQueryFilters()
            .AnyAsync(s => s.ProcessStepTypeId == processStepTypeId, cancellationToken);

    public async Task<IReadOnlyList<RugPurchaseLine>> TrackUnlinkedLegacyPurchaseLinesAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string label = RugPurchaseLine.LegacyPurchaseLabel;
        return await db.Set<RugPurchaseLine>()
            .IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId && l.CategoryId == null && l.Label == label)
            .ToListAsync(cancellationToken);
    }
}
