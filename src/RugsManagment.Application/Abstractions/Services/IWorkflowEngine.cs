using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>
/// قرارداد موتور گردش کار — منطق «کدام مرحله فعال است و چطور جلو می‌رود» بدون وابستگی به EF.
/// </summary>
public interface IWorkflowEngine
{
    Task<Rug> InitializeWorkflowFromTemplateAsync(
        Rug rug,
        WorkflowTemplate template,
        IReadOnlyList<Guid>? skippedOptionalStepIds,
        CancellationToken cancellationToken = default);

    Task<Rug> BuildCustomWorkflowAsync(
        Rug rug,
        IReadOnlyList<CustomWorkflowStepRequest> steps,
        CancellationToken cancellationToken = default);

    Task<RugWorkflowStep> AdvanceStepAsync(
        Rug rug,
        Guid stepId,
        AdvanceStepRequest request,
        CancellationToken cancellationToken = default);

    Task<RugWorkflowStep> UpdateStepPricingAsync(
        Rug rug,
        Guid stepId,
        AdvanceStepRequest request,
        CancellationToken cancellationToken = default);

    Task<RugWorkflowStep> SkipStepAsync(Rug rug, Guid stepId, CancellationToken cancellationToken = default);

    Task<Rug> GoBackStepAsync(Rug rug, CancellationToken cancellationToken = default);

    Task<Rug> ActivateStepAsync(Rug rug, Guid stepId, CancellationToken cancellationToken = default);

    /// <summary>مراحل تکمیل‌شده/ردشده حفظ می‌شود؛ بقیه با لیست جدید جایگزین می‌شود</summary>
    Task<Rug> UpdateWorkflowPathAsync(
        Rug rug,
        IReadOnlyList<CustomWorkflowStepRequest> pendingSteps,
        CancellationToken cancellationToken = default);

    RugCostSummary CalculateRugCosts(Rug rug);
}

/// <summary>یک مرحله در مسیر سفارشی هنگام ثبت فرش</summary>
public record CustomWorkflowStepRequest(
    Guid ProcessStepTypeId,
    bool IsOptional,
    Guid? ServiceProviderId,
    StepPricingOverride? Pricing);

/// <summary>جایگزینی نرخ پیش‌فرض نوع مرحله</summary>
public record StepPricingOverride(
    Domain.Enums.StepPricingModel? Model,
    decimal? UnitRate);

/// <summary>دادهٔ ارسالی هنگام تکمیل مرحله از UI</summary>
public record AdvanceStepRequest(
    Guid? ServiceProviderId,
    decimal? ManualCostOverride,
    Domain.Enums.StepPricingModel? PricingModel,
    decimal? UnitRate,
    string? PricingConfigJson,
    string? FieldValuesJson,
    string? Notes,
    bool MarkCompleted = true,
    decimal? Adjustment = null);

/// <summary>خلاصهٔ مالی یک فرش برای نمایش</summary>
public record RugCostSummary(
    decimal TotalProcessCost,
    decimal PurchaseCost,
    decimal TotalInvestment,
    decimal? TargetSalePrice,
    decimal? EstimatedMargin);
