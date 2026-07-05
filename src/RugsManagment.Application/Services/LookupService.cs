using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Application.Mapping;

namespace RugsManagment.Application.Services;

/// <summary>
/// دادهٔ کمکی برای فرم‌ها (قالب‌ها، انواع مرحله، طرف‌های خدمات) — فقط خواندنی، محدود به کارگاه.
/// </summary>
public interface ILookupService
{
    Task<IReadOnlyList<WorkflowTemplateDto>> WorkflowTemplatesAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<ProcessStepTypeDto>> StepTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ServiceProviderDto>> ServiceProvidersAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class LookupService(
    IWorkflowTemplateRepository templates,
    IProcessStepTypeRepository stepTypes,
    IServiceProviderRepository providers) : ILookupService
{
    public async Task<IReadOnlyList<WorkflowTemplateDto>> WorkflowTemplatesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await templates.ListByTenantAsync(tenantId, ct);
        return list.Select(t => t.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<ProcessStepTypeDto>> StepTypesAsync(CancellationToken ct = default)
    {
        var list = await stepTypes.ListAllOrderedAsync(ct);
        return list.Select(s => s.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<ServiceProviderDto>> ServiceProvidersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await providers.ListByTenantAsync(tenantId, ct);
        return list.Select(p => p.ToDto()).ToList();
    }
}
