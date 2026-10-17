using RugsManagment.Application.Common;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// قفل «گروه نوع هزینه» (spec B.7، D.5 #25): نوعی که با آن هزینه یا سطر خرید ثبت شده (حذف‌شده و سطل زباله هم) گروهش
/// (خرید / هزینه) عوض نمی‌شود و حذف نمی‌شود → ۴۰۹. چرا در پاس: مبلغ‌هایش در یکی از دو سطل <c>PurchaseCost</c> و
/// <c>ExpenseCost</c> جمع شده‌اند؛ عوض شدن بی‌صدای گروه آن پول را به سطل دیگر می‌برد و «به نسبت قیمت خرید» را دور باطل
/// می‌کرد. هر مسیر (API تنظیمات، پنل، کد آینده) از همین پاس می‌گذرد. نام و آیکون آزادند.
/// یک پرس‌وجوی ایندکس‌دار، فقط وقتی گروه عوض شده یا نوعی حذف می‌شود.
/// </summary>
public sealed class CostCategoryGuard(ICostLedgerRepository repository) : ILedgerGuard
{
    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(CostCategory)];

    public async Task CheckAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<Guid, (CostCategory Category, bool Deleted)>();
        foreach (var change in context.Changes.Of<CostCategory>())
        {
            var category = change.As<CostCategory>();
            switch (change.Kind)
            {
                case ChangeKind.Deleted:
                    candidates[category.Id] = (category, true);
                    break;
                case ChangeKind.Modified when change.Has(nameof(CostCategory.Group)):
                case ChangeKind.Unknown:
                    candidates[category.Id] = (category, false);
                    break;
            }
        }

        if (candidates.Count == 0)
            return;

        var uses = await repository.CategoryUsesAsync(context.TenantId, candidates.Keys.ToList(), cancellationToken);
        foreach (var use in uses.Where(u => u.Used))
        {
            var (category, deleted) = candidates[use.Id];
            if (deleted)
                throw new DomainConflictException(Messages.CostCategoryInUse.Format(("category", category.Name)), "category");

            // «نامعلوم» (وصل‌شده بی بارگذاری): با گروه ذخیره‌شده سنجیده می‌شود، نه با پرچم تغییر
            if (category.Group != use.Group)
                throw new DomainConflictException(Messages.CostCategoryGroupLocked.Format(("category", category.Name)), "group");
        }
    }
}
