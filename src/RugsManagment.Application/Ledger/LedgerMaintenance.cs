using RugsManagment.Application.DTOs.Rugs;

namespace RugsManagment.Application.Ledger;

/// <summary>
/// پیاده‌سازی <see cref="ILedgerMaintenance"/>: همان مرحله‌های پاس با «اثر = همهٔ فرش‌ها». چون همان مرحله‌ها و همان
/// CostSql به کار می‌روند، بازسازی همان عددی را می‌نویسد که پاس عادی نوشته بود؛ پس «سلامت حساب‌ها» بعد از بازسازی صفر
/// ناهمخوانی دارد مگر قاعده‌ها عوض شده باشند.
/// </summary>
public sealed class LedgerMaintenance(
    ILedgerSession session,
    ILedgerPass pass,
    ILedgerRepository repository,
    IRugCostTotalReader costTotals,
    TimeProvider? time = null) : ILedgerMaintenance
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<LedgerRebuildResultDto> RebuildAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await session.BeginAsync(cancellationToken);
        try
        {
            await session.LockTenantAsync(tenantId, cancellationToken);

            var context = new LedgerContext(tenantId, ChangeSet.Empty, _time.GetUtcNow(), isRebuild: true);
            context.Apply(await repository.GetSettingsAsync(tenantId, cancellationToken));

            var rugIds = await repository.ListRugIdsAsync(tenantId, sample: null, cancellationToken);
            foreach (var id in rugIds)
            {
                context.Impact.CostTotalRugs.Add(id);
                context.Impact.PurchaseLineRugs.Add(id);
                context.Impact.MemberRugs.Add(id);
            }

            if (rugIds.Count > 0)
                await pass.RunStagesAsync(context, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            costTotals.Clear();
            return new LedgerRebuildResultDto(rugIds.Count, context.EntriesRecomputed, context.LinesRecomputed);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            costTotals.Clear();
            throw;
        }
    }
}
