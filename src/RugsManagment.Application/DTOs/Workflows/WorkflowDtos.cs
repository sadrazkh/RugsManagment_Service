using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Workflows;

/// <summary>نوع مرحله برای UI ساخت قالب و مسیر سفارشی</summary>
public record ProcessStepTypeDto(
    Guid Id,
    string Code,
    string NameFa,
    string NameEn,
    string Icon,
    int SortOrder,
    StepPricingModel DefaultPricingModel,
    decimal DefaultUnitRate,
    string? FieldSchemaJson);

public record WorkflowTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsActive,
    IReadOnlyList<WorkflowTemplateStepDto> Steps);

public record WorkflowTemplateStepDto(
    Guid Id,
    Guid ProcessStepTypeId,
    string StepCode,
    string StepNameFa,
    int OrderIndex,
    bool IsOptional,
    Guid? DefaultServiceProviderId,
    StepPricingModel? OverridePricingModel,
    decimal? OverrideUnitRate);

public record CreateWorkflowTemplateRequest(
    string Name,
    string? Description,
    bool IsDefault,
    IReadOnlyList<CreateWorkflowTemplateStepRequest> Steps);

public record CreateWorkflowTemplateStepRequest(
    Guid ProcessStepTypeId,
    int OrderIndex,
    bool IsOptional,
    Guid? DefaultServiceProviderId,
    StepPricingModel? OverridePricingModel,
    decimal? OverrideUnitRate);

public record UpdateWorkflowTemplateRequest(
    string Name,
    string? Description,
    bool IsDefault,
    bool IsActive,
    IReadOnlyList<CreateWorkflowTemplateStepRequest> Steps);

public record ServiceProviderDto(
    Guid Id,
    string Name,
    string? Specialty,
    string? Phone,
    string? Address,
    bool IsActive,
    IReadOnlyList<string> SupportedStepTypeCodes);

public record CreateServiceProviderRequest(
    string Name,
    string? Specialty,
    string? Phone,
    string? Address,
    IReadOnlyList<string>? SupportedStepTypeCodes);
