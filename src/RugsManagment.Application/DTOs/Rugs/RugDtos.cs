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
    string? MetadataJson = null,
    /// <summary>گالری عکس‌ها به ترتیب نمایش؛ خالی یعنی هنوز عکسی آپلود نشده</summary>
    IReadOnlyList<RugImageDto>? Images = null);

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
    decimal? Adjustment = null,
    /// <summary>کاربری که این مرحله را تکمیل کرد — «چه کسی این کار را انجام داد»</summary>
    string? CompletedByName = null);

public record RugCostSummaryDto(
    decimal TotalProcessCost,
    decimal PurchaseCost,
    decimal TotalInvestment,
    decimal? TargetSalePrice,
    /// <summary>سود تخمینی بر اساس «قیمت هدف» — تا وقتی فرش فروخته نشده</summary>
    decimal? EstimatedMargin,
    /// <summary>مبلغ خالص فروش واقعی؛ null یعنی هنوز فروخته نشده</summary>
    decimal? ActualSaleAmount = null,
    /// <summary>سود واقعی = فروش خالص − سرمایه‌گذاری کل؛ null یعنی هنوز فروخته نشده</summary>
    decimal? ActualProfit = null);

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

public record ApplyTemplateRequest(Guid TemplateId, IReadOnlyList<Guid>? SkippedOptionalStepIds);

public record BulkRugIdsRequest(IReadOnlyList<Guid> RugIds);

/// <summary>
/// ویرایش گروهی مشخصات چند فرش.
///
/// هر فیلد null یعنی «دست نزن» — این تفاوت با «خالی کن» عمدی است، وگرنه
/// کاربری که فقط می‌خواهد اصالت را عوض کند ناخواسته بقیهٔ فیلدها را پاک می‌کرد.
/// </summary>
public record BulkUpdateFieldsRequest(
    IReadOnlyList<Guid> RugIds,
    string? Origin,
    string? Pattern,
    string? Material,
    int? KnotDensity,
    decimal? TargetSalePrice,
    RugStatus? Status,
    /// <summary>Guid.Empty یعنی «از گروه خارج کن»</summary>
    Guid? BatchId);

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
