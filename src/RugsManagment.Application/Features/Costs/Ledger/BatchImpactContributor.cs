using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// اثر حذف یک «ورودی» (spec D.5 #12، E4): پیوند <c>CostEntry.BatchId</c> در flush با SET NULL پاک می‌شود، پس ثبت‌های آن ورودی
/// <b>پیش از flush</b> خوانده می‌شوند. ثبت گروه «هزینه» (عضویت زنده) بعد از آن «تقسیم‌نشده» با <c>W_TARGET_DELETED</c> است و
/// سهم فرش‌های قبلی‌اش برداشته می‌شود (جمع هزینهٔ آن‌ها از diff سهم‌ها)؛ ثبت گروه «خرید» عکس فرش‌هاست و همچنان روی همان
/// فرش‌ها تقسیم می‌شود، پس خرید یکجا از فرش‌هایی که در آن ورودی خریده شدند کنده نمی‌شود.
/// تغییر عضویت ورودیِ یک فرش را <c>RugImpactContributor</c> (ورودی قدیم و تازه) در اثر می‌گذارد.
/// </summary>
public sealed class BatchImpactContributor(ICostLedgerRepository repository) : IChangeImpactContributor
{
    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(RugBatch)];

    public async Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var deleted = context.Changes.Of<RugBatch>()
            .Where(c => c.Kind == ChangeKind.Deleted)
            .Select(c => c.As<RugBatch>().Id)
            .ToList();
        if (deleted.Count == 0)
            return;

        context.Impact.Batches.UnionWith(deleted);
        var entries = await repository.EntriesLosingTargetsAsync(context.TenantId, [], [], deleted, [], cancellationToken);
        context.Impact.Entries.UnionWith(entries);
    }
}
