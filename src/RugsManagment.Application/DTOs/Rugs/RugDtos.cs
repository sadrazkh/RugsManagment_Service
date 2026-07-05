using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>نمای کامل یک فرش برای API — همان چیزی که Vue مصرف می‌کند</summary>
public record RugDto(
    Guid Id,
    string Sku,
    string? Title,
    string? Origin,
    string? Pattern,
    string? Material,
    int? KnotDensity,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal AreaSquareMeters,
    decimal? PurchaseCost,
    decimal? TargetSalePrice,
    RugStatus Status,
    string? ImageUrl,
    string? Notes,
    Guid? WorkflowTemplateId,
    Guid? BatchId,
    string? BatchName,
    string? CurrentStepNameFa,
    int CurrentStepIndex,
    IReadOnlyList<RugWorkflowStepDto> WorkflowSteps,
    RugCostSummaryDto Costs,
    string? MetadataJson = null);

public record RugWorkflowStepDto(
    Guid Id,
    Guid ProcessStepTypeId,
    string StepCode,
    string StepNameFa,
    string StepNameEn,
    string Icon,
    int OrderIndex,
    bool IsOptional,
    WorkflowStepStatus Status,
    Guid? ServiceProviderId,
    string? ServiceProviderName,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    decimal EffectiveCost,
    decimal? CalculatedCost,
    string? AppliedPricingModel,
    decimal? AppliedUnitRate,
    string? PricingConfigJson,
    string? FieldValuesJson,
    string? Notes,
    decimal? Adjustment = null);

public record RugCostSummaryDto(
    decimal TotalProcessCost,
    decimal PurchaseCost,
    decimal TotalInvestment,
    decimal? TargetSalePrice,
    decimal? EstimatedMargin);

/// <summary>ثبت فرش جدید — یا WorkflowTemplateId یا CustomSteps</summary>
public record CreateRugRequest(
    string? Title,
    string? Origin,
    string? Pattern,
    string? Material,
    int? KnotDensity,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal? PurchaseCost,
    decimal? TargetSalePrice,
    string? ImageUrl,
    string? Notes,
    Guid? WorkflowTemplateId,
    IReadOnlyList<Guid>? SkippedOptionalStepIds,
    IReadOnlyList<CustomRugStepRequest>? CustomSteps,
    string? MetadataJson = null);

public record CustomRugStepRequest(
    Guid ProcessStepTypeId,
    bool IsOptional,
    Guid? ServiceProviderId);

public record UpdateRugRequest(
    string? Title,
    string? Origin,
    string? Pattern,
    string? Material,
    int? KnotDensity,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal? PurchaseCost,
    decimal? TargetSalePrice,
    RugStatus? Status,
    string? ImageUrl,
    string? Notes,
    string? MetadataJson = null);

public record AdvanceRugStepRequest(
    Guid? ServiceProviderId,
    decimal? ManualCostOverride,
    StepPricingModel? PricingModel,
    decimal? UnitRate,
    string? PricingConfigJson,
    string? FieldValuesJson,
    string? Notes,
    bool MarkCompleted = true,
    decimal? Adjustment = null);

public record UpdateRugWorkflowRequest(IReadOnlyList<CustomRugStepRequest> PendingSteps);

public record BulkRugIdsRequest(IReadOnlyList<Guid> RugIds);

public record BulkAdvanceRequest(
    IReadOnlyList<Guid> RugIds,
    AdvanceRugStepRequest Step);

public record BulkUpdateWorkflowRequest(
    IReadOnlyList<Guid> RugIds,
    IReadOnlyList<CustomRugStepRequest> PendingSteps);

public record BulkOperationResultDto(
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<BulkItemErrorDto> Errors);

public record BulkItemErrorDto(Guid RugId, string Message);
