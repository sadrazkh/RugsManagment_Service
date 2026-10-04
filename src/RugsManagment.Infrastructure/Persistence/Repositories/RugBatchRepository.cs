using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>«ورودی»ها (RugBatch): فهرست کارگاه و یک ورودی با فرش‌ها و مراحلشان.</summary>
public class RugBatchRepository(AppDbContext db, IRugCostTotalReader costTotals) : Repository<RugBatch>(db), IRugBatchRepository
{
    public async Task<IReadOnlyList<RugBatch>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.RugBatches.AsNoTracking()
            .Include(b => b.Rugs)
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.ReceivedAt ?? b.CreatedAt)
            .ThenByDescending(b => b.Number)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// AsSplitQuery: مجموعهٔ تودرتو (فرش‌ها ← مراحل) در یک JOIN، ستون‌های گروه و هر فرش را به تعداد مراحلش تکرار می‌کند
    /// (گروه ۲۰۰ فرشی با ۸ مرحله = ۱۶۰۰ ردیف پهن). با تقسیم، هر سطح یک کوئری جدا و بدون تکرار است. بهای فرش‌ها یک‌جا در
    /// خوانندهٔ جمع گذاشته می‌شود (نه یک پرس‌وجو برای هر فرش).
    /// </summary>
    public async Task<RugBatch?> GetWithRugsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var batch = await Db.RugBatches
            .Include(b => b.Rugs)
                .ThenInclude(r => r.WorkflowSteps)
                    .ThenInclude(s => s.ProcessStepType)
            .Include(b => b.Rugs)
                .ThenInclude(r => r.PurchaseLines)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, cancellationToken);

        if (batch is not null && batch.Rugs.Count > 0)
            await costTotals.PreloadAsync(batch.Rugs.Select(r => r.Id).ToList(), cancellationToken);
        return batch;
    }
}
