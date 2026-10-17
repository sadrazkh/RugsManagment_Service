using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// اثر تغییرهای دفتر هزینه و حذف‌هایی که هدف ثبت‌ها را می‌برند (spec D.5 #6، #17، #18، #24، #25b):
/// <list type="bullet">
/// <item>ثبت هزینه (تازه، ویرایش، حذف نرم، بازگردانی)، هدف‌ها و مبلغ‌های ثابتش → همان ثبت. ثبتی که حذف سخت می‌شود (هیچ مسیری
/// امروز نمی‌کند؛ احتیاط) سهم‌هایش با cascade می‌روند، پس فرش‌هایش همین‌جا به جمع هزینه می‌روند.</item>
/// <item>نگلهٔ حذف‌شده، محمولهٔ حذف‌شده (و نگله‌هایش) و فرشِ حذف سخت (برگشت ورود از اکسل): هدف‌های نگله‌ای و فرشی با cascade و
/// پیوند محموله با SET NULL در flush می‌روند؛ پس ثبت‌هایشان <b>پیش از flush</b> خوانده می‌شوند (D.4 گام ۴) — بعد از آن
/// دیگر پیدا نمی‌شوند. آن ثبت‌ها دوباره تقسیم می‌شوند و آن که هدفی ندارد «تقسیم‌نشده» با <c>W_TARGET_DELETED</c> می‌ماند.</item>
/// <item>پیوند نوع هزینه به نوع مرحله (<c>StepTypeId</c>) → همهٔ ثبت‌های آن نوع، فقط برای هشدار «دوبار حساب شده».</item>
/// </list>
/// حذف ورودی را <see cref="BatchImpactContributor"/> می‌بیند.
/// </summary>
public sealed class CostEntryImpactContributor(ICostLedgerRepository repository) : IChangeImpactContributor
{
    public IReadOnlyCollection<Type> WatchedTypes { get; } =
        [typeof(CostEntry), typeof(CostEntryTarget), typeof(CostEntryPin), typeof(CostCategory), typeof(Bale), typeof(Shipment), typeof(Rug)];

    public async Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var impact = context.Impact;
        var hardDeletedEntries = new List<Guid>();

        foreach (var change in context.Changes.Of<CostEntry>())
        {
            var entry = change.As<CostEntry>();
            impact.Entries.Add(entry.Id);
            if (change.Kind == ChangeKind.Deleted)
                hardDeletedEntries.Add(entry.Id);
        }

        foreach (var change in context.Changes.Of<CostEntryTarget>())
        {
            impact.Entries.Add(change.As<CostEntryTarget>().EntryId);
            if (change.Original<Guid>(nameof(CostEntryTarget.EntryId)) is var old && old != Guid.Empty)
                impact.Entries.Add(old);
        }

        foreach (var change in context.Changes.Of<CostEntryPin>())
        {
            impact.Entries.Add(change.As<CostEntryPin>().EntryId);
            if (change.Original<Guid>(nameof(CostEntryPin.EntryId)) is var old && old != Guid.Empty)
                impact.Entries.Add(old);
        }

        var state = CostPassState.Of(context);
        foreach (var change in context.Changes.Of<CostCategory>())
        {
            if (change.Kind is ChangeKind.Modified or ChangeKind.Unknown && change.Has(nameof(CostCategory.StepTypeId)))
                state.Categories.Add(change.As<CostCategory>().Id);
        }

        var deletedShipments = context.Changes.Of<Shipment>()
            .Where(c => c.Kind == ChangeKind.Deleted).Select(c => c.As<Shipment>().Id).ToList();
        var deletedBales = context.Changes.Of<Bale>()
            .Where(c => c.Kind == ChangeKind.Deleted).Select(c => c.As<Bale>().Id).ToList();
        var deletedRugs = context.Changes.Of<Rug>()
            .Where(c => c.Kind == ChangeKind.Deleted).Select(c => c.As<Rug>().Id).ToList();

        if (deletedShipments.Count > 0 || deletedBales.Count > 0 || deletedRugs.Count > 0)
        {
            var losing = await repository.EntriesLosingTargetsAsync(
                context.TenantId, deletedShipments, deletedBales, [], deletedRugs, cancellationToken);
            impact.Entries.UnionWith(losing);
        }

        if (hardDeletedEntries.Count > 0)
        {
            var rugs = await repository.AllocatedRugsAsync(context.TenantId, hardDeletedEntries, cancellationToken);
            impact.CostTotalRugs.UnionWith(rugs);
        }
    }
}
