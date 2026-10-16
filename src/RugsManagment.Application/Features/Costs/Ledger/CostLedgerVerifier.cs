using System.Globalization;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Ledger;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// «سلامت حساب‌ها» برای دفتر هزینه (spec D.4 «Recovery»، <c>/api/diagnostics/cost-consistency</c>): سنجش فاز ۲ جمع هر فرش را با
/// سهم‌های ذخیره‌شده می‌سنجد، ولی نمی‌تواند ببیند خودِ سهم‌ها کهنه‌اند. این لایه بعد از آن (فقط در سنجش کامل، نه نمونهٔ استارت)
/// زیر قفل کارگاه هر ثبت را با همان <see cref="CostAllocator"/> از نو تقسیم می‌کند و هر سهم یا نتیجه‌ای را که با ذخیره‌شده
/// فرق دارد گزارش می‌کند — همان diffی که مرحله‌ها می‌نوشتند. فهرست خالی یعنی سهم‌ها همان «بازسازی تازه» است.
/// </summary>
public sealed class CostLedgerVerifier(
    ILedgerVerifier inner,
    ILedgerSession session,
    ILedgerRepository ledger,
    ICostLedgerRepository costs) : ILedgerVerifier
{
    /// <summary>کد بررسی سهم یک فرش از یک ثبت.</summary>
    public const string AllocationCheck = "allocation";

    /// <summary>کد بررسی نتیجهٔ یک ثبت (کل، تقسیم‌شده، وضع، هشدارها).</summary>
    public const string EntryResultCheck = "entryResult";

    public async Task<CostConsistencyReportDto> VerifyAsync(Guid tenantId, int? sample = null, CancellationToken cancellationToken = default)
    {
        var report = await inner.VerifyAsync(tenantId, sample, cancellationToken);
        if (sample is not null)
            return report;

        var mismatches = await VerifyEntriesAsync(tenantId, cancellationToken);
        return mismatches.Count == 0 ? report : report with { Mismatches = [.. report.Mismatches, .. mismatches] };
    }

    /// <summary>ناهمخوانی‌های سهم و نتیجهٔ همهٔ ثبت‌های کارگاه (زیر قفل کارگاه).</summary>
    public async Task<IReadOnlyList<CostMismatchDto>> VerifyEntriesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var transaction = await session.BeginAsync(cancellationToken);
        try
        {
            await session.LockTenantAsync(tenantId, cancellationToken);
            var settings = await ledger.GetSettingsAsync(tenantId, cancellationToken);
            var inputs = await costs.LoadAsync(tenantId, null, cancellationToken);
            var mismatches = new List<CostMismatchDto>();
            foreach (var input in inputs)
            {
                var (write, _) = EntryRunner.Plan(input, settings.Currency, settings.MoneyScale);
                if (write is null) continue;

                var detail = "هزینهٔ شمارهٔ " + PersianText.ToPersianDigits(input.Number.ToString(CultureInfo.InvariantCulture));
                var serials = input.Members.ToDictionary(m => m.RugId, m => m.Serial);
                var stored = input.Stored.ToDictionary(s => s.RugId);
                foreach (var rug in write.RemovedRugs)
                {
                    mismatches.Add(new CostMismatchDto(rug, string.Empty, serials.GetValueOrDefault(rug), AllocationCheck,
                        stored.TryGetValue(rug, out var old) ? old.Amount : null, null, detail));
                }

                foreach (var share in write.Upserts)
                {
                    mismatches.Add(new CostMismatchDto(share.RugId, string.Empty, serials.GetValueOrDefault(share.RugId), AllocationCheck,
                        stored.TryGetValue(share.RugId, out var old) ? old.Amount : null, share.Amount, detail));
                }

                if (write.Result is { } result)
                {
                    mismatches.Add(new CostMismatchDto(Guid.Empty, string.Empty, null, EntryResultCheck,
                        input.Result?.Allocated, result.Allocated, detail));
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return mismatches;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
