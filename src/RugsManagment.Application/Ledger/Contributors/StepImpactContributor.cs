using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Ledger.Contributors;

/// <summary>
/// اثر مرحله‌های فرش (spec D.5 #10): هزینه (محاسبه‌شده، دستی، تعدیل)، وضعیت (فقط «انجام‌شده» و «در حال انجام» در بها
/// شمرده می‌شوند)، افزودن یا برداشتن مرحله → جمع هزینهٔ همان فرش. هشدار <c>W_STEP_DOUBLE</c> (فاز هزینه‌ها) از جمع
/// ثبت‌ها می‌آید. یادداشت، فرم پویا، تاریخ و انجام‌دهنده پولی نیستند.
/// </summary>
public sealed class StepImpactContributor : IChangeImpactContributor
{
    private static readonly string[] CostProperties =
    [
        nameof(RugWorkflowStep.CalculatedCost), nameof(RugWorkflowStep.ManualCostOverride),
        nameof(RugWorkflowStep.Adjustment), nameof(RugWorkflowStep.Status), nameof(RugWorkflowStep.RugId)
    ];

    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(RugWorkflowStep)];

    public Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var impact = context.Impact;
        foreach (var change in context.Changes.Of<RugWorkflowStep>())
        {
            if (change.Kind == ChangeKind.Modified && !change.HasAny(CostProperties))
                continue;

            var step = change.As<RugWorkflowStep>();
            if (step.RugId != Guid.Empty)
                impact.CostTotalRugs.Add(step.RugId);

            if (change.Original<Guid>(nameof(RugWorkflowStep.RugId)) is var oldRug && oldRug != Guid.Empty)
                impact.CostTotalRugs.Add(oldRug);
        }

        return Task.CompletedTask;
    }
}
