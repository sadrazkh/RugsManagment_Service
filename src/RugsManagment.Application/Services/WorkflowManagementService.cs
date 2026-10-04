using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
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

/// <remarks>
/// نرخ‌ها پول‌اند: نرخ پیش‌فرض نوع مرحله و نرخ جایگزین مرحله‌های قالب در فهرست‌ها برای غیر Business null
/// می‌شوند. پاسخِ نوشتن (ساخت/ویرایش قالب) فقط به تنظیم‌کننده می‌رسد و دست نمی‌خورد.
/// </remarks>
public sealed class WorkflowManagementService(
    IProcessStepTypeRepository stepTypes,
    IWorkflowTemplateRepository templates,
    IServiceProviderRepository providers,
    IRepository<WorkflowTemplateStep> templateSteps,
    IUnitOfWork unitOfWork,
    IMoneyVisibility money) : IWorkflowManagementService
{
    /// <summary>لیست سراسری مراحل (قالیشویی، رفوگری، …) — برای ساخت قالب و مسیر سفارشی</summary>
    public async Task<IReadOnlyList<ProcessStepTypeDto>> ListStepTypesAsync(CancellationToken ct = default)
    {
        var list = await stepTypes.ListAllOrderedAsync(ct);
        return list.Select(t => t.ToDto(money)).ToList();
    }

    public async Task<IReadOnlyList<WorkflowTemplateDto>> ListTemplatesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await templates.ListByTenantAsync(tenantId, ct);
        return money.Redact(list.Select(t => t.ToDto()).ToList());
    }

    public async Task<WorkflowTemplateDto> CreateTemplateAsync(
        Guid tenantId, CreateWorkflowTemplateRequest request, CancellationToken ct = default)
    {
        await EnsureTemplateStepsOwnedAsync(tenantId, request.Steps, ct);

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

        await EnsureTemplateStepsOwnedAsync(tenantId, request.Steps, ct);

        if (request.IsDefault)
            await ClearDefaultTemplateAsync(tenantId, ct);

        template.Name = request.Name;
        template.Description = request.Description;
        template.IsDefault = request.IsDefault;
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        // مراحل قدیمی را صریح حذف و مراحل جدید را صریح Add می‌کنیم.
        // چون Id در BaseEntity از پیش ست می‌شود، افزودن به navigation باعث می‌شود EF
        // آن‌ها را «موجود/Modified» بپندارد؛ AddAsync وضعیت را قطعی Added می‌کند.
        foreach (var old in template.Steps.ToList())
            templateSteps.Remove(old);

        foreach (var s in request.Steps)
        {
            await templateSteps.AddAsync(new WorkflowTemplateStep
            {
                WorkflowTemplateId = template.Id,
                ProcessStepTypeId = s.ProcessStepTypeId,
                OrderIndex = s.OrderIndex,
                IsOptional = s.IsOptional,
                DefaultServiceProviderId = s.DefaultServiceProviderId,
                OverridePricingModel = s.OverridePricingModel,
                OverrideUnitRate = s.OverrideUnitRate
            }, ct);
        }

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
        // نرخ‌ها اینجا ست نمی‌شوند؛ در صفحهٔ «طرف‌های خدمات» مدیریت می‌شوند
        var provider = new ServiceProvider
        {
            TenantId = tenantId,
            Name = request.Name,
            Specialty = request.Specialty,
            Phone = request.Phone,
            Address = request.Address
        };

        await providers.AddAsync(provider, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return provider.ToDto();
    }

    /// <summary>
    /// هر نوع مرحلهٔ قالب باید سیستمی (TenantId = null) یا مال همین کارگاه باشد و هر انجام‌دهندهٔ پیش‌فرض مال
    /// همین کارگاه؛ وگرنه «یافت نشد» (۴۰۴) — شناسهٔ کارگاه دیگر درست مثل شناسهٔ ناموجود.
    ///
    /// چرا اینجا: مسیر قالب (ساخت فرش از قالب و «اعمال قالب») این شناسه‌ها را بی‌واسطه روی مراحل فرش کپی می‌کند
    /// و از بررسی‌های مسیر دستی (ProcessStepTypeLookup و بررسی مالکیت انجام‌دهنده) رد نمی‌شود. بدون این بررسی،
    /// مدیر کارگاه A با دانستن شناسه‌ای از کارگاه B نوع مرحلهٔ اختصاصیِ B را روی فرش‌های A می‌نشاند و کارِ A به
    /// حساب و ماندهٔ انجام‌دهندهٔ B اضافه می‌شد.
    /// </summary>
    private async Task EnsureTemplateStepsOwnedAsync(
        Guid tenantId, IReadOnlyList<CreateWorkflowTemplateStepRequest> steps, CancellationToken ct)
    {
        var stepTypeIds = steps.Select(s => s.ProcessStepTypeId).Distinct().ToList();
        if (stepTypeIds.Count > 0)
        {
            var visible = await stepTypes.ListAsync(
                t => stepTypeIds.Contains(t.Id) && (t.TenantId == null || t.TenantId == tenantId), ct);
            if (visible.Count != stepTypeIds.Count)
                throw new KeyNotFoundException("نوع مرحله یافت نشد.");
        }

        var providerIds = steps
            .Where(s => s.DefaultServiceProviderId.HasValue)
            .Select(s => s.DefaultServiceProviderId!.Value)
            .Distinct()
            .ToList();
        if (providerIds.Count > 0)
        {
            var owned = await providers.ListAsync(p => providerIds.Contains(p.Id) && p.TenantId == tenantId, ct);
            if (owned.Count != providerIds.Count)
                throw new KeyNotFoundException("طرف خدمات یافت نشد.");
        }
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

/// <summary>کارت‌های مالی داشبورد برای غیر Business null است؛ شمارنده‌ها برای همه.</summary>
public sealed class DashboardService(
    IRugRepository rugs,
    IWorkflowEngine workflowEngine,
    IMoneyVisibility money) : IDashboardService
{
    public async Task<DashboardStatsDto> GetTenantDashboardAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await rugs.ListByTenantAsync(tenantId, null, ct);
        return EntityMappers.ToDashboard(list, workflowEngine, money);
    }
}
