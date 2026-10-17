using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Application.Mapping;

namespace RugsManagment.Application.Services;

/// <summary>
/// دادهٔ کمکی برای فرم‌ها (قالب‌ها، انواع مرحله، طرف‌های خدمات) — فقط خواندنی، محدود به کارگاه.
/// نرخ‌ها (پیش‌فرض مرحله، جایگزین قالب) برای غیر Business null است، چون این مسیر به انباردار هم می‌رسد.
/// </summary>
public interface ILookupService
{
    Task<IReadOnlyList<WorkflowTemplateDto>> WorkflowTemplatesAsync(Guid tenantId, CancellationToken ct = default);
    /// <summary>مرحله‌های قابل استفادهٔ یک کارگاه: سیستمی + اختصاصی خودش.</summary>
    Task<IReadOnlyList<ProcessStepTypeDto>> StepTypesAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceProviderDto>> ServiceProvidersAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class LookupService(
    IWorkflowTemplateRepository templates,
    IProcessStepTypeRepository stepTypes,
    IServiceProviderRepository providers,
    IMoneyVisibility money) : ILookupService
{
    public async Task<IReadOnlyList<WorkflowTemplateDto>> WorkflowTemplatesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await templates.ListByTenantAsync(tenantId, ct);
        return money.Redact(list.Select(t => t.ToDto()).ToList());
    }

    public async Task<IReadOnlyList<ProcessStepTypeDto>> StepTypesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await stepTypes.ListForTenantAsync(tenantId, onlyActive: true, ct);
        return list.Select(s => s.ToDto(money)).ToList();
    }

    public async Task<IReadOnlyList<ServiceProviderDto>> ServiceProvidersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await providers.ListByTenantAsync(tenantId, ct);
        return list.Select(p => p.ToDto()).ToList();
    }
}
