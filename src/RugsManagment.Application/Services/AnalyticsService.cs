using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Analytics;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>گزارش‌های تحلیلی: کهنگی (گلوگاه)، شکست هزینه به تفکیک مرحله، و روند ماهانه.</summary>
public interface IAnalyticsService
{
    Task<AnalyticsReportDto> GetAsync(Guid tenantId, int trendMonths = 12, CancellationToken ct = default);
    Task<AgingReportDto> GetAgingAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class AnalyticsService(IRugRepository rugs, IWorkflowEngine workflowEngine) : IAnalyticsService
{
    /// <summary>آستانه‌های کهنگی بر حسب روز. عمداً ثابت‌اند تا رفتار قابل‌پیش‌بینی بماند؛
    /// در فاز بعد به «مدت انتظار هر نوع مرحله» تبدیل می‌شوند.</summary>
    private const int WarningDays = 7;
    private const int SeriousDays = 14;
    private const int CriticalDays = 30;

    public async Task<AgingReportDto> GetAgingAsync(Guid tenantId, CancellationToken ct = default)
        => BuildAging(await rugs.ListByTenantAsync(tenantId, null, ct));

    public async Task<AnalyticsReportDto> GetAsync(
        Guid tenantId, int trendMonths = 12, CancellationToken ct = default)
    {
        var all = await rugs.ListByTenantAsync(tenantId, null, ct);

        return new AnalyticsReportDto(
            BuildAging(all),
            BuildStepBreakdown(all),
            BuildTrend(all, trendMonths));
    }

    // ─────────────────────────────────────────────────────────
    // کهنگی — کدام فرش‌ها گیر کرده‌اند
    // ─────────────────────────────────────────────────────────
    private static AgingReportDto BuildAging(IReadOnlyList<Rug> all)
    {
        var now = DateTimeOffset.UtcNow;
        var items = new List<AgingItemDto>();

        foreach (var rug in all)
        {
            // فقط فرش‌های در جریان معنا دارند؛ فروخته‌شده و بایگانی گیر نکرده‌اند
            if (rug.Status is RugStatus.Sold or RugStatus.Archived) continue;

            var step = rug.WorkflowSteps.FirstOrDefault(s => s.Status == WorkflowStepStatus.InProgress);
            if (step is null) continue;

            // اگر زمان شروع ثبت نشده، از زمان آخرین تغییر خود فرش تخمین می‌زنیم
            var since = step.StartedAt ?? rug.UpdatedAt ?? rug.CreatedAt;
            var days = (int)Math.Floor((now - since).TotalDays);
            if (days < WarningDays) continue;

            items.Add(new AgingItemDto(
                rug.Id,
                rug.Sku,
                rug.Title,
                step.ProcessStepType?.NameFa ?? "نامشخص",
                days,
                step.StartedAt,
                step.ServiceProvider?.Name,
                Classify(days)));
        }

        items = items.OrderByDescending(i => i.DaysInStep).ToList();

        return new AgingReportDto(
            items,
            items.Count(i => i.Severity == AgingSeverity.Warning),
            items.Count(i => i.Severity == AgingSeverity.Serious),
            items.Count(i => i.Severity == AgingSeverity.Critical));
    }

    private static AgingSeverity Classify(int days) => days switch
    {
        >= CriticalDays => AgingSeverity.Critical,
        >= SeriousDays => AgingSeverity.Serious,
        >= WarningDays => AgingSeverity.Warning,
        _ => AgingSeverity.Normal
    };

    // ─────────────────────────────────────────────────────────
    // شکست هزینه و زمان به تفکیک نوع مرحله
    // ─────────────────────────────────────────────────────────
    private static IReadOnlyList<StepBreakdownDto> BuildStepBreakdown(IReadOnlyList<Rug> all)
    {
        var steps = all.SelectMany(r => r.WorkflowSteps).ToList();

        return steps
            .Where(s => s.ProcessStepType is not null)
            .GroupBy(s => new { s.ProcessStepTypeId, Name = s.ProcessStepType!.NameFa })
            .Select(g =>
            {
                var completed = g.Where(s => s.Status == WorkflowStepStatus.Completed).ToList();
                var totalCost = completed.Sum(s => s.EffectiveCost);

                // میانگین مدت فقط از مراحلی که هر دو زمان را دارند
                var timed = completed
                    .Where(s => s.StartedAt.HasValue && s.CompletedAt.HasValue)
                    .Select(s => (s.CompletedAt!.Value - s.StartedAt!.Value).TotalDays)
                    .ToList();

                return new StepBreakdownDto(
                    g.Key.ProcessStepTypeId,
                    g.Key.Name,
                    completed.Count,
                    totalCost,
                    completed.Count == 0 ? 0 : Math.Round(totalCost / completed.Count, 0),
                    timed.Count == 0 ? null : Math.Round(timed.Average(), 1),
                    g.Count(s => s.Status == WorkflowStepStatus.InProgress));
            })
            .OrderByDescending(s => s.TotalCost)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────
    // روند ماهانه (تقویم شمسی)
    // ─────────────────────────────────────────────────────────
    private IReadOnlyList<TrendPointDto> BuildTrend(IReadOnlyList<Rug> all, int months)
    {
        months = Math.Clamp(months, 3, 36);

        // ستون‌های ماه از قبل ساخته می‌شوند تا ماه‌های بدون فعالیت هم در نمودار خالی دیده شوند
        var buckets = new List<(int Key, string Label, DateTimeOffset Start, DateTimeOffset End)>();
        var cursor = JalaliCalendarHelper.StartOfCurrentJalaliMonthUtc();

        for (var i = months - 1; i >= 0; i--)
        {
            var start = JalaliCalendarHelper.AddJalaliMonths(cursor, -i);
            var end = JalaliCalendarHelper.AddJalaliMonths(cursor, -i + 1);
            buckets.Add((JalaliCalendarHelper.SortKey(start), JalaliCalendarHelper.MonthLabel(start), start, end));
        }

        return buckets.Select(b =>
        {
            var added = all.Count(r => r.CreatedAt >= b.Start && r.CreatedAt < b.End);

            var soldInMonth = all
                .Where(r => r.Sale is not null && r.Sale.SoldAt >= b.Start && r.Sale.SoldAt < b.End)
                .ToList();

            var net = soldInMonth.Sum(r => r.Sale!.NetAmount);
            var profit = soldInMonth.Sum(r => r.Sale!.NetAmount - workflowEngine.CalculateRugCosts(r).TotalInvestment);

            return new TrendPointDto(b.Key, b.Label, added, soldInMonth.Count, net, profit);
        }).ToList();
    }
}
