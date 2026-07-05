using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>
/// موتور گردش کار — قلب منطق «فرش از کدام مراحل رد می‌شود و چه زمانی مرحله بعد فعال می‌شود».
/// این کلاس به دیتابیس وصل نیست؛ فقط روی موجودیت Rug در حافظه کار می‌کند.
/// سرویس RugManagementService بعد از فراخوانی اینجا، تغییرات را SaveChanges می‌کند.
/// </summary>
public sealed class WorkflowEngine(
    ICostCalculationService costCalculation,
    IProcessStepTypeLookup stepTypes) : IWorkflowEngine
{
    /// <summary>
    /// ساخت مسیر فرش از روی یک قالب آماده.
    /// مراحل اختیاری که کاربر در skippedOptionalStepIds فرستاده باشد، اصلاً به فرش اضافه نمی‌شوند.
    /// </summary>
    public async Task<Rug> InitializeWorkflowFromTemplateAsync(
        Rug rug,
        WorkflowTemplate template,
        IReadOnlyList<Guid>? skippedOptionalStepIds,
        CancellationToken cancellationToken = default)
    {
        var skipped = skippedOptionalStepIds?.ToHashSet() ?? [];
        rug.WorkflowSteps.Clear();
        rug.WorkflowTemplateId = template.Id;

        var ordered = template.Steps.OrderBy(s => s.OrderIndex).ToList();
        var index = 0;
        foreach (var templateStep in ordered)
        {
            // مرحله اختیاری + کاربر خواسته رد شود → در مسیر این فرش نیست
            if (templateStep.IsOptional && skipped.Contains(templateStep.Id))
                continue;

            rug.WorkflowSteps.Add(MapTemplateStep(rug.Id, templateStep, index++));
        }

        rug.Status = RugStatus.InProgress;
        rug.CurrentStepIndex = 0;
        ActivateCurrentStep(rug);
        await Task.CompletedTask;
        return rug;
    }

    /// <summary>
    /// مسیر کاملاً دستی: کاربر خودش ترتیب انواع مرحله را انتخاب کرده (بدون قالب).
    /// </summary>
    public async Task<Rug> BuildCustomWorkflowAsync(
        Rug rug,
        IReadOnlyList<CustomWorkflowStepRequest> steps,
        CancellationToken cancellationToken = default)
    {
        rug.WorkflowSteps.Clear();
        rug.WorkflowTemplateId = null;

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var stepType = await stepTypes.GetRequiredAsync(step.ProcessStepTypeId, cancellationToken);
            var instance = new RugWorkflowStep
            {
                RugId = rug.Id,
                ProcessStepTypeId = stepType.Id,
                OrderIndex = index,
                IsOptional = step.IsOptional,
                ServiceProviderId = step.ServiceProviderId,
                Status = WorkflowStepStatus.Pending
            };
            ApplyPricing(instance, rug, stepType, step.Pricing);
            rug.WorkflowSteps.Add(instance);
        }

        rug.Status = RugStatus.InProgress;
        rug.CurrentStepIndex = 0;
        ActivateCurrentStep(rug);
        return rug;
    }

    /// <summary>
    /// پیش بردن مرحله: ثبت یادداشت، هزینه دستی، تکمیل یا فقط «در حال انجام».
    /// اگر MarkCompleted=true باشد، مرحله بعدی خودکار فعال می‌شود.
    /// </summary>
    public Task<RugWorkflowStep> AdvanceStepAsync(
        Rug rug,
        Guid stepId,
        AdvanceStepRequest request,
        CancellationToken cancellationToken = default)
    {
        var step = GetActiveOrPendingStep(rug, stepId);
        if (request.ServiceProviderId.HasValue)
            step.ServiceProviderId = request.ServiceProviderId;

        step.FieldValuesJson = request.FieldValuesJson ?? step.FieldValuesJson;
        step.Notes = request.Notes ?? step.Notes;
        ApplyStepPricing(step, request);

        step.CalculatedCost = costCalculation.CalculateStepCost(
            rug,
            step.ProcessStepType!,
            null,
            null,
            step.ManualCostOverride,
            step);

        if (request.MarkCompleted)
        {
            step.Status = WorkflowStepStatus.Completed;
            step.CompletedAt = DateTimeOffset.UtcNow;
            MoveToNextStep(rug);
        }
        else
        {
            step.Status = WorkflowStepStatus.InProgress;
            step.StartedAt ??= DateTimeOffset.UtcNow;
        }


        return Task.FromResult(step);
    }

    public Task<RugWorkflowStep> UpdateStepPricingAsync(
        Rug rug,
        Guid stepId,
        AdvanceStepRequest request,
        CancellationToken cancellationToken = default)
    {
        var step = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException("مرحله یافت نشد.");
        if (step.Status is WorkflowStepStatus.Cancelled)
            throw new InvalidOperationException("مرحله لغو شده قابل ویرایش نیست.");

        if (request.ServiceProviderId.HasValue)
            step.ServiceProviderId = request.ServiceProviderId;
        if (request.Notes is not null)
            step.Notes = request.Notes;
        ApplyStepPricing(step, request);

        step.CalculatedCost = costCalculation.CalculateStepCost(
            rug,
            step.ProcessStepType!,
            null,
            null,
            step.ManualCostOverride,
            step);

        return Task.FromResult(step);
    }

    /// <summary>
    /// رد کردن مرحله — فقط وقتی IsOptional=true مجاز است (مثلاً دارکشی).
    /// </summary>
    public Task<RugWorkflowStep> SkipStepAsync(Rug rug, Guid stepId, CancellationToken cancellationToken = default)
    {
        var step = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException("مرحله یافت نشد.");

        if (!step.IsOptional)
            throw new InvalidOperationException("فقط مراحل اختیاری قابل رد کردن هستند.");

        step.Status = WorkflowStepStatus.Skipped;
        step.CompletedAt = DateTimeOffset.UtcNow;
        MoveToNextStep(rug);
        return Task.FromResult(step);
    }

    public Task<Rug> GoBackStepAsync(Rug rug, CancellationToken cancellationToken = default)
    {
        var ordered = rug.WorkflowSteps.OrderBy(s => s.OrderIndex).ToList();
        var current = ordered.FirstOrDefault(s => s.Status == WorkflowStepStatus.InProgress);

        if (current is not null)
        {
            current.Status = WorkflowStepStatus.Pending;
            current.StartedAt = null;

            var previous = ordered
                .Where(s => s.OrderIndex < current.OrderIndex && s.Status == WorkflowStepStatus.Completed)
                .OrderByDescending(s => s.OrderIndex)
                .FirstOrDefault();

            if (previous is not null)
            {
                previous.Status = WorkflowStepStatus.InProgress;
                previous.CompletedAt = null;
                rug.CurrentStepIndex = previous.OrderIndex;
            }
            else
                ActivateCurrentStep(rug);
        }
        else
        {
            var lastCompleted = ordered.LastOrDefault(s => s.Status == WorkflowStepStatus.Completed);
            if (lastCompleted is null)
                throw new InvalidOperationException("مرحله‌ای برای بازگشت وجود ندارد.");

            foreach (var s in ordered.Where(x => x.OrderIndex > lastCompleted.OrderIndex && x.Status != WorkflowStepStatus.Skipped))
            {
                s.Status = WorkflowStepStatus.Pending;
                s.StartedAt = null;
                s.CompletedAt = null;
            }

            lastCompleted.Status = WorkflowStepStatus.InProgress;
            lastCompleted.CompletedAt = null;
            rug.CurrentStepIndex = lastCompleted.OrderIndex;
        }

        rug.Status = RugStatus.InProgress;
        return Task.FromResult(rug);
    }

    public Task<Rug> ActivateStepAsync(Rug rug, Guid stepId, CancellationToken cancellationToken = default)
    {
        var target = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException("مرحله یافت نشد.");

        if (target.Status == WorkflowStepStatus.Skipped)
            throw new InvalidOperationException("مرحله رد شده قابل فعال‌سازی نیست.");

        foreach (var s in rug.WorkflowSteps.OrderBy(x => x.OrderIndex))
        {
            if (s.Status == WorkflowStepStatus.Skipped)
                continue;

            if (s.OrderIndex < target.OrderIndex)
            {
                s.Status = WorkflowStepStatus.Completed;
                s.CompletedAt ??= DateTimeOffset.UtcNow;
            }
            else if (s.OrderIndex == target.OrderIndex)
            {
                s.Status = WorkflowStepStatus.InProgress;
                s.StartedAt = DateTimeOffset.UtcNow;
                s.CompletedAt = null;
            }
            else
            {
                s.Status = WorkflowStepStatus.Pending;
                s.StartedAt = null;
                s.CompletedAt = null;
            }
        }

        rug.CurrentStepIndex = target.OrderIndex;
        rug.Status = RugStatus.InProgress;
        return Task.FromResult(rug);
    }

    public async Task<Rug> UpdateWorkflowPathAsync(
        Rug rug,
        IReadOnlyList<CustomWorkflowStepRequest> pendingSteps,
        CancellationToken cancellationToken = default)
    {
        var locked = rug.WorkflowSteps
            .Where(s => s.Status is WorkflowStepStatus.Completed or WorkflowStepStatus.Skipped)
            .OrderBy(s => s.OrderIndex)
            .ToList();

        var removable = rug.WorkflowSteps
            .Where(s => s.Status is WorkflowStepStatus.Pending or WorkflowStepStatus.InProgress)
            .ToList();
        foreach (var s in removable)
            rug.WorkflowSteps.Remove(s);

        var startIndex = locked.Count;
        for (var i = 0; i < pendingSteps.Count; i++)
        {
            var input = pendingSteps[i];
            var stepType = await stepTypes.GetRequiredAsync(input.ProcessStepTypeId, cancellationToken);
            var instance = new RugWorkflowStep
            {
                RugId = rug.Id,
                ProcessStepTypeId = stepType.Id,
                OrderIndex = startIndex + i,
                IsOptional = input.IsOptional,
                ServiceProviderId = input.ServiceProviderId,
                Status = WorkflowStepStatus.Pending,
                ProcessStepType = stepType
            };
            ApplyPricing(instance, rug, stepType, input.Pricing);
            rug.WorkflowSteps.Add(instance);
        }

        rug.WorkflowTemplateId = null;
        if (!rug.WorkflowSteps.Any(s => s.Status == WorkflowStepStatus.InProgress))
            ActivateCurrentStep(rug);

        return rug;
    }

    /// <summary>
    /// جمع هزینه فرایند + خرید + حاشیه سود تخمینی برای نمایش در UI.
    /// </summary>
    public RugCostSummary CalculateRugCosts(Rug rug)
    {
        var processCost = rug.WorkflowSteps
            .Where(s => s.Status is WorkflowStepStatus.Completed or WorkflowStepStatus.InProgress)
            .Sum(s => s.EffectiveCost);

        var purchase = rug.PurchaseCost ?? 0;
        var total = processCost + purchase;
        decimal? margin = rug.TargetSalePrice.HasValue
            ? rug.TargetSalePrice.Value - total
            : null;

        return new RugCostSummary(processCost, purchase, total, rug.TargetSalePrice, margin);
    }

    /// <summary>تبدیل یک ردیف قالب به یک ردیف مرحله روی فرش</summary>
    private static RugWorkflowStep MapTemplateStep(Guid rugId, WorkflowTemplateStep templateStep, int orderIndex)
    {
        return new RugWorkflowStep
        {
            RugId = rugId,
            ProcessStepTypeId = templateStep.ProcessStepTypeId,
            OrderIndex = orderIndex,
            IsOptional = templateStep.IsOptional,
            ServiceProviderId = templateStep.DefaultServiceProviderId,
            Status = WorkflowStepStatus.Pending
        };
    }

    private void ApplyPricing(RugWorkflowStep instance, Rug rug, ProcessStepType stepType, StepPricingOverride? pricing)
    {
        instance.CalculatedCost = costCalculation.CalculateStepCost(
            rug,
            stepType,
            pricing?.Model,
            pricing?.UnitRate,
            null);
    }

    private void RecalculateStepCost(Rug rug, RugWorkflowStep step)
    {
        if (step.ProcessStepType is null)
            return;

        step.CalculatedCost = costCalculation.CalculateStepCost(
            rug,
            step.ProcessStepType,
            null,
            null,
            step.ManualCostOverride,
            step);
    }

    private static void ApplyStepPricing(RugWorkflowStep step, AdvanceStepRequest request)
    {
        if (request.PricingModel.HasValue)
            step.AppliedPricingModel = request.PricingModel;
        if (request.UnitRate.HasValue)
            step.AppliedUnitRate = request.UnitRate;
        if (request.PricingConfigJson is not null)
            step.PricingConfigJson = string.IsNullOrWhiteSpace(request.PricingConfigJson) ? null : request.PricingConfigJson;
        if (request.ManualCostOverride.HasValue)
            step.ManualCostOverride = request.ManualCostOverride;
        else if (request.ManualCostOverride is null && request.PricingModel.HasValue)
            step.ManualCostOverride = null;

        if (request.Adjustment.HasValue)
            step.Adjustment = request.Adjustment;
    }

    /// <summary>اولین مرحله Pending را InProgress می‌کند؛ اگر مرحله‌ای نماند → آماده فروش</summary>
    private static void ActivateCurrentStep(Rug rug)
    {
        var current = rug.WorkflowSteps
            .OrderBy(s => s.OrderIndex)
            .FirstOrDefault(s => s.Status == WorkflowStepStatus.Pending);

        if (current is null)
        {
            rug.Status = RugStatus.ReadyForSale;
            return;
        }

        current.Status = WorkflowStepStatus.InProgress;
        current.StartedAt = DateTimeOffset.UtcNow;
        rug.CurrentStepIndex = current.OrderIndex;
    }

    /// <summary>بعد از تکمیل/رد مرحله، نوبت بعدی را فعال می‌کند</summary>
    private static void MoveToNextStep(Rug rug)
    {
        var next = rug.WorkflowSteps
            .OrderBy(s => s.OrderIndex)
            .FirstOrDefault(s => s.Status == WorkflowStepStatus.Pending);

        if (next is null)
        {
            rug.Status = RugStatus.ReadyForSale;
            rug.CurrentStepIndex = rug.WorkflowSteps.Count;
            return;
        }

        next.Status = WorkflowStepStatus.InProgress;
        next.StartedAt = DateTimeOffset.UtcNow;
        rug.CurrentStepIndex = next.OrderIndex;
        rug.Status = RugStatus.InProgress;
    }

    private static RugWorkflowStep GetActiveOrPendingStep(Rug rug, Guid stepId)
    {
        var step = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException("مرحله یافت نشد.");

        if (step.Status is WorkflowStepStatus.Completed or WorkflowStepStatus.Skipped)
            throw new InvalidOperationException("این مرحله قبلاً بسته شده است.");

        return step;
    }
}

/// <summary>واسط بارگذاری نوع مرحله از دیتابیس — پیاده‌سازی در Infrastructure</summary>
public interface IProcessStepTypeLookup
{
    Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken cancellationToken = default);
}
