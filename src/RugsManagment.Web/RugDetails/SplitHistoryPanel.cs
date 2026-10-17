using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.RugDetails;

/// <summary>
/// چیپ «جداشده از 3911 - 3960» زیر نام فرشی که از برش یک دسته ساخته شد (D.9)، و «۲۰ تخته از این دسته جدا شد» روی خودِ
/// دسته. از تاریخچهٔ فعالیت خوانده می‌شود (رویداد <see cref="AuditAction.RugSplit"/>)، نه از ستونی روی فرش؛ برش یک‌بار رخ
/// می‌دهد و تاریخچه همان سند ماندگارش است.
/// </summary>
public sealed class SplitHistoryPanel(IAuditEntryRepository audit) : IRugDetailPanel
{
    public string Key => "split-history";

    public RugDetailSlot Slot => RugDetailSlot.Header;

    public int Order => 100;

    public async Task<RugDetailPanelView?> RenderAsync(RugDetailPanelContext context, CancellationToken cancellationToken)
    {
        var entries = await audit.ListForEntityAsync(context.TenantId, nameof(Rug), context.Rug.Id, cancellationToken);
        var splits = entries
            .Where(e => e.Action == AuditAction.RugSplit)
            .OrderBy(e => e.CreatedAt)
            .Select(e => e.Summary)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return splits.Count == 0
            ? null
            : RugDetailPanelView.ForPartial("~/Views/Rugs/_Details.SplitHistory.cshtml", splits);
    }
}
