using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Dashboard;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Services;

/// <summary>مدیریت قالب‌های مسیر، انواع مرحله (فقط خواندن)، و خدمات‌دهندگان</summary>
public interface IWorkflowManagementService
{
    Task<IReadOnlyList<ProcessStepTypeDto>> ListStepTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTemplateDto>> ListTemplatesAsync(Guid tenantId, CancellationToken ct = default);
    Task<WorkflowTemplateDto> CreateTemplateAsync(Guid tenantId, CreateWorkflowTemplateRequest request, CancellationToken ct = default);
    Task<WorkflowTemplateDto> UpdateTemplateAsync(Guid tenantId, Guid id, UpdateWorkflowTemplateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceProviderDto>> ListProvidersAsync(Guid tenantId, CancellationToken ct = default);
    Task<ServiceProviderDto> CreateProviderAsync(Guid tenantId, CreateServiceProviderRequest request, CancellationToken ct = default);
}

public sealed class WorkflowManagementService(
    IProcessStepTypeRepository stepTypes,
    IWorkflowTemplateRepository templates,
    IServiceProviderRepository providers,
    IUnitOfWork unitOfWork) : IWorkflowManagementService
{
    /// <summary>لیست سراسری مراحل (قالیشویی، رفوگری، …) — برای ساخت قالب و مسیر سفارشی</summary>
    public async Task<IReadOnlyList<ProcessStepTypeDto>> ListStepTypesAsync(CancellationToken ct = default)
    {
        var list = await stepTypes.ListAllOrderedAsync(ct);
        return list.Select(t => t.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<WorkflowTemplateDto>> ListTemplatesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await templates.ListByTenantAsync(tenantId, ct);
        return list.Select(t => t.ToDto()).ToList();
    }

    public async Task<WorkflowTemplateDto> CreateTemplateAsync(
        Guid tenantId, CreateWorkflowTemplateRequest request, CancellationToken ct = default)
    {
        // فقط یک قالب می‌تواند پیش‌فرض باشد
        if (request.IsDefault)
            await ClearDefaultTemplateAsync(tenantId, ct);

        var template = new WorkflowTemplate
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            IsDefault = request.IsDefault,
            IsActive = true,
            Steps = request.Steps.Select(s => new WorkflowTemplateStep
            {
                ProcessStepTypeId = s.ProcessStepTypeId,
                OrderIndex = s.OrderIndex,
                IsOptional = s.IsOptional,
                DefaultServiceProviderId = s.DefaultServiceProviderId,
                OverridePricingModel = s.OverridePricingModel,
                OverrideUnitRate = s.OverrideUnitRate
            }).ToList()
        };

        await templates.AddAsync(template, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var loaded = await templates.GetWithStepsAsync(template.Id, tenantId, ct)
            ?? throw new InvalidOperationException("بارگذاری قالب بعد از ایجاد ناموفق بود.");
        return loaded.ToDto();
    }

    public async Task<WorkflowTemplateDto> UpdateTemplateAsync(
        Guid tenantId, Guid id, UpdateWorkflowTemplateRequest request, CancellationToken ct = default)
    {
        var template = await templates.GetWithStepsAsync(id, tenantId, ct)
            ?? throw new KeyNotFoundException("قالب یافت نشد.");

        if (request.IsDefault)
            await ClearDefaultTemplateAsync(tenantId, ct);

        template.Name = request.Name;
        template.Description = request.Description;
        template.IsDefault = request.IsDefault;
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        template.Steps.Clear();

        foreach (var s in request.Steps)
        {
            template.Steps.Add(new WorkflowTemplateStep
            {
                ProcessStepTypeId = s.ProcessStepTypeId,
                OrderIndex = s.OrderIndex,
                IsOptional = s.IsOptional,
                DefaultServiceProviderId = s.DefaultServiceProviderId,
                OverridePricingModel = s.OverridePricingModel,
                OverrideUnitRate = s.OverrideUnitRate
            });
        }

        templates.Update(template);
        await unitOfWork.SaveChangesAsync(ct);

        var loaded = await templates.GetWithStepsAsync(id, tenantId, ct)
            ?? throw new InvalidOperationException("بارگذاری قالب ناموفق بود.");
        return loaded.ToDto();
    }

    public async Task<IReadOnlyList<ServiceProviderDto>> ListProvidersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await providers.ListByTenantAsync(tenantId, ct);
        return list.Select(p => p.ToDto()).ToList();
    }

    public async Task<ServiceProviderDto> CreateProviderAsync(
        Guid tenantId, CreateServiceProviderRequest request, CancellationToken ct = default)
    {
        var provider = new ServiceProvider
        {
            TenantId = tenantId,
            Name = request.Name,
            Specialty = request.Specialty,
            Phone = request.Phone,
            Address = request.Address,
            SupportedStepTypeCodesJson = request.SupportedStepTypeCodes is null
                ? null
                : System.Text.Json.JsonSerializer.Serialize(request.SupportedStepTypeCodes)
        };

        await providers.AddAsync(provider, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return provider.ToDto();
    }

    /// <summary>قبل از تنظیم IsDefault=true روی قالب جدید، بقیه را غیرپیش‌فرض می‌کند</summary>
    private async Task ClearDefaultTemplateAsync(Guid tenantId, CancellationToken ct)
    {
        var existing = await templates.ListByTenantAsync(tenantId, ct);
        foreach (var t in existing.Where(t => t.IsDefault))
        {
            t.IsDefault = false;
            templates.Update(t);
        }
    }
}

/// <summary>آمار کارت‌های داشبورد — تجمیع روی همهٔ فرش‌های یک کارگاه</summary>
public interface IDashboardService
{
    Task<DashboardStatsDto> GetTenantDashboardAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class DashboardService(
    IRugRepository rugs,
    IWorkflowEngine workflowEngine) : IDashboardService
{
    public async Task<DashboardStatsDto> GetTenantDashboardAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await rugs.ListByTenantAsync(tenantId, null, ct);
        return EntityMappers.ToDashboard(list, workflowEngine);
    }
}
