using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Ledger.Contributors;

/// <summary>
/// اثر تغییرهای خودِ فرش (spec D.5 ردیف‌های ۱ تا ۸، با E1):
/// <list type="bullet">
/// <item>فرش تازه، یا فرشی که مقدار اصلی‌اش معلوم نیست → همه‌چیز (سطرهای خرید، جمع، شماره، عضویت).</item>
/// <item>اندازهٔ اول یا تعداد → مبلغ سطرهای خرید (هر متر روی اندازهٔ اول، هر تخته و درصد روی تعداد) و جمع.</item>
/// <item>اندازهٔ نهایی یا جفت مؤثر → جمع (بها برای هر متر) و عضویت در ثبت‌های «اندازهٔ مؤثر».</item>
/// <item>شماره (E1) → سنجش همپوشانی زیر قفل، و ثبت‌هایی که فرش را دارند (شماره کلید شکستن تساوی در تقسیم است).</item>
/// <item>سطل زباله / بازگردانی → عضویت؛ بازگردانی شماره را دوباره می‌سنجد (M.2 #4: سطل زباله شماره را آزاد می‌کند).</item>
/// <item>ورودی (BatchId) → عضویت در ثبت‌های «این ورودی» ورودیِ قبلی و تازه.</item>
/// </list>
/// توصیف، وضعیت و مسیر کار اثری ندارند؛ فقط راه قفل‌دار را می‌روند تا قفل‌ها (فاز محموله) بسنجند (D.5 #8).
/// </summary>
public sealed class RugImpactContributor : IChangeImpactContributor
{
    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(Rug)];

    public Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var impact = context.Impact;
        foreach (var change in context.Changes.Of<Rug>())
        {
            var rug = change.As<Rug>();
            var id = rug.Id;

            switch (change.Kind)
            {
                case ChangeKind.Added:
                case ChangeKind.Unknown:
                    impact.AddFullRug(id);
                    if (rug.BatchId is Guid batch) impact.Batches.Add(batch);
                    continue;

                case ChangeKind.Deleted:
                    // حذف سخت (فقط برگشت ورود از اکسل): سطرها و جمعش با cascade می‌روند؛ ثبت‌هایی که آن را داشتند (فاز هزینه‌ها)
                    impact.MemberRugs.Add(id);
                    if (change.Original<Guid?>(nameof(Rug.BatchId)) is Guid oldBatch) impact.Batches.Add(oldBatch);
                    continue;
            }

            if (change.HasAny(nameof(Rug.IntakeLength), nameof(Rug.IntakeWidth), nameof(Rug.Quantity)))
            {
                impact.PurchaseLineRugs.Add(id);
                impact.CostTotalRugs.Add(id);
                impact.MemberRugs.Add(id);
            }

            if (change.HasAny(nameof(Rug.FinalLength), nameof(Rug.FinalWidth), nameof(Rug.LengthMeters), nameof(Rug.WidthMeters)))
            {
                impact.CostTotalRugs.Add(id);
                impact.MemberRugs.Add(id);
            }

            if (change.HasAny(nameof(Rug.SerialNo), nameof(Rug.SerialFrom), nameof(Rug.SerialTo), nameof(Rug.SerialSort)))
            {
                impact.SerialCheckRugs.Add(id);
                impact.MemberRugs.Add(id);
            }

            if (change.Has(nameof(Rug.DeletedAt)))
            {
                impact.MemberRugs.Add(id);
                impact.CostTotalRugs.Add(id);
                var restored = change.Original<DateTimeOffset?>(nameof(Rug.DeletedAt)) is not null && rug.DeletedAt is null;
                if (restored)
                    impact.SerialCheckRugs.Add(id);
            }

            if (change.Has(nameof(Rug.BatchId)))
            {
                impact.MemberRugs.Add(id);
                if (change.Original<Guid?>(nameof(Rug.BatchId)) is Guid oldBatch) impact.Batches.Add(oldBatch);
                if (rug.BatchId is Guid newBatch) impact.Batches.Add(newBatch);
            }
        }

        return Task.CompletedTask;
    }
}
