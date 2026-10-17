using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// اجرای تقسیم برای چند ثبت در یک مرحلهٔ پاس: هر ثبت با <see cref="CostAllocator"/> از نو تقسیم می‌شود (همهٔ هشدارها هم از
/// نو، D.4)، با سهم‌ها و نتیجهٔ ذخیره‌شده مقایسه می‌شود و فقط آنچه واقعاً عوض شده با یک دستور نوشته می‌شود («diff-write»).
/// فرش‌هایی که سهمشان عوض شد (قدیم یا تازه) به جمع هزینه (<see cref="LedgerImpact.CostTotalRugs"/>) می‌روند؛ برای گروه خرید
/// همین آن‌ها را به جمع خرید مرحلهٔ ۵۰۰ و از آن‌جا به «به نسبت قیمت خرید» می‌رساند.
/// </summary>
public static class EntryRunner
{
    /// <summary>تقسیم، diff و نوشتن؛ شمار ثبت‌هایی که نوشته شدند.</summary>
    public static async Task<int> RunAsync(
        LedgerContext context, ICostLedgerRepository repository, IReadOnlyList<EntryInput> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0)
            return 0;

        var writes = new List<EntryWrite>();
        foreach (var input in inputs)
        {
            var (write, changedRugs) = Plan(input, context.Currency, context.MoneyScale);
            context.Impact.CostTotalRugs.UnionWith(changedRugs);
            if (write is not null)
                writes.Add(write);
        }

        context.EntriesRecomputed += inputs.Count;
        if (writes.Count > 0)
            await repository.WriteAsync(context.TenantId, writes, context.Now, cancellationToken);
        return writes.Count;
    }

    /// <summary>نتیجهٔ تازهٔ یک ثبت (حذف‌شده: بی سهم، کل «تقسیم‌نشده»، بی هشدار).</summary>
    public static AllocationResult Compute(EntryInput input, CurrencyUnit tenantCurrency, int moneyScale)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.Deleted)
            return CostAllocator.Allocate(input.Entry, input.Members, input.Pins, tenantCurrency, moneyScale);

        var baseAmount = input.Entry.IsLive ? 0m : input.Entry.BaseAmount;
        return new AllocationResult(baseAmount, input.Entry.Amount ?? baseAmount, 0, 0, 0m, 0, 0m, baseAmount,
            AllocationState.Unallocated, [], [], DTOs.Costs.ShareWeightUnits.Amount);
    }

    /// <summary>
    /// diff یک ثبت: (نوشتن یا null، فرش‌هایی که سهمشان عوض شد). سهمی که فقط وزنش یا «ثابت» بودنش عوض شده هم نوشته
    /// می‌شود ولی فرشش در جمع هزینه نمی‌رود (پولش عوض نشده).
    /// </summary>
    public static (EntryWrite? Write, IReadOnlyCollection<Guid> ChangedRugs) Plan(
        EntryInput input, CurrencyUnit tenantCurrency, int moneyScale)
    {
        var result = Compute(input, tenantCurrency, moneyScale);
        var stored = input.Stored.ToDictionary(s => s.RugId);
        var fresh = result.Shares.ToDictionary(s => s.RugId);

        var changedRugs = new HashSet<Guid>();
        var removed = new List<Guid>();
        foreach (var old in input.Stored)
        {
            if (fresh.ContainsKey(old.RugId)) continue;
            removed.Add(old.RugId);
            if (old.Amount != 0m) changedRugs.Add(old.RugId);
        }

        var upserts = new List<AllocationShare>();
        foreach (var share in result.Shares)
        {
            if (stored.TryGetValue(share.RugId, out var old))
            {
                if (old.Amount == share.Amount && old.WeightUnits == share.WeightUnits && old.IsPinned == share.IsPinned)
                    continue;
                if (old.Amount != share.Amount) changedRugs.Add(share.RugId);
            }
            else if (share.Amount != 0m)
            {
                changedRugs.Add(share.RugId);
            }

            upserts.Add(share);
        }

        var row = ToStored(result);
        var resultChanged = input.Result is null || !Same(input.Result, row);

        if (removed.Count == 0 && upserts.Count == 0 && !resultChanged)
            return (null, changedRugs);

        return (new EntryWrite(input.EntryId, removed, upserts, resultChanged ? row : null), changedRugs);
    }

    /// <summary>نتیجه به شکل ردیف ذخیره‌شده.</summary>
    public static StoredResult ToStored(AllocationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new StoredResult(result.Lines, result.Pieces, result.AreaM2, result.TotalWeightUnits, result.BaseAmount,
            result.Allocated, result.Unallocated, result.State, result.Warnings);
    }

    /// <summary>دو نتیجه یکی‌اند (هشدارها به ترتیب و با مقدار).</summary>
    public static bool Same(StoredResult a, StoredResult b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.Lines == b.Lines && a.Pieces == b.Pieces && a.AreaM2 == b.AreaM2 && a.TotalWeightUnits == b.TotalWeightUnits
               && a.BaseAmount == b.BaseAmount && a.Allocated == b.Allocated && a.Unallocated == b.Unallocated
               && a.State == b.State && a.Warnings.SequenceEqual(b.Warnings);
    }
}
